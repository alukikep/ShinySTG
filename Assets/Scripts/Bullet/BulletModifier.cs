using System;
using SerializeReferenceEditor;
using UnityEngine;
using ShinySTG.EnemyAI; // EnemyHealth(追踪 modifier 需要引用敌人健康状态)

/// <summary>
/// 子弹行为的可插拔修饰(追踪/加速/减速/曲线/分裂...)。
/// 纯 C# 类(非 MonoBehaviour、非 ScriptableObject),
/// 走项目统一的 [SerializeReference] + [SRName] 扩展套路,
/// 在 FirePattern.ModifierPrefabs / FireAction.ExtraModifierPrefabs 里下拉选类型。
///
/// 运行时:每颗子弹会 Clone() 一份独立实例(默认 MemberwiseClone),
/// modifier 状态不会跨子弹污染 —— 安全地持有 _timer / _lockOnDelay 等 per-instance 字段。
///
/// 时间窗口(自 vX 起):所有 modifier 自动支持"Delay 后才生效 / Duration 秒后结束"。
///   - 默认 Delay=0 + Duration=0(=永久) + AutoSkipOutsideWindow=true → 行为与历史 100% 等价。
///   - 见 ARCHITECTURE.md §2.6 与字段 Tooltip。
///
/// 字段约定:
///   - 鼓励值类型(float/int/Vector2/...)—— 默认 Clone 即可,无额外开销
///   - UnityEngine.Object 引用(Transform 等)—— 默认 Clone 也安全(共享引用但很少写)
///   - 禁止 List&lt;T&gt; / T[] / 自定义类 —— 必须 override Clone 深拷
///   - 不要访问 this.transform —— modifier 不是 GameObject
/// </summary>
[Serializable]
public abstract class BulletModifier
{
    // ============================================================
    //  时间窗口字段(对所有子类生效;默认值与历史行为完全等价)
    // ============================================================

    [Header("Timing")]
    [Tooltip("从子弹生成开始,延迟多少秒后 modifier 才开始生效。\n" +
             "0 = 出生即生效(默认,与历史行为一致)。\n" +
             "典型用例:0.5s 后才激活追踪,前 0.5s 直线飞行的'假动作';或 1s 后才生成分裂弹(配合 OneShot)。")]
    [Min(0f)] public float Delay = 0f;

    [Tooltip("Modifier 启动触发器 —— 决定何时进入时间窗口。\n" +
             "默认 DelayStartTrigger(Delay=0,与历史 100% 等价)。\n" +
             "其他触发器(下拉选):\n" +
             "  - Trigger/Delay          : 等 Delay 秒(等价旧 Delay 字段)\n" +
             "  - Trigger/On Signal      : 订阅 BulletSignalBus 信号,收到即激活(可配 MaxWait / 距离判定)\n" +
             "  - Trigger/Delay Or Signal: Delay 与信号任一先到即激活\n" +
             "★ null = 用 DelayStartTrigger{Delay=this.Delay} 兜底,旧 .asset 无脑兼容。\n" +
             "详见 Assets/Scripts/Bullet/ModifierStartTrigger.cs + ARCHITECTURE.md §2.8。")]
    [SerializeReference, SR]
    public ModifierStartTrigger StartTrigger;

    [Tooltip("生效后持续多少秒。<b>&lt;=0 表示一直生效</b>(默认,与历史行为一致)。\n" +
             "典型用例:追踪 2 秒后切直线(燃料耗尽)、闪烁 1.5s 后熄灭、OneShot 触发后立刻结束。")]
    public float Duration = 0f;

    [Tooltip("Modifier 在窗口外(Delay 之前 / Duration 之后)是否完全禁用。\n" +
             "true(默认)= 直接 return,不调子类的 ModifyCore,子类完全不知道窗外的存在(简单可靠);\n" +
             "false = 仍每帧调用子类 ModifyCore,子类可通过 IsActive 自行判断(用于'窗外做插值/清理'等高级场景)。")]
    public bool AutoSkipOutsideWindow = true;

