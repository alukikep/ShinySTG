using System;
using SerializeReferenceEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 一条敌人行为的时间轴节点。所有可配置的行为都继承自它。
    /// ShooterEnemy 会按顺序执行 Actions,每条持续 DurationConfig 抽样时长 秒后自动进入下一条。
    ///
    /// <para>★ Duration 字段改造(vX 起):</para>
    /// <para>
    /// 老版本(改之前):基类直接持有 <c>public float Duration</c> 字段,持续时长写死。
    /// 新版本:基类持有多态 <see cref="ActionDurationConfig"/> 字段(Inspector 下拉选),
    /// 用户可选 Fixed(精确值,与旧 float 等价)/ Random Range(区间随机,每次进入行为抽一次)。
    /// </para>
    /// <para>
    /// 用户视角:Inspector 里看不到 float Duration 字段了 —— 多态字段完全替代。
    /// 老 .asset 兼容:旧 <c>Duration: X</c> 字段通过 <see cref="_legacyDuration"/>
    /// ([FormerlySerializedAs("Duration")])迁移,SR 字段为 null 时 Runtime 兜底用 legacy 值。
    /// </para>
    /// </summary>
    [Serializable]
    public abstract class EnemyAction
    {
        // ─────────────────────────────────────────────────────────
        // ★ Duration 字段已升级为多态策略(见 ActionDurationConfig.cs)
        //   - Inspector 里用户看到的就是 DurationConfig 下拉选
        //   - 默认实例 = FixedActionDuration { Value = 1f },与旧默认值 1f 等价
        //   - 抽样结果缓存在 _currentDuration,本条 Action 期间固定不变
        // ─────────────────────────────────────────────────────────

        [Tooltip("该行为持续多少秒后停止,自动进入下一条。\n" +
                 "走 SR 多态下拉:\n" +
                 "  - Duration/Fixed        : 精确值(默认,等价旧 float Duration)\n" +
                 "  - Duration/Random Range : 区间随机,每次进入行为时抽一次\n" +
                 "详见 Assets/Scripts/Enemy/AI/ActionDurationConfig.cs。\n" +
                 "抽样结果由 BehaviorFlowRuntime 在切到本 Action 时调用 Sample() 计算一次,\n" +
                 "本条 Action 期间固定,详见 CurrentDuration 属性。")]
        [SerializeReference, SR]
        public ActionDurationConfig DurationConfig = new FixedActionDuration();

        /// <summary>
        /// 老 .asset 兼容兜底字段 —— 仅在 <see cref="DurationConfig"/> 为 null 时使用。
        /// ★ 通过 <see cref="FormerlySerializedAsAttribute"/> 把旧 <c>Duration</c> 字段的 float 值
        ///   迁移到此字段(类型兼容 float → float)。
        /// ★ Inspector 里隐藏(用户视角看不到,只有 SR 多态字段)。
        /// ★ 不删是底线 —— 删了老 .asset 的 Duration: X 字段会被 Unity 静默忽略,X 非 1 的
        ///   .asset 行为会变化(变 1)。保留字段 + Runtime 兜底 = 100% 兼容。
        /// ★ 未来(v2):若所有 .asset 都已迁移到 SR,可删此字段并简化 Runtime 逻辑。
        /// </summary>
        [SerializeField, HideInInspector]
        [FormerlySerializedAs("Duration")]
        float _legacyDuration = 1f;

        /// <summary>
        /// 本条 Action 当前实际生效的 Duration(秒),由 <see cref="BehaviorFlowRuntime"/>
        /// 在切到本 Action 时调用 <see cref="ActionDurationConfig.Sample"/> 计算一次后写入。
        /// 本条 Action 期间固定不变。
        /// ★ 供子类 OnTick 内读(若需要根据剩余时间做内部逻辑)。
        /// </summary>
        public float CurrentDuration => _currentDuration;

        [NonSerialized] float _currentDuration = 1f;

        /// <summary>由 <see cref="BehaviorFlowRuntime.AdvanceTo"/> 在 OnEnter 之前调用,写入抽样结果。</summary>
        internal void SetCurrentDuration(float v) => _currentDuration = v;

        /// <summary>
        /// Runtime 拿实际 Duration —— 优先用 SR 字段,否则用 legacy 兜底。
        /// 由 <see cref="BehaviorFlowRuntime"/> 调用。
        /// </summary>
        internal float ResolveDuration()
        {
            return DurationConfig != null ? DurationConfig.Sample() : Mathf.Max(0f, _legacyDuration);
        }

        /// <summary>进入该行为时调用一次(可做初始化/重置状态)。</summary>
        public virtual void OnEnter(Transform enemy) { }

        /// <summary>每帧调用,dt = Time.deltaTime。</summary>
        public virtual void OnTick(Transform enemy, float dt) { }

        /// <summary>该行为时间到,即将切换到下一条时调用一次(可清理状态)。</summary>
        public virtual void OnExit(Transform enemy) { }
    }
}
