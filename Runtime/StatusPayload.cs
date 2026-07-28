#nullable enable
using System;
using System.Globalization;
using System.Text;

namespace VenueLink.VLAgent.Unity
{
    public static class StatusPayload
    {
        public static string CreateOnline(string deviceId, AgentState? state = null)
        {
            return CreateOnline(deviceId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), state);
        }

        public static string CreateOnline(string deviceId, long timestampMs, AgentState? state)
        {
            var builder = new StringBuilder(256);
            builder.Append("{\"deviceId\":\"").Append(Escape(deviceId))
                .Append("\",\"timestamp\":").Append(timestampMs.ToString(CultureInfo.InvariantCulture))
                .Append(",\"online\":true,\"state\":{");

            builder.Append("\"runtime\":\"unity\"");
            AppendOptionalString(builder, "mode", state != null ? state.mode : null);
            AppendMedia(builder, state != null ? state.media : null);
            AppendAudio(builder, state != null ? state.audio : null);
            AppendContent(builder, state != null ? state.content : null);
            AppendControls(builder, state != null ? state.controls : null);
            AppendFault(builder, state != null ? state.fault : null);

            builder.Append("}}");
            return builder.ToString();
        }

        public static string CreateOffline(string deviceId, string reason, bool useServerTimestamp = false)
        {
            var timestamp = useServerTimestamp ? 0L : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return string.Concat(
                "{\"deviceId\":\"", Escape(deviceId),
                "\",\"timestamp\":", timestamp.ToString(CultureInfo.InvariantCulture),
                ",\"online\":false",
                ",\"state\":{\"reason\":\"", Escape(reason ?? string.Empty), "\"}}");
        }

        private static void AppendMedia(StringBuilder builder, MediaState? media)
        {
            if (media == null) return;
            builder.Append(",\"media\":{");
            var first = true;
            first = AppendObjectString(builder, first, "id", media.id);
            first = AppendObjectBool(builder, first, "playing", media.playing);
            first = AppendObjectLong(builder, first, "positionMs", media.positionMs);
            first = AppendObjectLong(builder, first, "durationMs", media.durationMs);
            AppendObjectBool(builder, first, "loop", media.loop);
            builder.Append('}');
        }

        private static void AppendAudio(StringBuilder builder, AudioState? audio)
        {
            if (audio == null) return;
            builder.Append(",\"audio\":{");
            var first = true;
            first = AppendObjectFloat(builder, first, "volume", audio.volume);
            AppendObjectBool(builder, first, "muted", audio.muted);
            builder.Append('}');
        }

        private static void AppendContent(StringBuilder builder, ContentState? content)
        {
            if (content == null) return;
            builder.Append(",\"content\":{");
            AppendObjectString(builder, true, "sceneId", content.sceneId);
            builder.Append('}');
        }

        private static void AppendControls(StringBuilder builder, ControlsState? controls)
        {
            if (controls == null) return;
            builder.Append(",\"controls\":{");
            AppendObjectBool(builder, true, "busy", controls.busy);
            builder.Append('}');
        }

        private static void AppendFault(StringBuilder builder, FaultState? fault)
        {
            if (fault == null) return;
            builder.Append(",\"fault\":{");
            var first = true;
            first = AppendObjectString(builder, first, "code", fault.code);
            AppendObjectString(builder, first, "message", fault.message);
            builder.Append('}');
        }

        private static void AppendOptionalString(StringBuilder builder, string key, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            builder.Append(",\"").Append(key).Append("\":\"").Append(Escape(value)).Append('"');
        }

        private static bool AppendObjectString(StringBuilder builder, bool first, string key, string? value)
        {
            if (string.IsNullOrEmpty(value)) return first;
            if (!first) builder.Append(',');
            builder.Append('"').Append(key).Append("\":\"").Append(Escape(value)).Append('"');
            return false;
        }

        private static bool AppendObjectBool(StringBuilder builder, bool first, string key, bool? value)
        {
            if (value == null) return first;
            if (!first) builder.Append(',');
            builder.Append('"').Append(key).Append("\":").Append(value.Value ? "true" : "false");
            return false;
        }

        private static bool AppendObjectLong(StringBuilder builder, bool first, string key, long? value)
        {
            if (value == null) return first;
            if (!first) builder.Append(',');
            builder.Append('"').Append(key).Append("\":")
                .Append(value.Value.ToString(CultureInfo.InvariantCulture));
            return false;
        }

        private static bool AppendObjectFloat(StringBuilder builder, bool first, string key, float? value)
        {
            if (value == null) return first;
            if (!first) builder.Append(',');
            builder.Append('"').Append(key).Append("\":")
                .Append(value.Value.ToString("R", CultureInfo.InvariantCulture));
            return false;
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
