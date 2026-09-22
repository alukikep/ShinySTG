using System;
using SerializeReferenceEditor;
using ShinySTG.GameActions;
using ShinySTG.Presentation.SpellDeclaration;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries
{
    [Serializable, SRName("演出/播放标题")]
    public sealed class PlayPresentationEntry : SpawnEntry
    {
        [Tooltip("要播放的标题/宣言图片配置。")]
        public SpellDeclarationDefinition Presentation;
        [Tooltip("开启后暂停关卡时间轴，直到标题演出结束。")]
        public bool BlockTimeline = true;

        public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
        {
            if (Presentation == null) { Debug.LogWarning("[Level] PlayPresentationEntry 缺少 Presentation，已跳过。", def); return; }
            var process = new PresentationProcess(Presentation, BlockTimeline);
            runtime.AddTimelineProcess(process);
        }

        sealed class PresentationProcess : ILevelTimelineProcess
        {
            readonly SpellDeclarationDefinition _definition;
            readonly bool _blocks;
            SpellDeclarationHandle _handle;
            public PresentationProcess(SpellDeclarationDefinition definition, bool blocks) { _definition = definition; _blocks = blocks; }
            public bool BlocksTimeline => _blocks;
            public bool IsComplete => _handle != null && _handle.IsComplete;
            public void Tick(float dt)
            {
                if (_handle != null) return;
                SpellDeclarationService service = null;
                foreach (var candidate in UnityEngine.Object.FindObjectsOfType<SpellDeclarationService>())
                    if (candidate.isActiveAndEnabled) { if (service != null) throw new InvalidOperationException("[Presentation] 存在多个启用的宣言服务。"); service = candidate; }
                if (service == null) throw new InvalidOperationException("[Presentation] 缺少启用的宣言服务。");
                _handle = service.Play(_definition);
            }
            public void Dispose() { _handle?.Cancel(); _handle = null; }
        }
    }
}
