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
///   - _accumulator / _shotsFired 标 [NonSerialized],每颗 Clone 时显式归零
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

    [Header("Parent To Child (母弹 → 分裂弹 的信息传递)")]
    [Tooltip("母弹 → 分裂弹 的「信息传递」多态模块(走 [SerializeReference, SR] 下拉)。\n" +
             "留空 = 不传递任何信息(默认,分裂弹按母弹当前方向开火,与早期 FireOnce 行为一致)。\n" +
             "拖一个 Extra 子类 → 母弹可在 FireOnce 触发那一刻把额外信息传给分裂弹(本批次的旋转角偏移等)。\n" +
             "扩展方法:新建 FirePatternBulletExtra 子类 + 加 [SRName(\"Extra/<名字>\")],Inspector 自动出现。\n" +
             "已内置:Extra/None(显式不传递占位)、Extra/Angle Offset(每次开火后旋转角累加偏移)。")]
    [SerializeReference, SR]
    public FirePatternBulletExtra Extra;

    // ─── per-instance 状态(Clone / ResetWindow 显式重置) ───
    [NonSerialized] protected float _accumulator;   // 持续型触发的间隔累加器
    [NonSerialized] FirePatternRuntimeState _runtimeState;
    [NonSerialized] protected int   _shotsFired;    // 已发射次数(供 MaxShots / 调试)

    protected override void OnResetWindow()
    {
        _accumulator = 0f;
        _shotsFired = 0;
        _runtimeState = null;
    }

    protected override void OnDetach(Bullet bullet) => _runtimeState = null;

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
        if (b == null) return;
        if (Pattern == null || BulletPool.Instance == null) return;

        // ownerHitbox 取值:
        //   - InheritOwnerTeam = true → b.Hitbox(母弹阵营)
        //   - false              → null  (Neutral)
        // 注:b.Hitbox 是 HitboxComponent 引用(由 [RequireComponent] 保证非 null),
        //   不是 Unity API 调用,不会被 Destroy 失效。
        var owner = InheritOwnerTeam ? b.Hitbox : null;

        // ─── 母弹 → 分裂弹 信息传递钩子 ───
        // 1. 先调 OnFireTriggered:让 Extra 更新自己的累加状态(AngleOffset 的 _fireCount++)
        //   必须在 GetRotationOffset 之前调,否则累加公式「第 N 次」算错
        //   传入 this(modifier 自身 host),让 Extra 子类可读 host 的内部状态(目前主要给 AngleOffset 用)
        // 2. 再调 GetRotationOffset:拿到本次 rotationRad 增量
        //   null-safe:Extra 为 null 时默认不传递(与早期 FireOnce 行为 100% 等价)
        float extraOffsetRad = 0f;
        if (Extra != null)
        {
            Extra.OnFireTriggered(this);
            extraOffsetRad = Extra.GetRotationOffset();
        }

        float rotationRad = ComputeCenterAngle(b) + extraOffsetRad;

        // 走完整链路:PlayFireSounds → pattern.Fire → SpawnBullet → pool.Get
        //   - FireExtensions 角度管道由 Pattern 自己处理
        //   - Pattern.ModifierPrefabs + ExtraModifiers 自动挂载
        //   - Pattern.SpawnFog 透传
        //   - rotationRad = 母弹朝向 + Extra 增量
        BulletPool.Instance.FireGroup(
            Pattern,
            b.Position,
            rotationRad,
            ownerHitbox: owner,
            extraModifiers: ExtraModifiers,
            state: _runtimeState ??= new FirePatternRuntimeState());

        _shotsFired++;
    }

    // ─── Clone 深拷 ───
    // ExtraModifiers 是 BulletModifier[],默认 MemberwiseClone 会共享同一数组
    // → 多颗子弹共享同一组 modifier 引用(Clone 出来的 modifier 本身已被 Clone 深拷,
    //   但数组本身被共享会污染「增删元素」语义)。STG 高弹量场景下需要明确深拷。
    public override BulletModifier Clone()
    {
        var copy = (FirePatternBulletModifier)base.Clone();
        copy._accumulator = 0f;
        copy._shotsFired = 0;
        copy._runtimeState = null;
        if (ExtraModifiers != null)
        {
            copy.ExtraModifiers = new BulletModifier[ExtraModifiers.Length];
            for (int i = 0; i < ExtraModifiers.Length; i++)
            {
                copy.ExtraModifiers[i] = ExtraModifiers[i]?.Clone();
            }
        }
        // Extra 自己决定如何复制批次抽样；本 modifier 的计时和次数已显式归零。
        copy.Extra = Extra?.Clone();
        return copy;
    }
}
