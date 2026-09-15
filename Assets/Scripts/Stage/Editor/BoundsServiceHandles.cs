using ShinySTG.Stage;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.Stage.Editor
{
    /// <summary>
    /// Scene 视图拖拽手柄:让美术 / 关卡设计直接在 Scene 视图里拖动
    /// <see cref="BoundsService.PlayableArea"/> 与 <see cref="BoundsService.CullingArea"/> 的 4 个边。
    ///
    /// 复用 <see cref="LevelSceneGizmos"/> 的同款套路:
    ///   - [InitializeOnLoad] 自动订阅 SceneView.duringSceneGui(无需手动 Enable)
    ///   - 静态事件 + namespace ShinySTG.Stage.Editor,放在 Editor/ 子目录,只走 Assembly-CSharp-Editor(不影响运行时构建)
    ///
    /// 行为:
    ///   - 场景里没挂 BoundsService 时 → 不画手柄(避免空 Scene 一片黄点)。
    ///   - 选中有 BoundsService 的 GameObject 时 → 4 边各 1 个 PositionHandle(共 8 个),拖动改写 Rect。
    ///   - 拖动期间 Undo.RecordObject,松开自动 dirty,Unity 会标 * 在 Hierarchy 里。
    ///
    /// 为什么不用自定义 Editor(Editor.BoundsServiceInspector):
    ///   - Inspector 改数字已经很精确;手柄改的是"快速拖出大致范围"。
    ///   - 手柄在 Scene 视图里与 Gizmo 同框显示,所见即所得,不需要打开 Inspector 面板。
    /// </summary>
    [InitializeOnLoad]
    internal static class BoundsServiceHandles
    {
        const float HANDLE_SIZE = 0.18f; // 手柄在世界空间的大小,会被 Scene 视图自动按距离缩放

        static BoundsServiceHandles()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            var bs = BoundsService.Instance;
            if (bs == null) return;

            // 选中 BoundsService 所在 GameObject(或任意子物体)时才显示手柄,避免干扰其他选中。
            // 不强制:很多项目希望"只要 BoundsService 存在就显示",所以这里只看 Instance,不限 Selection。
            var prev = Handles.color;

            bs.PlayableArea = DrawResizableRect(bs.PlayableArea, bs.PlayableColor, "Playable");
            bs.CullingArea = DrawResizableRect(bs.CullingArea, bs.CullingColor, "Culling");

            Handles.color = prev;
        }

        /// <summary>
        /// 画一个可拖拽的矩形(4 个 PositionHandle 分别控制 minX / maxX / minY / maxY)。
        /// 返回调整后的 Rect。手柄颜色 = rect 的 gizmo 颜色。
        /// </summary>
        static Rect DrawResizableRect(Rect r, Color color, string label)
        {
            Handles.color = color;

            // 4 个边的中点(用户更直觉地"拉边"而非"拉角")
            Vector3 midTop    = new Vector3(r.center.x, r.yMax, 0f);
            Vector3 midBottom = new Vector3(r.center.x, r.yMin, 0f);
            Vector3 midLeft   = new Vector3(r.xMin, r.center.y, 0f);
            Vector3 midRight  = new Vector3(r.xMax, r.center.y, 0f);

            EditorGUI.BeginChangeCheck();
            Vector3 newTop    = Handles.PositionHandle(midTop,    Quaternion.identity);
            Vector3 newBottom = Handles.PositionHandle(midBottom, Quaternion.identity);
            Vector3 newLeft   = Handles.PositionHandle(midLeft,   Quaternion.identity);
            Vector3 newRight  = Handles.PositionHandle(midRight,  Quaternion.identity);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(BoundsService.Instance, $"Resize {label} Area");
                float newYMax = Mathf.Max(newTop.y,    r.yMin + 0.01f);
                float newYMin = Mathf.Min(newBottom.y, newYMax - 0.01f);
                float newXMax = Mathf.Max(newRight.x,  r.xMin + 0.01f);
                float newXMin = Mathf.Min(newLeft.x,   newXMax - 0.01f);

                r = Rect.MinMaxRect(newXMin, newYMin, newXMax, newYMax);
            }

            // 在矩形中心标个 label,方便识别哪块是哪个区域
            var labelPos = new Vector3(r.center.x, r.yMax + 0.15f, 0f);
            Handles.Label(labelPos, label);

            return r;
        }
    }
}