    [Tooltip("将本 modifier 标记为<b>一次性触发</b>:进入窗口瞬间调用 OnWindowEnter 一次,然后立刻退出(不再每帧调 ModifyCore)。\n" +
             "★ 用 OnWindowEnter(override)写一次性逻辑;ModifyCore 在 OneShot=true 时不会被调用。\n" +
             "典型用例:出生 N 秒后生成一圈分裂弹、播一次性音效、切换贴图。")]
    public bool OneShot = false;

    // per-instance 计时器(每颗子弹 Clone 时独立)
    // ★ 故意不复用 b.Lifetime:某些 modifier 会修改 b.Lifetime(ARCHITECTURE §2.5),
    //   会造成"自我修改自己计时器"的鸡生蛋问题。
    //
    // ★ 双时钟设计(自 vX 修复 Duration 语义后):
    //   - _elapsed          时钟 A:子弹出生至今。供 StartTrigger.ShouldActivate 用,始终累加。
    //   - _windowElapsed    时钟 B:窗口内累计时间。只在 _isActive=true 时累加,直接对接 Duration。
    //   - _windowStarted    ★ 粘性位:窗口是否「已触发过」。
    //                      用于:
    //                      1) OneShot 触发后由 _windowExhausted 锁死(见下),防止后续 frame 再触发 OnWindowEnter;
    //                      2) 信号型 trigger 即使信号持续到,也不会让 _windowElapsed 重置;
    //                      3) ResetWindow 时清零,让子弹回池复用时 OneShot 能再次触发。
    //   - _windowExhausted  ★ OneShot 专用锁死位:触发过一次 OnWindowEnter 后置 true,
    //                      后续 _windowStarted=true 但 nowActive=false,不再触发任何 enter/exit 钩子。
    [NonSerialized] float _elapsed;          // 时钟 A:出生至今
    [NonSerialized] float _windowElapsed;    // 时钟 B:窗口内累计(对接 Duration)
    [NonSerialized] bool  _isActive;         // 当前是否在窗口内
    [NonSerialized] bool  _windowStarted;    // ★ 窗口是否已触发过(粘性位)
    [NonSerialized] bool  _windowExhausted;  // ★ OneShot 已触发过 → 彻底结束,不再触发任何 enter/exit

    /// <summary>当前是否在时间窗口内。只读,供子类 / 外部查询。</summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// 窗口内累计时间(秒)。从窗口首次激活那一刻起算,Duration 内的每一帧累加。
    /// ★ 与 Duration 直接对接:ElapsedInWindow >= Duration 时窗口到期退出。
    /// ★ 与原"出生后多久"的旧实现不同 —— 旧实现是 _elapsed - Delay,语义混乱;
    ///   新实现保证「Duration=进入窗口后持续 N 秒」语义在所有 trigger 下都成立。
    /// </summary>
    public float ElapsedInWindow => _windowElapsed;

    /// <summary>
    /// 子弹生成时由 Bullet.Init 调用,重置计时器与激活状态。
    /// ★ 必须 ResetWindow 才能让 OneShot 在子弹复用时再次触发。
    /// </summary>
    public void ResetWindow()
    {
        // ★ 双时钟 + 两个粘性位一并清零 —— 必须四件套一起,否则:
        //   - 只清 _elapsed 不清 _windowStarted → 窗口永远激活(粘性位锁死);
        //   - 只清 _elapsed 不清 _windowElapsed → 复用时窗口一进就显示"快到期";
        //   - 只清 _elapsed 不清 _windowExhausted → OneShot 永远哑火。
        _elapsed          = 0f;
        _windowElapsed    = 0f;
        _isActive         = false;
        _windowStarted    = false;
        _windowExhausted  = false;

        // ★ 兼容兜底:StartTrigger == null → 新建 DelayStartTrigger 与旧 Delay 字段行为一致
        //   老 .asset 反序列化后 StartTrigger 字段是 null,必须在这里兜底,否则 Modify 第一帧 NRE。
        //   让「老 .asset 字段值 Delay=X」在 ResetWindow 调用时同步进新建的 StartTrigger.Delay,
        //   保证行为 100% 等价。
        //
        // ★ ★ ★ 关键修复(自 vX 起)★★★
        //   旧实现还会在 StartTrigger 非 null 时,把「基类 Delay 字段」强制同步进 trigger.Delay 字段。
        //   这是个隐藏 bug —— 策划在 Inspector 里给 DelayOrSignalStartTrigger.Delay 设了 2.0,
        //   但基类 Delay 默认是 0,ResetWindow 后 trigger.Delay 被偷偷改成 0,trigger 永远立即激活。
        //   现在:trigger 自己配的 Delay 字段是主,基类 Delay 仅在「StartTrigger=null」兜底分支
        //         生效(把基类 Delay 拷过去给新建的 DelayStartTrigger)。StartTrigger 非 null 时
        //         完全不动 trigger 内部字段 —— 让 trigger 自己管自己的 Delay。
        if (StartTrigger == null)
        {
            StartTrigger = new DelayStartTrigger { Delay = this.Delay };
            return;
        }
        // ★ 不再做「基类 Delay → trigger.Delay」同步 —— trigger 自己管自己的 Delay 字段。
    }

