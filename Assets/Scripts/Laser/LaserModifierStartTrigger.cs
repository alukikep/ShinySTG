using System;
using SerializeReferenceEditor;
using UnityEngine;
using ShinySTG.BulletCore; // BulletSignalBus —— 订阅型 trigger 静态调用(激光信号 trigger 复用同一个 Bus,跨子弹+激光子体系联动免费获得)

// 注:本文件所有 LaserModifierStartTrigger 子类放全局命名空间(对齐 ModifierStartTrigger 同款),
//     否则 LaserModifier 上的 [SerializeReference] 字段在 Inspector 下拉里看不到这些 SRName ——
//     踩坑记录见 CONTRIBUTING.md §4.7。

/// <summary>
/// <see cref="LaserModifier"/> 的「启动触发器」多态扩展 —— 决定 modifier 何时进入时间窗口。
///
/// <para>★ 设计动机(对齐 BulletModifier.ModifierStartTrigger,§2.8 / arch-bullet):</para>
/// <para>
/// 项目现状(改之前):所有 LaserModifier 共享基类的最小钩子 <c>OnTick</c>,modifier 激活时机 = 出生即生效。
/// 这无法对齐到敌人 AI 节奏 —— Boss 喊话、阶段切换、Parallel 容器内某条 Action 完成 等场景下,
/// 策划只能靠「写一个空的 LaserOrbitModifier(AngularSpeed=0)」凑,既不准又难维护。
/// </para>
/// <para>
/// 本抽象类把「何时进入窗口」抽成 SR 多态字段(对照 <see cref="FirePatternBulletExtra"/> /
/// <see cref="BaseOffsetStrategy"/> 的多态套路):
///   - <see cref="DelayLaserModifierStartTrigger"/> = 默认,等价旧「出生即生效」(向后兼容 100%)
///   - <see cref="OnSignalLaserModifierStartTrigger"/> = 订阅 <see cref="BulletSignalBus"/> 信号,收到即激活
///   - <see cref="DelayOrSignalLaserModifierStartTrigger"/> = Delay 与信号二选一(任一先到即激活)
/// </para>
///
/// <para>★ 跨子体系联动:</para>
/// <para>
/// 复用 <see cref="BulletSignalBus"/>(不新建 LaserSignalBus)——
/// 同一信号名可同时被子弹版 trigger + 激光版 trigger 订阅,
/// 实现「Boss 喊话 → 全场子弹 + 自机激光同时响应」的真实场景零成本。
/// 信号命名规范已有(&lt;场景&gt;_&lt;语义&gt;_&lt;时机&gt;),误触概率近零。
/// </para>
///
/// <para>★ 与基类时间窗口的关系:</para>
/// <para>
/// StartTrigger <b>只决定「何时进入窗口」</b>;进入窗口后的 Duration 持续期 / OneShot 退出等逻辑
/// 仍由 <see cref="LaserModifier.Modify"/> 基类统一管理。
/// 例如:OnSignalLaserModifierStartTrigger 收到信号 → trigger.ShouldActivate 返回 true →
/// 基类调 OnWindowEnter → 然后按 OneShot / Duration 决定后续。
/// </para>
///
/// <para>★ 生命周期契约:</para>
/// <list type="number">
///   <item><see cref="OnAttach"/>:modifier 挂到激光时调一次(由 LaserEntity.AttachSignalTriggers 触发)。
///         订阅型 trigger 在这里调 <see cref="BulletSignalBus.Subscribe"/>。</item>
///   <item><see cref="ShouldActivate"/>:每帧由 LaserModifier.Modify 调一次。返回 true 表示进入窗口。</item>
///   <item><see cref="OnDetach"/>:激光回收或销毁时调一次(LaserPool.Return 与 LaserEntity.OnDisable 兜底)。
///         订阅型 trigger 在这里调 <see cref="BulletSignalBus.Unsubscribe"/>。
///         ★ 这是契约 —— 不调 = 内存泄漏(handler 还挂在 _subs 字典里)。</item>
/// </list>
///
/// <para>★ 字段约定(与 LaserModifier 一致):</para>
/// <list type="bullet">
///   <item>值类型字段(float / int / bool / Vector2 / string)默认 Clone 即可。</item>
///   <item>per-instance 状态字段标 [NonSerialized],Clone 后自然归零。</item>
///   <item>引用类型字段(List / 自定义类)override <see cref="Clone"/> 深拷。</item>
/// </list>
/// </summary>
[Serializable]
public abstract class LaserModifierStartTrigger
{
    /// <summary>
    /// 被 LaserEntity.AttachSignalTriggers 调用一次:订阅 / 初始化状态。
    /// 默认空 —— DelayLaserModifierStartTrigger 不需要订阅。
    /// </summary>
    public virtual void OnAttach(ShinySTG.Laser.LaserEntity laser) { }

