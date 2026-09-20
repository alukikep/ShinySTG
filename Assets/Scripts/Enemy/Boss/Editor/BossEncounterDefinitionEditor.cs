using System.Collections.Generic;
using ShinySTG.Level.Encounter;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI.Boss.Editor
{
    [CustomEditor(typeof(BossEncounterDefinition))]
    public sealed class BossEncounterDefinitionEditor : UnityEditor.Editor
    {
        static readonly HashSet<string> ManagedFields = new()
        {
            "ExitMode", "ExitTriggers", "ConditionMatch", "ExitConditions", "ProtectBarTransition", "ProtectedBarId"
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var boss = (BossEncounterDefinition)target;
            var health = boss.Bars;
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUI.BeginChangeCheck();
                DrawPropertiesExcluding(serializedObject, "m_Script", "Phases");
                if (EditorGUI.EndChangeCheck()) BossInspectorFields.RepairIds(serializedObject);
                if (GUILayout.Button("整理血管 ID（补齐空值、修复重复）"))
                    BossInspectorFields.RepairIds(serializedObject);
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
                health = boss.Bars;
                if (health != null)
                    foreach (var bar in health)
                    {
                        if (bar == null) continue;
                        var error = BossInspectorFields.BarError(health, bar.Id);
                        if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
                    }
                EditorGUILayout.HelpBox("进入：入场动作 → 进入指令 → 行为。退出：行为退出 → 退出指令 → 退场动作。死亡执行退出指令和整场击破动作。", MessageType.Info);
                var phases = serializedObject.FindProperty("Phases");
                EditorGUILayout.LabelField("阶段", EditorStyles.boldLabel);
                for (int i = 0; i < phases.arraySize; i++)
                {
                    var phase = phases.GetArrayElementAtIndex(i);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    phase.isExpanded = EditorGUILayout.Foldout(phase.isExpanded, $"阶段 {i + 1} · {phase.FindPropertyRelative("DisplayName")?.stringValue}", true);
                    bool up = GUILayout.Button("↑", GUILayout.Width(26)) && i > 0;
                    bool down = GUILayout.Button("↓", GUILayout.Width(26)) && i + 1 < phases.arraySize;
                    bool delete = GUILayout.Button("删除", GUILayout.Width(44));
                    EditorGUILayout.EndHorizontal();
                    if (up || down || delete)
                    {
                        EditorGUILayout.EndVertical();
                        if (delete) phases.DeleteArrayElementAtIndex(i);
                        else phases.MoveArrayElement(i, up ? i - 1 : i + 1);
                        break;
                    }
                    if (phase.managedReferenceValue == null)
                    {
                        if (GUILayout.Button("创建 Shooter 阶段")) phase.managedReferenceValue = NewPhase();
                    }
                    else if (phase.isExpanded) DrawPhase(phase, boss, health);
                    EditorGUILayout.EndVertical();
                    continue;
                }
                if (GUILayout.Button("添加 Shooter 阶段"))
                {
                    int index = phases.arraySize++;
                    phases.GetArrayElementAtIndex(index).managedReferenceValue = NewPhase();
                }
            }
            serializedObject.ApplyModifiedProperties();
            if (!boss.TryValidate(out var validationError)) EditorGUILayout.HelpBox(validationError, MessageType.Error);
            if (boss.BossPrefab != null && boss.BossPrefab.GetComponent<Boss>() == null)
                EditorGUILayout.HelpBox("Prefab 缺少 Boss 组件。", MessageType.Error);
        }

        static ShooterPhase NewPhase() => new ShooterPhase { ExitMode = PhaseExitMode.Conditions };

        static void DrawPhase(SerializedProperty phase, BossEncounterDefinition boss, BossHealth.HealthBar[] health)
        {
            // Draw derived phase fields and existing command drawers without replacing their editors.
            var child = phase.Copy();
            var end = phase.GetEndProperty();
            if (child.NextVisible(true))
                do
                {
                    if (SerializedProperty.EqualContents(child, end)) break;
                    if (!ManagedFields.Contains(child.name)) EditorGUILayout.PropertyField(child, true);
                } while (child.NextVisible(false));

            BossInspectorFields.Field(phase, "ExitMode", "退出条件模式");
            bool modern = phase.FindPropertyRelative("ExitMode").enumValueIndex == (int)PhaseExitMode.Conditions;
            if (!modern) BossInspectorFields.Field(phase, "ExitTriggers", "旧信号条件（任意满足）");
            else
            {
                BossInspectorFields.Field(phase, "ConditionMatch", "条件组合");
                var conditions = phase.FindPropertyRelative("ExitConditions");
                var summaries = new List<string>();
                for (int i = 0; i < conditions.arraySize; i++)
                {
                    var condition = conditions.GetArrayElementAtIndex(i);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    summaries.Add(DrawCondition(condition, boss, health));
                    bool remove = GUILayout.Button("删除条件");
                    EditorGUILayout.EndVertical();
                    if (remove) { conditions.DeleteArrayElementAtIndex(i); break; }
                }
                if (GUILayout.Button("添加条件"))
                {
                    int index = conditions.arraySize++;
                    var item = conditions.GetArrayElementAtIndex(index);
                    item.FindPropertyRelative("Kind").enumValueIndex = 0;
                    item.FindPropertyRelative("BarId").stringValue = "";
                    item.FindPropertyRelative("Threshold").floatValue = 50f;
                }
                string join = phase.FindPropertyRelative("ConditionMatch").enumValueIndex == 0 ? " 或 " : " 且 ";
                EditorGUILayout.HelpBox(summaries.Count == 0 ? "无退出条件：保持本阶段，直到 Boss 死亡或被停止。" : string.Join(join, summaries), MessageType.Info);
            }
            BossInspectorFields.Field(phase, "ProtectBarTransition", "保护阶段交接");
            if (phase.FindPropertyRelative("ProtectBarTransition").boolValue)
            {
                BossInspectorFields.BarPopup(phase.FindPropertyRelative("ProtectedBarId"), health, "触发保护的血管");
                EditorGUILayout.HelpBox("血管耗尽后会阻止继续扣血。请确保退出条件仍能满足；All 条件若还要求另一管受伤，保护将无法自动结束。", MessageType.Warning);
            }
        }

        static string DrawCondition(SerializedProperty condition, BossEncounterDefinition boss, BossHealth.HealthBar[] health)
        {
            BossInspectorFields.Field(condition, "Kind", "条件类型");
            var kind = (PhaseExitConditionKind)condition.FindPropertyRelative("Kind").enumValueIndex;
            string bar = "";
            if (kind == PhaseExitConditionKind.BarDepleted || kind == PhaseExitConditionKind.BarPercentAtMost)
            {
                var id = condition.FindPropertyRelative("BarId");
                BossInspectorFields.BarPopup(id, health, "目标血管");
                bar = BossInspectorFields.BarName(health, id.stringValue);
            }
            float threshold = condition.FindPropertyRelative("Threshold").floatValue;
            if (kind != PhaseExitConditionKind.BarDepleted && kind != PhaseExitConditionKind.Signal)
            {
                BossInspectorFields.Field(condition, "Threshold", kind == PhaseExitConditionKind.PhaseTimeAtLeast ? "至少持续（秒）" : "剩余不超过（%）");
                threshold = condition.FindPropertyRelative("Threshold").floatValue;
                if (float.IsNaN(threshold) || float.IsInfinity(threshold) || threshold < 0f ||
                    (kind != PhaseExitConditionKind.PhaseTimeAtLeast && threshold > 100f))
                    EditorGUILayout.HelpBox("阈值无效：秒数需为有限非负数，百分比需在 0～100。", MessageType.Error);
            }
            if (kind == PhaseExitConditionKind.Signal)
            {
                BossInspectorFields.Field(condition, "SignalTrigger", "高级信号");
                int index = condition.FindPropertyRelative("SignalTrigger").FindPropertyRelative("SignalIndex").intValue;
                if (boss.GetSignal(index) == null) EditorGUILayout.HelpBox("信号索引无效或信号为空。", MessageType.Error);
                return $"高级信号 [{index}]";
            }
            return kind switch
            {
                PhaseExitConditionKind.BarDepleted => $"{bar}耗尽",
                PhaseExitConditionKind.BarPercentAtMost => $"{bar} ≤ {threshold:0.##}%",
                PhaseExitConditionKind.TotalHpPercentAtMost => $"总血量 ≤ {threshold:0.##}%",
                PhaseExitConditionKind.PhaseTimeAtLeast => $"持续 ≥ {threshold:0.##} 秒",
                _ => "未知条件"
            };
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
