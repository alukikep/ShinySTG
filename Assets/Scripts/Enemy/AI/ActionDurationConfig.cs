using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 「Action Duration 多态策略」抽象基类 —— 决定一条 <see cref="EnemyAction"/>
    /// 实际持续多长时间。
    ///
    /// <para>★ 设计动机:</para>
    /// <para>
    /// 项目现状(改之前):所有 <see cref="EnemyAction"/> 都从基类继承一个 <c>float Duration</c> 字段,
    /// 行为持续时长完全写死。Boss 节奏抖动 / 敌人巡逻时长随机 / 等待间隔抖动等场景策划只能
    /// 拆成「多条 Action 拼接」做近似,既冗长又不自然。
    /// </para>
    /// <para>
    /// 本抽象类把「持续多久」抽成 SR 多态字段(对照项目既有的 <see cref="BaseOffsetStrategy"/> /
    /// <see cref="ModifierStartTrigger"/> 套路):
    ///   - <see cref="FixedActionDuration"/> = 精确值(默认,等价旧 float Duration)
    ///   - <see cref="RandomRangeActionDuration"/> = 区间随机,每次进入 Action 时抽一次,期间固定
    /// </para>
    /// <para>
    /// 用户在 <see cref="EnemyAction.DurationConfig"/> 字段上下拉选 —— 与项目所有扩展点一致
    /// (SerializeReference + SRName)。
    /// </para>
    ///
    /// <para>★ 抽样时机(由 <see cref="BehaviorFlowRuntime"/> 控制):</para>
    /// <list type="bullet">
    ///   <item>每次 <c>BehaviorFlowRuntime.AdvanceTo</c> 切到该 Action 时,调一次 <see cref="Sample"/>。</item>
    ///   <item>抽样结果缓存在 <see cref="EnemyAction.CurrentDuration"/> 字段,本条 Action 期间固定不变。</item>
    ///   <item>Sequence / Loop 切到下一条 Action 时 → 重新抽样(若新策略是区间随机 → 抖动节奏)。</item>
    ///   <item>Parallel 内每个子 Action 独立抽样、互不影响(子 Action 各自记自己的 CurrentDuration)。</item>
    /// </list>
    ///
    /// <para>★ 字段约定(与 BulletModifier 一致):</para>
    /// <list type="bullet">
    ///   <item>值类型字段(float / int / bool)默认 Clone 即可,无额外开销。</item>
    ///   <item>per-instance 状态字段标 [NonSerialized],Clone 后自然归零。</item>
    ///   <item>引用类型字段(List / 自定义类)override <see cref="Clone"/> 深拷。</item>
    /// </list>
    /// </summary>
    [Serializable]
    public abstract class ActionDurationConfig
    {
        /// <summary>
        /// 返回本次要施加的 Duration(秒,必须 &gt;= 0)。
        /// 宿主(<see cref="BehaviorFlowRuntime"/>)在切到该 Action 时调一次,
        /// 之后本条 Action 期间固定不变(缓存在 <see cref="EnemyAction.CurrentDuration"/>)。
        /// </summary>
        public abstract float Sample();

        /// <summary>深拷。子类持有引用类型字段时 override;默认 MemberwiseClone 已覆盖纯值类型场景。</summary>
        public virtual ActionDurationConfig Clone() => (ActionDurationConfig)MemberwiseClone();
    }

    /// <summary>
    /// 精确值 Duration 策略 —— 直接返回 <see cref="Value"/>(秒)。
    ///
    /// <para>★ 对齐旧版 v1 行为:</para>
    /// <para>
    /// <c>EnemyAction.Duration = X</c> ≡ <c>EnemyAction.DurationConfig = FixedActionDuration { Value = X }</c>。
    /// Unity 在反序列化老 .asset 时,会把旧 <c>Duration: X</c> 的 float 值自动迁移到
    /// <see cref="FixedActionDuration.Value"/>(SR 字段的 Unity 兼容行为)。
    /// </para>
    ///
    /// <para>★ 典型用法:</para>
    /// <list type="bullet">
    ///   <item>所有内置 Action 的默认 Duration = 1f —— 默认实例 = <c>new FixedActionDuration { Value = 1f }</c>。</item>
    ///   <item>Boss 蓄力精确 2.5 秒:Value = 2.5f。</item>
    ///   <item>FireAction 一波固定 3 秒:Value = 3f。</item>
    /// </list>
    /// </summary>
    [Serializable, SRName("Duration/Fixed")]
    public class FixedActionDuration : ActionDurationConfig
    {
        [Tooltip("精确持续时长(秒)。\n" +
                 "  0  = 只执行一帧(下一帧立即进入下一条);\n" +
                 "  1  = 持续 1 秒(默认);\n" +
                 "  2.5= 持续 2.5 秒(Boss 蓄力 / 精确节拍)。\n" +
                 "内部用 Mathf.Max(0, Value) 钳位,负数按 0 处理。")]
        public float Value = 1f;

        public override float Sample() => Mathf.Max(0f, Value);
    }
