using System;
using System.IO;
using UnityEngine;

namespace VenueLink.VLAgent.Unity
{
    public static class AgentConfigLoader
    {
        public const string DefaultFileName = "vlagent.json";

        public static AgentConfig LoadFromStreamingAssets(string fileName = DefaultFileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("配置文件名不能为空。", nameof(fileName));

            var path = Path.Combine(Application.streamingAssetsPath, fileName);
            if (!File.Exists(path))
                throw new FileNotFoundException("找不到 VLAgent 配置文件：" + path, path);

            var json = File.ReadAllText(path);
            var config = JsonUtility.FromJson<AgentConfig>(json);
            if (config == null)
                throw new InvalidDataException("VLAgent 配置文件不是有效 JSON：" + path);

            config.Validate();
            return config;
        }
    }
}
