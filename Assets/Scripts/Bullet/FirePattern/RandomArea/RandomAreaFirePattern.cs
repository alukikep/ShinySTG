using UnityEngine;

/// <summary>
/// 在一个相对发射点的矩形区域内随机采样位置，并在每个位置执行一次子 FirePattern。
/// 子 Pattern 负责子弹 prefab、角度、速度、Modifier 和 SpawnFog 等具体发射配置。
/// </summary>
[CreateAssetMenu(menuName = "STG/FirePattern/Random Area")]
public class RandomAreaFirePattern : FirePattern
{
    [Header("Random Area")]
    [Min(0), Tooltip("随机采样并执行 Child Pattern 的次数。")]
    public int SpawnCount = 1;

    [Tooltip("相对于调用方发射位置的矩形区域最小偏移。")]
    public Vector2 MinOffset = Vector2.zero;

    [Tooltip("相对于调用方发射位置的矩形区域最大偏移。")]
    public Vector2 MaxOffset = Vector2.zero;

    [Tooltip("每次随机采样后执行的 FirePattern。其子弹配置、Modifier 和 SpawnFog 均由该 Pattern 决定。")]
    public FirePattern ChildPattern;

    public override void Fire(Vector2 position, float rotationRad, BulletPool pool,
                              ShinySTG.Hitbox.HitboxComponent ownerHitbox = null,
                              BulletModifier[] extraModifiers = null)
    {
        if (pool == null || ChildPattern == null || SpawnCount <= 0) return;

        Vector2 min = new Vector2(Mathf.Min(MinOffset.x, MaxOffset.x), Mathf.Min(MinOffset.y, MaxOffset.y));
        Vector2 max = new Vector2(Mathf.Max(MinOffset.x, MaxOffset.x), Mathf.Max(MinOffset.y, MaxOffset.y));
        for (int i = 0; i < SpawnCount; i++)
        {
            var offset = new Vector2(
                Random.Range(min.x, max.x),
                Random.Range(min.y, max.y));
            pool.FireChild(ChildPattern, position + offset, rotationRad, ownerHitbox, extraModifiers);
        }
    }

    public override int GetFireCount()
    {
        return CompositeFirePattern.CountTree(this, new System.Collections.Generic.HashSet<FirePattern>());
    }
}
