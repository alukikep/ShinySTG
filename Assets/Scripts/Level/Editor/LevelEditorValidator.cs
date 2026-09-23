using System;
using System.Collections.Generic;
using ShinySTG.Level.SpawnEntries;

namespace ShinySTG.Level.Editor
{
    public enum LevelEditorIssueSeverity { Warning, Error }

    public sealed class LevelEditorIssue
    {
        public LevelEditorIssueSeverity Severity { get; }
        public SpawnEntry Entry { get; }
        public string Message { get; }

        public LevelEditorIssue(LevelEditorIssueSeverity severity, SpawnEntry entry, string message)
        {
            Severity = severity;
            Entry = entry;
            Message = message;
        }
    }

    /// <summary>只读的关卡配置检查器；不修改资产，仅返回可定位到 Entry 的问题。</summary>
    public static class LevelEditorValidator
    {
        public static List<LevelEditorIssue> Validate(LevelDefinition definition)
        {
            var result = new List<LevelEditorIssue>();
            if (definition == null) return result;

            var entries = definition.Entries;
            if (entries == null || entries.Length == 0)
            {
                result.Add(new LevelEditorIssue(LevelEditorIssueSeverity.Warning, null, "关卡没有任何条目。"));
                return result;
            }

            bool hasCompletion = false;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    result.Add(new LevelEditorIssue(LevelEditorIssueSeverity.Error, null, $"第 {i} 个条目为空。"));
                    continue;
                }

                if (entry.TriggerTime < 0f)
                    result.Add(new LevelEditorIssue(LevelEditorIssueSeverity.Error, entry, "TriggerTime 不能小于 0。"));
                if (entry.Duration < 0f)
                    result.Add(new LevelEditorIssue(LevelEditorIssueSeverity.Error, entry, "Duration 不能小于 0。"));
                if (definition.Duration > 0f && entry.TriggerTime > definition.Duration)
                    result.Add(new LevelEditorIssue(LevelEditorIssueSeverity.Warning, entry, "条目触发时间超过关卡 Duration。"));

                switch (entry)
                {
                    case CompleteLevelEntry:
                        hasCompletion = true;
                        break;
                    case SimpleSpawnEntry simple when simple.EnemyPrefab == null:
                        result.Add(new LevelEditorIssue(LevelEditorIssueSeverity.Error, entry, "Simple 条目缺少 EnemyPrefab。"));
                        break;
                    case WaveSpawnEntry wave:
                        if (wave.Prefabs == null || wave.Prefabs.Length == 0)
                            result.Add(new LevelEditorIssue(LevelEditorIssueSeverity.Error, entry, "Wave 条目没有 Prefabs。"));
                        else if (Array.TrueForAll(wave.Prefabs, prefab => prefab == null))
                            result.Add(new LevelEditorIssue(LevelEditorIssueSeverity.Error, entry, "Wave 条目的 Prefabs 全为空。"));
                        break;
                    case BossEncounterEntry boss when boss.Encounter == null:
                        result.Add(new LevelEditorIssue(LevelEditorIssueSeverity.Error, entry, "Boss 条目缺少 Encounter。"));
                        break;
                    case PlaySfxSpawnEntry sfx when sfx.Cue == null:
                        result.Add(new LevelEditorIssue(LevelEditorIssueSeverity.Warning, entry, "Play SFX 条目没有 Cue，将不会播放声音。"));
                        break;
                    case PlayPresentationEntry presentation when presentation.Presentation == null:
                        result.Add(new LevelEditorIssue(LevelEditorIssueSeverity.Error, entry, "演出条目缺少 Presentation。"));
                        break;
                }
            }

            if (!hasCompletion)
                result.Add(new LevelEditorIssue(LevelEditorIssueSeverity.Warning, null, "关卡没有“流程/关卡通关”条目。"));
            else if (definition.Duration > 0f)
                result.Add(new LevelEditorIssue(LevelEditorIssueSeverity.Warning, null,
                    "关卡包含“流程/关卡通关”条目，但 Duration 大于 0；达到总时长会提前自动通关。使用通关条目时建议将 Duration 设为 0。"));
            if (definition.AutoSwitchBgm && definition.AudioBinding == null)
                result.Add(new LevelEditorIssue(LevelEditorIssueSeverity.Warning, null, "AutoSwitchBgm 已开启，但没有 AudioBinding。"));

            return result;
        }
    }
}
