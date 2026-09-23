using System;
using SerializeReferenceEditor;
using UnityEngine;

[Serializable, SRName("Modifier/Orbit")]
public sealed class OrbitBulletModifier : BulletModifier
{
    public enum CenterMode { FixedPosition, FiringEnemy }

    [Header("Orbit")]
    public CenterMode Mode = CenterMode.FixedPosition;
    public Vector2 FixedCenter;
    [Tooltip("逆时针为正，单位：度/秒。半径由子弹出生位置自动计算。")]
    public float AngularSpeed = 90f;

    float _radius;
    float _angle;
    bool _initialized;

    protected override void OnResetWindow()
    {
        _radius = 0f;
        _angle = 0f;
        _initialized = false;
    }

    public override void ModifyCore(Bullet bullet, float deltaTime)
    {
        if (Mode == CenterMode.FiringEnemy && bullet.OwnerTransform == null)
        {
            bullet.RequestReturn();
            return;
        }
        Vector2 center = ResolveCenter(bullet);
        if (!_initialized)
        {
            Vector2 offset = bullet.Position - center;
            _radius = offset.magnitude;
            _angle = Mathf.Atan2(offset.y, offset.x);
            _initialized = true;
        }

        _angle += AngularSpeed * Mathf.Deg2Rad * deltaTime;
        Vector2 position = center + new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle)) * _radius;
        bullet.SetModifierMovement(position, _angle + Mathf.PI * 0.5f);
    }

    Vector2 ResolveCenter(Bullet bullet)
    {
        if (Mode == CenterMode.FiringEnemy)
            return bullet.OwnerTransform.position;
        return FixedCenter;
    }
}
