using UnityEditor;
using UnityEngine;

namespace ShinySTG.Background.Editor
{
    [CustomEditor(typeof(BackgroundDefinition))]
    internal sealed class BackgroundDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "OverrideClearColor", "ClearColor");
            var overrideColor = serializedObject.FindProperty("OverrideClearColor");
            EditorGUILayout.PropertyField(overrideColor, new GUIContent("覆盖背景底色"));
            using (new EditorGUI.DisabledScope(!overrideColor.boolValue))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ClearColor"));
            serializedObject.ApplyModifiedProperties();

            var definition = (BackgroundDefinition)target;
            if (!definition.OverrideClearColor)
            {
                EditorGUILayout.HelpBox("换景保留当前相机底色与清屏模式。材质雾色独立配置；请在实际场景中检查远景是否融合。", MessageType.Info);
                return;
            }
            EditorGUILayout.HelpBox("换景将使用此底色并切换为 Solid Color，不修改材质雾色。底色与雾色不同可能产生明显边界。", MessageType.Info);
            if (definition.ContentPrefab == null) return;
            foreach (var renderer in definition.ContentPrefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || !material.HasProperty("_BackgroundFogColor")) continue;
                    var fog = material.GetColor("_BackgroundFogColor");
                    var clear = definition.ClearColor;
                    float difference = Mathf.Max(Mathf.Abs(fog.r - clear.r), Mathf.Abs(fog.g - clear.g), Mathf.Abs(fog.b - clear.b));
                    if (difference <= 0.02f) continue;
                    EditorGUILayout.HelpBox($"材质 {material.name} 的雾色与底色不同。请确认这是预期配色；工具不会自动同步。", MessageType.Warning);
                    return;
                }
            }
        }
    }
}
