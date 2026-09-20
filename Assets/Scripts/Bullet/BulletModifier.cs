using System;
using SerializeReferenceEditor;
using UnityEngine;

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
             "详见 Assets/Scripts/Bullet/Triggers/ModifierStartTrigger.cs + ARCHITECTURE.md §2.8。")]
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

    [NonSerialized] float _elapsed;
    [NonSerialized] float _windowElapsed;
    [NonSerialized] bool _isActive;
    [NonSerialized] bool _windowStarted;
    [NonSerialized] bool _windowExhausted;
    [NonSerialized] bool _detached;

    public bool IsActive => _isActive;
    public float ElapsedInWindow => _windowElapsed;

    /// <summary>仅在未挂载时重置；运行中的实例应先 Detach。</summary>
    public void ResetWindow()
    {
        _elapsed = _windowElapsed = 0f;
        _isActive = _windowStarted = _windowExhausted = _detached = false;
        OnResetWindow();
    }

    /// <summary>子类显式重置运行状态，NonSerialized 不影响 MemberwiseClone。</summary>
    protected virtual void OnResetWindow() { }

    public void Modify(Bullet bullet, float deltaTime)
    {
        if (bullet == null || _detached || !(deltaTime > 0f) || float.IsInfinity(deltaTime)) return;
        if (_windowExhausted)
        {
            if (!OneShot && !AutoSkipOutsideWindow) ModifyCore(bullet, deltaTime);
            return;
        }

        float previousElapsed = _elapsed;
        _elapsed += deltaTime;
        float activeTime = deltaTime;
        if (!_windowStarted)
        {
            bool ready = StartTrigger != null
                ? StartTrigger.ShouldActivate(bullet, _elapsed)
                : _elapsed >= Mathf.Max(0f, Delay);
            if (!ready)
            {
                if (!OneShot && !AutoSkipOutsideWindow) ModifyCore(bullet, deltaTime);
                return;
            }
            activeTime = StartTrigger != null
                ? StartTrigger.GetActivationDelta(previousElapsed, _elapsed)
                : Mathf.Clamp(_elapsed - Mathf.Max(0f, Delay), 0f, deltaTime);
            activeTime = Mathf.Clamp(activeTime, 0f, deltaTime);
            _windowStarted = _isActive = true;
            // 触发资格已锁存，不再需要信号订阅。
            StartTrigger?.OnDetach(bullet);
            OnWindowEnter(bullet);
            if (_detached) return;
            if (OneShot)
            {
                FinishWindow(bullet);
                return;
            }
        }

        if (Duration > 0f) activeTime = Mathf.Min(activeTime, Mathf.Max(0f, Duration - _windowElapsed));
        _windowElapsed += activeTime;
        if (activeTime > 0f) ModifyCore(bullet, activeTime);
        if (!_detached && Duration > 0f && _windowElapsed >= Duration) FinishWindow(bullet);
    }

    void FinishWindow(Bullet bullet)
    {
        _isActive = false;
        _windowExhausted = true;
        OnWindowExit(bullet);
    }

    /// <summary>回池、移除或销毁时调用一次，与窗口到期分开。</summary>
    public void Detach(Bullet bullet)
    {
        if (_detached) return;
        _detached = true;
        StartTrigger?.OnDetach(bullet);
        if (_isActive) FinishWindow(bullet);
        OnDetach(bullet);
    }

    protected virtual void OnDetach(Bullet bullet) { }
    protected virtual void OnWindowEnter(Bullet bullet) { }
    // 基类不拥有速度、转向或外观，不应在退出时修改这些属性。
    protected virtual void OnWindowExit(Bullet bullet) => OnWindowExitCleanup(bullet);
    protected virtual void OnWindowExitCleanup(Bullet bullet) { }
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
    /// Clone 深拷触发器后调用 ResetWindow；子类通过 OnResetWindow 重置运行状态。
    /// </summary>
    public virtual BulletModifier Clone()
    {
        var copy = (BulletModifier)MemberwiseClone();
        // ★ 深拷 StartTrigger:订阅型 trigger(订阅了 BulletSignalBus)的 handler 引用
        //   必须重新 Clone,否则多颗子弹共享同一 trigger 实例,会重复订阅 / 状态污染。
        if (StartTrigger != null) copy.StartTrigger = StartTrigger.Clone();
        copy.ResetWindow();
        return copy;
    }
}
