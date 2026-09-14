using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 「母弹 → 分裂弹」信息传递的多态模块 —— 抽象基类。
///
/// 设计动机:
///   <see cref="FirePatternBulletModifier"/> 本身在 <see cref="FirePatternBulletModifier.FireOnce"/>
///   那一刻向 BulletPool.FireGroup 传入 rotationRad(基线方向)。用户希望「父弹可以传递部分信息
///   改变子弹的基础逻辑」 —— 比如每次开火后累加一个旋转角偏移,实现「旋转喷射」「扇形铺开」「节拍扩散」等。
///
///   与 FireExtension / FireSound 对仗:
///     - FireExtension   → FirePattern 上的「角度管道」模块(每次发射算一次)
///     - FireSound       → FirePattern 上的「开火音并行触发器」(每次发射算一次)
///     - FirePatternBulletExtra → FirePatternBulletModifier 上的「母弹 → 分裂弹 信息传递」
///                              (每次 FireOnce 算一次)
///
///   命名空间感:
///     - 宿主 = <see cref="FirePatternBulletModifier"/> (modifier,挂在母弹上)
///     - 触发时机 = 宿主 FireOnce 内 BulletPool.FireGroup 之前(改 rotationRad + 可改 host 状态)
///     - 接收方 = 分裂弹(由 FireGroup → pattern.Fire → SpawnBullet → pool.Get 链路创建)
///
/// 接口设计(预留扩展):
///   - <see cref="GetRotationOffset"/>  → 本次 FireOnce 实际传给分裂弹的 rotationRad 增量(弧度)
///   - <see cref="OnFireTriggered"/>   → 钩子:每次 FireOnce 触发一次,子类用来更新自己的累加状态
///   - 未来若要传递更多信息(modifier覆盖 / 位置 override / 阵营 override),加新 virtual 方法即可
///
/// 字段约定(与 BulletModifier 一致):
///   - 值类型字段默认 Clone 即可
///   - 引用类型字段必须 override Clone 深拷
///   - per-instance 累加状态字段标 [NonSerialized],Clone 后自然归零(每次子弹复用重置)
/// </summary>
[Serializable]
public abstract class FirePatternBulletExtra
{
    /// <summary>
    /// 返回本次 FireOnce 要加到 rotationRad 上的额外旋转角(弧度)。
    /// 默认 = 0(不传递)。
    /// </summary>
    public virtual float GetRotationOffset() => 0f;

    /// <summary>
    /// 钩子:宿主 FireOnce 触发时调一次(在调 GetRotationOffset 之前)。
    /// 子类 override 此方法更新自己的累加状态(例:每次触发后 StepOffset 累加)。
    /// 参数 <paramref name="host"/> 是触发本次 FireOnce 的母弹 modifier 宿主(FirePatternBulletModifier 实例)。
    /// 默认空 —— 不传递任何信息的子类不需 override。
    /// </summary>
    public virtual void OnFireTriggered(FirePatternBulletModifier host) { }

    /// <summary>深拷。子类持有引用类型字段时 override。</summary>
    public virtual FirePatternBulletExtra Clone() => (FirePatternBulletExtra)MemberwiseClone();
}

/// <summary>
/// 「显式不传递」占位选项 —— 行为等价于 <see cref="FirePatternBulletExtra"/> 字段为 null。
///
/// 用途:用户已下拉选了类型但希望关掉时不必把字段再设为 null(避免拖回去找不到原选项)。
/// </summary>
[Serializable, SRName("Extra/None")]
public class NoFirePatternBulletExtra : FirePatternBulletExtra
{
}

