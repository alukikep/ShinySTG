using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShinySTG.UI
{
    /// <summary>只负责 Boss 血条表现，不结算血量或阶段。</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class BossHudView : MonoBehaviour
    {
        [SerializeField, Tooltip("当前管血量，Image 应设为 Filled / Horizontal。")]
        Image _fill;
        [SerializeField, Tooltip("分段图像容器；留空时在原填充下创建，兼容已有 HUD。")]
        RectTransform _segmentRoot;
        readonly System.Collections.Generic.List<Image> _segments = new();
        int _segmentCount;
        float _displayedFill;

        /// <summary>只在绑定或换管时调用；宽度以整管为单位，顺序为消耗顺序。</summary>
        public void ConfigureSegments(float[] widths, Color[] colors)
        {
            _segmentCount = widths?.Length ?? 0;
            if (_segmentCount > 0 && _segmentRoot == null && _fill != null)
            {
                _segmentRoot = new GameObject("Segments", typeof(RectTransform)).GetComponent<RectTransform>();
                _segmentRoot.SetParent(_fill.transform, false);
                _segmentRoot.anchorMin = Vector2.zero;
                _segmentRoot.anchorMax = Vector2.one;
                _segmentRoot.offsetMin = _segmentRoot.offsetMax = Vector2.zero;
            }
            if (_segmentRoot == null) _segmentCount = 0;
            if (_fill != null) _fill.enabled = _segmentCount == 0;
            if (_segmentRoot != null) _segmentRoot.gameObject.SetActive(_segmentCount > 0);
            float right = 1f;
            for (int i = 0; i < _segmentCount; i++)
            {
                if (i == _segments.Count)
                {
                    var image = new GameObject($"Segment {i + 1}", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    image.transform.SetParent(_segmentRoot, false);
                    _segments.Add(image);
                }
                var fill = _segments[i];
                float left = i + 1 == _segmentCount ? 0f : Mathf.Max(0f, right - widths[i]);
                var rect = fill.rectTransform;
                rect.anchorMin = new Vector2(left, 0f);
                rect.anchorMax = new Vector2(right, 1f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                fill.color = colors[i];
                fill.raycastTarget = false;
                // A sprite-less Image is a solid quad: resizing below avoids rounded seams between segments.
                fill.fillAmount = 1f;
                fill.enabled = true;
                fill.gameObject.SetActive(true);
                right = left;
            }
            for (int i = _segmentCount; i < _segments.Count; i++) _segments[i].gameObject.SetActive(false);
        }
        [SerializeField, Tooltip("后续管数文本，不包含当前管，最后一管显示 0；中文需使用支持中文的 TMP 字体。")]
        TMP_Text _barCount;
        [SerializeField, Min(0f), Tooltip("每秒变化的血条比例；0 表示立即更新。")]
        float _fillSpeed = 2f;

        [SerializeField, Tooltip("血条右侧倒计时，显示 00～99 秒。")]
        TMP_Text _timer;
        int _displayedSeconds = -1;
        bool _timerVisible;

        CanvasGroup _group;
        float _targetFill;

        void Awake() => Clear();
        void OnDisable() => Clear();

        public void SetHealth(float normalized, int remainingBars, bool immediate)
        {
            EnsureGroup();
            _targetFill = Mathf.Clamp01(normalized);
            if (immediate || _fillSpeed <= 0f) _displayedFill = _targetFill;
            DrawFill();
            // 数据源包含当前管；显示只计当前管之后的血条。
            if (_barCount != null) _barCount.SetText("{0}", remainingBars > 0 ? remainingBars - 1 : 0);
            _group.alpha = 1f;
        }

        public static int GetDisplayedSeconds(float seconds) =>
            float.IsNaN(seconds) ? 0 : Mathf.CeilToInt(Mathf.Clamp(seconds, 0f, 99f));

        public void SetTimer(float seconds, bool visible)
        {
            if (_timer == null) return;
            if (_timerVisible != visible || _timer.enabled != visible) _timer.enabled = visible;
            _timerVisible = visible;
            if (!visible) { _displayedSeconds = -1; return; }
            int value = GetDisplayedSeconds(seconds);
            if (_displayedSeconds == value) return;
            _displayedSeconds = value;
            _timer.SetText(value.ToString("00"));
        }

        public void Clear()
        {
            EnsureGroup();
            SetTimer(0f, false);
            _targetFill = 0f;
            _displayedFill = 0f;
            ConfigureSegments(null, null);
            if (_fill != null) _fill.fillAmount = 0f;
            if (_barCount != null) _barCount.SetText("0");
            _group.alpha = 0f;
        }

        void EnsureGroup()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        void Update()
        {
            _displayedFill = _fillSpeed <= 0f ? _targetFill :
                Mathf.MoveTowards(_displayedFill, _targetFill, _fillSpeed * Time.deltaTime);
            DrawFill();
        }

        void DrawFill()
        {
            if (_segmentCount == 0)
            {
                if (_fill != null) _fill.fillAmount = _displayedFill;
                return;
            }
            // Full layout boundaries are determined by the next segment's left edge, or 1 for the first.
            for (int i = 0; i < _segmentCount; i++)
            {
                var rect = _segments[i].rectTransform;
                float left = rect.anchorMin.x;
                float right = i == 0 ? 1f : _segments[i - 1].rectTransform.anchorMin.x;
                rect.anchorMax = new Vector2(Mathf.Clamp(_displayedFill, left, right), 1f);
                _segments[i].enabled = _displayedFill > left;
            }
        }
    }
}
