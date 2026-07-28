#nullable enable
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace VenueLink.VLAgent.Unity
{
    public sealed class VLAgentClient : IDisposable
    {
        private readonly AgentConfig _config;
        private readonly SemaphoreSlim _lifecycleGate = new SemaphoreSlim(1, 1);
        private readonly object _reconnectGate = new object();
        private readonly object _stateGate = new object();

        private IMqttClient? _client;
        private CancellationTokenSource? _lifetimeCts;
        private Task? _heartbeatTask;
        private Task? _reconnectTask;
        private HostInfoSnapshot? _hostSnapshot;
        private AgentState? _reportedState;
        private bool _started;
        private bool _stopping;
        private bool _disposed;

        public VLAgentClient(AgentConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public bool IsConnected
        {
            get { return _client != null && _client.IsConnected; }
        }

        public HostInfoSnapshot? LastHostInfo
        {
            get { return _hostSnapshot; }
        }

        public event Action<bool>? ConnectionChanged;
        public event Action<Exception>? BackgroundError;

        public async Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();
            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_started) return;

                _config.Validate();
                _hostSnapshot = HostInfo.Capture(_config.brokerHost, _config.brokerPort);

                _started = true;
                _stopping = false;
                _lifetimeCts = new CancellationTokenSource();

                var factory = new MqttFactory();
                _client = factory.CreateMqttClient();
                _client.ConnectedAsync += OnConnectedAsync;
                _client.DisconnectedAsync += OnDisconnectedAsync;
                _heartbeatTask = HeartbeatLoopAsync(_lifetimeCts.Token);

                try
                {
                    await _client.ConnectAsync(BuildOptions(), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _lifetimeCts.Cancel();
                    await AwaitBackgroundTasksAsync().ConfigureAwait(false);
                    await ResetClientAsync().ConfigureAwait(false);
                    throw;
                }
                catch (Exception ex)
                {
                    BackgroundError?.Invoke(ex);
                    EnsureReconnectLoop();
                }
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_started) return;

                _stopping = true;
                if (_lifetimeCts != null)
                    _lifetimeCts.Cancel();

                try
                {
                    if (_client != null && _client.IsConnected)
                    {
                        await PublishStatusAsync(
                            online: false,
                            reason: "graceful",
                            qos: MqttQualityOfServiceLevel.AtLeastOnce,
                            cancellationToken: cancellationToken).ConfigureAwait(false);

                        await _client.DisconnectAsync(
                            new MqttClientDisconnectOptions(),
                            cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    if (ex is not OperationCanceledException)
                        BackgroundError?.Invoke(ex);
                }

                await AwaitBackgroundTasksAsync().ConfigureAwait(false);
                await ResetClientAsync().ConfigureAwait(false);
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        public async Task ReportRegisterAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();
            _config.Validate();
            _hostSnapshot = HostInfo.Capture(_config.brokerHost, _config.brokerPort);
            await PublishRegisterAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 上报业务运行态（进度/音量等）。写入内存并立即发 retained online status；
        /// 未连接时仅缓存，待连接后首包/心跳带上。
        /// </summary>
        public Task ReportStateAsync(AgentState state, CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();
            if (state == null) throw new ArgumentNullException(nameof(state));

            lock (_stateGate)
            {
                _reportedState = CloneState(state);
            }

            return PublishStatusAsync(
                online: true,
                reason: null,
                qos: MqttQualityOfServiceLevel.AtMostOnce,
                cancellationToken: cancellationToken);
        }

        /// <summary>清空业务 state（仍保留 runtime=unity），并立即发一帧 online status。</summary>
        public Task ClearStateAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();
            lock (_stateGate)
            {
                _reportedState = null;
            }

            return PublishStatusAsync(
                online: true,
                reason: null,
                qos: MqttQualityOfServiceLevel.AtMostOnce,
                cancellationToken: cancellationToken);
        }

        private MqttClientOptions BuildOptions()
        {
            var statusTopic = GetStatusTopic();
            var lwtPayload = StatusPayload.CreateOffline(_config.deviceId, "lwt", useServerTimestamp: true);

            var identity = "agent-" + _config.deviceId;
            return new MqttClientOptionsBuilder()
                .WithTcpServer(_config.brokerHost, _config.brokerPort)
                .WithClientId(identity)
                .WithCredentials(identity, string.Empty)
                .WithCleanSession(true)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(5))
                .WithTimeout(TimeSpan.FromSeconds(10))
                .WithWillTopic(statusTopic)
                .WithWillPayload(Encoding.UTF8.GetBytes(lwtPayload))
                .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithWillRetain(true)
                .Build();
        }

        private async Task OnConnectedAsync(MqttClientConnectedEventArgs args)
        {
            ConnectionChanged?.Invoke(true);

            var token = _lifetimeCts != null ? _lifetimeCts.Token : CancellationToken.None;
            try
            {
                if (_hostSnapshot == null)
                    _hostSnapshot = HostInfo.Capture(_config.brokerHost, _config.brokerPort);

                await PublishRegisterAsync(token).ConfigureAwait(false);
                await PublishStatusAsync(
                    online: true,
                    reason: null,
                    qos: MqttQualityOfServiceLevel.AtMostOnce,
                    cancellationToken: token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                    BackgroundError?.Invoke(ex);
            }
        }

        private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
        {
            ConnectionChanged?.Invoke(false);
            if (!_stopping && _started && _lifetimeCts != null && !_lifetimeCts.IsCancellationRequested)
                EnsureReconnectLoop();
            return Task.CompletedTask;
        }

        private void EnsureReconnectLoop()
        {
            var lifetimeCts = _lifetimeCts;
            if (lifetimeCts == null) return;

            lock (_reconnectGate)
            {
                if (_reconnectTask != null && !_reconnectTask.IsCompleted) return;
                _reconnectTask = ReconnectLoopAsync(lifetimeCts.Token);
            }
        }

        private async Task ReconnectLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !_stopping)
            {
                try
                {
                    await Task.Delay(_config.reconnectDelayMs, cancellationToken).ConfigureAwait(false);
                    if (_client != null && !_client.IsConnected)
                    {
                        _hostSnapshot = HostInfo.Capture(_config.brokerHost, _config.brokerPort);
                        await _client.ConnectAsync(BuildOptions(), cancellationToken).ConfigureAwait(false);
                    }
                    if (_client != null && _client.IsConnected) return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    BackgroundError?.Invoke(ex);
                }
            }
        }

        private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(_config.heartbeatIntervalMs, cancellationToken).ConfigureAwait(false);
                    if (_client != null && _client.IsConnected)
                    {
                        await PublishStatusAsync(
                            online: true,
                            reason: null,
                            qos: MqttQualityOfServiceLevel.AtMostOnce,
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                BackgroundError?.Invoke(ex);
            }
        }

        private Task PublishRegisterAsync(CancellationToken cancellationToken)
        {
            var client = _client;
            var host = _hostSnapshot;
            if (client == null || !client.IsConnected || host == null) return Task.CompletedTask;

            var payload = RegisterPayload.Create(_config, host);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(GetRegisterTopic())
                .WithPayload(Encoding.UTF8.GetBytes(payload))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(false)
                .Build();

            return client.PublishAsync(message, cancellationToken);
        }

        private Task PublishStatusAsync(
            bool online,
            string? reason,
            MqttQualityOfServiceLevel qos,
            CancellationToken cancellationToken)
        {
            var client = _client;
            if (client == null || !client.IsConnected) return Task.CompletedTask;

            string payload;
            if (online)
            {
                AgentState? state;
                lock (_stateGate)
                {
                    state = _reportedState;
                }

                payload = StatusPayload.CreateOnline(_config.deviceId, state);
            }
            else
            {
                payload = StatusPayload.CreateOffline(_config.deviceId, reason ?? "graceful");
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(GetStatusTopic())
                .WithPayload(Encoding.UTF8.GetBytes(payload))
                .WithQualityOfServiceLevel(qos)
                .WithRetainFlag(true)
                .Build();

            return client.PublishAsync(message, cancellationToken);
        }

        private static AgentState CloneState(AgentState source)
        {
            return new AgentState
            {
                mode = source.mode,
                media = source.media == null
                    ? null
                    : new MediaState
                    {
                        id = source.media.id,
                        playing = source.media.playing,
                        positionMs = source.media.positionMs,
                        durationMs = source.media.durationMs,
                        loop = source.media.loop
                    },
                audio = source.audio == null
                    ? null
                    : new AudioState
                    {
                        volume = source.audio.volume,
                        muted = source.audio.muted
                    },
                content = source.content == null
                    ? null
                    : new ContentState { sceneId = source.content.sceneId },
                controls = source.controls == null
                    ? null
                    : new ControlsState { busy = source.controls.busy },
                fault = source.fault == null
                    ? null
                    : new FaultState
                    {
                        code = source.fault.code,
                        message = source.fault.message
                    }
            };
        }

        private string GetStatusTopic()
        {
            return "exhibit/" + _config.deviceId + "/status";
        }

        private string GetRegisterTopic()
        {
            return "exhibit/" + _config.deviceId + "/register";
        }

        private async Task AwaitBackgroundTasksAsync()
        {
            var heartbeat = _heartbeatTask;
            var reconnect = _reconnectTask;

            if (heartbeat != null)
            {
                try { await heartbeat.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
            if (reconnect != null)
            {
                try { await reconnect.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
        }

        private Task ResetClientAsync()
        {
            if (_client != null)
            {
                _client.ConnectedAsync -= OnConnectedAsync;
                _client.DisconnectedAsync -= OnDisconnectedAsync;
                _client.Dispose();
            }

            _lifetimeCts?.Dispose();
            _client = null;
            _lifetimeCts = null;
            _heartbeatTask = null;
            _reconnectTask = null;
            _hostSnapshot = null;
            _started = false;
            _stopping = false;
            return Task.CompletedTask;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VLAgentClient));
        }

        public void Dispose()
        {
            if (_disposed) return;
            StopAsync().GetAwaiter().GetResult();
            _lifecycleGate.Dispose();
            _disposed = true;
        }
    }
}
