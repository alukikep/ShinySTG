using UnityEngine;
using UnityEngine.Audio;

namespace ShinySTG.Audio
{
    /// <summary>
    /// 音频总线(Bus)配置 —— 把 SFX/BGM/UI/环境音分组管理音量。
    ///
    /// 设计要点:
    ///   - 兼容 Unity 内置 AudioMixer(项目目前未建,但留好接口以便后续启用)。
    ///   - 没设 AudioMixerGroup 时,BusMixer fallback 到「按 AudioSource.volume 缩放」,
    ///     保证零配置也能用。
    ///   - PlayerPrefsKey 不为空时,BusMixer 会自动把用户设置的音量写到 PlayerPrefs,
    ///     跨场景 / 跨重启保留。
    ///
    /// 使用:
    ///   - AudioMix.SetBusVolume(AudioBus.Sfx, 0.7f);
    ///   - AudioMix.GetBusVolume(AudioBus.Sfx);
    /// </summary>
    [CreateAssetMenu(menuName = "STG/Audio/Audio Bus", fileName = "NewAudioBus", order = 104)]
    public class AudioBus : ScriptableObject
    {
        [Header("Routing")]
        [Tooltip("Unity AudioMixer 中的目标 Group。可选 —— 不填则用 AudioSource.volume 缩放实现总线。\n" +
                 "项目目前未建 AudioMixer,先留空,后续可在 Project 里 Create → Audio Mixer 补上。")]
        public AudioMixerGroup MixerGroup;

        [Tooltip("总线标识符 —— 编程引用本总线的常量名(如 Sfx/Bgm/UI/Master),代码里通过 name 索引。\n" +
                 "默认 Master 是兜底总线,所有未指定 Bus 的 cue 走它。")]
        public string BusName = "Master";

        [Header("Persistence")]
        [Tooltip("PlayerPrefs 存储键。空 = 不持久化(每次启动用默认音量)。\n" +
                 "例如 \"audio.sfx\" / \"audio.bgm\" / \"audio.master\"。")]
        public string PlayerPrefsKey;

        [Range(0f, 1f)]
        [Tooltip("默认音量 [0..1]。")]
        public float DefaultVolume = 1f;
    }
}
