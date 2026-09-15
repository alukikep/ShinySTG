using ShinySTG.Stage;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.Debugging.Editor
{
    /// <summary>
    /// Scene 视图常驻 2D 坐标轴覆盖层。
    ///
    /// 用途:在 Scene 视图里实时显示 (0,0) 原点 + X/Y 主轴 + 整数刻度 + 数字标签 + 参考网格,
    ///       配置 FirePattern.PositionOffset / SpawnPosition / BoundsService 等带 Vector2 字段的
    ///       资产时,直接在 Scene 视图「量出」目标坐标对应的世界位置。
    ///
    /// 设计要点(对齐项目风格,见 CONTRIBUTING.md §1):
    ///   - 纯 Scene 视图覆盖层,Game 视图完全不画(Game 视图是玩家视角,污染画面)。
    ///   - 不挂 GameObject(订阅 SceneView.duringSceneGui 即可,场景里看不到任何 helper)。
    ///   - 不引入 asmdef、不进 build(放在 Editor/ 子文件夹,Unity 自动归 Assembly-CSharp-Editor)。
    ///   - 配置走 EditorWindow + EditorPrefs 持久化,无需建 ScriptableObject 资产污染 Project。
    ///
    /// 启用 / 配置:Unity 菜单 → Window → STG → Coordinate Axis Overlay
    /// (第一次打开窗口后所有 Inspector 字段实时生效,关掉 Unity 配置保留)。
    /// </summary>
    [InitializeOnLoad]
    internal static class CoordinateAxisOverlay
    {
        const string K_PREFIX = "ShinySTG.CoordAxis.";

        const bool   D_ENABLED      = true;
        const float  D_AXIS_EXTENT  = 20f;
        const float  D_GRID_EXTENT  = 10f;
        const float  D_MAJOR_STEP   = 1f;
        const float  D_MINOR_STEP   = 0.5f;
        const bool   D_SHOW_GRID    = true;
        const bool   D_SHOW_LABELS  = true;
        const bool   D_SHOW_STAGE   = true;
        static readonly Color D_X_COLOR      = new Color(1f,    0.25f, 0.25f, 0.9f);
        static readonly Color D_Y_COLOR      = new Color(0.25f, 1f,    0.25f, 0.9f);
        static readonly Color D_GRID_COLOR   = new Color(0.7f,  0.7f,  0.7f,  0.15f);
        static readonly Color D_LABEL_COLOR  = new Color(1f,    1f,    1f,    0.85f);
        static readonly Color D_CORNER_COLOR = new Color(1f,    0.85f, 0.2f,  0.9f);

        internal static bool Enabled        { get => EditorPrefs.GetBool  (K_PREFIX + "Enabled",    D_ENABLED);
                                       set => EditorPrefs.SetBool  (K_PREFIX + "Enabled",    value); }
        internal static float AxisExtent     { get => EditorPrefs.GetFloat (K_PREFIX + "AxisExtent", D_AXIS_EXTENT);
                                       set => EditorPrefs.SetFloat (K_PREFIX + "AxisExtent", value); }
        internal static float GridExtent     { get => EditorPrefs.GetFloat (K_PREFIX + "GridExtent", D_GRID_EXTENT);
                                       set => EditorPrefs.SetFloat (K_PREFIX + "GridExtent", value); }
        internal static float MajorStep      { get => EditorPrefs.GetFloat (K_PREFIX + "MajorStep",  D_MAJOR_STEP);
                                       set => EditorPrefs.SetFloat (K_PREFIX + "MajorStep",  value); }
        internal static float MinorStep      { get => EditorPrefs.GetFloat (K_PREFIX + "MinorStep",  D_MINOR_STEP);
                                       set => EditorPrefs.SetFloat (K_PREFIX + "MinorStep",  value); }
        internal static bool ShowGrid       { get => EditorPrefs.GetBool  (K_PREFIX + "ShowGrid",   D_SHOW_GRID);
                                       set => EditorPrefs.SetBool  (K_PREFIX + "ShowGrid",   value); }
        internal static bool ShowLabels     { get => EditorPrefs.GetBool  (K_PREFIX + "ShowLabels", D_SHOW_LABELS);
                                       set => EditorPrefs.SetBool  (K_PREFIX + "ShowLabels", value); }
        internal static bool ShowStageBounds{ get => EditorPrefs.GetBool  (K_PREFIX + "ShowStage",  D_SHOW_STAGE);
                                       set => EditorPrefs.SetBool  (K_PREFIX + "ShowStage",  value); }

        internal static Color XAxisColor
        {
            get => ColorFromHex(K_PREFIX + "XColor", D_X_COLOR);
            set => EditorPrefs.SetString(K_PREFIX + "XColor", "#" + ColorUtility.ToHtmlStringRGBA(value));
        }
        internal static Color YAxisColor
        {
            get => ColorFromHex(K_PREFIX + "YColor", D_Y_COLOR);
            set => EditorPrefs.SetString(K_PREFIX + "YColor", "#" + ColorUtility.ToHtmlStringRGBA(value));
        }
        internal static Color GridColor
        {
            get => ColorFromHex(K_PREFIX + "GridColor", D_GRID_COLOR);
            set => EditorPrefs.SetString(K_PREFIX + "GridColor", "#" + ColorUtility.ToHtmlStringRGBA(value));
        }
        internal static Color LabelColor
        {
            get => ColorFromHex(K_PREFIX + "LabelColor", D_LABEL_COLOR);
            set => EditorPrefs.SetString(K_PREFIX + "LabelColor", "#" + ColorUtility.ToHtmlStringRGBA(value));
        }
        internal static Color CornerColor
        {
            get => ColorFromHex(K_PREFIX + "CornerColor", D_CORNER_COLOR);
            set => EditorPrefs.SetString(K_PREFIX + "CornerColor", "#" + ColorUtility.ToHtmlStringRGBA(value));
        }

        static Color ColorFromHex(string key, Color fallback)
        {
            string hex = EditorPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(hex)) return fallback;
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : fallback;
        }

        static CoordinateAxisOverlay()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            if (!Enabled) return;
            var prevColor = Handles.color;
            try
            {
                if (ShowGrid)        DrawGrid();
                                   DrawAxes();
                                   DrawTicks();
                                   DrawOriginLabel();
                if (ShowStageBounds) DrawStageBoundsCorners();
            }
            finally
            {
                Handles.color = prevColor;
            }
        }

        // 参考网格(整数 1 单位灰色细线,辅助目测远处的点)
        static void DrawGrid()
        {
            float extent = Mathf.Max(0f, GridExtent);
            float step   = Mathf.Max(0.01f, MajorStep);
            if (extent <= 0f) return;

            Handles.color = GridColor;
            int count = Mathf.CeilToInt(extent / step);
            for (int i = -count; i <= count; i++)
            {
                if (i == 0) continue; // 跳过主轴位置,避免覆盖
                float p = i * step;
                Handles.DrawLine(new Vector3(p, -extent, 0), new Vector3(p, extent, 0));
                Handles.DrawLine(new Vector3(-extent, p, 0), new Vector3(extent, p, 0));
            }
        }

        // 主轴 X(红) / Y(绿),端点带箭头
        static void DrawAxes()
        {
            float extent = Mathf.Max(0.1f, AxisExtent);

            Handles.color = XAxisColor;
            Handles.DrawLine(new Vector3(-extent, 0, 0), new Vector3(extent, 0, 0), 2.5f);
            DrawArrowHead(new Vector3(extent, 0, 0), 0f, XAxisColor);

            Handles.color = YAxisColor;
            Handles.DrawLine(new Vector3(0, -extent, 0), new Vector3(0, extent, 0), 2.5f);
            DrawArrowHead(new Vector3(0, extent, 0), 90f, YAxisColor);
        }

        static void DrawArrowHead(Vector3 tip, float angleDeg, Color color)
        {
            const float size = 0.25f;
            float a = angleDeg * Mathf.Deg2Rad;
            // 箭头两端相对 tip 后退 size,展开 0.5 弧度
            Vector3 left  = tip + new Vector3(-Mathf.Cos(a - 0.5f), -Mathf.Sin(a - 0.5f), 0) * size;
            Vector3 right = tip + new Vector3(-Mathf.Cos(a + 0.5f), -Mathf.Sin(a + 0.5f), 0) * size;
            Handles.color = color;
            Handles.DrawLine(tip, left,  2f);
            Handles.DrawLine(tip, right, 2f);
        }

        // 刻度 + 数字标签
        static void DrawTicks()
        {
            float extent = Mathf.Max(0.1f, AxisExtent);
            float major  = Mathf.Max(0.01f, MajorStep);
            float minor  = Mathf.Max(0f,    MinorStep);

            int count = Mathf.CeilToInt(extent / major);

            // 主刻度短线(±0.08 高)
            Handles.color = XAxisColor;
            for (int i = -count; i <= count; i++)
            {
                if (i == 0) continue;
                float p = i * major;
                Handles.DrawLine(new Vector3(p, -0.08f, 0), new Vector3(p, 0.08f, 0), 1.5f);
            }

            Handles.color = YAxisColor;
            for (int i = -count; i <= count; i++)
            {
                if (i == 0) continue;
                float p = i * major;
                Handles.DrawLine(new Vector3(-0.08f, p, 0), new Vector3(0.08f, p, 0), 1.5f);
            }

            // 次刻度短线(±0.04 高,不画标签)
            if (minor > 0f && minor < major)
            {
                var minorColor = new Color(
                    (XAxisColor.r + YAxisColor.r) * 0.5f,
                    (XAxisColor.g + YAxisColor.g) * 0.5f,
                    (XAxisColor.b + YAxisColor.b) * 0.5f,
                    0.6f);
                Handles.color = minorColor;
                int minorCount = Mathf.CeilToInt(extent / minor);
                for (int i = -minorCount; i <= minorCount; i++)
                {
                    float p = i * minor;
                    // 跳过主刻度位置(避免和主刻度重合)
                    float rem = Mathf.Abs(Mathf.Repeat(p, major));
                    if (rem < 0.001f || Mathf.Abs(rem - major) < 0.001f) continue;
                    Handles.DrawLine(new Vector3(p,     -0.04f, 0), new Vector3(p,     0.04f, 0));
                    Handles.DrawLine(new Vector3(-0.04f, p,    0), new Vector3(0.04f,  p,    0));
                }
            }

            // 数字标签(主刻度间距太密时自动抽稀,避免重叠)
            if (ShowLabels)
            {
                Handles.color = LabelColor;
                int labelStep = 1;
                if (major < 0.5f) labelStep = Mathf.Max(1, Mathf.CeilToInt(0.5f / major));
                for (int i = -count; i <= count; i++)
                {
                    if (i == 0) continue;
                    if ((i % labelStep) != 0) continue;
                    float p = i * major;
                    Handles.Label(new Vector3(p, -0.3f, 0), p.ToString("0.##"));
                    Handles.Label(new Vector3(-0.3f, p, 0), p.ToString("0.##"));
                }
            }
        }

        // 原点标记
        static void DrawOriginLabel()
        {
            Handles.color = LabelColor;
            Handles.DrawSolidDisc(Vector3.zero, Vector3.forward, 0.08f);
            Handles.Label(new Vector3(0.3f, -0.3f, 0), "(0, 0)");
        }

        // 舞台边界 4 角标(联动 BoundsService;没挂时走 PlayerMovement 同款 fallback)
        static void DrawStageBoundsCorners()
        {
            var bs = BoundsService.Instance;
            Rect playable, culling;
            if (bs != null)
            {
                playable = bs.PlayableArea;
                culling  = bs.CullingArea;
            }
            else
            {
                // fallback 与 PlayerMovement / BoundsService 默认值一致
                playable = new Rect(-3.5f, -4.5f, 7f, 9f);
                culling  = new Rect(-10f,  -10f,  20f, 20f);
            }

            Handles.color = CornerColor;
            DrawCornerMarker(new Vector2(playable.xMin, playable.yMin));
            DrawCornerMarker(new Vector2(playable.xMax, playable.yMin));
            DrawCornerMarker(new Vector2(playable.xMin, playable.yMax));
            DrawCornerMarker(new Vector2(playable.xMax, playable.yMax));

            if (ShowLabels)
            {
                Handles.Label(
                    new Vector3(playable.xMin, playable.yMax + 0.4f, 0),
                    string.Format("Playable: {0:F1}~{1:F1} x {2:F1}~{3:F1}",
                        playable.xMin, playable.xMax, playable.yMin, playable.yMax));
                Handles.Label(
                    new Vector3(culling.xMin,  culling.yMax  + 0.4f, 0),
                    string.Format("Culling:  {0:F1}~{1:F1} x {2:F1}~{3:F1}",
                        culling.xMin, culling.xMax, culling.yMin, culling.yMax));
            }
        }

        static void DrawCornerMarker(Vector2 p)
        {
            const float s = 0.2f;
            // 小三角形(底边水平、顶点朝上),作为 PlayableArea 4 角标
            Vector3 v0 = new Vector3(p.x,         p.y + s,         0);
            Vector3 v1 = new Vector3(p.x - s,     p.y - s * 0.5f,  0);
            Vector3 v2 = new Vector3(p.x + s,     p.y - s * 0.5f,  0);
            Handles.DrawLine(v0, v1, 2f);
            Handles.DrawLine(v1, v2, 2f);
            Handles.DrawLine(v2, v0, 2f);
        }

        // Inspector 入口
        [MenuItem("Window/STG/Coordinate Axis Overlay")]
        static void OpenWindow()
        {
            var win = EditorWindow.GetWindow<CoordinateAxisOverlayWindow>("Coord Axis");
            win.minSize = new Vector2(320, 380);
            win.Show();
        }
    }

    // Inspector 面板:与 CoordinateAxisOverlay 平级放在同一 namespace,
    // 避免 static class 嵌套 EditorWindow 的访问性尴尬;同文件方便维护。
    internal sealed class CoordinateAxisOverlayWindow : EditorWindow
    {
        void OnEnable()
        {
            titleContent = new GUIContent("Coord Axis");
        }

        void OnGUI()
        {
            EditorGUI.BeginChangeCheck();

            bool enabled = EditorGUILayout.ToggleLeft(
                new GUIContent("Enabled (Scene 视图常驻坐标轴总开关)",
                               "勾掉 -> Scene 视图完全停止画轴、刻度、网格、角标。"),
                CoordinateAxisOverlay.Enabled);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Extents & Steps", EditorStyles.boldLabel);
            float axisExtent = EditorGUILayout.FloatField(
                new GUIContent("Axis Extent",
                               "主轴线长度,世界坐标对称 +-extent(默认 20 覆盖整个 CullingArea)。"),
                CoordinateAxisOverlay.AxisExtent);
            float gridExtent = EditorGUILayout.FloatField(
                new GUIContent("Grid Extent",
                               "参考网格覆盖范围,对称 +-extent(0 = 不画网格)。"),
                CoordinateAxisOverlay.GridExtent);
            float major = EditorGUILayout.FloatField(
                new GUIContent("Major Step",
                               "主刻度间距(数字标签步长)。"),
                CoordinateAxisOverlay.MajorStep);
            float minor = EditorGUILayout.FloatField(
                new GUIContent("Minor Step",
                               "次刻度间距(0 = 不画次刻度;必须 < Major Step)。"),
                CoordinateAxisOverlay.MinorStep);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Toggles", EditorStyles.boldLabel);
            bool showGrid = EditorGUILayout.ToggleLeft(
                new GUIContent("Show Grid", "灰色细网格,辅助目测远处的点。"),
                CoordinateAxisOverlay.ShowGrid);
            bool showLabels = EditorGUILayout.ToggleLeft(
                new GUIContent("Show Labels",
                               "刻度数字 + 原点 (0, 0) 标签 + 边界尺寸文字。"),
                CoordinateAxisOverlay.ShowLabels);
            bool showStage = EditorGUILayout.ToggleLeft(
                new GUIContent("Show Stage Bounds Corners",
                               "联动 BoundsService 画 PlayableArea 4 角标 + 尺寸文字。"),
                CoordinateAxisOverlay.ShowStageBounds);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);
            Color xc = EditorGUILayout.ColorField("X Axis Color",    CoordinateAxisOverlay.XAxisColor);
            Color yc = EditorGUILayout.ColorField("Y Axis Color",    CoordinateAxisOverlay.YAxisColor);
            Color gc = EditorGUILayout.ColorField("Grid Color",      CoordinateAxisOverlay.GridColor);
            Color lc = EditorGUILayout.ColorField("Label Color",     CoordinateAxisOverlay.LabelColor);
            Color cc = EditorGUILayout.ColorField("Corner Color",    CoordinateAxisOverlay.CornerColor);

            EditorGUILayout.Space();
            if (GUILayout.Button("Reset to Defaults (清掉所有本工具 EditorPrefs)"))
            {
                ResetPrefs();
                SceneView.RepaintAll();
            }

            if (EditorGUI.EndChangeCheck())
            {
                CoordinateAxisOverlay.Enabled         = enabled;
                CoordinateAxisOverlay.AxisExtent      = Mathf.Max(0.1f, axisExtent);
                CoordinateAxisOverlay.GridExtent      = Mathf.Max(0f,   gridExtent);
                CoordinateAxisOverlay.MajorStep       = Mathf.Max(0.01f, major);
                CoordinateAxisOverlay.MinorStep       = Mathf.Max(0f,   minor);
                CoordinateAxisOverlay.ShowGrid        = showGrid;
                CoordinateAxisOverlay.ShowLabels      = showLabels;
                CoordinateAxisOverlay.ShowStageBounds = showStage;
                CoordinateAxisOverlay.XAxisColor      = xc;
                CoordinateAxisOverlay.YAxisColor      = yc;
                CoordinateAxisOverlay.GridColor       = gc;
                CoordinateAxisOverlay.LabelColor      = lc;
                CoordinateAxisOverlay.CornerColor     = cc;
                SceneView.RepaintAll();
            }
        }

        static void ResetPrefs()
        {
            string[] keys = {
                "Enabled", "AxisExtent", "GridExtent", "MajorStep", "MinorStep",
                "ShowGrid", "ShowLabels", "ShowStage",
                "XColor", "YColor", "GridColor", "LabelColor", "CornerColor"
            };
            foreach (var k in keys) EditorPrefs.DeleteKey("ShinySTG.CoordAxis." + k);
        }
    }
}
