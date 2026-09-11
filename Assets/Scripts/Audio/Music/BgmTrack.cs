using UnityEngine;

namespace ShinySTG.Audio
{
    /// <summary>
    /// 单首 BGM 资产。
    ///
    /// 设计要点:
    ///   - 一首 BGM 通常是一个较长的 AudioClip(几秒到几分钟)。
    ///   - 可以是循环音(Loop=true),也可以是带 intro + loop 段的复杂音轨
    ///     (Loop=false + PlayOnAwake=false,由外部脚本控制播放)。
    ///   - 默认音量倍率与 Bus 路由独立配置 —— 方便「战斗 BGM 压低一点」这类调音。
    /// </summary>
    [CreateAssetMenu(menuName = "STG/Audio/BGM Track", fileName = "NewBgmTrack", order = 102)]
    public class BgmTrack : ScriptableObject
    {
        [Header("Clip")]
        [Tooltip("BGM 音频。一般是 ogg/mp3,建议 streaming 加载(Inspector 里选 Load Type)。")]
        public AudioClip Clip;

        [Header("Playback")]
        [Range(0f, 1f)]
        [Tooltip("默认音量倍率 [0..1]。会被 BusMixer 进一步缩放。")]
        public float DefaultVolume = 1f;

        [Tooltip("是否循环播放。大多数 BGM = true。")]
        public bool Loop = true;

        [Header("Routing")]
        [Tooltip("路由到哪个总线。空 = 走 BusMixer 默认 Bgm 总线。")]
        public AudioBus Bus;

        [Tooltip("播放时是否从指定时间点开始(秒)。\n" +
                 "0 = 从头开始。可用于「跳过 intro 直接进 loop 段」等用途。")]
        public float StartTime = 0f;

        [Tooltip("友好名称,Inspector 显示用。")]
        public string DisplayName;
    }
}