    /// <summary>
    /// 每帧由 Bullet.Update 调用。
    /// 基类统一管理时间窗口 + 边缘触发钩子,子类 override 的 ModifyCore() 只关心"在窗口内的行为"。
    /// ★ 此方法 sealed(不可 override),子类不可 override —— 强制所有 modifier 走基类窗口管理,避免漏改。
    ///
    /// <para>★ 时间窗口模型(自 vX 修复后):</para>
    /// <para>
    /// 「<b>何时进入窗口</b>」(由 StartTrigger 决定)和「<b>进入后持续多久</b>」(由 Duration 决定)
    /// 是两个独立的维度 —— 它们不能被压扁成「出生后 [Delay, Delay+Duration)」这一个区间。
    /// 否则信号型 trigger 在 Duration 预算外到达时永远进不了窗口,策划意图"信号到 → 持续 N 秒"失效。
    /// </para>
    /// <para>
    /// 正确模型:StartTrigger 是「一次性粘性触发」—— 首次返回 true 时 _windowStarted=true,
    /// 之后 _windowElapsed 从 0 累加,直到 _windowElapsed >= Duration 才退出。
    /// 信号型 trigger 即使后续信号继续到达也不会重置 _windowElapsed。
    /// </para>
    /// </summary>
    public void Modify(Bullet bullet, float deltaTime)
    {
        _elapsed += deltaTime;     // 时钟 A:出生至今,一直累加(供 trigger.ShouldActivate 用)

        // 1. 启动触发器判断(默认 DelayStartTrigger,与旧 Delay 字段行为一致)
        //    ResetWindow 路径已经兜底;这里再兜一次,防外部直接 new BulletModifier().Modify 的边缘场景。
        if (StartTrigger == null)
        {
            StartTrigger = new DelayStartTrigger { Delay = this.Delay };
        }

        // 2. 计算窗口状态(Duration<=0 视为永久生效)
        bool wasActive     = _isActive;
        bool triggerReady  = StartTrigger.ShouldActivate(bullet, _elapsed);
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
            //   旧实现只把 _windowStarted=true(粘性位),但下一帧 phase 2 仍走「!durationExpired」
            //   → nowActive=true → edge detected again → OnWindowEnter 反复触发 20 次。
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
            OnWindowEnter(bullet);

            // OneShot:触发一次后立刻退出窗口(子类不会再被 ModifyCore)
            // ★ _windowExhausted=true 锁死,下一帧 _windowStarted=true 但 phase 2-OneShot 分支
            //   强制 nowActive=false → 不会再触发 OnWindowEnter 也不会再触发 OnWindowExit。
            if (OneShot)
            {
                _isActive = false;
                _windowExhausted = true;
                OnWindowExit(bullet);
                return;
            }
        }
        else if (!nowActive && wasActive)
        {
            _isActive = false;
            OnWindowExit(bullet);
        }

