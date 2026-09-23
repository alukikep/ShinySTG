using UnityEngine;

namespace ShinySTG.Background
{
    /// <summary>一层贴图的独立播放状态；配置在请求时复制，取消保留当前画面。</summary>
    internal sealed class BackgroundImagePlayback
    {
        internal sealed class Snapshot
        {
            public Texture2D Texture;
            public Vector4 Region;
            public Vector4 Content;
            public float Aspect;
            public bool Repeat;
            public Color Tint;
            public BackgroundImageFit Fit;
            public Vector2 Scale, Offset, Speed;

            public static Snapshot Capture(BackgroundImageDefinition definition)
            {
                if (definition == null) return null;
                var sprite = definition.Sprite;
                var texture = sprite.texture;
                var rect = sprite.textureRect;
                var size = sprite.rect.size;
                var offset = sprite.textureRectOffset;
                return new Snapshot
                {
                    Texture = texture, Tint = definition.Tint, Fit = definition.Fit,
                    Scale = definition.UvScale, Offset = definition.UvOffset, Speed = definition.ScrollSpeed,
                    Aspect = size.x / size.y, Repeat = definition.Repeat,
                    Region = new Vector4(rect.width / texture.width, rect.height / texture.height,
                        rect.x / texture.width, rect.y / texture.height),
                    Content = new Vector4(rect.width / size.x, rect.height / size.y, offset.x / size.x, offset.y / size.y)
                };
            }
        }

        public Snapshot Image { get; private set; }
        public float Opacity { get; private set; }
        public BackgroundPlaybackHandle Handle { get; private set; }
        public Vector2 Scroll { get; private set; }
        Snapshot _next;
        float _fadeOut, _fadeIn, _elapsed, _startOpacity;
        bool _leaving;

        public BackgroundPlaybackHandle Set(Snapshot image, float fadeOut, float fadeIn)
        {
            Cancel();
            Handle = new BackgroundPlaybackHandle();
            _next = image;
            _fadeOut = Image == null ? 0f : fadeOut;
            _fadeIn = fadeIn;
            _startOpacity = Opacity;
            _elapsed = 0f;
            _leaving = true;
            Advance(0f);
            return Handle;
        }

        public void Advance(float deltaTime)
        {
            if (Image != null && Image.Texture == null || _next != null && _next.Texture == null)
            {
                Handle?.Fail("2D 背景贴图已被销毁。");
                Image = _next = null;
                Opacity = 0f;
                return;
            }
            if (Handle != null && !Handle.IsComplete)
            {
                _elapsed += deltaTime;
                if (_leaving)
                {
                    Opacity = _fadeOut <= 0f ? 0f : Mathf.Lerp(_startOpacity, 0f, _elapsed / _fadeOut);
                    if (_elapsed >= _fadeOut)
                    {
                        _elapsed -= _fadeOut;
                        Image = _next;
                        _next = null;
                        Scroll = Vector2.zero;
                        _leaving = false;
                    }
                }
                if (!_leaving)
                {
                    Opacity = Image == null ? 0f : _fadeIn <= 0f ? 1f : Mathf.Clamp01(_elapsed / _fadeIn);
                    if (Image == null || _elapsed >= _fadeIn) Handle.Complete();
                }
            }
            if (Image != null)
                Scroll += Image.Speed * deltaTime;
        }

        public void Cancel()
        {
            Handle?.Cancel();
            _next = null;
        }

        public void Clear()
        {
            Cancel();
            Image = null;
            Opacity = 0f;
            Scroll = Vector2.zero;
        }

        public Vector4 UvTransform(float aspect)
        {
            Vector2 fit = Vector2.one;
            float textureAspect = Image.Aspect;
            if (Image.Fit == BackgroundImageFit.Cover)
            {
                if (textureAspect > aspect) fit.x = aspect / textureAspect;
                else fit.y = textureAspect / aspect;
            }
            var scale = Vector2.Scale(fit, Image.Scale);
            var offset = (Vector2.one - fit) * 0.5f;
            offset = Vector2.Scale(offset, Image.Scale) + Image.Offset + Scroll;
            return new Vector4(scale.x, scale.y, offset.x, offset.y);
        }
    }
}
