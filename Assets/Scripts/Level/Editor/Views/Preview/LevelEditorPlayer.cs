using System;
using System.Collections.Generic;
using ShinySTG.Level;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.Level.Editor.Views.Preview
{
    /// <summary>
    /// Editor 模式下的关卡 Preview 播放器。
    /// 通过实例化 SpawnEntry 的 prefab 来"模拟"关卡生成,所有临时 GameObject 挂到 _previewRoot。
    ///
    /// 实现要点:
    ///   - 不依赖 LevelRuntime 的 Tick(运行时类),避免 Editor 程序集反向引用运行时程序集的可序列化问题
    ///   - 直接遍历 Definition.Entries + 比对 TriggerTime + 调 SpawnEntry.OnTrigger
    ///   - 持续条目(Duration > 0)由 StubLevelRuntime.RegisterSustained 接管,Player 通过
    ///     StubLevelRuntime.TickSustained(dt) 推动(Stub 内部维护 _sustained 列表)
    /// </summary>
    public class LevelEditorPlayer : ILevelEditorPreview
    {
        LevelDefinition _def;
        bool[] _fired;
        readonly List<GameObject> _spawned = new();
        Transform _previewRoot;
        StubLevelRuntime _runtime;

        double _elapsed;
        bool   _playing;
        bool   _paused;

        public bool   IsPlaying   => _playing && !_paused;
        public float  CurrentTime => (float)_elapsed;

        public LevelEditorPlayer()
        {
            _fired = new bool[0];
        }

        public void Start(LevelDefinition def, float startTime = 0f)
        {
            Stop();
            _def = def;
            _current = this;  // 让 Stub.TickSustained 能拿到 elapsed
            if (_def == null) return;

            // 1. 建 PreviewRoot(临时父物体,放在场景里)
            var rootGo = new GameObject($"[LevelEditor Preview] {(_def.name ?? "<null>")}");
            rootGo.hideFlags = HideFlags.DontSave;
            _previewRoot = rootGo.transform;

            // 2. 初始化 _fired
            ResetFired();

            // 3. 建 Stub runtime(接管 Track/Untrack + Sustained 列表)
            _runtime = new StubLevelRuntime(_def);

            // 4. SetTime 把 _elapsed 调到 startTime(已触发的条目会重新触发)
            SetTime(startTime);
            _playing = true;
            _paused  = false;
        }

        public void Stop()
        {
            _playing = false;
            _paused  = false;
            _elapsed = 0f;
            ResetFired();
            if (_current == this) _current = null;

            // 销毁所有生成的临时实例
            foreach (var go in _spawned)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();

            if (_previewRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_previewRoot.gameObject);
                _previewRoot = null;
            }
            _runtime = null;
            _def = null;
        }

        public void Pause(bool paused) => _paused = paused;

        public void SetTime(float seconds)
        {
            _elapsed = seconds;

            // 回退:清空已触发 + 已生成实例,从头扫到 seconds
            foreach (var go in _spawned)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
            ResetFired();

            // 持续条目列表也清空(Stub 自己持有)
            _runtime?.ClearSustained();

            // 触发 _elapsed 之前所有应触发的条目
            if (_def?.Entries == null) return;
            for (int i = 0; i < _def.Entries.Length; i++)
            {
                var e = _def.Entries[i];
                if (e == null) continue;
                if (_elapsed < e.TriggerTime) continue;
                _fired[i] = true;
                TriggerOne(e);
            }
        }

        public void Tick(double deltaSeconds)
        {
            if (!_playing || _paused || _def?.Entries == null) return;

            _elapsed += deltaSeconds;
            for (int i = 0; i < _def.Entries.Length; i++)
            {
                var e = _def.Entries[i];
                if (e == null || _fired[i]) continue;
                if (_elapsed < e.TriggerTime) continue;
                _fired[i] = true;
                TriggerOne(e);
            }

            // 推动持续条目
            _runtime?.TickSustained((float)deltaSeconds);
        }

        // ─── 内部 ───────────────────────────────────────────────
        void ResetFired()
        {
            var n = _def?.Entries?.Length ?? 0;
            if (_fired.Length != n) Array.Resize(ref _fired, n);
            for (int i = 0; i < _fired.Length; i++) _fired[i] = false;
        }

        void TriggerOne(SpawnEntry e)
        {
            try
            {
                e.OnTrigger(_runtime, _def);
                // 注意:SpawnEntry.OnTrigger 可能没把生成的实例存到 _spawned(只 LevelRuntime.Track 持有)
                // StubLevelRuntime 内部把 Track 的对象保存下来,Stop 时统一清理。
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LevelEditor Preview] {e.GetType().Name} 触发失败: {ex.Message}");
            }
        }

        /// <summary>
        /// Preview 专用的 LevelRuntime 替身:只 override Track / Untrack,不计时。
        /// SpawnEntry.OnTrigger 实际用到的就这两个(以及 def 用于回调,这里不用)。
        /// </summary>
        class StubLevelRuntime : LevelRuntime
        {
            readonly List<GameObject> _tracked = new();
            readonly List<SpawnEntry> _sustained = new();

            public StubLevelRuntime(LevelDefinition def) : base(def) { }

            public override void Track(GameObject go)
            {
                if (go != null && !_tracked.Contains(go))
                    _tracked.Add(go);
            }

            public override void Untrack(GameObject go)
            {
                _tracked.Remove(go);
            }

            /// <summary>持续条目注册:SustainSpawnEntry.OnTrigger 通过 runtime.RegisterSustained 调到。</summary>
            public override void RegisterSustained(SpawnEntry entry)
            {
                if (entry == null) return;
                if (!_sustained.Contains(entry)) _sustained.Add(entry);
            }

            /// <summary>Player.Tick 调用:推动持续条目的 OnTick,到 Duration 调 OnEnd + 移除。</summary>
            public void TickSustained(float dt)
            {
                if (_sustained.Count == 0) return;

                // elapsed 来自 Player 自己;我们调 OnTick 时把 elapsed 传给 t 参数(LevelRuntime 协议)
                // 但 Player 的 elapsed 不暴露给 Stub。最简方案:Stub 自己读 Player 的 Elapsed。
                // —— Player 是 outer 类,通过构造注入比较绕。直接传 elapsed:
                var player = LevelEditorPlayer.Current;  // 静态引用,见下方
                float elapsed = player?.CurrentTime ?? 0f;

                for (int i = _sustained.Count - 1; i >= 0; i--)
                {
                    var e = _sustained[i];
                    if (e == null || !e.HasDuration) { _sustained.RemoveAt(i); continue; }
                    float t = elapsed - e.TriggerTime;
                    if (t >= e.Duration)
                    {
                        e.OnEnd(this);
                        _sustained.RemoveAt(i);
                    }
                    else
                    {
                        e.OnTick(this, t, dt);
                    }
                }
            }

            public void ClearSustained()
            {
                _sustained.Clear();
            }
        }

        // ─── Stub 用的"current Player"引用 ──────────────────
        // Stub.TickSustained 需要知道 elapsed,直接拿 Player.CurrentTime
        // (Player 不用传 this 给 Stub 的构造,避免循环依赖)
        static LevelEditorPlayer _current;
        public static LevelEditorPlayer Current => _current;
    }
}