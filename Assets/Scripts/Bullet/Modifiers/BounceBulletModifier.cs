using System;
using SerializeReferenceEditor;
using UnityEngine;
using ShinySTG.Stage; // BoundsService —— 反弹区域从场景单例读(与 Bullet.cs 越界回收共用同一矩形)

/// <summary>
/// 反弹 modifier —— 子弹飞出 <see cref="BoundsService.CullingArea"/> 时按物理规律反射方向并 Clamp 回区内,
/// 可配置反弹次数 / 可在哪些边反弹 / 时间窗口,以及恢复系数(能量损失)。
///
/// <para>★ 物理规律(反射向量公式):</para>
/// <code>
///   法线 n(墙内法向,指向区内):
///     右墙 n = (-1, 0), 左墙 n = (1, 0), 上墙 n = (0, -1), 下墙 n = (0, 1)
///   反射公式(恢复系数 e ∈ [0, 1]):
///     v' = v - (1 + e) * (v · n) * n
///   完全弹性(e = 1,默认):
///     v' = v - 2 * (v · n) * n      ← 经典物理「入射角 = 反射角」
///   速率(标量):
///     Speed' = Speed * e              ← e=1 速率不变,e<1 损失能量
///   实现细节:
///     - 水平翻转(R / L):v.x 取反 → SteerAngle' = π - SteerAngle
///     - 垂直翻转(T / B):v.y 取反 → SteerAngle' = -SteerAngle
///     - 角上同时碰两个边:两次翻转 = 完全反向(SteerAngle' = α - π)
/// </code>
///
/// <para>★ 协作边界:</para>
/// <list type="bullet">
///   <item>本 modifier 仅改 bullet.SteerAngle / bullet.Speed / bullet.transform.position(后两者是反射副作用)。</item>
///   <item><see cref="bullet.AngularSpeed"/> 反弹时不动 —— 反弹不打断 modifier 状态。</item>
///   <item>反弹触发的越界判定与 <see cref="Bullet"/> 第 5 步共用同一 <see cref="BoundsService.CullingArea"/>
///         + fallback ±10/±20(无 BoundsService 时)。</item>
///   <item>多个反弹 modifier 同时挂在同一颗弹上时,只有第一个返回 true 的生效。</item>
/// </list>
/// </summary>
[Serializable, SRName("Modifier/Bounce")]
public class BounceBulletModifier : BulletModifier
{
    /// <summary>反弹触发的墙面集合。</summary>
    public enum BounceWalls
    {
        /// <summary>只在左右两边反弹(垂直弹幕场景常用)。</summary>
        HorizontalOnly,
        /// <summary>只在上下两边反弹(水平弹幕场景常用)。</summary>
        VerticalOnly,
        /// <summary>四边都可反弹(默认)。</summary>
        All,
        /// <summary>除下边外,其他三边都反弹(上 + 左 + 右)。
        /// 撞到下边时不反弹,走原越界回收 —— 典型用法:朝下飞的子弹不想被地板弹回,只想让它「撞顶/撞墙弹,撞地回收」。</summary>
        ExceptBottom
    }

    [Header("Bounce")]
    [Tooltip("最多反弹次数。\n" +
             "<b>0 或正整数</b> = 扣到此数不再反弹,下一帧越界时直接回收(走 Bullet 原越界回收逻辑)。\n" +
             "<b>负数(如 -1)</b> = 无限反弹(每颗弹在窗口内永远不回收,除非时间窗口结束)。\n" +
             "默认 3。")]
    public int MaxBounces = 3;

    [Tooltip("可在哪些边反弹。\n" +
             "HorizontalOnly = 只左右;VerticalOnly = 只上下;All = 四边(默认);ExceptBottom = 除下边外其他三边(上+左+右)。\n" +
             "若子弹撞到不允许反弹的边,该次越界不反弹,走原越界回收。\n" +
             "典型用例:ExceptBottom = 玩家朝下打的反弹弹,只想让它撞顶/撞墙弹回,撞地时直接回收不弹。")]
    public BounceWalls Walls = BounceWalls.All;

