using System;
using SerializeReferenceEditor;
using ShinySTG.GameplayCommands;
using UnityEngine;

namespace ShinySTG.GameActions
{
    [Serializable, SRName("Game Action/Execute Commands")]
    public sealed class ExecuteCommandsAction : GameAction
    {
        [SerializeReference, SR, Tooltip("进入时执行的瞬时指令。持久无敌锁需要显式配对解除。")]
        public GlobalCommand[] Commands;
        public override GameActionRuntime CreateRuntime(GameActionContext context) => new Runtime(Commands, context);
        sealed class Runtime : GameActionRuntime
        {
            readonly GlobalCommand[] _commands;
            readonly GameActionContext _context;
            public Runtime(GlobalCommand[] commands, GameActionContext context) { _commands = commands; _context = context; }
            public override bool IsComplete => true;
            public override void Start() => GlobalCommandExecutor.Execute(_commands, new GlobalCommandContext(_context.Owner, _context.Boss));
        }
    }
}
