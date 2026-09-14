using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 通用「子弹再发射」modifier 基类 —— 让子弹在飞行途中按一份 FirePattern 再开火。
///
/// 设计动机:
///   现有 modifier 只能改子弹自身状态(加速 / 转向 / 追踪 / 染色 / ...),
///   没有「在飞行途中再触发一次 FirePattern」的能力。
///   本基类 + 两个内置子类覆盖典型需求:
///     - <see cref="FireOnEnterBulletModifier"/>   OneShot:延迟 N 秒后开一次火组
///     - <see cref="FireOnDurationBulletModifier"/> 持续型:窗口期内每 N 秒开一次火组
///
/// 与旧 SpawnRingOnDelayModifier 的区别:
///   旧样例只支持"延迟 N 秒后均分环爆",且分裂弹阵营永远 = Neutral、不走 FireExtensions 角度管道。
///   新版复用 <see cref="BulletPool.FireGroup"/> 完整链路:
///     ✅ FireExtensions 角度管道(Base / PlayerAim / Offset ...)生效
///     ✅ Pattern.ModifierPrefabs 自动挂载到分裂弹
///     ✅ Pattern.SpawnFog 透传
///     ✅ Pattern.FireSounds 开火音触发
///     ✅ 分裂弹阵营 = 母弹阵营(可关),能正常撞人
///     ✅ 任意 FirePattern 形态可用(Ring / Line / Arc / Composite / ...)
///
/// 时序行为(对齐 ARCHITECTURE §2.6):
///   - 本基类不重定义时间窗口,直接复用基类的 Delay / Duration / OneShot / AutoSkipOutsideWindow
///   - 子类只需 override <see cref="ComputeCenterAngle"/> 决定「分裂弹的中线方向」,
///     再在 <see cref="OnWindowEnter"/>(OneShot)或 <see cref="ModifyCore"/>(持续型)里调一次 <see cref="FireOnce"/>
///
/// 字段约定:
///   - Pattern / InheritOwnerTeam / ExtraModifiers 是公共字段,直接被 Inspector 编辑
///   - _accumulator / _shotsFired 标 [NonSerialized],每颗 Clone 时自然归零
///   - ExtraModifiers 是 BulletModifier[] → 必须 override Clone 深拷
///
/// 协作边界:
///   - 不修改 Bullet / FirePattern / BulletPool 任何代码,纯新增文件
///   - 走 [SerializeReference, SR] 多态下拉,用户可在任意 FirePattern / FireAction 资产里挂这个 modifier
/// </summary>
[Serializable]
public abstract class FirePatternBulletModifier : BulletModifier
{
    [Header("Pattern")]
    [Tooltip("要发射的 FirePattern 资产(拖一个 .asset,比如 Ring / Line / Arc / Composite)。\n" +
             "留空则本 modifier 安静跳过,不会 NRE。")]
    public FirePattern Pattern;

    [Tooltip("分裂弹阵营策略:\n" +
             "  true (默认) = 继承母弹阵营,分裂弹能撞母弹本应撞的目标\n" +
             "                (玩家子弹分裂打敌人 / 敌人子弹分裂打玩家 / Boss 弹分裂打玩家)\n" +
             "  false       = 分裂弹阵营 = Neutral,不参与碰撞(纯视觉效果 / 表演性分裂)")]
    public bool InheritOwnerTeam = true;

    [Tooltip("额外追加到分裂弹的 modifier(会跟在 Pattern.ModifierPrefabs 之后追加)。\n" +
             "典型用法:\n" +
             "  - 母弹没挂追踪,但希望分裂弹带追踪 → 拖一个 HomingEnemyModifier\n" +
             "  - 母弹挂的是减速分裂,这里再叠加速 → 拖一个 AccelerateModifier\n" +
             "留空 = 只用 Pattern 资产自己的 ModifierPrefabs。")]
    [SerializeReference, SR]
    public BulletModifier[] ExtraModifiers;

    // ─── per-instance 状态(Clone 时 [NonSerialized] 自然归零) ───
    [NonSerialized] protected float _accumulator;   // 持续型触发的间隔累加器
    [NonSerialized] protected int   _shotsFired;    // 已发射次数(供 MaxShots / 调试)