/// <summary>
    /// 区间随机 Duration 策略 —— 在 <see cref="Min"/>(秒) ~ <see cref="Max"/>(秒)间均匀抽样一次。
    ///
    /// <para>★ 抽样时机:</para>
    /// <para>
    /// <see cref="BehaviorFlowRuntime"/> 在切到该 Action 时调一次 <see cref="Sample"/>,
    /// 之后本条 Action 期间固定不变。
    /// 多条 Action / Loop 多次循环各自独立抽样 → Boss 节奏每次都不同。
    /// </para>
    ///
    /// <para>★ 典型用法:</para>
    /// <list type="bullet">
    ///   <item>Boss 节奏混乱:<see cref="FireAction"/> DurationConfig = Random Range(2, 4) → 每波开火时长随机。</item>
    ///   <item>玩家反应窗口抖动:<see cref="WaitAction"/> DurationConfig = Random Range(0.5, 1.5) → 间隔不固定。</item>
    ///   <item>弹性巡逻:<see cref="MoveAction"/> DurationConfig = Random Range(3, 5) → 每段巡逻时长不固定。</item>
    ///   <item>伪随机弹幕间隔:FireAction DurationConfig = Random Range(0.8, 1.2) → 节奏自然。</item>
    /// </list>
    ///
    /// <para>★ 容错处理:</para>
    /// <list type="bullet">
    ///   <item>若 Min == Max,直接返回该值(避免极小区间噪声)。</item>
    ///   <item>若 Min &gt; Max(用户填反),直接返回 Min(不抛异常)。</item>
    ///   <item>负数 Min/Max:内部用 Mathf.Max(0f, ...) 钳位。</item>
    /// </list>
    /// </summary>
    [Serializable, SRName("Duration/Random Range")]
    public class RandomRangeActionDuration : ActionDurationConfig
    {
        [Tooltip("随机抽样区间下限(秒)。配合 Max 决定闭区间 [Min, Max]。\n" +
                 "  对称区间(如 Min = 0.8, Max = 1.2)效果最直观(节奏自然抖动);\n" +
                 "  非对称区间(如 Min = 2.0, Max = 4.0)可制造「长尾」节奏。")]
        public float Min = 1f;

        [Tooltip("随机抽样区间上限(秒)。配合 Min 决定闭区间 [Min, Max]。\n" +
                 "  Min == Max 时 Sample 直接返回该值(无随机);\n" +
                 "  Min >  Max 时 Sample 返回 Min(用户填反的容错)。")]
        public float Max = 2f;

        public override float Sample()
        {
            // 容错 1:用户填反 / 极小区间,直接返回确定值,避免 Random.Range 抛异常或噪声
            if (Min >= Max) return Mathf.Max(0f, Min);

            // 容错 2:负数钳位 —— UnityEngine.Random.Range 接受负数会直接返回,
            //   但 Duration 必须 >= 0(否则行为永远不结束),统一钳位更稳
            float sampled = UnityEngine.Random.Range(Min, Max);
            return Mathf.Max(0f, sampled);
        }
    }
}