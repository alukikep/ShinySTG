using UnityEngine;
using ShinySTG.Hitbox;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// Boss 判定点。继承通用 HitboxComponent,跟 EnemyHitbox 同款风格。
    ///
    /// Reset() 给推荐默认 1.5×1.5(STG 典型 boss 体积:中横版 boss 横向 1~2 单位)。
    /// Team = Enemy:玩家弹阵营 = Player,碰撞服务按 Player↔Enemy 配对,Boss 同样接受玩家弹攻击。
    ///
    /// 协作边界:
    ///   - BossHitbox 由 Boss 总控的 [RequireComponent] 自动挂,无需手填。
    ///   - 玩家弹 vs Boss:BossHitbox 进 CollisionService 网格,同 EnemyHitbox 走完全一致的查询路径。
    ///   - 追踪弹 vs Boss:HomingEnemyModifier 通过 IHomingTarget 接口识别 BossHealth。
    /// </summary>
    public class BossHitbox : HitboxComponent
    {
        void Reset()
        {
            Size = new Vector2(1.5f, 1.5f);
            Team = CollisionTeam.Enemy;
        }
    }
}