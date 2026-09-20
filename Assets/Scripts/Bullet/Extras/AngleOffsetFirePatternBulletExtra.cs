using System;
using SerializeReferenceEditor;
using UnityEngine;

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

    [Tooltip("Independent：每颗母弹首次分裂时抽样。Synchronized：同一次根发射中同一 Extra 配置共享抽样，下一批重抽；覆盖默认和追加 Modifier。")]
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
    public override float GetRotationOffset()
    {
        // 公式:第 N 次(N 从 1 开始) → sampledBaseOffset + (N-1) * StepOffset
        return (_sampledBaseOffset + (_fireCount - 1) * StepOffset) * Mathf.Deg2Rad;
    }

    /// <summary>由池写入当前根发射批次的抽样，仅写运行实例。</summary>
    public void SetBatchSample(float value)
    {
        _sampledBaseOffset = value;
        _baseOffsetSampled = true;
        _fireCount = 0;
    }

    [Obsolete("批次抽样由 BulletPool 管理；仅对运行实例使用 SetBatchSample。")]
    public void OnBatchFire()
    {
        if (BatchSample == BatchSampleMode.Synchronized) SetBatchSample(BaseOffset?.Sample() ?? 0f);
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
        var copy = (AngleOffsetFirePatternBulletExtra)MemberwiseClone();
        copy.BaseOffset = BaseOffset?.Clone();
        copy._fireCount = 0;
        copy._sampledBaseOffset = 0f;
        copy._baseOffsetSampled = false;
        return copy;
    }
}
