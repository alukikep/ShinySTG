using UnityEngine;

namespace ShinySTG.Audio
{
    /// <summary>
    /// 单个 BGM 通道 —— 一个 AudioSource,负责播放一首 BGM。
    /// 由 MusicPlayer 持有 N 个通道(交叉淡化需要至少 2 个)。
    /// </summary>
    public class MusicChannel : MonoBehaviour
    {
        AudioSource _source;
        public AudioSource Source => _source;

        /// <summary>当前正在播放的 BgmTrack(可空)。</summary>
        public BgmTrack CurrentTrack { get; private set; }

        /// <summary>当前的目标音量(经过 BusMixer + DefaultVolume 计算后的最终值)。</summary>
        public float TargetVolume { get; private set; }

        void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
        }

        /// <summary>开始播放指定 track(立即切到目标音量)。</summary>
        public void Play(BgmTrack track, float volume)
        {
            CurrentTrack = track;
            TargetVolume = volume;
            _source.loop = track != null && track.Loop;
            _source.clip = track != null ? track.Clip : null;
            _source.outputAudioMixerGroup = (track != null && track.Bus != null && track.Bus.MixerGroup != null)
                ? track.Bus.MixerGroup : null;
            _source.volume = volume;
            if (_source.clip != null)
            {
                _source.time = track != null ? Mathf.Max(0f, track.StartTime) : 0f;
                _source.Play();
            }
        }

        /// <summary>仅修改目标音量(由 MusicPlayer 在淡入淡出中每帧调用)。</summary>
        public void SetTargetVolume(float v)
        {
            TargetVolume = v;
            _source.volume = v;
        }

        public void Stop()
        {
            _source.Stop();
            _source.clip = null;
            CurrentTrack = null;
            TargetVolume = 0f;
        }

        public bool IsPlaying => _source.isPlaying;
    }
}
