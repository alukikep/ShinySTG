using System;
using SerializeReferenceEditor;
using UnityEngine;
using ShinySTG.BulletCore;

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
             "true = 收信号瞬间检查距离，通过后锁存激活资格。\n" +
             "典型用例:Boss 吼时只激活 8 单位内的弹(视觉/逻辑上'局部范围'响应)。")]
    public bool RequireInRange = false;

    [Tooltip("距离判定上限(世界单位)。仅 RequireInRange=true 时生效。")]
    [Min(0f)] public float MaxDistanceFromOrigin = 8f;

    // per-instance 状态 —— Clone 后归零
    [NonSerialized] bool   _signalReceived;          // 信号是否到达过(且通过距离早判)
    [NonSerialized] Vector2 _signalOrigin;           // 信号源位置(RequireInRange=true 时用)
    [NonSerialized] string _subscribedName;
    [NonSerialized] bool   _subscribedThisAttach;    // 防 OnAttach 被调多次时重复订阅
    [NonSerialized] Bullet _attachedBullet;          // ★ OnAttach 缓存的子弹引用,供 HandleSignal 距离早判用

    /// <summary>信号是否已到达(只读,供外部调试)。</summary>
    public bool SignalReceived => _signalReceived;

    public override void OnAttach(Bullet bullet)
    {
        OnDetach(bullet);
        _signalReceived = false;
        _signalOrigin = Vector2.zero;
        _subscribedThisAttach = false;
        _attachedBullet = bullet;     // ★ 缓存引用,供 HandleSignal 做距离早判(BulletSignalBus 派发时只给 origin)

        if (string.IsNullOrWhiteSpace(SignalName)) return;

        _subscribedName = SignalName;
        BulletSignalBus.Subscribe(_subscribedName, HandleSignal);
        _subscribedThisAttach = true;
    }

    public override void OnDetach(Bullet bullet)
    {
        if (_subscribedThisAttach)
        {
            BulletSignalBus.Unsubscribe(_subscribedName, HandleSignal);
        }
        _subscribedThisAttach = false;
        _attachedBullet = null;        // ★ 清引用,防 OnDetach 后 _attachedBullet 指向已回收的子弹
    }

    public override bool ShouldActivate(Bullet bullet, float elapsed) =>
        _signalReceived || (MaxWait > 0f && elapsed >= MaxWait);

    public override float GetActivationDelta(float previousElapsed, float elapsed) =>
        _signalReceived ? elapsed - previousElapsed : Mathf.Clamp(elapsed - MaxWait, 0f, elapsed - previousElapsed);

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
        if (!_subscribedThisAttach || _signalReceived) return;
        if (RequireInRange && _attachedBullet == null) return;
        if (RequireInRange && _attachedBullet != null)
        {
            float sqrDist = ((Vector2)_attachedBullet.Position - origin).sqrMagnitude;
            if (sqrDist > Mathf.Max(0f, MaxDistanceFromOrigin) * Mathf.Max(0f, MaxDistanceFromOrigin))
                return;     // ★ 距离超限 → 直接忽略本信号,_signalReceived 保持 false
        }
        _signalReceived = true;
        _signalOrigin = origin;
    }

    public override ModifierStartTrigger Clone()
    {
        var copy = (OnSignalStartTrigger)MemberwiseClone();
        copy._signalReceived = copy._subscribedThisAttach = false;
        copy._signalOrigin = Vector2.zero;
        copy._attachedBullet = null;
        copy._subscribedName = null;
        return copy;
    }
}
