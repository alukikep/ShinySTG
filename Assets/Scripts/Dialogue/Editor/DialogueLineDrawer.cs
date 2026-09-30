#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.Dialogue.Editor
{
    [CustomPropertyDrawer(typeof(DialogueLine))]
    public sealed class DialogueLineDrawer : PropertyDrawer
    {
        const float Gap = 2f;
        const float PreviewSize = 40f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var text = property.FindPropertyRelative("Text");
            float height = EditorGUIUtility.singleLineHeight * 4f + Gap * 3f;
            if (text != null) height += Mathf.Max(EditorGUIUtility.singleLineHeight, EditorGUI.GetPropertyHeight(text, true) - EditorGUIUtility.singleLineHeight);
            return height + PreviewSize + Gap + 44f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var character = property.FindPropertyRelative("Character");
            var side = property.FindPropertyRelative("Side");
            var text = property.FindPropertyRelative("Text");
            var expression = property.FindPropertyRelative("ExpressionId");
            float line = EditorGUIUtility.singleLineHeight;
            float y = position.y;

            EditorGUI.PropertyField(new Rect(position.x, y, position.width, line), character, new GUIContent("Character")); y += line + Gap;
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, line), side, new GUIContent("Side")); y += line + Gap;

            var characterAsset = character.objectReferenceValue as DialogueCharacter;
            if (character.hasMultipleDifferentValues)
            {
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, line), expression);
                y += line + Gap;
            }
            else DrawExpression(position, ref y, expression, characterAsset, line);
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, EditorGUI.GetPropertyHeight(text, true)), text, new GUIContent("Text")); y += EditorGUI.GetPropertyHeight(text, true) + Gap;
            if (!character.hasMultipleDifferentValues && !expression.hasMultipleDifferentValues)
                DrawPreview(position, y, characterAsset, expression.stringValue);
            EditorGUI.EndProperty();
        }

        static void DrawExpression(Rect position, ref float y, SerializedProperty expression, DialogueCharacter character, float line)
        {
            var rect = new Rect(position.x, y, position.width, line);
            if (character == null)
            {
                EditorGUI.PropertyField(rect, expression, new GUIContent("Expression ID"));
                y += line + Gap;
                return;
            }

            var options = new List<string> { "(Default)" };
            var ids = new List<string> { string.Empty };
            var labels = new List<string> { string.Empty };
            if (character.Expressions != null)
            {
                foreach (var item in character.Expressions)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.Id) || ids.Contains(item.Id)) continue;
                    ids.Add(item.Id);
                    labels.Add(string.IsNullOrWhiteSpace(item.DisplayName) ? item.Id : item.DisplayName);
                    options.Add(labels[labels.Count - 1] + " (" + item.Id + ")");
                }
            }

            int index = ids.IndexOf(expression.stringValue);
            bool valid = index >= 0;
            if (!valid)
            {
                index = ids.Count;
                ids.Add(expression.stringValue);
                options.Add("无效 ID: " + expression.stringValue);
            }
            bool mixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = expression.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.Popup(rect, "Expression", index, options.ToArray());
            if (EditorGUI.EndChangeCheck()) expression.stringValue = ids[selected];
            EditorGUI.showMixedValue = mixed;
            if (!valid && !string.IsNullOrEmpty(expression.stringValue))
            {
                var warning = new Rect(position.x, y + line + Gap, position.width, 40f);
                EditorGUI.HelpBox(warning, "差分 ID 不存在于当前角色。选择有效差分或 Default 清除。", MessageType.Warning);
                y += 44f;
            }
            y += line + Gap;
        }

        static void DrawPreview(Rect position, float y, DialogueCharacter character, string expressionId)
        {
            if (character == null) return;
            var sprite = character.DefaultPortrait;
            if (!string.IsNullOrEmpty(expressionId) && character.Expressions != null)
                foreach (var item in character.Expressions)
                    if (item != null && string.Equals(item.Id, expressionId, StringComparison.Ordinal))
                    {
                        if (item.Portrait != null) sprite = item.Portrait;
                        break;
                    }
            if (sprite == null) return;
            var preview = new Rect(position.x, y, PreviewSize, PreviewSize);
            var texture = AssetPreview.GetAssetPreview(sprite) ?? AssetPreview.GetMiniThumbnail(sprite);
            if (texture != null) GUI.DrawTexture(preview, texture, ScaleMode.ScaleToFit);
            EditorGUI.LabelField(new Rect(position.x + PreviewSize + Gap, y, position.width - PreviewSize - Gap, PreviewSize), "当前立绘预览");
        }
    }
}
#endif

