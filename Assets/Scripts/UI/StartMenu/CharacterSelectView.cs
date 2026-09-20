using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ShinySTG.UI
{
    /// <summary>展示角色资产与本页选择状态；开局交由流程控制器处理。</summary>
    public sealed class CharacterSelectView : MonoBehaviour
    {
        [Serializable]
        public sealed class CharacterEntry
        {
            [Tooltip("实际可玩的角色；配置后优先使用资产中的展示信息。")]
            public ShinySTG.GameFlow.CharacterDefinition Definition;
            [Tooltip("角色显示名称。")]
            public string DisplayName;
            [TextArea, Tooltip("选择页展示的角色简介。")]
            public string Description;
            [Tooltip("可选立绘；未配置时显示占位文字。")]
            public Sprite Portrait;

            public string ResolvedName => Definition != null ? Definition.DisplayName : DisplayName;
            public string ResolvedDescription => Definition != null ? Definition.Description : Description;
            public Sprite ResolvedPortrait => Definition != null ? Definition.Portrait : Portrait;
        }

        [SerializeField, Tooltip("可选角色，顺序与上下切换顺序一致。")]
        CharacterEntry[] _characters =
        {
            new CharacterEntry { DisplayName = "角色 A（占位）", Description = "角色资料待接入" },
            new CharacterEntry { DisplayName = "角色 B（占位）", Description = "角色资料待接入" }
        };
        [SerializeField, Tooltip("右侧角色列表文本。")]
        Text _list;
        [SerializeField, Tooltip("下方简介文本。")]
        Text _description;
        [SerializeField, Tooltip("没有立绘时的提示文本。")]
        Text _portraitLabel;
        [SerializeField, Tooltip("立绘显示区域，不与背景共用 Image。")]
        Image _portrait;

        public int SelectedIndex { get; private set; }
        public CharacterEntry SelectedCharacter => Count > 0 ? _characters[SelectedIndex] : null;
        public ShinySTG.GameFlow.CharacterDefinition SelectedDefinition => SelectedCharacter?.Definition;

        public void ShowError(string error)
        {
            if (_description != null) _description.text = error;
        }
        int Count => _characters == null ? 0 : _characters.Length;

        void OnEnable()
        {
            SelectedIndex = 0;
            Refresh();
        }

        public void MoveSelection(int direction)
        {
            if (!isActiveAndEnabled || Count == 0 || direction == 0) return;
            SelectedIndex = (SelectedIndex + (direction > 0 ? 1 : Count - 1)) % Count;
            Refresh();
        }

        void Refresh()
        {
            var entry = SelectedCharacter;
            if (_list != null)
            {
                // 只显示当前项及邻项，增加角色后仍保持固定区域内可读。
                var builder = new StringBuilder();
                int first = Mathf.Clamp(SelectedIndex - 1, 0, Mathf.Max(0, Count - 3));
                for (int i = first; i < Mathf.Min(Count, first + 3); i++)
                {
                    if (i > first) builder.Append("\n\n");
                    builder.Append(i == SelectedIndex ? ">  " : "    ");
                    builder.Append(_characters[i]?.ResolvedName ?? "未配置角色");
                }
                _list.supportRichText = false;
                _list.text = Count == 0 ? "暂无可选角色" : builder.ToString();
            }
            if (_description != null)
                _description.text = Count == 0 ? string.Empty
                    : $"{SelectedIndex + 1} / {Count}   {entry?.ResolvedName ?? "未配置角色"}\n{entry?.ResolvedDescription}";
            bool hasPortrait = entry != null && entry.ResolvedPortrait != null;
            if (_portrait != null)
            {
                _portrait.sprite = hasPortrait ? entry.ResolvedPortrait : null;
                _portrait.preserveAspect = true;
                _portrait.enabled = hasPortrait;
                _portrait.raycastTarget = false;
            }
            if (_portraitLabel != null)
            {
                _portraitLabel.text = Count == 0 ? "暂无角色" : $"{entry?.ResolvedName ?? "未配置角色"}\n立绘待接入";
                _portraitLabel.gameObject.SetActive(!hasPortrait);
            }
        }
    }
}
