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
        void Reset()
        {
            Size = new Vector2(0.5f, 0.5f);
            Team = CollisionTeam.Enemy;
        }
    }
}