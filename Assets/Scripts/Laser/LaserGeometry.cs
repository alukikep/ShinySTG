using UnityEngine;

namespace ShinySTG.Laser
{
    /// <summary>
    /// 激光几何 + 碰撞判定数学。零分配、纯函数。
    /// 对齐参考材料 §15(直线点-线段距离) + §16(曲线分段)。
    ///
    /// ★ 距离用平方版本,避免 sqrt(性能敏感) ★
    /// </summary>
    public static class LaserGeometry
    {
        /// <summary>
        /// 直线激光端点(从 origin + angle + length 算)。
        /// </summary>
        public static Vector2 EndPoint(Vector2 origin, float angleRad, float length)
            => origin + new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * length;

        /// <summary>
        /// 点到线段的最短距离平方。线段退化(length² ≤ 1e-6)时退化为点到 origin 的距离平方。
        /// 对齐参考材料 §15 伪代码(用 Clamp01 把投影点限制在线段内)。
        /// </summary>
        public static float DistanceSqPointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float ab2 = ab.sqrMagnitude;
            if (ab2 <= 1e-6f)
            {
                Vector2 d = p - a;
                return d.sqrMagnitude;
            }
            Vector2 ap = p - a;
            float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / ab2);
            Vector2 closest = a + ab * t;
            return (p - closest).sqrMagnitude;
        }

        /// <summary>
        /// 曲线激光碰撞(节点数组):任一段命中即返回 true(参考材料 §16)。
        /// </summary>
        public static bool CheckCurvedHit(Vector2 playerPos, Vector2[] nodes, float laserRadius,
                                          float playerRadius)
        {
            if (nodes == null || nodes.Length < 2) return false;
            float r = playerRadius + laserRadius;
            float r2 = r * r;
            for (int i = 0; i < nodes.Length - 1; i++)
            {
                if (DistanceSqPointToSegment(playerPos, nodes[i], nodes[i + 1]) < r2)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 曲线激光擦弹(节点数组):任一段与膨胀圆相交即返回 true。
        /// 与 CheckCurvedHit 区别:擦弹环用更大的 r(padding 已加好)。
        /// </summary>
        public static bool CheckCurvedGraze(Vector2 playerPos, Vector2[] nodes, float maxRadius)
        {
            if (nodes == null || nodes.Length < 2) return false;
            float r2 = maxRadius * maxRadius;
            for (int i = 0; i < nodes.Length - 1; i++)
            {
                if (DistanceSqPointToSegment(playerPos, nodes[i], nodes[i + 1]) < r2)
                    return true;
            }
            return false;
        }
    }
}
