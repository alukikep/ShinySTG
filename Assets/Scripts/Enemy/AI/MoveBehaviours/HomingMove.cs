using System;
using SerializeReferenceEditor;
using UnityEngine;
// 注意:不要 using ShinySTG.Player —— namespace 与 Player 类同名,
// using 后 C# 编译器优先把 Player 解析为 namespace,导致 Player.Instance
// 找不到类成员。本类显式用全限定名 ShinySTG.Player.Player.Instance。
// 踩坑笔记见 CONTRIBUTING.md §4.7。

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 持续追踪玩家(自机狙)。敌人按 TurnRate 限速转向玩家方向,同时匀速前进。
    ///
    /// 锁定延迟(LockOnDelay):
    ///   - 0(默认): 进入即开始追踪
    ///   - >0: 前 N 秒保持初始朝向匀速直线,之后才开始追踪
    ///     (经典用法:敌人先冲一段,再锁定玩家 — 增强可读性)
    ///
    /// 算法:
    ///   每帧:
    ///     if elapsed >= LockOnDelay:
    ///       target = Player.Instance?.transform.position
    ///       if target != null:
    ///         targetAngle = atan2(target - pos)
    ///         deltaAngle = shortest_rotation(currentAngle, targetAngle)
    ///         currentAngle += clamp(deltaAngle, -TurnRate*dt, +TurnRate*dt)
    ///     pos += (cos currentAngle, sin currentAngle) * Speed * dt
    ///
    /// 不旋转 enemy.transform —— 敌人 sprite 保持原朝向(STG 视觉惯例)。
    ///
    /// 注意:
    ///   目标解析走 Player.Instance 单例,不调 FindGameObjectWithTag,避免 GC。
    ///   Player 没准备好(场景没挂)时,保持当前朝向匀速前进(兜底)。
    /// </summary>
    [Serializable, SRName("Move/Homing")]
    public class HomingMove : MoveBehaviour
    {
        [Tooltip("前进速度(单位/秒)。不受 TurnRate 影响(只决定「追得多快」,不决定「转向多快」)。\n" +
                 "经典自机狙:2~4。")]
        public float Speed = 3f;

        [Tooltip("转向角速度(度/秒)。正值 = 逆时针(也允许,虽然少见)。\n" +
                 "常用值:90(慢慢转) ~ 720(瞬间锁定);360 = 一秒转一圈。\n" +
                 "TurnRate 越小,转弯越「温柔」;越大,越像「自机狙」死死咬住。")]
        public float TurnRate = 360f;

        [Tooltip("锁定延迟(秒)。进入行为后,前 N 秒保持初始朝向匀速直线,N 秒后才开始追踪。\n" +
                 "0(默认) = 立即追踪;>0 = 先冲一段再追。")]
        public float LockOnDelay = 0f;

        [Tooltip("最大追踪时间(秒)。<=0 = 无限追踪(直到外层 MoveAction.Duration 结束)。\n" +
                 ">0 = 追 N 秒后停下(保持最后朝向匀速直线)。\n" +
                 "典型用法:N=2 让自机狙只锁 2 秒,之后变普通子弹 — 增加可玩性。")]
        public float MaxHomingTime = 0f;

        // 运行时状态
        float   _elapsed;       // 进入行为后累计秒数
        float   _currentAngle;  // 当前朝向(弧度;0=右,π/2=上,π=左,3π/2=下)
        bool    _lockedExpired; // MaxHomingTime 到期后变 true,停止追踪

        public override void OnEnter(Transform enemy)
        {
            _elapsed = 0f;
            _lockedExpired = false;
            // 初始朝向 = 敌人 transform 的当前朝向(Z 轴,弧度)
            _currentAngle = enemy.eulerAngles.z * Mathf.Deg2Rad;
        }

        public override void OnTick(Transform enemy, float dt)
        {
            _elapsed += dt;

            // 追踪逻辑(未过期 且 已过锁定延迟)
            if (!_lockedExpired && _elapsed >= LockOnDelay)
            {
                // 检查最大追踪时间
                if (MaxHomingTime > 0f && _elapsed - LockOnDelay >= MaxHomingTime)
                {
                    _lockedExpired = true; // 超时,后续保持最后朝向匀速前进
                }
                else
                {
                    // 拿玩家目标(Player 单例,见 Player.cs:33)。
                    // 全限定名 ShinySTG.Player.Player.Instance —— namespace 与类同名坑。
                    Transform player = ShinySTG.Player.Player.Instance != null
                        ? ShinySTG.Player.Player.Instance.transform
                        : null;
                    if (player != null)
                    {
                        Vector2 toTarget = (Vector2)player.position - (Vector2)enemy.position;
                        if (toTarget.sqrMagnitude > 0.0001f)
                        {
                            float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x);
                            float deltaAngle = ShortestRotation(_currentAngle, targetAngle);
                            float maxStep = TurnRate * dt * Mathf.Deg2Rad;
                            if (deltaAngle > maxStep) deltaAngle = maxStep;
                            else if (deltaAngle < -maxStep) deltaAngle = -maxStep;
                            _currentAngle += deltaAngle;
                        }
                    }
                }
            }

            // 前进(永远匀速,只改朝向)
            Vector2 dir = new Vector2(Mathf.Cos(_currentAngle), Mathf.Sin(_currentAngle));
            enemy.position += (Vector3)(dir * Speed * dt);
        }

        /// <summary>
        /// 返回 from → to 的最短旋转(弧度,带正负表示方向)。
        /// 结果范围 (-π, π]。
        /// </summary>
        static float ShortestRotation(float from, float to)
        {
            float diff = to - from;
            // 把 diff 归一化到 (-π, π]
            while (diff > Mathf.PI)  diff -= 2f * Mathf.PI;
            while (diff <= -Mathf.PI) diff += 2f * Mathf.PI;
            return diff;
        }
    }
}
