using System;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 一条敌人行为的时间轴节点。所有可配置的行为都继承自它。
    /// ShooterEnemy 会按顺序执行 Actions,每条持续 Duration 秒后自动进入下一条。
    /// </summary>
    [Serializable]
    public abstract class EnemyAction
    {
        [Tooltip("该行为持续多少秒后停止,自动进入下一条。< =0 表示只执行一帧(下一帧立即进入下一条)。")]
        public float Duration = 1f;

        /// <summary>进入该行为时调用一次(可做初始化/重置状态)。</summary>
        public virtual void OnEnter(Transform enemy) { }

        /// <summary>每帧调用,dt = Time.deltaTime。</summary>
        public virtual void OnTick(Transform enemy, float dt) { }

        /// <summary>该行为时间到,即将切换到下一条时调用一次(可清理状态)。</summary>
        public virtual void OnExit(Transform enemy) { }
    }
}