    /// <summary>
    /// 被 LaserEntity.DetachSignalTriggers / LaserPool.Return / LaserEntity.OnDisable 调用:取消订阅 / 清理状态。
    /// 默认空。
    /// </summary>
    public virtual void OnDetach(ShinySTG.Laser.LaserEntity laser) { }

    /// <summary>
    /// 每帧由 <see cref="LaserModifier.Modify"/> 调用。
    /// 返回 true = 本帧进入时间窗口(基类会立刻调 <see cref="LaserModifier.OnWindowEnter"/>)。
    /// </summary>
    /// <param name="laser">当前激光(供子类查 position / State 等)。</param>
    /// <param name="elapsed">modifier 已累计运行时间(秒),与基类 _elapsed 同源。</param>
    public abstract bool ShouldActivate(ShinySTG.Laser.LaserEntity laser, float elapsed);

    /// <summary>深拷。子类持有引用类型字段时 override。</summary>
    public virtual LaserModifierStartTrigger Clone() => (LaserModifierStartTrigger)MemberwiseClone();
}

/// <summary>
/// 默认启动触发器 —— <c>elapsed &gt;= Delay</c> 即激活。
/// 行为与「出生即生效」(LaserModifier PR1 旧行为)100% 等价 —— 但要注意:
/// 旧 PR1 是「出生即生效」,新 DelayLaserModifierStartTrigger 是「Delay 秒后生效」;
/// 兼容策略见 <see cref="LaserModifier.ResetWindow"/> —— StartTrigger 为 null 时自动新建
/// <c>DelayLaserModifierStartTrigger{ Delay = this.Delay }</c>,基类 Delay=0 时立即激活,行为 100% 等价。
///
/// 字段含义见 <see cref="LaserModifier.Delay"/> 的 Tooltip。
/// </summary>
[Serializable, SRName("LaserTrigger/Delay")]
public class DelayLaserModifierStartTrigger : LaserModifierStartTrigger
{
    [Tooltip("激光生成后,延迟多少秒才进入 modifier 时间窗口。\n" +
             "0(默认)= 出生即生效,与 PR1 旧行为 100% 等价。\n" +
             "典型用例:0.5s 后才激活 Orbit 旋转,前 0.5s 激光沿初始方向直线飞行(「蓄力期」);\n" +
             "         1s 后才激活分裂 / 染色 等副作用(配合 OneShot)。")]
    [Min(0f)] public float Delay = 0f;

    public override bool ShouldActivate(ShinySTG.Laser.LaserEntity laser, float elapsed)
        => elapsed >= Delay;

    public override LaserModifierStartTrigger Clone() => (LaserModifierStartTrigger)MemberwiseClone();
}