        // 5. 调度策略
        if (!nowActive && AutoSkipOutsideWindow) return;
        ModifyCore(bullet, deltaTime);
    }

    /// <summary>
    /// 窗口从非激活 → 激活的瞬间调用一次。
    /// ★ OneShot modifier 的主战场:override 这里写"触发一次就结束"的逻辑(生成弹 / 播音效 / 切贴图)。
    /// 普通 modifier 也可 override 做"进入追踪瞬间播 lock-on 音效"等副作用。
    /// </summary>
    protected virtual void OnWindowEnter(Bullet bullet) { }

    /// <summary>
    /// 窗口从激活 → 非激活的瞬间调用一次(含 OneShot 触发后立刻退出、Duration 到期、子弹回池)。
    ///
    /// ★ 默认模板(★ 所有 modifier 自动获得 ★):
    ///   1. 清零 b.AngularSpeed —— 让 modifier 退出后子弹切直线
    ///      (这是几乎所有 modifier 的共同期望:Steer / Homing / 螺旋 / 蛇形 / 振荡 ...
    ///       退出后都应该回归直线飞行,而不是"保留最后角速度继续转")
    ///   2. 调子类的 OnWindowExitCleanup(bullet) —— 给子类追加自己的清理逻辑
    ///
    /// 子类要追加自己的清理时,override <b>OnWindowExitCleanup</b>(不要 override OnWindowExit),
    /// 这样基类保证 AngularSpeed 一定被清,不会有人忘 override 导致子弹"残留状态"。
    ///
    /// 典型需要 OnWindowExitCleanup 的场景:
    ///   - 视觉类 modifier:恢复到 prefab 原色 / 停止粒子
    ///   - 自定义运行时状态:清零内部计时器、释放引用
    ///
    /// 对称性说明:
    ///   - OnWindowEnter:默认空(进入窗口要做什么完全是子类的事,基类猜不出来)
    ///   - OnWindowExit:默认清 AngularSpeed(退出窗口几乎都希望切直线,这是公共需求)
    /// </summary>
    protected virtual void OnWindowExit(Bullet bullet)
    {
        // 公共清理:modifier 退出后切直线
        bullet.AngularSpeed = 0f;

        // 子类 hook:子类可在这里追加自己的清理
        OnWindowExitCleanup(bullet);
    }

    /// <summary>
    /// 子类 override 此方法做自己的清理。基类的 OnWindowExit 会在清完 AngularSpeed 后调到这里。
    /// 详见 OnWindowExit 注释。
    /// </summary>
    protected virtual void OnWindowExitCleanup(Bullet bullet) { }

    /// <summary>
    /// 子类 override 这个方法实现具体行为。
    /// 基类的 Modify() 已经在窗口外做了 return(除非 AutoSkipOutsideWindow=false),
    /// 或在 OneShot=true 时根本不会调用到这里。
    /// </summary>
    public abstract void ModifyCore(Bullet bullet, float deltaTime);

    /// <summary>
    /// 越界反弹钩子。Bullet.Update 在越界回收判定之前调用每个 modifier 的本方法。
    /// 子类(典型如 <see cref="BounceBulletModifier"/>)override 这里实现"碰边翻转方向 + Clamp 位置 + 扣次数"。
    ///
    /// <para>返回值语义:</para>
    /// <list type="bullet">
    ///   <item><c>true</c> = 本 modifier 已处理本次越界(包括:翻转 SteerAngle、按需改 Speed、
    ///         Clamp 位置写入 <paramref name="bullet"/>.transform.position、扣内部计数)。
    ///         Bullet.Update 收到 true 就跳过本次越界回收,继续走下一帧。</item>
    ///   <item><c>false</c> = 本 modifier 不处理本次越界。Bullet.Update 走原越界回收逻辑(回收子弹)。</item>
    /// </list>
    ///
    /// <para>★ 设计要点:</para>
    /// <list type="bullet">
    ///   <item>modifier 自行决定翻转哪条边 / 翻哪个分量 / 扣几次 —— Bullet 只调一次位置 Clamp,
    ///         不抢位置写入权。</item>
    ///   <item>基类默认返回 false —— 99% 的现有 modifier(加速 / 转向 / 追踪 / 染色 / 分裂)无需关心反弹。</item>
    ///   <item>BounceBulletModifier 会遵守 <see cref="IsActive"/> —— 窗口外不反弹(可叠加在时间窗口外禁止反弹)。</item>
    ///   <item>多个 modifier 都 override 并都返回 true 时,Bullet 只认第一个,后续跳过(防互相覆盖 SteerAngle)。</item>
    /// </list>
    /// </summary>
    /// <param name="bullet">当前子弹(供读 SteerAngle / Speed / 写 transform.position)。</param>
    /// <returns>true = 已处理越界,Bullet 跳过本次回收;false = 不处理,Bullet 走原越界回收。</returns>
    public virtual bool TryBounceOnOutOfBounds(Bullet bullet) => false;

    /// <summary>
    /// 深拷贝。子类若持有引用类型字段(List/数组/自定义类),必须 override 本方法手动深拷。
    /// 默认实现 MemberwiseClone 对值类型字段足够 —— STG modifier 通常只有 float/int/Vector2,
    /// 性能开销约 10~30ns/次,STG 高弹量场景(< 1000 颗/秒)完全可忽略。
    /// ★ 计时器字段标了 [NonSerialized],Clone 出来自然为 0/false,符合预期(每颗子弹重新计时)。
    /// </summary>
    public virtual BulletModifier Clone()
    {
        var copy = (BulletModifier)MemberwiseClone();
        // ★ 深拷 StartTrigger:订阅型 trigger(订阅了 BulletSignalBus)的 handler 引用
        //   必须重新 Clone,否则多颗子弹共享同一 trigger 实例,会重复订阅 / 状态污染。
        if (StartTrigger != null) copy.StartTrigger = StartTrigger.Clone();
        return copy;
    }
}

