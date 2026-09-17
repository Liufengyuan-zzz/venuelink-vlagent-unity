using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VenueLink.VLAgent.Unity
{
    /// <summary>
    /// 展项指令表：Inspector 里配显示名和 payload，把播放/切场景等方法拖到「收到后」。
    /// 中控下发的是 payload 原文；<see cref="Dispatch"/> 按 payload 匹配后 Invok。
    /// 和 <c>FixedTcpCommandServer</c> / <c>FixedUdpCommandServer</c> 挂同一物体时会自动分发。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("VenueLink/VLAgent Command Table")]
    public sealed class VLCommandTable : MonoBehaviour
    {
        [SerializeField]
        private List<VLCommandBinding> commands = new List<VLCommandBinding>();

        public IReadOnlyList<VLCommandBinding> Commands
        {
            get { return commands; }
        }

        /// <summary>主线程调用。payload 对得上则触发对应 UnityEvent，返回是否命中。</summary>
        public bool Dispatch(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || commands == null)
                return false;

            var incoming = line.Trim();
            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                if (command == null || string.IsNullOrWhiteSpace(command.payload))
                    continue;
                if (!string.Equals(command.payload.Trim(), incoming, StringComparison.Ordinal))
                    continue;

                if (command.onReceived != null)
                    command.onReceived.Invoke();
                return true;
            }

            Debug.LogWarning("[VLAgent] 未配置的指令：" + incoming);
            return false;
        }

        private void OnValidate()
        {
            EnsureIds();
        }

        public void EnsureIds()
        {
            if (commands == null)
                return;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                if (command == null)
                    continue;
                if (string.IsNullOrWhiteSpace(command.id))
                    command.id = Guid.NewGuid().ToString("N").Substring(0, 12);
                else
                    command.id = command.id.Trim();

                if (!seen.Add(command.id))
                    Debug.LogWarning("[VLAgent] 指令 id 重复：" + command.id + "。追加导入按 id 判重，重复导出只会留下第一条。");
            }
        }
    }

    [Serializable]
    public sealed class VLCommandBinding
    {
        [Tooltip("追加导入按此 id 判重。留空会自动生成，生成后不要改。")]
        public string id = "";

        [Tooltip("中控指令管理里的显示名。")]
        public string label = "";

        [Tooltip("中控原样发给展项的内容，须与 Dispatch 匹配。")]
        [TextArea(1, 4)]
        public string payload = "";

        [Tooltip("对应中控「等回执」。TCP 示例开启回执时才会真正回 ACK。")]
        public bool ackRequired;

        [Tooltip("收到这条指令后调用的展项方法，例如播放视频。")]
        public UnityEvent onReceived = new UnityEvent();
    }
}