/// <summary>
/// 信号启动触发器 —— 订阅 <see cref="BulletSignalBus"/> 具名信号,收到即激活。
/// 配合可选的 <see cref="MaxWait"/> 兜底(防信号丢失 / 敌人被秒杀导致 modifier 永远进不了窗口)。
///
/// <para>★ 典型用法:</para>
/// <list type="bullet">
///   <item>Boss 蓄力吼 → EmitSignalAction.Emit("boss_charge_done") → Boss 自机的激光 modifier 立即激活(开始圆周运动)</item>
///   <item>Boss 残血 → EmitSignalAction.Emit("boss_enrage") → 激光 modifier 立即触发分裂 / 旋转</item>
///   <item>玩家按 Z 时一次性音效 → EmitSignalAction.Emit("player_fire") → 激光染色 modifier 立即染色</item>
/// </list>
///
/// <para>★ 跨子体系联动:</para>
/// <para>
/// 同一信号名可同时被子弹版 <c>OnSignalStartTrigger</c> + 激光版 <c>OnSignalLaserModifierStartTrigger</c>
/// 订阅 —— Emit 一次全场响应,与策划「Boss 喊话全场响应」语义完美对齐。
/// </para>
/// </summary>
[Serializable, SRName("LaserTrigger/On Signal")]
public class OnSignalLaserModifierStartTrigger : LaserModifierStartTrigger
{
    [Tooltip("订阅的信号名。空 = 不订阅(永远不会被激活,除非靠 MaxWait 兜底)。\n" +
             "★ 大小写敏感。建议命名规范:'<场景>_<语义>_<时机>',如 'boss_yell_charge_done'。\n" +
             "★ 同一信号名可被子弹版 / 激光版 trigger 同时订阅,Emit 一次全场响应。")]
    public string SignalName;

    [Tooltip("最迟等待多久后强制激活(秒)。0 = 不强制,一直等信号。\n" +
             "防'信号丢失 / 敌人被秒杀 / 玩家没踩到触发器'导致 modifier 永远进不了窗口。\n" +
             "推荐:与激光生命周期同量级(典型 1~10 秒 —— 激光持续时间通常远短于子弹)。")]
    [Min(0f)] public float MaxWait = 10f;

    [Tooltip("信号到达时,是否做距离判定(只对 <see cref=\"MaxDistanceFromOrigin\"/> 范围内的激光生效)。\n" +
             "false(默认)= 收到就激活,不管激光离信号源多远;\n" +
             "true = 收信号时记录 origin,ShouldActivate 时激光与 origin 距离 &lt;= MaxDistanceFromOrigin 才激活。\n" +
             "典型用例:Boss 吼时只激活 8 单位内的激光(视觉/逻辑上'局部范围'响应)。")]
    public bool RequireInRange = false;

    [Tooltip("距离判定上限(世界单位)。仅 RequireInRange=true 时生效。")]
    [Min(0f)] public float MaxDistanceFromOrigin = 8f;

    // per-instance 状态 —— Clone 后归零
    [NonSerialized] bool   _signalReceived;          // 信号是否到达过(且通过距离早判)
    [NonSerialized] Vector2 _signalOrigin;           // 信号源位置(RequireInRange=true 时用)
    [NonSerialized] bool   _subscribedThisAttach;    // 防 OnAttach 被调多次时重复订阅
    [NonSerialized] ShinySTG.Laser.LaserEntity _attachedLaser; // ★ OnAttach 缓存的激光引用,供 HandleSignal 距离早判用

    /// <summary>信号是否已到达(只读,供外部调试)。</summary>
    public bool SignalReceived => _signalReceived;

    public override void OnAttach(ShinySTG.Laser.LaserEntity laser)
    {
        _signalReceived = false;
        _signalOrigin = Vector2.zero;
        _subscribedThisAttach = false;
        _attachedLaser = laser;     // ★ 缓存引用,供 HandleSignal 做距离早判(BulletSignalBus 派发时只给 origin)

        if (string.IsNullOrWhiteSpace(SignalName)) return;

        BulletSignalBus.Subscribe(SignalName, HandleSignal);
        _subscribedThisAttach = true;
    }

    public override void OnDetach(ShinySTG.Laser.LaserEntity laser)
    {
        if (_subscribedThisAttach && !string.IsNullOrWhiteSpace(SignalName))
        {
            BulletSignalBus.Unsubscribe(SignalName, HandleSignal);
        }
        _subscribedThisAttach = false;
        _attachedLaser = null;        // ★ 清引用,防 OnDetach 后 _attachedLaser 指向已回收的激光
    }