/// <summary>
/// 「每次开火后旋转角累加偏移」信息传递 —— 母弹每次触发分裂后,「本次传给分裂弹的 rotationRad」
/// 会比上次多一个 <see cref="StepOffset"/> 度(可正可负)。
///
/// 字段语义:
///   - <see cref="BaseOffset"/> = 本批次第一次 FireOnce 时施加的恒定偏移(度)。
///     走 SR 多态下拉,可选 Fixed(精确值)/ Random Range(在区间内随机抽样一次,本颗母弹窗口期固定)
///   - <see cref="StepOffset"/> = 每次 FireOnce 触发后,下一次再叠加的偏移量(度)
///
/// 行为(rotationRad 累加公式,弧度):
///   第 N 次 FireOnce(N 从 1 开始):
///     offsetRad = (sampledBaseOffset + (N - 1) * StepOffset) * Deg2Rad
///   其中 sampledBaseOffset = BaseOffset.Sample(),在本颗母弹第一次 FireOnce 时抽样一次,之后保持。
///
///   例:BaseOffset = Fixed(0), StepOffset = 10
///     第 1 次:offset = 0    → 分裂弹按母弹原方向
///     第 2 次:offset = 10°  → 分裂弹偏 10°
///     第 3 次:offset = 20°  → 分裂弹偏 20°
///     ...
///
/// 典型用法:
///   - 「旋转喷射」母弹(典型 Boss 散弹):BaseOffset = Fixed(0), StepOffset = 5,
///     持续型 + Duration = 3 + Interval = 0.05 → 60 次开火,旋转 300°,形成旋转扩散环
///   - 「扇形铺开」OneShot 分裂:BaseOffset = Fixed(-30), StepOffset = 10
///     → 一次性按累加公式施加偏移(实际扇形需配合 Pattern.Count,本类只改 rotationRad)
///   - 「抖动扩散」:BaseOffset = Random Range(Min = -15, Max = 15), StepOffset = 0
///     → 每颗母弹第一次分裂角度在 ±15° 随机,但后续无累加
///
/// 注意:
///   - 累加值是「母弹连续触发的累加」,不是「单次 FireOnce 内多颗分裂弹的累加」
///     (单次 FireOnce 内 Ring/Arc 的多颗分裂方向由 FireExtensions 角度管道控制,与本类无关)
///   - 累加 / 抽样状态按 per-instance 隔离:母弹 A 触发 5 次后,母弹 B 从 0 开始(各自 Clone 独立)
///   - 抽样时机:每颗母弹**第一次 FireOnce 时**抽样一次,本颗母弹窗口期内保持不变
///
/// ⚠️ 旧 .asset 兼容说明:
///   v1:BaseOffset 是 `float`;v2 起改为 `BaseOffsetStrategy` SR 多态。
///   旧 .asset 里 `BaseOffset = X` 的 float 值会被 Unity 静默忽略(字段已删),新资产会按默认值
///   `FixedBaseOffsetStrategy { Value = 0f }` 反序列化。若需复现旧值,请手动下拉选 `Fixed` → 填 Value = X。
/// </summary>
[Serializable, SRName("Extra/Angle Offset")]
public class AngleOffsetFirePatternBulletExtra : FirePatternBulletExtra
{
    [Tooltip("本批次第一次 FireOnce 时施加的恒定偏移(走 SR 多态策略):\n" +
             "  Base Offset/Fixed        = 精确值(默认,旧行为);\n" +
             "  Base Offset/Random Range = 在 Min ~ Max 间随机抽一次(度),\n" +
             "                              本颗母弹窗口期内固定,跨母弹独立。\n" +
             "抽样时机:每颗母弹第一次 FireOnce 时抽一次,之后保持不变。")]
    [SerializeReference, SR]
    public BaseOffsetStrategy BaseOffset = new FixedBaseOffsetStrategy { Value = 0f };

    [Tooltip("每次 FireOnce 触发后,下一次再叠加的偏移量(度)。\n" +
             "  0   = 不累加(等价「无传递」);\n" +
             "  10  = 每发顺时针转 10°(右旋扩散);\n" +
             "  -10 = 每发逆时针转 10°(左旋扩散);\n" +
             "  5   = 60 发后旋转 300°,形成旋转扇形。")]
    public float StepOffset = 0f;

