# 敌人 AI

> BehaviorFlow + EnemyAction + MoveBehaviour + ActionDurationConfig

> 本板块对应 ARCHITECTURE § 4. 敌人 AI(BehaviorFlow + EnemyAction)(原 ARCHITECTURE.md 第 823–913 行)。
> 本文档面向项目维护者,不复制实现细节,字段 / 数值 / 默认值以源文件为准。

---

## 4. 敌人 AI(BehaviorFlow + EnemyAction)

**职责:** 用"**时间序列 + 多态 Action + 数据驱动**"描述敌人行为。**所有行为在 Inspector 里用下拉菜单自由组合**,无需改代码。

**职责分工:**
- `BehaviorFlow`(SO 资产) —— 持有 `Actions: EnemyAction[]`,每条 Action 有 `Duration` + 三段式生命周期。
- `EnemyAction`(`[Serializable] class` + `[SerializeReference]`) —— 原子 / 容器行为单元:Fire / Move / Wait / SelfDestruct / Parallel / Sequence / ...。
- `MoveBehaviour`(`[Serializable] class` + `[SerializeReference]`) —— Move 内部再委托一层:匀速 / 线性 / 贝塞尔 / 圆形 / 追踪 / 区域内随机直线 / ...
- `ShooterEnemy`(MonoBehaviour) —— 极薄的总控组件,持 `Flow` 引用 + Update 驱动。

**协作边界:**
- `ShooterEnemy.Flow` 拖一个 `.flow` 资产即可,无需挂其他组件。
- Boss 走自己的 `BossController + ShooterPhase`,**不挂 ShooterEnemy**(避免组件污染,详见 §5)。
- `BehaviorFlow.Instantiate()` 用 `Object.Instantiate(SO)` 深拷贝 Actions 数组 —— **多个敌人引用同一资产互不干扰**。

**扩展点:**
- 新增敌人行为(动画 / 隐身 / 加血):新建 `EnemyAction` 子类,加 `[Serializable, SRName("Action/<名字>")]`。
- 新增移动方式(贝塞尔 / 圆形 / 追踪 / 区域内随机直线):新建 `MoveBehaviour` 子类,同套路。
- 详见 `Assets/Scripts/Enemy/AI/`。
- 新增"行为流"(符卡 / 小怪模式):右键 → Create → STG → Behavior Flow,创建 SO 资产并配置 Actions。

详见 `Assets/Scripts/Enemy/`。

### 4.0.5 ActionDurationConfig 多态 Duration 策略(vX 起)

**位置:** `Assets/Scripts/Enemy/AI/ActionDurationConfig.cs`

**职责:** 把「一条 Action 持续多久」从基类的写死 `float Duration` 字段,抽成 SR 多态下拉字段(对照项目既有的 `BaseOffsetStrategy` / `ModifierStartTrigger` 套路),支持精确值 / 区间随机两种策略,策划可在 Inspector 里直接切,无需改代码。

**字段位置:** `EnemyAction.DurationConfig`(基类持有,所有 Action 子类自动获得能力,无需自己写)

**内置子类:**

| 类 | SRName | 用途 |
|---|---|---|
| `FixedActionDuration` | `Duration/Fixed` | 精确值(默认,等价旧 `float Duration`)。字段 `Value`(秒,默认 1f) |
| `RandomRangeActionDuration` | `Duration/Random Range` | 区间随机,每次进入行为时抽一次,本条 Action 期间固定。字段 `Min` / `Max`(秒) |

**抽样时机(由 `BehaviorFlowRuntime.AdvanceTo` 触发):**

- 每次切到一条 Action 时,调一次 `DurationConfig.Sample()`,结果写入 `action.CurrentDuration`(per-instance 缓存)
- 本条 Action 期间 `CurrentDuration` 固定不变,`Tick` 用它跟 `_elapsedInCurrent` 比
- Sequence / Loop 切到下一条 → 重新抽样(若新策略是区间随机 → 节奏抖动)
- Parallel 内每个子 Action 独立抽样、互不影响(子 Action 各自记自己的 CurrentDuration)

**典型用法:**

- Boss 节奏混乱:`FireAction` DurationConfig = Random Range(2, 4) → 每波开火时长随机
- 玩家反应窗口抖动:`WaitAction` DurationConfig = Random Range(0.5, 1.5) → 间隔不固定
- 弹性巡逻:`MoveAction` DurationConfig = Random Range(3, 5) → 每段巡逻时长不固定
- 伪随机弹幕间隔:`FireAction` DurationConfig = Random Range(0.8, 1.2) → 节奏自然
- 精确节拍:`FireAction` DurationConfig = Fixed(2) → 与旧 float 完全等价

