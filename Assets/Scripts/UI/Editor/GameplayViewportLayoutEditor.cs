using UnityEditor;
using UnityEngine;

namespace ShinySTG.UI.Editor
{
    [CustomEditor(typeof(GameplayViewportLayout))]
    internal sealed class GameplayViewportLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("打开 HUD 布局窗口")) GameplayHudLayoutWindow.Open((GameplayViewportLayout)target);
            EditorGUILayout.HelpBox("仅编辑 UI。位置由 RectTransform 决定，分辨率适配由 CanvasScaler 决定。进入 Play 不会重新布局。", MessageType.Info);
        }
    }
}
