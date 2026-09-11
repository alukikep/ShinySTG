using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 移动行为。持有一个 MoveBehaviour(通过 [SerializeReference] 可扩展),
    /// 持续 Duration 秒后停止,自动进入下一条。
    /// </summary>
    [Serializable, SRName("Action/Move")]
    public class MoveAction : EnemyAction
    {
        [SerializeReference]
        [SR]
        [Tooltip("具体移动逻辑。当前可选:Linear (匀速直线)。后续可自行扩展子类。")]
        public MoveBehaviour Move = new LinearMove();

        public override void OnEnter(Transform enemy)
        {
            if (Move != null)
            {
                Move.OnEnter(enemy);
                // "绝对锚定"型 Move 在 OnEnter 后立即以 dt=0 同步一次位置,
                // 避免切到下一条 Action 时敌人从旧位置瞬移到新轨迹远处。
                // 增量型 Move(SnapOnEnter=false)走默认值,OnTick(dt=0) 不会改 enemy.position。
                if (Move.SnapOnEnter) Move.OnTick(enemy, 0f);
            }
        }

        public override void OnTick(Transform enemy, float dt)
        {
            if (Move != null) Move.OnTick(enemy, dt);
        }

        public override void OnExit(Transform enemy)
        {
            if (Move != null) Move.OnExit(enemy);
        }
    }
}
