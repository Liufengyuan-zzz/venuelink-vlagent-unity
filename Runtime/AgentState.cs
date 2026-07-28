#nullable enable
using System;

namespace VenueLink.VLAgent.Unity
{
    /// <summary>
    /// 展项业务运行态（写入 status.state）。字段均可选，只填需要上报的。
    /// SDK 发送时会自动补上 runtime=unity。
    /// </summary>
    [Serializable]
    public sealed class AgentState
    {
        /// <summary>idle / auto / manual / attract</summary>
        public string? mode;

        public MediaState? media;
        public AudioState? audio;
        public ContentState? content;
        public ControlsState? controls;
        public FaultState? fault;
    }

    [Serializable]
    public sealed class MediaState
    {
        public string? id;
        public bool? playing;
        public long? positionMs;
        public long? durationMs;
        public bool? loop;
    }

    [Serializable]
    public sealed class AudioState
    {
        /// <summary>0~1</summary>
        public float? volume;
        public bool? muted;
    }

    [Serializable]
    public sealed class ContentState
    {
        public string? sceneId;
    }

    [Serializable]
    public sealed class ControlsState
    {
        public bool? busy;
    }

    [Serializable]
    public sealed class FaultState
    {
        public string? code;
        public string? message;
    }
}
