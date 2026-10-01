using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(BulletModifierEntry))]
public sealed class BulletModifierEntryDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        var modifier = property.FindPropertyRelative(nameof(BulletModifierEntry.Modifier));
        if (property.isExpanded && modifier != null)
            height += BulletModifierGUI.GetBodyHeight(modifier, true);
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        int previousIndent = EditorGUI.indentLevel;
        try
        {
            var modifier = property.FindPropertyRelative(nameof(BulletModifierEntry.Modifier));
            var duration = property.FindPropertyRelative(nameof(BulletModifierEntry.Duration));
            Rect row = EditorGUI.IndentedRect(new Rect(position.x, position.y, position.width,
                EditorGUIUtility.singleLineHeight));
            EditorGUI.indentLevel = 0;

            // Keep the phase time editable even while the behavior is collapsed.
            float timeWidth = Mathf.Min(138f, row.width * 0.42f);
            Rect timeRect = new Rect(row.xMax - timeWidth, row.y, timeWidth, row.height);
            Rect typeRect = new Rect(timeRect.x - 28f, row.y, 24f, row.height);
            Rect titleRect = new Rect(row.x, row.y, Mathf.Max(20f, typeRect.x - row.x - 4f), row.height);
            string title = "阶段 " + BulletModifierGUI.GetElementNumber(property) + " · "
                + BulletModifierGUI.GetTypeName(modifier);
            property.isExpanded = EditorGUI.Foldout(titleRect, property.isExpanded,
                new GUIContent(BulletModifierGUI.FitTitle(title, titleRect.width), title), true);
            BulletModifierGUI.DrawTypeButton(typeRect, modifier, typeof(BulletModifier));

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = timeWidth - 53f;
            try
            {
                EditorGUI.PropertyField(timeRect, duration,
                    new GUIContent("阶段时长 (秒)", "由容器控制该阶段；子行为的启动条件、生效时长和一次触发设置不参与调度。"));
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }

            if (property.isExpanded)
            {
                Rect body = new Rect(row.x + 12f, row.yMax, Mathf.Max(0f, row.width - 12f),
                    position.height - row.height);
                BulletModifierGUI.DrawBody(body, modifier, true);
            }
        }
        finally
        {
            EditorGUI.indentLevel = previousIndent;
            EditorGUI.EndProperty();
        }
    }
}
