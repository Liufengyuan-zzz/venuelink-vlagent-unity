using System;
using System.IO;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#else
using System.Text.Json;
#endif

namespace VenueLink.VLAgent.Unity
{
    public static class AgentConfigLoader
    {
        public const string DefaultFileName = "vlagent.json";

        public static string GetStreamingAssetsPath(string fileName = DefaultFileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("配置文件名不能为空。", nameof(fileName));
#if UNITY_5_3_OR_NEWER
            return Path.Combine(Application.streamingAssetsPath, fileName);
#else
            throw new PlatformNotSupportedException("StreamingAssets 路径只在 Unity 运行时可用。");
#endif
        }

        public static string GetPersistentDataPath(string fileName = DefaultFileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("配置文件名不能为空。", nameof(fileName));
#if UNITY_5_3_OR_NEWER
            return Path.Combine(Application.persistentDataPath, fileName);
#else
            throw new PlatformNotSupportedException("persistentDataPath 只在 Unity 运行时可用。");
#endif
        }

        /// <summary>
        /// 读 StreamingAssets 模板，再用 persistentDataPath 里的签发身份覆盖 deviceId / mqttPassword。
        /// </summary>
        public static AgentConfig Load(string fileName = DefaultFileName)
        {
            return LoadFromFiles(GetStreamingAssetsPath(fileName), GetPersistentDataPath(fileName));
        }

        internal static AgentConfig LoadFromFiles(string templatePath, string persistentPath)
        {
            var config = ParseFile(templatePath);
            if (File.Exists(persistentPath))
                config.OverlayPersistedIdentity(ParseFile(persistentPath));
            config.Validate();
            return config;
        }

        public static AgentConfig LoadFromStreamingAssets(string fileName = DefaultFileName)
        {
            return LoadFromFile(GetStreamingAssetsPath(fileName));
        }

        internal static AgentConfig LoadFromFile(string absolutePath)
        {
            var config = ParseFile(absolutePath);
            config.Validate();
            return config;
        }

        private static AgentConfig ParseFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("找不到 VLAgent 配置文件：" + path, path);

            var json = File.ReadAllText(path);
            EnsureNumericFields(json);
            var config = Deserialize(json);
            if (config == null)
                throw new InvalidDataException("VLAgent 配置文件不是有效 JSON：" + path);
            config.identityAssignedSpecified = HasTopLevelJsonProperty(json, "identityAssigned");
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
                config = Deserialize(File.ReadAllText(absolutePath))
                         ?? new AgentConfig();
            }
            else
            {
                config = new AgentConfig();
            }

            config.deviceId = deviceId ?? string.Empty;
            config.mqttPassword = mqttPassword ?? string.Empty;
            config.identityAssigned = !string.IsNullOrEmpty(config.mqttPassword);
            config.identityAssignedSpecified = true;
            WriteAtomically(absolutePath, Serialize(config));
        }

        /// <summary>仅写回 mqttPassword，保留文件中的 deviceId。</summary>
        public static void SaveMqttPassword(string absolutePath, string mqttPassword)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
                throw new ArgumentException("配置路径不能为空。", nameof(absolutePath));

            var deviceId = string.Empty;
            if (File.Exists(absolutePath))
            {
                var existing = Deserialize(File.ReadAllText(absolutePath));
                if (existing != null) deviceId = existing.deviceId;
            }

            SaveAssignedIdentity(absolutePath, deviceId, mqttPassword);
        }

#if UNITY_5_3_OR_NEWER
        private static AgentConfig Deserialize(string json)
#else
        private static AgentConfig? Deserialize(string json)
#endif
        {
#if UNITY_5_3_OR_NEWER
            return JsonUtility.FromJson<AgentConfig>(json);
#else
            return JsonSerializer.Deserialize<AgentConfig>(json, JsonOptions);
#endif
        }

        private static string Serialize(AgentConfig config)
        {
#if UNITY_5_3_OR_NEWER
            return JsonUtility.ToJson(config, true);
#else
            return JsonSerializer.Serialize(config, JsonOptions);
#endif
        }

#if !UNITY_5_3_OR_NEWER
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true
        };
#endif

        private static void WriteAtomically(string absolutePath, string contents)
        {
            var fullPath = Path.GetFullPath(absolutePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidDataException("配置路径缺少父目录：" + absolutePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var tempPath = Path.Combine(
                directory,
                "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(tempPath, contents);
                // Unity / netstandard 没有 File.Move(src, dest, overwrite)。
                if (File.Exists(fullPath))
                    File.Replace(tempPath, fullPath, destinationBackupFileName: null);
                else
                    File.Move(tempPath, fullPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        private static bool HasTopLevelJsonProperty(string json, string key)
        {
            var depth = 0;
            var inString = false;
            var escaped = false;
            var stringStart = -1;
            for (var i = 0; i < json.Length; i++)
            {
                var current = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }
                    if (current == '\\')
                    {
                        escaped = true;
                        continue;
                    }
                    if (current != '"') continue;

                    inString = false;
                    if (depth != 1 || i - stringStart - 1 != key.Length
                        || string.CompareOrdinal(json, stringStart + 1, key, 0, key.Length) != 0)
                        continue;

                    var next = i + 1;
                    while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                    if (next < json.Length && json[next] == ':') return true;
                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    stringStart = i;
                }
                else if (current == '{')
                {
                    depth++;
                }
                else if (current == '}')
                {
                    depth--;
                }
            }

            return false;
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
