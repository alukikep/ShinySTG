using UnityEngine;

namespace ShinySTG.Player
{
    /// <summary>低速时显示受伤判定中心；独立于本体闪烁，不修改碰撞尺寸。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth), typeof(PlayerMovement), typeof(PlayerHitbox))]
    public sealed class PlayerHitboxIndicator : MonoBehaviour
    {
        [SerializeField, Tooltip("判定点贴图；留空使用运行时生成的中心亮点。")]
        Sprite _sprite;
        [SerializeField, Min(0.001f), Tooltip("图形在玩家本地坐标中的宽度，与实际碰撞尺寸独立。")]
        float _size = 0.16f;
        [SerializeField, Tooltip("判定点颜色。")]
        Color _color = Color.white;
        [SerializeField, Min(0f), Tooltip("显示和隐藏的过渡秒数；零表示立即切换。")]
        float _fadeDuration = 0.08f;
        [SerializeField, Tooltip("排序层名称，应与玩家处于相同或更高的排序层。")]
        string _sortingLayer = "Default";
        [SerializeField, Tooltip("层内排序，建议高于本体。")]
        int _sortingOrder = 30;

        PlayerHealth _health;
        PlayerMovement _movement;
        PlayerHitbox _hitbox;
        SpriteRenderer _renderer;
        Texture2D _generatedTexture;
        Sprite _generatedSprite;
        float _opacity;

        /// <summary>死亡表现跳过本组件独占的渲染器，隐藏由本组件处理。</summary>
        public bool OwnsRenderer(Renderer renderer) => renderer != null && renderer == _renderer;

        void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _movement = GetComponent<PlayerMovement>();
            _hitbox = GetComponent<PlayerHitbox>();
            var visual = new GameObject("Hitbox Indicator");
            visual.transform.SetParent(transform, false);
            _renderer = visual.AddComponent<SpriteRenderer>();
            _renderer.sprite = _sprite != null ? _sprite : CreateDefaultSprite();
            _renderer.sortingLayerName = _sortingLayer;
            _renderer.sortingOrder = _sortingOrder;
            float width = _renderer.sprite.bounds.size.x;
            visual.transform.localScale = Vector3.one * (Mathf.Max(0.001f, _size) / Mathf.Max(0.001f, width));
            HideImmediately();
        }

        void OnEnable()
        {
            _health.OnLifeLost += HideImmediately;
            HideImmediately();
        }

        void LateUpdate()
        {
            if (_renderer == null) return;
            if (_health == null || !_health.CanInteract || _movement == null || !_movement.isActiveAndEnabled
                || _hitbox == null || !_hitbox.isActiveAndEnabled)
            {
                HideImmediately();
                return;
            }
            // 使用受伤判定中心；LateUpdate 在移动之后同步。
            Vector2 position = _hitbox.Position;
            _renderer.transform.position = new Vector3(position.x, position.y, transform.position.z);
            float target = _movement.FocusHeld ? 1f : 0f;
            _opacity = _fadeDuration <= 0f ? target
                : Mathf.MoveTowards(_opacity, target, Time.deltaTime / _fadeDuration);
            var tint = _color;
            tint.a *= _opacity;
            _renderer.color = tint;
            _renderer.enabled = _opacity > 0f;
        }

        void HideImmediately()
        {
            _opacity = 0f;
            if (_renderer != null) _renderer.enabled = false;
        }

        void OnDisable()
        {
            if (_health != null) _health.OnLifeLost -= HideImmediately;
            HideImmediately();
        }

        Sprite CreateDefaultSprite()
        {
            const int resolution = 32;
            _generatedTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            _generatedTexture.name = "Player Hitbox Dot";
            _generatedTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                {
                    float radius = new Vector2(x + 0.5f - resolution / 2f, y + 0.5f - resolution / 2f).magnitude / (resolution / 2f);
                    float alpha = Mathf.Clamp01((1f - radius) * resolution / 2f);
                    pixels[y * resolution + x] = radius < 0.65f
                        ? new Color(1f, 1f, 1f, alpha) : new Color(0.2f, 0.35f, 0.6f, alpha);
                }
            _generatedTexture.SetPixels(pixels);
            _generatedTexture.Apply(false, true);
            _generatedSprite = Sprite.Create(_generatedTexture, new Rect(0, 0, resolution, resolution), Vector2.one * 0.5f, resolution);
            return _generatedSprite;
        }

        void OnDestroy()
        {
            if (_renderer != null) Destroy(_renderer.gameObject);
            if (_generatedSprite != null) Destroy(_generatedSprite);
            if (_generatedTexture != null) Destroy(_generatedTexture);
        }
    }
}