/// 示例：加速
[Serializable, SRName("Modifier/Accelerate")]
public class AccelerateModifier : BulletModifier
{
    public float Acceleration = 5f;
    public override void ModifyCore(Bullet b, float dt) => b.Speed += Acceleration * dt;
}

/// <summary>
/// 示例：按固定角速度转向(螺旋 / 弧线 / 蛇形 等)。
///
/// 行为:从子弹生成那一刻起,以 TurnRate (度/秒) 的角速度持续旋转。
/// 不依赖任何 Target,行为简单可预测 —— 设多少转多少,正数=逆时针,负数=顺时针。
///
/// 实现细节:modifier 每帧把 b.AngularSpeed 覆写为 TurnRate (度 → 弧度)。
/// Bullet.Update 第 2 步会做 SteerAngle += AngularSpeed * dt,所以效果等价于"持续旋转"。
/// </summary>
[Serializable, SRName("Modifier/Steer")]
public class SteerTowardModifier : BulletModifier
{
    [Tooltip("角速度(度/秒)。正数=逆时针,负数=顺时针。0=不转。\n" +
             "90 = 1/4 秒转 90°(常见螺旋弹);360 = 1 秒转一圈。")]
    public float TurnRate = 90f;

    public override void ModifyCore(Bullet b, float dt)
    {
        // 直接把 AngularSpeed 设为 TurnRate(弧度)。
        // Bullet.Update 第 2 步会自动把它累加到 SteerAngle,无需在这里直接改 SteerAngle。
        b.AngularSpeed = TurnRate * Mathf.Deg2Rad;
    }
}

/// <summary>
/// 玩家弹追踪最近敌人。
///
/// 行为流程(每帧):
///   1. LockOnDelay 期:不动,子弹直线飞行
///   2. 首次搜索:通过 CollisionService.Grid.QueryRadius(b.Position, SearchRadius) 拿半径内所有 hitbox,
///      按 Team==Enemy 过滤,选距离最近(没用 IsDead/无引用)的作为 _target
///   3. 已锁定时:每帧检查 _target 是否仍存活 + 仍在 SearchRadius 内;任一条件不满足 → 重搜
///   4. 锁定目标后:计算"子弹 → 目标"的方向角,与当前 SteerAngle 求最短有向角差,
///      按 TurnRate 限速后写入 b.AngularSpeed(由 Bullet.Update 累加到 SteerAngle)
///
/// 注意:
///   - 只能挂玩家阵营的子弹上(语义约束:谁追击敌人)。
///   - 通过 IHomingTarget 接口识别目标,EnemyHealth 和 BossHealth 都可被锁定(已统一实现接口)。
///   - modifier 不持有 target 的 Transform 引用,只持有 IHomingTarget 引用(避免 target Destroy 时伪 null 漏检)。
///   - 默认值:SearchRadius=6,TurnRate=180,LockOnDelay=0,MaxHomingTime=-1。
/// </summary>
[Serializable, SRName("Modifier/Homing Enemy")]
public class HomingEnemyModifier : BulletModifier
{
    [Header("Search")]
    [Tooltip("搜索半径(世界单位)。从子弹位置向外扩 SearchRadius 圈 cell,选最近敌人。\n" +
             "默认 6 覆盖 1.5 cell(类似 3×3);值越大越早锁定,代价是 O(候选) 距离过滤开销。\n" +
             "CellSize=4 时:6≈3×3,8≈5×5,12≈7×7。")]
    public float SearchRadius = 6f;

