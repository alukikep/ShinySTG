using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 正弦摆动移动。沿 BaseAngle 方向匀速推进的同时,
    /// 在垂直方向(基础方向逆时针 90°)上做 sin 波摆动 ——
    /// 经典 STG 横向小怪 / 入场飘移效果。
    ///
    /// 位置公式(每帧):
    ///   pos = entryPos
    ///       + (cos θ, sin θ)                * Speed * _t
    ///       + (-sin θ, cos θ)               * Amplitude * sin(2π · Frequency · _t)
    ///   其中 θ = BaseAngle (弧度)
    ///
    /// 角度约定与 BaseAngleFireExtension / EaseMove.BaseAngle 一致:
    ///   0=右, 90=上, 180=左, 270=下(度数,详见 ARCHITECTURE §2.1)。
    ///   垂直方向约定为基础方向**逆时针 90°**(与旧版 perp(x,y)=(-y, x) 一致)。
    ///
    /// 注:摆动只影响位移,不会旋转敌人本体;若需要"边走边歪头",
    /// 后续可加 AimBehavior 字段或在 SineMove 内加一个 bool SwayRotation。
    /// </summary>
    [Serializable, SRName("Move/Sine")]
    public class SineMove : MoveBehaviour
    {
        [Tooltip("基础推进方向(度,进入行为时即锁定,之后不变)。\n" +
                 "0=右,90=上,180=左,270=下(与 BaseAngleFireExtension 一致,详见 ARCHITECTURE §2.1)。\n" +
                 "摆动方向 = BaseAngle 逆时针 90°(例如 BaseAngle=270 向下时,摆动方向为 0 向右)。\n" +
                 "经典入场飘移:270(向下推进 + 横向摆动)。")]
        public float BaseAngle = 270f;

        [Tooltip("基础方向上的推进速度(单位/秒)。")]
        public float Speed = 2f;

        [Tooltip("摆动幅度(单位)。0 = 退化为直线(等价 LinearMove)。")]
        public float Amplitude = 0.6f;

        [Tooltip("摆动频率(Hz,每秒完整 sin 周期数)。1 = 每秒摆一个来回。")]
        public float Frequency = 1.5f;

        [Tooltip("摆动起始相位(度)。0 = 从中线向 +垂直方向开始;90 = 从峰值开始。" +
                 "常用于让多个敌人错开摆位、避免完全重叠。")]
        public float PhaseOffsetDeg = 0f;

        // 运行时状态(BehaviorFlow.Instantiate 会自动深拷,per-instance 安全)
        Vector2 _baseDir;   // 归一化后的基础方向 = (cos θ, sin θ)
        Vector2 _perpDir;   // 垂直方向 = (-sin θ, cos θ),即基础方向逆时针 90°
        Vector2 _entryPos;  // 进入行为时的位置(以 enemy 当前 pos 为锚)
        float   _t;         // 进入行为后累计秒数

        // 绝对锚定公式:enemy.position = _entryPos + offset(_t=0 时 offset=0,所以第一帧位置 = entryPos)。
        // SnapOnEnter=false:dt=0 时 OnTick 也会把 enemy.position 写为 _entryPos,但这是 no-op
        // (enemy.position 已经是 entryPos),无瞬移也无副作用,所以无需强制 snap。
        public override bool SnapOnEnter => false;

        public override void OnEnter(Transform enemy)
        {
            // BaseAngle(度) → 方向向量(弧度),
            // 与 BaseAngleFireExtension / Bullet.SteerAngle 共用同一约定。
            float rad = BaseAngle * Mathf.Deg2Rad;
            _baseDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            // 垂直方向 = 基础方向逆时针 90°
            // (cos(θ+90°), sin(θ+90°)) = (-sin θ, cos θ) ——
            // 等价旧版 perp(x,y) = (-y, x) 公式,只是现在从 θ 一步算出。
            _perpDir = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));

            _entryPos = enemy.position;
            _t = 0f;
        }

        public override void OnTick(Transform enemy, float dt)
        {
            _t += dt;

            float phaseRad = PhaseOffsetDeg * Mathf.Deg2Rad;
            float wave     = Mathf.Sin(2f * Mathf.PI * Frequency * _t + phaseRad);

            Vector2 offset = _baseDir * (Speed * _t)
                           + _perpDir * (Amplitude * wave);

            enemy.position = (Vector3)(_entryPos + offset);
        }
    }
}
