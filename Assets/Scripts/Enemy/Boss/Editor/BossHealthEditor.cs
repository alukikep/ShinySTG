using UnityEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI.Boss.Editor
{
    [CustomEditor(typeof(BossHealth))]
    public sealed class BossHealthEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("血管统一在 Boss Encounter SO 配置。此组件仅持有本场战斗的血量状态。", MessageType.Info);
            var health = (BossHealth)target;
            if (Application.isPlaying)
                EditorGUILayout.LabelField("实时血量", $"{health.CurrentHp:0.##} / {health.MaxHp:0.##} · 无敌 {health.IsInvincible}");
        }
        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
