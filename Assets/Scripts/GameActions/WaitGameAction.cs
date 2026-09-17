using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.GameActions
{
    [Serializable, SRName("Game Action/Wait")]
    public sealed class WaitGameAction : GameAction
    {
        [Min(0f), Tooltip("等待秒数，使用宿主传入的时间步长。")]
        public float Seconds = 1f;
        public override GameActionRuntime CreateRuntime(GameActionContext context) => new Runtime(Seconds);
        sealed class Runtime : GameActionRuntime
        {
            float _remaining;
            public Runtime(float seconds) { _remaining = Mathf.Max(0f, seconds); }
            public override bool IsComplete => _remaining <= 0f;
            public override void Tick(float dt) { _remaining -= dt; }
        }
    }
}
