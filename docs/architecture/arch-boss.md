# Boss 系统

> BossController + 多阶段 + 多管血 + BossSignal

> 本板块对应 ARCHITECTURE § 5. Boss 系统(BossController + 多阶段 + 多管血)(原 ARCHITECTURE.md 第 914–1008 行)。
> 本文档面向项目维护者,不复制实现细节,字段 / 数值 / 默认值以源文件为准。

---

## 5. Boss 系统(BossController + 多阶段 + 多管血)

**职责:** 与普通敌人**正交**的 Boss 编排层,提供多阶段 / 阶段触发条件 / 多管血。普通敌人就一段行为流,Boss 需要这些"上层编排"概念,所以单独建一层。

**协作边界:**
- Boss GameObject 上挂 `Boss + BossHealth + BossHitbox + BossController`,不挂 ShooterEnemy。
- Boss 发射次数统计当前未启用；阶段信号使用 HP、血管和阶段时间等明确来源。
- 阶段用 `ShooterPhase` 直接持 BehaviorFlow 资产,boss 行为复用普通敌人那套行为流。
- 多管血 / 多阶段 / 信号切换都在 Inspector 配,无需新代码。

**死亡收尾流程(单一路径):**
```
玩家弹 → CollisionService → BossHealth.TakeDamage(dmg)
                              ↓ 切管 → 触发 OnBarDepleted(int)
                              ↓ 全部清空 → 触发 OnDeath(防重入 _deathFired)
                                    ↓
                              Boss 总控.HandleDeath(_dead 防重入)
                                    ↓
                              BossController.Stop(_stopped 防重入)
                                    ├─ 当前 phase.OnExit
                                    ├─ LevelController.NotifyBossDefeated(_defeated 防重入)
                                    └─ _current = null
                                    ↓
                              无 Encounter：Destroy(gameObject)
                              有 Encounter：等待击破动作（按配置）后销毁
```

**扩展点:**
- 新增 Boss 阶段:新建 `BossPhase` 子类,加到 `BossController.Phases`(详见 `Assets/Scripts/Enemy/Boss/`)。
- 新增阶段退出信号源:新建 `BossSignal` 子类,加 `[Serializable, SRName("Signal/<你的名字>")]`,在 `BossController.Signals` 数组里下拉选(详见下文"内置 Signal")。
- 阶段演出由 `BossEncounterDefinition` 配置，Encounter 通过 `BossController.PhaseActions` 提供可等待句柄，通过 StartGate 延迟首阶段。OnPhaseEntered / OnPhaseExited 保留为通知事件；关卡时间轴等待 Encounter 完成，而非仅等待 HP 清零。
- 每个 `BossPhase` 可配置 `EnterCommands / ExitCommands`。进入指令在阶段行为流启动前执行;退出指令在阶段 `OnExit` 后执行,正常切阶段与 Boss 死亡 `Stop()` 共用同一路径。适合配置全屏消弹和阶段过渡无敌。

### 5.1 内置 Signal(BossSignal 多态信号源)

`BossSignal` 是 `BossController` 每帧 tick 的信号源,产出 `CurrentValue`,供 `PhaseTrigger.IsSatisfied(signal)` 读取。**新增 transition = 新建一个 BossSignal 子类 + 加 `[SRName("Signal/<名字>")]`,自动出现在 `Signals` 数组下拉**。

| 类型名 | SRName | 含义 | 典型触发 |
|---|---|---|---|
| `HpSignal` | `Signal/HP %` | 单管剩余 HP%(0~100,Bars 为空时 100) | `LessOrEqual + 50` = 当前管打掉一半切下阶段 |
| `CurrentBarPercentSignal` | `Signal/Current Bar %` | 当前血管剩余 HP%(0~100,打空自动重置到下一管) | `LessOrEqual + 0` = 当前管打空切下阶段 ⚠️ 见下方"多管血 + 大伤害"坑 |
| `CurrentBarIndexSignal` | `Signal/Current Bar Index` | 当前血管编号(0/1/2/...,Int,打完管单调递增) | `Equal + 1` = 打完第 1 管切下阶段;`GreaterOrEqual + 2` = 进入第 3 管 |
| `TotalHpPercentSignal` | `Signal/Total HP %` | 所有血管累计剩余百分比(0~100,按 MaxHp 加权) | `LessOrEqual + 30` = 残血 30% 切暴走 phase |
| `PhaseTimeSignal` | `Signal/Phase Time` | 当前阶段已持续秒数(每阶段 EnterPhase 时自动 Reset) | `GreaterOrEqual + 30` = 本阶段打了 30 秒强切下阶段 |

**配置模式:阶段退出触发 = (SignalIndex, Op, Threshold) 三元组**

`PhaseTrigger` 不再直接持有 `BossSignal` 实例(避免 SerializeReference 独立实例导致 `_health` 没绑),改成持有 `int SignalIndex`,引用 `BossController.Signals` 数组里的下标。Inspector 里给每个 PhaseTrigger 配 SignalIndex 时,下拉/数字框列出可用 Signal。

