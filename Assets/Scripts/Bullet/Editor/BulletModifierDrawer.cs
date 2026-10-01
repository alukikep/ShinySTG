using UnityEditor;
using UnityEngine;

// Explicit attribute avoids competing with the third-party SR attribute drawer.
[CustomPropertyDrawer(typeof(BulletModifierUIAttribute))]
public sealed class BulletModifierDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => BulletModifierGUI.GetHeight(property);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        try
        {
            BulletModifierGUI.Draw(position, property, label);
        }
        finally
        {
            EditorGUI.EndProperty();
        }
    }
}
