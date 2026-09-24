# Boss 系统

BossEncounterDefinition 负责一场 Boss 战的配置，BossController 负责阶段执行，
BossHealth 负责血量状态。配置步骤见 [Boss 操作说明](../../Assets/Scripts/Enemy/Boss/README.md)。

## 配置与实例边界

Encounter SO 持有实体 prefab、开始血管、血管节点、Signals 和整场动作。
每个血管拥有显式 NextBar，可配置有序分段，各段再持有行为状态；无分段时沿用整管状态列表。引用使用稳定 ID，不依赖资产数组顺序。
每个 BossPhase 保存行为、状态切换条件、指令、名称、音效和进退场动作，重排时一起移动。
运行时按开始血管和 NextBar 构建路径，再展平路径内状态供执行与动作回调使用。
不可达血管不执行、不计入 HUD；禁止血管循环，管内状态也不循环。

实体挂载 Boss、BossHealth、BossHitbox 和 BossController。prefab 保留外观、碰撞及
组件相关配置，血管、阶段和 Signals 在 Encounter Inspector 编辑。
ShooterPhase 引用 BehaviorFlow，复用普通敌人的行为系统。

每场遭遇用 Unity 序列化克隆生成临时 Definition，隔离嵌套 managed reference。
行为流、音效等 Unity 资产按引用共享，行为流自行创建执行实例。当前血量不序列化，
初始化时从血量上限恢复；计时器和阶段执行状态不得写回源 SO。Dispose 释放临时 Definition。
Boss 使用创建/销毁生命周期，不支持对执行过的实体重新初始化或作为对象池对象复用。

Encounter 要求有效开始血管、唯一非空血管 ID、有限正数血量及每管至少一个非空状态。
启用时限的血管必须设置有限正数秒数；关闭时限的血管不校验保存的秒数。
旧顶层 Phases/Loop 保留供对照，不再驱动遭遇，也不自动推断旧阶段所属血管。
底层 Health 单管 API 和旧条件/保护字段仅保留兼容用途，不构成 Encounter 配置回退路径。

## 启动与宿主

BossEncounterEntry 校验配置、生成实体、创建遭遇过程并登记到关卡，再通知 Boss 出场。
遭遇注入独立的血量、阶段与信号，绑定动作及死亡回调，然后显式启动 Controller。
Controller 在完成初始化前不自动推进阶段，避免依赖不同组件的 Start 顺序。

直接放入场景时，在 Boss 组件指定 Encounter；BossEncounterHost 在同一场景独立推进
遭遇，实体销毁后仍可运行收尾。该入口不提供 LevelRuntime，依赖关卡上下文的动作
应通过 BossEncounterEntry 使用。关卡和场景宿主均在结束时 Dispose 遭遇。

关卡的 BlockTimeline 决定是否等待整场遭遇；动作的 WaitForCompletion 决定是否等待
该组动作。阻塞关卡时间轴不会暂停遭遇 Tick，也不会暂停其他单位或自动提供无敌。

## 整管模式推进

管内状态按列表顺序执行。非末尾状态选择本状态持续时间或本管剩余血量百分比作为切换条件；
最后状态保持到血管结束。切状态不回血，状态计时在正式进入时重置。

打空换管始终启用，优先于管内状态切换。每管可独立开启或关闭时限，默认开启：
非符可关闭，此时只在打空后换管且不显示倒计时；符卡可开启，超时同样结束本管。
关闭整管时限不影响管内按时间切状态。血管计时跨状态累计，开场和过渡等待期间暂停。

Health 扣血后保留空血管并通知 Controller；Controller 统一完成状态收尾和血管推进。
本次击破溢出伤害丢弃，同帧后续伤害不能穿管。开场、状态切换和换管自动保护血量，
直到下一状态入场完成；无需另选保护血管或配置血管耗尽退出条件。
停止、死亡和等待动作失败/取消会释放 Controller 持有的保护。
禁用时释放、重新启用时恢复所需保护，不表示重新初始化或复活。

NextBar 为空时，本管结束即进入最终死亡与整场收尾路径。超时将本管剩余血量清零后推进，
不额外发送受击/打空事件。LastBarTimedOut 保留最近一次血管结束是否因超时，当前奖励仍沿用既有规则。

## 分段血量与推进

