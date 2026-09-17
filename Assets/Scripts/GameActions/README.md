# 通用游戏动作

BossEncounter 资产的 StartActions、DefeatActions、CompleteActions，以及每个
PhasePresentations 条目的 EnterActions、ExitActions 均使用相同的 ActionSequence。
在 Actions 数组使用 SR 下拉选择动作。默认按顺序执行，Parallel 同时启动子动作。

WaitForCompletion 控制宿主是否等待，不暂停整个游戏。旧 SFX 先播放，再运行对应动作；
旧 DefeatOutroDelay 从死亡开始计时，与击破动作并行等待。CompleteActions 在两者结束后启动。
非等待的动作只在宿主仍存活时继续运行；Encounter 完成或取消会取消剩余动作。
死亡取消未完成的开场和阶段动作，执行 DefeatActions；死亡演出请配置在这里。

勾选 WaitForCompletion 时，阶段进入动作完成后才启动战斗与阶段计时。阶段退出时取消 Encounter 当前动作（包括未结束的开场动作），
非等待退出动作可与下一阶段进入动作并行。无敌需显式配置，等待本身不提供无敌。
Execute Commands 中的无敌锁是持久操作，必须显式 Remove；临时保护推荐使用
Invincibility Scope 包裹子动作，它在完成、失败、取消时释放本次调用独有的锁。

代码可持有 GameActionRunner，调用 Play(sequence, context)，每帧 Tick(dt)，
结束时 Dispose()。返回句柄提供 IsComplete、IsCancelled、Failure 和 Cancel()。
GameAction 配置不得保存运行状态；CreateRuntime 每次创建独立的 GameActionRuntime。
Start/Tick 可报告实际完成，Dispose 负责释放资源（正常结束与取消均调用一次）。
对象可能在演出途中销毁，使用上下文 Position 快照或检查 Owner 的 Unity 空值。

Run Behavior Flow 在完成或取消时释放运行时克隆；循环 Flow 需要外部取消。
不要让该动作与战斗行为流同时控制同一个 Transform。

## 最小配置

在 BossEncounter 的 PhasePresentations 添加对应 PhaseIndex。展开 EnterActions，
保持 WaitForCompletion 开启，在 Actions 下拉选择 Game Action/Invincibility Scope。
勾选 ProtectOwner；在其 Sequence.Actions 中加入 Wait（例如 1 秒）和 Play SFX。
执行时 Boss 在这段动作期间无敌，动作结束自动解除该保护，再开始阶段战斗。

要在切阶段时消弹，在 ExitActions.Actions 添加 Execute Commands，
在 Commands 下拉选择 Command/Clear Projectiles。不要在 BossPhase.ExitCommands 再配置同一次消弹。
若要演出与战斗同时运行，关闭对应外层 WaitForCompletion；动作可能在下一次阶段退出或遭遇结束时被取消。

扩展契约见 [通用游戏动作架构](../../../docs/architecture/arch-game-actions.md)。
