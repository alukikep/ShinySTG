using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace ShinySTG.Audio
{
    /// <summary>
    /// 总线音量管理器 —— 维护每条总线的「当前音量」,并在 SFX/Music 播放时按需应用。
    ///
    /// 双路径实现(对齐用户场景:项目未建 AudioMixer,但接口预留):
    ///   - 若 Bus.MixerGroup != null → 走 AudioMixer.SetFloat("volume", dB),最标准
    ///   - 否则 → 由调用方在写入 _source.volume 时乘上 GetCurrentVolume(BusKind)
    ///     (SfxRouter 在 _source.volume = req.Volume * busVol;MusicPlayer 同理)
    ///
    /// 持久化:
    ///   - 若 AudioBus.PlayerPrefsKey 不空,把用户音量写到 PlayerPrefs,启动时读回。
    ///   - 跨场景/跨重启保留(对齐其他系统配置项)。
    /// </summary>
    public class BusMixer
    {
        readonly Dictionary<AudioBusKind, AudioBus> _buses = new();
        readonly Dictionary<AudioBusKind, float> _currentVolume = new();
        readonly Dictionary<AudioBusKind, bool> _muted = new();

        public void Register(AudioBus bus)
        {
            if (bus == null) return;
            var kind = ResolveKind(bus);
            _buses[kind] = bus;
            if (!_currentVolume.ContainsKey(kind))
                _currentVolume[kind] = LoadPersistedVolume(bus);
            if (!_muted.ContainsKey(kind))
                _muted[kind] = LoadPersistedMute(bus);
        }

        /// <summary>取当前某总线的最终音量(0..1)。</summary>
        public float GetCurrentVolume(AudioBusKind kind)
        {
            if (_muted.TryGetValue(kind, out bool m) && m) return 0f;
            return _currentVolume.TryGetValue(kind, out float v) ? v : 1f;
        }

        /// <summary>设置某总线音量(0..1)。Master=0 时全部静音。</summary>
        public void SetVolume(AudioBusKind kind, float vol01)
        {
            vol01 = Mathf.Clamp01(vol01);
            _currentVolume[kind] = vol01;
            if (_buses.TryGetValue(kind, out var bus)) PersistVolume(bus, vol01);
            ApplyToMixer(kind, vol01);
        }

        /// <summary>单独静音某总线(不影响其他总线)。</summary>
        public void SetMuted(AudioBusKind kind, bool muted)
        {
            _muted[kind] = muted;
            if (_buses.TryGetValue(kind, out var bus)) PersistMute(bus, muted);
            ApplyToMixer(kind, GetCurrentVolume(kind));
        }

        /// <summary>把所有已注册总线的当前音量应用到 Unity AudioMixer(SetFloat)。</summary>
        public void ApplyAllToMixer()
        {
            foreach (var kv in _buses)
                ApplyToMixer(kv.Key, GetCurrentVolume(kv.Key));
        }

        void ApplyToMixer(AudioBusKind kind, float vol01)
        {
            if (!_buses.TryGetValue(kind, out var bus)) return;
            if (bus.MixerGroup == null || bus.MixerGroup.audioMixer == null) return;
            // AudioMixer 参数名约定:"<BusName>Volume"(可在 Inspector 自定义 Param)
            // 项目未建 AudioMixer 时这步被跳过,fallback 到 source.volume 路径
            string param = $"{bus.BusName}Volume";
            // ToDecibels:0 → -80dB,1 → 0dB(标准 logarithmic mapping)
            float db = vol01 > 0.0001f ? Mathf.Log10(vol01) * 20f : -80f;
            bus.MixerGroup.audioMixer.SetFloat(param, db);
        }

        // ─── 持久化 ─────────────────────────────────────────────────────────

        static float LoadPersistedVolume(AudioBus bus)
        {
            if (string.IsNullOrEmpty(bus.PlayerPrefsKey)) return bus.DefaultVolume;
            return PlayerPrefs.GetFloat(bus.PlayerPrefsKey + ".vol", bus.DefaultVolume);
        }

        static void PersistVolume(AudioBus bus, float vol)
        {
            if (string.IsNullOrEmpty(bus.PlayerPrefsKey)) return;
            PlayerPrefs.SetFloat(bus.PlayerPrefsKey + ".vol", vol);
            PlayerPrefs.Save();
        }

        static bool LoadPersistedMute(AudioBus bus)
        {
            if (string.IsNullOrEmpty(bus.PlayerPrefsKey)) return false;
            return PlayerPrefs.GetInt(bus.PlayerPrefsKey + ".mute", 0) != 0;
        }

        static void PersistMute(AudioBus bus, bool muted)
        {
            if (string.IsNullOrEmpty(bus.PlayerPrefsKey)) return;
            PlayerPrefs.SetInt(bus.PlayerPrefsKey + ".mute", muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>把 AudioBus.BusName 字符串解析为 AudioBusKind 枚举。</summary>
        public static AudioBusKind ResolveKind(AudioBus bus)
        {
            if (bus == null) return AudioBusKind.Master;
            switch (bus.BusName)
            {
                case "Bgm":    return AudioBusKind.Bgm;
                case "Sfx":    return AudioBusKind.Sfx;
                case "UI":     return AudioBusKind.UI;
                case "Master": return AudioBusKind.Master;
                default:       return AudioBusKind.Master;  // 未知 → 兜底 Master
            }
        }
    }
}
