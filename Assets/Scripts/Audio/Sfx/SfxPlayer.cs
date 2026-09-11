using UnityEngine;

namespace ShinySTG.Audio
{
    /// <summary>
    /// 池化的单个 AudioSource 封装 —— SfxRouter 内部使用,外部不感知。
    ///
    /// 设计要点:
    ///   - 每个 SfxPlayer 是一个挂载在 AudioSystem 子物体下的 GameObject,
    ///     持有一个 AudioSource 组件,用于短促 SFX 播放。
    ///   - 支持跟随 Parent(敌人死亡音跟敌人移动)和绝对 Position(子弹命中的世界点)。
    ///   - 播放结束后回收到 SfxRouter 的可用池。
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        AudioSource _source;
        SfxRouter _owner;
        SfxRequest _activeRequest;
        float _endTime;        // PlayOneShot 不支持时长查询,用 clip.length / pitch 估算
        Transform _followTarget;
        Vector3 _followOffset;

        /// <summary>当前正在播放的 cue(供 SfxRouter 做同 cue voice 数限制)。</summary>
        public SfxCue ActiveCue => _activeRequest?.Cue;
        /// <summary>当前正在播放的优先级(供 SfxRouter 池满时挑最低优先级的丢)。</summary>
        public int ActivePriority => _activeRequest?.Cue != null ? _activeRequest.Cue.Priority : 0;

        void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;  // 2D 音(STG 项目视角固定,不需要 3D)
        }

        /// <summary>
        /// 由 SfxRouter 调用:配置并开始播放。
        /// </summary>
        public void Play(SfxRouter owner, SfxRequest req)
        {
            _owner = owner;
            _activeRequest = req;
            gameObject.SetActive(true);

            _source.clip = req.Clip;
            _source.volume = Mathf.Clamp01(req.Volume);
            _source.pitch = Mathf.Clamp(req.Pitch, 0.1f, 3f);
            _source.loop = req.Loop;

            // 路由到 Bus(优先 MixerGroup,空时用 source.volume 缩放已由 BusMixer 处理)
            if (req.Cue != null && req.Cue.Bus != null && req.Cue.Bus.MixerGroup != null)
                _source.outputAudioMixerGroup = req.Cue.Bus.MixerGroup;
            else
                _source.outputAudioMixerGroup = null;

            _source.priority = req.Cue != null ? req.Cue.Priority : 128;

            // 定位
            if (req.Parent != null)
            {
                _followTarget = req.Parent;
                _followOffset = transform.position - req.Parent.position;
                transform.position = req.Parent.position + _followOffset;
            }
            else if (req.Position.HasValue)
            {
                _followTarget = null;
                transform.position = req.Position.Value;
            }
            else
            {
                _followTarget = null;
                // 保持当前位置(通常是 AudioSystem 根位置)
            }

            _source.Play();
            // 估算结束时间(供 Update 自动回收)。PlayOneShot 不用,但这里走 _source.Play() 才能改 pitch。
            float duration = (req.Clip != null ? req.Clip.length : 0f) / Mathf.Max(0.0001f, _source.pitch);
            _endTime = Time.unscaledTime + duration;
        }

        /// <summary>由外部调用:停止这个 voice(供循环音停止用)。</summary>
        public void StopImmediate()
        {
            _source.Stop();
            Release();
        }

        void Update()
        {
            // 跟随 Parent(敌人死亡音等需要跟随移动的场景)
            if (_followTarget != null)
                transform.position = _followTarget.position + _followOffset;

            // 非循环音 + 已到结束时间 → 回收
            if (!_source.loop && Time.unscaledTime >= _endTime && !_source.isPlaying)
                Release();
        }

        void Release()
        {
            _source.Stop();
            _source.clip = null;
            _followTarget = null;
            _activeRequest = null;
            gameObject.SetActive(false);
            _owner?.Return(this);
        }
    }
}
