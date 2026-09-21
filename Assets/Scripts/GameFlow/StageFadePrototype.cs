using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using PlayerController = ShinySTG.Player.Player;

namespace ShinySTG.GameFlow
{
    /// <summary>仅验证关间渐变与可移动机体；不暂停世界或推进关卡。</summary>
    [DisallowMultipleComponent]
    public sealed class StageFadePrototype : MonoBehaviour
    {
        [SerializeField, Tooltip("绘制玩家的正交战斗相机；留空使用 MainCamera。")]
        Camera _gameplayCamera;
        [SerializeField, Tooltip("HUD Canvas 下标出主游戏画面的矩形，不包含侧栏；必须配置，保持屏幕轴对齐。")]
        RectTransform _battleArea;
        [SerializeField, Min(0.01f), Tooltip("渐黑与渐亮各自持续的秒数。")]
        float _fadeDuration = 1f;
        [SerializeField, Min(0f), Tooltip("全黑期间可移动机体的测试秒数。")]
        float _blackDuration = 3f;

        Canvas _canvas;
        Image _curtain;
        Image _body;
        SpriteRenderer _source;
        Camera _camera;
        Coroutine _routine;
        readonly Vector3[] _areaCorners = new Vector3[4];
        Rect _screenArea;
        bool _flowOwned;
        public bool IsPlaying => _routine != null || _flowOwned;
        public RectTransform BattleArea => _battleArea;

        public void ConfigureBattleArea(RectTransform area) => _battleArea = area;

        public void Configure(Camera gameplayCamera) => _gameplayCamera = gameplayCamera;

        // 正式流程复用已配置的 BattleArea 和机体绘制；没有有效配置时由宿主降级为全屏渐变。
        public bool TryBeginFlow()
        {
            if (!isActiveAndEnabled || IsPlaying) return false;
            var player = PlayerController.Instance;
            _camera = _gameplayCamera != null ? _gameplayCamera : Camera.main;
            _source = player != null ? player.GetComponent<SpriteRenderer>() : null;
            Canvas.ForceUpdateCanvases();
            if (_source == null || _source.sprite == null || _source.drawMode != SpriteDrawMode.Simple
                || _camera == null || !_camera.isActiveAndEnabled || !_camera.orthographic
                || _camera.targetTexture != null || _camera.targetDisplay != 0 || !TryReadBattleArea()) return false;
            EnsureCanvas();
            _canvas.gameObject.SetActive(true);
            _curtain.color = Color.clear;
            _flowOwned = true;
            SyncBody();
            return true;
        }

        public IEnumerator FadeForFlow(bool covered)
        {
            if (!_flowOwned) throw new System.InvalidOperationException("关间转场已取消。");
            yield return Fade(covered ? 0f : 1f, covered ? 1f : 0f);
            if (!_flowOwned) throw new System.InvalidOperationException("关间转场已取消。");
            yield return null;
        }

