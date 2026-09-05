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
    }
}
