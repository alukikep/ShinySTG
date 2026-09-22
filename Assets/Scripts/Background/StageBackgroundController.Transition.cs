using UnityEngine;
using UnityEngine.UI;

namespace ShinySTG.Background
{
    public sealed partial class StageBackgroundController
    {
        LoopingBackgroundStrip _initialStrip;
        bool _initialContentActive;
        GameObject _spawnedContent;
        Color _initialClearColor;
        CameraClearFlags _initialClearFlags;
        Canvas _fadeCanvas;
        Image _fadeImage;
        bool _switching;
        bool _swapped;
        float _switchElapsed;
        float _fadeOut;
        float _fadeIn;
        GameObject _nextPrefab;
        Vector3 _nextPosition;
        Quaternion _nextRotation;
        float _nextFov;
        float _nextSpeed;
        Color _nextColor;
        bool _nextOverrideClearColor;


        public BackgroundPlaybackHandle SwitchBackground(BackgroundDefinition definition, float fadeOut = 1f, float fadeIn = 1f)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !HasValidBindings() || definition == null
                || !Finite(fadeOut) || !Finite(fadeIn) || fadeOut < 0f || fadeIn < 0f
                || !Finite(definition.CameraPosition) || !Finite(definition.CameraEulerAngles)
                || !Finite(definition.FieldOfView) || definition.FieldOfView < 1f || definition.FieldOfView > 179f
                || !Finite(definition.ScrollSpeed) || definition.ScrollSpeed < 0f
                || (definition.OverrideClearColor && !Finite((Vector4)definition.ClearColor)))
                return FailedPlayback("换景配置或背景引用无效。");
            var prefab = definition.ContentPrefab;
            int layer = LayerMask.NameToLayer("Background3D");
            if (prefab == null || prefab.scene.IsValid() || !prefab.activeSelf
                || prefab.GetComponent<LoopingBackgroundStrip>() == null
                || !prefab.GetComponent<LoopingBackgroundStrip>().enabled
                || prefab.GetComponentsInChildren<LoopingBackgroundStrip>(true).Length != 1)
                return FailedPlayback("请选择根节点包含一个启用的 LoopingBackgroundStrip 的完整布景 Prefab。");
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (!(component is Transform) && !(component is MeshFilter) && !(component is MeshRenderer)
                    && !(component is SpriteRenderer) && !(component is LODGroup) && !(component is LoopingBackgroundStrip))
                    return FailedPlayback("换景 Prefab 仅支持静态布景和根节点循环组件，不支持相机、灯光或其它脚本。");
                if (component.gameObject.layer != layer)
                    return FailedPlayback("换景 Prefab 全部物体必须使用 Background3D 层。");
            }
            if (!prefab.GetComponent<LoopingBackgroundStrip>().IsLayoutValid)
                return FailedPlayback("换景布景布局无效，需要至少两个路段和有效长度。");
            CaptureInitialState();
            EnsureFade();
            CancelPlayback();
            CurrentPlayback = new BackgroundPlaybackHandle();
            _nextPrefab = prefab;
            _nextPosition = definition.CameraPosition;
            _nextRotation = Quaternion.Euler(definition.CameraEulerAngles);
            _nextFov = definition.FieldOfView;
            _nextSpeed = definition.ScrollSpeed;
            _nextColor = definition.ClearColor;
            _nextOverrideClearColor = definition.OverrideClearColor;
            _fadeOut = fadeOut;
            _fadeIn = fadeIn;
            _switchElapsed = 0f;
            _swapped = false;
            _switching = true;
            return CurrentPlayback;
        }

        void TickSwitch()
        {
            if (!IsTransitioning) { _switching = false; SetFade(0f); return; }
            if (!HasValidBindings() || _initialStrip == null || _fadeImage == null || (!_swapped && _nextPrefab == null))
            {
                CurrentPlayback.Fail("换景引用失效。");
                _switching = false;
                SetFade(0f);
                return;
            }
            if (!_strip.isActiveAndEnabled || IsPaused || Time.deltaTime <= 0f) return;
            _switchElapsed += Time.deltaTime;
            if (!_swapped)
            {
                SetFade(_fadeOut <= 0f ? 1f : Mathf.Clamp01(_switchElapsed / _fadeOut));
                if (_switchElapsed < _fadeOut) return;
                try { SwapContent(); }
                catch (System.Exception exception)
                {
                    CurrentPlayback.Fail(exception.Message);
                    _switching = false;
                    SetFade(0f);
                    Debug.LogException(exception, this);
                    return;
                }
                _swapped = true;
                _switchElapsed = 0f;
                return; // 至少保留一帧全遮罩，避免低帧率跳过换景遮挡。
            }
            SetFade(_fadeIn <= 0f ? 0f : 1f - Mathf.Clamp01(_switchElapsed / _fadeIn));
            if (_switchElapsed < _fadeIn) return;
            _switching = false;
            CurrentPlayback.Complete();
        }

        void SwapContent()
        {
            var content = Instantiate(_nextPrefab, _initialStrip.transform.parent, false);
            var next = content.GetComponent<LoopingBackgroundStrip>();
            if (!next.isActiveAndEnabled)
            {
                Destroy(content);
                throw new System.InvalidOperationException("新布景初始化失败。");
            }
            _strip.gameObject.SetActive(false);
            if (_spawnedContent != null) Destroy(_spawnedContent);
            _spawnedContent = content;
            _strip = next;
            _strip.Speed = _nextSpeed;
            _strip.Resume();
            _strip.ResetBackground();
            _cameraRig.localPosition = _nextPosition;
            _cameraRig.localRotation = _nextRotation;
            _backgroundCamera.fieldOfView = _nextFov;
            if (_nextOverrideClearColor)
            {
                _backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
                _backgroundCamera.backgroundColor = _nextColor;
            }

        }

        void RestoreInitialContent()
        {
            if (_spawnedContent != null)
            {
                _spawnedContent.SetActive(false);
                Destroy(_spawnedContent);
                _spawnedContent = null;
            }
            if (_initialStrip != null)
            {
                _strip = _initialStrip;
                _strip.gameObject.SetActive(_initialContentActive);

            }
            if (_hasInitialState && _backgroundCamera != null)
            {
                _backgroundCamera.backgroundColor = _initialClearColor;
                _backgroundCamera.clearFlags = _initialClearFlags;
            }
        }

        void EnsureFade()
        {
            if (_fadeCanvas != null) return;
            var go = new GameObject("BackgroundTransitionOverlay", typeof(RectTransform), typeof(Canvas));
            go.layer = LayerMask.NameToLayer("Background3D");
            go.transform.SetParent(_backgroundCamera.transform, false);
            _fadeCanvas = go.GetComponent<Canvas>();
            _fadeCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            _fadeCanvas.worldCamera = _backgroundCamera;
            _fadeCanvas.overrideSorting = true;
            _fadeCanvas.sortingOrder = short.MaxValue;
            var image = new GameObject("Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            image.layer = go.layer;
            image.transform.SetParent(go.transform, false);
            _fadeImage = image.GetComponent<Image>();
            _fadeImage.raycastTarget = false;
            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            SetFade(0f);
        }

        void SetFade(float alpha)
        {
            if (_fadeImage == null) return;
            _fadeCanvas.planeDistance = _backgroundCamera != null ? _backgroundCamera.nearClipPlane + 0.01f : 0.31f;
            _fadeImage.color = new Color(0f, 0f, 0f, alpha);
            _fadeCanvas.enabled = alpha > 0f;
            // 完成/取消后移出整个 Canvas 渲染层级，避免残留 Graphic 参与绘制。
            _fadeCanvas.gameObject.SetActive(alpha > 0f);
        }

        [ContextMenu("Log Background Visual State (Play Mode)")]
        void LogBackgroundVisualState()
        {
            if (!Application.isPlaying || _backgroundCamera == null || _strip == null) return;
            var report = new System.Text.StringBuilder("[Background Visual State]\n");
            report.AppendLine($"Playback={CurrentPlayback?.Status}, Switching={_switching}, Paused={IsPaused}");
            report.AppendLine($"Camera world={_backgroundCamera.transform.position}, rotation={_backgroundCamera.transform.eulerAngles}, FOV={_backgroundCamera.fieldOfView}");
            report.AppendLine($"Content={_strip.name}, world={_strip.transform.position}, scale={_strip.transform.lossyScale}");
            report.AppendLine($"Overlay active={(_fadeCanvas != null && _fadeCanvas.gameObject.activeInHierarchy)}, alpha={(_fadeImage != null ? _fadeImage.color.a : 0f)}");
            report.AppendLine($"Camera clear={_backgroundCamera.backgroundColor}, global fog={RenderSettings.fog}, global fog color={RenderSettings.fogColor}");
            int count = 0;
            foreach (var renderer in _strip.GetComponentsInChildren<MeshRenderer>())
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || !material.HasProperty("_BackgroundFogStart")) continue;
                    report.AppendLine($"Mesh={renderer.name}, center={renderer.bounds.center}, distance={Vector3.Distance(renderer.bounds.center, _backgroundCamera.transform.position):F2}, material={material.name}, shader={material.shader.name}, fogColor={material.GetColor("_BackgroundFogColor")}, fog={material.GetFloat("_BackgroundFogStart")}/{material.GetFloat("_BackgroundFogEnd")}, strength={material.GetFloat("_BackgroundFogStrength")}");
                    if (++count >= 4) break;
                }
                if (count >= 4) break;
            }
            Debug.Log(report.ToString(), this);
        }

        static bool Finite(Vector4 value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
    }
}

