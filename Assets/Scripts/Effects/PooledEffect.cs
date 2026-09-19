using UnityEngine;

namespace ShinySTG.Effects
{
    /// <summary>一次性粒子特效的池生命周期；无粒子特效按最大时长回收。</summary>
    public class PooledEffect : MonoBehaviour
    {
        [SerializeField, Min(0.01f), Tooltip("最长播放秒数；也用于防止循环粒子永久占用池。")]
        float _maxDuration = 10f;
        [SerializeField, Tooltip("每次特效正式播放时触发的音效；留空不播放。使用非循环 SfxCue，声音独立于特效回收。")]
        ShinySTG.Audio.SfxCue _playSfx;
        ParticleSystem[] _particles;
        EffectPool _pool;
        GameObject _prefab;
        bool _playing;
        public uint PlaybackVersion { get; private set; }
        public bool IsPlaybackActive(uint version) => this != null && isActiveAndEnabled && _playing && PlaybackVersion == version;
        protected float Elapsed { get; private set; }

        internal void Prepare(EffectPool pool, GameObject prefab, Bullet source)
        {
            PlaybackVersion++;
            _pool = pool;
            _prefab = prefab;
            Elapsed = 0f;
            _playing = false;
            if (_particles == null) _particles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particle in _particles)
            {
                // 回收只能由池负责，避免 prefab 的 StopAction 销毁或禁用实例。
                var main = particle.main;
                main.stopAction = ParticleSystemStopAction.None;
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            PrepareVisual(source);
        }

        protected virtual void PrepareVisual(Bullet source) { }

        internal void Play()
        {
            if (_playing) return;
            _playing = true;
            foreach (var particle in _particles)
                if (particle.gameObject.activeInHierarchy) particle.Play(false);
            // 不传 Parent，也不在 Release 时停止声音，让短暂残影的音效能自然结束。
            if (_playSfx != null)
                ShinySTG.Audio.AudioMix.PlaySfx(_playSfx, position: (Vector2)transform.position);
        }

        protected virtual bool TickVisual(float elapsed) => false;

        void Update()
        {
            if (!_playing) return;
            Elapsed += Time.deltaTime;
            bool finished = TickVisual(Elapsed);
            if (_particles.Length > 0)
            {
                bool alive = false;
                foreach (var particle in _particles) alive |= particle.IsAlive(true);
                finished |= !alive;
            }
            if (finished || Elapsed >= Mathf.Max(0.01f, _maxDuration)) Release();
        }

        public void Release()
        {
            if (!_playing) return;
            _playing = false;
            if (_pool != null) _pool.Return(this, _prefab);
            else Destroy(gameObject);
        }

        internal virtual void ResetPlayback()
        {
            _playing = false;
            Elapsed = 0f;
            foreach (var particle in _particles)
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            _pool = null;
            _prefab = null;
        }
    }
}
