using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>只改变子弹的视觉自转，不改变飞行方向或运动轨迹。</summary>
[Serializable, SRName("Modifier/Visual Spin")]
public class VisualSpinBulletModifier : BulletModifier
{
    [Tooltip("视觉自转角速度(度/秒)。正数=逆时针，负数=顺时针。不会改变子弹飞行方向。")]
    public float AngularSpeed = 180f;

    protected override void OnWindowExitCleanup(Bullet bullet) => bullet.ClearVisualSpin();

    public override void ModifyCore(Bullet bullet, float deltaTime)
    {
        bullet.SetVisualSpin(AngularSpeed);
    }
}