    [Tooltip("恢复系数(物理学「弹性」概念),取值范围 [0, 2]。\n" +
             "★ 公式:反弹后速率 Speed' = Speed × Restitution(方向按反射公式翻)。\n" +
             "1.0(默认) = 完全弹性碰撞,速率不变(经典 STG 反弹直觉)。\n" +
             "0.95 = 轻微能量损失(每次反弹留 95% 速度)。\n" +
             "0.5 = 每次反弹掉一半速度。\n" +
             "0.0 = 反弹后整体速度归零。\n" +
             "> 1 = 反物理(子弹反弹后加速,STG 偶尔需要)。")]
    [Range(0f, 2f)]
    public float Restitution = 1f;

    // ─── per-instance 运行时状态 ───
    // [NonSerialized]:不参与序列化;通过 OnResetWindow 显式重置(每颗子弹独立计数)。
    [NonSerialized] int _remaining;
    [NonSerialized] bool _initialized;


    // ★ OnWindowEnter / OnWindowExitCleanup / OnWindowExit 都是基类的 protected virtual 钩子
    //   (基类负责调度,子类 override 时必须保持 protected,不能放宽成 public —— 否则 C# 报 CS0507)。
    //   见 BulletModifier.cs 第 118 / 140 / 153 行。
    protected override void OnResetWindow()
    {
        _remaining = 0;
        _initialized = false;
    }

    protected override void OnWindowEnter(Bullet bullet)
    {
        // 每次进入窗口(Delay 后第一次激活)时重置次数 —— 配合 Delay>0 场景:
        //   子弹出生后先飞 N 秒直线,然后「解锁」反弹并给满次数。
        // 配合 OneShot:OnWindowEnter 后基类会立刻 OnWindowExit,所以这里 _initialized 不需要再做什么。
        _remaining = MaxBounces;
        _initialized = true;
    }

    protected override void OnWindowExitCleanup(Bullet bullet)
    {
        // 退出窗口时清 _initialized,下次进窗口(子弹复用 + Delay>0)能再次进入 OnWindowEnter 重置 _remaining。
        _initialized = false;
    }

    public override void ModifyCore(Bullet bullet, float deltaTime)
    {
        // 反弹的实际逻辑在 TryBounceOnOutOfBounds 里(Bullet.Update 第 5 步之前调用)。
        // ModifyCore 这里是 abstract 必须 override 的,但没有「每帧持续做」的事可做。
        // 留空(注释占位,避免有人误以为没 override)。
    }


