using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ShinySTG.Hitbox;

/// <summary>
/// 单颗子弹的运行时实体。由 BulletPool.Get(...) 创建/复用。
///
/// 总控模式(对齐 Player / Enemy):
///   - [RequireComponent] 强制每颗子弹自动挂 HitboxComponent(跟玩家/敌人总控一致)。
///   - 子弹阵营 HitboxComponent.Team 由发射者(owner)透传:玩家发射 → Team=Player,敌人发射 → Team=Enemy。
///   - 子弹伤害 Damage 由 FirePattern 提供(由 pool.Get 经 Init 写入)。
/// </summary>
[RequireComponent(typeof(HitboxComponent))]
public class Bullet : MonoBehaviour
{

    [HideInInspector] public float Speed;
    [HideInInspector] public float AngularSpeed; // 弧度/秒，0 = 不自转
    [HideInInspector] public float SteerAngle;   // 弧度，当前飞行方向
    [HideInInspector] public float Lifetime;     // 累计存活时间
    [HideInInspector] public bool  HasGrazed;    // 本弹是否已对玩家触发过擦弹(防一颗弹多次擦;Init 时重置)

    [HideInInspector] public Bullet SourcePrefab; // 记录本弹属于哪个 prefab 的桶（仅用于池归还路由，不影响逻辑）

    [Header("Combat")]
    [Tooltip("子弹命中敌人时的伤害值。由 FirePattern.Damage 在 pool.Get 时写入。\n" +
             "敌人弹不读此字段 — 敌人弹命中玩家直接走 player.OnHit(1),无视 Damage。")]
    public float Damage = 1f;

    [Header("Hitbox")]
    [Tooltip("由 [RequireComponent] 自动挂载;不需要手动拖拽。\n" +
             "阵营由发射者(owner)的 Hitbox.Team 透传设置 — 玩家发射 → Player,敌人发射 → Enemy。\n" +
             "正中尖刺时通过 Hitbox.Size 调整判定大小。")]
    public HitboxComponent Hitbox;

    [Header("Visual")]
    [Tooltip("子弹的 SpriteRenderer(可选)。Awake / Reset 时自动 GetComponentInChildren 抓取(包含 inactive)。\n" +
             "若 prefab 的 SpriteRenderer 在子物体上,自动找到。\n" +
             "BulletColorModifier 等视觉 modifier 通过此引用改 color。\n" +
             "允许为空(没有可见子弹 / 走 VFX-only / ParticleSystem 表现);视觉 modifier 自身会做 null 保护。")]
    public SpriteRenderer Renderer;

    public Vector2 Position => transform.position;

    /// <summary>是否与某 hitbox 相撞(便捷入口)。Hitbox 未配置时返回 false。</summary>
    public bool Overlaps(HitboxComponent other) =>
        Hitbox != null && other != null && Hitbox.Overlaps(other);

