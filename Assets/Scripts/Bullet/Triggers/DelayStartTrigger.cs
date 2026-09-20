using System;
using SerializeReferenceEditor;
using UnityEngine;

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

    public override bool ShouldActivate(Bullet bullet, float elapsed) => elapsed >= Mathf.Max(0f, Delay);
    public override float GetActivationDelta(float previousElapsed, float elapsed) =>
        Mathf.Clamp(elapsed - Mathf.Max(0f, Delay), 0f, elapsed - previousElapsed);
}
