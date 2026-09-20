using System;
using System.Collections.Generic;

namespace ShinySTG.GameFlow
{
    /// <summary>本局流程状态；实时分数仍由 PlayerResources 持有，本类只保存结算快照。</summary>
    public sealed class RunSession
    {
        readonly List<StageResult> _results = new List<StageResult>();
        readonly GameStartRequest _request;
        public CharacterDefinition Character => _request.Character;
        public StageSequenceDefinition Sequence => _request.Sequence;
        public int CurrentStageIndex { get; private set; }
        public StageDefinition CurrentStage => _request.Stages[CurrentStageIndex];
        public IReadOnlyList<StageDefinition> Stages => _request.Stages;
        public IReadOnlyList<StageResult> Results { get; }
        public bool IsCurrentStageRecorded => _results.Count > CurrentStageIndex;
        public bool IsComplete => _results.Count == Stages.Count;

        public RunSession(GameStartRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!request.Validate(out var error)) throw new ArgumentException(error, nameof(request));
            _request = request;
            Results = _results.AsReadOnly();
        }

        /// <summary>调试重开当前关时撤销当前关结果，不改变前面关卡的结算。</summary>
        public void ResetCurrentStageResult()
        {
            if (IsCurrentStageRecorded) _results.RemoveAt(CurrentStageIndex);
        }

        public bool TryRecordResult(StageResult result)
        {
            if (result == null || IsCurrentStageRecorded || result.StageIndex != CurrentStageIndex
                || result.StageId != CurrentStage.Id) return false;
            if (_results.Count > 0 && result.TotalScore < _results[_results.Count - 1].TotalScore) return false;
            _results.Add(result);
            return true;
        }

        public bool TryAdvanceStage()
        {
            if (!IsCurrentStageRecorded || CurrentStageIndex + 1 >= Stages.Count) return false;
            CurrentStageIndex++;
            return true;
        }
    }
}
