using UnityEngine;

namespace ShinySTG.Dialogue
{
    [CreateAssetMenu(menuName = "STG/Dialogue/Conversation", fileName = "Dialogue")]
    public sealed class DialogueDefinition : ScriptableObject
    {
        [Tooltip("按顺序播放的台词；空条目会跳过。")]
        public DialogueLine[] Lines;
    }
}
