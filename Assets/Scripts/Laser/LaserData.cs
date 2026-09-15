using UnityEngine;

namespace ShinySTG.Laser
{
    /// <summary>
    /// 激光的可复用视觉 + 判定参数集合。对齐参考材料 §17 + §6 视觉/判定分离。
    ///
    /// 设计要点:
    ///   - 视觉宽度(VisualWidth) 与 判定宽度(CollisionWidth) 独立(参考材料 §6)。
    ///     经典配置:VisualWidth = 0.8(贴图宽),CollisionWidth = 0.15(实际判定)。
    ///   - 生命周期四段时间全部可配,State 切换由 LaserEntity 自动按时间推进。
    ///   - 生命周期内碰撞开关策略(CollideInWarning / Expanding / Shrinking)独立控制,
    ///     默认全 false(只在 Active 期命中),与东方正作常见行为一致。
    /// </summary>
    [CreateAssetMenu(menuName = "STG/Laser/Laser Data")]
    public class LaserData : ScriptableObject
    {
        [Header("Geometry")]
        [Tooltip("视觉宽度(贴图拉伸宽)。仅用于渲染,不影响碰撞。\n" +
                 "经典配置:0.8(贴图 32px × 缩放 0.025)。")]
        public float VisualWidth = 0.8f;

        [Tooltip("实际判定宽度(碰撞半径)。通常远小于 VisualWidth(参考材料 §6)。\n" +
                 "经典配置:0.15(STG 玩家判定约 0.1,激光判定 ≈ 玩家判定的 1.5 倍)。")]
        public float CollisionWidth = 0.15f;

        [Tooltip("默认最大长度(世界单位)。运行时可在 Pattern / Action 里覆盖。\n" +
                 "经典配置:20~30(覆盖全屏对角)。")]
        public float MaxLength = 24f;

        [Header("Lifecycle")]
        [Tooltip("Warning 阶段持续秒数(预警线显示,碰撞关闭)。")]
        public float WarningTime = 0.6f;

        [Tooltip("Expanding 阶段持续秒数(激光从 0 长度增长到 MaxLength)。")]
        public float ExpandTime = 0.3f;

        [Tooltip("Active 阶段持续秒数(全长,碰撞开启)。")]
        public float ActiveTime = 1.5f;

        [Tooltip("Shrinking 阶段持续秒数(激光从 MaxLength 收到 0)。")]
        public float ShrinkTime = 0.3f;

        [Header("Lifecycle Collision Policy")]
        [Tooltip("Warning 期碰撞开关。默认 false(预警不命中,与东方正作一致)。")]
        public bool CollideInWarning = false;

        [Tooltip("Expanding 期碰撞开关。默认 false(正在展开时不命中,头部难判)。")]
        public bool CollideInExpanding = false;

        [Tooltip("Shrinking 期碰撞开关。默认 false(正在收缩时不命中)。")]
        public bool CollideInShrinking = false;

        [Header("Visual (Body / 激光本体)")]
        [Tooltip("激光条状贴图。可选,留空则只用头尾贴图或纯色。\n" +
                 "推荐:左右拼接的白色长条 Sprite,运行时按 VisualWidth 拉伸、按 CurrentLength 决定是否显示。")]
        public Sprite Sprite;

        [Tooltip("激光本体颜色(贴图 tint)。")]
        public Color BodyColor = new Color(1f, 0.85f, 0.4f, 1f);

        [Header("Visual (Head / 预警与发光头)")]
        [Tooltip("激光头贴图(可选)。")]
        public Sprite HeadSprite;

        [Tooltip("Warning 期颜色(预警线,通常带透明)。")]
        public Color WarningColor = new Color(1f, 0.3f, 0.3f, 0.55f);

        [Tooltip("Active 期激光头颜色(可与 BodyColor 不同,做发光感)。")]
        public Color HeadColor = new Color(1f, 1f, 1f, 1f);
    }
}
