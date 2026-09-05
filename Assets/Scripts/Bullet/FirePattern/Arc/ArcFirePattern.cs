using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "STG/FirePattern/Arc")]
public class ArcFirePattern : FirePattern
{
    public int Count = 8; // 子弹数
    public float ArcLength = 60f;    // 弧长（度）
    public float Radius = 0f;        // 起始偏移半径
    [Tooltip("该 pattern 的整体基准朝向（度）。0=右，90=上，180=左，270=下。")]
    public float BaseAngle = 270f;

    public override void Fire(Vector2 position, float rotationRad, BulletPool pool, Bullet owner = null)
    {
        // 中线 = BaseAngle（度） + rotationRad（弧度增量）
        float centerRad = BaseAngle * Mathf.Deg2Rad + rotationRad;

        if (Count <= 1)
        {
            FireOne(position, centerRad, pool);
            return;
        }
        float start = centerRad - (ArcLength * Mathf.Deg2Rad) / 2f;
        float step = (ArcLength * Mathf.Deg2Rad) / (Count - 1);
        for (int i = 0; i < Count; i++)
        {
            float rad = start + step * i;
            Vector2 offset = Radius * new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            FireOne(position + offset, rad, pool);
        }
    }
    void FireOne(Vector2 pos, float rad, BulletPool pool)
    {
        pool.Get(BulletPrefab, pos, rad, Speed, AngularSpeed);
    }

    public override int GetFireCount() => Count;
}