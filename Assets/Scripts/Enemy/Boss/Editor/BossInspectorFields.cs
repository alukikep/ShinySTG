using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI.Boss.Editor
{
    internal static class BossInspectorFields
    {
        public static bool RepairIds(SerializedObject serialized)
        {
            var bars = serialized.FindProperty("Bars");
            var used = new HashSet<string>(StringComparer.Ordinal);
            bool changed = false;
            for (int i = 0; i < bars.arraySize; i++)
            {
                var id = bars.GetArrayElementAtIndex(i).FindPropertyRelative("Id");
                if (id == null) continue;
                if (!string.IsNullOrWhiteSpace(id.stringValue) && used.Add(id.stringValue)) continue;
                id.stringValue = Guid.NewGuid().ToString("N");
                used.Add(id.stringValue);
                changed = true;
            }
            return changed;
        }

        public static string BarError(BossHealth.HealthBar[] bars, string id)
        {
            if (bars == null || string.IsNullOrWhiteSpace(id)) return "请选择血管，并先在 Encounter 整理血管 ID。";
            int count = 0;
            BossHealth.HealthBar found = null;
            foreach (var bar in bars)
                if (bar != null && bar.Id == id) { count++; found = bar; }
            if (count == 0) return "引用的血管已删除或 ID 已改变。";
            if (count > 1) return "血管 ID 重复，请在 Encounter 整理 ID 后确认引用。";
            if (!(found.EffectiveMaxHp > 0f) || float.IsInfinity(found.EffectiveMaxHp)) return "血管上限必须是有限正数。";
            return null;
        }

        public static string BarName(BossHealth.HealthBar[] bars, string id)
        {
            if (bars != null)
                for (int i = 0; i < bars.Length; i++)
                    if (bars[i] != null && bars[i].Id == id)
                        return $"第 {i + 1} 管 · {bars[i].Name}";
            return "未指定/失效血管";
        }

        public static void BarPopup(SerializedProperty property, BossHealth.HealthBar[] bars, string label, string emptyLabel = null)
        {
            var ids = new List<string> { "" };
            var labels = new List<string> { emptyLabel ?? "请选择血管" };
            if (bars != null)
                for (int i = 0; i < bars.Length; i++)
                {
                    var bar = bars[i];
                    if (bar == null || string.IsNullOrWhiteSpace(bar.Id)) continue;
                    ids.Add(bar.Id);
                    labels.Add($"第 {i + 1} 管 · {bar.Name}");
                }
            int selected = ids.IndexOf(property.stringValue);
            if (selected < 0)
            {
                selected = ids.Count;
                ids.Add(property.stringValue);
                labels.Add("失效引用（请重新选择）");
            }
            EditorGUI.BeginChangeCheck();
            int next = EditorGUILayout.Popup(label, selected, labels.ToArray());
            if (EditorGUI.EndChangeCheck()) property.stringValue = ids[next];
            string error = emptyLabel != null && string.IsNullOrEmpty(property.stringValue) ? null : BarError(bars, property.stringValue);
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        public static void Field(SerializedProperty parent, string name, string label = null)
        {
            var p = parent.FindPropertyRelative(name);
            if (p != null) EditorGUILayout.PropertyField(p, label == null ? new GUIContent(p.displayName) : new GUIContent(label), true);
        }
    }
}
