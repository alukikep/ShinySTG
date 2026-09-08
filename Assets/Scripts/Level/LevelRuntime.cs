using System.Collections.Generic;
using UnityEngine;

namespace ShinySTG.Level
{
    /// <summary>
    /// 关卡运行时的"状态持有者"(类比 BehaviorFlowRuntime / BossController._current)。
    /// 纯类,不是 MonoBehaviour;时间推进 + 触发判定 + 活跃单位追踪都在这里。
    ///
    /// 暴露 Track / Untrack 供 SpawnEntry 子类登记自己生成的 GameObject,
    /// 未来关卡清理 / 失败判定(全清超时 / 玩家死亡)直接遍历 ActiveUnits 即可。
    ///
    /// 持续条目支持(SpawnEntry.Duration > 0):
    ///   - Tick 时:已触发且 Duration > 0 的条目进入"持续态",每帧调 OnTick
    ///   - 到 Duration:调 OnEnd + 从持续列表移除
    ///   - 子类 OnTrigger 第一行可决定是否把自己 register 进去(给玩家 OnTick 用)
    ///     —— 默认行为见 SpawnEntry.OnTick 注释
    /// </summary>
    public class LevelRuntime
    {
        readonly LevelDefinition _def;
        readonly bool[] _fired;   // 每条是否已触发(OneShot 用)
        readonly List<GameObject> _alive = new();
        readonly List<SpawnEntry> _sustained = new();  // 持续中的条目

        public LevelDefinition Definition => _def;
        public float Elapsed { get; private set; }
        public IReadOnlyList<GameObject> ActiveUnits => _alive;
        public IReadOnlyList<SpawnEntry> SustainedEntries => _sustained;

        public LevelRuntime(LevelDefinition def)
        {
            _def = def;
            _fired = new bool[def?.Entries?.Length ?? 0];
        }

        /// <summary>LevelController.Update 每帧调用。推进时间 + 扫描条目触发 + 持续条目 tick。</summary>
        public void Tick(float dt)
        {
            // 兜底清理:已被外部 Destroy 但没调 Untrack 的 GameObject 会留下 null 引用,
            // 每帧开头顺手清掉,避免 _alive 越长越大、Reset 时残留历史。
            if (_alive.Count > 0) _alive.RemoveAll(g => g == null);

            Elapsed += dt;
            var entries = _def?.Entries;
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    var e = entries[i];
                    if (e == null) continue;
                    if (Elapsed < e.TriggerTime) continue;
                    if (!e.ShouldTrigger(_fired[i])) continue;

                    _fired[i] = true;
                    e.OnTrigger(this, _def);

                    // 持续型条目:OnTrigger 内通常已调 RegisterSustained 把自己加进去;
                    // 这里做兜底 —— 如果 OnTrigger 之后子类没注册,而又有 Duration,自动注册一次。
                    // 防止子类忘记 RegisterSustained 也能跑(OnTick 至少能拿到时间)。
                    if (e.HasDuration && !_sustained.Contains(e))
                        _sustained.Add(e);
                }
            }

            // 持续条目:倒序遍历以便安全 RemoveAt(到 Duration 的会移除)
            if (_sustained.Count > 0)
            {
                for (int i = _sustained.Count - 1; i >= 0; i--)
                {
                    var e = _sustained[i];
                    if (e == null || _def == null) { _sustained.RemoveAt(i); continue; }

                    // 找到原条目在 Entries 中的索引(避免依赖引用)
                    int origIdx = -1;
                    for (int k = 0; k < _def.Entries.Length; k++)
                        if (_def.Entries[k] == e) { origIdx = k; break; }
                    if (origIdx < 0) { _sustained.RemoveAt(i); continue; }

                    float t = Elapsed - e.TriggerTime;
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
        }

        /// <summary>
        /// 持续型条目自己调:把自己加进 _sustained 让 LevelRuntime 每帧调 OnTick。
        /// 通常在 OnTrigger 第一行调一次即可(防止重复,内部判 Contains)。
        ///
        /// virtual 是为了让 Editor Preview 的 StubLevelRuntime 接管(它有自己的 _sustained 列表
        /// + 由 LevelEditorPlayer.TickSustained 推动,不走 LevelRuntime.Tick)。
        /// </summary>
        public virtual void RegisterSustained(SpawnEntry entry)
        {
            if (entry == null) return;
            if (!_sustained.Contains(entry)) _sustained.Add(entry);
        }

        /// <summary>SpawnEntry.OnTrigger 用:登记一个生成出来的单位。可被 Editor Preview 子类 override。</summary>
        public virtual void Track(GameObject go)
        {
            if (go != null && !_alive.Contains(go)) _alive.Add(go);
        }

        /// <summary>外部(敌人自毁 / Boss 死亡回调)用:把已死的单位从活跃列表移除。可被 override。</summary>
        public virtual void Untrack(GameObject go)
        {
            if (go != null) _alive.Remove(go);
        }

        /// <summary>
        /// 清空活跃单位列表(默认同时销毁 GameObject)。
        /// 关卡完成 / 玩家失败 / 切换关卡时用来"清场"。
        /// destroyGameObjects:false = 仅清空列表保留 GameObject(给"保留敌人,只重置状态"这类特例用)。
        ///
        /// 销毁走 Object.Destroy(延迟到帧末)而不是 DestroyImmediate —— Play Mode 里
        /// Update 中调 DestroyImmediate 会触发不可预期的 GC + 引用问题。
        ///
        /// 遍历前 ToArray() 防御性 snapshot:虽然目前没有"销毁时回调 Untrack"的路径,
        /// 但保留 snapshot 模式更稳,未来加 OnDestroy→Untrack 也不会 ConcurrentModification。
        /// </summary>
        public void ClearAliveUnits(bool destroyGameObjects = true)
        {
            if (destroyGameObjects && _alive.Count > 0)
            {
                var snapshot = _alive.ToArray();
                foreach (var go in snapshot)
                    if (go != null) UnityEngine.Object.Destroy(go);
            }
            _alive.Clear();
        }

        /// <summary>
        /// 强制重置:状态归零 + 销毁活跃单位 + 清空持续条目。
        /// 给 LevelController.BeginLevel / ReloadLevel 等接口用。
        /// 默认会销毁活跃单位 GameObject(语义对齐 Editor Preview 的 Stop —— "不留尾巴");
        /// 不想销毁时传 destroyGameObjects:false(罕见用例)。
        /// </summary>
        public void Reset(bool destroyGameObjects = true)
        {
            Elapsed = 0f;
            for (int i = 0; i < _fired.Length; i++) _fired[i] = false;
            _sustained.Clear();
            ClearAliveUnits(destroyGameObjects);
        }
    }
}