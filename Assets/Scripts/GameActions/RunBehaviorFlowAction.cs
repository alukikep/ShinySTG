using System;
using SerializeReferenceEditor;
using ShinySTG.EnemyAI;
using UnityEngine;

namespace ShinySTG.GameActions
{
    [Serializable, SRName("Game Action/Run Behavior Flow")]
    public sealed class RunBehaviorFlowAction : GameAction
    {
        [Tooltip("运行至行为流完成；循环行为流需要外部取消。")]
        public BehaviorFlow Flow;
        public override GameActionRuntime CreateRuntime(GameActionContext context) => new Runtime(Flow, context.Owner);
        sealed class Runtime : GameActionRuntime
        {
            readonly Transform _owner;
            readonly BehaviorFlowRuntime _flow;
            public Runtime(BehaviorFlow flow, Transform owner) { _owner = owner; _flow = flow != null ? flow.Instantiate() : null; }
            public override bool IsComplete => _owner == null || _flow == null || _flow.IsComplete;
            public override void Tick(float dt) { if (_owner != null) _flow?.Tick(_owner, dt); }
            public override void Dispose() => _flow?.Dispose(_owner);
        }
    }
}
