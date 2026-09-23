using UnityEngine;
using UnityEngine.Rendering;

namespace ShinySTG.Background
{
    public sealed partial class StageBackgroundController
    {
        readonly BackgroundImagePlayback _lowerImage = new BackgroundImagePlayback();
        readonly BackgroundImagePlayback _upperImage = new BackgroundImagePlayback();
        BackgroundImagePlayback.Snapshot _nextLowerImage, _nextUpperImage;
        Material _imageMaterial;
        CommandBuffer _lowerCommands, _upperCommands;
        Camera _imageCamera;
        CameraEvent _lowerEvent;
        bool _suppressSkybox;
        CameraClearFlags _imageClearFlags;
        float _fadeAlpha;
        MaterialPropertyBlock _imageProperties;
        static readonly int ImageTextureId = Shader.PropertyToID("_MainTex");
        static readonly int ImageColorId = Shader.PropertyToID("_Color");
        static readonly int ImageUvId = Shader.PropertyToID("_UvTransform");

        public BackgroundPlaybackHandle SetImage(BackgroundImageLayer layer, BackgroundImageDefinition image,
            float fadeOut = 0f, float fadeIn = 0f)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !HasValidBindings()
                || (layer != BackgroundImageLayer.Lower && layer != BackgroundImageLayer.Upper)
                || !Finite(fadeOut) || !Finite(fadeIn) || fadeOut < 0f || fadeIn < 0f
                || image != null && !image.IsValid)
                return FailedPlayback("2D 背景需要有效贴图配置、图层、非负时长及启用的背景控制器。");
            if (!EnsureImageRenderer()) return FailedPlayback("2D 背景 Shader 不可用。");
            CaptureInitialState();
            return (layer == BackgroundImageLayer.Lower ? _lowerImage : _upperImage)
                .Set(BackgroundImagePlayback.Snapshot.Capture(image), fadeOut, fadeIn);
        }

        public void CancelImagePlayback()
        {
            _lowerImage.Cancel();
            _upperImage.Cancel();
        }

        void TickImages()
        {
            if (!HasValidBindings())
            {
                _lowerImage.Handle?.Fail("2D 背景引用失效。");
                _upperImage.Handle?.Fail("2D 背景引用失效。");
                ReleaseImageRenderer();
                return;
            }
            if (!_strip.isActiveAndEnabled || IsPaused) return;
            _lowerImage.Advance(Time.deltaTime);
            _upperImage.Advance(Time.deltaTime);
        }

        bool EnsureImageRenderer()
        {
            if (_imageMaterial != null) return true;
            if (_backgroundCamera == null) return false;
            var shader = Resources.Load<Shader>("Background/BackgroundImage");
            if (shader == null || !shader.isSupported) return false;
            _imageProperties = new MaterialPropertyBlock();
            _imageMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _imageCamera = _backgroundCamera;
            _lowerEvent = _imageCamera.actualRenderingPath == RenderingPath.DeferredShading
                ? CameraEvent.BeforeGBuffer : CameraEvent.BeforeForwardOpaque;
            _lowerCommands = new CommandBuffer { name = "Background lower image" };
            _upperCommands = new CommandBuffer { name = "Background upper image and transition" };
            _imageCamera.AddCommandBuffer(_lowerEvent, _lowerCommands);
            _imageCamera.AddCommandBuffer(CameraEvent.AfterEverything, _upperCommands);
            Camera.onPreCull += PrepareImageRendering;
            return true;
        }

        void PrepareImageRendering(Camera camera)
        {
            if (camera != _imageCamera) return;
            _lowerCommands.Clear();
            _upperCommands.Clear();
            if (!isActiveAndEnabled) return;
            // Inherit the camera's active color target and viewport. Forcing CameraTarget here
            // breaks direct-screen, HDR and MSAA camera composition, which also hides the fade.
            bool lowerVisible = _lowerImage.Image != null && _lowerImage.Opacity > 0f;
            if (lowerVisible && !_suppressSkybox)
            {
                _imageClearFlags = camera.clearFlags;
                _suppressSkybox = true;
            }
            if (lowerVisible) camera.clearFlags = CameraClearFlags.SolidColor;
            else RestoreImageClearFlags();
            DrawImage(_lowerCommands, _lowerImage);
            DrawImage(_upperCommands, _upperImage);
            if (_fadeAlpha > 0f)
            {
                var color = _transitionColor;
                color.a = _fadeAlpha;
                // Keep the curtain independent of the last Sprite's atlas and sampling parameters.
                _imageProperties.Clear();
                _imageProperties.SetColor(ImageColorId, color);
                _upperCommands.DrawProcedural(Matrix4x4.identity, _imageMaterial, 1,
                    MeshTopology.Triangles, 3, 1, _imageProperties);
            }
        }

        void DrawImage(CommandBuffer commands, BackgroundImagePlayback layer)
        {
            if (layer.Image == null || layer.Image.Texture == null || layer.Opacity <= 0f) return;
            Color tint = layer.Image.Tint;
            tint.a *= layer.Opacity;
            float aspect = _imageCamera.pixelHeight > 0 ? (float)_imageCamera.pixelWidth / _imageCamera.pixelHeight : 1f;
            DrawImageQuad(commands, layer.Image.Texture, tint, layer.UvTransform(aspect), layer.Image);
        }

        void DrawImageQuad(CommandBuffer commands, Texture texture, Color color, Vector4 uv,
            BackgroundImagePlayback.Snapshot image = null)
        {
            _imageProperties.Clear();
            _imageProperties.SetTexture(ImageTextureId, texture);
            _imageProperties.SetColor(ImageColorId, color);
            _imageProperties.SetVector(ImageUvId, uv);
            _imageProperties.SetVector("_SpriteRegion", image != null ? image.Region : new Vector4(1, 1, 0, 0));
            _imageProperties.SetVector("_SpriteContent", image != null ? image.Content : new Vector4(1, 1, 0, 0));
            _imageProperties.SetFloat("_Repeat", image != null && image.Repeat ? 1f : 0f);
            commands.DrawProcedural(Matrix4x4.identity, _imageMaterial, 0, MeshTopology.Triangles, 3, 1, _imageProperties);
        }

        void RestoreImageClearFlags()
        {
            if (_suppressSkybox && _imageCamera != null) _imageCamera.clearFlags = _imageClearFlags;
            _suppressSkybox = false;
        }

        void ResetImages()
        {
            _lowerImage.Clear();
            _upperImage.Clear();
            _nextLowerImage = _nextUpperImage = null;
            RestoreImageClearFlags();
            _lowerCommands?.Clear();
            _upperCommands?.Clear();
        }

        void ReleaseImageRenderer()
        {
            Camera.onPreCull -= PrepareImageRendering;
            RestoreImageClearFlags();
            if (_imageCamera != null)
            {
                if (_lowerCommands != null) _imageCamera.RemoveCommandBuffer(_lowerEvent, _lowerCommands);
                if (_upperCommands != null) _imageCamera.RemoveCommandBuffer(CameraEvent.AfterEverything, _upperCommands);
            }
            _lowerCommands?.Release();
            _upperCommands?.Release();
            _lowerCommands = _upperCommands = null;
            _imageCamera = null;
            if (_imageMaterial != null)
            {
                if (Application.isPlaying) Destroy(_imageMaterial);
                else DestroyImmediate(_imageMaterial);
            }
            _imageMaterial = null;
        }
    }
}
