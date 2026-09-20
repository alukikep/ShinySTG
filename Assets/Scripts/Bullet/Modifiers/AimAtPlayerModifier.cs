using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 激活时采样玩家位置并瞬间转向，保留速度，之后不持续追踪。
/// 覆盖此前提交的本帧转角；后续转向 modifier 仍按数组顺序生效。
/// </summary>
[Serializable, SRName("Modifier/Aim At Player")]
public class AimAtPlayerModifier : BulletModifier
{
    public AimAtPlayerModifier() => OneShot = true;

    protected override void OnWindowEnter(Bullet bullet)
    {
        var player = ShinySTG.Player.Player.Instance;
        if (player == null || !player.isActiveAndEnabled) return;

        Vector2 direction = (Vector2)player.transform.position - (Vector2)bullet.transform.position;
        if (direction.sqrMagnitude <= 0.00000001f) return;

        bullet.SteerAngle = Mathf.Atan2(direction.y, direction.x);
        // 同时清零角速度与待应用转角，避免本帧移动前再次偏转。
        bullet.SetModifierTurn(0f, 0f);
    }

    // 即使关闭 OneShot，也只在进入窗口时采样一次。
    public override void ModifyCore(Bullet bullet, float deltaTime) { }
}
