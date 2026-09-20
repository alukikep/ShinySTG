# Boss 系统

BossEncounterDefinition 负责一场 Boss 战的配置，BossController 负责阶段执行，
BossHealth 负责血量状态。配置步骤见 [Boss 操作说明](../../Assets/Scripts/Enemy/Boss/README.md)。

## 配置与实例边界

Encounter SO 持有实体 prefab、血管、Signals、阶段列表、循环选项和整场动作。
每个 BossPhase 同时包含行为、退出条件、过渡保护、指令、名称、音效和进退场动作。
阶段重排时这些内容一起移动，不再通过另一份演出列表的数字索引关联。
血管与行为阶段保持独立，多个阶段可以引用同一血管；血管引用使用稳定 ID。

实体挂载 Boss、BossHealth、BossHitbox 和 BossController。prefab 保留外观、碰撞及
组件相关配置，血管、阶段和 Signals 在 Encounter Inspector 编辑。
ShooterPhase 引用 BehaviorFlow，复用普通敌人的行为系统。

每场遭遇用 Unity 序列化克隆生成临时 Definition，隔离嵌套 managed reference。
行为流、音效等 Unity 资产按引用共享，行为流自行创建执行实例。当前血量不序列化，
初始化时从血量上限恢复；计时器和阶段执行状态不得写回源 SO。Dispose 释放临时 Definition。
Boss 使用创建/销毁生命周期，不支持对执行过的实体重新初始化或作为对象池对象复用。

Encounter 要求至少一个非空阶段和一管有效血量，血管 ID 唯一且非空，上限为有限正数。
不提供旧 prefab 战斗配置或旧阶段演出表的迁移、回退；测试配置需在 SO 中重建。
底层 Health 保留的单管 API 不构成 Encounter 的配置回退路径。

## 启动与宿主

BossEncounterEntry 校验配置、生成实体、创建遭遇过程并登记到关卡，再通知 Boss 出场。
遭遇注入独立的血量、阶段与信号，绑定动作及死亡回调，然后显式启动 Controller。
Controller 在完成初始化前不自动推进阶段，避免依赖不同组件的 Start 顺序。

直接放入场景时，在 Boss 组件指定 Encounter；BossEncounterHost 在同一场景独立推进
遭遇，实体销毁后仍可运行收尾。该入口不提供 LevelRuntime，依赖关卡上下文的动作
应通过 BossEncounterEntry 使用。关卡和场景宿主均在结束时 Dispose 遭遇。

关卡的 BlockTimeline 决定是否等待整场遭遇；动作的 WaitForCompletion 决定是否等待
该组动作。阻塞关卡时间轴不会暂停遭遇 Tick，也不会暂停其他单位或自动提供无敌。

## 阶段与退出条件

新建 Inspector 阶段使用 Conditions，可组合指定血管耗尽、血管剩余比例、总血量、
阶段时间及高级 Signal 条件。Any / All 决定组合方式；空列表不自动退出。
指定血管条件读取稳定状态，耗尽后切到下一管也仍成立，不依赖 TriggerOnEmpty。
阶段时间从正式进入开始，过渡等待不计时，循环重入会重置。

LegacyTriggers 是独立的条件模式，不与 Conditions 混合。其 SignalIndex 在
Encounter.Signals 中配置，运行时读取 Controller 持有的本场 Signal 实例。
旧模式通过 OnBarDepleted 捕获瞬时零血并排队，下次 Update 在伤害结算后最多推进一次，
最终死亡优先于进入下一阶段。大伤害可能跨过多个编号，离散索引条件需考虑越过阈值。
新配置检测耗尽优先使用稳定 ID 的 BarDepleted 条件。

DiscardOverflow 只丢弃打空该管的本次剩余伤害；阶段过渡保护则能阻止同帧后续攻击，
保持到下一阶段入场完成。保护不代替退出条件，All 条件不要依赖保护期间无法产生的伤害。
停止、死亡、阶段耗尽和等待动作失败/取消都会释放 Controller 持有的保护。
禁用 Controller 时释放保护，重新启用继续原阶段时恢复；这不表示重新初始化 Boss。

## 阶段动作与死亡收尾

阶段动作保存在 BossPhase，Encounter 负责执行，并通过 Controller 的等待接口协调：

- 正常入场先执行 EnterActions，再执行 EnterCommands 和阶段行为，最后发送进入通知。
- 正常退出先发送退出通知并停止行为，执行 ExitCommands，再执行 ExitActions。
- 死亡仍退出当前阶段并执行 ExitCommands；不启动阶段 ExitActions，但保留退出音效。
  取消未完成的开场与阶段动作后，转入整场 DefeatActions。

等待式开场或阶段动作失败/取消时，Controller 停止，Encounter 随后清理并结束。
击破和完成动作的失败句柄按已结束处理，继续收尾；非等待动作失败不会阻塞推进。
非等待退出动作可以与下一阶段入场并行，下次阶段退出会清理此前仍运行的动作。

BossHealth 全部血量清空触发死亡，Boss 和 Encounter 共用幂等 Stop 完成阶段收尾。
Stop 仅在 Health 确认死亡时发送击败事件；清场取消活着的 Boss 不发送击败通知。
Boss 击败通知与整场完成是不同事件：等待式 DefeatActions 可保留实体，完成后销毁；
DefeatOutroDelay 从死亡开始计时，与击破动作并行等待，两者满足后启动 CompleteActions。
CompleteActions 必须允许实体已销毁。遭遇完成或取消时清理剩余动作并释放临时配置。

## 血量 UI 与奖励

BossHealth 提供当前管比例和有效血管计数，死亡后读取当前管返回零。
OnHealthChanged 在一次伤害完整结算及死亡通知后发送，不受 TriggerOnEmpty 控制。
数据层剩余管数包含当前管，HUD 只显示后续管数，不以行为阶段数推算血管数。
绑定与显隐见 [HUD 架构](./arch-hud.md)。

阶段奖励在 ExitCommands 配置 SpawnDropsCommand。Controller 提供正常阶段结束、
Boss 死亡或手动停止的调用原因；阶段奖励按配置允许前两者，手动停止不产生奖励。
每次实际退出只结算一次，未进入或跳过的阶段不补发，循环阶段每次退出重新结算。
最终奖励可配置在保持到死亡的最后阶段，不要在退出指令和演出中重复配置同一奖励。

## 扩展与协作

新增 BossPhase 或 BossSignal 子类时沿用 Serializable / SRName 约定，在 Encounter 的
阶段或 Signals 列表配置。扩展类型的运行状态必须按每场实例隔离。
OnPhaseEntered / OnPhaseExited 用于通知；需要阻塞的演出使用阶段 ActionSequence。

- [敌人行为](./arch-enemy-ai.md)：ShooterPhase 复用 BehaviorFlow。
- [通用动作](./arch-game-actions.md)：等待、取消与指令执行契约。
- [关卡](./arch-level.md)：遭遇生成、推进与时间轴阻塞。
- [对话](./arch-dialogue.md)：战前用 StartActions，保留实体的战后用 DefeatActions。
- [道具](./arch-items.md)：阶段奖励生成及回收。
