using System;
using SerializeReferenceEditor;
using UnityEngine;

[Serializable, SRName("Modifier/Orbit")]
public sealed class OrbitBulletModifier : BulletModifier
{
    public enum CenterMode { FixedPosition, FiringEnemy }
    public enum RadiusMode { Initial, Constant, Linear, Curve }

    [Header("Orbit")]
    public CenterMode Mode = CenterMode.FixedPosition;
    public Vector2 FixedCenter;
    [Tooltip("逆时针为正，单位：度/秒。半径由子弹出生位置自动计算。")]
    public float AngularSpeed = 90f;

    [Header("Radius")]
    [Tooltip("半径变化方式。Initial 保持旧行为：首次生效时取子弹到圆心的初始距离。")]
    public RadiusMode RadiusBehavior = RadiusMode.Initial;
    [Min(0f)]
    [Tooltip("Constant 模式使用的半径。")]
    public float Radius = 1f;
    [Min(0f)]
    [Tooltip("Linear/Curve 模式的目标半径。")]
    public float TargetRadius = 2f;
    [Min(0f)]
    [Tooltip("Linear/Curve 模式从初始半径变化到目标半径所需的时间；<=0 表示立即到达目标。")]
    public float RadiusDuration = 1f;
    [Tooltip("Curve 模式的归一化半径曲线。0 对应初始半径，1 对应目标半径。")]
    public AnimationCurve RadiusCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    float _radius;
    float _initialRadius;
    float _angle;
    float _radiusElapsed;
    bool _initialized;

    protected override void OnResetWindow()
    {
        _radius = 0f;
        _initialRadius = 0f;
        _angle = 0f;
        _radiusElapsed = 0f;
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
            _initialRadius = offset.magnitude;
            _radius = EvaluateRadius(0f);
            _angle = Mathf.Atan2(offset.y, offset.x);
            _initialized = true;
        }

        _radiusElapsed += deltaTime;
        _radius = EvaluateRadius(_radiusElapsed);
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

    float EvaluateRadius(float elapsed)
    {
        switch (RadiusBehavior)
        {
            case RadiusMode.Constant:
                return Mathf.Max(0f, Radius);

            case RadiusMode.Linear:
            {
                float t = RadiusDuration > 0f ? Mathf.Clamp01(elapsed / RadiusDuration) : 1f;
                return Mathf.Max(0f, Mathf.Lerp(_initialRadius, TargetRadius, t));
            }

            case RadiusMode.Curve:
            {
                float t = RadiusDuration > 0f ? Mathf.Clamp01(elapsed / RadiusDuration) : 1f;
                float curveT = RadiusCurve != null ? RadiusCurve.Evaluate(t) : t;
                return Mathf.Max(0f, Mathf.Lerp(_initialRadius, TargetRadius, curveT));
            }

            default:
                return Mathf.Max(0f, _initialRadius);
        }
    }
}
