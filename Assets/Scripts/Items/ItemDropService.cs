using System.Collections.Generic;
using UnityEngine;

namespace ShinySTG.Items
{
    public sealed class ItemDropService : MonoBehaviour
    {
        public static ItemDropService Instance { get; private set; }
        [Min(0f), Tooltip("撒出后的向下加速度。")]
        public float Gravity = 8f;
        [Min(0f), Tooltip("最大下落速度。")]
        public float FallSpeed = 1.5f;
        [Min(0f), Tooltip("水平速度每秒衰减量。")]
        public float HorizontalDrag = 3f;
        [Min(0f), Tooltip("吸附移动速度。")]
        public float AttractionSpeed = 10f;
        [Min(0.1f), Tooltip("未拾取道具的最长存活秒数。")]
        public float Lifetime = 30f;
        [Tooltip("低于该世界 Y 坐标时回收。")]
        public float DespawnBelowY = -22f;
        [Header("上线收点")]
        [Tooltip("玩家位于收点线上方时，持续吸附场上及后续生成的道具。")]
        public bool EnableAutoCollect = true;
        [Tooltip("收点线的世界 Y 坐标；玩家达到或超过该高度时持续收点。")]
        public float AutoCollectLineY = 3f;
        readonly List<ItemPickup> _active = new();
        readonly Stack<ItemPickup> _pool = new();
        public IReadOnlyList<ItemPickup> ActiveItems => _active;
        public bool IsAutoCollecting { get; private set; }
        Sprite _fallback;
        Texture2D _texture;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _texture = new Texture2D(1, 1);
            _texture.SetPixel(0, 0, Color.white);
            _texture.Apply();
            _fallback = Sprite.Create(_texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
        }

        public static void Spawn(DropProfile profile, Vector2 position)
        {
            if (ShinySTG.Level.BattleRestriction.IsActive || profile == null || profile.Entries == null) return;
            if (Instance == null || !Instance.isActiveAndEnabled)
            {
                Debug.LogWarning("[Items] 场景缺少 ItemDropService，未生成掉落。", profile);
                return;
            }
            Instance.SpawnInternal(profile, position);
        }

        public static bool SpawnSingle(ItemDefinition definition, Vector2 position, Vector2 velocity)
        {
            if (definition == null || Instance == null || !Instance.isActiveAndEnabled) return false;
            Instance.SpawnSingleInternal(definition, position, velocity);
            return true;
        }

        void SpawnSingleInternal(ItemDefinition definition, Vector2 position, Vector2 velocity)
        {
            ItemPickup item = null;
            while (_pool.Count > 0 && item == null) item = _pool.Pop();
            if (item == null)
            {
                var go = new GameObject("Item Pickup");
                go.SetActive(false);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
                item = go.AddComponent<ItemPickup>();
            }
            item.Initialize(definition, position, velocity, _fallback);
            _active.Add(item);
        }

        void SpawnInternal(DropProfile profile, Vector2 position)
        {
            float min = Mathf.Max(0f, Mathf.Min(profile.SpeedRange.x, profile.SpeedRange.y));
            float max = Mathf.Max(min, Mathf.Max(profile.SpeedRange.x, profile.SpeedRange.y));
            foreach (var entry in profile.Entries)
            {
                if (entry == null || entry.Item == null) continue;
                float chance = Mathf.Clamp01(entry.DropChance);
                if (chance <= 0f || (chance < 1f && Random.value > chance)) continue;
                for (int i = 0; i < entry.Count; i++)
                {
                    ItemPickup item = null;
                    while (_pool.Count > 0 && item == null) item = _pool.Pop();
                    if (item == null)
                    {
                        var go = new GameObject("Item Pickup");
                        go.SetActive(false);
                        // 服务挂点可移动/缩放；道具用独立根对象避免被其 Transform 带动。
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
                        item = go.AddComponent<ItemPickup>();
                    }
                    float angle = (profile.DirectionDegrees + Random.Range(-0.5f, 0.5f) * Mathf.Clamp(profile.SpreadDegrees, 0f, 360f)) * Mathf.Deg2Rad;
                    Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(min, max);
                    item.Initialize(entry.Item, position + Random.insideUnitCircle * Mathf.Max(0f, profile.SpawnRadius), velocity, _fallback);
                    _active.Add(item);
                }
            }
        }

        void Update()
        {
            if (ShinySTG.GameFlow.GameplayPause.IsPaused) return;
            var player = ShinySTG.Player.Player.Instance;
            UpdateAutoCollectState(player);
            for (int i = _active.Count - 1; i >= 0; i--)
                if (_active[i] == null || !_active[i].Tick(Time.deltaTime, this, player)) ReturnAt(i);
        }

        void UpdateAutoCollectState(ShinySTG.Player.Player player)
        {
            IsAutoCollecting = EnableAutoCollect && ItemPickup.CanCollect(player)
                && player.transform.position.y >= AutoCollectLineY;
        }

        public void ResetAutoCollectState() => IsAutoCollecting = false;

        void OnDrawGizmosSelected()
        {
            if (!EnableAutoCollect) return;
            var bounds = ShinySTG.Stage.BoundsService.Instance;
            float left = bounds != null ? bounds.PlayableMin.x : -3.5f;
            float right = bounds != null ? bounds.PlayableMax.x : 3.5f;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(new Vector3(left, AutoCollectLineY, 0f),
                new Vector3(right, AutoCollectLineY, 0f));
        }

        // 碰撞遍历结束后调用，不在 TryCollect 内修改活跃集合。
        public void FlushCollected()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                if (_active[i] == null || _active[i].IsCollected) ReturnAt(i);
        }

        void ReturnAt(int index)
        {
            var item = _active[index];
            _active.RemoveAt(index);
            if (item == null) return;
            item.ResetForPool();
            _pool.Push(item);
        }

        void OnDisable()
        {
            IsAutoCollecting = false;
            ReturnAll();
        }

        public void ReturnAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--) ReturnAt(i);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_fallback != null) Destroy(_fallback);
            if (_texture != null) Destroy(_texture);
            foreach (var item in _active) if (item != null) Destroy(item.gameObject);
            foreach (var item in _pool) if (item != null) Destroy(item.gameObject);
            _active.Clear();
            _pool.Clear();
        }
    }
}
