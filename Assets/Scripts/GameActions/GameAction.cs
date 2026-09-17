using System;
using UnityEngine;
using ShinySTG.EnemyAI.Boss;

namespace ShinySTG.GameActions
{
    /// <summary>配置只读；每次 CreateRuntime 必须返回独立的执行实例。</summary>
    [Serializable]
    public abstract class GameAction
    {
        public abstract GameActionRuntime CreateRuntime(GameActionContext context);
    }

    public abstract class GameActionRuntime : IDisposable
    {
        public abstract bool IsComplete { get; }
        public virtual void Start() { }
        public virtual void Tick(float dt) { }
        public virtual void Dispose() { }
    }

    public readonly struct GameActionContext
    {
        public Transform Owner { get; }
        public BossController Boss { get; }
        public int PhaseIndex { get; }
        public Vector2 Position { get; }
        public ShinySTG.Level.LevelRuntime LevelRuntime { get; }
        public GameActionContext(Transform owner, BossController boss = null, int phaseIndex = -1,
            ShinySTG.Level.LevelRuntime levelRuntime = null)
        {
            Owner = owner;
            Boss = boss;
            PhaseIndex = phaseIndex;
            Position = owner != null ? (Vector2)owner.position : Vector2.zero;
            LevelRuntime = levelRuntime;
        }
    }
}
