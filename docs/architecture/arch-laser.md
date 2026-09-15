# 激光系统

> LaserEntity 5 段状态机 + 视觉/判定宽度分离

> 本板块对应 ARCHITECTURE § 13. 激光系统(东方风格直线 / 曲线)(原 ARCHITECTURE.md 第 1304–1413 行)。
> 本文档面向项目维护者,不复制实现细节,字段 / 数值 / 默认值以源文件为准。

---

## 13. 激光系统(东方风格直线 / 曲线)

> 把"具有位置、方向、长度、宽度、运动参数和生命周期状态的线段 / 节点集合"封装成与子弹平行的子体系。
> 视觉与判定严格分离;碰撞走"点到线段距离"而不是 AABB 矩形;生命周期走五段式状态机。
> 参考实现资料:东方 Project 风格激光系统参考说明 §1-§22。

### 13.1 为什么需要独立子系统

子弹系统(`Bullet` + `BulletPool` + `CollisionService`)的整个碰撞管线是为 AABB + `UniformGrid` 设计的,激光不行:

| 维度 | Bullet 现状 | 激光需求 | 兼容性 |
|---|---|---|---|
| 碰撞形状 | 统一 AABB(`HitboxComponent.WorldBounds` + `HitboxMath.AABBOverlap`) | 点到线段距离(`(P-Q)² < (r_p + r_l)²`) | ❌ AABB 框不住(激光长 20 单位、AABB 会吞半个屏幕) |
| 空间索引 | `UniformGrid` cell 默认 4 单位 | 激光跨 N 个 cell | ❌ 网格要么插入失真、要么查询退化为全扫 |
| 生命周期 | 子弹无状态机,过期靠 `BoundsService.ContainsCulling` 回收 | `Warning → Expanding → Active → Shrinking → Dead` 五段式 | ❌ Bullet 缺状态机字段 |
| 视觉 | 单 `SpriteRenderer` | 条状主体 + 头尾发光 + 残影 + 透明度 + 长度插值 | ❌ 单 Renderer 不够 |

结论:激光必须新建独立子系统(Laser 实体 + LaserEmitter/Pool + LaserService),**不继承** Bullet,**不污染** BulletPool。但复用现有架构的"语言":
- 走与 Bullet 平行的 `MonoBehaviour` + 对象池(对齐 §1 整体架构)
- 走 `[SerializeReference, SR]` 多态扩展(对齐 §3.1 FireExtension / §2.6 BulletModifier 套路)
- 走 SO 数据驱动 + `BehaviorFlow` Action 触发(对齐 §4 敌人 AI)
- 走 `CollisionTeam` 阵营过滤(不破坏现有碰撞协议)

### 13.2 职责分工

- **`LaserState`(enum)** —— 五段式状态机:`Warning / Expanding / Active / Shrinking / Dead`。纯枚举,零行为。

- **`LaserGeometry`(static class)** —— 激光几何 + 碰撞判定数学,零分配纯函数:
  - `EndPoint(origin, angleRad, length)` → 端点(`(cos θ, sin θ) * length`)
  - `DistanceSqPointToSegment(p, a, b)` → 距离平方(`Clamp01` 把投影点限制在线段内,退化时退化为点到 a)
  - `CheckCurvedHit(playerPos, nodes, laserR, playerR)` → 节点数组任一段命中即 true
  - `CheckCurvedGraze(playerPos, nodes, maxR)` → 擦弹版本(用更大的膨胀半径)

- **`LaserData`(SO 资产)** —— 可复用视觉 + 判定参数集合:
  - **几何**: `VisualWidth`(贴图宽,默认 0.8)/ `CollisionWidth`(判定半径,默认 0.15)/ `MaxLength`(默认 24)
  - **生命周期**: `WarningTime`(0.6) / `ExpandTime`(0.3) / `ActiveTime`(1.5) / `ShrinkTime`(0.3),四段时间全部独立
  - **生命周期碰撞策略**: `CollideInWarning / Expanding / Shrinking` 三个 bool,默认全 false(只在 Active 命中,与东方正作常见行为一致)
  - **视觉**: `Sprite`(条状贴图)/ `HeadSprite`(激光头)/ `WarningColor` / `BodyColor` / `HeadColor`
  - **关键**: 视觉宽度与判定宽度**严格分离**(参考材料 §6),屏幕看到的是 `VisualWidth`,实际判定只用 `CollisionWidth`

