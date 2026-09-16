using UnityEngine;
using ShinySTG.Hitbox;

namespace ShinySTG.Laser
{
    /// <summary>
    /// 双向直线激光:从 position 出发,沿 angleRad 和 angleRad+π 同时延伸。
    /// 两条激光是两条独立 LaserEntity,各自走完五段状态机,阵营 / 生命周期 / modifier 全部独立。
    ///
    /// 与 StraightLaserPattern 的区别:
    ///   - StraightLaserPattern:position = 激光起点,Body 单向延伸。
    ///   - BidirectionalStraightLaserPattern:position = 两条激光的共享起点,
    ///     各向相反方向延伸。视觉上是 ✕ 形 / 十字交叉。
    ///
    /// 注意:两条激光共享同一个 position(由本 pattern 决定),不做位置偏移。
    /// 若需要肩炮等"两个不同发射点"的双向,请在 Action 层用两个 FireLaserAction 各偏一点。
    /// </summary>
    [CreateAssetMenu(menuName = "STG/Laser/Pattern/Bidirectional Straight")]
    public class BidirectionalStraightLaserPattern : LaserPattern
    {
        [Tooltip("激光长度倍率(相对 LaserData.MaxLength)。1 = 整长。")]
        public float LengthMultiplier = 1f;

        public override LaserEntity Fire(Vector2 position, float angleRad, LaserPool pool,
                                         HitboxComponent ownerHitbox,
                                         LaserModifier[] extraModifiers = null)
        {
            var data = Data;
            if (data == null || pool == null) return null;

            // ─── FireExtensions 角度管道(与 StraightLaserPattern 完全一致) ───
            Vector2 from = position;
            float resolvedAngle = LaserFireExtensionResolver.ResolvePipelineWithOffset(
                FireExtensions,
                ref from,
                angleRad,
                pool.GetFireExtensionFireCounts());

            float length = data.MaxLength * LengthMultiplier;

            // 正向(从起点沿 resolvedAngle 延伸)
            var forward = pool.Get(data, from, resolvedAngle, length, ownerHitbox);
            if (forward == null) return null;
            AttachModifiers(forward, ModifierPrefabs, extraModifiers);

            // 反向(同一起点,角度 +π,长度相同 → 两条独立 LaserEntity 各自走完五段状态机)
            var backward = pool.Get(data, from, resolvedAngle + Mathf.PI, length, ownerHitbox);
            if (backward == null) return forward; // 至少正向成功,返正向

            AttachModifiers(backward, ModifierPrefabs, extraModifiers);
            return forward; // 主返回正向;反向也已被 pool 接管,无需再返
        }

        /// <summary>
        /// 一次 Fire 实际产出 2 条激光,Boss 系统 ShotsFired 统计按 2 算。
        /// 对齐 Bullet 端 CompositeFirePattern 的 GetFireCount() 语义。
        /// </summary>
        public override int GetFireCount() => 2;
    }
}