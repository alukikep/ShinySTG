using System;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 移动行为模块的抽象基类。MoveAction 持有一个 MoveBehaviour,
    /// 通过 [SerializeReference] 在 Inspector 里可自由切换/扩展具体移动逻辑。
    /// </summary>
    [Serializable]
    public abstract class MoveBehaviour
    {
        /// <summary>进入时调用(可记录初始位置 / 角度 / 速度 等)。</summary>
        public virtual void OnEnter(Transform enemy) { }

        /// <summary>每帧调用,实现具体位移。</summary>
        public abstract void OnTick(Transform enemy, float dt);

        /// <summary>退出时调用(可选清理)。</summary>
        public virtual void OnExit(Transform enemy) { }

        /// <summary>
        /// 返回 true 表示本 Move 是"绝对锚定"型(进入行为时一次性把 enemy.position
        /// 改到新轨迹的起点,如 Circular / Sine / Bezier)。
        /// 返回 false 表示增量型(基于当前位置累加位移,如 Linear)。
        ///
        /// MoveAction.OnEnter 会在 OnEnter 之后立即以 dt=0 调一次 OnTick,
        /// 把 enemy.position 同步到"新轨迹第一帧的位置",避免切到下一条 Action
        /// 时敌人从旧位置瞬移到新轨迹的远处。
        ///
        /// 增量型 Move 不需要此机制 —— 它们的第一帧位移 = dir * speed * 0 = 0,
        /// 同步不会改变 enemy.position。
        /// </summary>
        public virtual bool SnapOnEnter => false;
    }
}
