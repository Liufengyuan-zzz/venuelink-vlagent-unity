using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using VenueLink.VLAgent.Unity;

namespace VenueLink.VLAgent.Samples
{
    /// <summary>
    /// 接收 VLServer FixedTcp 指令：中控每次短连接发来「一行 UTF-8 + \n」。
    /// 监听端口应与 StreamingAssets/vlagent.json 的 advertisePort 一致（默认 9000）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FixedTcpCommandServer : MonoBehaviour
    {
        [SerializeField]
        private int listenPort = 9000;

        [Tooltip("为 true 时，收到指令后在同一连接回一行 AckMessage JSON（指令勾选「等回执」时需要）。")]
        [SerializeField]
        private bool replyAck = true;

        [SerializeField]
        private bool logToConsole = true;

        [SerializeField]
        private StringUnityEvent onCommandReceived;

        [SerializeField]
        [Tooltip("留空则自动取同物体上的 VLCommandTable。")]
        private VLCommandTable commandTable;

        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;
        private readonly ConcurrentQueue<string> _pending = new ConcurrentQueue<string>();

        public event Action<string> CommandReceived;

        private void Start()
        {
            if (commandTable == null)
                commandTable = GetComponent<VLCommandTable>();
            TryApplyPortFromConfig();
            StartServer();
        }

        private void Update()
        {
            while (_pending.TryDequeue(out var line))
            {
                if (logToConsole)
                    Debug.Log("[FixedTcp] 收到指令：" + line);

                CommandReceived?.Invoke(line);
                if (onCommandReceived != null)
                    onCommandReceived.Invoke(line);
                if (commandTable != null)
                    commandTable.Dispatch(line);
            }
        }

        private void OnDestroy()
        {
            StopServer();
        }

        private void OnApplicationQuit()
        {
            StopServer();
        }

        private void TryApplyPortFromConfig()
        {
            try
            {
                var path = Path.Combine(Application.streamingAssetsPath, "vlagent.json");
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var port = ExtractInt(json, "advertisePort");
                if (port >= 1 && port <= 65535)
                    listenPort = port;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FixedTcp] 读取 vlagent.json 失败，使用 Inspector 端口：" + ex.Message);
            }
        }

        private void StartServer()
        {
            if (_running) return;

            try
            {
                _listener = new TcpListener(IPAddress.Any, listenPort);
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Start();
                _running = true;
                _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "VL-FixedTcp" };
                _acceptThread.Start();
                Debug.Log(
                    "[FixedTcp] 已监听 0.0.0.0:" + listenPort
                    + "。设备档案 FixedTcp 端口须为此值；本机联调 IP 填 127.0.0.1。"
                    + " Console 未见本行 = 未真正 Listen（检查脚本是否启用、是否在 Play Mode）。");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[FixedTcp] 端口 " + listenPort + " 启动失败：" + ex.Message
                    + "（常见原因：端口被占用，或未以 Play Mode 运行）");
                _running = false;
            }
        }

        private void StopServer()
        {
            _running = false;
            try { _listener?.Stop(); } catch { /* ignore */ }
            _listener = null;

            if (_acceptThread != null && _acceptThread.IsAlive)
            {
                if (!_acceptThread.Join(1000))
                    Debug.LogWarning("[FixedTcp] 接受线程未在超时内退出");
            }

            _acceptThread = null;
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client = null;
                try
                {
                    client = _listener.AcceptTcpClient();
                    HandleClient(client);
                }
                catch (SocketException)
                {
                    if (!_running) break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[FixedTcp] 连接处理异常：" + ex.Message);
                }
                finally
                {
                    try { client?.Close(); } catch { /* ignore */ }
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n", AutoFlush = true })
            {
                // 中控一次连接只发一行；给一点读超时避免挂死
                client.ReceiveTimeout = 10000;
                client.SendTimeout = 5000;

                var line = reader.ReadLine();
                if (string.IsNullOrEmpty(line)) return;

                _pending.Enqueue(line);

                if (replyAck)
                {
                    var msgId = ExtractJsonString(line, "msgId") ?? string.Empty;
                    var ack = BuildAck(msgId, success: true, errorMsg: null);
                    writer.WriteLine(ack);
                }
            }
        }

        private static string BuildAck(string msgId, bool success, string errorMsg)
        {
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                .ToString(CultureInfo.InvariantCulture);
            var err = errorMsg == null ? "null" : "\"" + EscapeJson(errorMsg) + "\"";
            return string.Concat(
                "{\"msgId\":\"", EscapeJson(msgId ?? string.Empty),
                "\",\"timestamp\":", ts,
                ",\"type\":\"ack\",\"success\":", success ? "true" : "false",
                ",\"errorMsg\":", err, "}");
        }

        private static int ExtractInt(string json, string key)
        {
            var token = "\"" + key + "\"";
            var idx = json.IndexOf(token, StringComparison.Ordinal);
            if (idx < 0) return -1;
            idx = json.IndexOf(':', idx + token.Length);
            if (idx < 0) return -1;
            idx++;
            while (idx < json.Length && char.IsWhiteSpace(json[idx])) idx++;
            var end = idx;
            while (end < json.Length && (char.IsDigit(json[end]))) end++;
            if (end == idx) return -1;
            return int.TryParse(json.Substring(idx, end - idx), out var n) ? n : -1;
        }

        private static string ExtractJsonString(string json, string key)
        {
            var token = "\"" + key + "\"";
            var idx = json.IndexOf(token, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx = json.IndexOf(':', idx + token.Length);
            if (idx < 0) return null;
            idx = json.IndexOf('"', idx + 1);
            if (idx < 0) return null;
            var end = idx + 1;
            var sb = new StringBuilder();
            while (end < json.Length)
            {
                var ch = json[end];
                if (ch == '\\' && end + 1 < json.Length)
                {
                    sb.Append(json[end + 1]);
                    end += 2;
                    continue;
                }
                if (ch == '"') break;
                sb.Append(ch);
                end++;
            }
            return sb.ToString();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }

        [Serializable]
        public sealed class StringUnityEvent : UnityEvent<string>
        {
        }
    }
}
