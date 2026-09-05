using System.Collections;
using System.Collections.Generic;
using UnityEngine;



/// 组合：先环后扇（模仿 DanmakU 的 Of()）
/// 每个 child 自带自己的 BulletPrefab 与 Speed，组合时不强制覆盖。
[CreateAssetMenu(menuName = "STG/FirePattern/Composite")]
public class CompositeFirePattern : FirePattern
{
    public FirePattern[] Children;

    public override void Fire(Vector2 position, float rotationRad, BulletPool pool, Bullet owner = null)
    {
        foreach (var c in Children)
        {
            if (c != null) c.Fire(position, rotationRad, pool, owner);
        }
    }

    public override int GetFireCount()
    {
        if (Children == null) return 0;
        int sum = 0;
        foreach (var c in Children)
            if (c != null) sum += c.GetFireCount();
        return sum;
    }
}
