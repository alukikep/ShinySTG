using System.Collections.Generic;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.Player
{
    /// <summary>
    /// 子机系统。负责:
    ///   1. 根据当前火力级 (PlayerHealth.PowerLevel) 决定解锁几号子机
    ///   2. 用 OptionPositionForm 多态决定每个子机的目标偏移
    ///   3. 子机跟随 (平滑插值) 玩家本体
    ///   4. 子机开火(各自持一个 FirePattern,可配置)。与主炮同源:必须按住攻击键才喷。
    ///
    /// 跟项目现有架构的对齐点:
    ///   - PositionForm 用 [SerializeReference] + [SRName],跟 MoveBehaviour / EnemyAction 一个套路
    ///   - OptionPrefab 走 BulletPool 同款 Bullet prefab 体系,不需要新基础设施
    ///
    /// Inspector 推荐配置:
    ///   - PositionForm: 下拉选 Form/Touhou Symmetric / Linear Row / Rear Line / ...
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

        [Header("Position Form (多态)")]
        [SerializeReference, SR]
        [Tooltip("子机位置形态。下拉选:Touhou Symmetric / Linear Row / Rear Line / 自定义子类。")]
        public OptionPositionForm PositionForm = new TouhouSymmetricForm();

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

        int _lastPowerLevel = -1;

        void Start()
        {
            PositionForm?.OnEnter(this);
            // Start 时立刻按当前火力刷新一次(避免出生时空一帧再冒出来)
            Rebresh();
        }

        void OnDestroy()
        {
            PositionForm?.OnExit(this);
            foreach (var t in _spawned) if (t != null) Destroy(t.gameObject);
            _spawned.Clear();
            _fireCooldowns.Clear();
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

                Vector2 off = PositionForm != null
                    ? PositionForm.GetOffset(i, focus, _spawned.Count)
                    : Vector2.zero;

                Vector3 target = origin + new Vector3(off.x, off.y, 0f);
                t.position = Vector3.Lerp(t.position, target, FollowLerp);

                // 开火:仅在按住时 tick cooldown + FireGroup
                if (fireHeld
                    && OptionFirePatterns != null && i < OptionFirePatterns.Length
                    && OptionFirePatterns[i] != null)
                {
                    if (i >= _fireCooldowns.Count) _fireCooldowns.Add(0f);
                    _fireCooldowns[i] -= Time.deltaTime;
                    if (_fireCooldowns[i] <= 0f)
                    {
                        _fireCooldowns[i] = 1f / Mathf.Max(0.0001f, OptionFireRate);
                        // 子机弹阵营 = 玩家阵营(共用 Player.Instance.Hitbox.Team)
                        var ownerHb = Player.Instance?.Hitbox;
                        BulletPool.Instance.FireGroup(OptionFirePatterns[i], t.position, 0f, ownerHb);
                    }
                }
            }
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
                if (last < _fireCooldowns.Count) _fireCooldowns.RemoveAt(last);
            }
            // 不够的补上
            while (_spawned.Count < desired)
            {
                if (OptionPrefab == null) { _spawned.Add(null); _fireCooldowns.Add(0f); continue; }
                var t = Instantiate(OptionPrefab, transform.position, Quaternion.identity);
                _spawned.Add(t);
                _fireCooldowns.Add(0f);
            }

            ActiveCount = _spawned.Count;
        }
    }
}