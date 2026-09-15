using UnityEngine;

namespace ShinySTG.Stage
{
    /// <summary>
    /// 舞台边界管理器(场景单例)。
    ///
    /// 统一配置两块世界坐标矩形,所有"边界相关"的系统从这里读:
    ///   1. <see cref="PlayableArea"/> — 玩家活动区(矩形 clamp)。PlayerMovement 每帧 Clamp 到这块。
    ///   2. <see cref="CullingArea"/>  — 子弹回收区。飞出此矩形即由 BulletPool.Return 回池。
    ///
    /// 两者语义不同,Inspector 独立配置(不强制 Culling 覆盖 Playable —— 留缓冲 / 让美术自由调):
    ///   - 经典配置:CullingArea 略大于 PlayableArea,玩家活动区四周留 ~1 单位缓冲,避免"飞出去的瞬间才被回收"的边缘诡异行为。
    ///   - 极端配置:CullingArea 可以 < PlayableArea(玩家在边界内开火,但子弹超 Culling 立刻被回收)—— 用于特殊演出。
    ///
    /// 没挂本组件的场景:
    ///   - PlayerMovement fallback:内置 ±3.5 / ±4.5(本组件默认值)。
    ///   - Bullet fallback:硬编码 ±10 / ±20(历史行为)。
    /// 保证旧场景无需迁移即可继续运行。
    ///
    /// 可视化:
    ///   - 运行期 + 编辑期:OnDrawGizmos 画两块彩色矩形(PlayableColor / CullingColor),无需选中也能看到。
    ///   - 编辑器增强(Scene 视图拖拽手柄):见 Assets/Scripts/Stage/Editor/BoundsServiceHandles.cs。
    ///
    /// 协作边界:
    ///   - 本组件不读 / 不写 Player / Bullet 的位置字段,只暴露 Rect + ClampToPlayable + ContainsCulling 静态访问点。
    ///   - 任何需要"边界语义"的系统都通过 BoundsService.Instance 读,不再各自硬编码 magic number。
    /// </summary>
    [DisallowMultipleComponent]
    public class BoundsService : MonoBehaviour
    {
        public static BoundsService Instance { get; private set; }

        [Header("Player Playable Area(玩家活动区,世界坐标矩形)")]
        [Tooltip("玩家被 Clamp 到此矩形内。世界坐标,x=左,y=下。\n" +
                 "经典 STG:6.0~7.0 宽 × 8.0~10.0 高。\n" +
                 "PlayerMovement.Update 每帧 ClampToPlayable(...)。")]
        public Rect PlayableArea = new Rect(-3.5f, -4.5f, 7f, 9f);
        [Tooltip("PlayableArea 在 Scene 视图的 Gizmo 颜色(未选中)。")]
        public Color PlayableColor = new Color(0.2f, 1f, 0.4f, 0.9f);

        [Header("Bullet Culling Area(子弹回收区,世界坐标矩形)")]
        [Tooltip("子弹飞出此矩形即回池。世界坐标,x=左,y=下。\n" +
                 "建议:CullingArea 范围 ≥ PlayableArea,留 ~1 单位缓冲,避免「飞出去的瞬间才被回收」的边缘诡异行为。\n" +
                 "Bullet.Update 每帧检查 ContainsCulling(pos)。")]
        public Rect CullingArea = new Rect(-10f, -10f, 20f, 20f);
        [Tooltip("CullingArea 在 Scene 视图的 Gizmo 颜色(未选中)。")]
        public Color CullingColor = new Color(1f, 0.4f, 0.2f, 0.85f);

        [Header("Debug")]
        [Tooltip("未选中时是否也画 Gizmo(false = 只选中时画,Scene 视图更干净)。")]
        public bool AlwaysDraw = true;

        // ─── 便捷只读接口 ───

        /// <summary>PlayableArea 左下角(xMin, yMin)。</summary>
        public Vector2 PlayableMin => new Vector2(PlayableArea.xMin, PlayableArea.yMin);

        /// <summary>PlayableArea 右上角(xMax, yMax)。</summary>
        public Vector2 PlayableMax => new Vector2(PlayableArea.xMax, PlayableArea.yMax);

        /// <summary>CullingArea 左下角(xMin, yMin)。</summary>
        public Vector2 CullingMin => new Vector2(CullingArea.xMin, CullingArea.yMin);

        /// <summary>CullingArea 右上角(xMax, yMax)。</summary>
        public Vector2 CullingMax => new Vector2(CullingArea.xMax, CullingArea.yMax);

        /// <summary>点是否在 PlayableArea 内(含边界)。</summary>
        public bool ContainsPlayable(Vector2 p) => PlayableArea.Contains(p);

        /// <summary>点是否在 CullingArea 内(含边界)。子弹 Update 用这个判越界。</summary>
        public bool ContainsCulling(Vector2 p) => CullingArea.Contains(p);

        /// <summary>把点 Clamp 到 PlayableArea 内(返回新 Vector2,不动入参)。PlayerMovement 用。</summary>
        public Vector2 ClampToPlayable(Vector2 p) => new Vector2(
            Mathf.Clamp(p.x, PlayableArea.xMin, PlayableArea.xMax),
            Mathf.Clamp(p.y, PlayableArea.yMin, PlayableArea.yMax));

        // ─── 生命周期 ───

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // 场景里同时挂多份是配置错误,不要互相覆盖,直接销毁多余实例避免单例被抢。
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── Scene 可视化(运行期 + 编辑期都生效) ───

        void OnDrawGizmos()
        {
            if (!AlwaysDraw) return;
            DrawRectGizmo(PlayableArea, PlayableColor);
            DrawRectGizmo(CullingArea, CullingColor);
        }

        static void DrawRectGizmo(Rect r, Color color)
        {
            // 与 HitboxComponent.DrawBounds 同款画法,Rect → WireCube(z=0)。
            var center = new Vector3(r.center.x, r.center.y, 0f);
            var size = new Vector3(r.size.x, r.size.y, 0f);
            Gizmos.color = color;
            Gizmos.DrawWireCube(center, size);
        }
    }
}
