using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 什么都不做,仅占用 Duration 秒。常用于"停顿后下一拍"或"先落位再开火"。
    /// </summary>
    [Serializable, SRName("Action/Wait")]
    public class WaitAction : EnemyAction
    {
        public override void OnEnter(Transform enemy) { }
        public override void OnTick(Transform enemy, float dt) { }
        public override void OnExit(Transform enemy) { }
    }
}
