using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 圆周移动。敌人从入场位置出发,画一个直径 = 2·Radius 的圆弧。
    /// 走完整 360° 回到入场位置,继续走则重复画圆。
    ///
    /// 位置公式(每帧):
    ///   圆心 C = 入场位置 + (0, Radius)              ← 固定,等于入场位置正上方 Radius 处
    ///   pos = C + R · (cos θ, sin θ)
    ///   θ += AngularSpeed · dt    (单位:度/秒,正值 = 逆时针)
    ///
    /// 零瞬移保证:
    ///   OnEnter 时 θ 初始化为 -90°,此时 cos(-90°)=0、sin(-90°)=-1,
    ///   → 第一帧 pos = C + R·(0, -1) = 入场位置 ✓
    ///
    /// 视觉上:
    ///   敌人从入场位置向右上方弧线出发 → 到达入场位置正上方 2R 处 →
    ///   向左上方弧线 → 回到入场位置。整条路径是完整的 360° 圆。
    /// </summary>
    [Serializable, SRName("Move/Circular")]
    public class CircularMove : MoveBehaviour
    {
        [Tooltip("圆周半径(单位)。<=0 时兜底为 0.01(避免退化为一个点)。常用 0.5~3。")]
        public float Radius = 1.5f;

        [Tooltip("角速度(度/秒)。正值 = 逆时针(屏幕上看也是逆时针,因为 Unity 2D Y 向上)。" +
                 "负值 = 顺时针。0 = 停在入场位置不动。常用范围:30~360。\n" +
                 "完整走一圈用时 = 360 / |AngularSpeed| 秒(AngularSpeed=180 → 2 秒一圈)。")]
        public float AngularSpeed = 90f;

        // 运行时状态
        Vector2 _center; // 圆心 = 入场位置 + (0, Radius)
        float   _phase;  // 累计弧度(从 -π/2 起步,保证首帧位置 = 入场位置)

        // 本类是"绝对锚定"型;零瞬移靠 OnEnter 的相位初始化保证,SnapOnEnter 兜底
        public override bool SnapOnEnter => true;

        public override void OnEnter(Transform enemy)
        {
            Vector2 entry = enemy.position;
            _center = entry + new Vector2(0f, Mathf.Max(0.01f, Radius));
            _phase = -Mathf.PI / 2f; // -90°,使 cos = 0、sin = -1,首帧 pos = entry
        }

        public override void OnTick(Transform enemy, float dt)
        {
            _phase += AngularSpeed * dt * Mathf.Deg2Rad;

            float r = Mathf.Max(0.01f, Radius);
            Vector2 pos = _center + new Vector2(Mathf.Cos(_phase), Mathf.Sin(_phase)) * r;
            enemy.position = pos;
        }
    }
}

