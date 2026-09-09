using UnityEngine;

/// <summary>
/// 追踪子弹统一目标接口。HomingEnemyModifier 通过本接口识别候选,
/// 而非依赖具体 EnemyHealth / BossHealth 类型。
///
/// 实现约定(对齐 EnemyHealth / BossHealth 已有的公开属性):
///   - Position : 目标世界坐标(通常读 Hitbox.Position,无 Hitbox 时退回 transform.position)
///   - IsDead   : 目标是否已死亡
///
/// 新目标类型(可破坏道具、杂兵、中立 NPC ...)只需:
///   1. 实现本接口(通常 Position / IsDead 这两个属性本身就够)
///   2. 挂 HitboxComponent 子类且 Team=Enemy(或不挂 Hitbox 但 CollisionService 网格能拿到)
///
/// 设计动机:
///   - HomingEnemyModifier 不再写死 GetComponentInParent<EnemyHealth>,统一走 IHomingTarget
///   - Boss 与 Enemy 在追踪场景下等价,无需在 modifier 里写两次过滤逻辑
///   - 单元测试可写 IHomingTarget mock,不依赖 Unity GameObject 拓扑
///
/// ★ 注意:本类型为全局(无 namespace),与同目录 Bullet / BulletModifier / BulletPool 风格一致。
///   历史上曾放进 namespace ShinySTG.Bullet — 但 ShinySTG.Bullet 子命名空间会与全局 Bullet 类同名
///   (遮蔽:在 ShinySTG.* 命名空间内写 Bullet 时,C# 优先解析到 namespace 而非 type,触发 CS0118)。
///   详情见 CollisionService.cs / PlayerHealth.cs 注释。
/// </summary>
public interface IHomingTarget
{
    Vector2 Position { get; }
    bool    IsDead   { get; }
}