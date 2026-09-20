using System;
using SerializeReferenceEditor;
using UnityEngine;

/// 示例：加速
[Serializable, SRName("Modifier/Accelerate")]
public class AccelerateModifier : BulletModifier
{
    public float Acceleration = 5f;
    public override void ModifyCore(Bullet b, float dt) => b.Speed += Acceleration * dt;
}
