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


## 与其他板块的关系

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [bullet](./arch-bullet.md) — FireAction 通过 BulletPool 触发子弹
- [fire-pattern](./arch-fire-pattern.md) — FireAction.FirePattern 字段引用 .asset
- [boss](./arch-boss.md) — Boss 的每个 Phase 也用 ShooterPhase(本质是 BehaviorFlow 的子集)
- [extension-guide](./arch-extension-guide.md) — 新 Action / MoveBehaviour 的统一套路见此板块
