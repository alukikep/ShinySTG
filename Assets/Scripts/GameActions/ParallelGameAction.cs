using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.GameActions
{
    [Serializable, SRName("Game Action/Parallel")]
    public sealed class ParallelGameAction : GameAction
    {
        [SerializeReference, SR, Tooltip("同时启动所有子动作，全部结束后完成。")]
        public GameAction[] Children;
        public override GameActionRuntime CreateRuntime(GameActionContext context) => new Runtime(Children, context);
        sealed class Runtime : GameActionRuntime
        {
            readonly GameAction[] _children;
            readonly GameActionContext _context;
            readonly GameActionRunner _runner = new();
            GameActionHandle[] _handles;
            public Runtime(GameAction[] children, GameActionContext context) { _children = children; _context = context; }
            public override void Start()
            {
                _handles = new GameActionHandle[_children?.Length ?? 0];
                for (int i = 0; i < _handles.Length; i++)
                    _handles[i] = _runner.Play(new ActionSequence { Actions = new[] { _children[i] } }, _context);
            }
            public override bool IsComplete
            {
                get
                {
                    if (_handles == null) return false;
                    foreach (var handle in _handles)
                    {
                        if (handle.Failure != null) throw handle.Failure;
                        if (!handle.IsComplete) return false;
                    }
                    return true;
                }
            }
            public override void Tick(float dt) => _runner.Tick(dt);
            public override void Dispose() => _runner.Dispose();
        }
    }
}
