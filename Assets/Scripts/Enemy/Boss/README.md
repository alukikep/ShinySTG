# Boss 遭遇配置

Boss Encounter SO 是完整 Boss 战的配置入口：选择实体 prefab，配置血管、Signals、阶段和整场动作。
每个阶段同时保存行为流、退出条件、保护、音效、动作与指令；重排阶段会一起移动这些配置。
prefab 保留外观、碰撞及运行时组件，不再保存血管、阶段和 Signals。

关卡使用 BossEncounterEntry 引用 SO，并设置出生位置与是否等待整场结束。
直接放入场景时，在 Boss 组件指定 Encounter；独立宿主会在 Boss 销毁后继续推进收尾。
每次遭遇通过 Unity 序列化克隆创建临时配置，血量、阶段行为与信号状态不会写回源资产。
Boss 实体使用创建/销毁生命周期，不支持将执行过的实例重新初始化。

入场顺序为阶段 EnterActions → EnterCommands → 行为启动；正常退场为行为退出 →
ExitCommands → ExitActions。死亡仍执行当前阶段退出指令，随后运行整场 DefeatActions，
不运行阶段 ExitActions。CompleteActions 需允许 Boss 已销毁。等待动作不会自动无敌。

不提供旧 prefab 或 PhasePresentations 的迁移和回退，测试配置请在 SO 中重新建立。

## 最小配置

1. 创建 STG/Boss Encounter 资产，指定带 Boss 组件的实体 prefab。
2. 配置至少一管血：ID 唯一且非空，血量上限为有限正数。
3. 添加 Shooter 阶段并指定 BehaviorFlow；需要自动切阶段时再添加退出条件。
4. 按需配置开场、阶段入场/退场、击破和收尾动作。
5. 关卡中添加 BossEncounterEntry 引用此资产；直接场景测试则在 Boss 组件指定 Encounter。

Boss prefab 的 `BossHealth.ScoreValue` 是完整击破奖励，默认 10000；血管切换不会重复发放该奖励。
血管击破目前只推进 Encounter 生命周期，不单独发放分数。最终死亡事件由玩家的 `ScoreManager` 统一计入。

场景直接放置入口没有 LevelRuntime，需要关卡上下文的背景等动作应通过关卡条目运行。
资产校验会拒绝空阶段列表、空阶段及无效血管，不能通过 prefab 上的旧数值补全。

## 阶段条件

Inspector 新建阶段默认使用 `Conditions`。基类代码默认仍为 `LegacyTriggers`，按 Signals 与 ExitTriggers 判断；这只是条件模式，不代表旧 prefab 配置回退。
选择 `Conditions` 后只使用 ExitConditions；切换模式不会删除另一套配置。

## 配置新条件

1. 在 Boss Encounter SO 编辑血管名称和血量，编辑后自动补齐空 ID、修复重复 ID。
   已有列表可点击“整理血管 ID”；重复 ID 保留第一个，其余副本生成新 ID，请确认阶段引用。
   打开 Inspector 不自动写资产，编辑和整理操作支持 Undo。
2. 将目标阶段的 Exit Mode 设为 Conditions，添加 Exit Conditions。
   在血管下拉中按名称选择目标，无需手填 ID。新建 Shooter 阶段默认使用 Conditions。
3. 选择条件类型及阈值，再选择 Any（任意满足）或 All（全部满足）。
   例如“spell-a 剩余不超过 50% 且本阶段持续至少 10 秒”使用两个条件并选择 All。

面板只显示当前模式和条件类型需要的字段，并提供条件摘要、失效引用和阈值校验。
开启过渡保护时检查提示，避免依赖继续扣血才能退出。Play 中 Encounter 配置只读，BossController Inspector 的运行时观察区
显示当前阶段、计时、保护状态、条件当前值及满足状态。过渡时显示旧阶段条件值仅用于观察。
本项目处于测试阶段，不提供旧配置迁移工具；可以直接删除并重建测试阶段。

指定管条件始终读取被引用的血管。耗尽状态在切管后仍成立，不依赖 TriggerOnEmpty；
未来血管视为满血，已耗尽血管视为零。缺失、重复、空 Id 或非正血量上限不满足条件，
不会回退到当前管。运行中不要重排或改写血管配置。

阶段时间从正式进入阶段开始累计，过渡等待不计时，每次进入（包括循环）重置。
Encounter 要求有效血管列表；指定血管条件要求 Bars 中有有效 Id。底层 Health 的单管 API 仅保留给独立调用，不是 Encounter 的配置入口。
空条件列表不自动退出。All 中的空元素判为不满足；Any 可以由其他有效条件满足。

高级 Signal 条件保留信号数组索引与比较符语义，但新条件统一读取伤害结算后的状态。
要检测血管耗尽，请用 BarDepleted；旧模式仍保留瞬时零血兼容检测。

## 伤害与过渡保护

每管的 Discard Overflow 默认关闭，保持伤害继续扣下一管。开启后只丢弃打空本管的
这一次攻击的剩余伤害，后续攻击仍可伤害下一管；切阶段不自动回血。

一管一阶段可在阶段上开启 Protect Bar Transition，并将 Protected Bar Id 填为该管 Id。
该管耗尽时（不受 TriggerOnEmpty 控制）立即保护后续血管，包括本次溢出与同帧后续攻击，
持续到下一阶段的入场等待、进入指令和进入通知完成。它独立于演出和指令的无敌锁。
仍需配置退出条件；例如使用同一 Id 的 BarDepleted。若使用 All 组合，保护期间会继续等待
其他条件，避免使用需要继续扣血才能满足的组合。

停止、死亡、阶段耗尽、过渡动作取消或失败均释放保护；取消或失败不会进入下一阶段。
Controller 禁用时释放自己持有的保护，重新启用继续原阶段时恢复；这不表示 Boss 复活或池重置。
过渡保护默认关闭。旧瞬时零血请求每次 Update 仅推进一次，不在同一帧继续退出新阶段。

阶段行为与血管仍独立，多个阶段可以引用同一管。

在 Unity Test Runner 的 EditMode 中运行 `BossEncounterTests`、`BossInspectorTests`、`PhaseExitConditionTests`、`BossTransitionTests` 和 `BossHudTests`。
相关设计见 [Boss 架构](../../../../docs/architecture/arch-boss.md)。
