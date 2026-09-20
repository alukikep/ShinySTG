using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShinySTG.BulletCore
{
    /// <summary>
    /// 全局信号总线 —— 子弹 modifier 跨系统通信的轻量通道。
    ///
    /// <para>★ 设计动机:</para>
    /// <para>
    /// 项目现状:<see cref="BulletModifier"/> 的激活时机只有 Delay / Duration / OneShot(见
    /// <c>Assets/Scripts/Bullet/BulletModifier.cs</c> 与 ARCHITECTURE.md §2.6)。所有 modifier
    /// 的「解锁」只能用「延迟 N 秒后」表达,无法对齐到敌人 AI 节奏(Boss 喊话、阶段切换、
    /// Parallel 容器内某条 Action 完成 等)。
    /// </para>
    /// <para>
    /// 本类提供「敌人 → 子弹 modifier」的单向事件通道:
    ///   - <c>EmitSignalAction</c> 在 BehaviorFlow 某个时间点 Emit 具名信号
    ///   - 订阅了那名字的 BulletModifier 收到信号 → 立即进入窗口(替代 / 配合 Delay)
    /// </para>
    ///
    /// <para>★ 设计要点(对照其他基础设施):</para>
    /// <list type="bullet">
    ///   <item><b>静态门面</b>:与 <c>AudioMix.cs</c> 同套路,无需 MonoBehaviour 单例,场景里没挂任何东西也能用。</item>
    ///   <item><b>字典派发</b>:<c>Dictionary&lt;signalName, List&lt;Action&gt;&gt;</c>,O(1) Emit,O(订阅者数) 派发。
    ///         STG 高弹量场景(< 1000 颗/秒)下开销可忽略。</item>
    ///   <item><b>全局广播</b>:Emit 时不区分发射源、不区分阵营、不做同源过滤 —— STG 典型场景
    ///         「Boss 喊话全场响应」正需要这种简单粗暴的广播语义。隔离需求若出现,后续
    ///         可在 EmitSignalAction 与 OnSignalStartTrigger 加 <c>string SourceTag</c> 字段,
    ///         不破坏现有 API(向下兼容 null = 任意源)。</item>
    ///   <item><b>无名 / null 静默</b>:信号名为 null / 空 / 全空白时直接 return,
    ///         不会因为策划拼错信号名而崩溃 —— 与项目一贯的「静默兜底」风格一致
    ///         (对照 BoundsService 无单例时的硬编码 fallback)。</item>
    ///   <item><b>无引用泄漏</b>:modifier 退订统一走 <see cref="Unsubscribe"/>,由
    ///         <c>Bullet.DetachSignalTriggers</c> 在 BulletPool.Return 与
    ///         <c>Bullet.OnDestroy</c> 兜底触发,杜绝「弹已回收但 handler 还挂在字典里」的内存泄漏。
    ///         <b>⚠ 子类自己别忘了调 Unsubscribe</b> —— OnDetach 是契约,不是魔法。</item>
    ///   <item><b>场景切换清空</b>:<c>[RuntimeInitializeOnLoadMethod]</c> 在 SubsystemRegistration
    ///         阶段清空 _subs,避免 PlayMode 重启后留有上一次运行的死订阅。</item>
    /// </list>
    ///
    /// <para>★ 为什么不用 Unity 的 Timeline SignalReceiver:</para>
    /// <para>
    /// Timeline 那套走反射 + 序列化资产枚举(必须在 timeline 资产里登记信号),对 STG 这种
    /// 「策划自由命名、运行时即时订阅」的场景太重。本类用 string 名 + 静态字典,
    /// 零反射、零资产登记、新增信号 = 直接写字符串。
    /// </para>
    ///
    /// <para>★ 典型用法(代码层):</para>
    /// <code>
    ///   // 发射端(BulletSignalBus 不感知是谁发的,也不感知发给谁):
    ///   BulletSignalBus.Emit("boss_yell_charge_done", enemy.position);
    ///
    ///   // 接收端(订阅型 StartTrigger 自己管订阅):
    ///   BulletSignalBus.Subscribe("boss_yell_charge_done", OnSignalReceived);
    ///   // ...
    ///   BulletSignalBus.Unsubscribe("boss_yell_charge_done", OnSignalReceived);
    /// </code>
    ///
    /// 详见 <c>Assets/Scripts/Bullet/Triggers/ModifierStartTrigger.cs</c> 的 OnSignalStartTrigger 子类,
    /// 与 <c>Assets/Scripts/Enemy/AI/Actions/EmitSignalAction.cs</c>。
    /// </summary>
    public static class BulletSignalBus
    {
        // ── 信号名 → 订阅者列表(handler 是带 origin 的闭包) ──
        // ★ 使用 List 而非 LinkedList:STG 信号订阅量小(< 100 个活跃 modifier 同时订阅),
        //   List 的缓存友好性 + Remove 的 O(n) 在这个量级下完全够用。
        //   若将来需要高频订阅/退订(例如粒子池),再换 HashSet 也无伤大雅。
        static readonly Dictionary<string, List<Action<Vector2>>> _subs
            = new Dictionary<string, List<Action<Vector2>>>();

        /// <summary>
        /// 订阅某信号的发射。<paramref name="handler"/> 收到 origin(信号源位置)作为参数。
        /// ★ 信号名为 null / 空时直接 return(静默忽略)。
        /// ★ handler 为 null 时直接 return(防御性,防止调用方笔误)。
        /// ★ 同一 signalName + 同一 handler 多次 Subscribe 会被去重为一份(防御性,
        ///   避免 modifier 在 OnAttach 重复调用挂两份导致一次信号触发两次回调)。
        /// </summary>
        public static void Subscribe(string signalName, Action<Vector2> handler)
        {
            if (string.IsNullOrWhiteSpace(signalName)) return;
            if (handler == null) return;

            if (!_subs.TryGetValue(signalName, out var list))
            {
                list = new List<Action<Vector2>>(2);
                _subs[signalName] = list;
            }
            // 防御性去重:同一个 handler 不重复挂。
            if (!list.Contains(handler)) list.Add(handler);
        }

        /// <summary>
        /// 取消订阅。<paramref name="handler"/> 不在订阅列表时静默返回。
        /// ★ 配对原则:谁 Subscribe 谁 Unsubscribe(OnAttach 订阅 / OnDetach 退订)。
        /// </summary>
        public static void Unsubscribe(string signalName, Action<Vector2> handler)
        {
            if (string.IsNullOrWhiteSpace(signalName)) return;
            if (handler == null) return;
            if (!_subs.TryGetValue(signalName, out var list)) return;

            // SwapRemove 避免 List<T>.Remove 的元素移动(委托列表无序要求)。
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == handler)
                {
                    list[i] = list[list.Count - 1];
                    list.RemoveAt(list.Count - 1);
                    break;
                }
            }

            // 空列表清掉字典条目,避免字典越来越大。
            if (list.Count == 0) _subs.Remove(signalName);
        }

        /// <summary>
        /// 发射信号 —— 开始派发时的所有同名订阅者各调用一次；不保证订阅顺序。
        /// </summary>
        /// <param name="signalName">信号名,大小写敏感。空 / null 静默 return。</param>
        /// <param name="origin">信号源位置(世界坐标)。传给 handler 的 origin 参数;
        /// modifier 用来做距离判定(例:只对「Boss 8 单位范围内的弹」生效)。</param>
        public static void Emit(string signalName, Vector2 origin)
        {
            if (string.IsNullOrWhiteSpace(signalName)) return;
            if (!_subs.TryGetValue(signalName, out var list)) return;
            if (list.Count == 0) return;

            // ★ 必须复制元素而不是只复制 List 引用。
            //   回调可能在派发过程中 Subscribe / Unsubscribe；Unsubscribe 会对原列表
            //   做 swap-remove，直接遍历原列表会跳过元素或重复访问。ToArray() 固定本次
            //   派发开始时的订阅集合和顺序，新订阅者等下一次 Emit 才生效。
            var snapshot = list.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                var h = snapshot[i];
                if (h == null) continue;
                try { h(origin); }
                catch (Exception ex)
                {
                    // 单个 handler 抛异常不影响其他订阅者;但要 log 出来,方便排查 modifier bug。
                    Debug.LogException(ex);
                }
            }
        }

        /// <summary>
        /// 清空所有订阅。场景切换 / PlayMode 重启时由 [RuntimeInitializeOnLoadMethod] 自动调,
        /// 外部脚本不需要手动调。
        /// </summary>
        public static void ClearAll()
        {
            _subs.Clear();
        }

        // ★ 在 PlayMode 启动的 SubsystemRegistration 阶段清空字典。
        //   - 这是 Unity 官方推荐的「Runtime 重置静态字段」标准做法,
        //     比 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] 更早,
        //     避免上一次 PlayMode 残留的订阅者指针影响新一次运行。
        //   - Domain Reload Disabled(Unity 2019.3+ 的 Enter Play Mode Options 优化)场景下尤其关键。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            _subs.Clear();
        }

        /// <summary>当前已注册的不同信号名数量(调试 / Editor 工具用)。</summary>
        public static int DebugSignalCount => _subs.Count;

        /// <summary>指定信号名的当前订阅者数(调试 / Editor 工具用)。</summary>
        public static int DebugSubscriberCount(string signalName)
        {
            if (string.IsNullOrWhiteSpace(signalName)) return 0;
            return _subs.TryGetValue(signalName, out var list) ? list.Count : 0;
        }
    }
}
