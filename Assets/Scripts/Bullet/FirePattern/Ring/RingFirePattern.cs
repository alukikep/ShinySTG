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
        // position 转本地变量再传 ref,这样 Resolver 不会污染 FirePattern 调用栈的形参;
        // 同时让 Base.PositionOffset 在 Resolver 入口处叠加(后续模块包括 PlayerAim 都基于修正后的 from 工作)。
        Vector2 from = position;
        float centerRad = FireExtensionResolver.ResolvePipelineWithOffset(FireExtensions, ref from, rotationRad);
        float step = 360f / Count;
        var team = ownerHitbox != null ? ownerHitbox.Team : ShinySTG.Hitbox.CollisionTeam.Neutral;
        for (int i = 0; i < Count; i++)
        {
            float rad = centerRad + step * i * Mathf.Deg2Rad;
            // Radius 是环形起始偏移(本地,沿每发子弹方向),与 Base.PositionOffset 正交叠加。
            Vector2 offset = Radius * new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            // 走 SpawnBullet 会自动挂 ModifierPrefabs + extraModifiers
            SpawnBullet(pool, from + offset, rad, Speed, AngularSpeed, Damage, team, extraModifiers);
        }
    }

    public override int GetFireCount() => Count;
}