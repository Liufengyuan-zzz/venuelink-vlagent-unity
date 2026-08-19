using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VenueLink.VLAgent.Unity
{
    [DisallowMultipleComponent]
    public sealed class VLAgentBehaviour : MonoBehaviour
    {
        [SerializeField]
        private string configFileName = AgentConfigLoader.DefaultFileName;

        private VLAgentClient _client;
        private bool _stopped;

        public bool IsConnected
        {
            get { return _client != null && _client.IsConnected; }
        }

        public VLAgentClient Client
        {
            get { return _client; }
        }

        /// <summary>上报业务运行态（进度/音量等）。未启动时忽略。</summary>
        public Task ReportStateAsync(AgentState state, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_client == null) return Task.CompletedTask;
            return _client.ReportStateAsync(state, cancellationToken);
        }

        /// <summary>清空业务 state。</summary>
        public Task ClearStateAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_client == null) return Task.CompletedTask;
            return _client.ClearStateAsync(cancellationToken);
        }

        private async void Start()
        {
            try
            {
                var path = AgentConfigLoader.GetStreamingAssetsPath(configFileName);
                var config = AgentConfigLoader.LoadFromStreamingAssets(configFileName);
                _client = new VLAgentClient(config, (deviceId, password) =>
                    AgentConfigLoader.SaveAssignedIdentity(path, deviceId, password));
                _client.ConnectionChanged += OnConnectionChanged;
                _client.BackgroundError += OnBackgroundError;
                await _client.StartAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError("[VLAgent] 启动失败：" + ex);
            }
        }

        private void OnApplicationQuit()
        {
            StopBlocking();
        }

        private void OnDestroy()
        {
            StopBlocking();
        }

        private void StopBlocking()
        {
            if (_stopped || _client == null) return;
            _stopped = true;

            try
            {
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    _client.StopAsync(timeout.Token).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[VLAgent] 正常下线失败，将由 Broker LWT 兜底：" + ex.Message);
            }
            finally
            {
                _client.ConnectionChanged -= OnConnectionChanged;
                _client.BackgroundError -= OnBackgroundError;
                _client.Dispose();
                _client = null;
            }
        }

        private static void OnConnectionChanged(bool connected)
        {
            Debug.Log("[VLAgent] MQTT " + (connected ? "已连接" : "已断开"));
        }

        private static void OnBackgroundError(Exception exception)
        {
            Debug.LogWarning("[VLAgent] 后台重连/心跳异常：" + exception.Message);
        }
    }
}