    [Header("Tracking")]
    [Tooltip("追踪性能(方向修正能力)= 角速度上限(度/秒)。\n" +
             "0 = 不转(直线);180 = 经典 STG 追踪;360 = 1 秒转 90°;720 = 激进锁头。")]
    public float TurnRate = 180f;

    [Header("Timing")]
    [Tooltip("锁定延迟(秒)。前 N 秒不搜索不转向,子弹直线飞一段再开始追踪。\n" +
             "0 = 立即追踪(默认);经典用法 0.2~0.5 配合扇形扩散发射。")]
    public float LockOnDelay = 0f;

    [Tooltip("最大追踪时长(秒)。超过后停止搜索+转向,转直线飞行。\n" +
             "-1 = 永远追踪(默认);3~5 秒模拟'燃料耗尽'。")]
    public float MaxHomingTime = -1f;

    // ── per-instance 状态(Clone 复制,安全) ──
    float _timer;
    IHomingTarget _target;                   // null = 未锁定;EnemyHealth / BossHealth 都可
    bool _warnedNoService;                   // 是否已打印过"无 CollisionService"警告(每颗弹只警告一次)

    public override void ModifyCore(Bullet b, float dt)
    {
        _timer += dt;

        // 1. LockOnDelay 期 → 直线飞行
        // ★ 这里的 LockOnDelay / MaxHomingTime 是 modifier 自己的窗口(从生成开始计),
        //   与基类的 Delay/Duration 正交共存(详见 ARCHITECTURE §2.6)。
        //   - 用基类 Delay = "modifier 整体不工作"
        //   - 用这里的 LockOnDelay = "modifier 工作但不搜索"
        if (_timer < LockOnDelay)
        {
            b.AngularSpeed = 0f;  // ★ 找不到目标 / 未到时机 → 角速度清零,Bullet.Update 累加后 SteerAngle 不变(直线)
            return;
        }

        // 2. MaxHomingTime 超时 → 直线飞行
        if (MaxHomingTime > 0f && _timer > LockOnDelay + MaxHomingTime)
        {
            b.AngularSpeed = 0f;  // ★ "燃料耗尽" → 清零,行为与文档一致
            return;
        }

        // 3. 拿服务(可能在子弹飞行过程中场景被卸载)
        var cs = ShinySTG.Hitbox.CollisionService.Instance;
        if (cs == null || cs.Grid == null)
        {
            if (!_warnedNoService)
            {
                Debug.LogWarning(
                    $"[HomingEnemyModifier] CollisionService 未挂载,追踪 modifier 失效。" +
                    $"场景里需要一个 CollisionService 才能让 Homing Enemy modifier 工作。", b);
                _warnedNoService = true;
            }
            b.AngularSpeed = 0f;  // ★ 拿不到服务 → 直线
            return;
        }

        // 4. 目标失效检测 / 移出范围 → 重搜
        if (!IsTargetValid(b))
        {
            _target = FindNearestTarget(b, cs);
            // ★ 同 IsTargetValid 的接口伪 null 防御:_target 是 IHomingTarget 接口引用,
            //   普通 == 不会识别"已 Destroy 但 C# 引用还在"的情况(虽然 FindNearestTarget
            //   内部只会返回当前活跃 hitbox 上的组件,但加这层防御 0 成本)。
            //   标准模式:`as UnityEngine.Object` 后判 == null,既能识别真 null 也能识别伪 null。
            if (_target as UnityEngine.Object == null)
            {
                b.AngularSpeed = 0f;  // ★ 找不到目标 → 清零,子弹保持当前 SteerAngle 直线飞行
                return;
            }
        }

        // 5. 计算到目标的最短有向角差 + 限速
        //    ★ 把 Position 缓存到局部变量,后续不再访问 _target 的 Unity API
        //      (虽然 IsTargetValid 已保证 _target 存活,但防御性写法避免本帧内 _target 被销毁)
        Vector2 targetPos = _target.Position;
        Vector2 toTarget = targetPos - b.Position;
        float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x);
        float delta = Mathf.Atan2(
            Mathf.Sin(targetAngle - b.SteerAngle),
            Mathf.Cos(targetAngle - b.SteerAngle)
        );

