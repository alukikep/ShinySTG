using UnityEngine;

/// 环形
[CreateAssetMenu(menuName = "STG/FirePattern/Ring")]
public class RingFirePattern : FirePattern
{
    public int Count = 16;
    public float Radius = 0f;
    // 注:BaseAngle 字段已挪到 FireExtension 子类(FireExtension/Base / FireExtension/Player Aim)上,本类不再持有。

    public override void Fire(Vector2 position, float rotationRad, BulletPool pool,
                              ShinySTG.Hitbox.HitboxComponent ownerHitbox = null,
                              BulletModifier[] extraModifiers = null)
    {
        // 中线方向由 FireExtension 解析(null 时 fallback = 270° + rotationRad,等价旧版 normal)
        float centerRad = FireExtensionResolver.ResolveCenterAngle(FireExtension, position, rotationRad);
        float step = 360f / Count;
        var team = ownerHitbox != null ? ownerHitbox.Team : ShinySTG.Hitbox.CollisionTeam.Neutral;
        for (int i = 0; i < Count; i++)
        {
            float rad = centerRad + step * i * Mathf.Deg2Rad;
            Vector2 offset = Radius * new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            // 走 SpawnBullet 会自动挂 ModifierPrefabs + extraModifiers
            SpawnBullet(pool, position + offset, rad, Speed, AngularSpeed, Damage, team, extraModifiers);
        }
    }

    public override int GetFireCount() => Count;
}