    [Tooltip("本批 BaseOffset 共享策略:\n" +
             "  Independent (默认) = 每颗母弹独立 Sample(模糊抖动、不规则分裂);\n" +
             "  Synchronized       = 本批 FireGroup 内所有母弹共用一个抽样值(精准扇形、节拍同步)。\n" +
             "机制:BulletPool.FireGroup 入口会调 OnBatchFire(),把抽样结果写进 _sampledBaseOffset;\n" +
             "      MemberwiseClone 时这个状态会被复制到本批每颗母弹 modifier 上,实现共享。\n" +
             "★ 注意 ★\n" +
             "  - 旧 .asset 反序列化本字段时走枚举默认值 Independent,行为与历史 100% 等价。\n" +
             "  - 仅当挂在 FirePatternBulletModifier.Extra 上时生效(否则 BulletPool 入口遍历不到)。")]
    public BatchSampleMode BatchSample = BatchSampleMode.Independent;

    /// <summary>本批 BaseOffset 抽样共享策略(详见 AngleOffsetFirePatternBulletExtra.BatchSample 字段注释)。</summary>
    public enum BatchSampleMode
    {
        /// <summary>每颗母弹独立 Sample(默认,旧行为)。</summary>
        Independent,
        /// <summary>本批 FireGroup 内所有母弹共用一个抽样值。</summary>
        Synchronized,
    }

    // per-instance 累加 / 抽样状态(每次母弹 Clone 时独立,不会跨子弹污染)
    [NonSerialized] int   _fireCount;            // 已触发的 FireOnce 次数
    [NonSerialized] float _sampledBaseOffset;     // 第一次 FireOnce(或 OnBatchFire)时抽到的 BaseOffset(度)
    [NonSerialized] bool  _baseOffsetSampled;     // 避免重复抽样
    // ★ Synchronized 模式 marker:在 BulletPool.FireGroup 入口的原始 modifier 上设为 true,
    //   MemberwiseClone 会复制这个 bool=true 到本批每颗母弹 modifier 实例上,后续 OnBatchFire
    //   看到 true 直接 return(每颗母弹 modifier 不再重复抽样)。
    [NonSerialized] bool  _batchSampleActivated;

    public override float GetRotationOffset()
    {
        // 公式:第 N 次(N 从 1 开始) → sampledBaseOffset + (N-1) * StepOffset
        return (_sampledBaseOffset + (_fireCount - 1) * StepOffset) * Mathf.Deg2Rad;
    }

    /// <summary>
    /// 本批共享抽样入口 —— 由 <see cref="BulletPool.FireGroup"/> 在每次开火组触发时调一次。
    /// 仅在 <see cref="BatchSampleMode.Synchronized"/> 模式生效:本批 FireGroup 共享同一个 BaseOffset 抽样值,
    /// 通过 MemberwiseClone 的字段复制语义传到本批每颗母弹的 AngleOffset 实例上(per-instance 状态都被复制)。
    /// Independent 模式直接 return,每颗母弹 AngleOffset 在 OnFireTriggered 时各自 Sample,行为与历史一致。
    /// </summary>
    public void OnBatchFire()
    {
        // 仅 Synchronized 模式触发本批共享抽样
        if (BatchSample != BatchSampleMode.Synchronized) return;
        // 原始 modifier 抽样一次后设 _batchSampleActivated=true,Clone 出来的实例继承这个 true,
        // 后续 OnBatchFire 看到 true 直接 return(防止「按 modifier 数组逐个调用」时重复抽样)。
        if (_batchSampleActivated) return;

        _sampledBaseOffset = BaseOffset?.Sample() ?? 0f;
        _baseOffsetSampled = true;          // 标记母弹 OnFireTriggered 不再抽样
        _fireCount = 0;                     // ★ 关键:原始 modifier 的 _fireCount 必须归 0,
                                            //   MemberwiseClone 会把这个 0 复制到本批每颗母弹 modifier 上,
                                            //   保证每颗母弹的累加序列从 1 开始算(否则历史 _fireCount 残留会导致
                                            //   GetRotationOffset 公式错位)。
        _batchSampleActivated = true;       // 标记本批已抽过
    }

