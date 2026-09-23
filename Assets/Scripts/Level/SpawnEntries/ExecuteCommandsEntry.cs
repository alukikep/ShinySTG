using System;
using SerializeReferenceEditor;
using ShinySTG.GameplayCommands;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries
{
    /// <summary>在关卡时间轴指定时刻执行一组全局游戏指令。</summary>
    [Serializable, SRName("指令/Execute Commands")]
    public sealed class ExecuteCommandsEntry : SpawnEntry
    {
        [SerializeReference, SR, Tooltip("到达 TriggerTime 时按数组顺序执行一次。Owner 为 LevelController，掉落生成于其位置；持久无敌锁需另配移除指令。编辑器预览不执行。")]
        public GlobalCommand[] Commands;

        public override bool ShouldTrigger(bool alreadyFired) => !alreadyFired;

        public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
        {
            // 编辑器预览只模拟关卡表现，不应修改真实场景状态。
            if (!Application.isPlaying || runtime == null) return;
            var level = LevelController.Instance;
            if (level == null || !level.IsRunning || level.Runtime != runtime) return;
            GlobalCommandExecutor.Execute(Commands, new GlobalCommandContext(level.transform));
        }
    }
}
