using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 顺序执行一组子 Action,语义等价于把它们平铺到外层 Actions 数组。
    /// 自身 Duration 字段不生效(由外层 ShooterEnemy 的时间轴驱动)——
    /// 它的唯一作用是让 Inspector 中的长序列可以折叠分组,便于阅读。
    /// 可任意嵌套(Sequence 套 Parallel / Sequence / 其他 Action)。
    /// </summary>
    [Serializable, SRName("Action/Sequence")]
    public class SequenceAction : EnemyAction
    {
        [SerializeReference, SR]
        [Tooltip("按顺序执行的子行为。自身 Duration 字段被忽略,沿用外层时间轴。")]
        public EnemyAction[] Children;

        int   _idx;
        float _elapsedInCurrent;

        public override void OnEnter(Transform enemy)
        {
            _idx = -1;
            _elapsedInCurrent = 0f;
            Advance(0, enemy);
        }

        public override void OnTick(Transform enemy, float dt)
        {
            if (Children == null || Children.Length == 0) return;
            if (_idx < 0) return; // 已经跑完所有 children,等待外层 Sequence 自然结束

            var current = Children[_idx];
            if (current == null)
            {
                Advance(_idx + 1, enemy);
                return;
            }

            current.OnTick(enemy, dt);
            _elapsedInCurrent += dt;

            // ★ vX 起:读 CurrentDuration(已由 Advance 时抽样缓存),不再是基类 float Duration 字段
            if (_elapsedInCurrent >= current.CurrentDuration)
            {
                current.OnExit(enemy);
                Advance(_idx + 1, enemy);
            }
        }

        void Advance(int next, Transform enemy)
        {
            if (next >= Children.Length)
            {
                _idx = -1; // 跑完了,但仍占用外层时间
                return;
            }
            _idx = next;
            _elapsedInCurrent = 0f;

            // ★ vX 起 Duration 多态:Sequence 自己负责子 child 的 Duration 抽样
            //   (外层 BehaviorFlowRuntime.AdvanceTo 只抽样 SequenceAction 自身,
            //    子 child 的 CurrentDuration 仍然需要容器主动调用)。
            //   - Fixed:等价旧 float Duration(老 .asset 通过 _legacyDuration 兜底)
            //   - Random Range:每次 Sequence 切到下一条 child 时独立抽样 → 节奏抖动
            var child = Children[_idx];
            if (child != null)
            {
                child.SetCurrentDuration(child.ResolveDuration());
                child.OnEnter(enemy);
            }
        }
    }
}
