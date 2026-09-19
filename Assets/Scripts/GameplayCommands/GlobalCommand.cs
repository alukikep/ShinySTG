using System;
using ShinySTG.EnemyAI.Boss;
using UnityEngine;

namespace ShinySTG.GameplayCommands
{
    public enum CommandInvocation { Direct, PhaseCompleted, BossDeath, Stopped, PlayerDeath }
    /// <summary>全局游戏指令的执行上下文。指令按需读取场景服务，不持有跨场景状态。</summary>
    public readonly struct GlobalCommandContext
    {
        public Transform Owner { get; }
        public BossController BossController { get; }
        public CommandInvocation Invocation { get; }

        public GlobalCommandContext(Transform owner, BossController bossController = null,
            CommandInvocation invocation = CommandInvocation.Direct)
        {
            Invocation = invocation;
            Owner = owner;
            BossController = bossController != null
                ? bossController
                : owner != null ? owner.GetComponent<BossController>() : null;
        }
    }

    [Serializable]
    public abstract class GlobalCommand
    {
        public abstract void Execute(GlobalCommandContext context);
    }

    public static class GlobalCommandExecutor
    {
        public static void Execute(GlobalCommand[] commands, GlobalCommandContext context)
        {
            if (commands == null) return;
            for (int i = 0; i < commands.Length; i++)
                commands[i]?.Execute(context);
        }
    }
}
