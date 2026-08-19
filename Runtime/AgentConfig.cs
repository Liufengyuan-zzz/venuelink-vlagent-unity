using System;

namespace VenueLink.VLAgent.Unity
{
    [Serializable]
    public sealed class AgentConfig
    {
        public string deviceId = string.Empty;
        public string brokerHost = "127.0.0.1";
        public int brokerPort = 1883;
        public int heartbeatIntervalMs = 3000;
        public int reconnectDelayMs = 2000;

        /// <summary>
        /// 已确认入库设备的 MQTT 密码；未入库首次 register 可留空。
        /// </summary>
        public string mqttPassword = string.Empty;

        /// <summary>
        /// 展项自身 TCP 监听端口（FixedTcp 候选）。SDK 不监听该端口。
        /// </summary>
        public int advertisePort;

        public void Validate()
        {
            var pending = string.IsNullOrEmpty(mqttPassword);
            if (!pending && string.IsNullOrWhiteSpace(deviceId))
                throw new ArgumentException("已入库配置的 deviceId 不能为空。", nameof(deviceId));
            if (!string.IsNullOrWhiteSpace(deviceId)
                && (deviceId.IndexOf('/') >= 0 || deviceId.IndexOf('+') >= 0 || deviceId.IndexOf('#') >= 0))
                throw new ArgumentException("deviceId 不能包含 MQTT Topic 分隔符或通配符。", nameof(deviceId));
            if (string.IsNullOrWhiteSpace(brokerHost))
                throw new ArgumentException("brokerHost 不能为空。", nameof(brokerHost));
            if (brokerPort < 1 || brokerPort > 65535)
                throw new ArgumentOutOfRangeException(nameof(brokerPort), "brokerPort 必须在 1～65535 之间。");
            if (heartbeatIntervalMs < 500)
                throw new ArgumentOutOfRangeException(nameof(heartbeatIntervalMs), "heartbeatIntervalMs 不能小于 500ms。");
            if (reconnectDelayMs < 500)
                throw new ArgumentOutOfRangeException(nameof(reconnectDelayMs), "reconnectDelayMs 不能小于 500ms。");
            if (advertisePort < 1 || advertisePort > 65535)
                throw new ArgumentOutOfRangeException(nameof(advertisePort), "advertisePort 必须在 1～65535 之间（展项 TCP 监听端口）。");
        }
    }
}
