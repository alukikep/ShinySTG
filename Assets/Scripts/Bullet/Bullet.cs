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
    }

    readonly List<BulletModifier> _modifiers = new();

    public void AddModifier(BulletModifier m) => _modifiers.Add(m);
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
        transform.rotation = Quaternion.Euler(0, 0, fireAngleRad * Mathf.Rad2Deg);
        SteerAngle = fireAngleRad;
        Speed = speed;
        AngularSpeed = angularSpeed;
        Damage = damage;
        Lifetime = 0;
        _modifiers.Clear();

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
        transform.rotation = Quaternion.Euler(0, 0, SteerAngle * Mathf.Rad2Deg);

        // 5. 简单越界回收（先实现，后续再优化）
        if (Mathf.Abs(transform.position.x) > 10f ||
            Mathf.Abs(transform.position.y) > 20f)
        {
            BulletPool.Instance.Return(this);
        }
    }
}