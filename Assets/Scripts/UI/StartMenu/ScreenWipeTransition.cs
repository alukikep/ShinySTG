using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ShinySTG.UI
{
    /// <summary>以归一化锚点移动全屏黑幕，窗口缩放期间仍完整覆盖。</summary>
    public sealed class ScreenWipeTransition : MonoBehaviour
    {
        [SerializeField, Tooltip("最上层的纯黑 Image，父节点必须填满 Canvas。")]
        Image _curtain;
        [SerializeField, Min(0.01f), Tooltip("覆盖画面的秒数。")]
        float _coverDuration = 0.3f;
        [SerializeField, Min(0f), Tooltip("全黑停留秒数。")]
        float _blackHold = 0.05f;
        [SerializeField, Min(0.01f), Tooltip("揭开画面的秒数。")]
        float _revealDuration = 0.3f;

        public bool IsReady => _curtain != null && isActiveAndEnabled;
        public bool IsPlaying { get; private set; }

        public void Configure(Image curtain) => _curtain = curtain;

        public IEnumerator Cover()
        {
            if (!IsReady || IsPlaying) yield break;
            IsPlaying = true;
            _curtain.color = Color.black;
            _curtain.gameObject.SetActive(true);
            yield return Slide(1f, 0f, _coverDuration);
            yield return null;
        }

        public IEnumerator Reveal()
        {
            if (!IsReady) yield break;
            yield return Slide(0f, -1f, _revealDuration);
            ResetTransition();
        }

        public IEnumerator Fade(bool covered)
        {
            if (!IsReady) throw new InvalidOperationException("转场画布不可用。");
            IsPlaying = true;
            SetPosition(0f);
            _curtain.gameObject.SetActive(true);
            float duration = covered ? _coverDuration : _revealDuration;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float alpha = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _curtain.color = new Color(0f, 0f, 0f, covered ? alpha : 1f - alpha);
                yield return null;
            }
            _curtain.color = covered ? Color.black : Color.clear;
            if (!covered) ResetTransition();
            yield return null;
        }

        public IEnumerator Play(bool rightToLeft, Action covered)
        {
            if (!IsReady || IsPlaying) yield break;
            IsPlaying = true;
            float side = rightToLeft ? 1f : -1f;
            try
            {
                _curtain.color = Color.black;
                _curtain.raycastTarget = false;
                _curtain.gameObject.SetActive(true);
                _curtain.transform.SetAsLastSibling();
                yield return Slide(side, 0f, _coverDuration);
                // 至少绘制一帧全黑，再切换页面。
                yield return null;
                covered?.Invoke();
                float remaining = Mathf.Max(0f, _blackHold);
                while (remaining > 0f)
                {
                    remaining -= Time.unscaledDeltaTime;
                    yield return null;
                }
                yield return Slide(0f, -side, _revealDuration);
            }
            finally { ResetTransition(); }
        }

        IEnumerator Slide(float from, float to, float duration)
        {
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                SetPosition(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            SetPosition(to);
        }

        void SetPosition(float x)
        {
            if (_curtain == null) return;
            var rect = _curtain.rectTransform;
            rect.anchorMin = new Vector2(x, 0f);
            rect.anchorMax = new Vector2(x + 1f, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public void ResetTransition()
        {
            IsPlaying = false;
            if (_curtain != null) _curtain.gameObject.SetActive(false);
        }

        void OnDisable() => ResetTransition();
    }
}
