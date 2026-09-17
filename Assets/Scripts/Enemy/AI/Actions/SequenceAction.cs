using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 顺序执行一组子 Action,语义等价于把它们平铺到外层 Actions 数组。
    /// 可任意嵌套(Sequence 套 Parallel / Sequence / 其他 Action)。
    ///
    /// <para>★ 自身 Duration 字段语义(两段式):</para>
    /// <list type="bullet">
    ///   <item><b>Loop = false(默认,等价旧行为)</b>:Sequence 自身 Duration 字段被忽略,
    ///         沿用外层 BehaviorFlowRuntime 的时间轴,children 跑完后 Sequence 空转等外层切走。</item>
    ///   <item><b>Loop = true</b>:Sequence 自身 Duration 字段变为<b>封顶时间</b>(与
    ///         <see cref="ParallelAction"/> 自身 Duration 同套路)。children 跑完后
    ///         从头再跑一轮,直到 Sequence 自身的 CurrentDuration 耗尽。Sequence 自身
    ///         Duration 由外层 <see cref="BehaviorFlowRuntime.AdvanceTo"/> 抽样一次
    ///         (Random Range 策略下整段 Loop 期间只抽一次,与顶层 BehaviorFlow.Loop 一致)。</item>
    /// </list>
    /// </summary>
    [Serializable, SRName("Action/Sequence")]
    public class SequenceAction : EnemyAction
    {
        [SerializeReference, SR]
        [Tooltip("按顺序执行的子行为。")]
        public EnemyAction[] Children;

        [Tooltip("children 跑完后是否从头再跑一轮(直到 Sequence 自身 Duration 耗尽)。\n" +
                 "  false(默认,等价旧行为)= children 跑完即空转等外层切走,Sequence 自身 Duration 字段被忽略;\n" +
                 "  true = Sequence 自身 Duration 字段变为「封顶时间」(与 Parallel 同套路),\n" +
                 "         children 跑完后从头循环,直到封顶时间到点。")]
        public bool Loop = false;

        int   _idx;
        float _elapsedInCurrent;
        // ★ Loop 模式专用:Sequence 自身的封顶时间剩余(秒)。Loop=false 时恒为 0。
        //   初值由 OnEnter 从 CurrentDuration 拷贝(外层 AdvanceTo 已抽样好)。
        //   OnTick 每帧递减,<=0 时放手(children 跑完也不再回 0)。
        float _remainingSelfDuration;

        public override void OnEnter(Transform enemy)
        {
            _idx = -1;
            _elapsedInCurrent = 0f;
            // ★ Loop 模式下 Sequence 自身 Duration 字段首次生效
            //   (与 Parallel 容器同套路,见 ParallelAction 注释)
            _remainingSelfDuration = Loop ? Mathf.Max(0f, CurrentDuration) : 0f;
            Advance(0, enemy);
        }

        public override void OnTick(Transform enemy, float dt)
        {
            if (Children == null || Children.Length == 0) return;

            // ① Loop 模式:封顶时间递减
            if (Loop) _remainingSelfDuration -= dt;

            // ② children 已跑完一轮
            if (_idx < 0)
            {
                if (Loop && _remainingSelfDuration > 0f)
                {
                    // Sequence 自身还没到点 → 内部从头再跑一轮
                    //   (与 BehaviorFlowRuntime.AdvanceTo 顶层 Loop 同套路)
                    Advance(0, enemy);
                    // 不 return —— 继续往下 tick 新进入的 child 0
                }
                else
                {
                    // 不 loop 或封顶时间到 → 空转等外层结束
                    return;
                }
            }

            // ③ 跑当前 child
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

        public override void OnExit(Transform enemy)
        {
            // ★ Loop 模式下,如果 Sequence 自己被外层切走(ShooterPhase.OnExit → ForceExit,
            //   或 BehaviorFlowRuntime 自然切到下一条),当前 child 可能还在跑 —— 必须给它
            //   OnExit 收尾,否则 child 的清理会漏。与 ParallelAction.OnExit 同套路。
            if (_idx >= 0 && Children != null && _idx < Children.Length)
            {
                Children[_idx]?.OnExit(enemy);
                _idx = -1;
            }
            // 非循环序列也可能被宿主提前中断，必须清理仍在执行的 child。
        }

        void Advance(int next, Transform enemy)
        {
            if (Children == null || Children.Length == 0) { _idx = -1; return; }
            if (next >= Children.Length)
            {
                // 跑完一轮
                if (Loop && _remainingSelfDuration > 0f)
                {
                    // ★ 与 BehaviorFlowRuntime.AdvanceTo 顶层 Loop 同套路
                    next = 0;
                }
                else
                {
                    _idx = -1; // 跑完了,空转等外层结束
                    return;
                }
            }
            _idx = next;
            _elapsedInCurrent = 0f;

            // ★ vX 起 Duration 多态:Sequence 自己负责子 child 的 Duration 抽样
            //   (外层 BehaviorFlowRuntime.AdvanceTo 只抽样 SequenceAction 自身,
            //    子 child 的 CurrentDuration 仍然需要容器主动调用)。
            //   - Fixed:等价旧 float Duration(老 .asset 通过 _legacyDuration 兜底)
            //   - Random Range:每次 Sequence 切到下一条 child 时独立抽样 → 节奏抖动
            //   - Loop 模式下回到 child 0 时也会重新抽样,符合「节奏抖动」语义。
            var child = Children[_idx];
            if (child != null)
            {
                child.SetCurrentDuration(child.ResolveDuration());
                child.OnEnter(enemy);
            }
        }
    }
}
