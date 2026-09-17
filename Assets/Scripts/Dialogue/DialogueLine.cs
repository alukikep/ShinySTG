using System;
using UnityEngine;

namespace ShinySTG.Dialogue
{
    public enum DialogueSide { Left, Right }

    [Serializable]
    public sealed class DialogueLine
    {
        [Tooltip("说话角色；留空时作为旁白并隐藏当前侧立绘。")]
        public DialogueCharacter Character;
        [Tooltip("说话角色所在的一侧；另一侧保留上一句立绘。")]
        public DialogueSide Side;
        [TextArea(2, 8), Tooltip("本句台词，支持 TMP 富文本。")]
        public string Text;
        [Tooltip("本句表情立绘；留空使用角色默认立绘。")]
        public Sprite PortraitOverride;
    }
}
