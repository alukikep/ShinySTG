using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 累加型角度偏移扩展 —— 在基础偏移的基础上叠加"每次发射后累加"的偏移角,实现旋转喷射/扇形铺开/抖动扩散等节奏感弹幕。
///
/// ★ 设计要点 ★
///   与"覆盖型" (BaseAngleFireExtension / PlayerAimFireExtension)和"单次累加型" (OffsetAngleFireExtension)的关系:
///     - BaseAngleFireExtension   = "锚点",覆盖式,忽略上一步
///     - PlayerAimFireExtension   = "瞄准",覆盖式,瞄不到透传
///     - OffsetAngleFireExtension = "单次累加",currentAngleRad + 固定 N°,**不随开火批次变化**
///     - 本类(AccumulatingOffsetAngle) = "批次累加",currentAngleRad + (BaseOffset + (fireCount-1) * StepOffset),
///       每次 FireGroup 触发后 fireCount 自增,累加角度随开火批次"扩散"
///
/// ★ BaseOffset SR 多态 ★
///   基础偏移(本批次第一次开火施加的恒定值)走 [SerializeReference, SR] 下拉,可选:
///     - Base Offset/Fixed        = 精确值(默认,旧版 OffsetAngle 行为)
///     - Base Offset/Random Range = 在 Min ~ Max 间随机抽一次,本批次(整个 fireGroup)内固定,跨 batch 独立
///   抽样时机:第一次 OnFireGroupTriggered(fireCount=1) 时 Sample 一次,之后保持到下次 OnFireGroupTriggered。
///   (如果 BatchSampleMode 设了 Synchronized/本批次共享,所有 8 颗 Ring 子弹共用抽样值,详见 FirePatternBulletModifier.OnBatchFire)
///   本类此处用最简单的 per-instance 隔离:每次 fireCount 变化重新抽样一次 —— 配合大多数节奏弹幕"每次开火重新决定基础偏移"的需求。
///
/// ★ 双维度累加 ★
///   同一份 FirePattern 上可同时挂两个累加型 OffsetAngle 模块,分别累加自己的 StepOffset:
///     [Base(270°), Accumulating Offset(0°, 5°), Accumulating Offset(0°, 10°)]
///     → 第 1 次开火:中线 + 0° + 0° = Base 原始方向
///     → 第 2 次开火:中线 + 5° + 10° = 偏 15°
///     → 第 3 次开火:中线 + 10° + 20° = 偏 30°
///     per-FireExtension fireCount 字典(由 BulletPool 维护,key=元素 ref)保证各自计数独立。
///
/// ★ PerBullet 累加(每发独立方向)★
///   override ProcessAngleForBullet:本类除了按 fireCount 累加外,还按 bulletIndex 再叠加 offsetPerBullet。
///   适用:Ring 8 颗每颗发散(螺旋环)、Arc 每颗再偏一点(节拍扩散)。
///   与 fireCount 累加正交叠加,公式:
///     totalOffset = (BaseOffset + (fireCount - 1) * StepOffset) + bulletIndex * OffsetPerBullet
///   关闭方式:OffsetPerBullet = 0(默认)。
///
/// ★ 典型用法 ★
///   - "Boss 散弹母弹旋转喷射":[Base(270°), Accumulating Offset(Fixed(0), 5°)]
///     Duration=3s,Interval=0.05s → 60 次开火,旋转 300°,形成旋转扩散环。
///   - "抖动扩散":[Base(270°), Accumulating Offset(Random Range(-15, 15), 0°)]
///     每次开火基础偏移随机,无累加(等价旧版 OffsetAngle + 随机)。
///   - "螺旋环":[Base(270°), Accumulating Offset(Fixed(0), 3°)] + OffsetPerBullet=22.5°
///     8 颗 Ring × 22.5° = 180° 扩散;每次开火再转 3° → 旋转螺旋环。
///
/// ★ 命名空间说明 ★
///   本文件放在**全局命名空间**(与 FireExtension.cs / OffsetAngleFireExtension.cs / FireSound.cs 同款),
///   不要放 namespace ShinySTG.Bullet 里 —— 否则 FirePattern.cs 看不到(踩坑记录见 CONTRIBUTING §4.7)。
/// </summary>
[Serializable, SRName("FireExtension/Offset Angle Accumulating")]
public class AccumulatingOffsetAngleFireExtension : FireExtension
{
    [Tooltip("本批次第一次开火施加的恒定偏移(走 SR 多态策略):\n" +
             "  Base Offset/Fixed        = 精确值(默认,旧版 OffsetAngle 行为);\n" +
             "  Base Offset/Random Range = 在 Min ~ Max 间随机抽一次(度),本批次(整个 FireGroup)内固定。\n" +
             "抽样时机:每次 OnFireGroupTriggered(fireCount) 触发时 Sample 一次(每次开火重抽)。\n" +
             "★ 如果希望\"本批次共享抽样\"(Ring 8 颗共用一个抽样值),用 Extra/Angle Offset 的 Synchronized 模式 ——\n" +
             "   本类此处用最简单的 per-instance 隔离:每次 fireCount 变化重抽一次。")]
    [SerializeReference, SR]
    public BaseOffsetStrategy BaseOffset = new FixedBaseOffsetStrategy { Value = 0f };

