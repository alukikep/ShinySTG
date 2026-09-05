# ShinySTG 架构说明

> 本文档面向项目维护者(以及未来的你自己),目的是**理解现有架构的工作原理**,便于安全地扩展功能。
> 阅读顺序建议:第 1 章 → 第 2 章 → 第 3 章,然后按需翻后面的章节。

---

## 目录

1. [整体架构一览](#1-整体架构一览)
2. [子弹系统(Bullet / BulletPool / BulletModifier)](#2-子弹系统bullet--bulletpool--bulletmodifier)
3. [射击模式系统(FirePattern SO 体系)](#3-射击模式系统firepattern-so-体系)
4. [敌人 AI 时间轴(BehaviorFlow + EnemyAction)](#4-敌人-ai-时间轴behaviorflow--enemyaction)
5. [Boss 系统(BossController + 多阶段 + 多管血)](#5-boss-系统bosscontroller--多阶段--多管血)
6. [扩展指南](#6-扩展指南)
7. [常见问题 / 设计决策记录](#7-常见问题--设计决策记录)

---

## 1. 整体架构一览

整个项目遵循"**职责分离 + 数据驱动 + 组合优于继承**"的原则,可分为三层:

```
┌──────────────────────────────────────────────────────────────────┐
│  游戏场景 (Scene)                                                  │
│   ├─ 玩家 (Player)                                                │
│   ├─ 普通敌人:挂 ShooterEnemy(持 BehaviorFlow 资产)              │
│   ├─ Boss:挂 BossController + BossHealth + BossShotCounter       │
│   └─ BulletPool (场景单例,负责子弹的复用)                            │
└──────────────────────────────────────────────────────────────────┘
            │                    │                    │
            ▼                    ▼                    ▼
┌──────────────────────┐ ┌──────────────────────┐ ┌────────────────┐
│ 敌人 AI 层           │ │ Boss 系统层         │ │ 子弹层          │
│ ShooterEnemy         │ │ BossController       │ │ Bullet         │
│  └─ BehaviorFlow SO  │ │  ├─ BossPhase[]      │ │ BulletPool     │
│       └─ EnemyAction[]│ │  ├─ BossSignal[]     │ │ BulletModifier │
│            ├─ Fire... │ │  ├─ PhaseTrigger[]  │ │ FirePattern SO │
│            ├─ Move... │ │  └─ BossHealth      │ │   ├─ Ring      │
│            ├─ Wait... │ │       (多管血)      │ │   ├─ Line      │
│            ├─ Parallel│ │ ShooterPhase        │ │   ├─ Arc       │
│            └─ Sequence│ │  └─ BehaviorFlow SO │ │   └─ Composite │
└──────────────────────┘ └──────────────────────┘ └────────────────┘
            │
            ▼
┌──────────────────────────────────────────────────────────────────┐
│ 通用层                                                            │
│  Singleton<T> / PersistentSingleton<T>  (单例基类)                 │
└──────────────────────────────────────────────────────────────────┘
```

**核心设计原则:**

| 原则 | 体现 |
|---|---|
| **数据驱动** | FirePattern 用 ScriptableObject 定义"怎么射",Inspector 直接拖资产即可 |
| **组合优于继承** | 敌人行为是 `EnemyAction[]` 数组而不是一大坨 `class BossEnemy : Enemy`,完全靠 Inspector 拼装 |
| **多态通过 SerializeReference** | 用项目自带的 SREditor 插件,行为/移动在 Inspector 里通过下拉菜单选择具体类型,无需改代码 |
| **对象池** | 子弹频繁创建/销毁,用 `BulletPool` 复用,避免 GC 抖动 |
| **行为流资产化** | `BehaviorFlow` SO 把"一段完整的敌人行为"封装成可复用资产,多个敌人/boss 共享同一份逻辑 |

---

## 2. 子弹系统(Bullet / BulletPool / BulletModifier)

文件位置:
- `Assets/Scripts/Bullet/Bullet.cs`
- `Assets/Scripts/Bullet/BulletPool.cs`
- `Assets/Scripts/Bullet/BulletModifier.cs`

### 2.1 `BulletPool`(对象池,场景单例)

**角色:** 整个游戏的子弹池,所有子弹的"出生与回收"都走它。

**原理:**
```
_Fire() → Get(prefab, pos, angle, speed, angular) → 弹出一颗空闲 Bullet
                                                   ↓
                                              Update() 飞行
                                                   ↓
                                              出界/命中 → Return(this) 回收
```

**关键字段:**
- `Stack<Bullet> _available` —— 空闲子弹栈
- `HashSet<Bullet> _active` —— 当前活跃子弹集合
- `Bullet DefaultPrefab` —— 兜底 prefab(当 Pattern 没指定时使用)
- `InitialSize` —— 启动时预创建的子弹数量

**对外 API:**
```csharp
Bullet Get(Bullet prefab, Vector2 pos, float fireAngleRad, float speed, float angularSpeed);
void  Return(Bullet bullet);
void  FireGroup(FirePattern pattern, Vector2 pos, float rotationRad); // 便捷:触发一个 pattern
```

**注意事项:**
- `BulletPool` 继承 `MonoBehaviour`,但它**没有**继承项目里的 `Singleton<T>`(是个手写单例,直接用 `Instance`)。
- 池容量不足时会 `Instantiate` 新子弹,但**会走 `_active.Add`**,所以回收时一定要 `Return` 才能再被复用。
- 场景里必须有且仅有一个挂着 `BulletPool` 的 GameObject(通常放在 `_Bootstrap` 之类的常驻对象上)。

### 2.2 `Bullet`(子弹本体)

**每一帧 `Update` 流程:**

```
1. Lifetime += dt                                      累计存活时间
2. foreach (modifier in _modifiers) modifier.Modify() 子弹效果(加速/转向/...)
3. SteerAngle += AngularSpeed * dt                    当前飞行方向按角速度更新
4. transform.position += dir(SteerAngle) * Speed * dt 按当前方向前进
---

## 3. 射击模式系统(FirePattern SO 体系)

文件位置:
- `Assets/Scripts/Bullet/FirePattern.cs`(基类)
- `Assets/Scripts/Bullet/FirePattern/Ring/RingFirePattern.cs`
- `Assets/Scripts/Bullet/FirePattern/Line/LineFirePattern.cs`
- `Assets/Scripts/Bullet/FirePattern/Arc/ArcFirePattern.cs`
- `Assets/Scripts/Bullet/FirePattern/Composite/CompositeFirePattern.cs`

### 3.1 核心思想

把"**射什么子弹 + 怎么射**"完全封装进一个 ScriptableObject 资产。**改一个 SO 资产 = 改变全场景所有引用它的敌人开火方式**,完全无需改代码或重新编译。

### 3.2 基类 `FirePattern`

```csharp
public abstract class FirePattern : ScriptableObject
{
    public Bullet BulletPrefab;       // 该 pattern 用什么子弹
    public float Speed = 5f;          // 子弹速度(可被子类覆盖)
    public float AngularSpeed = 0f;    // 子弹角速度(可被子类覆盖)
    public float BaseAngle = 270f;     // 基准朝向(度,通常 270=下)

    public abstract void Fire(Vector2 position, float rotationRad,
                              BulletPool pool, Bullet owner = null);
}
```

**重要语义:**
- `BaseAngle` 是**度**,每个具体 pattern 可以再定义自己的 `BaseAngle`(因为子类可能需要不同基准)。
- `Fire` 的 `rotationRad` 是**相对** BaseAngle 的**弧度**增量,方便运行时做"边射边转向"。
- `Bullet owner` —— 可选,标识"谁射的"。目前没怎么用,但留口子给以后做"自家子弹不伤自"。

### 3.3 子类一览

| Pattern | 文件 | 行为 | 关键参数 |
---

## 4. 敌人 AI 时间轴(BehaviorFlow + EnemyAction)

文件位置:
- `Assets/Scripts/Enemy/AI/BehaviorFlow.cs`(行为流 SO 资产)
- `Assets/Scripts/Enemy/AI/BehaviorFlowRuntime.cs`(运行时驱动器)
- `Assets/Scripts/Enemy/ShooterEnemy.cs`(行为流播放机组件)
- `Assets/Scripts/Enemy/AI/EnemyAction.cs`(行为基类)
- `Assets/Scripts/Enemy/AI/MoveBehaviour.cs`(移动模块基类)
- `Assets/Scripts/Enemy/AI/MoveBehaviours/LinearMove.cs`
- `Assets/Scripts/Enemy/AI/Actions/FireAction.cs`
- `Assets/Scripts/Enemy/AI/Actions/MoveAction.cs`
- `Assets/Scripts/Enemy/AI/Actions/WaitAction.cs`
- `Assets/Scripts/Enemy/AI/Actions/SelfDestructAction.cs`
- `Assets/Scripts/Enemy/AI/Actions/ParallelAction.cs`
- `Assets/Scripts/Enemy/AI/Actions/SequenceAction.cs`

### 4.1 设计动机

传统做法:`class BossEnemy : Enemy` 里写一大坨 `if (state == X) { ... } else if (state == Y) { ... }`。  
问题:每加一种行为/每个新 Boss 都要改代码/加类,Inspector 改不了。

**本项目做法:** 把敌人行为做成"**时间序列 + 多态 + 数据驱动**"。**所有行为在 Inspector 里用下拉菜单自由组合**,无需改代码。

### 4.2 核心抽象:`EnemyAction`

```csharp
public abstract class EnemyAction
{
    public float Duration = 1f;          // 持续秒数,到了自动切换到下一条

    public virtual void OnEnter(Transform enemy) { }   // 进入时(初始化)
    public virtual void OnTick (Transform enemy, float dt) { }  // 每帧
    public virtual void OnExit (Transform enemy) { }   // 退出时(清理)
}
```

**三段式生命周期:**
- `OnEnter`:进入该行为,可重置状态、读初始位置等。
- `OnTick`:每帧执行。
- `OnExit`:行为时间到,被切走之前调用一次,**用于清理**(停止射击 timer、重置 angular speed 等)。

**Duration 语义:**<= 0 表示只执行一帧(下一帧立即切下一条)。

### 4.3 三层组件:`BehaviorFlow` / `BehaviorFlowRuntime` / `ShooterEnemy`

**设计动机:** 旧版 ShooterEnemy 内部自己持有 Actions 数组,导致:
- boss prefab 上要挂多个 ShooterEnemy 组件来切换不同行为流
- 行为流无法在多个敌人/boss 间复用
- Inspector 维护痛苦

**新版架构:三层职责分离**

```
┌──────────────────────────────────────────────────────────┐
│ BehaviorFlow (SO 资产)                                    │
│   - Actions: EnemyAction[]   ← 时间序列配置               │
│   - Loop, StartDelay          ← 行为流元数据              │
│   - Instantiate(): 克隆成 BehaviorFlowRuntime            │
├──────────────────────────────────────────────────────────┤
│ BehaviorFlowRuntime (普通 C# 类)                          │
│   - _index, _elapsedInCurrent, _delayLeft, _started     │
│   - Tick(owner, dt): 驱动时间轴                            │
│   - ForceExit(owner): ShooterPhase 切阶段时调用           │
├──────────────────────────────────────────────────────────┤
│ ShooterEnemy (MonoBehaviour 组件)                         │
│   - 字段: Flow: BehaviorFlow                              │
│   - OnEnable: _runtime = Flow.Instantiate()              │
│   - Update: _runtime.Tick(transform, dt)                  │
└──────────────────────────────────────────────────────────┘
```

**核心循环(在 `BehaviorFlowRuntime.Tick` 里):**

```csharp
public void Tick(Transform owner, float dt)
{
    var actions = _flow.Actions;
    if (actions == null || actions.Length == 0) return;

    // 1. 启动延迟
    if (!_started)
    {
        _delayLeft -= dt;
        if (_delayLeft > 0f) return;
        _started = true;
        AdvanceTo(0, owner);
    }

    // 2. tick 当前 action
    var current = actions[_index];
    current.OnTick(owner, dt);
    _elapsedInCurrent += dt;

    // 3. 到时间,OnExit 并切下一条
    if (_elapsedInCurrent >= current.Duration)
    {
        current.OnExit(owner);
        AdvanceTo(_index + 1, owner);
    }
}

void AdvanceTo(int next, Transform owner)
{
    if (next >= actions.Length)
    {
        if (_flow.Loop && actions.Length > 0) next = 0;
        else { _index = -1; return; }
    }
    _index = next;
    _elapsedInCurrent = 0f;
    actions[_index]?.OnEnter(owner);
}
```

**关键设计:`Instantiate(SO)` 深拷贝**

`BehaviorFlow.Instantiate()` 用 `UnityEngine.Object.Instantiate(this)` 复制 SO。Unity 会自动深拷贝 `[SerializeReference]` 字段,所以**多个 ShooterEnemy 引用同一 .flow 资产时,运行时互不干扰**(每个敌人有独立的 `_index / _elapsed` 状态)。

**ShooterEnemy 完整源码(28 行):**

```csharp
public class ShooterEnemy : MonoBehaviour
{
    [Tooltip("拖入一个 BehaviorFlow 资产。")]
    public BehaviorFlow Flow;

    BehaviorFlowRuntime _runtime;

    void OnEnable() { _runtime = Flow != null ? Flow.Instantiate() : null; }
    void Update() { _runtime?.Tick(transform, Time.deltaTime); }
}
```

### 4.4 内置 Action 一览

| Action | 作用 | 关键参数 |
|---|---|---|
| **FireAction** | 按 FireRate 持续发射一个 FirePattern | `Pattern`、`FireRate`、`AimOffsetDeg` |
| **MoveAction** | 持续移动(委托给 MoveBehaviour) | `Move`(可切换实现) |
| **WaitAction** | 什么都不做,只占 Duration | (无) |
| **SelfDestructAction** | Duration 到时销毁敌人 | (无) |
| **ParallelAction** | **并行**容器,内含多个子 Action | `Children[]`、`Duration`(封顶) |
| **SequenceAction** | **顺序**容器,纯 Inspector 折叠分组用 | `Children[]`(Duration 字段被忽略) |

**Inspector 下拉菜单分组(由 `[SRName("路径")]` 控制):**
```
Action/Fire
Action/Move
Action/Wait
Action/Self Destruct
Action/Parallel
Action/Sequence
Move/Linear
```

### 4.5 MoveBehaviour 子系统

`MoveAction` 不直接管移动,而是委托给一个 `MoveBehaviour`(又是 `[SerializeReference]`)。

```csharp
public abstract class MoveBehaviour
{
    public virtual void OnEnter(Transform enemy) { }
    public abstract void OnTick(Transform enemy, float dt);
    public virtual void OnExit(Transform enemy) { }
}
```

**当前实现:**

| 类 | 行为 | 关键参数 |
|---|---|---|
| `LinearMove` | 匀速直线 | `Direction`(Down/Up/Left/Right/ToPlayer/Custom)、`Speed`、`CustomAngleDeg` |

`ToPlayer` 模式会在 `OnEnter` 时**锁定一次**朝向玩家的方向(之后不再追踪);以后若需要"持续追踪"则新增一个 `HomingMove` 子类即可。

### 4.6 容器类详解(关键)

#### `ParallelAction` —— 同时跑多个

```
Parallel.Duration = 3.0
  ├─ Fire   (Duration = 3.0)   ← 整个 3 秒都在射
  └─ Move   (Duration = 3.0)   ← 整个 3 秒都在走
```

**实现:** 每个 child 有独立的 `_childElapsed` 计时器;`OnTick` 一次性调用所有未结束的 child 的 `OnTick`;`OnExit` 只对"还活着"的 child 调一次 `OnExit`(防止重复清理)。

**Duration 字段是"封顶时间":** 即使某个 child 配置成 Duration=999,Parallel.Duration=5,也会在第 5 秒被强制清理。

#### `SequenceAction` —— 纯折叠分组

语义**完全等价于**把 `Children` 平铺到外层 `Actions` 数组。  
存在的唯一理由:让 Inspector 里几十条 Action 的列表能折叠成几个组,**便于阅读**。

**自身 Duration 字段不生效**——时间由外层时间轴驱动。

#### 无限嵌套

`Parallel.Children` 和 `Sequence.Children` 的元素类型都是 `EnemyAction`,所以可以:
```
Sequence
 ├─ Parallel
 │   ├─ Fire
 │   └─ Move
 ├─ Wait
 └─ SelfDestruct
```
深度不限。

### 4.7 如何新增一种 Action

**3 步,完全无需改 ShooterEnemy:**

1. 在 `Assets/Scripts/Enemy/AI/Actions/` 下新建 `<你的>Action.cs`
2. 继承 `EnemyAction`,加 `[Serializable, SRName("Action/<你的名字>")]`
3. override `OnEnter / OnTick / OnExit`(按需)

```csharp
[Serializable, SRName("Action/Animate Scale")]
public class AnimateScaleAction : EnemyAction
{
    public Vector3 FromScale = Vector3.one;
    public Vector3 ToScale = new Vector3(1.5f, 1.5f, 1.5f);

    public override void OnEnter(Transform enemy) { enemy.localScale = FromScale; }
    public override void OnTick(Transform enemy, float dt)
    {
        float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.0001f, Duration));  // 你需要自己加 elapsed 字段
        enemy.localScale = Vector3.Lerp(FromScale, ToScale, t);
    }
}
```

下次在 Inspector 里就能选到 `Action/Animate Scale`。

### 4.8 如何新增一种移动方式

同上,在 `Assets/Scripts/Enemy/AI/MoveBehaviours/` 下新建 `MoveBehaviour` 子类即可。

---

## 5. Boss 系统(BossController + 多阶段 + 多管血)

文件位置:
- `Assets/Scripts/Enemy/Boss/BossController.cs`(主驱动引擎)
- `Assets/Scripts/Enemy/Boss/BossHealth.cs`(多管血组件)
- `Assets/Scripts/Enemy/Boss/BossShotCounter.cs`(全局开火计数)
- `Assets/Scripts/Enemy/Boss/BossPhase.cs`(阶段抽象)
- `Assets/Scripts/Enemy/Boss/Phases/ShooterPhase.cs`(行为流阶段)
- `Assets/Scripts/Enemy/Boss/PhaseTrigger.cs`(退出条件)
- `Assets/Scripts/Enemy/Boss/Signals/BossSignal.cs`(信号抽象)
- `Assets/Scripts/Enemy/Boss/Signals/HpSignal.cs`(单管 HP%——兼容别名)
- `Assets/Scripts/Enemy/Boss/Signals/CurrentBarPercentSignal.cs`(当前管剩余 %)
- `Assets/Scripts/Enemy/Boss/Signals/CurrentBarIndexSignal.cs`(当前管编号)
- `Assets/Scripts/Enemy/Boss/Signals/TotalHpPercentSignal.cs`(所有管累计 %)
- `Assets/Scripts/Enemy/Boss/Signals/PhaseTimeSignal.cs`(阶段内时间)
- `Assets/Scripts/Enemy/Boss/Signals/ShotsFiredSignal.cs`(累计开火数)

### 5.1 设计动机

普通敌人一段行为流就够了,boss 需要:
- **多阶段**(符卡 / 非符,东方式)
- **阶段切换条件**(HP 阈值 / 时间 / 开火数 / ...)
- **每阶段独立行为流**(不同弹幕、不同节奏)
- **多管血**(一管血打空切下一管,残血暴走等)

如果硬塞进 ShooterEnemy + Actions 体系会污染架构。所以单独建一层 `BossController` + `BossPhase` + `BossSignal` + `PhaseTrigger`,与普通敌人**正交**。

### 5.2 整体数据流

```
Boss GameObject
  ├─ BossHealth       (HealthBar[] 多管血,TakeDamage 自动切管)
  ├─ BossShotCounter  (场景单例,每发一弹计数)
  └─ BossController
      ├─ Signals: BossSignal[]
      │     ├─ HpSignal / CurrentBarPercentSignal
      │     ├─ CurrentBarIndexSignal
      │     ├─ TotalHpPercentSignal
      │     ├─ PhaseTimeSignal
      │     └─ ShotsFiredSignal
      └─ Phases: BossPhase[]
            ├─ ShooterPhase → 持 BehaviorFlow SO 资产
            └─ (其他 phase 子类可扩展)

BossController.Update:
  1. signals.Tick(this, dt)        ← 累加内部状态
  2. currentPhase.OnTick(boss, dt) ← 调用 BehaviorFlowRuntime.Tick
  3. currentPhase.ShouldExit()     ← 任意 trigger 满足就 NextPhase()
```

### 5.3 `BossHealth` 多管血

```csharp
public class BossHealth : MonoBehaviour
{
    public HealthBar[] Bars;   // 多管血,每管打空自动切下一管
    public int CurrentBarIndex;

    public float CurrentBarPercent;  // 当前管剩余 %
    public float TotalHpPercent;     // 所有管累计剩余 %
    public bool   IsDead;            // 全部打空

    public void TakeDamage(float dmg);  // 自动扣穿、触发 OnBarDepleted
    public event Action<int> OnBarDepleted; // 打空时触发(int = BarIndex)
}
```

**兼容老配置:** 如果 `Bars` 为空,`HpPercent` 退化为单管模式,旧 prefab 不破坏。

**属性语义:**

| 属性 | 用途 |
|---|---|
| `HpPercent` | 单管剩余 % —— 兼容老 HpSignal |
| `CurrentBarPercent` | 当前管剩余 % —— 管内阶段切换 |
| `CurrentBarIndex` | 当前管编号 —— 打完第 N 管切阶段(单调递增,不会一帧穿多阶段) |
| `TotalHpPercent` | 所有管累计 % —— 残血触发 |

### 5.4 `BossSignal` + `PhaseTrigger`(退出条件机制)

**为什么用"信号"而不是"transition 内嵌在 phase 里"?**

把 trigger 抽成全局信号池好处:
- 同一个 signal 可被多个 phase 监听(共享)
- 新增 transition = 新建一个 signal 类(开扩展,跟 Action/MoveBehaviour 一个套路)
- phase 之间解耦,phase A 不知道 phase B 是什么

```csharp
public abstract class BossSignal
{
    public virtual void OnAttach(BossController boss) { }
    public virtual void Tick(BossController boss, float dt) { }
    public abstract float CurrentValue { get; }
}

public class PhaseTrigger
{
    [SerializeReference, SR] public BossSignal Signal;
    public ComparisonOp Op = ComparisonOp.LessOrEqual;
    public float Threshold = 50f;
    public bool IsSatisfied();
}

public enum ComparisonOp { LessThan, LessOrEqual, Equal, GreaterOrEqual, GreaterThan }
```

**内置 Signal:**

| Signal | CurrentValue | 用途 |
|---|---|---|
| `CurrentBarPercentSignal` | 当前管剩余 % | 管内切阶段 |
| `CurrentBarIndexSignal` | 当前管编号(0/1/2/...) | 打完第 N 管切阶段 |
| `TotalHpPercentSignal` | 所有管累计 % | 残血触发 |
| `PhaseTimeSignal` | 阶段内已用秒数 | "过 N 秒切下一阶段" |
| `ShotsFiredSignal` | boss 累计开火数 | "射 N 发切下一阶段" |
| `HpSignal` | = CurrentBarPercent(兼容别名) | 老配置不破坏 |

**扩展:** 新建 `BossSignal` 子类,加 `[Serializable, SRName("Signal/<你的>")]`,Inspector 立刻能选。

### 5.5 `BossPhase` + `ShooterPhase`

```csharp
public abstract class BossPhase
{
    [SerializeReference, SR] public PhaseTrigger[] ExitTriggers;
    public bool ShouldExit();  // 任意 trigger 满足即 true
    public abstract void OnEnter(Transform boss);
    public abstract void OnTick (Transform boss, float dt);
    public abstract void OnExit (Transform boss);
}

[Serializable, SRName("Phase/Shooter")]
public class ShooterPhase : BossPhase
{
    public BehaviorFlow Flow;       // ← 持 SO 资产,不再需要 ShooterEnemy 组件
    public bool ResetOnEnter = true;

    // OnEnter: _runtime = Flow.Instantiate();
    // OnTick:  _runtime.Tick(boss, dt);
    // OnExit:  _runtime.ForceExit(boss);
}
```

**关键设计:** BossPhase **不持有 ShooterEnemy 组件引用**,直接持有 `BehaviorFlow` SO 资产。运行时内部 instantiate 一个 `BehaviorFlowRuntime` 自己驱动。**Boss prefab 上不再需要挂任何 ShooterEnemy 组件。**

### 5.6 BulletPool 钩子:开火计数

为了 `ShotsFiredSignal` 能统计 boss 累计开火数,BulletPool.FireGroup 末尾加了 1 行:

```csharp
public void FireGroup(FirePattern pattern, Vector2 pos, float rotationRad)
{
    pattern.Fire(pos, rotationRad, this);
    ShinySTG.EnemyAI.Boss.BossShotCounter.Instance?.OnBossFired(pattern);
}
```

`?.` 保证 BossShotCounter 不存在时直接跳过,**完全不影响普通敌人**。

`FirePattern` 多了个虚方法 `GetFireCount()`,各 pattern override 返回本帧发射数,Composite 递归求和 —— 这样不同形态的弹幕都能正确计数。

### 5.7 Inspector 实际配置

```
▼ Boss GameObject
 ├─ BossHealth 
 │ ▼ Bars
 │   [0] Name: "Bar 1 (符卡 A)" MaxHp: 1000
 │   [1] Name: "Bar 2 (符卡 B)" MaxHp: 800
 │   [2] Name: "Bar 3 (非符)"    MaxHp: 500
 │   [3] Name: "Bar 4 (残血暴走)" MaxHp: 300
 ├─ BossShotCounter
 └─ BossController
     Health: ◀ BossHealth ▶
     ▼ Signals
         [0] Signal/Current Bar Index
         [1] Signal/Current Bar %
         [2] Signal/Total HP %
         [3] Signal/Phase Time
         [4] Signal/Shots Fired
     ▼ Phases
         [0] Phase/Shooter
             Flow: ◀ 符卡A_攻击.flow ▶
             ▼ ExitTriggers
                 [0] Signal: Current Bar Index
                     Op: ≥, Threshold: 1
         [1] Phase/Shooter
             Flow: ◀ 符卡B_弹幕.flow ▶
             ExitTriggers:
                 [0] Signal: Phase Time
                     Op: ≥, Threshold: 30
         [2] Phase/Shooter
             Flow: ◀ 非符_平静.flow ▶
             ExitTriggers:
                 [0] Signal: Total HP %
                     Op: ≤, Threshold: 30
         [3] Phase/Shooter
             Flow: ◀ 残血_暴走.flow ▶
             (无 ExitTriggers → boss 待毙)
```

### 5.8 为什么 BossPhase 不持有 ShooterEnemy 组件?

旧设计:boss 上挂 N 个 ShooterEnemy,ShooterPhase.Shooter 字段拖引用。
问题:
- boss prefab 组件列表很长
- 行为流无法跨敌人复用
- Inspector 维护痛苦

**新设计:** ShooterPhase 直接持 `BehaviorFlow` SO 资产,内部 instantiate BehaviorFlowRuntime 驱动。**完全不需要 ShooterEnemy 组件**。

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

## 7. 常见问题 / 设计决策记录

### Q1:为什么 EnemyAction 是类而不是 ScriptableObject?
**答:** 行为要"持有运行时状态"(比如 FireAction 的 `_timer`),SO 是资产,跨实例共享会出问题。用 `[Serializable] class + [SerializeReference]` 可以让 Inspector 多态下拉选,同时支持每实例独立字段。

### Q2:为什么 MoveAction 不直接是 MoveBehaviour?
**答:** 保持 Action 的"时间轴语义"统一。Move 一定要有 Duration,把它包成 MoveAction 可以挂在 Parallel/外层时间轴上参与编排;如果 MoveBehaviour 直接挂外层,就没有 Duration 概念了。

### Q3:为什么不直接用 Unity 的 StateMachineBehaviour?
**答:** Animator StateMachine 偏动画,语义不够通用。Action 三段式(OnEnter/Tick/Exit)+ 容器组合 + SREditor 下拉,**更符合"行为编排"直觉**,且不依赖 Animator 资源。

### Q4:为什么 BulletModifier 是 MonoBehaviour 而不是普通类?
**答:** 历史原因(方便挂到 GameObject 上查看参数)。**但目前 Bullet 没有自动从 GameObject 收集 modifier 的逻辑**,所以本质等价于"普通类"。如果以后清理代码,可以把它改成普通类,反而更简单(还能省一个 `gameObject` 开销)。

### Q5:为什么 ParallelAction 自己持有 Duration?
**答:** 防止某个子项配错 Duration(比如 9999)导致敌人卡住。Parallel.Duration 是"硬封顶"——到了强行清理所有仍存活的子项。如果觉得不需要,可以改实现,但目前这层保护值得保留。

### Q6:SequenceAction 的 Duration 字段为啥不生效?
**答:** 故意忽略。Sequence 的语义就是"平铺到外层",它本身不消耗时间轴上的"独立槽位"。如果需要"先 Wait 0.5s 再做 Sequence",应该在外层放两个兄弟条目:`[0] Wait 0.5` `[1] Sequence(...)`。

### Q7:FirePattern 的 BulletPrefab 和 BulletPool.DefaultPrefab 优先级?
**答:** `BulletPool.Get(prefab, ...)` 里:`prefab != null ? prefab : DefaultPrefab`。所以**优先用 Pattern 自带的 prefab**,Pattern 没填时兜底用 Pool 的默认 prefab。建议**所有 Pattern 都显式填自己的 BulletPrefab**,避免共享导致修改时牵连。

### Q8:为什么把"行为流"独立成 BehaviorFlow SO?直接放 ShooterEnemy 里不行吗?
**答:** 不行。三大原因:
1. **复用**:多个敌人 / boss 想用同一段行为流 → 共享一个 .flow 资产即可,不需要复制 Actions 数组。
2. **boss prefab 整洁**:boss 旧设计要挂 N 个 ShooterEnemy 组件;现在每个 ShooterPhase 直接持 BehaviorFlow,**完全不挂 ShooterEnemy**。
3. **资产级版本管理**:.flow 是独立 .asset 文件,可在 Git 里单独 diff,Prefab 不会因为行为调整就变动。

### Q9:为什么 Boss 不继承 ShooterEnemy?
**答:** boss 跟普通敌人语义正交:
- boss 有阶段(多段行为流)、多管血、阶段切换条件
- 普通敌人就一段行为流

如果 Boss:ShooterEnemy,要么把 ShooterEnemy 拖到 boss 上(组件污染),要么抽出一堆抽象。**单独建 BossController + BossPhase + BossSignal 体系更干净**。

### Q10:为什么 BossSignal / PhaseTrigger 抽成"全局信号池"而不是"phase 内嵌 transition"?
**答:** 解耦 + 复用:
- 同一信号可被多 phase 监听(共享 `CurrentBarIndexSignal` 给所有 phase 用)
- phase 之间互不依赖
- 新增 transition = 新建 BossSignal 子类(同 Action / MoveBehaviour 的开放扩展套路)

### Q11:BehaviorFlow.Instantiate 用 Object.Instantiate(SO) 安全吗?
**答:** 安全。Unity 的 `Object.Instantiate(SO)` 会自动深拷贝 `[SerializeReference]` 字段,包括 `EnemyAction[]` 数组里的每个对象。只要所有 Action 字段都用 `[SerializeField]` 或 `[SerializeReference]` 标注,就能完整复制。**唯一边界**:未被序列化的字段(私有字段、属性)不会被复制。我们的所有 Action 字段都是 public + 标注过,**安全**。

---

## 附录:目录速查

```
Assets/Scripts/
├── Singleton.cs                              # MonoBehaviour 单例基类
├── Bullet/
│   ├── Bullet.cs                             # 子弹本体
│   ├── BulletPool.cs                         # 子弹对象池(+ BossShotCounter 钩子)
│   ├── BulletModifier.cs                     # 子弹行为修饰器(基类 + 2 示例)
│   ├── FirePattern.cs                        # 射击模式 SO 基类(+ GetFireCount)
│   └── FirePattern/
│       ├── Ring/RingFirePattern.cs
│       ├── Line/LineFirePattern.cs
│       ├── Arc/ArcFirePattern.cs
│       └── Composite/CompositeFirePattern.cs
└── Enemy/
    ├── ShooterEnemy.cs                       # 行为流播放机(28 行)
    ├── AI/
    │   ├── BehaviorFlow.cs                   # 行为流 SO 资产
    │   ├── BehaviorFlowRuntime.cs            # 行为流运行时驱动器
    │   ├── EnemyAction.cs                    # 行为基类
    │   ├── MoveBehaviour.cs                  # 移动模块基类
    │   ├── MoveBehaviours/
    │   │   └── LinearMove.cs
    │   └── Actions/
    │       ├── FireAction.cs
    │       ├── MoveAction.cs
    │       ├── WaitAction.cs
    │       ├── SelfDestructAction.cs
    │       ├── ParallelAction.cs
    │       └── SequenceAction.cs
    └── Boss/
        ├── BossController.cs                 # Boss 主驱动
        ├── BossHealth.cs                     # 多管血组件
        ├── BossShotCounter.cs                # 全局开火计数(场景单例)
        ├── BossPhase.cs                      # 阶段抽象
        ├── PhaseTrigger.cs                   # 退出条件
        ├── Phases/
        │   └── ShooterPhase.cs               # 行为流阶段(持 BehaviorFlow)
        └── Signals/
            ├── BossSignal.cs                 # 信号抽象
            ├── HpSignal.cs                   # 兼容别名
            ├── CurrentBarPercentSignal.cs
            ├── CurrentBarIndexSignal.cs
            ├── TotalHpPercentSignal.cs
            ├── PhaseTimeSignal.cs
            └── ShotsFiredSignal.cs
```

---

**最后更新:** 引入 BehaviorFlow SO 资产化 + Boss 系统(多阶段 + 多管血)。
**作者注:** 这套架构的核心目的是"**让数据(行为)在 Inspector 里流动起来,而不是塞进代码里**"。扩展前先想清楚"这是数据还是逻辑":数据 → Inspector 字段 / SO 资产;逻辑 → 多态子类。