        [ContextMenu("Play Fade Prototype (Play Mode)")]
        public void PlayPrototype()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsPlaying) return;
            var flow = GameFlowController.Instance;
            var player = PlayerController.Instance;
            _camera = _gameplayCamera != null ? _gameplayCamera : Camera.main;
            _source = player != null ? player.GetComponent<SpriteRenderer>() : null;
            if ((flow != null && (flow.IsLoading || flow.Failure != null))
                || player == null || player.Health == null || !player.Health.CanInteract
                || ShinySTG.Player.PlayerControlLock.IsLocked || Time.timeScale <= 0f
                || _source == null || _source.sprite == null || _source.drawMode != SpriteDrawMode.Simple
                || _camera == null || !_camera.isActiveAndEnabled || !_camera.orthographic
                || _camera.targetTexture != null || _camera.targetDisplay != 0)
            {
                Debug.LogWarning("[StageFade] 需要可操作的玩家根 SpriteRenderer 和输出到主屏幕的正交战斗相机；请等待开局完成。", this);
                return;
            }
            Canvas.ForceUpdateCanvases();
            if (!TryReadBattleArea())
            {
                Debug.LogWarning("[StageFade] 请绑定 HUD Canvas 下有效且屏幕轴对齐的 Battle Area，不能用相机范围代替。", this);
                return;
            }
            EnsureCanvas();
            _canvas.gameObject.SetActive(true);
            SyncBody();
            _routine = StartCoroutine(Play());
        }

        IEnumerator Play()
        {
            try
            {
                yield return Fade(0f, 1f);
                float remaining = Mathf.Max(0f, _blackDuration);
                // 即使停留时间为零，也让全黑画面实际绘制一帧。
                do
                {
                    yield return null;
                    remaining -= Time.unscaledDeltaTime;
                } while (remaining > 0f);
                yield return Fade(1f, 0f);
            }
            finally { ResetVisuals(); }
        }

        IEnumerator Fade(float from, float to)
        {
            float duration = Mathf.Max(0.01f, _fadeDuration);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                _curtain.color = new Color(0f, 0f, 0f,
                    Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
                yield return null;
            }
            _curtain.color = new Color(0f, 0f, 0f, to);
        }

        void LateUpdate()
        {
            if (!IsPlaying) return;
            var flow = GameFlowController.Instance;
            if (_source == null || _camera == null || !_camera.isActiveAndEnabled
                || !TryReadBattleArea()
                || (!_flowOwned && flow != null && (flow.IsLoading || flow.Failure != null)))
            {
                Cancel();
                return;
            }
            SyncBody();
        }

        void SyncBody()
        {
            SetBattleViewport(_curtain.rectTransform);
            SetBattleViewport((RectTransform)_body.transform.parent);
            var sprite = _source.sprite;
            _body.enabled = sprite != null && _source.enabled && !_source.forceRenderingOff
                && _source.gameObject.activeInHierarchy;
            if (!_body.enabled) return;
            _body.sprite = sprite;
            var bounds = sprite.bounds;
            var center = bounds.center;
            if (_source.flipX) center.x = -center.x;
            if (_source.flipY) center.y = -center.y;
            Vector3 position = _camera.WorldToScreenPoint(_source.transform.TransformPoint(center));
            Vector3 right = _camera.WorldToScreenPoint(_source.transform.TransformPoint(center + Vector3.right * bounds.size.x)) - position;
            Vector3 up = _camera.WorldToScreenPoint(_source.transform.TransformPoint(center + Vector3.up * bounds.size.y)) - position;
            _body.enabled = position.z > 0f;
            var rect = _body.rectTransform;
            rect.position = new Vector3(position.x, position.y, 0f);
            rect.sizeDelta = new Vector2(new Vector2(right.x, right.y).magnitude, new Vector2(up.x, up.y).magnitude);
            rect.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg);
            float handedness = right.x * up.y - right.y * up.x < 0f ? -1f : 1f;
            rect.localScale = new Vector3(_source.flipX ? -1f : 1f, (_source.flipY ? -1f : 1f) * handedness, 1f);
            // 在黑幕渐变时补回被遮掉的机体，避免零透明度时重复绘制透明边缘。
            var tint = _source.color;
            tint.a *= _curtain.color.a;
            _body.color = tint;
        }

        void EnsureCanvas()
        {
            if (_canvas != null) return;
            var root = new GameObject("StageFadeCanvas", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = short.MaxValue - 1;
            _curtain = CreateImage("Black", root.transform);
            SetBattleViewport(_curtain.rectTransform);
            _curtain.color = Color.clear;
            // 黑幕和机体共享显式 UI 区域，独立于战斗相机视口。
            var viewport = new GameObject("PlayerViewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(root.transform, false);
            SetBattleViewport((RectTransform)viewport.transform);
            _body = CreateImage("PlayerBody", viewport.transform);
            _body.useSpriteMesh = true;
        }

        void SetBattleViewport(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(_screenArea.xMin / Screen.width, _screenArea.yMin / Screen.height);
            rect.anchorMax = new Vector2(_screenArea.xMax / Screen.width, _screenArea.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        bool TryReadBattleArea()
        {
            if (_battleArea == null || !_battleArea.gameObject.activeInHierarchy
                || Screen.width <= 0 || Screen.height <= 0) return false;
            var canvas = _battleArea.GetComponentInParent<Canvas>();
            if (canvas == null) return false;
            canvas = canvas.rootCanvas;
            if (!canvas.isActiveAndEnabled || canvas.targetDisplay != 0
                || canvas.renderMode == RenderMode.WorldSpace) return false;
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera
                && (uiCamera == null || !uiCamera.isActiveAndEnabled || uiCamera.targetTexture != null)) return false;
            _battleArea.GetWorldCorners(_areaCorners);
            for (int i = 0; i < 4; i++)
                _areaCorners[i] = RectTransformUtility.WorldToScreenPoint(uiCamera, _areaCorners[i]);
            // 拒绝旋转区域，避免包围盒扩大后遮住侧栏。
            if (Mathf.Abs(_areaCorners[0].x - _areaCorners[1].x) > 0.5f
                || Mathf.Abs(_areaCorners[1].y - _areaCorners[2].y) > 0.5f
                || Mathf.Abs(_areaCorners[2].x - _areaCorners[3].x) > 0.5f
                || Mathf.Abs(_areaCorners[3].y - _areaCorners[0].y) > 0.5f) return false;
            float left = Mathf.Clamp(_areaCorners[0].x, 0f, Screen.width);
            float bottom = Mathf.Clamp(_areaCorners[0].y, 0f, Screen.height);
            float right = Mathf.Clamp(_areaCorners[2].x, 0f, Screen.width);
            float top = Mathf.Clamp(_areaCorners[2].y, 0f, Screen.height);
            _screenArea = Rect.MinMaxRect(left, bottom, right, top);
            return _screenArea.width > 0f && _screenArea.height > 0f;
        }

        static Image CreateImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        public void Cancel()
        {
            if (_routine != null) StopCoroutine(_routine);
            ResetVisuals();
        }

        void ResetVisuals()
        {
            _flowOwned = false;
            _routine = null;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            _source = null;
        }

        void OnDisable() => Cancel();
    }
}
