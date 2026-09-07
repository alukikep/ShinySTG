using UnityEngine;

namespace ShinySTG.Hitbox
{
    /// <summary>
    /// 通用 Hitbox 组件。挂任何 GameObject(玩家 / 敌人 / 子弹 / 子机 / 道具)上即可。
    ///
    /// 形状:统一轴对齐矩形(AABB)。Size.x = 宽,Size.y = 高。
    ///   - 不受 transform.rotation 影响(经典 STG 碰撞盒的做法)
    ///   - 不受 transform.lossyScale 影响(读 lossyScale 实时计算 world size,
    ///     允许父物体整体缩放,但 hitbox 自身 scale 保持 1)
    ///
    /// Inspector:
    ///   - Size:AABB 尺寸
    ///   - Color / SelectedColor / AlwaysDraw:Gizmos 可视化
    ///
    /// 碰撞 API:
    ///   - Overlaps(HitboxComponent):是否相交
    ///   - OverlapsPoint(Vector2 worldPoint):点是否在内
    ///   - WorldBounds / Position:外部代码直接读
    ///
    /// 后续扩展形状(圆 / 胶囊 / 多边形):抽 HitboxShape 抽象基类,
    ///   把 Size / Overlaps / DrawGizmos 挪到子类即可,本组件接口不动。
    /// </summary>
    public class HitboxComponent : MonoBehaviour
    {
        [Header("Shape")]
        [Tooltip("AABB 尺寸(x=宽, y=高,世界单位)。\n" +
                 "经典 STG:玩家 (0.1, 0.1),敌人 (0.5, 0.5),子弹 (0.08, 0.08)。")]
        public Vector2 Size = new Vector2(0.1f, 0.1f);

        [Header("Editor")]
        [Tooltip("Gizmos 颜色(未选中时)。")]
        public Color Color = new Color(0f, 1f, 0.3f, 0.9f);
        [Tooltip("Gizmos 颜色(选中时)。")]
        public Color SelectedColor = new Color(1f, 0.6f, 0f, 1f);
        [Tooltip("未选中时是否也画(关掉 = 只选中时画,Scene 视图更干净)。")]
        public bool AlwaysDraw = true;

        [Header("Collision")]
        [Tooltip("阵营。碰撞服务按阵营配对:\n" +
                 "Player 弹撞 Enemy;Enemy 弹撞 Player;Neutral 不参与伤害判定。\n" +
                 "手动配置:玩家弹 prefab 标 Player,敌人弹 prefab 标 Enemy;PlayerHitbox/EnemyHitbox Reset() 已给默认值。")]
        public CollisionTeam Team = CollisionTeam.Neutral;

        public Vector2 Position => transform.position;

        /// <summary>世界空间 AABB(实时反映 transform.lossyScale)。</summary>
        public Rect WorldBounds
        {
            get
            {
                Vector3 ls = transform.lossyScale;
                Vector2 worldSize = new Vector2(
                    Mathf.Abs(Size.x * ls.x),
                    Mathf.Abs(Size.y * ls.y));
                Vector2 half = worldSize * 0.5f;
                return new Rect(Position - half, worldSize);
            }
        }

        /// <summary>
        /// 供 CollisionService 每帧一次性刷新:把当前 WorldBounds 写入缓存,避免高频碰撞循环内多次触发属性 get。
        /// 用法:在 Service.Tick 开头先 RefreshCachedBounds(),然后整帧走 _cachedBounds。
        /// </summary>
        public Rect _cachedBounds;
        public void RefreshCachedBounds()
        {
            _cachedBounds = WorldBounds;
        }

        void OnDrawGizmos()
        {
            if (!AlwaysDraw) return;
            DrawBounds(Color);
        }

        void OnDrawGizmosSelected()
        {
            DrawBounds(SelectedColor);
        }

        void DrawBounds(Color color)
        {
            Gizmos.color = color;
            Rect r = WorldBounds;
            Vector3 c = new Vector3(r.center.x, r.center.y, transform.position.z);
            Vector3 s = new Vector3(r.size.x, r.size.y, 0);
            Gizmos.DrawWireCube(c, s);
        }

        // ─── 碰撞便捷入口 ───
        public bool Overlaps(HitboxComponent other)
        {
            if (other == null) return false;
            return HitboxMath.AABBOverlap(WorldBounds, other.WorldBounds);
        }

        public bool OverlapsPoint(Vector2 worldPoint)
        {
            Rect r = WorldBounds;
            return worldPoint.x >= r.xMin && worldPoint.x <= r.xMax &&
                   worldPoint.y >= r.yMin && worldPoint.y <= r.yMax;
        }
    }
}