**老 .asset 兼容:**

- 旧 `EnemyAction.Duration: X`(float)通过 `[FormerlySerializedAs("Duration")]` 迁移到隐藏字段 `_legacyDuration`
- Runtime: `DurationConfig != null ? DurationConfig.Sample() : Mathf.Max(0f, _legacyDuration)`,SR 字段为 null 时自动用老值
- 用户视角:Inspector 里看不到 float Duration 字段了,只有 SR 多态下拉;老 .asset 行为 100% 等价

**扩展指南:**

新 Duration 策略 = 新建 `ActionDurationConfig` 子类 + `[Serializable, SRName("Duration/<名字>")]` + override `Sample()`,参考 `RandomRangeActionDuration` 实现(内置容错:Min==Max / Min>Max / 负数钳位)。新策略自动出现在所有 `EnemyAction` 子类的 `DurationConfig` 下拉里(适用于 FireAction / MoveAction / WaitAction / ParallelAction / SequenceAction / EmitSignalAction 等所有内置 Action,以及未来新加的 Action)。

---

### 4.0.6 SequenceAction.Loop(自身 Duration 封顶 + 内部循环)

**位置:** `Assets/Scripts/Enemy/AI/Actions/SequenceAction.cs`

**职责:** 让 `SequenceAction` 支持"**children 跑完后从头再跑一轮,直到 Sequence 自身 Duration 耗尽**"的语义,等价于把 `BehaviorFlow` 顶层 `Loop` 能力下沉到 Sequence 容器层级。典型场景:Boss 阶段内"开火 2 秒 → 停顿 1 秒 → 开火 2 秒 → 停顿 1 秒" 这样的短循环节奏,不必再外置一个完整 Flow。

**字段:**

| 字段 | 类型 | 默认 | 含义 |
|---|---|---|---|
| `Loop` | `bool` | `false`(等价旧行为) | children 跑完后是否从头再跑一轮 |
| `Children` | `EnemyAction[]` | — | 顺序执行的子行为(SR 字段) |
| `DurationConfig` | `ActionDurationConfig` | `FixedActionDuration{Value=1f}` | Sequence 自身的 Duration,**仅 Loop=true 时生效**,语义与 `ParallelAction` 自身 Duration 一致(封顶时间) |

**两段式 Duration 语义:**

- **`Loop = false`(默认,等价旧行为)**:Sequence 自身 Duration 字段被忽略,沿用外层 `BehaviorFlowRuntime` 时间轴,children 跑完后 Sequence 空转等外层切走。这是 vX 之前的旧行为,**老 .asset 自动以 `Loop=false` 反序列化,无回归**。
- **`Loop = true`**:Sequence 自身 Duration 字段变为**封顶时间**(与 `ParallelAction` 自身 Duration 同套路)。
  - 进入 Sequence 时,拷贝 `CurrentDuration` 到 `_remainingSelfDuration`
  - 每 Tick 递减 `_remainingSelfDuration -= dt`
  - children 跑完一轮后(`_idx == -1`):若 `_remainingSelfDuration > 0` → 从 0 再跑一轮,直到封顶时间到点
  - Sequence 自身 Duration 由外层 `BehaviorFlowRuntime.AdvanceTo` 抽样一次,Random Range 策略下整段 Loop 期间只抽一次(与顶层 `BehaviorFlow.Loop` 一致)

**Loop 触发的 child 重新进入:**

每次 `Advance` 切到 child(无论是首次还是 Loop 重新进入 child 0),都重新 `child.ResolveDuration()` 抽样:
- Fixed → 等价旧行为
- Random Range → 每次循环节奏抖动(每个 child 独立抽)

**嵌套语义:**

- **Sequence 套 Sequence(外 Loop,内不 Loop)**:外层 Loop 触发内层 Sequence 重新 `OnEnter`(经 `Advance` 调用 `child.OnEnter`),内层按自己的 `_idx = -1` 逻辑从头跑,符合预期。
- **Sequence 套 Parallel**:Sequence 在 Advance 时调 `child.OnEnter` → `ParallelAction.OnEnter` 已经重建 `_childElapsed` / `_childFinished`,不会脏。

**OnExit 收尾:**

