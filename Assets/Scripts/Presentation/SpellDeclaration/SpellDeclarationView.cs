using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShinySTG.Presentation.SpellDeclaration
{
    /// <summary>复用场景 UI；CanvasGroup 隐藏视觉，不禁用承载服务的节点。</summary>
    public sealed class SpellDeclarationView : MonoBehaviour
    {
        [SerializeField, Tooltip("整段宣言的透明度根节点。")]
        CanvasGroup _root;
        [SerializeField, Tooltip("符卡名称。")]
        TMP_Text _title;
        [SerializeField, Tooltip("可选的标题图片；配置后优先于文字标题。")]
        Image _titleImage;
        [SerializeField, Tooltip("包含名称与底图的横幅节点。")]
        RectTransform _banner;
        [SerializeField, Tooltip("立绘图片；播放时可不提供 Sprite。")]
        Image _portrait;
        Vector2 _bannerPosition, _portraitPosition;
        bool _captured;

        public bool IsReady => UnavailableReason == null;

        public string UnavailableReason
        {
            get
            {
                if (!isActiveAndEnabled) return "SpellDeclarationView 组件或其所在对象/父对象未启用。";
                if (_root == null) return "View 的 Root 未绑定 CanvasGroup。";
                if (!_root.gameObject.activeInHierarchy) return "Root 所在对象或父对象未启用。";
                if (_title == null && _titleImage == null) return "View 未绑定 TMP 标题或标题图片。";
                if (_title != null && !_title.isActiveAndEnabled) return "Title 文本组件或其所在对象/父对象未启用。";
                if (_titleImage != null && !_titleImage.isActiveAndEnabled) return "标题图片组件或其所在对象/父对象未启用。";
                if (_banner == null) return "View 的 Banner 未绑定 RectTransform。";
                if (!_banner.gameObject.activeInHierarchy) return "Banner 所在对象或父对象未启用。";
                if (_portrait == null) return "View 的 Portrait 未绑定 Image（Sprite 可以留空）。";
                if (!_portrait.gameObject.activeInHierarchy) return "Portrait 所在对象或父对象未启用；留空立绘无需禁用对象。";
                return null;
            }
        }

        void Awake() => Clear();
        void OnDisable() => Clear();

        void CaptureLayout()
        {
            if (_captured || _banner == null || _portrait == null) return;
            _bannerPosition = _banner.anchoredPosition;
            _portraitPosition = _portrait.rectTransform.anchoredPosition;
            _captured = true;
        }

        public void Show(string title, Sprite portrait, Sprite titleImage = null)
        {
            Clear();
            if (_title != null) { _title.text = title ?? string.Empty; _title.maxVisibleCharacters = int.MaxValue; }
            if (_titleImage != null) { _titleImage.sprite = titleImage; _titleImage.enabled = titleImage != null; }
            _portrait.sprite = portrait;
            _portrait.enabled = portrait != null;
            _portrait.preserveAspect = true;
            _portrait.color = Color.white;
        }

        public void Render(float alpha, float offset)
        {
            CaptureLayout();
            _root.alpha = Mathf.Clamp01(alpha);
            _root.interactable = false;
            _root.blocksRaycasts = false;
            _banner.anchoredPosition = _bannerPosition + Vector2.right * offset;
            _portrait.rectTransform.anchoredPosition = _portraitPosition - Vector2.right * offset;
        }

        public void Clear()
        {
            CaptureLayout();
            if (_root != null) { _root.alpha = 0f; _root.interactable = false; _root.blocksRaycasts = false; }
            if (_title != null) { _title.text = string.Empty; _title.maxVisibleCharacters = int.MaxValue; }
            if (_titleImage != null) { _titleImage.sprite = null; _titleImage.enabled = false; }
            if (_banner != null && _captured) _banner.anchoredPosition = _bannerPosition;
            if (_portrait != null)
            {
                _portrait.sprite = null;
                _portrait.enabled = false;
                _portrait.color = Color.white;
                if (_captured) _portrait.rectTransform.anchoredPosition = _portraitPosition;
            }
        }
    }
}