    [Tooltip("每次开火(本批 FireGroup 触发)后,下一次再叠加的偏移量(度)。\n" +
             "  0   = 不累加(等价\"无批次累加\");\n" +
             "  5   = 每发顺时针转 5°;\n" +
             "  -5  = 每发逆时针转 5°;\n" +
             "  典型用法:Boss 散弹母弹 StepOffset=5 + Interval=0.05 + Duration=3 → 60 发旋转 300°。")]
    public float StepOffset = 0f;

    [Tooltip("每颗子弹独立累加偏移(度),override ProcessAngleForBullet 时生效。\n" +
             "  0   = 关闭(默认),所有子弹共用同一中线(Ring/Arc 等分仍由各自 FirePattern 子类决定);\n" +
             "  22.5 = Ring 8 颗第 i 颗再额外偏 22.5°*i(配合 StepOffset 形成旋转螺旋环)。\n" +
             "与 fireCount 累加正交叠加:totalOffset = BaseOffset + (fireCount-1)*StepOffset + bulletIndex*OffsetPerBullet")]
    public float OffsetPerBullet = 0f;

    public override FireExtension Clone()
    {
        var copy = (AccumulatingOffsetAngleFireExtension)MemberwiseClone();
        copy.BaseOffset = BaseOffset?.Clone();
        copy._currentFireCount = 0;
        copy._sampledBaseOffset = 0f;
        copy._baseOffsetSampled = false;
        return copy;
    }

    // per-instance 累加 / 抽样状态([NonSerialized],Clone 时自然归零)
    [NonSerialized] int   _currentFireCount;       // 最新 OnFireGroupTriggered 写入的 fireCount(本批次序号,1 起)
    [NonSerialized] float _sampledBaseOffset;      // 本批次抽样的 BaseOffset(度)
    [NonSerialized] bool  _baseOffsetSampled;      // 是否已抽样(防止 ProcessAngle 比 OnFireGroupTriggered 先调用时多抽样)

    public override void OnFireGroupTriggered(int fireCount)
    {
        _currentFireCount = fireCount;
        // ★ 每次开火重抽 BaseOffset(配合大多数节奏弹幕"每次开火重新决定基础偏移"的需求)。
        //   若用户希望"本批次共享抽样"应改用 Extra/Angle Offset。
        _sampledBaseOffset = BaseOffset?.Sample() ?? 0f;
        _baseOffsetSampled = true;
    }

    public override float ProcessAngle(Vector2 from, float baseRotationRad, float currentAngleRad)
    {
        // 第一次 ProcessAngle 在 OnFireGroupTriggered 之前调用(理论不会发生,防御性兜底)
        if (!_baseOffsetSampled)
        {
            _sampledBaseOffset = BaseOffset?.Sample() ?? 0f;
            _baseOffsetSampled = true;
        }
        // 累加公式:BaseOffset + (fireCount - 1) * StepOffset
        // fireCount=1 时偏移 = BaseOffset(等价本批次原始偏移)
        float batchOffset = _sampledBaseOffset + Mathf.Max(0, _currentFireCount - 1) * StepOffset;
        return currentAngleRad + batchOffset * Mathf.Deg2Rad;
    }

    public override float ProcessAngleForBullet(Vector2 from, int bulletIndex, int totalCount,
                                                float baseRotationRad, float currentAngleRad)
    {
        // 第一次 ProcessAngleForBullet 在 OnFireGroupTriggered 之前调用(防御性兜底)
        if (!_baseOffsetSampled)
        {
            _sampledBaseOffset = BaseOffset?.Sample() ?? 0f;
            _baseOffsetSampled = true;
        }
        // 公式:BaseOffset + (fireCount - 1) * StepOffset + bulletIndex * OffsetPerBullet
        float batchOffset = _sampledBaseOffset + Mathf.Max(0, _currentFireCount - 1) * StepOffset;
        float perBulletOffset = bulletIndex * OffsetPerBullet;
        float totalOffset = batchOffset + perBulletOffset;
        return currentAngleRad + totalOffset * Mathf.Deg2Rad;
    }
}
