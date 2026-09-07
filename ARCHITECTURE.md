# ShinySTG 架构说明

> 本文档面向项目维护者,目的是**理解架构的工作原理与扩展套路**,便于安全地加新功能。
> 具体字段、参数、伪代码请直接看对应的源文件;本文档不复制实现细节。

---

## 目录

1. [整体架构一览](#1-整体架构一览)
2. [子弹系统](#2-子弹系统)
3. [射击模式系统(FirePattern)](#3-射击模式系统firepattern)
4. [敌人 AI(BehaviorFlow + EnemyAction)](#4-敌人-aibehaviorflow--enemyaction)
5. [Boss 系统(BossController + 多阶段 + 多管血)](#5-boss-系统bosscontroller--多阶段--多管血)
6. [玩家系统(Player + 子机 + 多态位置)](#6-玩家系统player--子机--多态位置)
7. [扩展指南](#7-扩展指南)
8. [Hitbox 系统(统一 AABB)](#8-hitbox-系统统一-aabb)
9. [目录速查(一级)](#9-目录速查一级)

---

## 1. 整体架构一览

整个项目遵循"**职责分离 + 数据驱动 + 组合优于继承**"的原则。游戏运行时由五类对象组成:

```
┌──────────────────────────────────────────────────────────────────┐
│ 游戏场景 (Scene)                                                  │
│  ├─ 玩家:挂 Player + PlayerMovement + PlayerShooting + Options │
│  │          + PlayerHitbox                                          │
│  ├─ 普通敌人:挂 Enemy 总控 + EnemyHealth + ShooterEnemy +       │
│  │            EnemyHitbox(引用一个 BehaviorFlow 资产)             │
│  ├─ Boss:挂 BossController + BossHealth + BossShotCounter       │
│  ├─ 子弹:挂 Bullet(RequireComponent 自动挂 HitboxComponent)         │
│  └─ BulletPool (场景单例,负责子弹的复用)                          │
└──────────────────────────────────────────────────────────────────┘
            │                    │                    │
            ▼                    ▼                    ▼
┌──────────────────────┐ ┌──────────────────────┐ ┌────────────────┐
│ 敌人 AI 层           │ │ Boss 系统层         │ │ 子弹层          │
│ Enemy (总控)         │ │ BossController       │ │ Bullet + Damage │
│  ├─ EnemyHealth      │ │  ├─ BossPhase[]      │ │ BulletPool     │
│  └─ ShooterEnemy     │ │  ├─ BossSignal[]     │ │ BulletModifier │
│      └─ BehaviorFlow  │ │  └─ BossHealth      │ │ FirePattern SO │
│          └─ EnemyAction[] │                 │ └────────────────┘
└──────────────────────┘ └──────────────────────┘
            │                    │
            └────────┬───────────┘
                     ▼
┌──────────────────────────────────────────────────────────────────┐
│ 通用层                                                            │
│  Singleton<T> / PersistentSingleton<T>  (单例基类)                 │
│  HitboxComponent / HitboxMath (数学碰撞 + 编辑器可视化,正交层)     │
└──────────────────────────────────────────────────────────────────┘
```

**核心设计原则(以及它们被选中的理由):**

| 原则 | 体现 | 为什么这样做 |
|---|---|---|
| **数据驱动** | FirePattern / BehaviorFlow 都是 ScriptableObject 资产,Inspector 直接拖 | 改一个 .asset 即可改变全场景所有引用它的对象行为,无需重编 |
| **组合优于继承** | 敌人行为 = `EnemyAction[]` 数组;Boss 行为 = `BossPhase[]` 数组 | 加新行为 = 加新数组元素,不引入新类层级 |
| **多态通过 SerializeReference** | Action / MoveBehaviour / BossPhase / BossSignal / OptionPositionForm 全用项目自带的 SREditor 插件,Inspector 下拉选类型 | Inspector 里就能扩展,无需改宿主组件的代码 |
| **对象池** | 子弹频繁创建/销毁,走 `BulletPool` 复用 | 避免 GC 抖动,STG 高弹量场景必备 |
| **行为流资产化** | `BehaviorFlow` SO 把"一段完整敌人行为"封装成可复用资产 | 多个敌人/Boss 共享同一份逻辑,资产级 Git diff 友好 |
| **Action 用 `[Serializable] class` 而非 SO** | `EnemyAction` / `MoveBehaviour` 是普通类 + `[SerializeReference]` | 行为要持有运行时状态(如 `_timer`),SO 是跨实例共享会出问题 |
| **Move 包成 MoveAction 而不是直接挂外层** | `MoveAction` 持有 `MoveBehaviour` | 统一 Action 的"时间轴 + Duration"语义,让 Move 也能被 Parallel 编排 |
| **行为编排自造而非 StateMachineBehaviour** | Action 三段式 OnEnter/Tick/Exit + 容器组合 | Animator 偏动画,语义不够通用;自造更贴合"行为编排"直觉 |
| **Boss 与普通敌人正交** | BossController 独立于 ShooterEnemy | Boss 有"阶段 / 多管血 / 阶段切换条件",与单段行为流的普通敌人语义不同 |
| **Boss 阶段切换用"信号池"而非 phase 内嵌 transition** | `BossSignal` + `PhaseTrigger` 全局共享 | 同一信号可被多 phase 监听;phase 之间解耦;新增切换条件 = 新建 Signal 子类 |
| **BulletPool 按 prefab 分桶** | `Dictionary<Bullet, Stack<Bullet>>` | 不同 prefab 互不污染;同 prefab 跨敌人/Pattern 共用桶,复用率最大化 |
| **玩家子机位置形态走 SerializeReference** | `OptionPositionForm` 多态子类 | 与项目里 Action / MoveBehaviour 完全对齐的扩展套路 |
| **碰撞走数学而非物理引擎** | HitboxMath + HitboxComponent(AABB),不走 Physics2D / Collider | STG 高弹量场景下纯数学 O(1) 比物理引擎快,且代码可控、可视化(见 §8) |

---

## 2. 子弹系统

涉及三个协作组件:`BulletPool`(场景单例,出生与回收)、`Bullet`(飞行体 + 总控)、`BulletModifier`(效果修饰器)。

**职责:**
- **BulletPool** —— 所有子弹的"工厂 + 回收站",按 `BulletPrefab` 分桶,复用同款子弹,避免 GC。
- **Bullet** —— 每帧按当前方向飞行,叠加效果 modifier,出界/命中后归还到池。
  - **总控模式(对齐 Player / Enemy)**:`[RequireComponent(typeof(HitboxComponent))]` 强制每颗子弹自动挂 HitboxComponent,`Awake` 自动 GetComponent 注入。
  - **阵营透传**:子弹 `Hitbox.Team` 由发射者 (owner) 的 `Hitbox.Team` 在 `Bullet.Init` 时设置 —— 玩家发射 → Team=Player,敌人发射 → Team=Enemy。
  - **伤害字段**:`public float Damage` 由 `FirePattern.Damage` 在 `BulletPool.Get` 时写入,`CollisionService` 玩家弹 vs 敌人时按 `b.Damage` 扣血。**敌人弹不读此字段** —— 敌人弹命中玩家直接走 `player.OnHit(1f)`,无视 Damage。
- **BulletModifier** —— 子弹行为的可插拔修饰(加速、转向等),由外部代码手动 `AddModifier` 挂载。

**协作边界:**
- 调用方通过 `BulletPool.Instance.Get(prefab, pos, angle, speed, angular, damage, ownerTeam)` 拿弹,或 `FireGroup(pattern, pos, rotation, ownerHitbox)` 一步触发一个 FirePattern。
- `FireGroup` 的 `ownerHitbox` 是发射者(玩家 Hitbox / 敌人 Hitbox / 子机共用玩家 Hitbox),`null` 表示中性阵营(不参与碰撞)。
- 调用方负责在合适时机调 `Return(bullet)`,Bullet 不会自动回收(出界检测由调用方或 modifier 决定)。

**阵营透传数据流:**

```
玩家开火:   PlayerShooting → FireGroup(playerHitbox) → pattern.Fire(...)
         → pool.Get(..., Team=Player) → bullet.Init(..., damage=1, team=Player)
         → bullet.Hitbox.Team = Player
         → CollisionService: enemy.TakeDamage(b.Damage)

敌人开火:   FireAction → FireGroup(enemyHitbox) → pattern.Fire(...)
         → pool.Get(..., Team=Enemy)  → bullet.Init(..., damage=1, team=Enemy)
         → bullet.Hitbox.Team = Enemy
         → CollisionService: player.OnHit(1f)  ← 无视 b.Damage
```

**扩展点:**
- 新增子弹效果(减速 / 爆炸 / 分裂):新建 `BulletModifier` 子类,手动挂到 `bullet._modifiers`。
- 调整某玩家弹的伤害:改 `FirePattern.asset` 的 `Damage` 字段(无需碰代码或 prefab)。
- 装饰/道具弹不参与战斗:用 `Hitbox.Team = Neutral` 的 prefab(或从 `null` owner 发射)。

详见 `Assets/Scripts/Bullet/`。
---

## 3. 射击模式系统(FirePattern)

**职责:** 把"**射什么子弹 + 怎么射**"封装进 ScriptableObject 资产。一个 .asset = 一种弹幕形态,改资产即改变所有引用它的对象。

**关键类型:**
- `FirePattern`(SO 基类)—— 持有 `BulletPrefab` / `Speed` / `AngularSpeed` / `BaseAngle` / `Damage`,定义 `Fire(position, rotationRad, pool, ownerHitbox)` 抽象方法。
  - `Damage`:玩家弹命中敌人时的伤害值(由 `CollisionService` 按 `b.Damage` 扣血)。**敌人弹不读此字段** —— 敌人弹命中玩家直接 `player.OnHit(1f)`,固定扣 1 命。
- 子类:`RingFirePattern` / `LineFirePattern` / `ArcFirePattern` / `CompositeFirePattern`(可嵌套其他 FirePattern)。

**协作边界:**
- 被 `FireAction`(敌人行为)、`PlayerShooting`(玩家主炮)、`PlayerOptions`(子机开火)、`BossController`(Boss 开火)调用。
- 实际开火委托给 `BulletPool.FireGroup` 或 `BulletPool.Get`。
- `Fire(...)` 收到 `ownerHitbox` 后,子类在内部 `pool.Get(...)` 时把 `Damage` + `ownerHitbox.Team` 透传给每颗新生成的子弹 —— **子弹阵营由发射者阵营决定,无需 prefab 静态配置**。

**扩展点:**
- 新增弹幕形态(螺旋 / 樱花 / ...):新建 `FirePattern` 子类 + 创建对应 .asset,Inspector 里直接拖。
- 调整某玩家弹的伤害:改 `FirePattern.asset` 的 `Damage` 字段(默认 1;做"高火力一击多血"改 2+)。
- 想给"同一发弹不同次发射不同伤害"(如蓄力弹):在调用方 `pool.Get` 前改 `pattern.Damage`,然后再调 `Fire(...)`。

详见 `Assets/Scripts/Bullet/FirePattern*`。
---

## 4. 敌人 AI(BehaviorFlow + EnemyAction)

**职责:** 用"**时间序列 + 多态 Action + 数据驱动**"描述敌人行为。**所有行为在 Inspector 里用下拉菜单自由组合**,无需改代码。

**关键类型(三层职责):**

```
BehaviorFlow (SO 资产)
  └─ Actions: EnemyAction[]   ← 时间序列配置
        ├─ 原子 Action:Fire / Move / Wait / SelfDestruct
        ├─ 容器 Action:Parallel(同时跑多个) / Sequence(纯折叠分组)
        └─ Move 内部再委托给 MoveBehaviour(匀速 / 线性等)
BehaviorFlowRuntime (普通 C# 类)
  └─ 驱动器,持有 _index / _elapsedInCurrent / _started 等运行时状态
ShooterEnemy (MonoBehaviour 组件,极薄)
  └─ OnEnable 时 Flow.Instantiate() 出 Runtime,Update 时 Tick 驱动
```

**Action 三段式生命周期:** `OnEnter`(进入初始化) → `OnTick`(每帧执行) → `OnExit`(退出清理,用于重置状态)。`Duration <= 0` 表示只跑一帧。

**协作边界:**
- `ShooterEnemy.Flow` 拖一个 `.flow` 资产即可,无需挂其他组件。
- Boss 走自己的 `BossController + ShooterPhase`,**不挂 ShooterEnemy**(避免组件污染,详见 §5)。
- `BehaviorFlow.Instantiate()` 用 `Object.Instantiate(SO)` 深拷贝 Actions 数组,所以**多个敌人引用同一资产互不干扰**(Unity 会自动深拷贝 `[SerializeReference]` 字段)。

**扩展点:**
- 新增敌人行为(动画 / 隐身 / 加血):新建 `EnemyAction` 子类,加 `[Serializable, SRName("Action/<名字>")]`,Inspector 下拉即可用。
- 新增移动方式(贝塞尔 / 圆形 / 追踪):新建 `MoveBehaviour` 子类,同套路。
- 新增"行为流"(符卡 / 小怪模式):右键 → Create → STG → Behavior Flow,创建 SO 资产并配置 Actions。

详见 `Assets/Scripts/Enemy/`。

---

## 5. Boss 系统(BossController + 多阶段 + 多管血)

**职责:** 与普通敌人**正交**的 Boss 编排层,提供多阶段 / 阶段触发条件 / 多管血。普通敌人就一段行为流,Boss 需要这些"上层编排"概念,所以单独建一层。

**关键类型:**

```
BossController (主驱动)
  ├─ BossHealth      ← 多管血组件(HealthBar[]),TakeDamage 自动切管
  ├─ BossShotCounter ← 场景单例,每发一弹计数(可被 phase 用作切换条件)
  ├─ BossSignal[]    ← 全局信号池(CurrentBarIndex / TotalHp% / PhaseTime / ShotsFired ...)
  └─ BossPhase[]     ← 阶段列表(目前用 ShooterPhase 直接持 BehaviorFlow)
        └─ PhaseTrigger[] ← 每阶段独立配"满足哪个 Signal + Op + Threshold 就退出"
```

**每帧驱动流程:**
1. `signals.Tick(boss, dt)` —— 累加信号内部状态。
2. `currentPhase.OnTick(boss, dt)` —— 调用内部 BehaviorFlowRuntime。
3. 检查 `PhaseTrigger.IsSatisfied()` —— 任一触发就 `NextPhase()`。

**协作边界:**
- Boss GameObject 上挂 `BossController + BossHealth + BossShotCounter`,**不挂 ShooterEnemy**。
- `ShooterPhase` 直接持 BehaviorFlow 资产,boss 行为复用普通敌人那套行为流。
- `BossHealth.Bars` 为空时退化为单管血模式,兼容旧 prefab。

**扩展点:**
- 新增 Boss 阶段:新建 `BossPhase` 子类,加到 `BossController.Phases`。
- 新增阶段切换条件(开场过场 / 玩家撞 N 次 / ...):新建 `BossSignal` 子类,在 `PhaseTrigger` 里引用。
- 多管血配置:直接配 `BossHealth.Bars` 数组,无需代码。

详见 `Assets/Scripts/Enemy/Boss/`。

---

## 6. 扩展指南

### 6.1 决策树:我要加新功能,改哪里?

| 你想做的事 | 改哪里 |
|---|---|
| 加一种新的发射模式(螺旋、樱花、...) | 新建 `FirePattern` 子类 + 创建 SO 资产 |
| 加一种新的敌人行为(动画、隐身、加血、...) | 新建 `EnemyAction` 子类 |
| 加一种新的移动方式(贝塞尔、圆形、追踪、...) | 新建 `MoveBehaviour` 子类 |
| 加一种新的子弹效果(减速、爆炸、分裂、...) | 新建 `BulletModifier` 子类 + 手动 `bullet.AddModifier()` |
| 加一种新的"行为流"(符卡 A / 小怪攻击模式 / ...) | 右键 → Create → STG → Behavior Flow,创建 SO 资产 |
| 改某个敌人的行为流 | 改它引用的 .flow 资产(影响所有引用它的敌人) |
| 加 boss 阶段 | 新建 `BossPhase` 子类 + 加到 `BossController.Phases` |
| 加 boss 阶段切换条件 | 新建 `BossSignal` 子类 + 在 `PhaseTrigger` 里引用 |
| 整个 boss prefab 行为完全重排 | 改每个 Phase 引用的 .flow 资产 |
| 加 boss 多管血 | 配 `BossHealth.Bars` 数组 |
| 加新碰撞形状(圆 / 胶囊 / 多边形) | 抽 `HitboxShape` 抽象基类 + 子类,扩 `HitboxMath`(详见 §8) |
| 接入子弹碰撞服务(每帧遍历) | **已就位**:挂 `CollisionService` 组件到场景 GameObject 上,阵营透传 + 伤害应用都已自动(详见 §8)。子弹 prefab 不需要手动配 `Hitbox.Team` —— 由发射者透传 |
| 玩家 / 敌人装配 Hitbox | 不需要手动:`Player.cs` / `Enemy.cs` 已 `[RequireComponent(typeof(...Hitbox))]` 自动挂,Awake 自动注入 `Health.Hitbox`(详见 §8) |

### 6.2 调试小贴士

- **行为没触发?** 检查该 Action 的 `Duration` 是否 > 0(<= 0 立即跳过)。
- **行为卡住不切换?** 检查 `OnTick` 抛异常没;`OnTick` 抛异常会导致 BehaviorFlowRuntime 后续 Tick 中断(Unity 行为)。
- **OnExit 没调用?** Sequence 跑完所有 children 后 `_idx=-1`,但 Sequence 本身仍占用外层时间,等外层 Duration 到时才 OnExit。这是预期行为。
- **Parallel 中某个 child 提前停了?** 它自己的 Duration 到了 → OnExit → 不再 tick。但不影响其他 child。
- **boss 阶段不切换?** 检查 `PhaseTrigger` 的 `Signal` 是否在 Signals 数组里、`Op` 方向是否正确(例:`HP ≤ 50%` 用 `LessOrEqual`,不是 `GreaterOrEqual`)。
- **boss 阶段一帧穿多个?** 改用 `CurrentBarIndexSignal` 而不是 `CurrentBarPercentSignal`,前者单调递增。
- **BehaviorFlow 资产改动不生效?** 运行时每次都 `Object.Instantiate` 深拷贝,所以**资产修改对当前运行无效**,需要重新进入 Play Mode。

### 6.3 性能注意

- **对象池预热**:把 `BulletPool.InitialSize` 调到预期峰值,避免运行时 Instantiate。
- **避免在 OnTick 里分配**:每帧 `new` 会触发 GC,推荐用对象池或 `struct`。
- **Modifier 数量**:每颗子弹的 `Bullet.Update` 会遍历所有 modifier,modifier 别太多(目前看一两个就够)。
- **BehaviorFlow 资产 Instantiate**:每个敌人/boss 进入时都 Instantiate 一份 SO,虽然不重(都是浅数据),但每个 Flow 都会有一个 `(Runtime)` 实例驻留内存;若敌人规模很大(数千),考虑改用轻量 Runtime。

---

## 7. 玩家系统(Player + 子机 + 多态位置)

**职责:** 玩家主控(单例 + 输入分发) + 八方向移动 + 按住攻击 + 残机/火力级/无敌 + 子机系统。整体走"**数据驱动 + 多态位置**"的套路,与项目既有扩展机制一致。

**关键类型:**

```
Player (主控单例 + 输入分发)
PlayerMovement (八方向 + Focus 低速)
PlayerShooting (主炮:按住即喷,复用 FirePattern)
PlayerHealth   (残机 + 火力级 + 复活无敌,暴露 PowerUp/AddLife 等事件)
PlayerHitbox   (子物体判定点)
PlayerOptions  (子机系统:活力阈值解锁 + 跟随 + 开火 + 多态位置形态)
  └─ OptionPositionForm (多态抽象,通过 [SerializeReference] 子类切换形态)
       ├─ TouhouSymmetricForm (东方对称)
       ├─ LinearRowForm (线性横排)
       └─ RearLineForm (后排直线)
```

**协作边界:**
- 主炮与子机开火**共用同一触发源**:`PlayerShooting.FireHeld`,松开攻击键时主炮和子机**同步停**(不会"主炮停 / 子机还在喷")。
- 子机数量由 `PlayerHealth.PowerLevel` 单向驱动;子机不会反向改 PowerLevel,避免循环依赖。
- 子机开火形态也是 FirePattern 资产(`OptionFirePatterns[]`),直接复用敌人那套弹幕体系。
- 玩家事件(`OnLifeLost` / `OnRevive` / `OnAllLivesLost`)只发通知,不硬编码死亡动画 / 特效,保持"事件层与表现层分离"。

**输入方案(双重兼容):**
- 装了 `com.unity.inputsystem` → 用 `PlayerInput` 组件(Behavior=Invoke C# Events)调 `Player.OnPlayer / OnAttack / OnFocus`。
- 没装 → 直接挂 `LegacyInputDriver`,每帧 `Input.GetKey` 灌给 Movement / Shooting。
- **二选一,不要两个都挂,会双重输入。**

**扩展点:**
- 新增子机位置形态:新建 `OptionPositionForm` 子类 + `[Serializable, SRName("Form/<名字>")]`,Inspector 下拉即可用。
- 新增子机开火模式:直接配 `OptionFirePatterns[i]` 拖不同形态的 FirePattern 资产,无需新代码。
- 死亡动画 / 重生特效:订阅 `PlayerHealth.OnLifeLost / OnRevive / OnAllLivesLost`。
- 火力 / 续命道具:外部脚本调 `Player.Instance.Health.PowerUp(1)` / `AddLife(1)`。

详见 `Assets/Scripts/Player/`。

---

## 8. Hitbox 系统(统一 AABB)

碰撞检测**走数学计算、不依赖 Unity Physics2D / Collider**。理由:STG 弹量大,纯几何 O(1) 比物理引擎快;且代码可控、可在 Scene 视图直接可视化。

**职责分工:**

```
HitboxComponent  # 通用 MonoBehaviour:Inspector 配 Size / Gizmos 颜色 / 暴露 Overlaps / OverlapsPoint
HitboxMath      # 静态数学库:仅 AABB×AABB 一个函数,Rect 相交判定,O(1) 零分配
```

**关键设计:**

- **统一 AABB** —— 全项目只用轴对齐矩形碰撞盒(Size.x = 宽,Size.y = 高)。`transform.rotation` 不影响形状,`transform.lossyScale` 实时反映。绝大多数 STG 判定点都是小方块,这样省去圆 / 旋转矩形的分支代码。
- **正交层** —— Hitbox 与 Player / Enemy / Bullet 完全解耦。玩家 / 敌人 / 子弹各自持一个 HitboxComponent 引用,谁的 size 谁决定;玩家死亡时不被碰撞逻辑拖着走。
- **数学入口单一** —— 所有"是否相交"都走 `HitboxMath.AABBOverlap(Rect, Rect)`,内部用 4 次比较;后续要扩圆 / 胶囊 / 多边形,只需在 HitboxMath 里加分支,HitboxComponent 接口不动。
- **编辑器可视化** —— `OnDrawGizmos` / `OnDrawGizmosSelected` 用 `Gizmos.DrawWireCube` 画 AABB 轮廓,选中变橙色,`AlwaysDraw = false` 时只选中才画,大场景保持 Scene 视图干净。
- **碰撞服务已就位** —— `CollisionService`(`ShinySTG.Hitbox` 命名空间,场景单例,`LateUpdate` 调度)按阵营配对跑完所有"子弹 vs 实体"判定。
  - 阵营字段 `HitboxComponent.Team`(`Neutral / Player / Enemy`):
    - **玩家 / 敌人**:由 `PlayerHitbox.Reset()` / `EnemyHitbox.Reset()` 静态配置(玩家 = Player,敌人 = Enemy)。
    - **子弹**:由发射者透传设置 —— `BulletPool.FireGroup(pattern, pos, rot, ownerHitbox)` 时取 `ownerHitbox.Team` 写入 `bullet.Hitbox.Team`。**子弹 prefab 不需要手动配 Team**。
  - 性能两层可选:阵营分桶 + 缓存(默认)/ 空间哈希(Inspector 勾 `UseSpatialHash`,弹量 > 500 时开启)。详见 §8.1。
- **未来扩展形状的留口子** —— 若要加圆 / 胶囊 / 多边形:抽 `HitboxShape` 抽象基类,把 `Size` / `Overlaps` / `DrawGizmos` 挪到子类,HitboxComponent 接口 (`Overlaps` / `OverlapsPoint` / `WorldBounds`) 不变。

**协作边界:**

- **总控统一挂载,不暴露 Inspector 拖拽** —— `Player.cs` / `Enemy.cs` / `Bullet.cs` 都用 `[RequireComponent(typeof(HitboxComponent))]` + Awake 里 `GetComponent` 自动注入:
  - `Player` 总控:`Hitbox` 是只读公开属性 (`Player.Instance.Hitbox`),外部 CollisionService 直接拿。
  - `Enemy` 总控:`Hitbox` 同款只读属性 (`enemy.Hitbox`);Awake 时把 `EnemyHitbox` 灌给 `EnemyHealth.Hitbox`,这样 `EnemyHealth.Position` 自动跟随 Hitbox 走,**EnemyHealth.Hitbox 字段不需要 Inspector 手填**(Tooltip 已说明)。
  - 这一选择避免了在多个组件上重复拖同一引用,也保证总控"管住"自己装配的 Hitbox 不会被外部误改。
- `PlayerHitbox` / `EnemyHitbox` 都是 `HitboxComponent` 的薄子类,`Reset()` 给推荐默认值(玩家 0.1 + Team=Player / 敌人 0.5 + Team=Enemy),子类本身**不持有逻辑**。
- Boss 暂时不挂 HitboxComponent(走 `BossHealth` 自己的逻辑,正交)。若未来 Boss 也想用同一套 AABB,可直接挂 HitboxComponent 子类,无需扩 HitboxMath。
- `CollisionService` 是唯一一处真正遍历"子弹 vs 实体"的地方:读 `BulletPool.ActiveBullets` + `EnemyHealth.Alive`,调 `enemy.TakeDamage(b.Damage)` / `player.OnHit(1f)`,不直接读写 HP 字段。玩家弹伤害来自 `b.Damage`(由 `FirePattern.Damage` 在 `pool.Get` 时写入);敌人弹命中无视 Damage,固定扣玩家 1 命。命中事件 `OnPlayerBulletHitEnemy` / `OnEnemyBulletHitPlayer` 留给外部特效/音效/计分订阅。
- `Bullet.Reset()` 给推荐默认值(0.08×0.08 + Team=Neutral);Neutral 表示"等待发射者透传"的初始态,真正发射时由 `Bullet.Init` 用 owner 的阵营覆盖。

详见 `Assets/Scripts/Hitbox/`。

---

## 9. 目录速查(一级)

```
Assets/Scripts/
├── Bullet/    # 子弹系统 + FirePattern
├── Enemy/     # 普通敌人 + Boss(都挂这里)
├── Hitbox/    # 通用碰撞盒(数学 + 编辑器可视化,正交层)
└── Player/    # 玩家系统
```

具体文件清单请看 IDE 的项目浏览器或对应子目录的 README(如未来新增)。

