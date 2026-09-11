using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 匀加速直线移动。沿 BaseAngle 方向移动,速度从 StartSpeed 线性递增到 MaxSpeed。
    /// 经典 STG 自爆冲锋怪(初速 0,持续加速)、Boss 退场加速。
    ///
    /// 位置公式(每帧):
    ///   speed = clamp(StartSpeed + Acceleration · elapsed, -∞, MaxSpeed)
    ///   pos += baseDir · speed · dt
    ///
    /// 约定:增量式 —— 每帧基于当前位置累加位移。
    /// (与 LinearMove 同款,见 LinearMove.cs 注释。)
    ///
    /// 角度约定与 BaseAngleFireExtension / EaseMove.BaseAngle 一致:
    ///   0=右, 90=上, 180=左, 270=下(度数,详见 ARCHITECTURE §2.1)。
    /// </summary>
    [Serializable, SRName("Move/Accelerate")]
    public class AccelerateMove : MoveBehaviour
    {
        [Tooltip("基础推进方向(度,进入行为时即锁定,之后不变)。\n" +
                 "0=右,90=上,180=左,270=下(与 BaseAngleFireExtension 一致,详见 ARCHITECTURE §2.1)。\n" +
                 "经典自爆冲锋:270(向下);Boss 退场:90(向上)。")]
        public float BaseAngle = 270f;

        [Tooltip("起始速度(单位/秒)。0 = 从静止开始加速;正值 = 一开始就已有速度。")]
        public float StartSpeed = 0f;

        [Tooltip("加速度(单位/秒²)。每过 1 秒,速度增加这么多。\n" +
                 "5 = 每秒加速 5 单位(温和);10 = 明显加速;20+ = 极速冲刺。")]
        public float Acceleration = 5f;

        [Tooltip("最大速度钳制(单位/秒)。速度不会超过此值。<0 = 不钳制(无限加速,慎用)。\n" +
                 "经典 STG 冲锋怪:StartSpeed=0, Acceleration=8, MaxSpeed=10。")]
        public float MaxSpeed = 10f;

        // 运行时状态
        Vector2 _baseDir;  // 归一化后的基础方向(由 BaseAngle 算得)
        float   _elapsed;  // 进入行为后累计秒数

        // 增量型:dt=0 时 step=0,无需 SnapOnEnter,零瞬移自动满足。
        public override bool SnapOnEnter => false;

        public override void OnEnter(Transform enemy)
        {
            // BaseAngle(度) → 方向向量(弧度),
            // 与 BaseAngleFireExtension / Bullet.SteerAngle 共用同一约定。
            float rad = BaseAngle * Mathf.Deg2Rad;
            _baseDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            _elapsed = 0f;
        }

        public override void OnTick(Transform enemy, float dt)
        {
            _elapsed += dt;

            float speed = StartSpeed + Acceleration * _elapsed;
            if (MaxSpeed >= 0f && speed > MaxSpeed) speed = MaxSpeed;

            enemy.position += (Vector3)(_baseDir * speed * dt);
        }
    }
}
