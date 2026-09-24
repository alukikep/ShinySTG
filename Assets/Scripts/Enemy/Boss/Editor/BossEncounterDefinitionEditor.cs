using System.Collections.Generic;
using ShinySTG.Level.Encounter;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI.Boss.Editor
{
    [CustomEditor(typeof(BossEncounterDefinition))]
    public sealed class BossEncounterDefinitionEditor : UnityEditor.Editor
    {
        static readonly HashSet<string> HiddenStateFields = new()
        {
            "ExitMode", "ExitTriggers", "ConditionMatch", "ExitConditions", "ProtectBarTransition", "ProtectedBarId",
            "AdvanceMode", "AdvanceAfterSeconds", "AdvanceAtPercent"
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var boss = (BossEncounterDefinition)target;
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                DrawPropertiesExcluding(serializedObject, "m_Script", "Phases", "Loop", "Bars", "StartBarId");
                if (GUILayout.Button("整理血管 ID（补齐空值、修复重复）"))
                {
                    BossInspectorFields.RepairIds(serializedObject);
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();
                }
                BossInspectorFields.BarPopup(serializedObject.FindProperty("StartBarId"), boss.Bars, "开始血管");
                EditorGUILayout.HelpBox("每管打空必定换管；启用时限后，超时也会换管。NextBar 为空时结束战斗。管内状态顺序执行，最后状态保持到本管结束；过渡等待不计时且不能受伤。", MessageType.Info);
                var bars = serializedObject.FindProperty("Bars");
                for (int i = 0; i < bars.arraySize; i++)
                {
                    var bar = bars.GetArrayElementAtIndex(i);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    bar.isExpanded = EditorGUILayout.Foldout(bar.isExpanded, $"血管 {i + 1} · {bar.FindPropertyRelative("Name").stringValue}", true);
                    bool remove = false;
                    if (bar.isExpanded)
                    {
                        BossInspectorFields.Field(bar, "Id", "稳定 ID");
                        BossInspectorFields.Field(bar, "Name", "名称");
                        var segments = bar.FindPropertyRelative("Segments");
                        if (segments.arraySize == 0)
                        {
                            EditorGUILayout.LabelField("模式", "整管血量（兼容配置）");
                            BossInspectorFields.Field(bar, "MaxHp", "血量上限");
                            BossInspectorFields.Field(bar, "HasTimeLimit", "启用时限");
                            if (bar.FindPropertyRelative("HasTimeLimit").boolValue)
                                BossInspectorFields.Field(bar, "TimeLimit", "时限（秒）");
                            DrawStates(bar.FindPropertyRelative("States"));
                            if (GUILayout.Button("将本管转为单个分段（保留原配置，可撤销）")) ConvertToSegment(bar);
                        }
                        else DrawSegments(segments);
                        EditorGUILayout.LabelField("打空换管", "始终启用");
                        BossInspectorFields.BarPopup(bar.FindPropertyRelative("NextBarId"), boss.Bars, "下一血管", "无（结束战斗）");
                        remove = GUILayout.Button("删除血管");
                    }
                    EditorGUILayout.EndVertical();
                    if (remove) { bars.DeleteArrayElementAtIndex(i); break; }
                }
                if (GUILayout.Button("添加血管"))
                {
                    int index = bars.arraySize++;
                    var bar = bars.GetArrayElementAtIndex(index);
                    bar.FindPropertyRelative("Id").stringValue = System.Guid.NewGuid().ToString("N");
                    bar.FindPropertyRelative("Name").stringValue = "Bar";
                    bar.FindPropertyRelative("MaxHp").floatValue = 1000f;
                    bar.FindPropertyRelative("HasTimeLimit").boolValue = true;
                    bar.FindPropertyRelative("TimeLimit").floatValue = 60f;
                    bar.FindPropertyRelative("TriggerOnEmpty").boolValue = true;
                    bar.FindPropertyRelative("DiscardOverflow").boolValue = true;
                    bar.FindPropertyRelative("NextBarId").stringValue = "";
                    bar.FindPropertyRelative("Segments").arraySize = 0;
                    var states = bar.FindPropertyRelative("States");
                    states.arraySize = 1;
                    states.GetArrayElementAtIndex(0).managedReferenceValue = new ShooterPhase();
                    bar.isExpanded = true;
                }
                // Keep old serialized data inspectable instead of silently assigning states to the wrong bar.
                var legacy = serializedObject.FindProperty("Phases");
                if (legacy.arraySize > 0)
                {
                    EditorGUILayout.HelpBox("检测到旧的全局阶段数据。它不再驱动战斗，请将各阶段重新配置到所属血管；旧数据暂留供对照。", MessageType.Warning);
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(legacy, new GUIContent("旧阶段（只读）"), true);
                }
            }
            serializedObject.ApplyModifiedProperties();
            if (!boss.TryValidate(out var error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            else if (boss.GetOrderedBars().Length != boss.Bars.Length)
                EditorGUILayout.HelpBox("存在从开始血管不可达的血管；本场不会执行，也不计入 HUD 管数。", MessageType.Warning);
        }

        internal static void ConvertToSegment(SerializedProperty bar)
        {
            var segments = bar.FindPropertyRelative("Segments");
            if (segments.arraySize > 0) return;
            AddSegment(segments, false);
            var segment = segments.GetArrayElementAtIndex(0);
            foreach (string field in new[] { "Name", "MaxHp", "HasTimeLimit", "TimeLimit" })
            {
                var source = bar.FindPropertyRelative(field);
                var destination = segment.FindPropertyRelative(field);
                if (source.propertyType == SerializedPropertyType.String) destination.stringValue = source.stringValue;
                else if (source.propertyType == SerializedPropertyType.Boolean) destination.boolValue = source.boolValue;
                else destination.floatValue = source.floatValue;
            }
            var sourceStates = bar.FindPropertyRelative("States");
            var states = segment.FindPropertyRelative("States");
            states.arraySize = sourceStates.arraySize;
            for (int i = 0; i < states.arraySize; i++)
            {
                var source = sourceStates.GetArrayElementAtIndex(i).managedReferenceValue;
                states.GetArrayElementAtIndex(i).managedReferenceValue = source == null ? null :
                    JsonUtility.FromJson(JsonUtility.ToJson(source), source.GetType());
            }
        }

        internal static void AddSegment(SerializedProperty segments, bool spell)
        {
            int index = segments.arraySize++;
            var segment = segments.GetArrayElementAtIndex(index);
            segment.FindPropertyRelative("Id").stringValue = System.Guid.NewGuid().ToString("N");
            segment.FindPropertyRelative("Name").stringValue = spell ? "符卡" : "非符";
            segment.FindPropertyRelative("Kind").enumValueIndex = spell ? 1 : 0;
            segment.FindPropertyRelative("MaxHp").floatValue = 1000f;
            segment.FindPropertyRelative("DamageTakenMultiplier").floatValue = 1f;
            segment.FindPropertyRelative("Color").colorValue = spell ? new Color(1f, 0.25f, 0.3f) : Color.white;
            segment.FindPropertyRelative("HasTimeLimit").boolValue = spell;
            segment.FindPropertyRelative("TimeLimit").floatValue = 60f;
            var states = segment.FindPropertyRelative("States");
            states.arraySize = 1;
            states.GetArrayElementAtIndex(0).managedReferenceValue = new ShooterPhase();
            segment.isExpanded = true;
        }

        static void DrawSegments(SerializedProperty segments)
        {
            double total = 0;
            for (int i = 0; i < segments.arraySize; i++) total += segments.GetArrayElementAtIndex(i).FindPropertyRelative("MaxHp").floatValue;
            EditorGUILayout.LabelField("模式", $"分段血量 · 合计 {total:0.##}");
            EditorGUILayout.HelpBox("按列表顺序消耗；每段独立计时、减伤。原整管设置保留但不生效。删除最后一段将恢复原整管配置。", MessageType.Info);
            for (int i = 0; i < segments.arraySize; i++)
            {
                var segment = segments.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                double ratio = total > 0 ? segment.FindPropertyRelative("MaxHp").floatValue / total * 100 : 0;
                segment.isExpanded = EditorGUILayout.Foldout(segment.isExpanded,
                    $"分段 {i + 1} · {segment.FindPropertyRelative("Name").stringValue} · {ratio:0.#}%", true);
                bool up = GUILayout.Button("↑", GUILayout.Width(26)) && i > 0;
                bool down = GUILayout.Button("↓", GUILayout.Width(26)) && i + 1 < segments.arraySize;
                bool remove = GUILayout.Button("删除", GUILayout.Width(44));
                EditorGUILayout.EndHorizontal();
                if (up || down || remove)
                {
                    if (remove) segments.DeleteArrayElementAtIndex(i);
                    else segments.MoveArrayElement(i, up ? i - 1 : i + 1);
                    EditorGUILayout.EndVertical();
                    break;
                }
                if (segment.isExpanded)
                {
                    BossInspectorFields.Field(segment, "Id", "稳定 ID");
                    BossInspectorFields.Field(segment, "Name", "名称");
                    var kind = segment.FindPropertyRelative("Kind");
                    kind.enumValueIndex = EditorGUILayout.Popup("类型", kind.enumValueIndex, new[] { "非符", "符卡" });
                    BossInspectorFields.Field(segment, "MaxHp", "分段血量");
                    BossInspectorFields.Field(segment, "DamageTakenMultiplier", "承伤倍率");
                    BossInspectorFields.Field(segment, "Color", "血条颜色");
                    BossInspectorFields.Field(segment, "HasTimeLimit", "启用分段时限");
                    if (segment.FindPropertyRelative("HasTimeLimit").boolValue) BossInspectorFields.Field(segment, "TimeLimit", "时限（秒）");
                    DrawStates(segment.FindPropertyRelative("States"), true);
                }
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("添加非符分段")) AddSegment(segments, false);
            if (GUILayout.Button("添加符卡分段")) AddSegment(segments, true);
        }

        static void DrawStates(SerializedProperty states, bool segmented = false)
        {
            for (int i = 0; i < states.arraySize; i++)
            {
                var state = states.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                state.isExpanded = EditorGUILayout.Foldout(state.isExpanded, $"状态 {i + 1} · {state.FindPropertyRelative("DisplayName")?.stringValue}", true);
                bool up = GUILayout.Button("↑", GUILayout.Width(26)) && i > 0;
                bool down = GUILayout.Button("↓", GUILayout.Width(26)) && i + 1 < states.arraySize;
                bool remove = GUILayout.Button("删除", GUILayout.Width(44));
                EditorGUILayout.EndHorizontal();
                if (up || down || remove)
                {
                    if (remove) states.DeleteArrayElementAtIndex(i);
                    else states.MoveArrayElement(i, up ? i - 1 : i + 1);
                    EditorGUILayout.EndVertical();
                    break;
                }
                if (state.managedReferenceValue == null)
                {
                    if (GUILayout.Button("创建 Shooter 状态")) state.managedReferenceValue = new ShooterPhase();
                }
                else if (state.isExpanded)
                {
                    var child = state.Copy();
                    var end = state.GetEndProperty();
                    if (child.NextVisible(true))
                        do
                        {
                            if (SerializedProperty.EqualContents(child, end)) break;
                            if (!HiddenStateFields.Contains(child.name)) EditorGUILayout.PropertyField(child, true);
                        } while (child.NextVisible(false));
                    if (i + 1 == states.arraySize) EditorGUILayout.LabelField("结束方式", segmented ? "保持到本段打空或超时" : "保持到本管打空或超时");
                    else
                    {
                        var mode = state.FindPropertyRelative("AdvanceMode");
                        mode.enumValueIndex = EditorGUILayout.Popup("下一状态条件", mode.enumValueIndex, new[] { "本状态持续时间", segmented ? "本段剩余血量百分比" : "本管剩余血量百分比" });
                        BossInspectorFields.Field(state, mode.enumValueIndex == 0 ? "AdvanceAfterSeconds" : "AdvanceAtPercent",
                            mode.enumValueIndex == 0 ? "持续秒数" : "剩余不超过（%）");
                    }
                }
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button(segmented ? "添加本段状态" : "添加本管状态"))
            {
                int index = states.arraySize++;
                states.GetArrayElementAtIndex(index).managedReferenceValue = new ShooterPhase();
                states.GetArrayElementAtIndex(index).isExpanded = true;
            }
        }
    }
}
