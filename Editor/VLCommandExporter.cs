using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using VenueLink.VLAgent.Unity;

namespace VenueLink.VLAgent.Unity.Editor
{
    /// <summary>
    /// 把场景里 <see cref="VLCommandTable"/> 打成中控可追加导入的明文 .vlconfig（只含 commands.json）。
    /// </summary>
    internal static class VLCommandExporter
    {
        private static readonly string[] ReservedIdPrefixes = { "vlhost-" };

        [MenuItem("VenueLink/VLAgent/导出指令配置包")]
        private static void Export()
        {
            var tables = UnityEngine.Object.FindObjectsByType<VLCommandTable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (tables == null || tables.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "导出指令",
                    "当前打开的场景里没有 VLCommandTable。把它和收指令脚本挂在同一物体上，配好指令后再导出。",
                    "确定");
                return;
            }

            var entries = new List<VLCommandBinding>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<string>();

            foreach (var table in tables)
            {
                if (table == null)
                    continue;
                table.EnsureIds();
                EditorUtility.SetDirty(table);
                foreach (var command in table.Commands)
                {
                    var error = TryAdd(command, ids, entries);
                    if (error != null)
                        errors.Add(error);
                }
            }

            if (errors.Count > 0)
            {
                EditorUtility.DisplayDialog("导出指令", string.Join("\n", errors), "确定");
                return;
            }

            if (entries.Count == 0)
            {
                EditorUtility.DisplayDialog("导出指令", "指令表是空的，至少填一条显示名和指令内容。", "确定");
                return;
            }

            var path = EditorUtility.SaveFilePanel(
                "导出指令配置包",
                "",
                "commands",
                "vlconfig");
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                WritePackage(path, entries);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("导出指令", "写出失败：" + ex.Message, "确定");
                return;
            }

            EditorUtility.DisplayDialog(
                "导出指令",
                "已写出 " + entries.Count + " 条指令。\n中控：设置 → 备份与迁移 → 追加导入。",
                "确定");
        }

        private static string TryAdd(VLCommandBinding command, HashSet<string> ids, List<VLCommandBinding> entries)
        {
            if (command == null)
                return null;

            var id = command.id == null ? "" : command.id.Trim();
            var label = command.label == null ? "" : command.label.Trim();
            var payload = command.payload == null ? "" : command.payload.Trim();

            if (string.IsNullOrEmpty(label) && string.IsNullOrEmpty(payload))
                return null;
            if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(payload))
                return "每条指令都要有显示名和指令内容。";
            if (string.IsNullOrEmpty(id))
                return "指令 id 为空。";
            if (IsReserved(id))
                return "指令 id 不能使用中控保留前缀 vlhost-：" + id;
            if (!ids.Add(id))
                return "指令 id 重复：" + id + "。追加导入按 id 判重。";

            command.id = id;
            command.label = label;
            command.payload = payload;
            entries.Add(command);
            return null;
        }

        private static bool IsReserved(string id)
        {
            foreach (var prefix in ReservedIdPrefixes)
            {
                if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void WritePackage(string destinationPath, List<VLCommandBinding> entries)
        {
            var commandsJson = BuildCommandsJson(entries);
            var commandsBytes = new UTF8Encoding(false).GetBytes(commandsJson);

            var filesJson =
                "    {\n" +
                "      \"path\": \"config/commands.json\",\n" +
                "      \"sizeBytes\": " + commandsBytes.Length + ",\n" +
                "      \"sha256\": \"" + Sha256Hex(commandsBytes) + "\"\n" +
                "    }";

            var manifestJson =
                "{\n" +
                "  \"formatVersion\": 1,\n" +
                "  \"appVersion\": \"" + Escape(HostInfo.AgentVersion) + "\",\n" +
                "  \"exportedAt\": \"" + DateTimeOffset.UtcNow.ToString("o") + "\",\n" +
                "  \"sourceServerName\": \"Unity VLAgent\",\n" +
                "  \"profile\": \"Custom\",\n" +
                "  \"includes\": [\n" +
                "    \"commands\"\n" +
                "  ],\n" +
                "  \"files\": [\n" +
                filesJson + "\n" +
                "  ]\n" +
                "}\n";
            var manifestBytes = new UTF8Encoding(false).GetBytes(manifestJson);

            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            using (var stream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(zip, "manifest.json", manifestBytes);
                WriteEntry(zip, "config/commands.json", commandsBytes);
            }
        }

        private static string BuildCommandsJson(List<VLCommandBinding> entries)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"commands\": [\n");
            for (var i = 0; i < entries.Count; i++)
            {
                var command = entries[i];
                var id = command.id.Trim();
                sb.Append("    {\n");
                sb.Append("      \"id\": \"").Append(Escape(id)).Append("\",\n");
                sb.Append("      \"action\": \"").Append(Escape(id)).Append("\",\n");
                sb.Append("      \"label\": \"").Append(Escape(command.label.Trim())).Append("\",\n");
                sb.Append("      \"payload\": \"").Append(Escape(command.payload.Trim())).Append("\",\n");
                sb.Append("      \"payloadFormat\": \"text\",\n");
                sb.Append("      \"ackRequired\": ").Append(command.ackRequired ? "true" : "false").Append(",\n");
                sb.Append("      \"timeoutMs\": 3000,\n");
                sb.Append("      \"params\": [],\n");
                sb.Append("      \"appliesTo\": []\n");
                sb.Append("    }");
                if (i < entries.Count - 1)
                    sb.Append(',');
                sb.Append('\n');
            }

            sb.Append("  ]\n}\n");
            return sb.ToString();
        }

        private static void WriteEntry(ZipArchive zip, string name, byte[] data)
        {
            var entry = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
            using (var dest = entry.Open())
                dest.Write(data, 0, data.Length);
        }

        private static string Sha256Hex(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(data);
                var sb = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 32)
                            sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else
                            sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
