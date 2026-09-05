using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// 环形
[CreateAssetMenu(menuName = "STG/FirePattern/Ring")]
public class RingFirePattern : FirePattern
{
    public int Count = 16;
    public float Radius = 0f;
    [Tooltip("该 pattern 的整体基准朝向（度）。0=右，90=上，180=左，270=下。")]
    public float BaseAngle = 270f;

    public override void Fire(Vector2 position, float rotationRad, BulletPool pool, Bullet owner = null)
    {
        // 中线 = BaseAngle（度） + rotationRad（弧度增量）
        float centerRad = BaseAngle * Mathf.Deg2Rad + rotationRad;
        float step = 360f / Count;
        for (int i = 0; i < Count; i++)
        {
            float rad = centerRad + step * i * Mathf.Deg2Rad;
            Vector2 offset = Radius * new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            pool.Get(BulletPrefab, position + offset, rad, Speed, AngularSpeed);
        }
    }

    public override int GetFireCount() => Count;
}