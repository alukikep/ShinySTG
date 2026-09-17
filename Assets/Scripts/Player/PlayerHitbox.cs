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
        [Header("Items")]
        [Tooltip("道具拾取范围，不影响受伤判定。")]
        public Vector2 PickupSize = new Vector2(0.5f, 0.5f);
        [Tooltip("是否开启近距离道具吸附。")]
        public bool AttractionEnabled = true;
        [Tooltip("开始吸附的范围，不影响受伤判定。")]
        public Vector2 AttractionSize = new Vector2(2f, 2f);
        public Rect PickupBounds => ItemBounds(PickupSize);
        public Rect AttractionBounds => ItemBounds(AttractionSize);

        Rect ItemBounds(Vector2 size)
        {
            Vector3 scale = transform.lossyScale;
            size = new Vector2(Mathf.Max(0f, size.x) * Mathf.Abs(scale.x), Mathf.Max(0f, size.y) * Mathf.Abs(scale.y));
            return new Rect(Position - size * 0.5f, size);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            DrawItemBounds(PickupBounds, Color.cyan);
            if (AttractionEnabled) DrawItemBounds(AttractionBounds, Color.yellow);
        }

        void DrawItemBounds(Rect bounds, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(new Vector3(bounds.center.x, bounds.center.y, transform.position.z),
                new Vector3(bounds.width, bounds.height, 0f));
        }

        void Reset()
        {
            Size = new Vector2(0.1f, 0.1f);
            Team = CollisionTeam.Player;
        }
    }
}
