using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 持续型:modifier 窗口期(<see cref="BulletModifier.Delay"/> 之后,持续 <see cref="BulletModifier.Duration"/> 秒)
/// 内,每 <see cref="Interval"/> 秒触发一次 <see cref="FirePatternBulletModifier.Pattern"/>。
///
/// 默认值:<see cref="BulletModifier.OneShot"/> = false(每 Interval 都触发,直到 Duration 到期)。
///
/// 典型用法:
///   - Boss 散弹母弹挂本 modifier,Interval = 0.2,Duration = 3,MaxShots = 10
///     → Boss 弹飞行 3 秒内,每 0.2 秒在自身位置生成一份子 pattern,最多 10 次
///   - 玩家追踪母弹挂本 modifier,Interval = 0.05,Duration = 1
///     → 飞 1 秒内每帧 / 每 0.05 秒生成拖尾小弹(经典「弹尾」表现)
///   - 持续激光 / 光束弹挂本 modifier,Interval = 0.033(≈ 30Hz),Duration = 2
///     → 2 秒内连续发射,~60 段拼接成光束
///
/// 防刷屏:
///   - <see cref="MaxShots"/> 上限(<= 0 = 不限)
///   - <see cref="Interval"/> 必须 > 0;若用户填 0 → 每帧触发 → 故意外
/// </summary>
[Serializable, SRName("Modifier/Fire Pattern While Active")]
public class FireOnDurationBulletModifier : FirePatternBulletModifier
{
    [Tooltip("每隔多少秒触发一次 FirePattern。\n" +
             "  0.033 ≈ 30Hz(每帧,激光拼接);\n" +
             "  0.1   ≈ 每秒 10 发(连射);\n" +
             "  0.2   ≈ 每秒 5 发(机枪);\n" +
             "  0.5   ≈ 每秒 2 发(节拍)。")]
    [Min(0f)] public float Interval = 0.1f;

    [Tooltip("窗口期内最多发射几次。\n" +
             "  <=0 = 不限次数,直到 Duration 到期;\n" +
             "  > 0 = 触发 N 次后,本 modifier 即使还在窗口期也不再触发(避免长时间运行刷屏)。")]
    public int MaxShots = -1;

    public override void ModifyCore(Bullet b, float dt)
    {
        // 累加到 Interval 后触发一次,扣减余数继续累加(允许实际周期有 jitter)
        if (!(dt > 0f) || float.IsInfinity(dt) || b == null || Pattern == null || BulletPool.Instance == null) return;
        if (MaxShots > 0 && _shotsFired >= MaxShots) return;
        if (!(Interval > 0f))
        {
            _accumulator = 0f;
            FireOnce(b);
            return;
        }
        _accumulator += dt;
        // 防御:Interval=0 时(用户在 Inspector 填 0)不要进死循环,直接退化为「每帧触发一次」
        float step = Interval > 0f ? Interval : dt;
        int bursts = 0;
        while (_accumulator >= step && bursts < FireCadence.MaxBurstsPerTick)
        {
            bursts++;
            _accumulator -= step;
            if (MaxShots > 0 && _shotsFired >= MaxShots)
            {
                _accumulator = 0f;
                return;
            }
            FireOnce(b);
            // 安全保险:FireOnce 失败时(Pattern 留空 / BulletPool 消失),不要死循环
            if (Pattern == null) return;
        }
        if (_accumulator >= step) _accumulator %= step;
    }
}
