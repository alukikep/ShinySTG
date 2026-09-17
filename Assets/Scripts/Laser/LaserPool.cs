using System.Collections.Generic;
using UnityEngine;
using ShinySTG.Hitbox;

namespace ShinySTG.Laser
{
    /// <summary>
    /// 激光对象池。对齐 BulletPool 的"按 prefab 分桶 + 懒扩容 + 按 SourceData 路由回池"。
    ///
    /// 区别:Get 重载多一个 targetLength / curveNodes 参数,内部用 InitCurve 挂节点。
    /// 分桶 key 用 LaserData(同一份 Data 配不同 Renderer 时,共用桶效率最高)。
    /// </summary>
    public class LaserPool : MonoBehaviour
    {
        public static LaserPool Instance { get; private set; }

        [Tooltip("激光 prefab 模板(必须有 LaserEntity 组件 + LaserRendererBase 子组件)。")]
        public LaserEntity DefaultPrefab;

        [Tooltip("初始预热数量。")]
        public int InitialSize = 20;

        readonly Dictionary<LaserData, Stack<LaserEntity>> _available = new();
        readonly HashSet<LaserEntity> _active = new();

        /// <summary>供 LaserService 中心化碰撞时遍历活跃激光。</summary>
        public IReadOnlyCollection<LaserEntity> ActiveLasers => _active;

        /// <summary>
        /// per-LaserFireExtension 累加计数(由 FireGroup 入口维护,key = pattern.FireExtensions 数组里的具体元素 ref)。
        /// 与 BulletPool._fireCounts 1:1 对齐,详见 FireGroup 内注释。
        /// </summary>
        readonly Dictionary<LaserFireExtension, int> _fireCounts = new();

