using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// <see cref="BulletModifier"/> 的「启动触发器」多态扩展 —— 决定 modifier 何时进入时间窗口。
///
/// <para>★ 设计动机:</para>
/// <para>
/// 项目现状(改之前):所有 modifier 共用基类的 <c>Delay</c> 字段,modifier 激活时机 = <c>Delay</c> 秒后。
/// 这无法对齐到敌人 AI 节奏 —— Boss 喊话、阶段切换、Parallel 容器内某条 Action 完成 等场景下,
/// 策划只能靠「凑 Delay 时间」做对位,既不准又难维护。
/// </para>
/// <para>
/// 本抽象类把「何时进入窗口」抽成 SR 多态字段(对照 <see cref="FirePatternBulletExtra"/> /
/// <see cref="BaseOffsetStrategy"/> 的多态套路):
///   - <see cref="DelayStartTrigger"/> = 默认,等价旧 Delay 字段(向后兼容 100%)
///   - <see cref="OnSignalStartTrigger"/> = 订阅 <see cref="BulletSignalBus"/> 信号,收到即激活
///   - <see cref="DelayOrSignalStartTrigger"/> = Delay 与信号二选一(任一先到即激活)
/// </para>
/// <para>
/// 用户在 FirePattern 资产 ModifierPrefabs[] / FireAction.ExtraModifierPrefabs[] / BounceBulletModifier
/// 上看到的「StartTrigger」字段会是个下拉菜单 —— 与既有项目扩展点一致(SerializeReference + SRName)。
/// </para>
///
/// <para>★ 与基类时间窗口的关系:</para>
/// <para>
/// StartTrigger **只决定「何时进入窗口」**;进入窗口后的 Duration 持续期 / OneShot 退出等逻辑
/// 仍由 <see cref="BulletModifier.Modify"/> 基类统一管理。
/// 例如:OnSignalStartTrigger 收到信号 → trigger.ShouldActivate 返回 true →
/// 基类调 OnWindowEnter → 然后按 OneShot / Duration 决定后续。
/// </para>
///
/// <para>★ 生命周期契约:</para>
/// <list type="number">
///   <item><see cref="OnAttach"/>:modifier 挂到子弹时调一次(由 Bullet.AttachSignalTriggers 触发)。
///         订阅型 trigger 在这里调 <see cref="BulletSignalBus.Subscribe"/>。</item>
///   <item><see cref="ShouldActivate"/>:每帧由 BulletModifier.Modify 调一次。返回 true 表示进入窗口。</item>
///   <item><see cref="OnDetach"/>:子弹回收或销毁时调一次(BulletPool.Return 与 Bullet.OnDestroy 兜底)。
///         订阅型 trigger 在这里调 <see cref="BulletSignalBus.Unsubscribe"/>。
///         ★ 这是契约 —— 不调 = 内存泄漏(handler 还挂在 _subs 字典里)。</item>
/// </list>
///
/// <para>★ 字段约定(与 BulletModifier 一致):</para>
/// <list type="bullet">
///   <item>值类型字段(float / int / bool / Vector2 / string)默认 Clone 即可。</item>
///   <item>per-instance 状态字段标 [NonSerialized],由 Clone 显式重置。</item>
///   <item>引用类型字段(List / 自定义类)override <see cref="Clone"/> 深拷。</item>
/// </list>
/// </summary>
[Serializable]
public abstract class ModifierStartTrigger
{
    /// <summary>
    /// 被 Bullet.AttachSignalTriggers 调用一次:订阅 / 初始化状态。
    /// 默认空 —— DelayStartTrigger 不需要订阅。
    /// </summary>
    public virtual void OnAttach(Bullet bullet) { }

    /// <summary>
    /// 被 Bullet.DetachSignalTriggers / BulletPool.Return / Bullet.OnDestroy 调用:取消订阅 / 清理状态。
    /// 默认空。
    /// </summary>
    public virtual void OnDetach(Bullet bullet) { }

    /// <summary>
    /// 每帧由 <see cref="BulletModifier.Modify"/> 调用。
    /// 返回 true = 本帧进入时间窗口(基类会立刻调 <see cref="BulletModifier.OnWindowEnter"/>)。
    /// </summary>
    /// <param name="bullet">当前子弹(供子类查 position / IsActive 等)。</param>
    /// <param name="elapsed">modifier 已累计运行时间(秒),与基类 _elapsed 同源。</param>
    public abstract bool ShouldActivate(Bullet bullet, float elapsed);

    /// <summary>时间触发器裁剪跨过阈值的首帧；信号按检测帧生效。</summary>
    public virtual float GetActivationDelta(float previousElapsed, float elapsed) => elapsed - previousElapsed;

    /// <summary>深拷。子类持有引用类型字段时 override。</summary>
    public virtual ModifierStartTrigger Clone() => (ModifierStartTrigger)MemberwiseClone();
}