```text
Phase 1 (开场符卡)
  ExitTriggers:
    [0] SignalIndex = 0  (默认指向 Signals[0] = CurrentBarPercentSignal)
        Op = LessOrEqual
        Threshold = 0
        // 当前管打空就切 → Phase 2

Phase 2 (中期弹幕)
  ExitTriggers:
    [0] SignalIndex = 0  (指向 Signals[0] = TotalHpPercentSignal,见 Signals 数组配置)
        Op = LessOrEqual
        Threshold = 30
        // 残血 30% → Phase 3(暴走)

Phase 3 (暴走)
  (没有 ExitTrigger,玩家继续打到 BossHealth 全清 → BossHealth.OnDeath 触发总控收尾)
```

**算子选择要点:**
- `LessOrEqual` 是绝大多数 HP 触发的默认。
- `GreaterOrEqual` 是 PhaseTime / ShotsFired 累加型信号的默认(打够 N 秒 / N 发)。
- `Equal` 仅推荐用于 `CurrentBarIndexSignal`(离散 Int);不推荐用于累加型浮点(详见 `PhaseTrigger.cs` 注释)。

**⚠️ 多管血 + 大伤害的"击穿"坑**

`TakeDamage` 在 `LateUpdate` 同步上下文里走完整个扣血循环,可能**一帧内**把多管打空(溢出伤害),而 `BossController.Update()` 已经跑过了。下一帧 `Update` 检测时 `CurrentBarPercent` 已经是新 Bar 的满血值,**永远检测不到 `= 0` 的瞬间**。

**处理方式**：OnBarDepleted 在血管清空的瞬间检查 ExitTrigger，记录切换请求；下一次 Update 在伤害结算结束后推进切换。这样可以捕获瞬时零血，并让最终死亡优先于进入下一阶段。同一帧多次满足条件会合并为一次请求，不会逐管回放多个阶段。

溢出伤害仍可能打穿多管。需要表达“已经进入或越过第 N 管”时使用 CurrentBarIndexSignal + GreaterOrEqual；Equal 可能被跳过。阶段动作提供无敌并不撤销此前已经结算的伤害。

### 5.2 多管血(BossHealth.Bars)

每管血有独立的 `MaxHp` + `Name` + `TriggerOnEmpty` 字段。`TakeDamage` 自动处理扣穿(溢出伤害继续扣下一管),每管打空触发 `OnBarDepleted(int)` 事件,全部清空触发 `OnDeath`。

供 Signal 读的属性:

| 属性 | 含义 |
|---|---|
| `HpPercent` | 单管剩余百分比(兼容旧 HpSignal) |
| `CurrentBarPercent` | 当前管剩余百分比 |
| `CurrentBarIndex` | 当前管编号(打空后自增) |
| `TotalHpPercent` | 所有管加权累计百分比(残血/暴走触发用) |
| `IsDead` | 所有管清空(Legacy 模式 LegacyCurrentHp 归零) |

---


## 阶段掉落

在 BossPhase.ExitCommands 配置 SpawnDropsCommand，复用阶段退出的单一路径。
Controller 通过 GlobalCommandContext.Invocation 区分正常阶段结束、Boss 死亡和手动停止；
撒道具指令可分别允许前两种原因，手动停止不产生阶段奖励。奖励在阶段 OnExit 后、Encounter 退出动作之前生成。

每次实际退出只执行一次；过渡期间死亡不会重复结算已退出阶段。未进入或被跳过的阶段不补发奖励，
没有当前阶段时死亡也没有阶段奖励。需要最终击破奖励时，让最终阶段保持到死亡并配置退出指令。
循环阶段每次实际退出均重新结算。不要同时在阶段指令与 Encounter 演出里配置同一份奖励。

## 对话接入

战前对话使用等待式 StartActions；战后需保留 Boss 时使用 DefeatActions，仅立绘时可使用 CompleteActions。PlayDialogueAction 不改变 Boss 死亡通知的时机；无敌和消弹仍需显式配置。

## 与其他板块的关系

- [items](./arch-items.md) — 阶段退出提供触发原因，道具系统独立生成和回收。

- [dialogue](./arch-dialogue.md) — 对话播放及与战斗的协作边界。

- [game-actions](./arch-game-actions.md) — 通用动作的运行、取消与扩展契约。

阶段顺序为：旧阶段通知及 OnExit/ExitCommands → Encounter ExitActions → Encounter EnterActions → 新阶段 EnterCommands/OnEnter → OnPhaseEntered。
只有配置等待时才延迟后续步骤；等待期间不推进阶段行为与信号计时，但不会自动无敌。
死亡取消尚未完成的开场和阶段附加动作，运行 DefeatActions；死亡不启动 Encounter ExitActions，旧 ExitSfx 仍保留。
BossPhase 自身的 ExitCommands 仍在 Stop 时执行。需要尸体或 Transform 的动画放入等待式 DefeatActions，
CompleteActions 必须允许 Boss 已销毁。无 Encounter 时保持直接销毁路径。

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [enemy-ai](./arch-enemy-ai.md) — Phase 体本质是 BehaviorFlow(ShooterPhase);复用 EnemyAction 全部类型
- [hitbox](./arch-hitbox.md) — BossHitbox 继承 HitboxComponent,阵营 = Enemy
- [bullet](./arch-bullet.md) — Boss 可发玩家弹(玩家阵营)打其他敌人(罕见)
- [level](./arch-level.md) — `BossEncounterEntry` 启动遭遇并按需阻塞关卡时间轴;旧 `BossSpawnEntry` 已弃用
