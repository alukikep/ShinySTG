# 敌人行为配置

行为流和移动模块的职责见[敌人 AI 架构](../../../docs/architecture/arch-enemy-ai.md)，
Boss 的配置入口见 [Boss README](Boss/README.md)。

## 清除与强制死亡

在 `Action/Execute Global Commands` 中使用 `Command/Clear Enemies` 可无奖励清除普通敌人，
勾选 `Play Death Effect` 可播放各敌人自身的死亡特效。需要同时触发掉落和击杀计分时，
使用 `Command/Kill Enemies`。两者都作用于当前全部普通敌人，保留 Boss。
配置步骤与验收见[清除普通敌人](../GameActions/README.md#清除普通敌人)。

## 直线移动：加速起步与急刹

在 Behavior Flow 资产中添加或展开 `Action/Move`，将 `Move` 设为 `Move/Linear`。
设置移动方向和巡航速度，再通过 `Profile` 选择匀速、仅加速、仅刹车或同时启用两者。
旧资产默认保持 `Constant`，需要主动切换才会启用过渡。

例如，配置一次向下入场并停住的动作：

1. 将动作的 `DurationConfig` 设为 `Duration/Fixed`，`Value = 2` 秒。
2. 将直线移动的 `Direction` 设为 `Down`，`Speed = 3`。
3. 将 `Profile` 设为 `AccelerateAndBrake`。
4. 将 `Acceleration Time` 设为 `0.25` 秒，`Braking Time` 设为 `0.12` 秒。

这段动作在前 0.25 秒从静止加速，随后巡航，在最后 0.12 秒减速到零。
缩短刹车时间会更有急刹感，延长则更柔和；过渡时间设为零表示瞬间切换。
如果只需要入场刹停，选择 `Brake`；只需要起步加速，选择 `Accelerate`。

加减速使用动作本次的实际时长，因此 `Duration/Random Range` 和循环同样适用，
无需额外设置移动总时长。动作太短、容纳不下两段过渡时，会按比例缩短过渡并取消巡航段。

## 调整落点与行为衔接

`Speed` 表示巡航速度。启用加减速后，同样速度和时长的总位移会缩短；
调整已有入场轨迹时，需要重新确认落点。移动方向始终保持直线，
`ToPlayer` 仍然只在进入动作时锁定一次方向，不会持续追踪玩家。

刹车不会延长动作时间。需要到位后停留时，在后面安排 `Action/Wait`，
或安排不包含移动的开火动作。死亡、切阶段或父容器提前结束会立即中断移动；
若要完整刹停，应保证外层容器或阶段允许该移动动作自然结束。

并行容器会执行子动作到期前最后一段有效更新，再退出子动作。
因此并行配置中的持续开火也会计入最后这段时间，调整已有配置后应检查末次发射节奏。
