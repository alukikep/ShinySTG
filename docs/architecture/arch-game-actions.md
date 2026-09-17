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
对话和 Timeline 的具体适配尚未实现，后续适配应按实际完成状态结束，而非猜测播放时长。

- Start 启动，Tick 推进，IsComplete 表示结束；Dispose 在正常结束、取消或失败时释放资源。
- 句柄的 Cancel 幂等；异常被记录到 Failure 并终止该序列，后续动作不再执行。
  当前 Encounter 将失败句柄视为已结束，不自动重试或中止整场遭遇。
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
一次清除不禁止后续发射，也不暂停世界。

## 与其他板块的关系

- [boss](./arch-boss.md)：阶段过渡与击破保留由 Controller/Boss 提供生命周期入口。
- [level](./arch-level.md)：Encounter 是动作宿主，关卡阻塞时仍推进动作。
- [enemy-ai](./arch-enemy-ai.md)：通过行为流适配和全局指令共享现有能力。
- [操作说明](../../Assets/Scripts/GameActions/README.md)：Inspector 配置、最小例子和公共调用入口。
