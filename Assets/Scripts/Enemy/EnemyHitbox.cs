using UnityEngine;
using ShinySTG.Hitbox;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 敌人判定点。继承通用 HitboxComponent。
    /// Inspector 自由改 Size,Gizmos 实时显示。Reset() 给推荐默认 0.5×0.5(中等敌人)。
    /// </summary>
    public class EnemyHitbox : HitboxComponent
    {
        [Header("Contact Damage")]
        [Tooltip("与玩家实体碰撞时是否造成体术伤害。关闭时仍可被玩家子弹命中。")]
        public bool DealsContactDamage = false;

        void Reset()
        {
            Size = new Vector2(0.5f, 0.5f);
            Team = CollisionTeam.Enemy;
        }
    }
}
