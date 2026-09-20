using System;
using SerializeReferenceEditor;
using UnityEngine;

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
