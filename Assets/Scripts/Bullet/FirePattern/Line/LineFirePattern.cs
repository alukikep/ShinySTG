using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 连射（累加速度）
[CreateAssetMenu(menuName = "STG/FirePattern/Line")]
public class LineFirePattern : FirePattern
{
    public int Count = 3;
    public float DeltaSpeed = 1f;
    [Tooltip("该 pattern 的整体基准朝向（度）。0=右，90=上，180=左，270=下。")]
    public float BaseAngle = 270f;

    public override void Fire(Vector2 position, float rotationRad, BulletPool pool, Bullet owner = null)
    {
        float rad = BaseAngle * Mathf.Deg2Rad + rotationRad;
        float speed = Speed; // 起点使用基类 Speed
        for (int i = 0; i < Count; i++)
        {
            pool.Get(BulletPrefab, position, rad, speed, AngularSpeed);
            speed += DeltaSpeed;
        }
    }

    public override int GetFireCount() => Count;
}