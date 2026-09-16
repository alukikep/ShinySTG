using UnityEngine;
using ShinySTG.Hitbox;

namespace ShinySTG.Laser
{
    /// <summary>
    /// 单向直线激光。<see cref="LaserEntity.Position"/> = 激光起点,
    /// 端点 = Position + (cos Angle, sin Angle) * CurrentLength。
    /// 渲染端:Body 从起点沿 Angle 方向延伸(PR1 Renderer 修正后视觉与几何一致)。
    /// 碰撞端:DistanceSqPointToSegment(playerPos, Position, EndPoint) 同样从起点到端点。
    ///
    /// PR1 阶段:用 Data.MaxLength 作默认长度;LengthMultiplier 提供简单倍率覆盖。
    /// </summary>
    [CreateAssetMenu(menuName = "STG/Laser/Pattern/Straight")]
    public class StraightLaserPattern : LaserPattern
    {
        [Tooltip("激光长度倍率(相对 LaserData.MaxLength)。1 = 整长。")]
        public float LengthMultiplier = 1f;

        public override LaserEntity Fire(Vector2 position, float angleRad, LaserPool pool,
                                         HitboxComponent ownerHitbox,
                                         LaserModifier[] extraModifiers = null)
        {
            var data = Data;
            if (data == null || pool == null) return null;

            // ─── FireExtensions 角度管道(对齐 RingFirePattern.Fire 走 BulletPool.GetFireExtensionFireCounts 的套路) ───
            //   - 本地变量 from:被 Resolver 内部通过 ref 修改(叠加 Base.PositionOffset),不会污染调用方 position
            //   - 用字典版(per-LaserFireExtension fireCount):支持多个累加型模块独立计数
            Vector2 from = position;
            float resolvedAngle = LaserFireExtensionResolver.ResolvePipelineWithOffset(
                FireExtensions,
                ref from,
                angleRad,
                pool.GetFireExtensionFireCounts());

            float length = data.MaxLength * LengthMultiplier;
            var laser = pool.Get(data, from, resolvedAngle, length, ownerHitbox);
            if (laser == null) return null;

            // 挂 modifier(ModifierPrefabs + extraModifiers,与 BulletPool.attach 同思路;复用基类 helper)
            AttachModifiers(laser, ModifierPrefabs, extraModifiers);
            // ★ FireSounds 由 LaserPool.FireGroup 入口统一触发(对齐 BulletPool.FireGroup 中心化触发),
            //   这里不再调用 PlayFireSounds(避免重复触发)。
            return laser;
        }
    }
}
