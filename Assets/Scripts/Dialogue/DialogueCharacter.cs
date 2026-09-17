using UnityEngine;

namespace ShinySTG.Dialogue
{
    [CreateAssetMenu(menuName = "STG/Dialogue/Character", fileName = "DialogueCharacter")]
    public sealed class DialogueCharacter : ScriptableObject
    {
        [Tooltip("对话框中显示的角色名。")]
        public string DisplayName;
        [Tooltip("未指定表情时使用的立绘，可留空。")]
        public Sprite DefaultPortrait;
        [Tooltip("角色名的显示颜色。")]
        public Color NameColor = Color.white;
    }
}
