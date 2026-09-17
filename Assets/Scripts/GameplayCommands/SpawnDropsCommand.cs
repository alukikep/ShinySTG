using System;
using SerializeReferenceEditor;
using UnityEngine;
using ShinySTG.Items;

namespace ShinySTG.GameplayCommands
{
    [Serializable, SRName("Command/Spawn Drops")]
    public sealed class SpawnDropsCommand : GlobalCommand
    {
        [Tooltip("道具种类、数量和散布配置。")]
        public DropProfile Profile;
        [Tooltip("正常阶段结束时执行。")]
        public bool OnPhaseCompleted = true;
        [Tooltip("Boss 死亡退出当前阶段时执行。")]
        public bool OnBossDeath = true;

        public override void Execute(GlobalCommandContext context)
        {
            if (context.Owner == null || context.Invocation == CommandInvocation.Stopped) return;
            if (context.Invocation == CommandInvocation.PhaseCompleted && !OnPhaseCompleted) return;
            if (context.Invocation == CommandInvocation.BossDeath && !OnBossDeath) return;
            ItemDropService.Spawn(Profile, context.Owner.position);
        }
    }
}
