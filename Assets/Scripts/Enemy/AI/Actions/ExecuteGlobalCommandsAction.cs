using System;
using SerializeReferenceEditor;
using ShinySTG.GameplayCommands;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    [Serializable, SRName("Action/Execute Global Commands")]
    public sealed class ExecuteGlobalCommandsAction : EnemyAction
    {
        [SerializeReference, SR]
        public GlobalCommand[] Commands;

        public override void OnEnter(Transform enemy)
        {
            GlobalCommandExecutor.Execute(Commands, new GlobalCommandContext(enemy));
        }
    }
}