        void Awake()
        {
            Instance = this;
            // 只为 DefaultPrefab 预热一颗桶,避免用户尚未配置时 NRE。
            // 其他 Data 在第一次被 LaserPattern 真正使用时按需扩容。
            if (DefaultPrefab != null && InitialSize > 0)
            {
                var stack = GetOrCreateStack(DefaultPrefab.Data);
                for (int i = 0; i < InitialSize; i++)
                {
                    var l = Instantiate(DefaultPrefab, transform);
                    l.SourcePool = this;
                    l.gameObject.SetActive(false);
                    stack.Push(l);
                }
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        Stack<LaserEntity> GetOrCreateStack(LaserData data)
        {
            if (data == null) data = DefaultPrefab != null ? DefaultPrefab.Data : null;
            if (data == null) return new Stack<LaserEntity>(); // 兜底空桶(可能让 Get 直接失败,但不 NRE)
            if (!_available.TryGetValue(data, out var stack))
                _available[data] = stack = new Stack<LaserEntity>();
            return stack;
        }

        /// <summary>
        /// 取一条直线激光。prefab = DefaultPrefab(激光不分 prefab 桶,只分 Data 桶)。
        /// </summary>
        public LaserEntity Get(LaserData data, Vector2 pos, float angleRad, float length,
                               HitboxComponent ownerHitbox)
        {
            var prefab = DefaultPrefab;
            if (prefab == null) return null; // 没配 prefab → 直接 return,不 NRE
            var stack = GetOrCreateStack(data);
            var l = stack.Count > 0 ? stack.Pop() : Instantiate(prefab, transform);
            l.SourcePool = this;
            l.gameObject.SetActive(true);
            l.transform.SetParent(transform, worldPositionStays: false);
            l.Init(data, pos, angleRad, length, ownerHitbox);
            _active.Add(l);
            return l;
        }

        /// <summary>
        /// 取一条曲线激光(节点数组)。内部 Get 后挂节点。
        /// </summary>
        public LaserEntity GetCurve(LaserData data, Vector2 pos, float angleRad,
                                    Vector2[] curveNodes, HitboxComponent ownerHitbox)
        {
            var l = Get(data, pos, angleRad, 0f, ownerHitbox);
            if (l != null) l.InitCurve(data, pos, angleRad, curveNodes, ownerHitbox);
            return l;
        }

        /// <summary>
        /// 回收一条激光。严格对齐 BulletPool.Return 流程:
        ///   1. SetActive(false) → 同步触发 LaserEntity.OnDisable →
        ///      OnDisable 内部:DetachSignalTriggers + ClearModifiers(PR3 起新增 Detach 步骤)
        ///   2. 从 _active 移除
        ///   3. 按 Data 路由到正确桶
        ///
        /// ★ 为什么不在这里显式调 DetachSignalTriggers + ClearModifiers:
        ///   Unity SetActive(false) 是同步触发 OnDisable 的,这里显式调会与 OnDisable 内调用重复。
        ///   OnDetach 内部 _subscribedThisAttach 防重复 Subscribe,语义上安全,但徒增一次遍历 —— 没必要。
        ///
        /// ★ 但 BulletPool.Return 是显式调(因为 Bullet.OnDestroy 才触发 OnDisable,SetActive(false) 不一定触发)
        ///   —— 这是激光版 vs 子弹版的差异,不是 bug,详见 arch-laser §13.5.2 注释。
        /// </summary>
        public void Return(LaserEntity laser)
        {
            if (laser == null) return;
            // ★ SetActive(false) 同步触发 OnDisable,内部完成 DetachSignalTriggers + ClearModifiers
            laser.gameObject.SetActive(false);
            _active.Remove(laser);

            var key = laser.Data != null ? laser.Data : DefaultPrefab?.Data;
            if (key != null)
            {
                var stack = GetOrCreateStack(key);
                stack.Push(laser);
            }
            // 若 key 也为 null,则直接丢弃(极端兜底,不让池逻辑崩溃)。
        }

        /// <summary>批量回收符合阵营条件的活跃激光。filter 为空时回收全部。</summary>
        public int ReturnAll(System.Predicate<CollisionTeam> filter = null)
        {
            if (_active.Count == 0) return 0;
            var snapshot = new List<LaserEntity>(_active);
            int returned = 0;
            for (int i = 0; i < snapshot.Count; i++)
            {
                var laser = snapshot[i];
                if (laser == null) continue;
                if (filter != null && !filter(laser.Team)) continue;
                Return(laser);
                returned++;
            }
            return returned;
        }

        // ═══════════════════════════════════════════════════════════
        // 中心化开火入口(对齐 BulletPool.FireGroup)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 供 FireLaserAction / LaserEmitter 调用。
        /// 与 BulletPool.FireGroup 1:1 对齐,在调 pattern.Fire(...) 之前负责:
        ///   1. 触发 FireSounds(中心化触发,与子弹 BulletPool.FireGroup 行为对齐)
        ///   2. 维护 FireExtensions fireCounts 字典(per-LaserFireExtension 累加)
        /// pattern.Fire() 内部通过 LaserFireExtensionResolver 读取字典 + 挂 modifier + 生成激光实体。
        /// </summary>
        public LaserEntity FireGroup(LaserPattern pattern, Vector2 pos, float angleRad,
                                     HitboxComponent ownerHitbox,
                                     LaserModifier[] extraModifiers = null)
        {
            if (pattern == null) return null;

            // ─── 触发 LaserPattern 的开火音(FireSounds 数组) ───
            // 在 pattern.Fire(...) 之前调 —— 每次"开火组"触发一次。
            // 与 BulletPool.FireGroup.PlayFireSounds 调用点对齐(原 PR1 由 StraightLaserPattern.Fire 内部触发,
            // 现统一上移到 Pool 入口,与子弹架构保持一致,便于未来 CompositeLaserPattern 递归时只在最外层触发一次)。
            pattern.PlayFireSounds(pos, ownerHitbox);

            // ─── 本批 LaserFireExtension 累加计数 ───
            // 遍历 pattern.FireExtensions 数组,对每个非 null 元素在 _fireCounts 字典里 ++,
            // 得到的 fireCount(从 1 起)会在 pattern.Fire 内部被 Resolver 入口调 OnFireGroupTriggered。
            //
            // ★ per-instance 隔离 ★
            //   key = FireExtensions 数组里的具体元素引用(不是 SO 资产本身)。
            //   - 同一份 LaserPattern SO 被敌人 A / B 共用 → 它们 FireExtensions 数组里的元素是同一引用,
            //     字典累加会跨敌人 —— 这是项目想要的行为("Boss 旋转激光每 0.05s 开火转 5°" 跨多次开火累加)。
            //   - 用户复制一份 LaserPattern 资产(Ctrl+D)→ 新资产的 FireExtensions 数组是新元素,字典独立累加。
            //   - 用户手动给同一资产在多处挂不同 LaserFireExtension 子类实例 → 字典按 ref 区分,各自累加。
            //
            // ★ 字典清理 ★
            //   累加计数随 pattern 资产整个生命周期保留(场景切换时 LaserPool.OnDestroy 清 Instance,
            //   字典随之释放,无泄漏风险)。若未来要支持"关卡重置后累加清零",加一个 ResetFireCounts() 公共方法。
            //
            // ★ 空数组 / null → 不累加,行为 100% 等价历史。
            if (pattern.FireExtensions != null)
            {
                for (int i = 0; i < pattern.FireExtensions.Length; i++)
                {
                    var ext = pattern.FireExtensions[i];
                    if (ext == null) continue;
                    _fireCounts.TryGetValue(ext, out int prev);
                    _fireCounts[ext] = prev + 1;
                }
            }

            return pattern.Fire(pos, angleRad, this, ownerHitbox, extraModifiers);
        }

        /// <summary>
        /// 返回 pattern.FireExtensions 数组里每个非 null 元素当前的 fireCount(1 起)。
        /// 由 LaserPattern.Fire 内部调 Resolver 时传入(对齐 BulletPool.GetFireExtensionFireCounts)。
        ///
        /// 不存在 key → 不返回该元素(Resolver 走默认值 0,不调 OnFireGroupTriggered)。
        /// </summary>
        public IReadOnlyDictionary<LaserFireExtension, int> GetFireExtensionFireCounts() => _fireCounts;

        // ═══════════════════════════════════════════════════════════
        // 调试入口:Inspector 右键 LaserPool → "Test Fire" 即可在 (0,0) 朝右生成一条 1 秒静态激光,
        // 用于 5 秒内定位"激光系统本身是否能工作"(若能,则问题在 FireLaserAction / BehaviorFlow / 字段配置;
        // 若不能,问题在 LaserPool / Renderer / Prefab 结构)。
        // ═══════════════════════════════════════════════════════════
        [ContextMenu("Test Fire (1s static laser at world origin, facing right)")]
        void DebugTestFire()
        {
            // ★ 取默认 prefab 的 LaserData(无需额外配置,只要 DefaultPrefab 不为空)
            if (DefaultPrefab == null)
            {
                Debug.LogError("[Laser/DebugTestFire] DefaultPrefab 为 null。请先把 LaserEntity prefab 拖到 LaserPool.DefaultPrefab。", this);
                return;
            }
            if (DefaultPrefab.Data == null)
            {
                Debug.LogError("[Laser/DebugTestFire] DefaultPrefab.Data 为 null。请给 LaserEntity prefab 根上的 LaserEntity.Data 字段(或 DefaultPrefab 引用的 LaserData 资产)赋值。", this);
                return;
            }

            // ★ 不依赖任何 ownerHitbox —— 调试激光阵营 = Neutral,不会撞玩家,只用于视觉验证。
            var laser = Get(DefaultPrefab.Data, Vector2.zero, 0f,
                             DefaultPrefab.Data.MaxLength, ownerHitbox: null);
            if (laser == null)
            {
                Debug.LogError("[Laser/DebugTestFire] Get() 返回 null。LaserPool 内部出问题(检查 Data 与 prefab 是否匹配)。", this);
                return;
            }

            Debug.Log($"[Laser/DebugTestFire] 已生成激光:pos={laser.Position}, angle={laser.Angle * Mathf.Rad2Deg}°, " +
                      $"length={laser.CurrentLength}/{laser.TargetLength}, state={laser.State}, " +
                      $"data={laser.Data?.name}, hasRenderer={(laser.Renderer != null)}", laser);
        }
    }
}
