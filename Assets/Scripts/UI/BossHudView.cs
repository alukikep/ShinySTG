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
        [SerializeField, Tooltip("后续管数文本，不包含当前管，最后一管显示 0；中文需使用支持中文的 TMP 字体。")]
        TMP_Text _barCount;
        [SerializeField, Min(0f), Tooltip("每秒变化的血条比例；0 表示立即更新。")]
        float _fillSpeed = 2f;

        CanvasGroup _group;
        float _targetFill;

        void Awake() => Clear();
        void OnDisable() => Clear();

        public void SetHealth(float normalized, int remainingBars, bool immediate)
        {
            EnsureGroup();
            _targetFill = Mathf.Clamp01(normalized);
            if (_fill != null && (immediate || _fillSpeed <= 0f)) _fill.fillAmount = _targetFill;
            // 数据源包含当前管；显示只计当前管之后的血条。
            if (_barCount != null) _barCount.SetText("{0}", remainingBars > 0 ? remainingBars - 1 : 0);
            _group.alpha = 1f;
        }

        public void Clear()
        {
            EnsureGroup();
            _targetFill = 0f;
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
            if (_fill != null)
                _fill.fillAmount = _fillSpeed <= 0f ? _targetFill :
                    Mathf.MoveTowards(_fill.fillAmount, _targetFill, _fillSpeed * Time.deltaTime);
        }
    }
}
