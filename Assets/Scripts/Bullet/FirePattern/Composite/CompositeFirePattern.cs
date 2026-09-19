using System.Collections;
using System.Collections.Generic;
using UnityEngine;



/// 组合：先环后扇（模仿 DanmakU 的 Of()）
/// 每个 child 自带自己的 BulletPrefab 与 Speed，组合时不强制覆盖。
[CreateAssetMenu(menuName = "STG/FirePattern/Composite")]
public class CompositeFirePattern : FirePattern
{
    public FirePattern[] Children;

    public override void Fire(Vector2 position, float rotationRad, BulletPool pool,
                              ShinySTG.Hitbox.HitboxComponent ownerHitbox = null,
                              BulletModifier[] extraModifiers = null)
    {
        if (pool == null || Children == null) return;
        // 透传 extras 给所有子 pattern;每个 child 自己会跟自己的 ModifierPrefabs 合并。
        foreach (var c in Children)
        {
            if (c != null) pool.FireChild(c, position, rotationRad, ownerHitbox, extraModifiers);
        }
    }

    public override int GetFireCount() => CountTree(this, new HashSet<FirePattern>());

    static int CountTree(FirePattern pattern, HashSet<FirePattern> path)
    {
        if (pattern == null || path.Count >= BulletPool.MaxPatternDepth || !path.Add(pattern)) return 0;
        try
        {
            if (!(pattern is CompositeFirePattern composite)) return Mathf.Max(0, pattern.GetFireCount());
            long sum = 0;
            if (composite.Children != null)
                foreach (var child in composite.Children) sum += CountTree(child, path);
            return (int)System.Math.Min(int.MaxValue, sum);
        }
        finally { path.Remove(pattern); }
    }
}
