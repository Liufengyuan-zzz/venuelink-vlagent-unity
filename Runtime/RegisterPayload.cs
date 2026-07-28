using System;
using System.Globalization;
using System.Text;

namespace VenueLink.VLAgent.Unity
{
    public static class RegisterPayload
    {
        public const string SuggestedAccessMode = "fixedTcp";

        public static string Create(AgentConfig config, HostInfoSnapshot host, long? timestampMs = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (host == null) throw new ArgumentNullException(nameof(host));

            var timestamp = timestampMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return string.Concat(
                "{\"deviceId\":\"", Escape(config.deviceId),
                "\",\"timestamp\":", timestamp.ToString(CultureInfo.InvariantCulture),
                ",\"hostname\":\"", Escape(host.Hostname),
                "\",\"ip\":\"", Escape(host.Ip),
                "\",\"port\":", config.advertisePort.ToString(CultureInfo.InvariantCulture),
                ",\"mac\":\"", Escape(host.Mac),
                "\",\"agentVersion\":\"", Escape(host.AgentVersion),
                "\",\"os\":\"", Escape(host.Os),
                "\",\"runtime\":\"", Escape(host.Runtime),
                "\",\"suggestedAccessMode\":\"", Escape(SuggestedAccessMode),
                "\"}");
        }

        private static string Escape(string value)
        {
            if (value == null) return string.Empty;

            var builder = new StringBuilder(value.Length + 8);
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (ch < 0x20)
                            builder.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            builder.Append(ch);
                        break;
                }
            }
            return builder.ToString();
        }
    }
}
