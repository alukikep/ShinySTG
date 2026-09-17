using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShinySTG.Dialogue
{
    /// <summary>根节点使用 CanvasGroup 隐藏，不禁用承载服务和输入的 GameObject。</summary>
    public sealed class DialogueView : MonoBehaviour
    {
        [SerializeField, Tooltip("对话 UI 的 CanvasGroup。")]
        CanvasGroup _root;
        [SerializeField, Tooltip("姓名文本，可留空。")]
        TMP_Text _speaker;
        [SerializeField, Tooltip("正文文本，必填。")]
        TMP_Text _body;
        [SerializeField, Tooltip("左侧立绘，可留空。")]
        Image _leftPortrait;
        [SerializeField, Tooltip("右侧立绘，可留空。")]
        Image _rightPortrait;
        [SerializeField, Tooltip("本句显示完毕后的继续提示，可留空。")]
        GameObject _continueIndicator;
        [SerializeField, Range(0f, 1f), Tooltip("非说话角色立绘的亮度。")]
        float _inactiveBrightness = 0.45f;

        public bool IsReady => isActiveAndEnabled && _root != null && _body != null
            && _root.gameObject.activeInHierarchy && _body.isActiveAndEnabled;
        public int CharacterCount => _body != null ? _body.textInfo.characterCount : 0;

        void Awake() => Hide();
        void OnDisable() => Hide();

        public void Show()
        {
            Hide();
            _root.alpha = 1f;
            _root.interactable = false;
            _root.blocksRaycasts = true;
        }

        public void ShowLine(DialogueLine line)
        {
            if (_speaker != null)
            {
                _speaker.text = line.Character != null ? line.Character.DisplayName : string.Empty;
                _speaker.color = line.Character != null ? line.Character.NameColor : Color.white;
            }
            var portrait = line.PortraitOverride != null ? line.PortraitOverride
                : line.Character != null ? line.Character.DefaultPortrait : null;
            SetPortrait(line.Side == DialogueSide.Left ? _leftPortrait : _rightPortrait, portrait);
            SetTint(_leftPortrait, line.Side == DialogueSide.Left);
            SetTint(_rightPortrait, line.Side == DialogueSide.Right);
            _body.text = line.Text ?? string.Empty;
            _body.maxVisibleCharacters = int.MaxValue;
            _body.ForceMeshUpdate();
            SetVisibleCharacters(0);
        }

        public void SetVisibleCharacters(int count)
        {
            _body.maxVisibleCharacters = count;
            if (_continueIndicator != null) _continueIndicator.SetActive(count >= CharacterCount);
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.alpha = 0f;
                _root.interactable = false;
                _root.blocksRaycasts = false;
            }
            if (_body != null) { _body.text = string.Empty; _body.maxVisibleCharacters = 0; }
            if (_speaker != null) { _speaker.text = string.Empty; _speaker.color = Color.white; }
            SetPortrait(_leftPortrait, null);
            SetPortrait(_rightPortrait, null);
            if (_continueIndicator != null) _continueIndicator.SetActive(false);
        }

        static void SetPortrait(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
            image.color = Color.white;
        }

        void SetTint(Image image, bool active)
        {
            if (image == null) return;
            float brightness = active ? 1f : _inactiveBrightness;
            image.color = new Color(brightness, brightness, brightness, 1f);
        }
    }
}
