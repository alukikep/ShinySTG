using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.GameActions
{
    [Serializable]
    public sealed class ActionSequence
    {
        [Tooltip("等待这组动作完成后，再推进宿主的阶段或收尾。不会暂停游戏世界。")]
        public bool WaitForCompletion = true;
        [SerializeReference, SR, Tooltip("按顺序执行；可通过 Parallel 动作配置并行。")]
        public GameAction[] Actions;
    }
}