    /// <summary>已发射的分裂次数(只读)。用于调试 / 上层逻辑判断。</summary>
    public int ShotsFired => _shotsFired;

    /// <summary>
    /// 子类 override:计算本次分裂的「中线方向」(弧度)。
    /// 默认 = 母弹当前飞行方向(b.SteerAngle),适合「沿母弹方向分裂」。
    /// 子类可改成「玩家方向」「母弹反方向」「相对母弹 +N°」等。
    /// </summary>
    protected virtual float ComputeCenterAngle(Bullet b) => b.SteerAngle;

    /// <summary>
    /// 触发一次开火组(已走完整 BulletPool.FireGroup 链路)。
    /// 由子类的 OnWindowEnter(OneShot)或 ModifyCore(持续型)调一次。
    /// null-safe:Pattern 留空 / BulletPool 未挂 → 静默 return。
    /// </summary>
    protected void FireOnce(Bullet b)
    {
        if (Pattern == null || BulletPool.Instance == null) return;

        // ownerHitbox 取值:
        //   - InheritOwnerTeam = true → b.Hitbox(母弹阵营)
        //   - false              → null  (Neutral)
        // 注:b.Hitbox 是 HitboxComponent 引用(由 [RequireComponent] 保证非 null),
        //   不是 Unity API 调用,不会被 Destroy 失效。
        var owner = InheritOwnerTeam ? b.Hitbox : null;
        float rotationRad = ComputeCenterAngle(b);

        // 走完整链路:PlayFireSounds → pattern.Fire → SpawnBullet → pool.Get
        //   - FireExtensions 角度管道由 Pattern 自己处理
        //   - Pattern.ModifierPrefabs + ExtraModifiers 自动挂载
        //   - Pattern.SpawnFog 透传
        BulletPool.Instance.FireGroup(
            Pattern,
            b.Position,
            rotationRad,
            ownerHitbox: owner,
            extraModifiers: ExtraModifiers);

        _shotsFired++;
    }

    // ─── Clone 深拷 ───
    // ExtraModifiers 是 BulletModifier[],默认 MemberwiseClone 会共享同一数组
    // → 多颗子弹共享同一组 modifier 引用(Clone 出来的 modifier 本身已被 Clone 深拷,
    //   但数组本身被共享会污染「增删元素」语义)。STG 高弹量场景下需要明确深拷。
    public override BulletModifier Clone()
    {
        var copy = (FirePatternBulletModifier)MemberwiseClone();
        if (ExtraModifiers != null)
        {
            copy.ExtraModifiers = new BulletModifier[ExtraModifiers.Length];
            for (int i = 0; i < ExtraModifiers.Length; i++)
            {
                copy.ExtraModifiers[i] = ExtraModifiers[i]?.Clone();
            }
        }
        // _accumulator / _shotsFired 标了 [NonSerialized],MemberwiseClone 后自然为 0 / 0,
        // 每颗子弹重新计时,符合预期。
        return copy;
    }
}

/// <summary>
/// OneShot 版:子弹出生 <see cref="BulletModifier.Delay"/> 秒后,触发一次 <see cref="FirePatternBulletModifier.Pattern"/>,
/// 然后本 modifier 立刻结束。
///
/// 默认值:<see cref="BulletModifier.OneShot"/> = true(进入窗口瞬间调一次 OnWindowEnter)。
///
/// 典型用法(完整复用 FirePattern 能力):
///   1. 主炮挂本 modifier,Pattern = 一份 PlayerAim 的 RingFirePattern,Delay = 0.5
///      → 主炮飞 0.5 秒后,在自身位置按 RingFirePattern 发射一圈分裂弹
///      → 分裂弹自动获得 RingFirePattern 的 FireExtensions / ModifierPrefabs / SpawnFog
///   2. Boss 挂本 modifier,Pattern = 一份 PlayerAim 的 CompositeFirePattern(包含「追踪玩家」modifier)
///      → Boss 弹飞行 N 秒后爆出一组瞄准玩家的复合弹,分裂弹也带追踪
///
/// 与旧 SpawnRingOnDelayModifier 区别:
///   - 旧:自己写均分循环、阵营永远中性、不走 FireExtensions
///   - 新:走完整 FireGroup 链路、阵营可继承母弹、FireExtensions 全生效
/// </summary>
[Serializable, SRName("Modifier/Fire Pattern On Delay")]
public class FireOnEnterBulletModifier : FirePatternBulletModifier
{
    public FireOnEnterBulletModifier()
    {
        // ★ 默认开启 OneShot —— 「延迟 N 秒后触发一次」就是 OneShot 的标准用法
        OneShot = true;
    }

