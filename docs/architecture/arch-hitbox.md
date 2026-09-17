# Hitbox 系统

> 统一 AABB + 阵营 + 网格空间索引

> 本板块对应 ARCHITECTURE § 8. Hitbox 系统(统一 AABB)(原 ARCHITECTURE.md 第 1084–1119 行)。
> 本文档面向项目维护者,不复制实现细节,字段 / 数值 / 默认值以源文件为准。

---

## 8. Hitbox 系统(统一 AABB)

碰撞检测**走数学计算、不依赖 Unity Physics2D / Collider**。理由:STG 弹量大,纯几何 O(1) 比物理引擎快;且代码可控、可在 Scene 视图直接可视化。

**职责分工:**
- `HitboxComponent` —— 通用 MonoBehaviour,Inspector 配 Size / Gizmos 颜色,提供 Overlaps / OverlapsPoint 接口。
- `HitboxMath` —— 静态数学库,AABB×AABB 一个函数,O(1) 零分配。
- `UniformGrid` —— 均匀网格空间索引,内部 `Dictionary<cellKey, List<HitboxComponent>>`。公开 `Insert(hb)`(单 cell 插入,基于 `hb._cachedBounds.center`)、`Query3x3(center)`(中心 + 8 邻)、`QueryRadius(center, radius)`(中心 + 外扩 r 圈,追踪 modifier 用这个)。见下文"网格作为查询基础设施"。
- `CollisionService` —— 场景单例,`LateUpdate` 跑"子弹 vs 实体"判定;**维护一份共享的 `UniformGrid` 实例**,每帧开头 Clear + 重建(玩家 + 敌人 + 子弹全部 Insert),通过公开只读属性 `Grid` 暴露给其他系统(modifier 等)查询用。

**关键设计:**

- **统一 AABB** —— 全项目只用轴对齐矩形碰撞盒,`transform.rotation` 不影响形状,`transform.lossyScale` 实时反映。
- **正交层** —— Hitbox 与 Player / Enemy / Bullet 完全解耦,谁的 size 谁决定;玩家死亡时不被碰撞逻辑拖着走。
- **数学入口单一** —— 所有"是否相交"都走 `HitboxMath.AABBOverlap`,后续要扩圆 / 胶囊 / 多边形,只需在 HitboxMath 里加分支。
- **阵营语义**:`HitboxComponent.Team` 有 Neutral / Player / Enemy 三种 —— 玩家敌人由子类静态配置,子弹由发射者透传(prefab 不需要手动配 Team)。
- **未来扩展形状的留口子** —— 若要加圆 / 胶囊 / 多边形:抽 `HitboxShape` 抽象基类,把 `Size` / `Overlaps` / `DrawGizmos` 挪到子类,HitboxComponent 接口不动。

**网格作为查询基础设施(不只是碰撞用)**

- `CollisionService` 维护的 `Grid` 是项目里**唯一一份**活跃 hitbox 的空间索引 —— 不再有"碰撞用一份网格 / 追踪用另一份网格"的双重维护。
- 任何需要"看到场景里其他 hitbox"的系统(追踪 modifier / Boss 锁定目标 / 关卡选中敌人 / ...)都通过 `CollisionService.Instance.Grid.QueryRadius(point, radius)` 查候选,自己按 `hb.Team` + 距离 / 角度等策略筛目标。
- 单 cell 插入 + 3×3 / N×N 矩形 query 的设计:STG 单位尺寸(玩家 0.1 / 敌人 0.5 / 子弹 0.08)远小于 `CellSize`(默认 4),不会跨 cell 漏查;3×3 query 已覆盖目标 12×12 范围,足以覆盖典型 STG 子弹-敌人距离。
- 边界约定:modifier 的 `Modify` 跑在 `Update` 阶段,`CollisionService.LateUpdate` 才建网格,modifier 读到的网格是**上一帧**快照(STG 帧率下无感知)。

**协作边界:**

- **总控统一挂载,不暴露 Inspector 拖拽** —— `Player.cs` / `Enemy.cs` / `Bullet.cs` 都用 `[RequireComponent(typeof(HitboxComponent))]` + Awake 里 `GetComponent` 自动注入,无需手填。
- Boss 现在与普通敌人一致,挂 `BossHitbox : HitboxComponent`(`Team=Enemy`),玩家弹按 Player↔Enemy 阵营配对打到 Boss,CollisionService 走与普通敌人同构的查询路径。
- `CollisionService` 是唯一一处真正遍历"子弹 vs 实体"的地方,不直接读写 HP 字段 —— 玩家弹伤害来自 `b.Damage`,敌人弹命中无视 Damage,固定扣玩家 1 命。
- modifier 通过 `CollisionService.Grid` 读网格是**只读**的,不得调用 `Clear` / `Insert`(否则会破坏本帧的碰撞判定)。

详见 `Assets/Scripts/Hitbox/`。

---


## 道具拾取边界

CollisionService 同时承担道具接触检测：在自身子弹伤害结算之后，重新检查玩家拾取资格，
遍历活跃道具，与 PlayerHitbox.PickupBounds 做 AABB 比较，调用 ItemPickup.TryCollect。
道具不进入伤害网格，也不新建空间索引；单玩家与道具逐一比较即可。
没有 BulletPool 时仍执行拾取检测。此顺序只约束 CollisionService 内部，不声明与独立 LaserService 的先后关系。

拾取与吸附范围均独立于受伤 WorldBounds。吸附由道具系统读取 AttractionBounds 并驱动运动，
CollisionService 不负责移动或具体奖励。出生当帧不拾取，遍历结束后统一回收已收取道具。

## 与其他板块的关系

- [items](./arch-items.md) — 统一接触检测，运动、奖励和回收由道具系统承担。

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [bullet](./arch-bullet.md) — Bullet / Laser 自动挂 HitboxComponent;CollisionService 给子弹做 AABB 命中
- [boss](./arch-boss.md) — BossHitbox / EnemyHitbox 阵营不同,但 HitboxComponent 同一份
- [player](./arch-player.md) — PlayerHitbox 同源
- [laser](./arch-laser.md) — 激光不走 AABB 网格(见 arch-laser.md);CollisionService 阵营过滤共用
