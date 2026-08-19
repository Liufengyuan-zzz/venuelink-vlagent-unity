using System;
using System.IO;
using UnityEngine;

namespace VenueLink.VLAgent.Unity
{
    public static class AgentConfigLoader
    {
        public const string DefaultFileName = "vlagent.json";

        public static string GetStreamingAssetsPath(string fileName = DefaultFileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("配置文件名不能为空。", nameof(fileName));
            return Path.Combine(Application.streamingAssetsPath, fileName);
        }

        public static AgentConfig LoadFromStreamingAssets(string fileName = DefaultFileName)
        {
            var path = GetStreamingAssetsPath(fileName);
            if (!File.Exists(path))
                throw new FileNotFoundException("找不到 VLAgent 配置文件：" + path, path);

            var json = File.ReadAllText(path);
            EnsureNumericFields(json);
            var config = JsonUtility.FromJson<AgentConfig>(json);
            if (config == null)
                throw new InvalidDataException("VLAgent 配置文件不是有效 JSON：" + path);

            config.Validate();
            return config;
        }

        /// <summary>
        /// 将中控签发的 deviceId 与 mqttPassword 写回配置文件。
        /// </summary>
        public static void SaveAssignedIdentity(string absolutePath, string deviceId, string mqttPassword)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
                throw new ArgumentException("配置路径不能为空。", nameof(absolutePath));

            AgentConfig config;
            if (File.Exists(absolutePath))
            {
                config = JsonUtility.FromJson<AgentConfig>(File.ReadAllText(absolutePath))
                         ?? new AgentConfig();
            }
            else
            {
                config = new AgentConfig();
            }

            config.deviceId = deviceId ?? string.Empty;
            config.mqttPassword = mqttPassword ?? string.Empty;
            var dir = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(absolutePath, JsonUtility.ToJson(config, true));
        }

        /// <summary>仅写回 mqttPassword，保留文件中的 deviceId。</summary>
        public static void SaveMqttPassword(string absolutePath, string mqttPassword)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
                throw new ArgumentException("配置路径不能为空。", nameof(absolutePath));

            var deviceId = string.Empty;
            if (File.Exists(absolutePath))
            {
                var existing = JsonUtility.FromJson<AgentConfig>(File.ReadAllText(absolutePath));
                if (existing != null) deviceId = existing.deviceId;
            }

            SaveAssignedIdentity(absolutePath, deviceId, mqttPassword);
        }

        private static void EnsureNumericFields(string json)
        {
            EnsureJsonNumber(json, "brokerPort");
            EnsureJsonNumber(json, "heartbeatIntervalMs");
            EnsureJsonNumber(json, "reconnectDelayMs");
            EnsureJsonNumber(json, "advertisePort");
        }

        private static void EnsureJsonNumber(string json, string key)
        {
            var needle = "\"" + key + "\"";
            var idx = json.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0) return;

            var colon = json.IndexOf(':', idx + needle.Length);
            if (colon < 0)
                throw new InvalidDataException(key + " 格式无效。");

            var i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] is '"' or 'n' or 't' or 'f')
                throw new InvalidDataException(key + " 必须是 JSON 数字。");
        }
    }
}