    public override void OnFireTriggered(FirePatternBulletModifier host)
    {
        _fireCount++;

        // 第一次 FireOnce 时确定 BaseOffset,之后保持(per-instance 隔离,Clone 后下次重新抽)
        if (!_baseOffsetSampled)
        {
            // Synchronized 模式时,_baseOffsetSampled 已被 OnBatchFire() 在 BulletPool.FireGroup
            // 入口提前设为 true → 跳过此处抽样,直接用 _sampledBaseOffset。
            // Independent 模式时 → 这里走 Sample()(旧行为,每颗母弹独立抽)。
            _sampledBaseOffset = BaseOffset?.Sample() ?? 0f;
            _baseOffsetSampled = true;
        }
    }

    public override FirePatternBulletExtra Clone()
    {
        // ★ 关键:标 [NonSerialized] 的 per-instance 状态(_fireCount / _sampledBaseOffset /
        //   _baseOffsetSampled / _batchSampleActivated)随 MemberwiseClone 自动浅拷贝——
        //   这正是「Synchronized 模式本批共享」的关键:原始 modifier 在 BulletPool 入口抽样后,
        //   这些字段被 Clone 复制到本批每颗母弹 modifier 上,每颗母弹都继承同一个 _sampledBaseOffset。
        //   注意:_fireCount 在复制后本批每颗母弹都从原始值继续递增(而不是归零)——
        //   这不影响语义,因为每颗母弹的 AngleOffset 只关心「自己」累加序列,共享的是「起点」而非「计数」。
        var copy = (AngleOffsetFirePatternBulletExtra)MemberwiseClone();
        // BaseOffset 是 BaseOffsetStrategy 引用类型字段,必须深拷(用户可能继承出带状态的子类)。
        copy.BaseOffset = BaseOffset?.Clone();
        return copy;
    }
}

/// <summary>
/// 「本批次第一次 FireOnce 的恒定偏移值」采样策略 —— 抽象基类。
///
/// 设计动机:
///   <see cref="AngleOffsetFirePatternBulletExtra.BaseOffset"/> 之前是固定 float。
///   用户希望支持两种语义:精确值(Fixed,旧行为)与区间随机(Random Range)。
///   走 [SerializeReference] 多态下拉,与项目内所有策略类(SpawnFogConfig / FireExtension / SfxRule)
///   保持同一套路 —— Inspector 只显示当前模式字段,不显示无关字段。
///
/// 接口:
///   - <see cref="Sample"/> → 返回本次要施加的偏移值(度)。AngleOffsetFirePatternBulletExtra
///     在第一次 FireOnce 时调一次 Sample,之后缓存结果在窗口期内复用。
///
/// 字段约定(与 BulletModifier 一致):
///   - 纯值类型字段默认 MemberwiseClone 即可
///   - 持有引用类型字段的子类必须 override Clone 深拷
/// </summary>
[Serializable]
public abstract class BaseOffsetStrategy
{
    /// <summary>
    /// 返回本次要施加的 BaseOffset(度,正负皆可)。
    /// 宿主(AngleOffsetFirePatternBulletExtra)会在第一次 FireOnce 时调一次,之后缓存结果复用。
    /// </summary>
    public abstract float Sample();

    /// <summary>深拷。子类持有引用类型字段时 override;默认 MemberwiseClone 已覆盖纯值类型场景。</summary>
    public virtual BaseOffsetStrategy Clone() => (BaseOffsetStrategy)MemberwiseClone();
}

