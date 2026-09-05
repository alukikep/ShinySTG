using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BulletModifier : MonoBehaviour
{
    /// 每帧由 Bullet.Update 调用。
    public abstract void Modify(Bullet bullet, float deltaTime);
}

/// 示例：加速
public class AccelerateModifier : BulletModifier
{
    public float Acceleration = 5f;
    public override void Modify(Bullet b, float dt) => b.Speed += Acceleration * dt;
}

/// 示例：朝目标点转向
public class SteerTowardModifier : BulletModifier
{
    public Transform Target;
    public float TurnRate = 180f; // 度/秒
    public override void Modify(Bullet b, float dt)
    {
        if (Target == null) return;
        Vector2 toTarget = (Vector2)Target.position - (Vector2)b.transform.position;
        float desiredAngle = Mathf.Atan2(toTarget.y, toTarget.x); // 弧度
        float currentAngle = b.SteerAngle;                       // 弧度
        float maxStep = TurnRate * Mathf.Deg2Rad * dt;           // 度/秒 → 弧度
        b.SteerAngle = Mathf.MoveTowards(currentAngle, desiredAngle, maxStep);
    }
}

