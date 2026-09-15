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
        /// 回收一条激光。严格对齐 BulletPool.Return:先摘 modifier 订阅 → 清 modifier 列表
        /// → SetActive(false) → 从 _active 移除 → 按 Data 路由到正确桶。
        /// </summary>
        public void Return(LaserEntity laser)
        {
            if (laser == null) return;
            // ★ OnDisable 会自动 ClearModifiers(LaserEntity.OnDisable),这里不用手动。
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

        // ═══════════════════════════════════════════════════════════
        // 中心化开火入口(对齐 BulletPool.FireGroup)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 供 FireLaserAction / LaserEmitter 调用。
        /// pattern.Fire() 内部已经处理 modifier 挂载 / FireSounds 触发,
        /// 本入口只负责把 LaserEntity 注册到活跃集合(由 Get 完成)。
        /// </summary>
        public LaserEntity FireGroup(LaserPattern pattern, Vector2 pos, float angleRad,
                                     HitboxComponent ownerHitbox,
                                     LaserModifier[] extraModifiers = null)
        {
            if (pattern == null) return null;
            return pattern.Fire(pos, angleRad, this, ownerHitbox, extraModifiers);
        }

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
