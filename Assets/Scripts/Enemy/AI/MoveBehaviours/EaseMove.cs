using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 朝某个方向缓动移动 Duration 秒。
    /// 速度按 Ease 曲线随剩余时间缩放 —— **子弹式「角度方向 + 时长」范式**,
    /// 不依赖场景里的具体世界坐标(对比旧版的 TargetPosition 字段)。
    ///
    /// 设计动机(对齐子弹系统,见 ARCHITECTURE §2.1 + §3.1):
    ///   - 子弹只持有 SteerAngle + Speed,轨迹由每帧 pos += dir*speed*dt 累加,
    ///     没有任何「走到世界某点即停」的概念 —— 这种范式让子弹资产跨场景完全可复用。
    ///   - EaseMove 走同一思路:BaseAngle 决定方向,Duration 决定停止时间,
    ///     敌人从入场位置沿方向缓动 N 秒,中间不与场景耦合。
    ///   - 角度约定与 BaseAngleFireExtension.BaseAngle 一致:
    ///     0=右, 90=上, 180=左, 270=下(度数)。
    ///
    /// 算法(每帧):
    ///   t_used   = clamp(_elapsed / Duration, 0, 1)     ← 已用时间比例
    ///   t_remain = 1 - t_used                            ← 剩余时间比例
    ///   speedFactor = Ease.Evaluate(Mode, t_remain)
    ///   step = PeakSpeed * speedFactor * dt
    ///   pos += dir * step
    ///   (到时 _elapsed >= Duration 后,后续帧不再移动)
    ///
    /// Ease 曲线语义(传入 t_remain,即「剩余时间比例」):
    ///   t_remain = 1 (远/刚起步) → Ease 返回 1 → 全速
    ///   t_remain = 0 (到时/收尾) → Ease 返回 0 → 停
    ///   Linear     = 全程匀速(Ease.Evaluate 对 Linear 特例返回常数 1)
    ///   InOutSine  = 起步慢中段快收尾慢(经典缓动,需 MinSpeedFactor > 0 避免起步 0 速)
    ///   OutBack    = 终点略微超出再弹回(经典 STG,需 MinSpeedFactor > 0)
    ///
    /// 与外层 MoveAction.Duration 的关系(参考 BezierMove.cs 的同类约定):
    ///   - 本类有独立的 Duration 字段(缓动 t=0..1 跨多久);
    ///   - 外层 MoveAction.Duration 决定"整条 Move 行为"持续多久;
    ///   - 当 _elapsed &lt; Duration  → 沿方向缓动;
    ///   - 当 _elapsed ≥ Duration   → 停在当前位置(后续帧不移动,直到外层切走);
    ///   - 若外层 Duration &lt; 本类 Duration → 缓动被截断。
    ///   实用建议:把外层 Duration 设为 ≥ 本类 Duration,让缓动走完整。
    ///
    /// 典型配法(Boss 从入场位置向「下」缓动 1.5 秒):
    ///   - BaseAngle = 270 (向下,STG 惯例)
    ///   - Duration  = 1.5
    ///   - PeakSpeed = 3
    ///   - Mode      = Linear (默认,稳妥)
    ///
    /// 不旋转 enemy.transform —— 敌人 sprite 保持原朝向(STG 视觉惯例,
    /// 与 HomingMove 一致)。
    /// </summary>
    [Serializable, SRName("Move/Ease")]
    public class EaseMove : MoveBehaviour
    {
        [Tooltip("移动方向(度)。0=右,90=上,180=左,270=下。\n" +
                 "与 BaseAngleFireExtension.BaseAngle 一致(详见 ARCHITECTURE §2.1)。\n" +
                 "敌人从入场位置沿这个方向缓动 Duration 秒,不依赖场景里的世界坐标。\n" +
                 "经典 STG Boss 入场:270(向下)。")]
        public float BaseAngle = 270f;

        [Tooltip("缓动持续时间(秒)。t = clamp(_elapsed / Duration, 0, 1),到时停。\n" +
                 "<=0 时兜底为 0.0001(避免除零,实际行为等价立即停)。\n" +
                 "建议把外层 MoveAction.Duration 设为 ≥ 此值,否则缓动会被外层截断。")]
        public float Duration = 1.5f;

        [Tooltip("峰值速度(单位/秒)。当 Ease 因子 = 1 时,每帧最多走这么多。\n" +
                 "起步和收尾会被 Ease 曲线拉慢。常用 1~5。")]
        public float PeakSpeed = 3f;

        [Tooltip("缓动曲线。决定速度如何随时间变化。\n" +
                 "Linear(默认) = 匀速,适合大多数场景;" +
                 "InOutSine / InOutQuad / InOutCubic = 起步慢中段快收尾慢,经典动画感(但需要 MinSpeedFactor > 0 避免卡住);" +
                 "OutBack = 终点略微超出再弹回(经典 STG,需要 MinSpeedFactor > 0)。")]
        public EaseMode Mode = EaseMode.Linear;

        [Tooltip("最小速度因子(0~1)。即使 Ease 曲线在起点/终点返回 0,实际速度也不会低于 PeakSpeed * 此值。\n" +
                 "0(默认) = 完全按 Ease 曲线(可能起步 0 速,看起来「卡住」);\n" +
                 "0.1 = 起步也有 10% 速度,保证能动。")]
        public float MinSpeedFactor = 0f;

        // 运行时状态(BehaviorFlow.Instantiate 会自动深拷,per-instance 安全)
        Vector2 _dir;      // 归一化后的移动方向(由 BaseAngle 算得)
        float   _elapsed;  // 进入行为后累计秒数

        // 本类是「增量型」:每帧在当前位置上累加位移,
        // dt=0 时 step=0,自然不会改 enemy.position —— 无需 SnapOnEnter,
        // OnEnter 后立即调一次 OnTick(dt=0) 等价不动,零瞬移自动满足。
        public override bool SnapOnEnter => false;

        public override void OnEnter(Transform enemy)
        {
            // BaseAngle(度) → 方向向量(弧度),
            // 与 BaseAngleFireExtension / Bullet.SteerAngle 共用同一约定。
            float rad = BaseAngle * Mathf.Deg2Rad;
            _dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            _elapsed = 0f;
        }

        public override void OnTick(Transform enemy, float dt)
        {
            _elapsed += dt;

            float duration = Mathf.Max(0.0001f, Duration);
            float tUsed = Mathf.Clamp01(_elapsed / duration);

            // t_remain = 剩余时间比例(1=起步, 0=到时)
            // Ease.Evaluate 期望「剩余比例」(1→0 的曲线输入)—— 与旧版「剩余距离比例」一致。
            float tRemain = 1f - tUsed;
            float speedFactor = Ease.Evaluate(Mode, tRemain);

            // 钳制最小速度因子(避免 Ease 曲线在 tRemain=0 时返回 0 导致敌人「卡住」)
            if (speedFactor < MinSpeedFactor) speedFactor = MinSpeedFactor;

            enemy.position += (Vector3)(_dir * PeakSpeed * speedFactor * dt);
        }
    }
}
