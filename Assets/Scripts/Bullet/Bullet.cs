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

    // ─── 出生雾化(per-instance 状态) ───
    [HideInInspector] public float FogElapsed;          // 雾化计时器
    [HideInInspector] public float FogDuration;         // 雾化总时长(0 = 不雾化)
    [HideInInspector] public SpawnFogConfig FogCfg;     // 雾化配置引用(只读,SO 不需 Clone)
    [HideInInspector] public bool  IsFogged => FogDuration > 0f && FogElapsed < FogDuration;
    [HideInInspector] public Vector3 _baseLocalScale;   // prefab 美术缩放基准(Init 时从 transform 读一次,典型 0.28)
    MaterialPropertyBlock _fogMpb;                      // 雾化期 MPB(懒分配,与 BulletColorModifier 不冲突)

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

        // ★ 视觉兼容兜底:BulletColorModifier 通过 MaterialPropertyBlock 写 _TintColor,
        //   但配套 shader STG/BulletTint 才会读这个字段。如果 prefab 的 SpriteRenderer 用了
        //   其他 shader(最常见的就是忘了切的 Sprites/Default),染色/渐变/闪烁会"静默失效"。
        //   这里做一次懒切换:不是 STG/BulletTint → 自动换成配套 shader 派生的 .mat 实例。
        //   ★ 不会破坏 batching:STG/BulletTint 的 _TintColor 走 [PerRendererData],MPB 友好。
        //   ★ 不会污染 prefab:这里改的是 runtime 实例的 sharedMaterial,prefab 源资产不动。
        EnsureTintCompatibleMaterial();
    }

    /// <summary>
    /// 确保 SpriteRenderer.sharedMaterial 的 shader 是 STG/BulletTint。
    /// 如果不是,运行时用 Shader.Find 创建一个 material 替换上去(per-instance 不破坏 prefab)。
    /// 配套 shader 路径:Assets/Shaders/BulletTint.shader。
    /// </summary>
    void EnsureTintCompatibleMaterial()
    {
        if (Renderer == null) return;
        var sm = Renderer.sharedMaterial;
        if (sm == null || sm.shader == null) return;
        if (sm.shader.name == "STG/BulletTint") return;  // 已对,跳过

        var sh = Shader.Find("STG/BulletTint");
        if (sh == null) return;  // shader 没编进来(不应该发生,兜底静默)
        var tint = new Material(sh) { name = "BulletTint (auto-fallback)" };
        Renderer.sharedMaterial = tint;
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
    /// 重置所有 modifier 的时间窗口计时器(由 BulletPool.AttachModifiers 调用)。
    /// 配合基类的 Delay / Duration / OneShot:
    ///   - 每颗子弹从池里取出时,_elapsed=0,IsActive=false
    ///   - OneShot modifier 重新具备触发机会(回池复用时不会"哑火")
    ///
    /// 为什么在 BulletPool 而不是 Bullet.Init 里调:
    ///   Init 时刻 _modifiers 已被 ClearModifiers 清空;真正的 modifier 在 AttachModifiers
    ///   才挂上,所以 ResetWindow 也必须在挂完之后立刻调,时间窗口从这一刻起算。
    /// </summary>
    public void ResetAllModifierWindows()
    {
        for (int i = 0; i < _modifiers.Count; i++) _modifiers[i].ResetWindow();
    }

    /// <summary>
    /// 由池在 Get 时调用:写入初始参数 + 重置 modifiers。
    /// </summary>
    /// <param name="position">发射位置</param>
    /// <param name="fireAngleRad">发射角度(弧度)</param>
    /// <param name="speed">飞行速度</param>
    /// <param name="angularSpeed">角速度(弧度/秒)</param>
    /// <param name="damage">伤害值(玩家弹才用)</param>
    /// <param name="ownerTeam">发射者阵营(用于把子弹 Hitbox.Team 设为同阵营)</param>
    /// <param name="spawnFog">出生雾化配置(可空)。null 或 Duration=0 = 不雾化。</param>
    public void Init(Vector2 position, float fireAngleRad, float speed, float angularSpeed,
                     float damage, CollisionTeam ownerTeam, SpawnFogConfig spawnFog = null)
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
        // 注意:ResetAllModifierWindows 不在这里调 —— Init 时刻 _modifiers 已清空,
        // 真正的 modifier 在 BulletPool.AttachModifiers 里挂上,所以 ResetWindow 也在那里调。
        // 重置擦弹标记:让上一轮擦过玩家的弹,回池后再发射可以重新擦(HasGrazed 由 CollisionService 在擦弹时置 true)。
        HasGrazed = false;

        // ★ 出生雾化参数(由 BulletPool.Get 透传自 FirePattern.SpawnFog)
        //   null-safe;Duration<=0 时所有雾化字段进入"不雾化"状态,
        //   Update 跳过雾化 early-return,行为与历史 100% 等价。
        //   视觉缩放走 transform.localScale 乘法:_baseLocalScale(从 prefab 实例读一次,典型 0.28) × fogScale
        //   → 雾化期 0.28 × fogScale,清晰后 0.28 × 1 = 0.28(prefab 基准)。
        //   prefab 自带美术缩放始终保留,绝不被破坏。
        FogElapsed  = 0f;
        FogCfg      = spawnFog;
        FogDuration = (spawnFog != null) ? Mathf.Max(0f, spawnFog.Duration) : 0f;
        // ★ 缓存 prefab 美术缩放基准(从 transform 读,首次 spawn 时是 prefab 里的 0.28;
        //   池复用时是上次的 _baseLocalScale,保证不受之前雾化期 * fogScale 的影响)。
        _baseLocalScale = transform.localScale;
        // 兜底:以防 prefab 美术缩放本身不是 (0.28, 0.28, 0.28),统一用 prefab 实际值。
        // Init 时刻 transform.localScale 来自 prefab 实例,完全反映 prefab 美术基准。
        if (Hitbox != null) Hitbox.IsFogged = FogDuration > 0f;
        if (Renderer != null && FogDuration > 0f) ApplyFogVisual();  // 立刻设 _FogAmount=1 + localScale *= fogScale
        else if (Renderer != null) ClearFogVisual();                  // 立刻清 _FogAmount=0 + localScale = _baseLocalScale(兜底)

        // 自动透传阵营:玩家弹 → Team=Player;敌人弹 → Team=Enemy;owner 为 null 时保持默认(Neutral)
        if (Hitbox != null) Hitbox.Team = ownerTeam;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Lifetime += dt;

        // ═══════════════════════════════════════════════════════════
        // ★ 出生雾化期(优先级最高,先于 modifier + 移动 + 碰撞)
        //   雾化期行为:
        //     - 位置固定(不按 Speed 移动)
        //     - Hitbox.IsFogged=true → CollisionService 三个 Tick 都会跳过
        //     - 不调任何 modifier → modifier 时间窗口从雾化结束开始累计
        //     - 视觉:STG/BulletTintFog shader 走 _FogAmount=1→0 的连续过渡
        //   雾化结束那一帧:清除 IsFogged + 视觉复位,modifier 从 0 开始计时
        // ═══════════════════════════════════════════════════════════
        if (FogDuration > 0f)
        {
            FogElapsed += dt;

            if (IsFogged)
            {
                // 雾化期内:不动、不参与碰撞、不调 modifier
                ApplyFogVisual();
                return;  // ★ early-return,跳过下面所有逻辑
            }
            else
            {
                // ★ 雾化刚结束这一帧(只在 FogElapsed 第一次越过 FogDuration 时进一次)
                //   Hitbox.IsFogged 清掉,后续 CollisionService 正常处理;
                //   ClearFogVisual 把 _FogAmount=0 + transform.localScale=_baseLocalScale,
                //   让 BulletColorModifier 在下一帧正常接管 tint 渲染,Hitbox 判定盒大小恢复正常。
                if (Hitbox != null) Hitbox.IsFogged = false;
                ClearFogVisual();
                // 不 return:继续走下面的"清晰后"逻辑
            }
        }

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

    // ═══════════════════════════════════════════════════════════
    // 出生雾化视觉方法
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 在雾化期内每帧调用:把 _FogAmount / _FogColor 写到 Renderer 上,并按 fogScale 修改 transform.localScale。
    ///   - _FogAmount = 1 - EasedT(雾化→清晰)
    ///   - transform.localScale = _baseLocalScale × Lerp(FogStartScale, 1, EasedT)
    ///   - _FogColor = FogCfg.FogColor
    /// ★ 设计要点 ★:走 transform.localScale 乘法缩放(prefab 美术基准 _baseLocalScale 始终保留),FogStartScale 语义直观:
    ///   FogStartScale=1.4 表示"出生瞬间大小是正常的 1.4 倍"。
    ///   shader vertex 完全不动 → 不产生位置偏移,不会出现 vertex 缩放路径的"快速移动"bug。
    /// ★ Hitbox 影响:雾化期 Hitbox.IsFogged=true → CollisionService 各 Tick 跳过,不参与碰撞/擦弹。
    ///   雾化结束 ClearFogVisual 把 localScale 恢复为 _baseLocalScale,Hitbox 判定盒大小恢复正常。
    /// 与 BulletColorModifier 的协作:
    ///   - 两者都走 MPB;这里用 per-instance _fogMpb,先 GetPropertyBlock(保留 ColorModifier 已写的 _TintColor),
    ///     再覆盖 _FogAmount / _FogColor,SetPropertyBlock(合并应用)。
    /// </summary>
    void ApplyFogVisual()
    {
        if (FogCfg == null || Renderer == null) return;
        float t = Mathf.Clamp01(FogElapsed / Mathf.Max(FogDuration, 0.0001f));
        float eased = ApplyEasing(t, FogCfg.Easing);
        float fogAmount = 1f - eased;                              // 1(全雾)→ 0(清晰)
        float fogScale  = Mathf.Lerp(Mathf.Max(FogCfg.FogStartScale, 0.01f), 1f, eased);

        // ★ 视觉缩放:transform.localScale 乘法叠加在 prefab 美术基准上。
        //   _baseLocalScale = 0.28(典型),fogScale=1.4 → 最终 0.392(显示大小 1.4× 正常)。
        //   fogScale=1 → 0.28(正常,等价历史)。
        //   不会破坏 prefab 美术缩放,Hitbox 在雾化期不参与碰撞所以 lossyScale 变化无副作用。
        transform.localScale = _baseLocalScale * fogScale;

        if (_fogMpb == null) _fogMpb = new MaterialPropertyBlock();
        Renderer.GetPropertyBlock(_fogMpb);                          // 保留 BulletColorModifier 的 _TintColor
        _fogMpb.SetFloat("_FogAmount", fogAmount);
        _fogMpb.SetColor("_FogColor", FogCfg.FogColor);
        Renderer.SetPropertyBlock(_fogMpb);
    }

    /// <summary>
    /// 雾化结束后调用一次:把 _FogAmount=0 + transform.localScale=_baseLocalScale,让 BulletColorModifier / 普通 tint 完全接管渲染。
    /// 不复位 _FogColor(下次再用时 ApplyFogVisual 会重写)。
    /// </summary>
    void ClearFogVisual()
    {
        if (Renderer == null) return;
        // ★ 视觉缩放复位:回到 prefab 美术基准,Hitbox 判定盒大小恢复正常。
        transform.localScale = _baseLocalScale;
        if (_fogMpb == null) _fogMpb = new MaterialPropertyBlock();
        Renderer.GetPropertyBlock(_fogMpb);
        _fogMpb.SetFloat("_FogAmount", 0f);
        Renderer.SetPropertyBlock(_fogMpb);
    }

    /// <summary>
    /// 雾化期 _FogAmount 的缓动函数(影响'凝聚'节奏,与视觉是否切换的逻辑完全无关)。
    /// 默认 None = 线性;用户可选 EaseOut / EaseIn / EaseInOut 给雾化期不同节奏感。
    /// </summary>
    static float ApplyEasing(float t, FogEasing e)
    {
        switch (e)
        {
            case FogEasing.None:     return t;
            case FogEasing.EaseOut:  return 1f - (1f - t) * (1f - t);                // 前期快,后期慢
            case FogEasing.EaseIn:   return t * t;                                    // 前期慢,后期快
            case FogEasing.EaseInOut:
                return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);       // 两头慢,中间快
            default:                return t;
        }
    }
}