- **`LaserEntity`(MonoBehaviour,主控)** —— 单条激光的运行时实体,职责:
  - **状态机**:每帧 `LateUpdate` 按时间推进 `Warning → Expanding → Active → Shrinking → Dead`,依据 `LaserData` 设置 `CollisionEnabled`
  - **几何**: `Position += Velocity * dt`、`Angle += AngularVelocity * dt`、`CurrentLength` 按状态机 + `LengthEase` delegate 插值(默认 `LinearEase`,可在 modifier 里切换)
  - **运动**: 直线模式直接重算端点;曲线模式按 `BaseCurveNodes[i] + Position` 每帧重算世界节点(让节点跟随激光移动)
  - **碰撞查询**: `CheckStraightHit(playerPos, playerRadius)` 一行入口(直线 / 曲线统一),内部走 `LaserGeometry`
  - **修饰器**: `_modifiers` 私有列表,提供 `AddModifier` / `ClearModifiers` / `ResetAllModifierWindows`(对齐 `Bullet` 的同名 API)
  - **生命周期**: `OnDisable` 自动 `ClearModifiers`(`LaserPool.Return` 触发),死亡由状态机切到 `Dead` 后自己调 `SourcePool.Return`

- **`LaserModifier`(abstract `[Serializable]` class)** —— 激光行为修饰器(纯 C#,非 MonoBehaviour),对齐 `BulletModifier`:
  - `OnTick(laser, dt)` 每帧调用(状态机推进之前),子类修改 Position / Angle / Velocity / AngularVelocity / LengthEase
  - `OnDetach(laser)` / `ResetWindow()` 默认空,子类 override
  - `Clone()` 默认 `MemberwiseClone`,子类含引用字段需 override 深拷
  - **PR1 阶段**:只提供最小钩子(对齐 BulletModifier 完整双时钟体系留到 PR3)

- **`LaserPool`(MonoBehaviour,场景单例)** —— 激光对象池,严格对齐 `BulletPool` 拓扑:
  - **分桶**: key 用 `LaserData`(同一份 Data 配不同 Renderer 时共用桶效率最高)
  - **懒扩容**: `Awake` 只为 `DefaultPrefab` 预热一份,其他 Data 按需 Instantiate
  - **路由回池**: `Return(laser)` 按 `laser.Data` 路由到正确桶(不按 SourcePrefab 因为激光不分 prefab 桶)
  - **中心化开火**: `FireGroup(pattern, pos, angleRad, ownerHitbox, extraModifiers)` 与 `BulletPool.FireGroup` 形似,内部调 `pattern.Fire(...)`

- **`LaserPattern`(abstract SO)** —— 激光描述,与 `FirePattern` 平行:
  - 字段: `Data: LaserData` / `ModifierPrefabs: LaserModifier[]`(SR 多态)/ `FireSounds: FireSound[]`(SR 多态,与 `FirePattern.FireSounds` 复用)/ `Damage: float`
  - 抽象方法 `Fire(position, angleRad, pool, ownerHitbox, extraModifiers)` 返回 `LaserEntity`
  - **关键边界**: **不继承 `FirePattern`**(基类持 Speed/Angular/BulletPrefab,对激光无意义),但保留 SR 多态扩展位与 `FirePattern` 完全对仗

- **`StraightLaserPattern: LaserPattern`** —— PR1 唯一具体 Pattern:
  - 字段: `LengthMultiplier: float`(默认 1)
  - `Fire` 内部:`pool.Get(data, pos, angleRad, length, ownerHitbox)` → 挂 modifier → 触发 FireSounds

- **`LaserRendererBase`(abstract MonoBehaviour)** + **`SpriteStretchLaserRenderer`** —— 视觉抽象:
  - `LaserRendererBase` 暴露 `OnLaserInit(laser)` / `OnLaserTick(laser)` 两个钩子
  - `SpriteStretchLaserRenderer`(PR1 默认实现)抓三个子 SpriteRenderer:
    - `Body`(按 `CurrentLength` 拉伸 X,按 `VisualWidth` 设 Y)
    - `Head`(激光头,固定不动)
    - `Warning`(Warning 期显示一条细预警线,长度 `WarningLineLength` 可配)
  - **Body 拉伸公式**: `Body.localScale.x = length`(世界长度),**不**乘任何 `BaseLocalX`。这是为了**对象池复用安全**:若公式带 `_bodyBaseLocalX` 系数,`OnLaserInit` 读 `Body.localScale.x` 会被前一次发射的 `Shrinking` 末期残留污染(接近0),导致每次池复用时 Active 期长度都比上一次短(参考 PROGRESS.md PR1 修复 2 轮的 bug 记录)
  - **与碰撞完全解耦**:换 Renderer 实现(`LineRenderer` / 自定义 Mesh)不影响 `LaserService` 判定

- **`LaserService`(MonoBehaviour,场景单例)** —— 中心化碰撞服务,**独立于 `CollisionService`**:
  - **为什么不在 CollisionService 加分支**: AABB 不适合激光 + UniformGrid 失真;中心化服务避免给 CollisionService 加大量特例判断
  - `LateUpdate` 遍历 `LaserPool.ActiveLasers`,按 `CollisionTeam.Enemy` 阵营过滤,逐条 `laser.CheckStraightHit(pPos, PlayerRadius)`
  - 命中 → 非无敌期 `ShinySTG.Player.Player.Instance.OnHit(1f)` + 触发 `OnPlayerHitByLaser` 事件 + 加入回收队列(避免在 foreach 中修改 `_active`)
  - 擦弹:借鉴 `CollisionService` 的膨胀环思路,激光擦过判定外圈时触发 `OnPlayerGrazedByLaser` 事件,激光继续飞行
  - 出界: 读 `BoundsService.Instance.ContainsCulling(laser.Position)`,出界立刻加入回收队列(避免长期占用池)
  - **玩家自动注册**: `Awake` 自动抓 `ShinySTG.Player.Player.Instance.Hitbox`,无需手动拖

- **`FireLaserAction: EnemyAction`**(`[SRName("Action/Fire Laser")]`)** —— BehaviorFlow 接入层:
  - 字段: `Pattern: LaserPattern` / `FireRate: float` / `AimOffsetDeg: float` / `ExtraModifierPrefabs: LaserModifier[]`
  - 与 `FireAction` **完全对仗**: 同级 SR 标签、相同 OnEnter/OnTick/OnExit 生命周期、相同 ownerHitbox 透传、相同 `FireRate` 节拍
  - Boss / 敌人在 BehaviorFlow 时间轴上挂一条 `Action/Fire Laser` 即可定时放激光,与已有 `Action/Fire` 并行不冲突

### 13.3 协作边界

| 既有层 | 怎么协作 | 是否修改它 |
|---|---|---|
| `Bullet` / `BulletPool` / `CollisionService` / `UniformGrid` | **完全不动**,激光走独立子系统(不污染子弹代码) | ❌ 0 改动 |
| `HitboxComponent` | 激光实体不挂(避免 AABB 插入网格);阵营透传走 `LaserEntity.Team` 字段,值类型与 `HitboxComponent.CollisionTeam` 共用 enum | ❌ 0 改动 |
| `CollisionTeam`(enum) | 复用:`LaserService.LateUpdate` 阵营过滤用 `Team != CollisionTeam.Enemy` continue | ❌ 0 改动 |
| `BoundsService` | 激光出界判定读 `BoundsService.Instance.ContainsCulling(laser.Position)`,沿用已有 Culling Area;无单例时跳过出界检查 | ❌ 0 改动(只读) |
| `Player` / `PlayerHealth` | 命中通过 `ShinySTG.Player.Player.Instance.OnHit(1f)`(与 `CollisionService` 同款,避免 namespace-同名陷阱);无敌判定读 `player.IsInvincible`(全限定名) | ❌ 0 改动 |
| `EnemyAction` / `BehaviorFlow` | 新增 `FireLaserAction`,作为同级 Action 类型(对齐 `FireAction`);BehaviorFlow.Actions 数组 `[SerializeReference, SR]` 字段**自动**支持新 Action,无需改 `BehaviorFlow` | ❌ 0 改动 |
| `FireSound` / `FirePattern.FireSounds` | 激光 `LaserPattern.FireSounds` 直接复用 `FireSound[]`(走 `[SerializeReference, SR]`);开火音触发由 `StraightLaserPattern.Fire` 内部负责 | ❌ 0 改动(复用) |
| 子弹 Modifier(`BulletModifier` / `ModifierStartTrigger` / `BulletSignalBus`) | **不互通**(子弹 modifier 引用 `Bullet`,激光 modifier 引用 `LaserEntity`),但 API 形态完全对齐,PR3 起激光 modifier 也会补 `StartTrigger` SR 多态 + 信号总线订阅 | ❌ 0 改动 |

### 13.4 与既有层的关系

```
┌──────────────────────────────────────────────────────────────────┐
│ 游戏场景 (Scene)                                                  │
│  ├─ 玩家:挂 Player + PlayerMovement + PlayerShooting + Options  │


## 与其他板块的关系

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [bullet](./arch-bullet.md) — 与 Bullet 子体系平行、互不依赖
- [fire-pattern](./arch-fire-pattern.md) — LaserPattern 复用 FireSound(FireSound 数组)
- [enemy-ai](./arch-enemy-ai.md) — FireLaserAction 接入 BehaviorFlow,与 FireAction 对仗
- [hitbox](./arch-hitbox.md) — 阵营过滤共用 CollisionTeam,但不走 AABB 网格
- [bounds](./arch-bounds.md) — 出界判定复用 BoundsService.ContainsCulling