    public override bool ShouldActivate(ShinySTG.Laser.LaserEntity laser, float elapsed)
    {
        // 路径 1:MaxWait 兜底 —— 信号没来,时间到了强制激活
        if (!_signalReceived && MaxWait > 0f && elapsed >= MaxWait)
            return true;

        // 路径 2:信号来了
        if (_signalReceived)
        {
            // ★ 兜底距离判定(理论上 _signalReceived=true 时 HandleSignal 距离早判已通过,
            //   这里再判一次仅作防御 —— 对齐子弹版 OnSignalStartTrigger.ShouldActivate 兜底逻辑)
            if (RequireInRange && laser != null)
            {
                float sqrDist = ((Vector2)laser.Position - _signalOrigin).sqrMagnitude;
                if (sqrDist > MaxDistanceFromOrigin * MaxDistanceFromOrigin)
                    return false;
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// 信号到达回调。<b>不要 override</b> —— 整个订阅生命周期由基类管(OnAttach 挂 / OnDetach 摘)。
    /// 子类若需要响应信号,实现自定义 trigger 即可。
    ///
    /// <para>★ 距离早判(对齐子弹版 vX 修复后):</para>
    /// <para>
    /// 旧实现:HandleSignal 不管距离,无条件 _signalReceived=true;距离判定放在 ShouldActivate 每帧跑。
    /// 问题:激光在信号到达的瞬间超距 → _signalReceived=true → 后续每帧 ShouldActivate 距离判定都不通过
    ///       → 永远不激活,只能等 MaxWait 兜底。
    /// 修复:HandleSignal 里直接做距离判定,超距就不置 _signalReceived(相当于「本信号对本激光无效」),
    ///      激光继续等下一个信号 / MaxWait 兜底。
    /// </para>
    /// </summary>
    void HandleSignal(Vector2 origin)
    {
        if (RequireInRange && _attachedLaser != null)
        {
            float sqrDist = ((Vector2)_attachedLaser.Position - origin).sqrMagnitude;
            if (sqrDist > MaxDistanceFromOrigin * MaxDistanceFromOrigin)
                return;     // ★ 距离超限 → 直接忽略本信号,_signalReceived 保持 false
        }
        _signalReceived = true;
        _signalOrigin = origin;
    }

    public override LaserModifierStartTrigger Clone() => (LaserModifierStartTrigger)MemberwiseClone();
}

/// <summary>
/// Delay 或信号 —— 任一先到即激活。
/// Delay 期正常订阅信号;Delay 内收到信号立即激活;Delay 到了没信号也激活。
///
/// <para>★ 典型用法:</para>
/// <list type="bullet">
///   <item>Boss 蓄力最迟 3 秒 → 3 秒到了一定开火;但如果 1.5 秒时玩家撞了 Boss 提前触发
///         「boss_early_release」信号 → 立刻开火(等不及 3 秒)。</item>
///   <item>激光「等待 1 秒或玩家按下 Z」即激活 modifier。</item>
/// </list>
/// </summary>
[Serializable, SRName("LaserTrigger/Delay Or Signal")]
public class DelayOrSignalLaserModifierStartTrigger : LaserModifierStartTrigger
{
    [Tooltip("最迟等多少秒。0 = 不设兜底(必须收到信号才激活)。")]
    [Min(0f)] public float Delay = 1f;

    [Tooltip("订阅的信号名。空 = 不订阅,等价 DelayLaserModifierStartTrigger。")]
    public string SignalName;

    [NonSerialized] bool   _signalReceived;
    [NonSerialized] Vector2 _signalOrigin;
    [NonSerialized] bool   _subscribedThisAttach;
    [NonSerialized] ShinySTG.Laser.LaserEntity _attachedLaser;

    public override void OnAttach(ShinySTG.Laser.LaserEntity laser)
    {
        _signalReceived = false;
        _signalOrigin = Vector2.zero;
        _subscribedThisAttach = false;
        _attachedLaser = laser;

        if (string.IsNullOrWhiteSpace(SignalName)) return;
        BulletSignalBus.Subscribe(SignalName, HandleSignal);
        _subscribedThisAttach = true;
    }

    public override void OnDetach(ShinySTG.Laser.LaserEntity laser)
    {
        if (_subscribedThisAttach && !string.IsNullOrWhiteSpace(SignalName))
        {
            BulletSignalBus.Unsubscribe(SignalName, HandleSignal);
        }
        _subscribedThisAttach = false;
        _attachedLaser = null;
    }

    public override bool ShouldActivate(ShinySTG.Laser.LaserEntity laser, float elapsed)
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

    public override LaserModifierStartTrigger Clone() => (LaserModifierStartTrigger)MemberwiseClone();
}


