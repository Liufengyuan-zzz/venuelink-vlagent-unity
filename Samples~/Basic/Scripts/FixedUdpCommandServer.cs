using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

namespace VenueLink.VLAgent.Samples
{
    /// <summary>
    /// 接收 VLServer FixedUdp 指令：中控每条指令发来一个 UTF-8 数据报，一包即一条消息，**没有换行符**。
    /// 监听端口应与 StreamingAssets/vlagent.json 的 advertisePort 一致。
    /// <para>
    /// 与 <c>FixedTcpCommandServer</c> 二选一，不要两个都挂同一端口。UDP 无连接、不回执：
    /// 中控侧「等回执」只代表它把包发出去了，收没收到、执行没执行都靠展项自己的日志判断。
    /// </para>
    /// <para>十六进制指令是原始字节，不是文本；需要处理二进制帧时请改用下面的 <c>DatagramReceived</c>。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FixedUdpCommandServer : MonoBehaviour
    {
        [SerializeField]
        private int listenPort = 9090;

        [SerializeField]
        private bool logToConsole = true;

        [SerializeField]
        private StringUnityEvent onCommandReceived;

        private UdpClient _client;
        private Thread _receiveThread;
        private volatile bool _running;
        private readonly ConcurrentQueue<byte[]> _pending = new ConcurrentQueue<byte[]>();

        /// <summary>文本指令（已按 UTF-8 解码并去掉首尾空白与 NUL）。</summary>
        public event Action<string> CommandReceived;

        /// <summary>原始数据报，用于十六进制 / 私有二进制帧。</summary>
        public event Action<byte[]> DatagramReceived;

        private void Start()
        {
            TryApplyPortFromConfig();
            StartServer();
        }

        private void Update()
        {
            while (_pending.TryDequeue(out var datagram))
            {
                if (DatagramReceived != null)
                    DatagramReceived(datagram);

                // 中控发文本时不加换行；但第三方转发链路可能补上 \n 或补齐 NUL，这里一并去掉
                var line = Encoding.UTF8.GetString(datagram).Trim().Trim('\0');
                if (line.Length == 0)
                    continue;

                if (logToConsole)
                    Debug.Log("[FixedUdp] 收到指令：" + line);

                if (CommandReceived != null)
                    CommandReceived(line);
                if (onCommandReceived != null)
                    onCommandReceived.Invoke(line);

                // 在此根据 line 驱动你的展项逻辑，例如：
                // if (line == "standby") { ... }
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
                Debug.LogWarning("[FixedUdp] 读取 vlagent.json 失败，使用 Inspector 端口：" + ex.Message);
            }
        }

        private void StartServer()
        {
            if (_running) return;

            try
            {
                // 不设 ReuseAddress：端口被占用时要立刻报错，否则两个监听方会各收到一部分包，现场极难查
                _client = new UdpClient(new IPEndPoint(IPAddress.Any, listenPort));
                _running = true;
                _receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "VL-FixedUdp" };
                _receiveThread.Start();
                Debug.Log(
                    "[FixedUdp] 已监听 0.0.0.0:" + listenPort
                    + "。设备档案 FixedUdp 端口须为此值；本机联调 IP 填 127.0.0.1。"
                    + " Console 未见本行 = 未真正监听（检查脚本是否启用、是否在 Play Mode）。");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[FixedUdp] 端口 " + listenPort + " 启动失败：" + ex.Message
                    + "（常见原因：端口被占用，或与 FixedTcpCommandServer 配了同一端口）");
                _running = false;
                CloseClient();
            }
        }

        private void StopServer()
        {
            _running = false;

            // 关掉 socket 让阻塞中的 Receive 抛异常退出；不用 Thread.Abort（.NET Core 已不支持且会漏资源）
            CloseClient();

            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                if (!_receiveThread.Join(1000))
                    Debug.LogWarning("[FixedUdp] 接收线程未在超时内退出");
            }

            _receiveThread = null;
        }

        private void CloseClient()
        {
            if (_client == null) return;
            try { _client.Close(); } catch { /* ignore */ }
            _client = null;
        }

        private void ReceiveLoop()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                try
                {
                    var datagram = _client.Receive(ref remote);
                    if (datagram != null && datagram.Length > 0)
                        _pending.Enqueue(datagram);
                }
                catch (SocketException)
                {
                    // 停止监听时 Close 会让 Receive 抛这个异常；仍在运行则是单次收包出错，继续收下一包
                    if (!_running) break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!_running) break;
                    Debug.LogWarning("[FixedUdp] 收包异常：" + ex.Message);
                }
            }
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
            while (end < json.Length && char.IsDigit(json[end])) end++;
            if (end == idx) return -1;
            return int.TryParse(json.Substring(idx, end - idx), out var n) ? n : -1;
        }

        [Serializable]
        public sealed class StringUnityEvent : UnityEvent<string>
        {
        }
    }
}
