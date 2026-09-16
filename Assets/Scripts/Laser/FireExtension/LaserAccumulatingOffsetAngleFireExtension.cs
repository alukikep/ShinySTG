using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 激光累加型角度偏移扩展 —— 在基础偏移的基础上叠加"每次发射后累加"的偏移角,实现旋转激光/扇形铺开/抖动扩散等节奏感激光。
///
/// ★ 设计要点 ★
/// 与"覆盖型" (BaseAngleLaserFireExtension / PlayerAimLaserFireExtension)和"单次累加型" (OffsetAngleLaserFireExtension)的关系:
///   - BaseAngleLaserFireExtension    = "锚点",覆盖式,忽略上一步
///   - PlayerAimLaserFireExtension    = "瞄准",覆盖式,瞄不到透传
///   - OffsetAngleLaserFireExtension  = "单次累加",currentAngleRad + 固定 N°,**不随开火批次变化**
///   - 本类(AccumulatingOffsetAngle) = "批次累加",currentAngleRad + (BaseOffset + (fireCount-1) * StepOffset),
///     每次 FireGroup 触发后 fireCount 自增,累加角度随开火批次"扩散"
///
/// ★ BaseOffset SR 多态 ★
/// 基础偏移(本批次第一次开火施加的恒定值)走 [SerializeReference, SR] 下拉,可选:
///   - Base Offset/Fixed        = 精确值(默认,旧版 OffsetAngle 行为)
///   - Base Offset/Random Range = 在 Min ~ Max 间随机抽一次,本批次(整个 fireGroup)内固定,跨 batch 独立
/// 抽样时机:第一次 OnFireGroupTriggered(fireCount=1) 时 Sample 一次,之后保持到下次 OnFireGroupTriggered。
/// (激光每批只生成 1 条,BatchSampleMode 的 "本批次共享" 概念对激光无意义,
///  沿用 AccumulatingOffsetAngleFireExtension 的"每次开火重抽"默认行为即可。)
///
/// ★ 双维度累加 ★
/// 同一份 LaserPattern 上可同时挂两个累加型 OffsetAngle 模块,分别累加自己的 StepOffset:
///   [Base(270°), Accumulating Offset(0°, 5°), Accumulating Offset(0°, 10°)]
///   → 第 1 次开火:中线 + 0° + 0° = Base 原始方向
///   → 第 2 次开火:中线 + 5° + 10° = 偏 15°
///   → 第 3 次开火:中线 + 10° + 20° = 偏 30°
///   per-LaserFireExtension fireCount 字典(由 LaserPool 维护,key=元素 ref)保证各自计数独立。
///
/// ★ 与子弹 AccumulatingOffsetAngleFireExtension 的差异 ★
/// 激光版本**不带 OffsetPerBullet 字段**(激光每批只生成 1 条,没有 bulletIndex 概念)。
/// 因此本类 override 只 ProcessAngle,不 override ProcessAngleForBullet(激光版本来就没有这个方法)。
///
/// ★ 典型用法 ★
///   - "Boss 旋转激光":[Base(0°), Accumulating Offset(Fixed(0), 5°)]
///     Duration=3s,Interval=0.05s → 60 次开火,旋转 300°,形成 5°/帧 旋转激光。
///   - "抖动扩散激光":[Base(270°), Accumulating Offset(Random Range(-15, 15), 0°)]
///     每次开火基础偏移随机,无累加(等价旧版 OffsetAngle + 随机)。
///   - "瞄准 + 旋转激光":[PlayerAim, Accumulating Offset(Fixed(0), 3°)]
///     每次开火瞄向玩家 + 累计旋转 3°。
///
/// ★ 命名空间说明 ★
/// 本文件放在**全局命名空间**(与 LaserFireExtension.cs / LaserOffsetAngleFireExtension.cs / FireSound.cs 同款),
/// 不要放 namespace ShinySTG.Laser 里 —— 否则 LaserPattern.cs 看不到(踩坑记录见 CONTRIBUTING §4.7)。
/// </summary>
[Serializable, SRName("LaserFireExtension/Offset Angle Accumulating")]
public class AccumulatingOffsetAngleLaserFireExtension : LaserFireExtension
{
    [Tooltip("本批次第一次开火施加的恒定偏移(走 SR 多态策略):\n" +
             "  Base Offset/Fixed        = 精确值(默认,旧版 OffsetAngle 行为);\n" +
             "  Base Offset/Random Range = 在 Min ~ Max 间随机抽一次(度),本批次(整个 FireGroup)内固定。\n" +
             "抽样时机:每次 OnFireGroupTriggered(fireCount) 触发时 Sample 一次(每次开火重抽)。\n" +
             "★ 复用子弹版 BaseOffsetStrategy(全局命名空间),激光版本无需新建对应子类。")]
    [SerializeReference, SR]
    public BaseOffsetStrategy BaseOffset = new FixedBaseOffsetStrategy { Value = 0f };

    [Tooltip("每次开火(本批 FireGroup 触发)后,下一次再叠加的偏移量(度)。\n" +
             "  0   = 不累加(等价\"无批次累加\");\n" +
             "  5   = 每发顺时针转 5°;\n" +
             "  -5  = 每发逆时针转 5°;\n" +
             "  典型用法:Boss 旋转激光 StepOffset=5 + Interval=0.05 + Duration=3 → 60 发旋转 300°。")]
    public float StepOffset = 0f;

    // per-instance 累加 / 抽样状态([NonSerialized],Clone 时自然归零)
    [NonSerialized] int   _currentFireCount;       // 最新 OnFireGroupTriggered 写入的 fireCount(本批次序号,1 起)
    [NonSerialized] float _sampledBaseOffset;      // 本批次抽样的 BaseOffset(度)
    [NonSerialized] bool  _baseOffsetSampled;      // 是否已抽样(防止 ProcessAngle 比 OnFireGroupTriggered 先调用时多抽样)

    public override void OnFireGroupTriggered(int fireCount)
    {
        _currentFireCount = fireCount;
        // ★ 每次开火重抽 BaseOffset(配合大多数节奏激光"每次开火重新决定基础偏移"的需求)。
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
}