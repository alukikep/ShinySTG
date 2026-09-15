using System;
using SerializeReferenceEditor;
using UnityEngine;
using ShinySTG.BulletCore; // BulletSignalBus —— 订阅型 trigger 静态调用

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
///   <item>per-instance 状态字段标 [NonSerialized],Clone 后自然归零。</item>
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

    /// <summary>深拷。子类持有引用类型字段时 override。</summary>
    public virtual ModifierStartTrigger Clone() => (ModifierStartTrigger)MemberwiseClone();
}

/// <summary>
/// 默认启动触发器 —— <c>elapsed &gt;= Delay</c> 即激活。
/// 行为与旧 <c>BulletModifier.Delay</c> 字段 100% 等价。
/// 字段含义见 <c>BulletModifier.Delay</c> 的 Tooltip。
/// </summary>
[Serializable, SRName("Trigger/Delay")]
public class DelayStartTrigger : ModifierStartTrigger
{
    [Tooltip("子弹生成后,延迟多少秒才进入 modifier 时间窗口。\n" +
             "0 = 出生即生效(默认,与历史行为一致)。\n" +
             "★ 等价旧 BulletModifier.Delay 字段 —— 旧 .asset 兼容由 ResetWindow 兜底完成(把 Delay 值同步到本字段)。")]
    [Min(0f)] public float Delay = 0f;

    public override bool ShouldActivate(Bullet bullet, float elapsed) => elapsed >= Delay;
}

/// <summary>
/// 信号启动触发器 —— 订阅 <see cref="BulletSignalBus"/> 具名信号,收到即激活。
/// 配合可选的 <see cref="MaxWait"/> 兜底(防信号丢失 / 敌人被秒杀导致 modifier 永远进不了窗口)。
///
/// <para>★ 典型用法:</para>
/// <list type="bullet">
///   <item>Boss 蓄力吼 → EmitSignalAction.Emit("boss_charge_done") → 全场追踪 modifier 立即激活</item>
///   <item>Boss 残血 → EmitSignalAction.Emit("boss_enrage") → 子弹分裂 modifier 立即触发分裂</item>
///   <item>玩家按 Z 时一次性音效 → EmitSignalAction.Emit("player_fire") → 弹幕染色 modifier 立即染色</item>
/// </list>
/// </summary>
[Serializable, SRName("Trigger/On Signal")]
public class OnSignalStartTrigger : ModifierStartTrigger
{
    [Tooltip("订阅的信号名。空 = 不订阅(永远不会被激活,除非靠 MaxWait 兜底)。\n" +
             "★ 大小写敏感。建议命名规范:'<场景>_<语义>_<时机>',如 'boss_yell_charge_done'。")]
    public string SignalName;

    [Tooltip("最迟等待多久后强制激活(秒)。0 = 不强制,一直等信号。\n" +
             "防'信号丢失 / 敌人被秒杀 / 玩家没踩到触发器'导致 modifier 永远进不了窗口。\n" +
             "推荐:与 Bullet.lifetime 同量级(典型 8~20 秒)。")]
    [Min(0f)] public float MaxWait = 20f;

    [Tooltip("信号到达时,是否做距离判定(只对 <see cref=\"MaxDistanceFromOrigin\"/> 范围内的子弹生效)。\n" +
             "false(默认)= 收到就激活,不管子弹离信号源多远;\n" +
             "true = 收信号时记录 origin,ShouldActivate 时子弹与 origin 距离 &lt;= MaxDistanceFromOrigin 才激活。\n" +
             "典型用例:Boss 吼时只激活 8 单位内的弹(视觉/逻辑上'局部范围'响应)。")]
    public bool RequireInRange = false;

    [Tooltip("距离判定上限(世界单位)。仅 RequireInRange=true 时生效。")]
    [Min(0f)] public float MaxDistanceFromOrigin = 8f;

    // per-instance 状态 —— Clone 后归零
    [NonSerialized] bool   _signalReceived;          // 信号是否到达过(且通过距离早判)
    [NonSerialized] Vector2 _signalOrigin;           // 信号源位置(RequireInRange=true 时用)
    [NonSerialized] bool   _subscribedThisAttach;    // 防 OnAttach 被调多次时重复订阅
    [NonSerialized] Bullet _attachedBullet;          // ★ OnAttach 缓存的子弹引用,供 HandleSignal 距离早判用

    /// <summary>信号是否已到达(只读,供外部调试)。</summary>
    public bool SignalReceived => _signalReceived;

    public override void OnAttach(Bullet bullet)
    {
        _signalReceived = false;
        _signalOrigin = Vector2.zero;
        _subscribedThisAttach = false;
        _attachedBullet = bullet;     // ★ 缓存引用,供 HandleSignal 做距离早判(BulletSignalBus 派发时只给 origin)

        if (string.IsNullOrWhiteSpace(SignalName)) return;

        BulletSignalBus.Subscribe(SignalName, HandleSignal);
        _subscribedThisAttach = true;
    }

