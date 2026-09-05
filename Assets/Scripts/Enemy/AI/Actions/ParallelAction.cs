using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 并行容器:同时执行多个子 Action,每个 child 各自独立计时。
    /// Parallel.Duration 是"封顶时间",到了会强制调用所有仍存活的 child 的 OnExit,
    /// 防止某个 child 的 Duration 配置过大导致敌人行为卡住。
    /// 可任意嵌套(Parallel 套 Parallel / Sequence / 其他 Action)。
    /// </summary>
    [Serializable, SRName("Action/Parallel")]
    public class ParallelAction : EnemyAction
    {
        [SerializeReference, SR]
        [Tooltip("并行执行的子行为。每个 child 各自计时,到期自动停止。")]
        public EnemyAction[] Children;

        // 每个 child 的已用时间(在 OnEnter 时按 Children.Length 重建)
        float[] _childElapsed;
        // 标记每个 child 是否已经自然结束(避免 OnExit 重复调用)
        bool[]  _childFinished;

        public override void OnEnter(Transform enemy)
        {
            int n = Children != null ? Children.Length : 0;
            _childElapsed  = new float[n];
            _childFinished = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (Children[i] != null) Children[i].OnEnter(enemy);
            }
        }

        public override void OnTick(Transform enemy, float dt)
        {
            if (Children == null) return;
            int n = Children.Length;
            for (int i = 0; i < n; i++)
            {
                if (_childFinished[i] || Children[i] == null) continue;

                _childElapsed[i] += dt;

                // 该 child 自然到期 → 走 OnExit,后续帧不再 tick
                if (_childElapsed[i] >= Children[i].Duration)
                {
                    Children[i].OnExit(enemy);
                    _childFinished[i] = true;
                    continue;
                }

                Children[i].OnTick(enemy, dt);
            }
        }

        public override void OnExit(Transform enemy)
        {
            if (Children == null) return;
            int n = Children.Length;
            for (int i = 0; i < n; i++)
            {
                // 只对"还活着"的 child 调用 OnExit,避免重复清理
                if (!_childFinished[i] && Children[i] != null)
                    Children[i].OnExit(enemy);
            }
        }
    }
}
