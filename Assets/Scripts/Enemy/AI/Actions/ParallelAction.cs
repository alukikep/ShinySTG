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
            for (int i = 0; i < n; i++) _childFinished[i] = true;
            for (int i = 0; i < n; i++)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) return;
                var c = Children[i];
                if (c != null)
                {
                    // ★ vX 起 Duration 多态:Parallel 自己负责子 child 的 Duration 抽样
                    //   (外层 BehaviorFlowRuntime.AdvanceTo 只抽样 ParallelAction 自身,
                    //    子 child 的 CurrentDuration 仍然需要容器主动调用)。
                    //   - Fixed:等价旧 float Duration(老 .asset 通过 _legacyDuration 兜底)
                    //   - Random Range:每次 Parallel 进入时各 child 独立抽样 → 节奏抖动
                    c.SetCurrentDuration(c.ResolveDuration());
                    _childFinished[i] = false;
                    c.OnEnter(enemy);
                }
            }
        }

        public override void OnTick(Transform enemy, float dt)
        {
            if (Children == null) return;
            int n = Children.Length;
            for (int i = 0; i < n; i++)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) return;
                if (_childFinished[i] || Children[i] == null) continue;

                var child = Children[i];
                float remaining = Mathf.Max(0f, child.CurrentDuration - _childElapsed[i]);
                float tickDuration = Mathf.Min(Mathf.Max(0f, dt), remaining);
                _childElapsed[i] += tickDuration;

                // 自然到期前补齐最后一段有效时间；先 Tick 再 Exit。
                if (tickDuration > 0f) child.OnTick(enemy, tickDuration);
                if (enemy == null || !enemy.gameObject.activeInHierarchy) return;
                // OnTick 可能重入容器 OnExit，避免重复退出。
                if (_childFinished[i]) continue;

                // 该 child 自然到期 → 走 OnExit,后续帧不再 tick
                // ★ vX 起:读 CurrentDuration(已由 OnEnter 时抽样缓存),不再是基类 float Duration 字段
                if (_childElapsed[i] >= child.CurrentDuration)
                {
                    _childFinished[i] = true;
                    child.OnExit(enemy);
                    continue;
                }
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
                {
                    _childFinished[i] = true;
                    Children[i].OnExit(enemy);
                }
            }
        }
    }
}
