using UnityEngine;
using ShinySTG.Hitbox;

namespace ShinySTG.Laser
{
    /// <summary>
    /// 单向环形激光:从同一 position 沿 N 个等分方向各发一条独立单向激光,
    /// 每条是独立 LaserEntity(各自走完五段状态机)。
    /// 对齐 Bullet.RingFirePattern 的环形语义,但每条激光独占生命周期 / 视觉 / 阵营 / 状态机。
    ///
    /// ★ 与 BidirectionalStraightLaserPattern 的边界 ★
    ///   - BidirectionalStraightLaserPattern:同点反向 2 条(✕ 形)
    ///   - RingLaserPattern:同点扇出 N 条(N≥1,默认 16),全部沿 ResolvePipeline 中心方向等分,
    ///     每条都是单向直线,不用反向第二条。
    ///
    /// ★ 与 StraightLaserPattern 的关系 ★
    ///   - StraightLaserPattern 是"生成 1 条"的最小实现;
    ///   - RingLaserPattern 内部循环调 pool.Get(...) N 次,每次逻辑 = 单向直线。
    ///   - 复用基类 AttachModifiers /复用 FireExtensions 中心方向解析(与 RingFirePattern 同思路)。
    /// </summary>
    [CreateAssetMenu(menuName = "STG/Laser/Pattern/Ring")]
    public class RingLaserPattern : LaserPattern
    {
        [Tooltip("激光条数(N)。1 = 退化为单条。")]
        public int Count = 16;

        [Tooltip("起始偏移半径:每条激光从中心点沿自身方向外推的距离。0 = 同点齐发。")]
        public float Radius = 0f;

        [Tooltip("激光长度倍率(相对 LaserData.MaxLength)。1 = 整长。")]
        public float LengthMultiplier = 1f;

        public override LaserEntity Fire(Vector2 position, float angleRad, LaserPool pool,
                                         HitboxComponent ownerHitbox,
                                         LaserModifier[] extraModifiers = null)
        {
            var data = Data;
            if (data == null || pool == null) return null;
            if (Count < 1) return null;

            // ─── FireExtensions 角度管道(对齐 RingFirePattern / StraightLaserPattern) ───
            //   - 本地变量 from:被 Resolver 内部通过 ref 修改(叠加 Base.PositionOffset),不会污染调用方 position
            //   - 用字典版(per-LaserFireExtension fireCount):支持多个累加型模块独立计数
            Vector2 from = position;
            float centerRad = LaserFireExtensionResolver.ResolvePipelineWithOffset(
                FireExtensions,
                ref from,
                angleRad,
                pool.GetFireExtensionFireCounts());

            float length = data.MaxLength * LengthMultiplier;
            // 2π / N —— 每条激光均分 360°;N 条共用一次 FireExtensions 解析 → fireCount 仅 +1(对齐 Bullet 端 RingFirePattern)
            float step = (Mathf.PI * 2f) / Count;

            LaserEntity first = null;
            for (int i = 0; i < Count; i++)
            {
                float rad = centerRad + step * i;
                // Radius 是每条激光沿自身方向的外推距离(本地),与 Base.PositionOffset 正交叠加。
                Vector2 origin = from + Radius * new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                var laser = pool.Get(data, origin, rad, length, ownerHitbox);
                if (laser == null) continue;
                AttachModifiers(laser, ModifierPrefabs, extraModifiers);
                if (first == null) first = laser;
            }

            // ★ FireSounds 由 LaserPool.FireGroup 入口统一触发(对齐 StraightLaserPattern),
            //   这里不再调用 PlayFireSounds(避免重复触发)。
            return first;
        }

        /// <summary>
        /// 一次 Fire 实际产出 N 条激光,Boss 系统 ShotsFired 统计按 N 算。
        /// 对齐 CompositeFirePattern 的 GetFireCount() 语义。
        /// </summary>
        public override int GetFireCount() => Mathf.Max(0, Count);
    }
}