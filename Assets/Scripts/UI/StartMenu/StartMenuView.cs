using UnityEngine;
using UnityEngine.UI;

namespace ShinySTG.UI
{
    /// <summary>只负责菜单表现。排版由场景中的 RectTransform 和 CanvasScaler 决定。</summary>
    [DisallowMultipleComponent]
    public sealed class StartMenuView : MonoBehaviour
    {
        [SerializeField, Tooltip("按开始、设置、退出顺序绑定三个选项文本。")]
        Text[] _options;
        [SerializeField, Tooltip("与选项文本共用父节点的选择指示符。")]
        RectTransform _indicator;
        [SerializeField, Tooltip("底部占位确认提示。")]
        Text _feedback;
        [SerializeField, Tooltip("未选中时的文字颜色。")]
        Color _normalColor = new Color(0.68f, 0.7f, 0.79f);
        [SerializeField, Tooltip("选中时的文字颜色。")]
        Color _selectedColor = new Color(1f, 0.83f, 0.46f);
        [SerializeField, Tooltip("选中文字向右偏移的 UI 单位数。")]
        float _selectedOffset = 14f;

        readonly Vector2[] _basePositions = new Vector2[3];
        bool _captured;
        int _selected;

        public bool IsReady => _options != null && _options.Length == 3
            && _options[0] != null && _options[1] != null && _options[2] != null;

        public void ResetView(StartMenuOption selected)
        {
            if (_feedback != null) _feedback.text = string.Empty;
            ShowSelection(selected);
        }

        public void ShowSelection(StartMenuOption selected)
        {
            if (!IsReady) return;
            if (!_captured)
            {
                for (int i = 0; i < 3; i++) _basePositions[i] = _options[i].rectTransform.anchoredPosition;
                _captured = true;
            }
            _selected = Mathf.Clamp((int)selected, 0, 2);
            for (int i = 0; i < 3; i++)
            {
                _options[i].color = i == _selected ? _selectedColor : _normalColor;
                _options[i].rectTransform.anchoredPosition = _basePositions[i]
                    + (i == _selected ? Vector2.right * _selectedOffset : Vector2.zero);
            }
            if (_indicator != null)
            {
                var position = _indicator.anchoredPosition;
                position.y = _basePositions[_selected].y;
                _indicator.anchoredPosition = position;
            }
        }

        public void ShowConfirmation(StartMenuOption selected)
        {
            if (!IsReady) return;
            if (_feedback != null) _feedback.text = selected == StartMenuOption.StartGame
                ? string.Empty : $"已选择：{_options[(int)selected].text}（功能待接入）";
        }

        public void ShowConfirmFlash(bool flashing, bool bright)
        {
            if (!IsReady) return;
            _options[_selected].color = flashing && bright ? Color.white : _selectedColor;
        }
    }
}
