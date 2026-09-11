using System;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 缓动曲线函数库。供 EaseMove / PatrolMove 等需要速度/距离调制的 Move 复用。
    ///
    /// 入参 t 是归一化进度(0..1):
    ///   t = 0 → 返回 0(完全没动)
    ///   t = 1 → 返回 1(走完)
    ///
    /// 用于「速度因子」(EaseMove.PeakSpeed * Ease(t))时:
    ///   - Linear: 全程匀速
    ///   - InOutSine: 起步慢 → 中段快 → 收尾慢(典型缓动)
    ///   - InOutQuad / InOutCubic: 类似,但更陡
    ///   - OutBack: 收尾略微「超出」再弹回,经典 STG 视觉(敌人到位时的"咚"一下)
    /// </summary>
    public enum EaseMode
    {
        Linear,
        InOutSine,
        InOutQuad,
        InOutCubic,
        OutBack
    }

    public static class Ease
    {
        /// <summary>
        /// 计算 t 处的"速度因子"(0..1,OutBack 可 > 1)。
        /// t 通常是「剩余距离比例」(1 = 远,0 = 到)。
        ///
        /// Linear 特殊处理:返回常数 1(无缓动,匀速),不要 t,避免「速度随距离指数衰减永远到不了」。
        /// 其他曲线正常返回缓动后的因子。
        /// t 不在 [0,1] 区间时会被 Clamp。
        /// </summary>
        public static float Evaluate(EaseMode mode, float t)
        {
            t = Mathf.Clamp01(t);
            switch (mode)
            {
                case EaseMode.Linear:    return 1f; // 匀速,无缓动
                case EaseMode.InOutSine: return InOutSine(t);
                case EaseMode.InOutQuad: return InOutQuad(t);
                case EaseMode.InOutCubic: return InOutCubic(t);
                case EaseMode.OutBack:   return OutBack(t);
                default: return 1f;
            }
        }

        // 标准缓动公式(参考 easings.net,适配 Unity Mathf)

        static float InOutSine(float t)
        {
            return -0.5f * (Mathf.Cos(Mathf.PI * t) - 1f);
        }

        static float InOutQuad(float t)
        {
            return t < 0.5f
                ? 2f * t * t
                : -1f + (4f - 2f * t) * t;
        }

        static float InOutCubic(float t)
        {
            return t < 0.5f
                ? 4f * t * t * t
                : (t - 1f) * (2f * t - 2f) * (2f * t - 2f) + 1f;
        }

        // OutBack: 终点超出 1.0 一些再回到 1(视觉上"弹一下")
        // c1 = 1.70158, c3 = c1 + 1 = 2.70158(标准值,无需修改)
        static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
