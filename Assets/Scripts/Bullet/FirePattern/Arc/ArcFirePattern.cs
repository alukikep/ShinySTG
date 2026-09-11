using UnityEngine;

[CreateAssetMenu(menuName = "STG/FirePattern/Arc")]
public class ArcFirePattern : FirePattern
{
    public int Count = 8;       // 子弹数
    public float ArcLength = 60f; // 弧长(度)
    public float Radius = 0f;     // 起始偏移半径
    // 注:BaseAngle 字段已挪到 FireExtension 子类(FireExtension/Base / FireExtension/Player Aim)上,本类不再持有。

    public override void Fire(Vector2 position, float rotationRad, BulletPool pool,
                              ShinySTG.Hitbox.HitboxComponent ownerHitbox = null,
                              BulletModifier[] extraModifiers = null)
    {
        // position 转本地变量再传 ref,让 Base.PositionOffset 在 Resolver 入口处叠加。
        Vector2 from = position;
        // 中线方向由 FireExtensions pipeline 解析(空数组 → fallback 270° + rotationRad,等价旧版 normal)
        float centerRad = FireExtensionResolver.ResolvePipelineWithOffset(FireExtensions, ref from, rotationRad);
        var team = ownerHitbox != null ? ownerHitbox.Team : ShinySTG.Hitbox.CollisionTeam.Neutral;

        if (Count <= 1)
        {
            FireOne(from, centerRad, pool, team, extraModifiers);
            return;
        }
        float start = centerRad - (ArcLength * Mathf.Deg2Rad) / 2f;
        float step = (ArcLength * Mathf.Deg2Rad) / (Count - 1);
        for (int i = 0; i < Count; i++)
        {
            float rad = start + step * i;
            // Radius 是扇形起始偏移(本地,沿每发子弹方向),与 Base.PositionOffset 正交叠加。
            Vector2 offset = Radius * new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            FireOne(from + offset, rad, pool, team, extraModifiers);
        }
    }

    void FireOne(Vector2 pos, float rad, BulletPool pool, ShinySTG.Hitbox.CollisionTeam team,
                 BulletModifier[] extraModifiers)
    {
        // 走 SpawnBullet 会自动挂 ModifierPrefabs + extraModifiers
        SpawnBullet(pool, pos, rad, Speed, AngularSpeed, Damage, team, extraModifiers);
    }

    public override int GetFireCount() => Count;
}