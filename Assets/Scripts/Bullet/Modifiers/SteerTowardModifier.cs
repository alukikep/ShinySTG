using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 示例：按固定角速度转向(螺旋 / 弧线 / 蛇形 等)。
///
/// 行为:从子弹生成那一刻起,以 TurnRate (度/秒) 的角速度持续旋转。
/// 不依赖任何 Target,行为简单可预测 —— 设多少转多少,正数=逆时针,负数=顺时针。
///
/// 实现细节:modifier 每帧把 b.AngularSpeed 覆写为 TurnRate (度 → 弧度)。
/// Bullet.Update 第 2 步会做 SteerAngle += AngularSpeed * dt,所以效果等价于"持续旋转"。
/// </summary>
[Serializable, SRName("Modifier/Steer")]
public class SteerTowardModifier : BulletModifier
{
    [Tooltip("角速度(度/秒)。正数=逆时针,负数=顺时针。0=不转。\n" +
             "90 = 1 秒转 90°(常见螺旋弹);360 = 1 秒转一圈。")]
    public float TurnRate = 90f;

    protected override void OnWindowExitCleanup(Bullet bullet) => bullet.ClearModifierTurnRate();

    public override void ModifyCore(Bullet b, float dt)
    {
        // 直接把 AngularSpeed 设为 TurnRate(弧度)。
        // Bullet.Update 第 2 步会自动把它累加到 SteerAngle,无需在这里直接改 SteerAngle。
        b.SetModifierTurn(TurnRate * Mathf.Deg2Rad, dt);
    }
}
