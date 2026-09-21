using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 玩家弹追踪最近敌人。
///
/// 行为流程(每帧):
///   1. LockOnDelay 期:不动,子弹直线飞行
///   2. 首次搜索:通过 CollisionService.Grid.QueryRadius(b.Position, Mathf.Max(0f, SearchRadius)) 拿半径内所有 hitbox,
///      按 Team==Enemy 过滤,选距离最近(没用 IsDead/无引用)的作为 _target
///   3. 已锁定时:每帧检查 _target 是否仍存活 + 仍在 SearchRadius 内;任一条件不满足 → 重搜
///   4. 锁定目标后:计算"子弹 → 目标"的方向角,与当前 SteerAngle 求最短有向角差,
///      按 TurnRate 与近距离辅助限速后提交本帧转角。
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
    [Min(0f)]
    [Tooltip("基础转向上限(度/秒)，同时控制近距离辅助强度。0 = 不转向；数值越大诱导越强。")]
    public float TurnRate = 180f;

    [Min(0f)]
    [Tooltip("近距离辅助范围(世界单位)。进入后按弹速和距离逐渐提高转向上限，缓解绕圈。\n" +
             "0 = 使用原固定上限；范围应覆盖绕行轨迹。弱诱导不保证命中。")]
    public float AssistDistance = 6f;

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

    protected override void OnResetWindow()
    {
        _timer = 0f;
        _target = null;
        _warnedNoService = false;
    }

    protected override void OnDetach(Bullet bullet) => _target = null;
    protected override void OnWindowExitCleanup(Bullet bullet)
    {
        bullet.ClearModifierTurnRate();
        _target = null;
    }

    public override void ModifyCore(Bullet b, float dt)
    {
        float previousTime = _timer;
        _timer += dt;
        float trackingTime = Mathf.Max(0f, _timer - Mathf.Max(previousTime, LockOnDelay));
        if (MaxHomingTime > 0f)
            trackingTime = Mathf.Min(trackingTime, Mathf.Max(0f, LockOnDelay + MaxHomingTime - Mathf.Max(previousTime, LockOnDelay)));

        // 1. LockOnDelay 期 → 直线飞行
        // ★ 这里的 LockOnDelay / MaxHomingTime 是 modifier 自己的窗口(从 modifier 生效开始计),
        //   与基类的 Delay/Duration 正交共存(详见 ARCHITECTURE §2.6)。
        //   - 用基类 Delay = "modifier 整体不工作"
        //   - 用这里的 LockOnDelay = "modifier 工作但不搜索"
        if (trackingTime <= 0f)
        {
            b.SetModifierTurn(0f, dt);  // ★ 找不到目标 / 未到时机 → 角速度清零,Bullet.Update 累加后 SteerAngle 不变(直线)
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
            b.SetModifierTurn(0f, dt);  // ★ 拿不到服务 → 直线
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
                b.SetModifierTurn(0f, dt);  // ★ 找不到目标 → 清零,子弹保持当前 SteerAngle 直线飞行
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

        float maxRadPerSec = Mathf.Max(0f, TurnRate) * Mathf.Deg2Rad;
        if (AssistDistance > 0f && TurnRate > 0f)
        {
            float distance = toTarget.magnitude;
            float assist = 1f - Mathf.Clamp01(distance / AssistDistance);
            // v / d 随接近目标而增大，使允许的转弯半径随距离缩小。
            // 保留 TurnRate 对近处诱导强度的控制，不强制弱诱导必定命中。
            float closeRate = (TurnRate / 90f) * Mathf.Abs(b.Speed) / Mathf.Max(distance, 0.01f);
            maxRadPerSec += closeRate * assist;
        }
        // 夹角限制避免单帧转过目标方向；仍使用裁剪后的追踪时长。
        b.SetModifierTurn(Mathf.Clamp(delta / trackingTime, -maxRadPerSec, maxRadPerSec), trackingTime);
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
        if (_target is Behaviour behaviour && !behaviour.isActiveAndEnabled) return false;
        if (_target.IsDead) return false;                              // 死亡(HP <= 0)
        // 目标位置离子弹太远 → 视作失效,重搜
        if (((Vector2)b.Position - _target.Position).sqrMagnitude > Mathf.Max(0f, SearchRadius) * Mathf.Max(0f, SearchRadius))
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
        var hits = cs.Grid.QueryRadius(b.Position, Mathf.Max(0f, SearchRadius));
        IHomingTarget best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < hits.Count; i++)
        {
            var hb = hits[i];
            if (hb == null || !hb.isActiveAndEnabled) continue;
            if (hb.Team != ShinySTG.Hitbox.CollisionTeam.Enemy) continue;

            // 拿到 hitbox 对应的 IHomingTarget(EnemyHealth 或 BossHealth)。
            // 性能:这里走 GetComponentInParent ~200ns,但只在"首次搜索"时跑一次,
            // 后续锁定后每帧只做 IsTargetValid + Atan2 转向,不再 GetComponent。
            var t = hb.GetComponentInParent<IHomingTarget>();
            if (t as UnityEngine.Object == null || t.IsDead) continue;
            if (t is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;

            float sqr = ((Vector2)b.Position - t.Position).sqrMagnitude;
            if (sqr <= Mathf.Max(0f, SearchRadius) * Mathf.Max(0f, SearchRadius) && sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }
        return best;
    }
}
