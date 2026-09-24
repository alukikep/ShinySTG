# 通用游戏动作与全局指令

`GameAction` 用于可等待、可取消的跨帧逻辑；`GlobalCommand` 用于消弹、添加或移除无敌锁等瞬时操作。
动作通过 `ExecuteCommandsAction` 复用指令层，敌人行为通过 `ExecuteGlobalCommandsAction` 调用同一指令层。
`EnemyAction` 继续负责基于 Transform 和 Duration 的战斗行为，不迁移已有资产类型。

## 配置与执行边界

`ActionSequence` 持有 SR 多态动作数组。`GameActionRunner` 由宿主持有并推进，不是场景单例。
每次 Play 返回独立句柄；连续瞬时动作可在同一次调用中完成，等待动作由后续 Tick 推进。
Parallel 同时启动子动作并等待全部结束。数组中的配置作为只读模板，运行状态放在
每次 CreateRuntime 新建的实例中，不能通过修改共享 SO 模板记录计时或目标。

`WaitForCompletion` 由宿主解释，Runner 本身不暂停游戏。
Encounter 将其解释为阶段或收尾的等待条件，关卡时间轴是否阻塞仍由 BlockTimeline 决定。
无敌作用域总是等待内部序列结束，内部序列的 WaitForCompletion 不改变保护范围。

## 生命周期与扩展契约

新增演出能力时，新建带 `[Serializable, SRName("Game Action/名称")]` 的 GameAction 子类，
实现 CreateRuntime，返回拥有独立状态的 GameActionRuntime。可参考
[WaitGameAction](../../Assets/Scripts/GameActions/WaitGameAction.cs)；不需要为每种演出修改 Encounter 字段。
PlayDialogueAction 已适配对话句柄，按真实结束状态完成，Dispose 只取消自己启动的会话。
场景需要唯一启用的 DialogueService；缺服务、播放失败或外部取消终止本组动作并记录 Failure。
PlayBackgroundCueAction 通过当前关卡的 LevelBackgroundBinding 播放，使用独立背景句柄等待及取消。Encounter 将真实 LevelRuntime 传入 Context，CompleteActions 不依赖 Owner 存活。编辑模式预览跳过背景动作，Play 中的预览或旧 Runtime 被拒绝。外部接管、播放失败和缺绑定终止本组动作并记录 Failure。
Timeline 适配尚未实现，也应按实际完成状态结束，而非猜测播放时长。

- Start 启动，Tick 推进，IsComplete 表示结束；Dispose 在正常结束、取消或失败时释放资源。
- 句柄的 Cancel 幂等；异常被记录到 Failure 并终止该序列，后续动作不再执行。
  等待式开场和阶段动作失败/取消会停止 Controller 并结束遭遇；击破、完成动作的失败句柄按已结束处理，继续收尾。非等待动作不阻塞推进，均不自动重试。
- Runner.Dispose 取消当前所有句柄并清空集合；Runner 可以继续 Play 新序列，Encounter 在死亡时用它切换到击破动作。
- Context 的 Position 是调用时快照；Owner 是可能已被销毁的 Unity 对象，跨帧使用前必须检查。
- RunBehaviorFlowAction 克隆 Flow，完成或取消时释放克隆。循环 Flow 需要外部取消；
  不应与战斗行为流同时写入同一对象的位置。

## 无敌与回收

ExecuteCommandsAction 的无敌操作是持久命名锁，取消动作不会撤销已经执行的命令。
需要临时保护时使用 InvincibilityScopeAction：每次执行生成独立 Key，保存本次保护对象，
在完成、取消、异常时解除自己的锁，不影响复活无敌或其他来源。当前仅支持玩家与 Owner；
期间新生成的单位不会自动继承保护。

消弹仍走 BulletPool/LaserPool 的标准回收流程。指令按阵营筛选，默认包括敌方子弹和激光；
一次清除不禁止后续发射，也不暂停世界。`ClearProjectilesCommand` 可为 Bullet 选择批量表现：
静默回收、按空间网格聚合的消弹特效，或按比例且受上限约束的道具转换。
默认仍为静默回收；激光目前继续走静默回收。表现超出预算时只跳过多余表现，不影响子弹回池。

