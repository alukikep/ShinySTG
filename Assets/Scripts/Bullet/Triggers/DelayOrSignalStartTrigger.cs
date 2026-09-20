using System;
using SerializeReferenceEditor;
using UnityEngine;
using ShinySTG.BulletCore;

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
    [NonSerialized] string _subscribedName;
    [NonSerialized] bool   _subscribedThisAttach;

    public override void OnAttach(Bullet bullet)
    {
        OnDetach(bullet);
        _signalReceived = false;
        _signalOrigin = Vector2.zero;
        _subscribedThisAttach = false;

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
        if (!_subscribedThisAttach) return;
        _signalReceived = true;
        _signalOrigin = origin;
    }

    public override float GetActivationDelta(float previousElapsed, float elapsed) =>
        _signalReceived ? elapsed - previousElapsed : Mathf.Clamp(elapsed - Delay, 0f, elapsed - previousElapsed);

    public override ModifierStartTrigger Clone()
    {
        var copy = (DelayOrSignalStartTrigger)MemberwiseClone();
        copy._signalReceived = copy._subscribedThisAttach = false;
        copy._signalOrigin = Vector2.zero;
        copy._subscribedName = null;
        return copy;
    }
}
