using System.Collections.Generic;
using UnityEngine;

namespace ShinySTG.Player
{
    [System.Serializable]
    public class OptionPowerLayout
    {
        [Tooltip("普通状态下各子机相对玩家的偏移。索引 = 子机编号。")]
        public Vector2[] NormalPositions = System.Array.Empty<Vector2>();

        [Tooltip("低速状态下各子机相对玩家的偏移。留空时使用普通位置。")]
        public Vector2[] FocusPositions = System.Array.Empty<Vector2>();

        public bool HasPosition(int index, bool focus)
        {
            var positions = focus && FocusPositions != null && FocusPositions.Length > 0
                ? FocusPositions
                : NormalPositions;
            return positions != null && index >= 0 && index < positions.Length;
        }

        public Vector2 GetPosition(int index, bool focus)
        {
            var positions = focus && FocusPositions != null && FocusPositions.Length > 0
                ? FocusPositions
                : NormalPositions;
            return positions[index];
        }
    }

    /// <summary>
    /// 子机系统。负责:
    ///   1. 根据当前火力级 (PlayerHealth.PowerLevel) 决定解锁几号子机
    ///   2. 用 PowerLayouts 按火力级决定每个子机的目标偏移
    ///   3. 子机跟随 (平滑插值) 玩家本体
    ///   4. 子机开火(各自持一个 FirePattern,可配置)。与主炮同源:必须按住攻击键才喷。
    ///
    ///   - OptionPrefab 走 BulletPool 同款 Bullet prefab 体系,不需要新基础设施
    ///
    /// Inspector 推荐配置:
    ///   - PowerLayouts: 数组索引 = 火力级,每项直接填写各子机的普通/低速位置
    ///   - OptionBulletPrefab: 拖 NatsuhaAOptionBullet.prefab 或任意 Bullet prefab
    ///   - OptionFirePatterns: 每号子机自己的弹幕(可空 = 只跟随不开火)
    ///   - UnlockThresholds: 长度 = 最大子机数(默认 4);每项 = 该号子机解锁需要的火力级
    ///     例: [1, 1, 3, 4] = 火力 1 解锁 2 个;火力 3 解锁 3 个;火力 4 解锁 4 个
    ///
    /// 满级(4 火力)解出 4 子机 → 符合你给的"4p 火力最大,子机最大 4"的需求。
    /// </summary>
    public class PlayerOptions : MonoBehaviour
    {
        [Header("Unlocks (活力阈值)")]
        [Tooltip("每个子机解锁所需的火力级。索引 = 子机编号 0..N-1。" +
"MaxPower = 4,通常阈值是 [1, 1, 3, 4] 或 [1, 2, 3, 4] 或 [1, 2, 4, 4]。")]
        public int[] UnlockThresholds = new[] { 1, 1, 3, 4 };

        [Header("Per Power Layout (按火力级布局)")]
        [Tooltip("按火力级配置子机位置。数组索引 = 火力级；每项可分别配置普通和低速位置。运行时始终使用此配置。")]
        public OptionPowerLayout[] PowerLayouts = new OptionPowerLayout[5];

        [Header("Option Prefab")]
        [Tooltip("子机 GameObject prefab。需要带 SpriteRenderer。脚本不需要,本组件直接控制 transform。")]
        public Transform OptionPrefab;

        [Header("Option Fire")]
        [Tooltip("每号子机的开火 FirePattern。索引 = 子机编号。null = 该子机只跟随不开火。")]
        public FirePattern[] OptionFirePatterns;

        [Tooltip("子机开火速率(每秒几轮)。")]
        public float OptionFireRate = 6f;

        [Header("Follow")]
        [Tooltip("子机跟随玩家的平滑系数(0 = 瞬移,值越大越平滑)。每帧:L = lerp(L, target, k)。")]
        [Range(0.01f, 1f)]
        public float FollowLerp = 0.25f;

        // [Header] 不允许用在 property 上,改用 region 分隔 / 在 Inspector 里看 ActiveCount 字样
        [Tooltip("当前已解锁的子机数(只读)。")]
        public int ActiveCount { get; private set; }

        readonly List<Transform> _spawned = new();
        readonly List<float> _fireCooldowns = new();
        readonly List<FirePatternRuntimeState> _fireStates = new();

        int _lastPowerLevel = -1;
        bool _visible = true;

        public void SetVisible(bool visible, bool snap = false)
        {
            _visible = visible;
            for (int i = 0; i < _spawned.Count; i++)
            {
                var option = _spawned[i];
                if (option == null) continue;
                if (snap)
                {
                    Vector2 offset = GetOffset(i, false);
                    option.position = transform.position + (Vector3)offset;
                }
                option.gameObject.SetActive(visible);
            }
        }

