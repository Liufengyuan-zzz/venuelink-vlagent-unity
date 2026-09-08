#nullable enable
using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace VenueLink.VLAgent.Unity
{
    public sealed class HostInfoSnapshot
    {
        public string Hostname { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public string Mac { get; set; } = string.Empty;
        public string Os { get; set; } = string.Empty;
        public string AgentVersion { get; set; } = HostInfo.AgentVersion;
        public string Runtime { get; set; } = "unity";
    }

    /// <summary>
    /// 采集与 Broker 出站路由一致的本机 IPv4 及同网卡 MAC。
    /// 仅依赖 BCL，便于 xunit 链接测试。
    /// </summary>
    public static class HostInfo
    {
        public const string AgentVersion = "0.3.7";

        public static HostInfoSnapshot Capture(string brokerHost, int brokerPort)
        {
            if (string.IsNullOrWhiteSpace(brokerHost))
                throw new ArgumentException("brokerHost 不能为空。", nameof(brokerHost));
            if (brokerPort < 1 || brokerPort > 65535)
                throw new ArgumentOutOfRangeException(nameof(brokerPort));

            var brokerIp = ResolveBrokerIpv4(brokerHost);
            var localIp = ResolveLocalIpv4ViaRoute(brokerIp, brokerPort);
            ValidateLocalIp(localIp, brokerIp);

            var mac = FindMacForIpv4(localIp);
            if (string.IsNullOrWhiteSpace(mac))
            {
                if (IPAddress.IsLoopback(brokerIp) && IPAddress.IsLoopback(localIp))
                    mac = "00:00:00:00:00:00";
                else
                    throw new InvalidOperationException(
                        "无法取得与本地 IP " + localIp + " 对应的网卡 MAC。请确认该网卡已启用。");
            }

            return new HostInfoSnapshot
            {
                Hostname = GetHostname(),
                Ip = localIp.ToString(),
                Mac = mac,
                Os = RuntimeInformation.OSDescription,
                AgentVersion = AgentVersion,
                Runtime = "unity"
            };
        }

        private static IPAddress ResolveBrokerIpv4(string brokerHost)
        {
            if (IPAddress.TryParse(brokerHost, out var parsed))
            {
                if (parsed.AddressFamily != AddressFamily.InterNetwork)
                    throw new InvalidOperationException("brokerHost 必须是 IPv4 或可解析为 IPv4 的主机名。");
                return parsed;
            }

            var addresses = Dns.GetHostAddresses(brokerHost);
            var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (ipv4 == null)
                throw new InvalidOperationException("无法将 brokerHost 解析为 IPv4：" + brokerHost);
            return ipv4;
        }

        private static IPAddress ResolveLocalIpv4ViaRoute(IPAddress brokerIp, int brokerPort)
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect(brokerIp, brokerPort);
                    if (socket.LocalEndPoint is IPEndPoint ep && ep.Address.AddressFamily == AddressFamily.InterNetwork)
                        return ep.Address;
                }
            }
            catch
            {
                // fall through to TCP / enumeration
            }

            try
            {
                using (var client = new TcpClient())
                {
                    var connectTask = client.ConnectAsync(brokerIp, brokerPort);
                    if (connectTask.Wait(TimeSpan.FromSeconds(2)) && client.Client.LocalEndPoint is IPEndPoint ep)
                        return ep.Address;
                }
            }
            catch
            {
                // fall through
            }

            return FallbackPrivateIpv4(brokerIp);
        }

        private static IPAddress FallbackPrivateIpv4(IPAddress brokerIp)
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(u => u.Address)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Where(a => !IPAddress.IsLoopback(a))
                .Where(a => !IsLinkLocal(a))
                .ToList();

            if (candidates.Count == 0)
            {
                if (IPAddress.IsLoopback(brokerIp))
                    return IPAddress.Loopback;
                throw new InvalidOperationException(
                    "无法取得有效局域网 IPv4。请确认展项主机已连接与中控同网段网络。");
            }

            var samePrefix = candidates.FirstOrDefault(a => SameClassCPrefix(a, brokerIp));
            if (samePrefix != null) return samePrefix;

            var privateIp = candidates.FirstOrDefault(IsPrivateIpv4);
            return privateIp ?? candidates[0];
        }

        private static void ValidateLocalIp(IPAddress localIp, IPAddress brokerIp)
        {
            if (localIp.AddressFamily != AddressFamily.InterNetwork)
                throw new InvalidOperationException("本地地址必须是 IPv4。");

            if (IsLinkLocal(localIp))
                throw new InvalidOperationException("拒绝链路本地地址（169.254.x.x）作为 register IP。");

            if (IPAddress.IsLoopback(localIp) && !IPAddress.IsLoopback(brokerIp))
                throw new InvalidOperationException(
                    "拒绝回环地址作为 register IP。请将 brokerHost 设为中控局域网地址，而非 127.0.0.1。");
        }

        private static string FindMacForIpv4(IPAddress localIp)
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;

                var hasIp = nic.GetIPProperties().UnicastAddresses
                    .Any(u => u.Address.Equals(localIp));
                if (!hasIp) continue;

                var bytes = nic.GetPhysicalAddress().GetAddressBytes();
                if (bytes == null || bytes.Length == 0) continue;
                if (bytes.All(b => b == 0)) continue;

                return string.Join(":", bytes.Select(b => b.ToString("X2")));
            }

            return string.Empty;
        }

        private static string GetHostname()
        {
            try
            {
                var name = Dns.GetHostName();
                return string.IsNullOrWhiteSpace(name) ? Environment.MachineName : name;
            }
            catch
            {
                return Environment.MachineName ?? string.Empty;
            }
        }

        private static bool IsLinkLocal(IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
        }

        private static bool IsPrivateIpv4(IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            if (bytes.Length != 4) return false;
            if (bytes[0] == 10) return true;
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            return false;
        }

        private static bool SameClassCPrefix(IPAddress a, IPAddress b)
        {
            var ab = a.GetAddressBytes();
            var bb = b.GetAddressBytes();
            if (ab.Length != 4 || bb.Length != 4) return false;
            return ab[0] == bb[0] && ab[1] == bb[1] && ab[2] == bb[2];
        }
    }
}
