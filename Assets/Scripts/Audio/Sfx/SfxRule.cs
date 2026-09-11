using System;
using System.Collections.Generic;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.Audio
{
    /// <summary>
    /// SfxCue.Rules 多态模块 —— 对一个 SFX 请求(已选定 clip)做可插拔处理。
    ///
    /// ★ Pipeline 模型(与 FireExtension 对齐)★
    ///
    /// SfxCue.Rules 是一个 SfxRule[] 数组,数组里每个模块按顺序串行处理:
    ///   request = SfxRequest { clip, volume, pitch, loop, position, parent, voice }
    ///   for rule in Rules (按数组顺序):
    ///       request = rule.Process(request)
    ///   final = request (由 SfxRouter 实际播放)
    ///
    /// 每个规则接收「上一步产出的 request」,产出「下一步的 request」。
    /// 模块可以修改字段(随机 pitch、随机选 clip、加 cooldown),也可以 throw 跳过播放。
    ///
    /// 数组顺序就是执行顺序,改顺序 = 改语义。例:
    ///   [RandomPick, PitchVariation]     → 先抽 clip → 再在抽到的 clip 上叠 pitch 抖动
    ///   [PitchVariation, RandomPick]     → pitch 抖动(所有 clip 共用)→ 再抽 clip
    ///   []                                → 直接播放 Clips[0](等价于无规则)
    /// </summary>
    [Serializable]
    public abstract class SfxRule
    {
        /// <summary>
        /// 处理一次 SFX 请求,返回修改后的请求。子类可 throw / 直接返回原 request / 返回新 request。
        /// </summary>
        public abstract SfxRequest Process(SfxRequest request);
    }

    /// <summary>
    /// 一次 SFX 调用的参数包(SfxCue 默认值 + 调用方覆盖值 + 路由中间状态)。
    /// Pipeline 每一道规则都拿这个对象,改字段就改输出。
    /// </summary>
    public class SfxRequest
    {
        /// <summary>即将播放的 clip(可能已被 RandomPickRule 替换)。</summary>
        public AudioClip Clip;
        /// <summary>最终音量(已乘 DefaultVolume × 调用方 volumeMul)。</summary>
        public float Volume = 1f;
        /// <summary>最终 pitch(已乘 DefaultPitch × 调用方 pitch)。</summary>
        public float Pitch = 1f;
        /// <summary>是否循环。</summary>
        public bool Loop;
        /// <summary>世界坐标(2D 监听时通常为发声点;可为 null = 跟随 Listener)。</summary>
        public Vector2? Position;
        /// <summary>跟随的 GameObject(子弹命中的弹、敌人死亡的本敌人;可为 null = 一次性 2D 音)。</summary>
        public Transform Parent;
        /// <summary>调用的 cue(规则可读 cue 字段做条件)。</summary>
        public SfxCue Cue;
        /// <summary>调用的源 MonoBehaviour(规则可读,通常用于 GetBusOverride 等)。</summary>
        public MonoBehaviour Caller;

        public SfxRequest Clone() => (SfxRequest)MemberwiseClone();
    }

    // ─── 内置规则 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 随机抽取 clip:Cue.Clips 数组里随机选一个(可避开上一次抽到的,避免重复)。
    /// </summary>
    [Serializable, SRName("Rule/Random Pick")]
    public class RandomPickRule : SfxRule
    {
        [Tooltip("true = 避开上一次播放的 clip(避免连抽同一个)。\n" +
                 "Clips 长度 <= 1 时此设置无效。")]
        public bool AvoidRepeatLast = true;

        [NonSerialized] int _lastIndex = -1;

        public override SfxRequest Process(SfxRequest request)
        {
            var clips = request.Cue != null ? request.Cue.Clips : null;
            if (clips == null || clips.Length == 0) return request;
            if (clips.Length == 1) { request.Clip = clips[0]; return request; }

            int idx;
            if (AvoidRepeatLast && _lastIndex >= 0 && _lastIndex < clips.Length)
            {
                // 避开上一次 —— 用 do-while 直到不等于上次
                do { idx = UnityEngine.Random.Range(0, clips.Length); }
                while (idx == _lastIndex);
            }
            else
            {
                idx = UnityEngine.Random.Range(0, clips.Length);
            }
            _lastIndex = idx;
            request.Clip = clips[idx];
            return request;
        }
    }

    /// <summary>
    /// Pitch 抖动:在 [1 - Range, 1 + Range] 范围内随机一个 pitch,叠加到当前 pitch 上(乘)。
    /// </summary>
    [Serializable, SRName("Rule/Pitch Variation")]
    public class PitchVariationRule : SfxRule
    {
        [Range(0f, 1f)]
        [Tooltip("抖动幅度。例如 0.1 = [0.9, 1.1] 范围随机。0 = 不变。")]
        public float Range = 0.05f;

        public override SfxRequest Process(SfxRequest request)
        {
            if (Range <= 0f) return request;
            float jitter = 1f + UnityEngine.Random.Range(-Range, Range);
            request.Pitch *= jitter;
            return request;
        }
    }

    /// <summary>
    /// Cooldown 节流:同 cue 在 MinInterval 秒内最多播一次,多余调用直接吞掉。
    /// 与 SfxCue.Cooldown 字段的区别:Cooldown 字段由 SfxRouter 在调度前统一检查
    /// (全局最小间隔),本规则可以做更精细的节流(例如按节奏分组的「同一拍只播一次」)。
    /// </summary>
    [Serializable, SRName("Rule/Cooldown")]
    public class CooldownRule : SfxRule
    {
        [Tooltip("最小播放间隔(秒)。0.1 = 100ms 内同 cue 最多播一次。")]
        public float MinInterval = 0.05f;

        // key = SfxCue instance id,value = 下次允许播放的 Time.time
        static readonly Dictionary<int, float> _nextAllowedTime = new();

        public override SfxRequest Process(SfxRequest request)
        {
            if (request.Cue == null || MinInterval <= 0f) return request;
            int key = request.Cue.GetInstanceID();
            float now = Time.unscaledTime;  // unscaledTime —— 不受 timeScale=0 暂停影响(暂停菜单不应继续节流)
            if (_nextAllowedTime.TryGetValue(key, out float next) && now < next)
            {
                // 还在冷却 —— 不阻止播放(由 Pipeline 行为不合适),而是标记为 muted
                // (Volume=0 是最简实现,AudioSource.PlayOneShot 会播但听不见)。
                request.Volume = 0f;
            }
            _nextAllowedTime[key] = now + MinInterval;
            return request;
        }
    }
}
