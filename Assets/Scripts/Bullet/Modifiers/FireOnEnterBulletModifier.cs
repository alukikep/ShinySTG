using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// OneShot 版:子弹出生 <see cref="BulletModifier.Delay"/> 秒后,触发一次 <see cref="FirePatternBulletModifier.Pattern"/>,
/// 然后本 modifier 立刻结束。
///
/// 默认值:<see cref="BulletModifier.OneShot"/> = true(进入窗口瞬间调一次 OnWindowEnter)。
///
/// 典型用法(完整复用 FirePattern 能力):
///   1. 主炮挂本 modifier,Pattern = 一份 PlayerAim 的 RingFirePattern,Delay = 0.5
///      → 主炮飞 0.5 秒后,在自身位置按 RingFirePattern 发射一圈分裂弹
///      → 分裂弹自动获得 RingFirePattern 的 FireExtensions / ModifierPrefabs / SpawnFog
///   2. Boss 挂本 modifier,Pattern = 一份 PlayerAim 的 CompositeFirePattern(包含「追踪玩家」modifier)
///      → Boss 弹飞行 N 秒后爆出一组瞄准玩家的复合弹,分裂弹也带追踪
///
/// 与旧 SpawnRingOnDelayModifier 区别:
///   - 旧:自己写均分循环、阵营永远中性、不走 FireExtensions
///   - 新:走完整 FireGroup 链路、阵营可继承母弹、FireExtensions 全生效
/// </summary>
[Serializable, SRName("Modifier/Fire Pattern On Delay")]
public class FireOnEnterBulletModifier : FirePatternBulletModifier
{
    public FireOnEnterBulletModifier()
    {
        // ★ 默认开启 OneShot —— 「延迟 N 秒后触发一次」就是 OneShot 的标准用法
        OneShot = true;
    }

    protected override void OnWindowEnter(Bullet b)
    {
        // 复用基类的 FireOnce(走 BulletPool.FireGroup 完整链路)
        FireOnce(b);
    }

    public override void ModifyCore(Bullet b, float dt)
    {
        // OneShot=true → 基类不会调用本方法,留空即可。
        // 若 OneShot=false(用户主动关掉)→ 退化为「每帧在母弹位置开一次火组」的疯狂模式 —— 故意外,不优化。
    }
}