    protected override void OnWindowEnter(Bullet b)
    {
        // 复用基类的 FireOnce(走 BulletPool.FireGroup 完整链路)
        FireOnce(b);
    }

    public override void ModifyCore(Bullet b, float dt)
    {
        // OneShot=true → 基类不会调用本方法,留空即可。
        // 若 OneShot=false(用户主动关掉)→ 退化为「每帧在母弹位置开一次火组」的疯狂模式 —— 故意外,不优化。
    }
}

/// <summary>
/// 持续型:modifier 窗口期(<see cref="BulletModifier.Delay"/> 之后,持续 <see cref="BulletModifier.Duration"/> 秒)
/// 内,每 <see cref="Interval"/> 秒触发一次 <see cref="FirePatternBulletModifier.Pattern"/>。
///
/// 默认值:<see cref="BulletModifier.OneShot"/> = false(每 Interval 都触发,直到 Duration 到期)。
///
/// 典型用法:
///   - Boss 散弹母弹挂本 modifier,Interval = 0.2,Duration = 3,MaxShots = 10
///     → Boss 弹飞行 3 秒内,每 0.2 秒在自身位置生成一份子 pattern,最多 10 次
///   - 玩家追踪母弹挂本 modifier,Interval = 0.05,Duration = 1
///     → 飞 1 秒内每帧 / 每 0.05 秒生成拖尾小弹(经典「弹尾」表现)
///   - 持续激光 / 光束弹挂本 modifier,Interval = 0.033(≈ 30Hz),Duration = 2
///     → 2 秒内连续发射,~60 段拼接成光束
///
/// 防刷屏:
///   - <see cref="MaxShots"/> 上限(<= 0 = 不限)
///   - <see cref="Interval"/> 必须 > 0;若用户填 0 → 每帧触发 → 故意外
/// </summary>
[Serializable, SRName("Modifier/Fire Pattern While Active")]
public class FireOnDurationBulletModifier : FirePatternBulletModifier
{
    [Tooltip("每隔多少秒触发一次 FirePattern。\n" +
             "  0.033 ≈ 30Hz(每帧,激光拼接);\n" +
             "  0.1   ≈ 每秒 10 发(连射);\n" +
             "  0.2   ≈ 每秒 5 发(机枪);\n" +
             "  0.5   ≈ 每秒 2 发(节拍)。")]
    [Min(0f)] public float Interval = 0.1f;

    [Tooltip("窗口期内最多发射几次。\n" +
             "  <=0 = 不限次数,直到 Duration 到期;\n" +
             "  > 0 = 触发 N 次后,本 modifier 即使还在窗口期也不再触发(避免长时间运行刷屏)。")]
    public int MaxShots = -1;

    public override void ModifyCore(Bullet b, float dt)
    {
        // 累加到 Interval 后触发一次,扣减余数继续累加(允许实际周期有 jitter)
        _accumulator += dt;
        // 防御:Interval=0 时(用户在 Inspector 填 0)不要进死循环,直接退化为「每帧触发一次」
        float step = Interval > 0f ? Interval : dt;
        while (_accumulator >= step)
        {
            _accumulator -= step;
            if (MaxShots > 0 && _shotsFired >= MaxShots)
            {
                _accumulator = 0f;
                return;
            }
            FireOnce(b);
            // 安全保险:FireOnce 失败时(Pattern 留空 / BulletPool 消失),不要死循环
            if (Pattern == null) return;
        }
    }
}