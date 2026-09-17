# Boss 系统

> BossController + 多阶段 + 多管血 + BossSignal

> 本板块对应 ARCHITECTURE § 5. Boss 系统(BossController + 多阶段 + 多管血)(原 ARCHITECTURE.md 第 914–1008 行)。
> 本文档面向项目维护者,不复制实现细节,字段 / 数值 / 默认值以源文件为准。

---

## 5. Boss 系统(BossController + 多阶段 + 多管血)

**职责:** 与普通敌人**正交**的 Boss 编排层,提供多阶段 / 阶段触发条件 / 多管血。普通敌人就一段行为流,Boss 需要这些"上层编排"概念,所以单独建一层。

**协作边界:**
- Boss GameObject 上挂 `Boss + BossHealth + BossHitbox + BossController`,**不挂 ShooterEnemy**,**也不挂 BossShotCounter**。
- `BossShotCounter` 是**场景级单例**(`Singleton<BossShotCounter>`),由场景里单独挂一份。把它从 Boss prefab 摘掉的原因:之前它是 `static Instance + RequireComponent` 双绑,在"同场景多 Boss" / "Boss 多次入场销毁" 场景下会把 Instance 误清成 null;改成 Singleton 后重复挂载自动 Destroy(只留第一份),`BulletPool.FireGroup` 钩子读 Instance 永远稳。
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
                              Destroy(gameObject)
```

**扩展点:**
- 新增 Boss 阶段:新建 `BossPhase` 子类,加到 `BossController.Phases`(详见 `Assets/Scripts/Enemy/Boss/`)。
- 新增阶段退出信号源:新建 `BossSignal` 子类,加 `[Serializable, SRName("Signal/<你的名字>")]`,在 `BossController.Signals` 数组里下拉选(详见下文"内置 Signal")。
- 阶段演出不直接写进 `BossPhase`:正式 Boss 战由 `BossEncounterDefinition` 配置表现映射,监听 `BossController.OnPhaseEntered / OnPhaseExited`。关卡时间轴等待的是 Encounter 完成,而不是仅等待 HP 清零。

### 5.1 内置 Signal(BossSignal 多态信号源)

`BossSignal` 是 `BossController` 每帧 tick 的信号源,产出 `CurrentValue`,供 `PhaseTrigger.IsSatisfied(signal)` 读取。**新增 transition = 新建一个 BossSignal 子类 + 加 `[SRName("Signal/<名字>")]`,自动出现在 `Signals` 数组下拉**。

| 类型名 | SRName | 含义 | 典型触发 |
|---|---|---|---|
| `HpSignal` | `Signal/HP %` | 单管剩余 HP%(0~100,Bars 为空时 100) | `LessOrEqual + 50` = 当前管打掉一半切下阶段 |
| `CurrentBarPercentSignal` | `Signal/Current Bar %` | 当前血管剩余 HP%(0~100,打空自动重置到下一管) | `LessOrEqual + 0` = 当前管打空切下阶段 ⚠️ 见下方"多管血 + 大伤害"坑 |
| `CurrentBarIndexSignal` | `Signal/Current Bar Index` | 当前血管编号(0/1/2/...,Int,打完管单调递增) | `Equal + 1` = 打完第 1 管切下阶段;`GreaterOrEqual + 2` = 进入第 3 管 |
| `TotalHpPercentSignal` | `Signal/Total HP %` | 所有血管累计剩余百分比(0~100,按 MaxHp 加权) | `LessOrEqual + 30` = 残血 30% 切暴走 phase |
| `PhaseTimeSignal` | `Signal/Phase Time` | 当前阶段已持续秒数(每阶段 EnterPhase 时自动 Reset) | `GreaterOrEqual + 30` = 本阶段打了 30 秒强切下阶段 |
| `ShotsFiredSignal` | `Signal/Shots Fired` | Boss 全局累计开火数(BossShotCounter.Total,跨阶段累计) | `GreaterOrEqual + 500` = 开火 500 次后切下阶段 |

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

**解决**:`BossController.OnEnable` 订阅了 `Health.OnBarDepleted`,在 Bar 切管的瞬间同步检查 ExitTrigger。这样无论伤害多大,只要 Bar 切管就会立即切阶段。**正常使用 `CurrentBarPercentSignal + LessOrEqual + 0` 即可正常工作**。

**如果伤害极大**(`dmg > 单管 MaxHp`,一发生命同时击穿多管),`CurrentBarIndexSignal + Equal + N` 比 `CurrentBarPercentSignal + LessOrEqual + 0` 更稳 —— 因为 int 信号不会被"瞬时跳过",下一帧 Update 一定能看到新值。

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


## 与其他板块的关系

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [enemy-ai](./arch-enemy-ai.md) — Phase 体本质是 BehaviorFlow(ShooterPhase);复用 EnemyAction 全部类型
- [hitbox](./arch-hitbox.md) — BossHitbox 继承 HitboxComponent,阵营 = Enemy
- [bullet](./arch-bullet.md) — Boss 可发玩家弹(玩家阵营)打其他敌人(罕见)
- [level](./arch-level.md) — `BossEncounterEntry` 启动遭遇并按需阻塞关卡时间轴;旧 `BossSpawnEntry` 已弃用
