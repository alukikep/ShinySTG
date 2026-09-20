using UnityEngine;

namespace ShinySTG.GameFlow
{
    [CreateAssetMenu(menuName = "STG/Stage Sequence", fileName = "StageSequence")]
    public sealed class StageSequenceDefinition : ScriptableObject
    {
        [Tooltip("流程的稳定标识。")]
        public string Id;
        [Tooltip("按游玩顺序排列；当前要求全部关卡使用同一个 Gameplay 场景。")]
        public StageDefinition[] Stages = new StageDefinition[0];
    }
}
