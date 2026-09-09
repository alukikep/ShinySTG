# ShinySTG 架构说明

> 本文档面向项目维护者,目的是**理解架构的工作原理与扩展套路**,便于安全地加新功能。
> 具体字段、参数、伪代码请直接看对应的源文件;本文档不复制实现细节。

---

## 目录

1. [整体架构一览](#1-整体架构一览)
2. [子弹系统](#2-子弹系统)
3. [射击模式系统(FirePattern)](#3-射击模式系统firepattern)
   - 3.1 [FireExtension 扩展点(基础发射逻辑的多态扩展)](#31-fireextension-扩展点基础发射逻辑的多态扩展)
4. [敌人 AI(BehaviorFlow + EnemyAction)](#4-敌人-aibehaviorflow--enemyaction)
5. [Boss 系统(BossController + 多阶段 + 多管血)](#5-boss-系统bosscontroller--多阶段--多管血)
6. [扩展指南](#6-扩展指南)
7. [玩家系统(Player + 子机 + 多态位置)](#7-玩家系统player--子机--多态位置)
8. [Hitbox 系统(统一 AABB)](#8-hitbox-系统统一-aabb)
9. [关卡系统(LevelDefinition + LevelController)](#9-关卡系统leveldefinition--levelcontroller)
10. [关卡编辑器子系统](#10-关卡编辑器子系统)

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

**职责分工:**
- **BulletPool** —— 子弹的工厂 + 回收站(场景单例)。按 prefab 分桶复用,避免 GC;同时是 modifier 的统一挂载入口。
- **Bullet** —— 飞行体 + 总控(自动挂 HitboxComponent,Awake 注入);`Update` 第 1 步调度所有 modifier,再驱动移动/旋转。
- **BulletModifier** —— 子弹行为的可插拔修饰(加速 / 转向 / 减速 / 分裂等),走 `[SerializeReference, SR]` 多态,在 `FirePattern` 或 `FireAction` 里下拉选类型。

**协作边界:**
- 调用方通过 `BulletPool` 拿弹或一步触发一个 FirePattern;`FireGroup` 接收 `ownerHitbox` 透传阵营。
- 子弹不会自动回收 —— 出界 / 命中后由调用方或 modifier 决定 `Return` 时机。

**阵营语义:**
- 子弹阵营由**发射者**透传,prefab 不需要手动配 `Hitbox.Team`。
- 玩家弹命中敌人 → 按子弹 `Damage` 扣血;敌人弹命中玩家 → 固定扣 1 命,无视 `Damage`。

### 2.1 子弹朝向约定(美术 / 代码对齐)

- **角度 0°** 在代码里 = 指向 `+X`(右),用 `Mathf.Cos / Sin` 直接算方向。
- 美术贴图默认尖头朝 `+Y`(朝上) —— 这是 **STG 美术资源的惯例**,Bullet 在旋转时统一加 `-90°` 偏移:
  ```csharp
  // Bullet.Init / Bullet.Update 都用这个公式
  transform.rotation = Quaternion.Euler(0, 0, SteerAngle * Mathf.Rad2Deg - 90f);
  ```
- 视觉对照表(美术尖头朝上):

  | SteerAngle | 飞行方向 | 贴图朝向 | 视觉 |
  |---|---|---|---|
  | 0° (右) | (1, 0) | z = -90° | 尖头朝右 ✅ |
  | 90° (上) | (0, 1) | z = 0° | 尖头朝上 ✅ |
  | 180° (左) | (-1, 0) | z = 90° | 尖头朝左 ✅ |
  | 270° (下) | (0, -1) | z = 180° | 尖头朝下 ✅ |

  > 💡 如果某些子弹贴图尖头朝 `+X`(横的),需要单独为它做补偿;当前所有内置 prefab 都按"尖头朝上"制作。

### 2.2 BulletModifier 多态修饰体系

BulletModifier 是子弹行为的**可插拔插件**。每个 modifier 是纯 C# 类(非 MonoBehaviour、非 ScriptableObject),走项目统一的 `[SerializeReference, SR]` 多态扩展套路(与 `EnemyAction` / `MoveBehaviour` / `BossPhase` 等一致)。

**Modifier 何时跑?**
```csharp
// Bullet.Update()
void Update() {
    // 1. modifier 改 b.Speed / b.SteerAngle / b.AngularSpeed 等
    foreach (var m in _modifiers) m.Modify(this, dt);

    // 2. AngularSpeed 累加到飞行方向(SteerAngle)
    SteerAngle += AngularSpeed * dt;

    // 3. 按当前方向移动 + 同步旋转
    Vector2 dir = new Vector2(Mathf.Cos(SteerAngle), Mathf.Sin(SteerAngle));
    transform.position += (Vector3)(dir * Speed * dt);
    transform.rotation = Quaternion.Euler(0, 0, SteerAngle * Mathf.Rad2Deg - 90f);
}
```

**Modifier 在哪里配置?两层入口:**

| 配置位置 | 生效范围 | 字段 | 写法 |
|---|---|---|---|
| `FirePattern.ModifierPrefabs` | 该 pattern 发出的所有子弹 | `[SerializeReference, SR] BulletModifier[]` | Inspector 点 `+` 下拉选类型 |
| `FireAction.ExtraModifierPrefabs` | 仅本次 Fire 调用(覆盖到上面之后) | `[SerializeReference, SR] BulletModifier[]` | 同上 |

**运行时挂载路径**(单颗子弹的完整链路):
```
FireAction.OnTick
  → BulletPool.FireGroup(pattern, pos, rot, hitbox, ExtraModifierPrefabs)
    → pattern.Fire(pos, rot, this, hitbox, ExtraModifierPrefabs)
      → SpawnBullet(...)  // FirePattern 基类的 protected helper,合并 ModifierPrefabs + extras
        → pool.Get(prefab, ..., modifiersToAttach)  // 8 参重载
          → b.Init(...)  // ClearModifiers(回池后清空旧 modifier)
          → AttachModifiers(b, mods)  // 遍历 + Clone() + AddModifier
            → bullet.AddModifier(mod.Clone())  // Clone 出一份独立实例
          → _active.Add(b)
```

**为什么走 Clone 而不是共享?**

- 同一 FirePattern 资产是 SO,**被 N 个敌人引用**;
- 如果 modifier 是单例被 N 颗子弹共享,任何 modifier 上的状态字段(如 `计时器` / `追踪冷却`)会被所有子弹同时读写,行为完全错乱;
- **Clone 出独立实例**(默认 `MemberwiseClone`,纯值类型字段无开销,引用类型字段需手动 override),保证 per-instance state 安全。

### 2.3 BulletModifier 字段约定(写 modifier 时遵守)

| 字段类型 | 默认 Clone 行为 | 是否需要 override Clone |
|---|---|---|
| `float` / `int` / `bool` / `Vector2` 等值类型 | ✅ 浅拷贝足够,无共享问题 | ❌ 不需要 |
| `Transform` / `GameObject`(UnityEngine.Object 引用) | ⚠️ 浅拷贝共享引用(基本安全,很少写) | 通常不需要 |
| `List<T>` / `T[]` / 字典 | ⚠️ **共享同一集合**,Add/Remove 会污染 | ✅ **必须 override Clone 深拷** |
| 自定义 `class` 字段 | ⚠️ 共享同一对象 | ✅ **必须 override Clone 深拷** |

**Modifier 不可做的事:**
- ❌ 访问 `this.transform`(modifier 不是 GameObject,会 NRE)
- ❌ 用 `MonoBehaviour` 派生(违反本架构一致性原则)
- ❌ 持有场景级单例的"硬引用"(会让 modifier 跟场景耦合)

### 2.4 内置 BulletModifier 示例

| 类型名 | SRName | 行为 | 关键字段 |
|---|---|---|---|
| `AccelerateModifier` | `Modifier/Accelerate` | `b.Speed += Acceleration * dt` | `Acceleration` (float, 默认 5) |
| `SteerTowardModifier` | `Modifier/Steer` | 每帧把 `b.AngularSpeed = TurnRate * Deg2Rad`(由 `Bullet.Update` 第 2 步自动累加到 `SteerAngle`) | `TurnRate` (度/秒, 默认 90) |
| `HomingEnemyModifier` | `Modifier/Homing Enemy` | 通过 `CollisionService.Grid.QueryRadius(...)` 查最近敌人,按 `TurnRate` 限速转向 | `SearchRadius` / `TurnRate` / `LockOnDelay` / `MaxHomingTime` |
| `BulletColorModifier` | `Modifier/Color` | 给 `b.Renderer.color` 染色(Solid / FadeByLifetime / Flash 三模式);黑白灰 bullet 素材一染色即变彩色,适合做"主炮红 / 子机蓝 / Boss 紫"。需 `Bullet.Renderer` 字段(Awake/Reset 自动 `GetComponentInChildren<SpriteRenderer>(true)` 抓取) | `Mode` / `Color` / `FadeOutColor` / `FadeStart` / `ReferenceLifetime` / `FlashFrequency` / `FlashMinAlpha` |

**追踪玩家(敌人弹挂的 modifier)是常见扩展**,但当前项目未内置 `HomingPlayerModifier`(因玩家是单例,目标解析无需走网格);按下方"扩展点"小节的三步套路自行实现即可,大致套路是用 `ShinySTG.Player.Player.Instance?.transform` 拿到目标,其余转向逻辑与 `HomingEnemyModifier` 同源。

### 2.5 扩展点

**新增 modifier 类型**:三步套路(与 EnemyAction 等完全一致)
1. 在 `Assets/Scripts/Bullet/` 新建 `<你的>Modifier.cs`
2. 继承 `BulletModifier`(`[Serializable]` abstract class),加 `[SRName("Modifier/<名字>")]`
3. override `Modify(Bullet b, float dt)`,通过修改 `b.Speed` / `b.SteerAngle` / `b.AngularSpeed` / `b.Lifetime` 等子弹字段影响行为

需要引用类型字段深拷时 override `Clone()`,默认 `MemberwiseClone` 对值类型字段够用。

**Modifier 如何查询空间信息(追踪 / 找最近敌人 / 任何需要"看到"其他 hitbox 的逻辑)**
- `CollisionService` 公开只读属性 `Grid`(类型 `UniformGrid`),每帧 `LateUpdate` 开头 Clear + 重建,内含所有活跃 hitbox(玩家 + 敌人 + 子弹)。
- 在 modifier 内调 `CollisionService.Instance.Grid.QueryRadius(point, radius)`,拿半径 r 内的所有 `HitboxComponent`,自己按 `hb.Team` 过滤(玩家阵营 / 敌人阵营 / 中立),按距离 / 角度等选目标。
- `QueryRadius` 是 `UniformGrid` 暴露的公开 API,基于均匀网格索引,O(候选数) 选目标;同 `Query3x3(center)` 是 `QueryRadius(center, cellSize)` 的薄封装。
- **生命周期约定**:modifier 的 `Modify` 由 `Bullet.Update` 调用(`Update` 阶段),`CollisionService.LateUpdate` 才建网格 —— 同一帧内 modifier 读到的网格是**上一帧**的快照。STG 帧率下两帧差异 < 1/60s,实际无感知,但**不能**假设"本帧新生成的目标立刻可见"。

**为什么不在 BulletPool.Get 里直接传 prefab 数组?**
- 当前架构只走 `AttachModifiers` 单条挂载路径(在 pool 8 参重载里),保证 modifier 挂载的唯一入口;
- 如果未来需要"modifier 列表"或"modifier 池化",改 `AttachModifiers` 一处即可,FirePattern 子类不动。

---

## 3. 射击模式系统(FirePattern)

**职责:** 把"**射什么子弹 + 怎么射**"封装进 ScriptableObject 资产。一个 .asset = 一种弹幕形态,改资产即改变所有引用它的对象。

**协作边界:**
- 被 `FireAction`(敌人行为)、`PlayerShooting`(玩家主炮)、`PlayerOptions`(子机开火)、`BossController`(Boss 开火)调用。
- 实际开火委托给 `BulletPool.FireGroup` / `BulletPool.Get`;阵营由发射者透传,prefab 不需要静态配置。

**FirePattern 字段**:
| 字段 | 类型 | 作用 |
|---|---|---|
| `BulletPrefab` | `Bullet` | 该 pattern 发射的子弹 prefab;留空走 BulletPool.DefaultPrefab 兜底 |
| `ModifierPrefabs` | `[SerializeReference, SR] BulletModifier[]` | 每颗子弹自动挂的 modifier(详见 §2.2) |
| `Speed` | `float` | 飞行速度,默认 5 |
| `AngularSpeed` | `float` | 固定角速度(弧度/秒),0 = 不转;等同于 modifier 里 Steer 的效果 |
| `Damage` | `float` | 玩家弹伤害;敌人弹忽略 |

**扩展点:**
- 新增弹幕形态:新建 `FirePattern` 子类 + 创建对应 .asset(详见 `Assets/Scripts/Bullet/FirePattern/`)。
- 子类在生成子弹时**必须**用基类的 `protected Bullet SpawnBullet(...)` helper,**不要直接调** `pool.Get`,否则 modifier 不会挂上。

详见 `Assets/Scripts/Bullet/FirePattern*`。

### 3.1 FireExtension 扩展点(基础发射逻辑的多态扩展)

**职责:** 对 FirePattern 子类的"中心方向解析"做可插拔的多态扩展。任何"决定本轮发射方向"的策略(BaseAngle / 瞄准玩家 / 瞄准 Boss / 每发旋转 / 振荡...)都属于 FireExtension,而非 FirePattern 本身 —— FirePattern 负责"怎么射"的几何(几颗 / 扇形 / 环形 / 容器),FireExtension 负责"朝哪儿射"。

**协作边界:**
- 字段挂在 FirePattern 基类上(`[SerializeReference, SR] FireExtension FireExtension`),所有 FirePattern 子类(Arc / Line / Ring / Composite / 未来)自动支持 Inspector 下拉。
- 子类通过静态 helper `FireExtensionResolver.ResolveCenterAngle(...)` 拿方向;helper 与基类解耦,可被其他系统复用。
- 走 SpawnBullet 路径的"必须走基类 helper"约束不变(详见 §3)。
- `CompositeFirePattern` 是纯容器(只有 `Children` + 透传 + 递归 GetFireCount),自己不持任何发射字段 —— 它对 FireExtension 的处理是"透传给 children,各自解析"。

**类型:**
- 基类:`FireExtension`(纯 C# `[Serializable] abstract class`)
- 内置子类:`BaseAngleFireExtension` `[SRName("FireExtension/Base")]` / `PlayerAimFireExtension` `[SRName("FireExtension/Player Aim")]`
- Helper:`FireExtensionResolver`(静态,Null-safe,`extension == null` 时 fallback 到默认方向,与旧版 normal 行为一致)

**扩展点:** 新增"瞄准 X / 旋转 / 振荡"等策略 = 新建 `FireExtension` 子类 + 加 `[SRName("FireExtension/<名字>")]`,自动出现在所有 FirePattern 资产的下拉菜单。详见 `Assets/Scripts/Bullet/FireExtension/`。
---

## 4. 敌人 AI(BehaviorFlow + EnemyAction)

**职责:** 用"**时间序列 + 多态 Action + 数据驱动**"描述敌人行为。**所有行为在 Inspector 里用下拉菜单自由组合**,无需改代码。

**职责分工:**
- `BehaviorFlow`(SO 资产) —— 持有 `Actions: EnemyAction[]`,每条 Action 有 `Duration` + 三段式生命周期。
- `EnemyAction`(`[Serializable] class` + `[SerializeReference]`) —— 原子 / 容器行为单元:Fire / Move / Wait / SelfDestruct / Parallel / Sequence / ...。
- `MoveBehaviour`(`[Serializable] class` + `[SerializeReference]`) —— Move 内部再委托一层:匀速 / 线性 / 贝塞尔 / 圆形 / 追踪 / ...
- `ShooterEnemy`(MonoBehaviour) —— 极薄的总控组件,持 `Flow` 引用 + Update 驱动。

**协作边界:**
- `ShooterEnemy.Flow` 拖一个 `.flow` 资产即可,无需挂其他组件。
- Boss 走自己的 `BossController + ShooterPhase`,**不挂 ShooterEnemy**(避免组件污染,详见 §5)。
- `BehaviorFlow.Instantiate()` 用 `Object.Instantiate(SO)` 深拷贝 Actions 数组 —— **多个敌人引用同一资产互不干扰**。

**扩展点:**
- 新增敌人行为(动画 / 隐身 / 加血):新建 `EnemyAction` 子类,加 `[Serializable, SRName("Action/<名字>")]`。
- 新增移动方式(贝塞尔 / 圆形 / 追踪):新建 `MoveBehaviour` 子类,同套路。
- 详见 `Assets/Scripts/Enemy/AI/`。
- 新增"行为流"(符卡 / 小怪模式):右键 → Create → STG → Behavior Flow,创建 SO 资产并配置 Actions。

详见 `Assets/Scripts/Enemy/`。

---

## 5. Boss 系统(BossController + 多阶段 + 多管血)

**职责:** 与普通敌人**正交**的 Boss 编排层,提供多阶段 / 阶段触发条件 / 多管血。普通敌人就一段行为流,Boss 需要这些"上层编排"概念,所以单独建一层。

**协作边界:**
- Boss GameObject 上挂 `BossController + BossHealth + BossShotCounter`,**不挂 ShooterEnemy**。
- 阶段用 `ShooterPhase` 直接持 BehaviorFlow 资产,boss 行为复用普通敌人那套行为流。
- 多管血 / 多阶段 / 信号切换都在 Inspector 配,无需新代码。

**扩展点:**
- 新增 Boss 阶段:新建 `BossPhase` 子类,加到 `BossController.Phases`(详见 `Assets/Scripts/Enemy/Boss/`)。

---

## 6. 扩展指南

> **"我要加新功能,改哪里?"** 的入口在 [`CONTRIBUTING.md §2`](./CONTRIBUTING.md#2-架构扩展入口),那里是给"动手前"看的速查表。
>
> 本节只讲**架构原则**:加新东西时应该走哪条套路,以及为什么。

### 6.1 多态扩展点的统一套路

项目里有 9 个用 `[SerializeReference]` + 子类多态的扩展点,全部走同一个三步套路:

1. 在约定文件夹新建 `<你的>类名.cs`
2. 继承对应的抽象基类 + 加 `[Serializable, SRName("<下拉菜单路径>")]`
3. override 该 override 的方法

| 扩展点 | 基类 | 用途 |
|---|---|---|
| 弹幕形态 | `FirePattern` | 新增螺旋 / 樱花 / 自定义轨迹 |
| 弹幕角度 / 瞄准扩展 | `FireExtension` | 基础发射逻辑的多态扩展(BaseAngle / 瞄准玩家 / 瞄准 Boss / 每发旋转 / 振荡...),挂在 FirePattern 上,详见 §3.1 |
| 子弹行为 | `BulletModifier` | 加速 / 转向 / 追踪 / 分裂(追踪类用 `CollisionService.Grid` 查候选,见 §2.5) |
| 敌人行为 | `EnemyAction` | 新增攻击 / 移动 / 自毁 / 容器 |
| 移动方式 | `MoveBehaviour` | 贝塞尔 / 圆形 / 追踪 |
| Boss 阶段 | `BossPhase` | 阶段 = 行为流,或自定义阶段体 |
| Boss 切换条件 | `BossSignal` | 开场过场 / 玩家撞 N 次 / 自定义条件 |
| 子机位置 | `OptionPositionForm` | 子机怎么排队 |
| 关卡条目 | `SpawnEntry` | 配关卡时一行下拉 |
| 关卡编辑器画法 | `ISpawnEntryDrawer` | 编辑器时间轴 / Scene 视图怎么画 |

**为什么 `[SerializeReference]` + `[SRName]`(而不是直接子类列表 / enum)**:
- 改 Inspector 不需要重编 / 不动宿主组件代码
- 加新子类 = 加一个 .cs,无需注册中心 / 反射配置
- 资产层与代码层解耦,跨场景 / 跨敌人共享行为流直接拖资产

### 6.2 数据驱动 vs 代码逻辑的边界

- **可配置 → SO 资产**:`FirePattern` / `BehaviorFlow` / `LevelDefinition` 都用 ScriptableObject。改一个 .asset 改全场景所有引用对象。**不要把"美术数值 / 关卡时间表 / 弹幕参数"硬编码进 .cs**。
- **运行时状态 → `[Serializable] class`**:`EnemyAction` / `MoveBehaviour` / `BossPhase` / `BossSignal` / `OptionPositionForm` 全是普通 `[Serializable] class` + `[SerializeReference]`。原因:这些对象要持有运行时状态(`_timer` / `_idx` 等),SO 跨实例共享会出问题。
- **数学 / 算法 / 正交层 → static class**:`HitboxMath` / `LevelEditorMath` 这类纯计算层,不带状态、不需要 Inspector 配置,放 static 类。
- **总控 / 生命周期 → MonoBehaviour**:`Player` / `Enemy` / `BossController` / `BulletPool` / `LevelController` 是 MonoBehaviour,管 Unity 生命周期;它们内部**引用** SO / `[SerializeReference]` 对象,不**继承**它们。

### 6.3 加新功能时的反模式

- ❌ 在 `Enemy` 上加 `if (敌人类型 == A) ... else if (类型 == B) ...`
  → ✅ 加新 `EnemyAction` 子类,配进 `.flow` 资产的 `Actions[]`
- ❌ 在 `Bullet` 上加分支做"加速弹 / 减速弹 / 分裂弹"
  → ✅ 加 `BulletModifier` 子类,外部 `bullet.AddModifier(...)` 挂载
- ❌ 在 `BossController` 上 `if (currentPhase == Phase1 && hp < 50) switch(...)`
  → ✅ 在 `PhaseTrigger[]` 里配 `Signal + Op + Threshold`,Inspector 下拉选条件
- ❌ 把"关卡时间表"写死在 `LevelController.cs`
  → ✅ 建 `LevelDefinition` 资产,在 `Entries[]` 数组里组合条目

---

## 7. 玩家系统(Player + 子机 + 多态位置)

**职责:** 玩家主控(单例 + 输入分发) + 八方向移动 + 按住攻击 + 残机/火力级/无敌 + 子机系统。整体走"**数据驱动 + 多态位置**"的套路,与项目既有扩展机制一致。

**协作边界:**
- 主炮与子机开火**共用同一触发源**,松开攻击键时同步停。
- 子机数量由 `PlayerHealth.PowerLevel` 单向驱动;子机不会反向改 PowerLevel,避免循环依赖。
- 子机开火形态也是 FirePattern 资产(`OptionFirePatterns[]`),直接复用敌人那套弹幕体系。
- 玩家事件只发通知,不硬编码死亡动画 / 特效 —— "事件层与表现层分离"。

**输入方案(双重兼容):**
- 装了 `com.unity.inputsystem` → 用 `PlayerInput` 组件(Behavior=Invoke C# Events)。
- 没装 → 直接挂 `LegacyInputDriver`,每帧 `Input.GetKey` 灌给 Movement / Shooting。
- **二选一,不要两个都挂,会双重输入。**

**扩展点:**
- 新增子机位置形态:新建 `OptionPositionForm` 子类 + `[Serializable, SRName("Form/<名字>")]`(详见 `Assets/Scripts/Player/`)。

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
- Boss 暂时不挂 HitboxComponent(走 `BossHealth` 自己的逻辑,正交);若未来想用同一套 AABB,可直接挂 HitboxComponent 子类。
- `CollisionService` 是唯一一处真正遍历"子弹 vs 实体"的地方,不直接读写 HP 字段 —— 玩家弹伤害来自 `b.Damage`,敌人弹命中无视 Damage,固定扣玩家 1 命。
- modifier 通过 `CollisionService.Grid` 读网格是**只读**的,不得调用 `Clear` / `Insert`(否则会破坏本帧的碰撞判定)。

详见 `Assets/Scripts/Hitbox/`。

---

## 9. 关卡系统(LevelDefinition + LevelController)

负责把"什么时间点生成什么敌人"封装成可复用的 SO 资产,并在场景里按时间轴驱动执行。**与现有层完全正交** —— 不修改 Player / Enemy / Bullet 任何代码,仅复用 `ShooterEnemy` + `BehaviorFlow` + `BulletPool` 等既有资产。

**职责分工:**
- `LevelDefinition`(SO 资产) —— 持有 `Entries: SpawnEntry[]`(多态下拉)+ `Duration`(总时长)+ `Pool`(可选专用 BulletPool)。
- `LevelController`(场景单例,`Singleton<T>`) —— 持有 `Definition` + `Runtime`,提供关卡事件(OnLevelStart / OnLevelComplete / OnEnemySpawned / OnBossSpawned / OnBossDefeated)。

**协作边界:**

- **不侵入 Enemy/Bullet/Player**:关卡只 `Instantiate(prefab)`,prefab 自己带 `ShooterEnemy` + `BehaviorFlow` + Hitbox/Health,沿用既有 AI 链。
- **总控统一挂载**:`LevelController` 场景里只挂一份。
- **事件发送权集中在 Controller**:`SpawnEntry` 子类不直接 Invoke 事件,而是通过 `LevelController.Instance` 的公开方法触发 —— 事件层与表现层分离。
- **数据驱动一致性**:`LevelDefinition.Entries` 与 `BehaviorFlow.Actions` 同样走 `[SerializeReference, SR]`,Inspector 下拉体验完全一致。

**扩展点:**

- **新条目类型**(等玩家到位 / 周期性 / 全清触发 / 概率触发 / ...):新建 `SpawnEntry` 子类 + `[SRName("Entry/<名字>")]`,在 `ShouldTrigger` / `OnTrigger` 两个钩子实现,无需改 `LevelController`(详见 `Assets/Scripts/Level/`)。
- **可视化时间轴编辑器**:已实现完整的时间轴 / 列表 / 详情面板 + Preview + Scene Gizmos。详见 [§10](#10-关卡编辑器子系统)。


---

## 10. 关卡编辑器子系统

> **本节只讲"它在架构里的位置"**,不讲怎么用 / 怎么扩展。
>
> - **使用者**(配关卡):见 [`LEVEL_EDITOR.md`](./LEVEL_EDITOR.md)
> - **扩展者**(加新 Drawer / Preview):见 [`Assets/Scripts/Level/Editor/README.md`](./Assets/Scripts/Level/Editor/README.md)

### 在架构里的位置

- **与运行时完全正交**:`Editor/` 是独立 asmdef(`includePlatforms = ["Editor"]`),不修改运行时任何类型,不反向依赖运行时的可序列化细节。运行时程序集对编辑器 0 感知。
- **只读运行时 SO 资产**:打开编辑器 = 拖一份 `LevelDefinition.asset` 进来;Editor 程序集不发明新数据格式,关卡数据 100% 在运行时侧描述。
- **多态扩展走两条独立通道**:`SpawnEntry` 子类通过 SREditor 的 `[SRName]` 在 Inspector 下拉;`ISpawnEntryDrawer` 子类通过 `LevelEditorDrawerRegistry`(反射枚举)被 Editor 窗口拿到。两条通道都用"反射枚举 + 第一个匹配胜出"的同一套路。
- **Preview 与运行时解耦**:编辑器自己定义 `ILevelEditorPreview` 接口 + 默认实现 `LevelEditorPlayer`,不依赖 `LevelRuntime` 的时间推进逻辑;持续条目用 Editor 侧的 `StubLevelRuntime` 接管,Stop 时统一销毁临时 GameObject,不污染场景。

### 与既有层的关系

| 既有层 | 编辑器怎么用 |
|---|---|
| `LevelDefinition` (SO) | 主窗口直接 `SerializedObject` 渲染,Editor 侧**不复制**字段 |
| `SpawnEntry` (多态条目) | 反射枚举所有子类,加 `+ Add ▾` 下拉;`ISpawnEntryDrawer` 按 type 分派 |
| SREditor 插件 | 右栏 `LevelEntryDetailView` 直接 `EditorGUILayout.PropertyField`,**0 行自定义 IMGUI**;下拉 / 折叠 / missing type 检测全由 SREditor 接管 |
| `BehaviorFlow` / `FirePattern` | 菜单平行,扩展点在它们各自的 Inspector 里配,编辑器不重复配 |

**总 ~1500 行**,14 个源文件 + 1 个 asmdef,**对运行时的影响 = 0**。

