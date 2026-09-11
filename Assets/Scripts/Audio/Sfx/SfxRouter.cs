using System.Collections.Generic;
using UnityEngine;

namespace ShinySTG.Audio
{
    /// <summary>
    /// SFX 路由 + 限流器 —— 由 AudioSystem 持有,处理一次 SfxCue 调用:
    ///   1. 检查 cue 是否空(无 clip)→ 直接返回
    ///   2. 检查 cue.MaxVoices 同 cue 限流(超出丢最老的低优先级 voice)
    ///   3. 检查 cue.Cooldown 全局最小间隔
    ///   4. 构造 SfxRequest,按 Rules 数组串行 Pipeline 处理
    ///   5. 从池取一个 SfxPlayer,Play(request)
    ///
    /// 对齐项目惯例:
    ///   - 池分桶:同 BulletPool 按 prefab 分桶类似,这里按 cue 实例分桶
    ///     (避免不同 cue 互相覆盖,但共享一个物理 AudioSource 池)。
    ///   - 限流策略:与"同 cue 最多 N voice"对齐用户需求。
    /// </summary>
    public class SfxRouter
    {
        readonly AudioSystem _owner;
        readonly List<SfxPlayer> _allPlayers = new();        // 所有创建过的 SfxPlayer
        readonly Stack<SfxPlayer> _available = new();        // 当前空闲的 SfxPlayer
        readonly Dictionary<int, List<SfxPlayer>> _activeByCue = new();  // 按 cue instance id 索引活跃 voice

        // cue → 下次允许播放的 Time.unscaledTime(全局 cooldown 检查)
        readonly Dictionary<int, float> _cooldownUntil = new();

        public SfxRouter(AudioSystem owner) { _owner = owner; }

        /// <summary>
        /// 播放一个 cue。可指定世界坐标 / Parent / 临时音量倍率 / 临时 pitch。
        /// </summary>
        public void Play(SfxCue cue, Vector2? position = null, Transform parent = null,
                         float volumeMul = 1f, float pitch = 1f, MonoBehaviour caller = null)
        {
            if (cue == null || cue.IsEmpty) return;

            // 1. 全局 cooldown 检查
            int cueId = cue.GetInstanceID();
            float now = Time.unscaledTime;
            if (_cooldownUntil.TryGetValue(cueId, out float cdUntil) && now < cdUntil)
                return;
            _cooldownUntil[cueId] = now + Mathf.Max(0f, cue.Cooldown);

            // 2. 同 cue 限流:超出 MaxVoices → 丢最老且优先级最低的 voice
            if (cue.MaxVoices > 0)
            {
                if (!_activeByCue.TryGetValue(cueId, out var activeList))
                {
                    activeList = new List<SfxPlayer>(cue.MaxVoices);
                    _activeByCue[cueId] = activeList;
                }
                if (activeList.Count >= cue.MaxVoices)
                {
                    // 找到最老的 voice 释放(SfxRouter.Return 会从 activeList 移除)
                    SfxPlayer oldest = activeList[0];
                    // 简化:如果 oldest 优先级比新请求低(或相同)才丢 —— 否则直接吞掉本次请求
                    // 但新请求的优先级就是 cue.Priority,固定。所以策略 = 直接丢最老的。
                    oldest.StopImmediate();
                }
            }

            // 3. 构造 request,跑 Pipeline
            var req = new SfxRequest
            {
                Cue = cue,
                Clip = cue.Clips[0],  // 默认第一个 clip,Rules 可改
                Volume = cue.DefaultVolume * volumeMul,
                Pitch = cue.DefaultPitch * pitch,
                Loop = cue.Loop,
                Position = position,
                Parent = parent,
                Caller = caller,
            };
            if (cue.Rules != null)
            {
                for (int i = 0; i < cue.Rules.Length; i++)
                {
                    var rule = cue.Rules[i];
                    if (rule != null) req = rule.Process(req);
                    if (req == null || req.Volume <= 0f) return;  // 规则要求跳过播放
                }
            }
            if (req.Clip == null) return;  // 没 clip(可能规则清掉了)

            // 4. 从池取 SfxPlayer 并播放
            var player = AcquirePlayer();
            player.Play(this, req);

            // 5. 登记到 active 列表(供后续限流检查)
            if (cue.MaxVoices > 0)
                _activeByCue[cueId].Add(player);
        }

        /// <summary>
        /// 停止某 cue 全部正在播放的 voice(供「关卡结束清场」「boss 死亡 silence」等用)。
        /// </summary>
        public void StopAll(SfxCue cue)
        {
            if (cue == null) return;
            if (!_activeByCue.TryGetValue(cue.GetInstanceID(), out var list)) return;
            // 拷贝一份,避免 StopImmediate 中途修改列表
            var snapshot = new List<SfxPlayer>(list);
            for (int i = 0; i < snapshot.Count; i++)
                snapshot[i].StopImmediate();
        }

        /// <summary>停止所有 SFX(场景切换、暂停菜单用)。</summary>
        public void StopAll()
        {
            var snapshot = new List<SfxPlayer>(_allPlayers);
            for (int i = 0; i < snapshot.Count; i++)
            {
                var p = snapshot[i];
                if (p != null && p.gameObject.activeSelf) p.StopImmediate();
            }
        }

        SfxPlayer AcquirePlayer()
        {
            // 清理栈顶已销毁的(场景切换兜底)
            while (_available.Count > 0)
            {
                var p = _available.Pop();
                if (p != null) { p.gameObject.SetActive(true); return p; }
            }
            // 没有空闲 → 新建一个
            var go = new GameObject($"SfxPlayer_{_allPlayers.Count}");
            go.transform.SetParent(_owner.transform, false);
            var player = go.AddComponent<SfxPlayer>();
            _allPlayers.Add(player);
            return player;
        }

        /// <summary>由 SfxPlayer.Release 调用 —— 归还到池,从 active 列表摘除。</summary>
        internal void Return(SfxPlayer player)
        {
            if (player == null) return;
            // 从 active 列表里摘除
            if (player.ActiveCue != null)
            {
                int key = player.ActiveCue.GetInstanceID();
                if (_activeByCue.TryGetValue(key, out var list))
                    list.Remove(player);
            }
            _available.Push(player);
        }
    }
}
