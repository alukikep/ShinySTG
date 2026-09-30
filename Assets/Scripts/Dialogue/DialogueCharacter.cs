using System;
using UnityEngine;

namespace ShinySTG.Dialogue
{
    [Serializable]
    public sealed class DialogueExpression
    {
        [Tooltip("稳定的差分 ID，供台词引用。")]
        public string Id;
        [Tooltip("编辑器中显示的差分名称。")]
        public string DisplayName;
        [Tooltip("差分立绘。")]
        public Sprite Portrait;
    }

    [CreateAssetMenu(menuName = "STG/Dialogue/Character", fileName = "DialogueCharacter")]
    public sealed class DialogueCharacter : ScriptableObject
    {
        [Tooltip("对话框中显示的角色名。")]
        public string DisplayName;
        [Tooltip("未指定差分时使用的立绘，可留空。")]
        public Sprite DefaultPortrait;
        [Tooltip("角色名的显示颜色。")]
        public Color NameColor = Color.white;
        [Tooltip("角色可用的立绘差分；台词通过 Id 引用。")]
        public DialogueExpression[] Expressions;

        public Sprite ResolvePortrait(string expressionId)
        {
            if (string.IsNullOrEmpty(expressionId)) return DefaultPortrait;
            if (Expressions != null)
            {
                foreach (var expression in Expressions)
                {
                    if (expression == null || !string.Equals(expression.Id, expressionId, StringComparison.Ordinal)) continue;
                    if (expression.Portrait != null) return expression.Portrait;
                    Warn($"差分 '{expressionId}' 未配置立绘，已回退到默认立绘。", expressionId);
                    return DefaultPortrait;
                }
            }
            Warn($"找不到差分 '{expressionId}'，已回退到默认立绘。", expressionId);
            return DefaultPortrait;
        }

        void OnValidate()
        {
            if (Expressions == null) return;
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var expression in Expressions)
            {
                if (expression == null) continue;
                if (string.IsNullOrWhiteSpace(expression.Id))
                    Debug.LogWarning($"[Dialogue] 角色 '{name}' 存在空差分 ID。", this);
                else if (!ids.Add(expression.Id))
                    Debug.LogWarning($"[Dialogue] 角色 '{name}' 存在重复差分 ID '{expression.Id}'。", this);
                if (expression.Portrait == null)
                    Debug.LogWarning($"[Dialogue] 角色 '{name}' 的差分 '{expression.Id}' 未配置立绘。", this);
            }
        }

        void Warn(string message, string expressionId)
        {
            Debug.LogWarning($"[Dialogue] 角色 '{name}'：{message}", this);
        }
    }
}
