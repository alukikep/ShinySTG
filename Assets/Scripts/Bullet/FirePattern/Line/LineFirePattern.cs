using UnityEngine;

// 连射(累加速度)
[CreateAssetMenu(menuName = "STG/FirePattern/Line")]
public class LineFirePattern : FirePattern
{
    public int Count = 3;
    public float DeltaSpeed = 1f;
    // 注:BaseAngle 字段已挪到 FireExtension 子类(FireExtension/Base / FireExtension/Player Aim)上,本类不再持有。

    public override void Fire(Vector2 position, float rotationRad, BulletPool pool,
                              ShinySTG.Hitbox.HitboxComponent ownerHitbox = null,
                              BulletModifier[] extraModifiers = null)
    {
        // 中线方向由 FireExtension 解析(null 时 fallback = 270° + rotationRad,等价旧版 normal)
        float rad = FireExtensionResolver.ResolveCenterAngle(FireExtension, position, rotationRad);
        float speed = Speed; // 起点使用基类 Speed
        var team = ownerHitbox != null ? ownerHitbox.Team : ShinySTG.Hitbox.CollisionTeam.Neutral;
        for (int i = 0; i < Count; i++)
        {
            // 走 SpawnBullet 会自动挂 ModifierPrefabs + extraModifiers
            SpawnBullet(pool, position, rad, speed, AngularSpeed, Damage, team, extraModifiers);
            speed += DeltaSpeed;
        }
    }

    public override int GetFireCount() => Count;
}