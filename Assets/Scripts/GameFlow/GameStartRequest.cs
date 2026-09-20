using System;
using System.Collections.Generic;

namespace ShinySTG.GameFlow
{
    /// <summary>每次开局的不可变参数，不携带旧场景对象。</summary>
    public sealed class GameStartRequest
    {
        public CharacterDefinition Character { get; }
        public StageDefinition Stage => Stages.Count > 0 ? Stages[0] : null;
        public StageSequenceDefinition Sequence { get; }
        public IReadOnlyList<StageDefinition> Stages { get; }

        public GameStartRequest(CharacterDefinition character, StageDefinition stage)
        {
            Character = character;
            Stages = Array.AsReadOnly(new[] { stage });
        }

        GameStartRequest(CharacterDefinition character, StageSequenceDefinition sequence, bool sequenceRequest)
        {
            Character = character;
            Sequence = sequence;
            // 固定本局顺序，之后修改资产数组不会改变进行中的流程。
            var stages = sequence != null && sequence.Stages != null
                ? (StageDefinition[])sequence.Stages.Clone() : new StageDefinition[0];
            Stages = Array.AsReadOnly(stages);
        }

        // 命名工厂避免旧 new GameStartRequest(character, null) 出现重载歧义。
        public static GameStartRequest FromSequence(CharacterDefinition character, StageSequenceDefinition sequence)
            => new GameStartRequest(character, sequence, true);

        public bool Validate(out string error)
        {
            error = null;
            if (Character == null || Character.PlayerPrefab == null)
                error = "角色未配置玩家 prefab。";
            else if (!Character.PlayerPrefab.gameObject.activeSelf || !Character.PlayerPrefab.enabled)
                error = "玩家 prefab 及 Player 组件必须启用。";
            else if (Stages.Count == 0)
                error = "关卡序列为空。";
            if (error != null) return false;
            for (int i = 0; i < Stages.Count; i++)
            {
                var stage = Stages[i];
                if (stage == null || stage.Level == null) error = "未配置关卡或关卡时间轴。";
                else if (string.IsNullOrWhiteSpace(stage.ScenePath)) error = "关卡场景路径为空。";
                else if (!Finite(stage.SpawnPosition.x) || !Finite(stage.SpawnPosition.y) || !Finite(stage.SpawnPosition.z))
                    error = "出生位置必须为有限数值。";
                else if (!string.Equals(stage.ScenePath, Stage.ScenePath, StringComparison.Ordinal))
                    error = "当前关卡序列必须使用同一个 Gameplay 场景。";
                if (error != null) { error = $"第 {i + 1} 关：{error}"; return false; }
            }
            return error == null;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
