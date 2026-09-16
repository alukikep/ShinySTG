using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.Laser
{
    /// <summary>
    /// 激光行为的可插拔修饰(持续旋转 / 跟随 Boss 移动 / 长度波形变化 / ...)。
    /// 纯 C# 类(非 MonoBehaviour、非 ScriptableObject),与 BulletModifier 对仗。
    ///
    /// <para>★ 时间窗口体系(对齐 BulletModifier.vX,arch-bullet §2.6 / §2.8):</para>
    /// <para>
    /// 所有 LaserModifier 自动支持「Delay 后才生效 / Duration 秒后结束 / 订阅信号触发」
    /// 完整双时钟 + 信号触发体系:
    ///   - <see cref="Delay"/> + <see cref="StartTrigger"/> + <see cref="Duration"/> + <see cref="AutoSkipOutsideWindow"/> + <see cref="OneShot"/>
    ///   - 默认值与 PR1 旧行为 100% 等价(Delay=0,Duration=0,AutoSkipOutsideWindow=true,OneShot=false,StartTrigger=null)
    /// </para>
    ///
    /// <para>字段约定(对齐 BulletModifier):</para>
    /// <list type="bullet">
    ///   <item>鼓励值类型(float/int/Vector2/...)—— 默认 Clone 即可,无额外开销</item>
    ///   <item>禁止 List&lt;T&gt; / T[] / 自定义类 —— 必须 override Clone 深拷</item>
    ///   <item>不要访问 this.transform —— modifier 不是 GameObject</item>
    ///   <item>访问激光数据请通过 <see cref="LaserEntity"/> 的公开字段(Position / Angle / Velocity ...)</item>
    /// </list>
    /// </summary>
    [Serializable]
    public abstract class LaserModifier
    {
        // ============================================================
        //  时间窗口字段(对所有子类生效;默认值与 PR1 旧行为完全等价)
        // ============================================================

        [Header("Timing")]
        [Tooltip("从激光生成开始,延迟多少秒后 modifier 才开始生效。\n" +
                 "0 = 出生即生效(默认,与 PR1 旧行为一致)。\n" +
                 "典型用例:0.5s 后才激活 Orbit 旋转,前 0.5s 激光沿初始方向直线飞行(「蓄力期」);\n" +
                 "         1s 后才激活分裂 / 染色 等副作用(配合 OneShot)。")]
        [Min(0f)] public float Delay = 0f;

        [Tooltip("Modifier 启动触发器 —— 决定何时进入时间窗口。\n" +
                 "默认 DelayLaserModifierStartTrigger(Delay=0,与 PR1 旧 100% 等价)。\n" +
                 "其他触发器(下拉选):\n" +
                 "  - LaserTrigger/Delay          : 等 Delay 秒(等价旧 Delay 字段)\n" +
                 "  - LaserTrigger/On Signal      : 订阅 BulletSignalBus 信号,收到即激活(可配 MaxWait / 距离判定)\n" +
                 "  - LaserTrigger/Delay Or Signal: Delay 与信号任一先到即激活\n" +
                 "★ null = 用 DelayLaserModifierStartTrigger{Delay=this.Delay} 兜底,旧 .asset 无脑兼容。\n" +
                 "详见 Assets/Scripts/Laser/LaserModifierStartTrigger.cs + arch-laser §13.5。\n" +
                 "★ 复用 BulletSignalBus:同一信号名可被子弹版 / 激光版 trigger 同时订阅,Emit 一次全场响应。")]
        [SerializeReference, SR]
        public LaserModifierStartTrigger StartTrigger;

        [Tooltip("生效后持续多少秒。<b>&lt;=0 表示一直生效</b>(默认,与 PR1 旧行为一致)。\n" +
                 "典型用例:旋转 2 秒后切回直线(燃料耗尽)、闪烁 1.5s 后熄灭、OneShot 触发后立刻结束。")]
        public float Duration = 0f;

        [Tooltip("Modifier 在窗口外(Delay 之前 / Duration 之后)是否完全禁用。\n" +
                 "true(默认)= 直接 return,不调子类的 ModifyCore,子类完全不知道窗外的存在(简单可靠);\n" +
                 "false = 仍每帧调用子类 ModifyCore,子类可通过 IsActive 自行判断(用于「窗外做插值 / 清理」等高级场景)。")]
        public bool AutoSkipOutsideWindow = true;

        [Tooltip("将本 modifier 标记为<b>一次性触发</b>:进入窗口瞬间调用 OnWindowEnter 一次,然后立刻退出(不再每帧调 ModifyCore)。\n" +
                 "★ 用 OnWindowEnter(override)写一次性逻辑;ModifyCore 在 OneShot=true 时不会被调用。\n" +
                 "典型用例:出生 N 秒后生成一圈分裂激光、播一次性音效、切换贴图。")]
        public bool OneShot = false;

        // per-instance 计时器(每条激光 Clone 时独立)
        // ★ 故意不复用 laser.Timer:某些 modifier 会修改 LaserData 字段,可能造成「自我修改自己计时器」的鸡生蛋问题。
        //
        // ★ 双时钟设计(对齐 BulletModifier.vX 修复 Duration 语义后):
        //   - _elapsed          时钟 A:激光出生至今。供 StartTrigger.ShouldActivate 用,始终累加。
        //   - _windowElapsed    时钟 B:窗口内累计时间。只在 _isActive=true 时累加,直接对接 Duration。
        //   - _windowStarted    ★ 粘性位:窗口是否「已触发过」。
        //                      用于:
        //                      1) OneShot 触发后由 _windowExhausted 锁死(见下),防止后续 frame 再触发 OnWindowEnter;
        //                      2) 信号型 trigger 即使信号持续到,也不会让 _windowElapsed 重置;
        //                      3) ResetWindow 时清零,让激光回池复用时 OneShot 能再次触发。
        //   - _windowExhausted  ★ OneShot 专用锁死位:触发过一次 OnWindowEnter 后置 true,
        //                      后续 _windowStarted=true 但 nowActive=false → 不会再触发任何 enter/exit。
        [NonSerialized] float _elapsed;
        [NonSerialized] float _windowElapsed;
        [NonSerialized] bool  _isActive;
        [NonSerialized] bool  _windowStarted;
        [NonSerialized] bool  _windowExhausted;

        /// <summary>当前是否在时间窗口内。只读,供子类 / 外部查询。</summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// 窗口内累计时间(秒)。从窗口首次激活那一刻起算,Duration 内的每一帧累加。
        /// ★ 与 Duration 直接对接:ElapsedInWindow >= Duration 时窗口到期退出。
        /// </summary>
        public float ElapsedInWindow => _windowElapsed;

        /// <summary>
        /// 激光生成时由 LaserPattern.AttachModifiers 调用,重置计时器与激活状态。
        /// ★ 必须 ResetWindow 才能让 OneShot 在激光复用时再次触发。
        /// </summary>
        public void ResetWindow()
        {
            // ★ 双时钟 + 两个粘性位一并清零 —— 必须四件套一起,否则:
            //   - 只清 _elapsed 不清 _windowStarted → 窗口永远激活(粘性位锁死);
            //   - 只清 _elapsed 不清 _windowElapsed → 复用时窗口一进就显示「快到期」;
            //   - 只清 _elapsed 不清 _windowExhausted → OneShot 永远哑火。
            _elapsed          = 0f;
            _windowElapsed    = 0f;
            _isActive         = false;
            _windowStarted    = false;
            _windowExhausted  = false;

            // ★ 自 CS0506 修复新增:子类 hook(对齐子弹版 OnWindowEnter 虚钩子模型)
            //   ResetWindow 是基类管双时钟的事,子类只关心自己的池复用状态扩展点。
            //   子类 override OnResetWindow() 清自己的 lazy Init 状态等,不需要 super 调 base。
            //   ★ 调用点放在 StartTrigger null 兜底分支之前:确保 StartTrigger=null 这条 return 路径
            //     也触发 OnResetWindow(否则兜底路径会绕过子类的状态清零)。
            OnResetWindow();

            // ★ 兼容兜底:StartTrigger == null → 新建 DelayLaserModifierStartTrigger 与旧 Delay 字段行为一致
            //   老 .asset 反序列化后 StartTrigger 字段是 null,必须在这里兜底,否则 Modify 第一帧 NRE。
            //   让「老 .asset 字段值 Delay=X」在 ResetWindow 调用时同步进新建的 StartTrigger.Delay,
            //   保证行为 100% 等价。
            //
            // ★ ★ ★ 关键修复(对齐 BulletModifier.vX 修复)★★★
            //   trigger 自己配的 Delay 字段是主,基类 Delay 仅在「StartTrigger=null」兜底分支
            //   生效(把基类 Delay 拷过去给新建的 DelayLaserModifierStartTrigger)。
            //   StartTrigger 非 null 时完全不动 trigger 内部字段 —— 让 trigger 自己管自己的 Delay。
            if (StartTrigger == null)
            {
                StartTrigger = new DelayLaserModifierStartTrigger { Delay = this.Delay };
                return;
            }
            // ★ 不再做「基类 Delay → trigger.Delay」同步 —— trigger 自己管自己的 Delay 字段。
        }

        /// <summary>
        /// 子类 override 此方法做自己的池复用状态清零(对齐子弹版 <c>OnWindowEnter</c> 虚钩子模型)。
        /// 基类的 <see cref="ResetWindow"/> 会在双时钟清零之后调到这里。
        ///
        /// <para>★ 为什么需要这个钩子(对齐子弹版的设计模式):</para>
        /// <para>
        /// 子弹版 <c>BulletModifier.ResetWindow</c> 是 <c>public void</c>(非 virtual),子类通过 override
        /// <c>OnWindowEnter / OnWindowExitCleanup</c> 等虚钩子做自己的池复用状态管理(如
        /// <c>BounceBulletModifier._remaining = MaxBounces</c> 在 <c>OnWindowEnter</c> 初始化)。
        /// </para>
        /// <para>
        /// 激光版延续这个设计:<c>ResetWindow</c> 仍是 <c>public void</c>(非 virtual),
        /// 子类 override <c>OnResetWindow</c> 做自己的状态清零。
        /// 典型用例:<see cref="LaserOrbitModifier"/> override <c>OnResetWindow</c> 清零
        /// <c>_initialized = false</c> 等 lazy Init 状态,使池复用时第一帧 ModifyCore 重新触发 LazyInit。
        /// </para>
        ///
        /// <para>★ 钩子调用时机:</para>
        /// <para>
        /// 基类 <c>ResetWindow</c> 在双时钟清零后调用本钩子(<b>在 StartTrigger null 兜底分支 return 之前</b>,
        /// 确保两条路径都触发)。子类不需要 super 调 base — 基类 <c>OnResetWindow</c> 默认空。
        /// </para>
        /// </summary>
        protected virtual void OnResetWindow() { }

        /// <summary>
        /// 每帧由 LaserEntity.LateUpdate 调用。
        /// 基类统一管理时间窗口 + 边缘触发钩子,子类 override 的 ModifyCore() 只关心「在窗口内的行为」。
        /// ★ 此方法不可 override —— 强制所有 modifier 走基类窗口管理,避免漏改。
        ///
        /// <para>★ 时间窗口模型(对齐 BulletModifier.vX 修复后):</para>
        /// <para>
        /// 「<b>何时进入窗口</b>」(由 StartTrigger 决定)和「<b>进入后持续多久</b>」(由 Duration 决定)
        /// 是两个独立的维度 —— 它们不能被压扁成「出生后 [Delay, Delay+Duration)」这一个区间。
        /// 否则信号型 trigger 在 Duration 预算外到达时永远进不了窗口,策划意图「信号到 → 持续 N 秒」失效。
        /// </para>
        /// <para>
        /// 正确模型:StartTrigger 是「一次性粘性触发」—— 首次返回 true 时 _windowStarted=true,
        /// 之后 _windowElapsed 从 0 累加,直到 _windowElapsed >= Duration 才退出。
        /// 信号型 trigger 即使后续信号继续到达也不会重置 _windowElapsed。
        /// </para>
        /// </summary>
        public void Modify(LaserEntity laser, float deltaTime)
        {
            _elapsed += deltaTime;     // 时钟 A:出生至今,一直累加(供 trigger.ShouldActivate 用)

            // 1. 启动触发器判断(默认 DelayLaserModifierStartTrigger,与 PR1 旧 Delay 字段行为一致)
            //    ResetWindow 路径已经兜底;这里再兜一次,防外部直接 new LaserModifier().Modify 的边缘场景。
            if (StartTrigger == null)
            {
                StartTrigger = new DelayLaserModifierStartTrigger { Delay = 0f };
            }

            // 2. 计算窗口状态(Duration<=0 视为永久生效)
            bool wasActive     = _isActive;
            bool triggerReady  = StartTrigger.ShouldActivate(laser, _elapsed);
            // ★ 核心修复:Duration 是「窗口内持续时长」,从 _windowElapsed 读,不再是「出生后总预算」。
            bool durationExpired = Duration > 0f && _windowElapsed >= Duration;
            bool nowActive;

            if (!_windowStarted)
            {
                // 阶段 1:窗口从未触发过 —— triggerReady + Duration 未超期 → 触发并打粘性位
                //   (此时 _windowElapsed=0,只要 Duration>0 就永远不会超期,保证「信号到即激活」对所有 Duration 生效)
                //   Duration<=0 的永久型 modifier:SignalReady → 永久激活。
                nowActive = triggerReady && !durationExpired;
                if (nowActive)
                {
                    _windowStarted = true;
                    _windowElapsed = 0f;       // 进入窗口瞬间重置窗口时钟,Duration 从此刻起算
                }
            }
            else if (_windowExhausted)
            {
                // ★ 阶段 2-OneShot:OneShot 已触发过 → 彻底结束,不再激活、不再触发任何 enter/exit。
                //   _windowExhausted=true 后本分支直接锁死,后续所有帧 nowActive=false。
                nowActive = false;
            }
            else
            {
                // 阶段 2-持续:窗口已触发过 —— 一直 active 直到 Duration 到期(信号型 trigger 后续
                //   重复激活也不会重置 _windowElapsed)
                nowActive = !durationExpired;
            }

            // 3. 窗口内累加(必须在边缘判定之后,否则首次触发当帧 _windowElapsed 会被多算一帧)
            //   ★ _windowExhausted 时不累加(已经退出窗口)。
            if (nowActive) _windowElapsed += deltaTime;

            // 4. 边缘触发钩子
            if (nowActive && !wasActive)
            {
                _isActive = true;
                OnWindowEnter(laser);

                // OneShot:触发一次后立刻退出窗口(子类不会再被 ModifyCore)
                // ★ _windowExhausted=true 锁死,下一帧 _windowStarted=true 但 phase 2-OneShot 分支
                //   强制 nowActive=false → 不会再触发 OnWindowEnter 也不会再触发 OnWindowExit。
                if (OneShot)
                {
                    _isActive = false;
                    _windowExhausted = true;
                    OnWindowExit(laser);
                    return;
                }
            }
            else if (!nowActive && wasActive)
            {
                _isActive = false;
                OnWindowExit(laser);
            }

            // 5. 调度策略
            if (!nowActive && AutoSkipOutsideWindow) return;
            ModifyCore(laser, deltaTime);
        }

        /// <summary>
        /// 窗口从非激活 → 激活的瞬间调用一次。
        /// ★ OneShot modifier 的主战场:override 这里写「触发一次就结束」的逻辑(生成子激光 / 播音效 / 切贴图)。
        /// 普通 modifier 也可 override 做「进入追踪瞬间播 lock-on 音效」等副作用。
        /// </summary>
        protected virtual void OnWindowEnter(LaserEntity laser) { }

        /// <summary>
        /// 窗口从激活 → 非激活的瞬间调用一次(含 OneShot 触发后立刻退出、Duration 到期、激光回池)。
        ///
        /// <para>★ 默认模板(对齐 BulletModifier):</para>
        /// <para>
        /// 1. 调子类的 OnWindowExitCleanup(laser) —— 给子类追加自己的清理逻辑
        /// 2. <b>激光版基类不做额外清理</b>(对齐子弹版清 AngularSpeed 的对称性设计 —— 子弹版默认清 AngularSpeed 是因为子弹 modifier 大多通过 AngularSpeed 转向,退出后希望切直线;
        ///   激光 modifier 大多<b>直接重写 Angle/Position</b>,框架累积的 AngularVelocity 对我们无效,所以 noop)。
        /// </para>
        ///
        /// <para>对称性说明:</para>
        /// <list type="bullet">
        ///   <item>OnWindowEnter:默认空(进入窗口要做什么完全是子类的事,基类猜不出来)</item>
        ///   <item>OnWindowExit:默认调子类 OnWindowExitCleanup 后 noop(激光没有 AngularSpeed 等效字段)</item>
        /// </list>
        /// </summary>
        protected virtual void OnWindowExit(LaserEntity laser)
        {
            // 公共:子类 hook,子类可在这里追加自己的清理
            OnWindowExitCleanup(laser);
        }

        /// <summary>
        /// 子类 override 此方法做自己的清理。基类的 OnWindowExit 会在自己 noop 后调到这里。
        /// 详见 OnWindowExit 注释。
        /// </summary>
        protected virtual void OnWindowExitCleanup(LaserEntity laser) { }

        /// <summary>
        /// 子类 override 这个方法实现具体行为。
        /// 基类的 Modify() 已经在窗口外做了 return(除非 AutoSkipOutsideWindow=false),
        /// 或在 OneShot=true 时根本不会调用到这里。
        /// </summary>
        public abstract void ModifyCore(LaserEntity laser, float deltaTime);

        /// <summary>
        /// 激光被回池(LaserEntity.OnDisable / LaserPool.Return)时调用。
        /// 子类清理订阅 / 计时器等 per-instance 资源。默认无操作。
        ///
        /// <para>★ 与 StartTrigger.OnDetach 的区别 ★</para>
        /// <para>
        /// OnDetach(laser) 是 modifier 自己的清理钩子(子类 override);
        /// StartTrigger.OnDetach(laser) 是 trigger 的清理钩子,由 LaserEntity.AttachSignalTriggers 统一调用。
        /// 两者职责不重叠,不要混用。
        /// </para>
        /// </summary>
        public virtual void OnDetach(LaserEntity laser) { }

        /// <summary>
        /// 深拷贝。子类若持有引用类型字段(List/数组/自定义类),必须 override 本方法手动深拷。
        /// 默认实现 MemberwiseClone 对值类型字段足够 —— STG modifier 通常只有 float/int/Vector2,
        /// 性能开销约 10~30ns/次,STG 高弹量场景(&lt; 1000 条/秒)完全可忽略。
        /// ★ 计时器字段标了 [NonSerialized],Clone 出来自然为 0/false,符合预期(每条激光重新计时)。
        /// </summary>
        public virtual LaserModifier Clone()
        {
            var copy = (LaserModifier)MemberwiseClone();
            // ★ 深拷 StartTrigger:订阅型 trigger(订阅了 BulletSignalBus)的 handler 引用
            //   必须重新 Clone,否则多条激光共享同一 trigger 实例,会重复订阅 / 状态污染。
            if (StartTrigger != null) copy.StartTrigger = StartTrigger.Clone();
            return copy;
        }
    }
}
