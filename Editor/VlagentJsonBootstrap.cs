using System.IO;
using UnityEditor;
using UnityEngine;

namespace VenueLink.VLAgent.Unity.Editor
{
    /// <summary>
    /// 导入 SDK 或打开工程时：若没有 Assets/StreamingAssets/vlagent.json 则创建。
    /// 已有文件不覆盖。
    /// </summary>
    [InitializeOnLoad]
    internal static class VlagentJsonBootstrap
    {
        private const string FileName = "vlagent.json";
        private const string StreamingAssetsFolder = "Assets/StreamingAssets";

        static VlagentJsonBootstrap()
        {
            EditorApplication.delayCall += EnsureConfigFile;
        }

        [MenuItem("VenueLink/VLAgent/补全 vlagent.json")]
        private static void MenuEnsureConfigFile()
        {
            var path = Path.Combine(Application.streamingAssetsPath, FileName);
            if (File.Exists(path))
            {
                Debug.Log("[VLAgent] 已存在 " + StreamingAssetsFolder + "/" + FileName + "，未覆盖。");
                return;
            }

            EnsureConfigFile();
        }

        private static void EnsureConfigFile()
        {
            var path = Path.Combine(Application.streamingAssetsPath, FileName);
            if (File.Exists(path))
                return;

            if (!AssetDatabase.IsValidFolder(StreamingAssetsFolder))
                AssetDatabase.CreateFolder("Assets", "StreamingAssets");

            Directory.CreateDirectory(Application.streamingAssetsPath);
            File.WriteAllText(path, BuildDefaultJson());
            AssetDatabase.Refresh();
            Debug.Log("[VLAgent] 已创建 " + StreamingAssetsFolder + "/" + FileName +
                      "。请改 brokerHost（中控 IP）、advertisePort（展项监听端口）。已有文件不会被覆盖。");
        }

        private static string BuildDefaultJson()
        {
            return
                "{\n" +
                "  \"deviceId\": \"\",\n" +
                "  \"brokerHost\": \"127.0.0.1\",\n" +
                "  \"brokerPort\": 1883,\n" +
                "  \"heartbeatIntervalMs\": 3000,\n" +
                "  \"reconnectDelayMs\": 2000,\n" +
                "  \"advertisePort\": 9000,\n" +
                "  \"mqttPassword\": \"\"\n" +
                "}\n";
        }
    }
}
