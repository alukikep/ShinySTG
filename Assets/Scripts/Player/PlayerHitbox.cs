using UnityEngine;
using ShinySTG.Hitbox;

namespace ShinySTG.Player
{
    /// <summary>
    /// 玩家判定点。继承通用 HitboxComponent。
    ///
    /// Inspector 自由改 Size,Gizmos 实时显示绿色 AABB 框(选中变橙)。
    /// 经典 STG 推荐 Size = (0.1, 0.1);Reset() 已给该默认。
    ///
    /// 调用示例(碰撞代码):
    ///   if (Player.Instance.Hitbox.Overlaps(bullet.Hitbox)) ...
    ///   if (Player.Instance.Hitbox.OverlapsPoint(bullet.Position)) ...
    /// </summary>
    public class PlayerHitbox : HitboxComponent
    {
        void Reset()
        {
            Size = new Vector2(0.1f, 0.1f);
            Team = CollisionTeam.Player;
        }
    }
}