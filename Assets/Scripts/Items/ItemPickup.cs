using UnityEngine;
using ShinySTG.Hitbox;

namespace ShinySTG.Items
{
    // 由 ItemDropService 创建和推进，不使用 Physics2D，也不自行执行碰撞循环。
    public sealed class ItemPickup : MonoBehaviour
    {
        SpriteRenderer _renderer;
        ItemDefinition _definition;
        Vector2 _velocity;
        float _age;
        bool _attracting;
        bool _collected;
        float _size;
        public int SpawnFrame { get; private set; }
        public bool IsCollected => _collected;
        public Rect Bounds => new Rect((Vector2)transform.position - Vector2.one * _size * 0.5f, Vector2.one * _size);

        internal void Initialize(ItemDefinition definition, Vector2 position, Vector2 velocity, Sprite fallback)
        {
            if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
            _definition = definition;
            _velocity = velocity;
            _age = 0f;
            _attracting = false;
            _collected = false;
            SpawnFrame = Time.frameCount;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            _size = Mathf.Max(0.01f, definition.Size);
            transform.localScale = Vector3.one * _size;
            _renderer.sprite = definition.Sprite != null ? definition.Sprite : fallback;
            _renderer.color = definition.Tint;
            _renderer.sortingOrder = 20;
            // 统一世界显示尺寸，同时保持图片宽高比。
            float width = _renderer.sprite.bounds.size.x;
            if (width > 0f) transform.localScale /= width;
            gameObject.SetActive(true);
        }

        internal bool Tick(float dt, ItemDropService service, ShinySTG.Player.Player player)
        {
            if (_collected || _definition == null || !gameObject.activeInHierarchy) return false;
            _age += dt;
            bool canCollect = CanCollect(player);
            if (!canCollect) _attracting = false;
            else if (!player.Hitbox.AttractionEnabled) _attracting = false;
            else if (
                     HitboxMath.AABBOverlap(Bounds, player.Hitbox.AttractionBounds)) _attracting = true;

            Vector2 position = transform.position;
            if (_attracting)
            {
                position = Vector2.MoveTowards(position, player.transform.position, Mathf.Max(0f, service.AttractionSpeed) * dt);
                _velocity = Vector2.zero;
            }
            else
            {
                _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, Mathf.Max(0f, service.HorizontalDrag) * dt);
                _velocity.y = Mathf.Max(-Mathf.Max(0f, service.FallSpeed), _velocity.y - Mathf.Max(0f, service.Gravity) * dt);
                position += _velocity * dt;
            }
            transform.position = position;
            return _age < Mathf.Max(0.1f, service.Lifetime) && position.y >= service.DespawnBelowY;
        }

        public static bool CanCollect(ShinySTG.Player.Player player) => player != null && player.isActiveAndEnabled &&
            player.Health != null && player.Health.isActiveAndEnabled && !player.Health.IsDead &&
            player.Hitbox != null && player.Hitbox.isActiveAndEnabled;

        public bool TryCollect(ShinySTG.Player.Player player)
        {
            if (_collected || _definition == null || SpawnFrame >= Time.frameCount || !CanCollect(player)) return false;
            if ((_definition.Kind == ItemKind.Score || _definition.Kind == ItemKind.Bomb) && player.Resources == null) return false;
            _collected = true; // 在奖励事件之前锁定，防止回调重入。
            switch (_definition.Kind)
            {
                case ItemKind.SmallPower: player.Health.AddPowerUnits(1); break;
                case ItemKind.LargePower: player.Health.AddPowerUnits(100); break;
                case ItemKind.Score: player.Resources.AddScore(Mathf.Max(1, _definition.ScoreValue)); break;
                case ItemKind.Bomb: player.Resources.AddBombs(); break;
                case ItemKind.OneUp: player.Health.AddLife(); break;
            }
            return true;
        }

        internal void ResetForPool()
        {
            _definition = null;
            _velocity = Vector2.zero;
            _age = 0f;
            _size = 0f;
            _attracting = false;
            _collected = false;
            SpawnFrame = -1;
            _renderer.sprite = null;
            _renderer.color = Color.white;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }
    }
}
