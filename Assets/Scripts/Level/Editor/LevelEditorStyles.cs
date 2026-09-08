using ShinySTG.Level;
using ShinySTG.Level.SpawnEntries;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.Level.Editor
{
    /// <summary>
    /// 关卡编辑器共享样式/常量。所有 IMGUI 颜色/尺寸集中在这里,改起来一处生效。
    /// </summary>
    internal static class LevelEditorStyles
    {
        // ─── 颜色(按 SpawnEntry 类型上色,默认实现沿用)───────────────────────
        public static readonly Color ColorSimple = new Color(0.40f, 0.75f, 0.40f); // 绿
        public static readonly Color ColorWave   = new Color(0.40f, 0.60f, 0.95f); // 蓝
        public static readonly Color ColorBoss   = new Color(0.95f, 0.50f, 0.35f); // 橙红
        public static readonly Color ColorOther  = new Color(0.65f, 0.65f, 0.65f); // 灰
        public static readonly Color ColorSelected = new Color(1f, 0.85f, 0.30f);
        public static readonly Color ColorTimelineBg = new Color(0.18f, 0.18f, 0.18f);
        public static readonly Color ColorRuler     = new Color(0.30f, 0.30f, 0.30f);
        public static readonly Color ColorCursor    = new Color(1f, 0.85f, 0.30f, 0.85f);

        // ─── 尺寸 ─────────────────────────────────────────────────────────
        public const float RulerHeight       = 22f;
        public const float BlockHeight       = 38f;
        public const float BlockMinWidth     = 16f;      // 再短的 entry 也有这个最小宽度,保证可见
        public const float BlockPointWidth   = 140f;     // Duration=0 的瞬时点默认宽度
        public const float BlockEdgeGrabWidth = 6f;      // 拖右边缘改 Duration 的命中宽度
        public const float BlockSustainSolidWidth = 8f;  // 持续型 block 左侧实色标识宽度
        public const float TrackTopPadding   = 6f;
        public const float TrackLaneGap      = 4f;       // 多 lane(堆叠)间 y 间距
        public const float PixelsPerSecondMin = 10f;
        public const float PixelsPerSecondMax = 400f;
        public const float DefaultPixelsPerSecond = 50f;

        // ─── GUIStyle 缓存 ───────────────────────────────────────────────
        static GUIStyle _blockNormal;
        static GUIStyle _blockSelected;
        static GUIStyle _rulerLabel;
        static GUIStyle _detailTitle;

        public static GUIStyle BlockNormal => _blockNormal ??= new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleLeft,
            padding   = new RectOffset(6, 6, 0, 0),
            fontStyle = FontStyle.Normal,
            fontSize  = 11,
            margin    = new RectOffset(0, 0, 0, 0),
        };

        public static GUIStyle BlockSelected => _blockSelected ??= new GUIStyle(BlockNormal)
        {
            fontStyle = FontStyle.Bold,
        };

        public static GUIStyle RulerLabel => _rulerLabel ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = new Color(0.85f, 0.85f, 0.85f) },
        };

        public static GUIStyle DetailTitle => _detailTitle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
        };

        // ─── 工具 ─────────────────────────────────────────────────────────
        public static Color GetTypeColor(SpawnEntry entry)
        {
            switch (entry)
            {
                case SimpleSpawnEntry _: return ColorSimple;
                case WaveSpawnEntry   _: return ColorWave;
                case BossSpawnEntry   _: return ColorBoss;
                default:                    return ColorOther;
            }
        }

        public static string GetTypeShortName(SpawnEntry entry) => entry?.GetType().Name ?? "Null";
    }
}