## 普通敌人清场

ClearEnemiesCommand 对执行时已登记的普通敌人发起无奖励自毁，排除 Boss。
该操作不依赖有效 Owner，不经过伤害或击杀事件，不受无敌限制；不清除已发射弹幕，
也不影响之后生成的敌人。需要同时消弹时组合 ClearProjectilesCommand。
指令为瞬时操作，取消外层动作不会恢复已清除的敌人。
ClearEnemiesCommand 可选择播放敌人自身的死亡特效，仍不触发死亡事件、掉落或击杀分。
KillEnemiesCommand 则请求普通敌人强制死亡，绕过无敌和伤害限制，复用死亡事件、特效、掉落及计分。
两者使用活跃登记快照遍历，均排除 Boss、不依赖 Owner，也不影响已有弹幕和后续刷怪。
生命周期由 [Enemy 自毁入口](./arch-enemy-ai.md#自毁与出界)与[死亡结算](./arch-enemy-ai.md#死亡掉落)负责。

## 撒道具指令

SpawnDropsCommand 是瞬时生成指令；通过 ExecuteCommandsAction 或 ExecuteGlobalCommandsAction
可复用，不等待道具下落或拾取，也不会在取消动作时撤销已生成道具。
GlobalCommandContext.Invocation 由调用宿主提供：BossController 传入阶段退出原因，
PlayerDeathController 传入 PlayerDeath，普通显式调用为 Direct。
Direct 和 PlayerDeath 不受 SpawnDropsCommand 的 Boss 阶段完成或死亡开关限制。

当前撒道具指令需要有效 Owner，使用其执行时位置；ExecuteCommandsAction 不将 GameActionContext.Position
快照传给指令。需要 Boss 位置时应在其销毁前执行，不能依赖 CompleteActions 中已失效的 Owner。

## 玩家死亡指令

PlayerDeathController 直接执行 SR 指令数组，不通过 GameActionRunner 等待指令。
每次失去一命（包含最后一命）执行一次，Owner 是玩家 Transform；特效和重生等待由玩家协调组件负责。
执行延后到受击的下一帧，避开子弹和激光碰撞遍历。指令按数组顺序同步执行，空项跳过；
异常会中断余下指令，由玩家宿主记录并继续死亡演出，不回滚已执行的指令。
取消演出不会撤销已清除弹幕、已生成道具或命名无敌锁。
配置见 [玩家操作说明](../../Assets/Scripts/Player/README.md)。

## 与其他板块的关系

- [background](./arch-background.md)：Cue 句柄、关卡绑定与背景动作控制权。

- [items](./arch-items.md)：SpawnDropsCommand 复用道具生成与配置。

- [dialogue](./arch-dialogue.md)：对话播放、控制锁与取消边界。
- [boss](./arch-boss.md)：阶段过渡与击破保留由 Controller/Boss 提供生命周期入口。
- [level](./arch-level.md)：Encounter 是动作宿主，关卡阻塞时仍推进动作。
- [enemy-ai](./arch-enemy-ai.md)：通过行为流适配和全局指令共享现有能力。
- [操作说明](../../Assets/Scripts/GameActions/README.md)：Inspector 配置、最小例子和公共调用入口。

## 背景 Sprite 动作

SetBackgroundImageAction 通过当前关卡绑定控制 Lower 或 Upper 层，沿用真实 LevelRuntime 校验和预览隔离，按实际淡化句柄等待。每层独立于 3D 镜头播放；同层接管会取消旧动作，旧句柄释放不会影响新请求。正常完成后图片继续显示，Image 留空执行隐藏，符卡退出与击破收尾应显式配置隐藏动作。取消仅保留当前画面，不自动回滚到进入前的图片；完整换景和关卡生命周期负责统一重置。配置与渲染边界见 [背景架构](./arch-background.md)。