        void Start()
        {
            // Start 时立刻按当前火力刷新一次(避免出生时空一帧再冒出来)
            Rebresh();
        }

        void OnDisable()
        {
            foreach (var state in _fireStates) state.Reset();
            for (int i = 0; i < _fireCooldowns.Count; i++) _fireCooldowns[i] = 0f;
        }

        void OnDestroy()
        {
            foreach (var t in _spawned) if (t != null) Destroy(t.gameObject);
            _spawned.Clear();
            _fireCooldowns.Clear();
            _fireStates.Clear();
        }

        void Update()
        {
            // 1. 监听火力级变化,刷新子机数量
            int power = Player.Instance != null && Player.Instance.Health != null
                ? Player.Instance.Health.PowerLevel : 0;
            if (power != _lastPowerLevel)
            {
                _lastPowerLevel = power;
                Rebresh();
            }

            if (!_visible) return;

            // 2. 跟随 + 开火
            bool focus = Player.Instance != null && Player.Instance.Movement != null
                && Player.Instance.Movement.FocusHeld;
            // 子机开火跟主炮同源:必须按住攻击键才喷。松开 → 完全停火(cooldown 也不衰减)。
            bool fireHeld = Player.Instance != null && Player.Instance.Shooting != null
                && Player.Instance.Shooting.FireHeld;

            Vector3 origin = transform.position;
            for (int i = 0; i < _spawned.Count; i++)
            {
                var t = _spawned[i];
                if (t == null) continue;

                Vector2 off = GetOffset(i, focus);

                Vector3 target = origin + new Vector3(off.x, off.y, 0f);
                t.position = Vector3.Lerp(t.position, target, FollowLerp);

                // 开火:仅在按住时 tick cooldown + FireGroup
                if (fireHeld && BulletPool.Instance != null
                    && OptionFirePatterns != null && i < OptionFirePatterns.Length
                    && OptionFirePatterns[i] != null)
                {
                    if (i >= _fireCooldowns.Count) _fireCooldowns.Add(0f);
                    float cooldown = _fireCooldowns[i];
                    int bursts = FireCadence.Tick(ref cooldown, OptionFireRate, Time.deltaTime);
                    _fireCooldowns[i] = cooldown;
                    for (int burst = 0; burst < bursts; burst++)
                    {
                        // 子机弹阵营 = 玩家阵营(共用 Player.Instance.Hitbox.Team)
                        var ownerHb = Player.Instance?.Hitbox;
                        BulletPool.Instance.FireGroup(OptionFirePatterns[i], t.position, 0f, ownerHb, null, _fireStates[i]);
                    }
                }
            }
        }

        Vector2 GetOffset(int index, bool focus)
        {
            if (PowerLayouts != null)
            {
                int power = Player.Instance != null && Player.Instance.Health != null
                    ? Player.Instance.Health.PowerLevel : 0;
                if (power >= 0 && power < PowerLayouts.Length)
                {
                    var layout = PowerLayouts[power];
                    if (layout != null && layout.HasPosition(index, focus))
                        return layout.GetPosition(index, focus);
                }
            }
            return Vector2.zero;
        }

        /// 根据当前火力级 + UnlockThresholds 算出应解锁几个,然后增减 _spawned 列表。
        /// 已有子机的 transform 保留,只在尾部增删,不会重新生成。
        public void Rebresh()
        {
            int power = Player.Instance != null && Player.Instance.Health != null
                ? Player.Instance.Health.PowerLevel : 0;

            int desired = 0;
            if (UnlockThresholds != null)
            {
                for (int i = 0; i < UnlockThresholds.Length; i++)
                    if (power >= UnlockThresholds[i]) desired++;
            }

            // 多出来的要删掉(从尾删,保留前 N 个稳定不变)
            while (_spawned.Count > desired)
            {
                int last = _spawned.Count - 1;
                if (_spawned[last] != null) Destroy(_spawned[last].gameObject);
                _spawned.RemoveAt(last);
                _fireStates.RemoveAt(last);
                if (last < _fireCooldowns.Count) _fireCooldowns.RemoveAt(last);
            }
            // 不够的补上
            while (_spawned.Count < desired)
            {
                _fireStates.Add(new FirePatternRuntimeState());
                if (OptionPrefab == null) { _spawned.Add(null); _fireCooldowns.Add(0f); continue; }
                var t = Instantiate(OptionPrefab, transform.position, Quaternion.identity);
                t.gameObject.SetActive(_visible);
                _spawned.Add(t);
                _fireCooldowns.Add(0f);
            }

            ActiveCount = _spawned.Count;
        }
    }
}