/// <summary>
/// 精确值 BaseOffset 策略 —— 直接返回 <see cref="Value"/>(度)。
///
/// 对齐旧版 v1 行为:`BaseOffset = X` ≡ `BaseOffset = FixedBaseOffsetStrategy { Value = X }`。
/// </summary>
[Serializable, SRName("Base Offset/Fixed")]
public class FixedBaseOffsetStrategy : BaseOffsetStrategy
{
    [Tooltip("精确偏移值(度)。\n" +
             "  0  = 分裂与母弹原方向一致(默认);\n" +
             "  90 = 分裂朝母弹左 90°;\n" +
             "  -45 = 分裂朝母弹右 45°。")]
    public float Value = 0f;

    public override float Sample() => Value;
}

/// <summary>
/// 区间随机 BaseOffset 策略 —— 在 <see cref="Min"/>(度) ~ <see cref="Max"/>(度)间均匀抽样一次。
///
/// 抽样时机:宿主在母弹第一次 FireOnce 时调一次 Sample,本颗母弹窗口期内固定不变。
/// 多颗母弹各自独立抽样 → 整批母弹集合看起来是「随机抖动」,但单颗母弹的分裂序列是「固定偏移 + 累加」。
///
/// 典型用法:
///   - 「抖动扩散」:Min = -15, Max = 15,StepOffset = 0 → 每颗母弹分裂角度在 ±15° 随机,但无累加
///   - 「随机起点扇形」:Min = -45, Max = 45,StepOffset = 10 → 起点随机落在扇形内,扇形内部累加展开
///
/// 注意:
///   - 若 Min > Max(用户填反),Sample 会直接返回 Min(不抛异常,容错优先)
///   - 若 Min == Max,Sample 直接返回该值(避免极小区间噪声)
/// </summary>
[Serializable, SRName("Base Offset/Random Range")]
public class RandomRangeBaseOffsetStrategy : BaseOffsetStrategy
{
    [Tooltip("随机抽样区间下限(度)。配合 Max 决定闭区间 [Min, Max]。\n" +
             "  对称区间(如 Min = -15, Max = 15)效果最直观(整批母弹看起来是「±15° 抖动」);\n" +
             "  非对称区间(如 Min = -30, Max = 5)可制造「向某一侧偏移的随机起点」。")]
    public float Min = -15f;

    [Tooltip("随机抽样区间上限(度)。配合 Min 决定闭区间 [Min, Max]。\n" +
             "  Min == Max 时 Sample 直接返回该值(无随机);\n" +
             "  Min >  Max 时 Sample 返回 Min(用户填反的容错)。")]
    public float Max = 15f;

    public override float Sample()
    {
        // 容错:用户填反 / 极小区间,直接返回确定值,避免 Random.Range 抛异常或噪声
        if (Min >= Max) return Min;

        // UnityEngine.Random.Range(float, float):闭区间 [min, max]
        return UnityEngine.Random.Range(Min, Max);
    }
}

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

    [Header("Parent To Child (母弹 → 分裂弹 的信息传递)")]
    [Tooltip("母弹 → 分裂弹 的「信息传递」多态模块(走 [SerializeReference, SR] 下拉)。\n" +
             "留空 = 不传递任何信息(默认,分裂弹按母弹当前方向开火,与早期 FireOnce 行为一致)。\n" +
             "拖一个 Extra 子类 → 母弹可在 FireOnce 触发那一刻把额外信息传给分裂弹(本批次的旋转角偏移等)。\n" +
             "扩展方法:新建 FirePatternBulletExtra 子类 + 加 [SRName(\"Extra/<名字>\")],Inspector 自动出现。\n" +
             "已内置:Extra/None(显式不传递占位)、Extra/Angle Offset(每次开火后旋转角累加偏移)。")]
    [SerializeReference, SR]
    public FirePatternBulletExtra Extra;

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
        // Extra 是 FirePatternBulletExtra,标了 [NonSerialized] 的 per-instance 累加字段
        // (如 AngleOffset 的 _fireCount)会随 MemberwiseClone 自动归零(每颗子弹从 0 开始累加),
        // 只需要把 Extra 自身深拷出来。
        copy.Extra = Extra?.Clone();
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