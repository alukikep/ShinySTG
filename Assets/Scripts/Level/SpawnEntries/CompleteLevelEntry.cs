using System;
using SerializeReferenceEditor;

namespace ShinySTG.Level.SpawnEntries
{
    /// <summary>请求成功通关；等待已启动的阻塞过程收尾，不等待普通敌人全灭。</summary>
    [Serializable, SRName("流程/关卡通关")]
    public sealed class CompleteLevelEntry : SpawnEntry
    {
        public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
            => runtime?.RequestCompletion();
    }
}