    public override bool TryBounceOnOutOfBounds(Bullet bullet)
    {
        // ─── 0. 守门:窗口外 / 未初始化 / 次数用尽 → 不反弹 ───
        if (!IsActive) return false;
        if (!_initialized) return false;
        if (MaxBounces >= 0 && _remaining <= 0) return false;

        // ─── 1. 取反弹区域(优先 BoundsService,fallback ±10/±20) ───
        Rect cull;
        var bs = BoundsService.Instance;
        if (bs != null)
        {
            cull = bs.CullingArea;
        }
        else
        {
            // 与 Bullet.cs 越界回收的 fallback 保持完全一致 —— 旧场景行为 100% 兼容。
            cull = new Rect(-10f, -10f, 20f, 20f);
        }

        // ─── 2. 判断越界方向(可能同时撞两边的角) ───
        Vector3 p = bullet.transform.position;
        bool outLeft   = p.x < cull.xMin;
        bool outRight  = p.x > cull.xMax;
        bool outBottom = p.y < cull.yMin;
        bool outTop    = p.y > cull.yMax;
        if (!outLeft && !outRight && !outBottom && !outTop)
        {
            // 不越界 —— 理论上 Bullet 不会在这里调我们(它先判越界再调),
            // 但防御性写法:万一边界判定边界 case 漏过,Bullet 会照常回收,这里提前 return false。
            return false;
        }

        // ─── 3. 计算反射方向(应用反射公式 + Restitution) ───
        //   水平翻转(R / L):v.x → -v.x  ←  SteerAngle' = π - SteerAngle
        //   垂直翻转(T / B):v.y → -v.y  ←  SteerAngle' = -SteerAngle
        //   角上碰两边的合成:SteerAngle' = α - π(完全反向,符合 v → -v)
        //
        //   ★ 数学验证(对任意 α):
        //     水平翻转后方向 (cos(π-α), sin(π-α)) = (-cos α, sin α) ✓
        //     垂直翻转后方向 (cos(-α), sin(-α))    = (cos α, -sin α)  ✓
        //     两次翻转(角上)方向 = 第一次水平: (-cos α, sin α);再垂直: (-cos α, -sin α) = -(cos α, sin α) ✓
        //
        //   速率:Speed' = Speed × Restitution(Restitution=1 时速率不变,完全弹性)
        //
        //   ★ 4 边独立判断:
        //     老版本用「canHorizontal / canVertical」两个全局开关,只能表达「能翻水平/能翻垂直」二选一。
        //     ExceptBottom 需要「左/右/上能翻,下不能翻」—— 必须升级到 4 个 per-edge bool。
        //     未来加 ExceptTop / ExceptLeft / ExceptRight 时,只需在下面的 switch 加 case,不动主逻辑。
        bool canBounceLeft, canBounceRight, canBounceTop, canBounceBottom;
        switch (Walls)
        {
            case BounceWalls.HorizontalOnly:
                canBounceLeft = canBounceRight = true;
                canBounceTop = canBounceBottom = false;
                break;
            case BounceWalls.VerticalOnly:
                canBounceLeft = canBounceRight = false;
                canBounceTop = canBounceBottom = true;
                break;
            case BounceWalls.All:
                canBounceLeft = canBounceRight = canBounceTop = canBounceBottom = true;
                break;
            case BounceWalls.ExceptBottom:
                canBounceLeft = canBounceRight = canBounceTop = true;
                canBounceBottom = false;
                break;
            default:
                // 未知的枚举值(理论上 switch 已穷尽,但编译器要 fallback)
                canBounceLeft = canBounceRight = canBounceTop = canBounceBottom = false;
                break;
        }

        // 角落同时越过禁止反弹边时也应回收，不能被另一条边救回。
        if ((outLeft && !canBounceLeft) || (outRight && !canBounceRight)
            || (outTop && !canBounceTop) || (outBottom && !canBounceBottom)) return false;

        bool flippedX = false, flippedY = false;
        // 水平翻转:左/右任一越界 + 对应边允许翻 → 翻。两侧分开判断是为了未来 ExceptLeft / ExceptRight 也能直接复用。
        if ((outLeft && canBounceLeft) || (outRight && canBounceRight))
        {
            bullet.SteerAngle = Mathf.PI - bullet.SteerAngle;
            flippedX = true;
        }
        // 垂直翻转:上/下任一越界 + 对应边允许翻 → 翻。
        if ((outTop && canBounceTop) || (outBottom && canBounceBottom))
        {
            // 注意:不论是否先翻了 X,翻 Y 永远等价于 SteerAngle = -SteerAngle
            //   (数学证明见上方「两次翻转」分支)
            bullet.SteerAngle = -bullet.SteerAngle;
            flippedY = true;
        }

        if (!flippedX && !flippedY)
        {
            // 配置不允许在当前越界方向反弹(如 Walls=HorizontalOnly 但撞了上下边,
            //   或 Walls=ExceptBottom 但撞了下边)
            // → 不算反弹,Bullet 走原越界回收。
            return false;
        }

        // ─── 4. 应用恢复系数(改变速率标量) ───
        //   Restitution=1 → 速率不变(经典物理);< 1 → 损失能量;> 1 → 加速(非物理但 STG 偶尔需要)。
        //   钳到 >= 0:负数没物理意义,且会导致 Speed 变负(等价反向飞行,违反「反射」直觉)。
        if (Restitution != 1f)
        {
            bullet.Speed = Mathf.Max(0f, bullet.Speed * Mathf.Max(0f, Restitution));
        }

        // ─── 5. Clamp 位置到 CullingArea 内(避免下一帧又在边界外触发第二次反弹 / 误判) ───
        Vector3 clamped = new Vector3(
            Mathf.Clamp(p.x, cull.xMin, cull.xMax),
            Mathf.Clamp(p.y, cull.yMin, cull.yMax),
            p.z);
        bullet.transform.position = clamped;

        // ─── 6. 扣次数(MaxBounces<0 表示无限,跳过扣减) ───
        if (MaxBounces > 0) _remaining--;

        return true;
    }
}
