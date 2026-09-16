using UnityEngine;
using ShinySTG.Hitbox;

namespace ShinySTG.Laser
{
    /// <summary>
    /// 单向弧形激光:从同一 position 沿弧长内均分的 N 个方向各发一条独立单向激光。
    /// 对齐 Bullet.ArcFirePattern 的扇形语义:中线由 FireExtensions 解析,
    /// 每条角度在中线 ± ArcLength/2 范围内等分。
    ///
    /// ★ Count==1 退化 ★
    ///   Count==1 时只发 1 条沿中线的激光(等价 StraightLaserPattern),无 ArcLength 影响。
    ///
    /// ★ 与 BidirectionalStraightLaserPattern 的边界 ★
    ///   全部单向,无反向第二条;与 Bidirectional 的"✕ 形"不同,本类是扇形(◣ 形)。
    ///
    /// ★ 与 RingLaserPattern 的关系 ★
    ///   - RingLaserPattern:全 360° 等分,默认 16 条
    ///   - ArcLaserPattern:仅 ArcLengthDeg 范围内等分,默认 8 条
    ///   - 两者复用相同的 FireExtensions pipeline + AttachModifiers helper
    /// </summary>
    [CreateAssetMenu(menuName = "STG/Laser/Pattern/Arc")]
    public class ArcLaserPattern : LaserPattern
    {
        [Tooltip("激光条数(N)。1 = 退化为单条(沿中线)。")]
        public int Count = 8;

        [Tooltip("弧长(度)。整条扇形的角度跨度。")]
        public float ArcLengthDeg = 60f;

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

            // ─── FireExtensions 角度管道(与 RingLaserPattern 完全一致:一次解析,N 条共用) ───
            Vector2 from = position;
            float centerRad = LaserFireExtensionResolver.ResolvePipelineWithOffset(
                FireExtensions,
                ref from,
                angleRad,
                pool.GetFireExtensionFireCounts());

            float length = data.MaxLength * LengthMultiplier;
            LaserEntity first = null;

            if (Count == 1)
            {
                // Count==1 退化:只发 1 条沿中线的激光,等价 StraightLaserPattern。
                float rad = centerRad;
                Vector2 origin = from + Radius * new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                var single = pool.Get(data, origin, rad, length, ownerHitbox);
                if (single != null)
                {
                    AttachModifiers(single, ModifierPrefabs, extraModifiers);
                    first = single;
                }
                return first;
            }

            float arcRad = ArcLengthDeg * Mathf.Deg2Rad;
            float start = centerRad - arcRad * 0.5f;
            float step = arcRad / (Count - 1);
            for (int i = 0; i < Count; i++)
            {
                float rad = start + step * i;
                // Radius 是每条激光沿自身方向的外推距离(本地),与 Base.PositionOffset 正交叠加。
                Vector2 origin = from + Radius * new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                var laser = pool.Get(data, origin, rad, length, ownerHitbox);
                if (laser == null) continue;
                AttachModifiers(laser, ModifierPrefabs, extraModifiers);
                if (first == null) first = laser;
            }

            // ★ FireSounds 由 LaserPool.FireGroup 入口统一触发,这里不再调用 PlayFireSounds。
            return first;
        }

        /// <summary>
        /// 一次 Fire 实际产出 N 条激光,Boss 系统 ShotsFired 统计按 N 算。
        /// 对齐 CompositeFirePattern 的 GetFireCount() 语义。
        /// </summary>
        public override int GetFireCount() => Mathf.Max(0, Count);
    }
}