分段负责非符／符卡归属、血量、承伤倍率、颜色和独立时限，状态负责弹幕行为。
有分段时整管上限由各段血量合计，旧整管血量、时限与状态不生效；无分段时保持整管模式。
不自动迁移 SO，Inspector 的显式转换复制原状态，保留旧配置并支持 Undo。
分段 ID 在本管内唯一，每段至少一个非空状态；血量与启用的时限须为有限正数，承伤倍率须为有限非负数。
分段内血量切换阈值以当前段为基准，处于 0 与 100 之间且严格递减。

BossHealth 独立持有分段剩余血量，不写回配置。伤害按当前段倍率结算，击破溢出丢弃，
空段在 Controller 推进前拒绝后续伤害。整管剩余血量包含当前段剩余血量及未来段血量；
只有最后一段耗尽才发送整管耗尽通知，分段耗尽通知不受 TriggerOnEmpty 控制。

Controller 按路径展平分段内状态，沿用现有动作、指令和阶段通知，避免重复的分段演出系统。
段内切状态不回血、不重置分段计时；打空或超时优先于状态切换，超时只清空当前段且不发送击破通知。
切段复用退出和入场等待，期间无敌且暂停计时。最后一段结束才换管，最终死亡仍走既有幂等收尾。
LastSegmentEndReason 及对应管、段索引记录击破、超时或取消；LastBarTimedOut 只反映最近整管结束原因。
状态奖励仍按实际退出结算，未进入的状态不补发；分段结束原因不等于完整的符卡收取判定。

## 阶段动作与死亡收尾

阶段动作保存在 BossPhase，Encounter 负责执行，并通过 Controller 的等待接口协调：

- 正常入场先执行 EnterActions，再执行 EnterCommands 和阶段行为，最后发送进入通知。
- 正常退出先发送退出通知并停止行为，执行 ExitCommands，再执行 ExitActions。
- 死亡仍退出当前阶段并执行 ExitCommands；不启动阶段 ExitActions，但保留退出音效。
  取消未完成的开场与阶段动作后，转入整场 DefeatActions。

等待式开场或阶段动作失败/取消时，Controller 停止，Encounter 随后清理并结束。
击破和完成动作的失败句柄按已结束处理，继续收尾；非等待动作失败不会阻塞推进。
非等待退出动作可以与下一阶段入场并行，下次阶段退出会清理此前仍运行的动作。

实际路径的最后一管打空或超时后，Controller 完成血管推进并由 BossHealth 触发死亡，Boss 和 Encounter 共用幂等 Stop 完成阶段收尾。
Stop 仅在 Health 确认死亡时发送击败事件；清场取消活着的 Boss 不发送击败通知。
Boss 击败通知与整场完成是不同事件：等待式 DefeatActions 可保留实体，完成后销毁；
DefeatOutroDelay 从死亡开始计时，与击破动作并行等待，两者满足后启动 CompleteActions。
CompleteActions 必须允许实体已销毁。遭遇完成或取消时清理剩余动作并释放临时配置。

## 血量 UI 与奖励

BossHealth 提供当前管比例和实际路径上的有效血管计数，死亡后读取当前管返回零。
OnHealthChanged 在一次伤害完整结算及死亡通知后发送，不受 TriggerOnEmpty 控制。
数据层剩余管数包含当前管，HUD 只显示后续管数，不以行为阶段数推算血管数。
Controller 提供只读剩余秒数；关闭时限、停止或没有有效数据时返回不可用。
倒计时格式、绑定与显隐见 [HUD 架构](./arch-hud.md)。

阶段奖励在 ExitCommands 配置 SpawnDropsCommand。Controller 提供正常阶段结束、
Boss 死亡或手动停止的调用原因；阶段奖励按配置允许前两者，手动停止不产生奖励。
每次实际退出只结算一次，未进入或跳过的阶段不补发。
最终奖励可配置在保持到死亡的最后阶段，不要在退出指令和演出中重复配置同一奖励。

## 扩展与协作

新增 BossPhase 或 BossSignal 子类时沿用 Serializable / SRName 约定，在 Encounter 的
血管内状态或 Signals 列表配置。扩展类型的运行状态必须按每场实例隔离。
OnPhaseEntered / OnPhaseExited 用于通知；需要阻塞的演出使用阶段 ActionSequence。

- [敌人行为](./arch-enemy-ai.md)：ShooterPhase 复用 BehaviorFlow。
- [通用动作](./arch-game-actions.md)：等待、取消与指令执行契约。
- [关卡](./arch-level.md)：遭遇生成、推进与时间轴阻塞。
- [对话](./arch-dialogue.md)：战前用 StartActions，保留实体的战后用 DefeatActions。
- [道具](./arch-items.md)：阶段奖励生成及回收。
