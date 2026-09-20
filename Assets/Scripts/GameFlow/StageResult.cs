using System;

namespace ShinySTG.GameFlow
{
    /// <summary>单关结算的不可变快照，不保留玩家或场景引用。索引从零开始。</summary>
    public sealed class StageResult
    {
        public int StageIndex { get; }
        public string StageId { get; }
        public long Score { get; }
        public long TotalScore { get; }
        public int Lives { get; }
        public int Bombs { get; }
        public int PowerUnits { get; }
        public int GrazeCount { get; }

        public StageResult(int stageIndex, string stageId, long score, long totalScore,
            int lives, int bombs, int powerUnits, int grazeCount)
        {
            if (stageIndex < 0 || score < 0 || totalScore < score || lives < 0
                || bombs < 0 || powerUnits < 0 || grazeCount < 0)
                throw new ArgumentOutOfRangeException(nameof(stageIndex), "结算数值不能为负，累计分数不能小于本关分数。");
            StageIndex = stageIndex;
            StageId = stageId;
            Score = score;
            TotalScore = totalScore;
            Lives = lives;
            Bombs = bombs;
            PowerUnits = powerUnits;
            GrazeCount = grazeCount;
        }
    }
}