        float maxRadPerSec = TurnRate * Mathf.Deg2Rad;
        // 把 delta/dt 限制在 ±maxRadPerSec;直接给 b.AngularSpeed,Bullet.Update 累加。
        b.AngularSpeed = Mathf.Clamp(delta / dt, -maxRadPerSec, maxRadPerSec);
    }

    /// <summary>
    /// 目标是否仍有效:引用未死 + 未超出 SearchRadius。
    ///
    /// ★ 关键防御:`_target` 是 <see cref="IHomingTarget"/> 接口引用。
    ///   接口的 `==` 不走 UnityEngine.Object 的伪 null 重载 —— 敌人 GameObject
    ///   被 Destroy 后,C# 引用还在,`if (_target == null)` 永远 false,
    ///   直接调到 `_target.Position` 会抛 MissingReferenceException。
    ///   必须先 cast 到 UnityEngine.Object 才能识别"已被 Destroy 的 GameObject 上的组件"。
    /// </summary>
    bool IsTargetValid(Bullet b)
    {
        // ★ IHomingTarget 是接口,接口的 == 不会触发 Unity 的"伪 null"检测。
        //   标准模式:`as UnityEngine.Object` 后判 == null,既能识别真 null 也能识别伪 null
        //   (已被 Destroy 但 C# 引用还在的情况,常见于敌人死亡 Destroy 后下一帧追踪弹还在 tick)。
        if (_target as UnityEngine.Object == null) return false;        // 真 null 或伪 null
        if (_target.IsDead) return false;                              // 死亡(HP <= 0)
        // 目标位置离子弹太远 → 视作失效,重搜
        if (((Vector2)b.Position - _target.Position).sqrMagnitude > SearchRadius * SearchRadius)
            return false;
        return true;
    }

    /// <summary>
    /// 一次 QueryRadius 拿半径内所有 hitbox,过滤 Enemy 阵营,反查 IHomingTarget 组件,选最近。
    /// 等价于"先查自己 cell → 扩展到 3×3 → 5×5 ..."(因为单次 query 拿所有候选,选最近是同一个结果)。
    ///
    /// 反查用 GetComponentInParent<IHomingTarget>():一次 ComponentInParent 调用覆盖 EnemyHealth / BossHealth 两种类型,
    /// 新目标类型只需加 : IHomingTarget,无需改本方法。
    /// </summary>
    IHomingTarget FindNearestTarget(Bullet b, ShinySTG.Hitbox.CollisionService cs)
    {
        var hits = cs.Grid.QueryRadius(b.Position, SearchRadius);
        IHomingTarget best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < hits.Count; i++)
        {
            var hb = hits[i];
            if (hb == null) continue;
            if (hb.Team != ShinySTG.Hitbox.CollisionTeam.Enemy) continue;

            // 拿到 hitbox 对应的 IHomingTarget(EnemyHealth 或 BossHealth)。
            // 性能:这里走 GetComponentInParent ~200ns,但只在"首次搜索"时跑一次,
            // 后续锁定后每帧只做 IsTargetValid + Atan2 转向,不再 GetComponent。
            var t = hb.GetComponentInParent<IHomingTarget>();
            if (t == null || t.IsDead) continue;

            float sqr = ((Vector2)b.Position - t.Position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }
        return best;
    }
}

// 原 SpawnRingOnDelayModifier(OneShot + 延迟分裂样例)已弃用并删除 ——
// 通用能力(延迟/持续型 + 任意 FirePattern + 阵营透传)由 FirePatternBulletModifier
// 及其子类 FireOnEnter / FireOnDuration 提供,详见 Assets/Scripts/Bullet/FirePatternBulletModifier.cs
// 与 ARCHITECTURE.md §2.4。