    void Awake()
    {
        // 总控模式:跟 Player / Enemy 一致 — [RequireComponent] 自动挂 HitboxComponent,
        // 这里只负责把引用抓回来。Unity 在 Inspector 看不到此字段的赋值(运行时 Awake),
        // 但代码里调 Hitbox.X 时一定不为 null。
        if (Hitbox == null) Hitbox = GetComponent<HitboxComponent>();
        // Renderer 是可选的(走 [SerializeReference] 多态视觉 modifier 的入口);
        // 用 GetComponentInChildren(true) 兼顾 SpriteRenderer 在子物体上的 prefab 结构。
        if (Renderer == null) Renderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    void Reset()
    {
        // 编辑器新建/选中 bullet prefab 时给一个合理的默认大小(STG 经典弹径 0.08)。
        // 阵营默认 Neutral — 必须由发射者在 pool.Get 时透传设置;否则不参与碰撞。
        if (Hitbox == null) Hitbox = GetComponent<HitboxComponent>();
        if (Hitbox != null)
        {
            Hitbox.Size = new Vector2(0.08f, 0.08f);
            Hitbox.Team = CollisionTeam.Neutral;
        }
        // Renderer 同样在 Reset 时尝试抓一次,方便编辑器新建 prefab 即看到 Inspector 字段已填;
        // includeInactive=true 让 Editor 在 prefab 折叠 / inactive 状态下也能拿到引用。
        if (Renderer == null) Renderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    readonly List<BulletModifier> _modifiers = new();

    public void AddModifier(BulletModifier m) => _modifiers.Add(m);

    /// <summary>
    /// 清空所有 modifier(纯 C# 列表操作,无需 Destroy)。
    /// 在 BulletPool.Return / Init 里被调用,确保回池后列表干净。
    /// Modifier 不是 GameObject(走 SerializeReference + Clone 路线),不需要销毁子对象。
    /// </summary>
    public void ClearModifiers() => _modifiers.Clear();

    /// <summary>
    /// 由池在 Get 时调用:写入初始参数 + 重置 modifiers。
    /// </summary>
    /// <param name="position">发射位置</param>
    /// <param name="fireAngleRad">发射角度(弧度)</param>
    /// <param name="speed">飞行速度</param>
    /// <param name="angularSpeed">角速度(弧度/秒)</param>
    /// <param name="damage">伤害值(玩家弹才用)</param>
    /// <param name="ownerTeam">发射者阵营(用于把子弹 Hitbox.Team 设为同阵营)</param>
    public void Init(Vector2 position, float fireAngleRad, float speed, float angularSpeed,
                     float damage, CollisionTeam ownerTeam)
    {
        transform.position = position;
        // 视觉补偿:美术贴图默认尖头朝 +Y(朝上),代码约定 SteerAngle=0 指向 +X(朝右)。
        // 因此需要 -90° 的旋转偏移,才能让贴图尖头对齐飞行方向(否则向下发射时子弹会变横)。
        transform.rotation = Quaternion.Euler(0, 0, fireAngleRad * Mathf.Rad2Deg - 90f);
        SteerAngle = fireAngleRad;
        Speed = speed;
        AngularSpeed = angularSpeed;
        Damage = damage;
        Lifetime = 0;
        // 清空 modifier 列表。Clone 出的 modifier 是纯 C# 对象,可直接 GC 回收;
        // 没有 GameObject 子对象需要 Destroy(对比旧 MonoBehaviour 路线)。
        ClearModifiers();
        // 重置擦弹标记:让上一轮擦过玩家的弹,回池后再发射可以重新擦(HasGrazed 由 CollisionService 在擦弹时置 true)。
        HasGrazed = false;

        // 自动透传阵营:玩家弹 → Team=Player;敌人弹 → Team=Enemy;owner 为 null 时保持默认(Neutral)
        if (Hitbox != null) Hitbox.Team = ownerTeam;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Lifetime += dt;

        // 1. 让 modifier 修改当前状态
        foreach (var m in _modifiers) m.Modify(this, dt);

        // 2. 角速度累加到当前飞行方向
        SteerAngle += AngularSpeed * dt;

        // 3. 按当前方向移动
        Vector2 dir = new Vector2(Mathf.Cos(SteerAngle), Mathf.Sin(SteerAngle));
        transform.position += (Vector3)(dir * Speed * dt);

        // 4. 用方向同步旋转（让贴图朝向飞行方向）
        //    与 Init() 同源:贴图尖头朝 +Y,所以需要 -90° 的视觉补偿偏移。
        transform.rotation = Quaternion.Euler(0, 0, SteerAngle * Mathf.Rad2Deg - 90f);

        // 5. 简单越界回收（先实现，后续再优化）
        if (Mathf.Abs(transform.position.x) > 10f ||
            Mathf.Abs(transform.position.y) > 20f)
        {
            BulletPool.Instance.Return(this);
        }
    }
}