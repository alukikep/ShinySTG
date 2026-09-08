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
/// 字段约定:
///   - 鼓励值类型(float/int/Vector2/...)—— 默认 Clone 即可,无额外开销
///   - UnityEngine.Object 引用(Transform 等)—— 默认 Clone 也安全(共享引用但很少写)
///   - 禁止 List&lt;T&gt; / T[] / 自定义类 —— 必须 override Clone 深拷
///   - 不要访问 this.transform —— modifier 不是 GameObject
/// </summary>
[Serializable]
public abstract class BulletModifier
{
    /// 每帧由 Bullet.Update 调用。
    public abstract void Modify(Bullet bullet, float deltaTime);

    /// <summary>
    /// 深拷贝。子类若持有引用类型字段(List/数组/自定义类),必须 override 本方法手动深拷。
    /// 默认实现 MemberwiseClone 对值类型字段足够 —— STG modifier 通常只有 float/int/Vector2,
    /// 性能开销约 10~30ns/次,STG 高弹量场景(< 1000 颗/秒)完全可忽略。
    /// </summary>
    public virtual BulletModifier Clone() => (BulletModifier)MemberwiseClone();
}

/// 示例：加速
[Serializable, SRName("Modifier/Accelerate")]
public class AccelerateModifier : BulletModifier
{
    public float Acceleration = 5f;
    public override void Modify(Bullet b, float dt) => b.Speed += Acceleration * dt;
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

    public override void Modify(Bullet b, float dt)
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
///   - Boss 不在 EnemyHealth.Alive 中,因此不会被追踪(架构上 Boss 走 BossHealth,正交)。
///   - modifier 不持有 target 的 Transform 引用,只持有 EnemyHealth(避免 target Destroy 时伪 null 漏检)。
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
    EnemyHealth _target;       // null = 未锁定
    bool _warnedNoService;     // 是否已打印过"无 CollisionService"警告(每颗弹只警告一次)

    public override void Modify(Bullet b, float dt)
    {
        _timer += dt;

        // 1. LockOnDelay 期 → 直线飞行
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
            _target = FindNearestEnemy(b, cs);
            if (_target == null)
            {
                b.AngularSpeed = 0f;  // ★ 找不到目标 → 清零,子弹保持当前 SteerAngle 直线飞行
                return;
            }
        }

        // 5. 计算到目标的最短有向角差 + 限速
        Vector2 toTarget = _target.Position - b.Position;
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
    /// </summary>
    bool IsTargetValid(Bullet b)
    {
        if (_target == null) return false;     // 伪 null(已 Destroy)
        if (_target.IsDead) return false;      // 死亡(HP <= 0,但 OnDisable 还没触发)
        // 目标位置离子弹太远 → 视作失效,重搜
        if (((Vector2)b.Position - _target.Position).sqrMagnitude > SearchRadius * SearchRadius)
            return false;
        return true;
    }

    /// <summary>
    /// 一次 QueryRadius 拿半径内所有 hitbox,过滤 Enemy 阵营,选最近。
    /// 等价于"先查自己 cell → 扩展到 3×3 → 5×5 ..."(因为单次 query 拿所有候选,选最近是同一个结果)。
    /// </summary>
    EnemyHealth FindNearestEnemy(Bullet b, ShinySTG.Hitbox.CollisionService cs)
    {
        var hits = cs.Grid.QueryRadius(b.Position, SearchRadius);
        EnemyHealth best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < hits.Count; i++)
        {
            var hb = hits[i];
            if (hb == null) continue;
            if (hb.Team != ShinySTG.Hitbox.CollisionTeam.Enemy) continue;

            // 拿到 hitbox 对应的 EnemyHealth。
            // 性能:这里走 GetComponentInParent ~200ns,但只在"首次搜索"时跑一次,
            // 后续锁定后每帧只做 IsTargetValid + Atan2 转向,不再 GetComponent。
            var e = hb.GetComponentInParent<EnemyHealth>();
            if (e == null || e.IsDead) continue;

            float sqr = ((Vector2)b.Position - e.Position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = e;
            }
        }
        return best;
    }
}
