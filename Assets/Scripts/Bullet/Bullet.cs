using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{

    [HideInInspector] public float Speed;
    [HideInInspector] public float AngularSpeed; // 弧度/秒，0 = 不自转
    [HideInInspector] public float SteerAngle;   // 弧度，当前飞行方向
    [HideInInspector] public float Lifetime;     // 累计存活时间

    readonly List<BulletModifier> _modifiers = new();

    public void AddModifier(BulletModifier m) => _modifiers.Add(m);
    public void ClearModifiers() => _modifiers.Clear();

    /// 由池在 Get 时调用：写入初始参数、重置 modifiers。
    public void Init(Vector2 position, float fireAngleRad, float speed, float angularSpeed)
    {
        transform.position = position;
        transform.rotation = Quaternion.Euler(0, 0, fireAngleRad * Mathf.Rad2Deg);
        SteerAngle = fireAngleRad;
        Speed = speed;
        AngularSpeed = angularSpeed;
        Lifetime = 0;
        _modifiers.Clear();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Lifetime += dt;

        // 1. 让 modifier 修改当前状态
        foreach (var m in _modifiers) m.Modify(this, dt);

        // 2. 角速度累加到当前飞行方向
        SteerAngle += AngularSpeed * dt;

        // 3. 按当前方向移动
        Vector2 dir = new Vector2(Mathf.Cos(SteerAngle), Mathf.Sin(SteerAngle));
        transform.position += (Vector3)(dir * Speed * dt);

        // 4. 用方向同步旋转（让贴图朝向飞行方向）
        transform.rotation = Quaternion.Euler(0, 0, SteerAngle * Mathf.Rad2Deg);

        // 5. 简单越界回收（先实现，后续再优化）
        if (Mathf.Abs(transform.position.x) > 10f ||
            Mathf.Abs(transform.position.y) > 20f)
        {
            BulletPool.Instance.Return(this);
        }
    }
}