Sequence override 了 `OnExit`(基类原本是空实现):
- `Loop=true` 且当前有 child 在跑 → 调 `Children[_idx].OnExit(enemy)` 收尾,避免外层 `ForceExit`(ShooterPhase 切走)时漏清理
- `Loop=false` → 不 override 行为,children 自身在 OnTick 内已逐个 OnExit,与旧行为等价

**典型用法:**

- Boss 短循环节奏:`SequenceAction` Loop=true,自身 Duration=5s,Children=[Fire(2s), Wait(1s)] → 5 秒内反复"开火 2 秒 + 停顿 1 秒"两次
- 自机狙走位:`SequenceAction` Loop=true,自身 Duration=10s,Children=[MoveToA(3s), MoveToB(3s), Wait(1s)] → 10 秒内反复绕场
- 旧用法不变:`SequenceAction` Loop=false,Children=[A, B, C] → 顺序跑完 A→B→C 后空转等外层切走(等价旧 v1 行为)

**老 .asset 兼容:**

- `Loop` 是新增字段,默认 `false` → 老 .asset 反序列化后 `Loop=false` → 走现状分支,**行为 100% 等价**。
- 不需要 `[FormerlySerializedAs]`(字段是新加,不是改名)。

---

#### 4.1 内置 MoveBehaviour 速查表

| 类型 | SRName | 范式 | 关键字段 |
|---|---|---|---|
| `LinearMove` | `Move/Linear` | 固定方向匀速直线 | `Direction`(枚举:Down/Up/Left/Right/ToPlayer/Custom) + `CustomAngleDeg` + `Speed` |
| `AccelerateMove` | `Move/Accelerate` | 匀加速直线(`StartSpeed→MaxSpeed`) | `BaseAngle` + `StartSpeed` + `Acceleration` + `MaxSpeed` |
| `EaseMove` | `Move/Ease` | 沿方向缓动 Duration 秒(时间维度) | `BaseAngle` + `Duration` + `PeakSpeed` + `Mode`(复用 `EaseMode`)+ `MinSpeedFactor` |
| `SineMove` | `Move/Sine` | 匀速推进 + 垂直方向 sin 摆动 | `BaseAngle` + `Speed` + `Amplitude` + `Frequency` + `PhaseOffsetDeg` |
| `PatrolMove` | `Move/Patrol` | 两点之间 PingPong / Once + Ease | `EndPosition` + `Speed` + `LoopMode` + `Mode` + `ArrivedThreshold` + `MinSpeedFactor` |
| `CircularMove` | `Move/Circular` | 圆周运动(绝对锚定) | `Radius` + `AngularSpeed` |
| `BezierMove` | `Move/Bezier` | 二次贝塞尔曲线(绝对锚定) | `StartMode` + `ControlPoint1/2` + `IsRelative/IsAbsolute` + `Duration` |
| `HomingMove` | `Move/Homing` | 朝玩家持续转向 + 匀速前进 | `Speed` + `TurnRate` + `LockOnDelay` + `MaxHomingTime` |
| `RandomWalkInRegionMove` | `Move/Random Walk In Region` | 矩形区域内随机直线 + 停顿循环 | `RegionCenter` + `RegionSize` + `MaxStepDistance` + `DirectionCenterDeg` + `DirectionSpreadDeg` + `IntervalMin/Max` + `Mode`(Constant/FastToSlow/SlowToFast) + `PeakSpeed` + `ArrivedThreshold` + `RandomSeedOffset` |

**§4.1 辅助枚举 / Helper**(挂在 `RandomWalkInRegionMove` 上,作为"区域型移动"的参考实现):

| 名称 | 用途 |
|---|---|
| `SpeedCurve`(类内 enum) | 三档速度曲线:`Constant`(匀速,因子 1) / `FastToSlow`(`cos(t·π/2)`,单调递减,撞墙感) / `SlowToFast`(`sin(t·π/2)`,单调递增,蓄力感) |
| `Phase`(类内 enum) | 子状态机:`Moving`(在走)/ `Idle`(停顿中) |
| `MaxDistanceInsideBox(start, dir, boxMin, boxMax)`(static helper) | 算"从 start 沿 dir 走到 box 边界前能走多远",用于把 `MaxStepDistance` 裁剪到区域内。圆形区域 / 多边形区域只要重写这个 helper 即可扩展 |
| `CurveFactor(mode, t01)`(static helper) | 把 `t01` 映射到 `[0,1]` 速度因子,三条曲线都光滑且始终 ≥ 0,无需 `MinSpeedFactor` 兜底 |
| `AverageCurveFactor(mode)`(static helper) | `t01 ∈ [0,1]` 区间上的速度均值,用于反推 `moveDuration`,让 `t01` 在 `Constant/FastToSlow/SlowToFast` 下都按"真实耗时"归一化 |

