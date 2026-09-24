using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(BulletClearPresentation))]
public sealed class BulletClearPresentationDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lines = 1;
        var mode = property.FindPropertyRelative("Mode");
        if (mode == null) return EditorGUIUtility.singleLineHeight;
        if ((BulletClearPresentationMode)mode.enumValueIndex == BulletClearPresentationMode.BurstEffect) lines += 3;
        else if ((BulletClearPresentationMode)mode.enumValueIndex == BulletClearPresentationMode.ConvertToItems) lines += 5;
        return lines * EditorGUIUtility.singleLineHeight + (lines - 1) * EditorGUIUtility.standardVerticalSpacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var mode = property.FindPropertyRelative("Mode");
        float y = position.y;
        float h = EditorGUIUtility.singleLineHeight;
        Rect Line() { var r = new Rect(position.x, y, position.width, h); y += h + EditorGUIUtility.standardVerticalSpacing; return r; }
        EditorGUI.PropertyField(Line(), mode, label);
        if (mode == null) { EditorGUI.EndProperty(); return; }
        switch ((BulletClearPresentationMode)mode.enumValueIndex)
        {
            case BulletClearPresentationMode.BurstEffect:
                Draw(property, "EffectPrefab", Line());
                Draw(property, "MaxEffectCount", Line());
                Draw(property, "EffectGridSize", Line());
                break;
            case BulletClearPresentationMode.ConvertToItems:
                Draw(property, "Item", Line());
                Draw(property, "ItemsPerBullet", Line());
                Draw(property, "MaxItemCount", Line());
                Draw(property, "ItemScatterRadius", Line());
                Draw(property, "ItemSpeed", Line());
                break;
        }
        EditorGUI.EndProperty();
    }

    static void Draw(SerializedProperty root, string name, Rect rect)
    {
        var child = root.FindPropertyRelative(name);
        if (child != null) EditorGUI.PropertyField(rect, child);
    }
}