    public override void OnDetach(Bullet bullet)
    {
        if (_subscribedThisAttach && !string.IsNullOrWhiteSpace(SignalName))
        {
            BulletSignalBus.Unsubscribe(SignalName, HandleSignal);
        }
        _subscribedThisAttach = false;
        _attachedBullet = null;        // ★ 清引用,防 OnDetach 后 _attachedBullet 指向已回收的子弹
    }

    public override bool ShouldActivate(Bullet bullet, float elapsed)
    {
        // 路径 1:MaxWait 兜底 —— 信号没来,时间到了强制激活
        if (!_signalReceived && MaxWait > 0f && elapsed >= MaxWait)
            return true;

        // 路径 2:信号来了
        if (_signalReceived)
        {
            // ★ 旧实现这里在 ShouldActivate 里做距离判定 —— 已被 HandleSignal 早判取代
            //   (旧逻辑保留兜底:理论上 _signalReceived=true 时距离必然已通过,这里再判一次仅作防御)
            if (RequireInRange && bullet != null)
            {
                float sqrDist = ((Vector2)bullet.Position - _signalOrigin).sqrMagnitude;
                if (sqrDist > MaxDistanceFromOrigin * MaxDistanceFromOrigin)
                    return false;
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// 信号到达回调。**不要 override** —— 整个订阅生命周期由基类管(OnAttach 挂 / OnDetach 摘)。
    /// 子类若需要响应信号,实现自定义 trigger 即可。
    ///
    /// <para>★ 距离早判(自 vX 修复后):</para>
    /// <para>
    /// 旧实现:HandleSignal 不管距离,无条件 _signalReceived=true;距离判定放在 ShouldActivate 每帧跑。
    /// 问题:子弹在信号到达的瞬间超距 → _signalReceived=true → 后续每帧 ShouldActivate 距离判定都不通过
    ///       → 永远不激活,只能等 MaxWait 兜底。
    /// 修复:HandleSignal 里直接做距离判定,超距就不置 _signalReceived(相当于「本信号对本子弹无效」),
    ///      子弹继续等下一个信号 / MaxWait 兜底。
    /// </para>
    /// </summary>
    void HandleSignal(Vector2 origin)
    {
        if (RequireInRange && _attachedBullet != null)
        {
            float sqrDist = ((Vector2)_attachedBullet.Position - origin).sqrMagnitude;
            if (sqrDist > MaxDistanceFromOrigin * MaxDistanceFromOrigin)
                return;     // ★ 距离超限 → 直接忽略本信号,_signalReceived 保持 false
        }
        _signalReceived = true;
        _signalOrigin = origin;
    }

    public override ModifierStartTrigger Clone() => (ModifierStartTrigger)MemberwiseClone();
}

/// <summary>
/// Delay 或信号 —— 任一先到即激活。
/// Delay 期正常订阅信号;Delay 内收到信号立即激活;Delay 到了没信号也激活。
///
/// <para>★ 典型用法:</para>
/// <list type="bullet">
///   <item>Boss 蓄力最迟 3 秒 → 3 秒到了一定开火;但如果 1.5 秒时玩家撞了 Boss 提前触发
///         「boss_early_release」信号 → 立刻开火(等不及 3 秒)。</item>
///   <item>弹幕「等待 1 秒或玩家按下 Z」即激活 modifier。</item>
/// </list>
/// </summary>
[Serializable, SRName("Trigger/Delay Or Signal")]
public class DelayOrSignalStartTrigger : ModifierStartTrigger
{
    [Tooltip("最迟等多少秒。0 = 不设兜底(必须收到信号才激活)。")]
    [Min(0f)] public float Delay = 1f;

    [Tooltip("订阅的信号名。空 = 不订阅,等价 DelayStartTrigger。")]
    public string SignalName;

    [NonSerialized] bool   _signalReceived;
    [NonSerialized] Vector2 _signalOrigin;
    [NonSerialized] bool   _subscribedThisAttach;

    public override void OnAttach(Bullet bullet)
    {
        _signalReceived = false;
        _signalOrigin = Vector2.zero;
        _subscribedThisAttach = false;

        if (string.IsNullOrWhiteSpace(SignalName)) return;
        BulletSignalBus.Subscribe(SignalName, HandleSignal);
        _subscribedThisAttach = true;
    }

    public override void OnDetach(Bullet bullet)
    {
        if (_subscribedThisAttach && !string.IsNullOrWhiteSpace(SignalName))
        {
            BulletSignalBus.Unsubscribe(SignalName, HandleSignal);
        }
        _subscribedThisAttach = false;
    }

    public override bool ShouldActivate(Bullet bullet, float elapsed)
    {
        // 信号比 Delay 先到 → 立即激活
        if (_signalReceived) return true;
        // Delay 到 → 兜底激活
        return Delay > 0f && elapsed >= Delay;
    }

    void HandleSignal(Vector2 origin)
    {
        _signalReceived = true;
        _signalOrigin = origin;
    }

    public override ModifierStartTrigger Clone() => (ModifierStartTrigger)MemberwiseClone();
}
