using UnityEngine;
using VenueLink.VLAgent.Unity;

namespace VenueLink.VLAgent.Samples
{
    /// <summary>
    /// Basic Sample：挂到空 GameObject，自动添加无界面的 VLAgentBehaviour。
    /// 配置从 Assets/StreamingAssets/vlagent.json 读取。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VLAgentBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            if (GetComponent<VLAgentBehaviour>() == null)
                gameObject.AddComponent<VLAgentBehaviour>();
        }
    }
}
