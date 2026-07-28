using System.Collections;
using UnityEngine;
using VenueLink.VLAgent.Unity;

namespace VenueLink.VLAgent.Samples
{
    /// <summary>
    /// 演示 ReportState：假进度/音量上报。与 VLAgentBehaviour 挂在同一物体上即可。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FakeTelemetryDemo : MonoBehaviour
    {
        [SerializeField]
        private float reportIntervalSeconds = 1f;

        [SerializeField]
        private long durationMs = 600000;

        private VLAgentBehaviour _agent;
        private long _positionMs;
        private float _volume = 0.6f;

        private void Awake()
        {
            _agent = GetComponent<VLAgentBehaviour>();
            if (_agent == null)
                _agent = gameObject.AddComponent<VLAgentBehaviour>();
        }

        private void OnEnable()
        {
            StartCoroutine(ReportLoop());
        }

        private IEnumerator ReportLoop()
        {
            var wait = new WaitForSeconds(reportIntervalSeconds);
            while (enabled)
            {
                yield return wait;
                if (_agent == null || !_agent.IsConnected) continue;

                _positionMs += (long)(reportIntervalSeconds * 1000);
                if (_positionMs > durationMs)
                    _positionMs = 0;

                var state = new AgentState
                {
                    mode = "manual",
                    media = new MediaState
                    {
                        id = "demo-clip",
                        playing = true,
                        positionMs = _positionMs,
                        durationMs = durationMs,
                        loop = true
                    },
                    audio = new AudioState
                    {
                        volume = _volume,
                        muted = false
                    },
                    content = new ContentState { sceneId = "sample-basic" }
                };

                _ = _agent.ReportStateAsync(state);
            }
        }
    }
}