---

#### 4.2 内置 EnemyAction 速查表

| 类型 | SRName | 范式 | 关键字段 |
|---|---|---|---|
| `FireAction` | `Action/Fire` | 持续按 FireRate 触发 `BulletPool.FireGroup(Pattern, ...)`(OneShot=true 时只触发一次) | `Pattern`(FirePattern 资产) + `FireRate` + `OneShot` + `AimOffsetDeg` + `ExtraModifierPrefabs` |
| `FireLaserAction` | `Action/Fire Laser` | 持续按 FireRate 触发 `LaserPool.FireGroup(Pattern, ...)`,字段镜像 FireAction(OneShot=true 时只触发一次) | `Pattern`(LaserPattern 资产) + `FireRate` + `OneShot` + `AimOffsetDeg` + `ExtraModifierPrefabs` |
| `MoveAction` | `Action/Move` | 用 `MoveBehaviour` 多态驱动位移 | `Move`(SR 多态) + 视 MoveBehaviour 类型的内部字段 |
| `WaitAction` | `Action/Wait` | 什么都不做,只占用 Duration | (无) |
| `EmitSignalAction` | `Action/Emit Signal` | 在时间轴上向 `BulletSignalBus` 发全局信号(Boss 喊话 / 阶段切换 / modifier 解锁) | `SignalName` + `EmitMode`(OnEnterOnly / EveryTick / OnInterval) + `Interval` |
| `SelfDestructAction` | `Action/Self Destruct` | 到达时销毁自身(常作 Sequence 收尾) | (无) |
| `ParallelAction` | `Action/Parallel` | 并行容器,所有 children 同时跑 | `Children` + `StartTrigger`(SR 多态) + 基础 DurationConfig |
| `SequenceAction` | `Action/Sequence` | 顺序容器,逐个跑完 children,可选 `Loop=true` 在自身 Duration 内循环 | `Children` + `Loop` + 基础 DurationConfig |

**§4.2 关键模式:OneShot 字段(FireAction / FireLaserAction vX 起)**

- 字段定义:`public bool OneShot = false`,默认 `false` → 与历史 100% 等价,不需要迁移逻辑。
- `OneShot=false`(默认):按 `FireRate` 节流,在每段 `1 / FireRate` 秒开火一次,持续 DurationConfig 抽样时长后进入下一条。
- `OneShot=true`:**忽略 `FireRate`**,仅在 `OnEnter` 触发时调用一次 `FireGroup`,**剩余时间由 DurationConfig 占用**(`BehaviorFlowRuntime` 照常计时,OnTick 期间不再触发任何 FireGroup)。
- **典型用法**:
  - Boss 一次性放 24 颗散弹 + 喘气 2 秒:`OneShot=true` + `DurationConfig=Fixed(2)` → 行为进入时开火一次,然后空跑 2 秒给玩家喘息
  - 蓄力后放一道激光 + 停顿:`FireLaserAction` OneShot=true + Duration=2
- **与 Loop 协作**:`Loop=true` 的 BehaviorFlow 每次循环回到本 Action 会再次触发 `OnEnter`,因此 OneShot=true 也会**每次循环再开一次火**(等价于"每波散弹 + 停顿"的节奏,正符合 STG Boss 战常见模式)。
- **与 DurationConfig 协作**:OneShot 模式下 Duration 仍由 `ActionDurationConfig` 多态策略控制(Fixed 精确值 / Random Range 区间随机),与节流模式完全一致。
- **实现位置**:`Assets/Scripts/Enemy/AI/Actions/FireAction.cs` 与 `FireLaserAction.cs`,开火逻辑抽取为 `FireOnce(enemy)` 私有方法,被 `OnEnter`(OneShot 路径)与 `OnTick`(节流路径)共用,保证两条路径在 Pattern / AimOffset / ExtraModifier / 阵营透传上行为 100% 一致。

---


## 与其他板块的关系

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [bullet](./arch-bullet.md) — FireAction 通过 BulletPool 触发子弹
- [fire-pattern](./arch-fire-pattern.md) — FireAction.FirePattern 字段引用 .asset
- [boss](./arch-boss.md) — Boss 的每个 Phase 也用 ShooterPhase(本质是 BehaviorFlow 的子集)
- [extension-guide](./arch-extension-guide.md) — 新 Action / MoveBehaviour 的统一套路见此板块
