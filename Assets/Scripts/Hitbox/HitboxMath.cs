using UnityEngine;

namespace ShinySTG.Hitbox
{
    /// <summary>
    /// Hitbox 数学碰撞。仅 AABB×AABB(项目统一用轴对齐矩形)。
    /// 纯 Rect 相交,O(1)、零分配、不依赖 Unity Physics2D。
    /// </summary>
    public static class HitboxMath
    {
        /// <summary>两 Rect 是否相交(含边界接触)。</summary>
        public static bool AABBOverlap(Rect a, Rect b)
        {
            // 显式写而非调 Rect.Overlaps,避免不同 Unity 版本在边界相等时行为不一致
            return a.xMin <= b.xMax && a.xMax >= b.xMin &&
                   a.yMin <= b.yMax && a.yMax >= b.yMin;
        }
    }
}