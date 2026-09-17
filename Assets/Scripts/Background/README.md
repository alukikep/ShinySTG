# 3D 背景系统

提供独立于战斗相机的三维背景、直线循环布景、Cue 过渡、播放句柄与关卡绑定。Boss GameAction 尚未接入。

职责和扩展边界见 [背景架构](../../../docs/architecture/arch-background.md)。首次配置按下列章节顺序操作。

## 双相机与占位布景

1. 等 Unity 完成脚本导入，确认 Console 没有编译错误。
2. 打开游戏场景。当前项目中的候选场景是 `Assets/Scenes/SampleScene.unity`；先保存已有编辑。
3. 在 `Edit > Project Settings > Tags and Layers` 中，将一个空闲的 User Layer 命名为 `Background3D`。不要覆盖已有层。
4. 在 Hierarchy 选中 `Main Camera`，执行 `STG > Background > Create Prototype For Selected Camera`。
5. 保存场景。工具创建 `StageBackgroundRoot`，并自动选中其 `CameraRig`。

工具只修改所选 Camera 的 Clear Flags 和 Culling Mask，保留位置、旋转、正交尺寸和视口。背景相机复制创建时的视口和 Target Display，并先于战斗相机绘制；以后手动修改战斗视口或输出显示器时，需要同步背景相机。背景使用默认立方体材质，无 Collider、无额外 AudioListener；专用灯光只照射背景层，已有场景灯光仍可能影响背景。

当前 SampleScene 的 Main Camera 正交尺寸为 9.5，位置为 (0, 1, -10)。不要为了配合背景更改这些战斗参数。现有 Canvas 是 Screen Space Overlay，保持原配置即可。

## 双相机验收

1. 进入 Play，确认能看到道路、两侧方柱和原有玩家、弹幕、UI。
2. 在 Scene 视图关闭工具栏的 2D 按钮，以便编辑三维布景；这不会改变 Game 视图的相机投影。
3. 选中 `StageBackgroundRoot/CameraRig`，在 Play 中把 Rotation Y 从 0 调到 15，Rotation X 从 30 调到 40，再将 Position Z 从 -10 调到 -5。不要修改 Main Camera。
4. 在不输入移动时观察玩家：背景构图改变，玩家屏幕位置应保持不变。开火、移动和敌弹碰撞仍应正常。
5. 将背景方柱临时放到背景相机前，确认方柱不会遮挡战斗对象。检查 UI 和对话仍能显示。
6. 分别使用 4:3 和 16:9 的 Game 视图比例验证两层对齐。原型沿用现有全屏视口，不会自动创建固定比例的东方式侧栏；窗口比例改变可能改变正交相机的水平可见范围。
7. 退出 Play，确认测试时对 CameraRig 的改动已恢复，Console 无新增错误。

创建操作合并为一个 Undo 组，且将场景标记为未保存。创建后立即 Undo 应同时删除原型并恢复战斗相机的原配置，Redo 应重新创建。此检查需要在进入 Play 前进行；之后保存并重新打开场景，确认引用仍有效。Layer 的手动添加不属于该 Undo 组。

## 循环布景

1. 保存第一阶段的场景，退出 Play，等待脚本导入完成。
2. 选中 `StageBackgroundRoot` 或其子物体，执行 `STG > Background > Upgrade Selected Background To Loop`。
3. 工具保留并隐藏旧 `BackgroundContent`，创建 `LoopingBackgroundContent` 和八段静态布景，不改变相机。保存场景。
4. 进入 Play 后自动向背景局部 -Z 滚动。在新物体的 `Looping Background Strip` 组件修改 Speed，例如 0、8、24。
5. 用组件右上角菜单执行 `Pause Background`、`Resume Background`、`Reset Background`，或直接勾选 Paused。Reset 恢复循环相位和路段根变换，保留当前速度及暂停状态；建议暂停后重置，方便观察。

验收：连续运行数分钟，观察道路接缝与方柱；分别测试速度 0/8/24、暂停后恢复、暂停后重置、关闭再启用组件。路段数量应始终为八，重新进入 Play 应恢复初始排列。升级后先在编辑模式检查一次 Undo/Redo：旧布景启用状态、新物体及组件引用应一同恢复，再保存并重新打开场景检查持久化。

运行时使用缩放时间，`Time.timeScale = 0` 会暂停。仅禁用组件或背景根物体时保留相位，再启用继续。玩家输入锁不影响背景。循环组件自身不监听关卡重开；配置关卡绑定后会统一复位，也可通过控制器手动复位。

路段中心位于各段局部原点，几何沿 Z 对称铺满 Segment Length，直接子物体的层级顺序决定排列。布局参数和直接子路段数量只能在非播放模式调整。循环区间后端必须位于视野之外，前端必须覆盖背景相机的可见范围；默认布景按第一阶段相机配置设计，若扩大 Far Clip 或大幅移动、旋转镜头，需要重新配置覆盖范围。这里的循环仅复用静态几何，不支持带独立动画、粒子或其他可变状态的路段；加入这些内容时须先扩展复用重置契约。

升级代码：[LoopingBackgroundSetup.cs](./Editor/LoopingBackgroundSetup.cs)；运行组件：[LoopingBackgroundStrip.cs](./LoopingBackgroundStrip.cs)。每帧只更新预建路段的位置，没有实例化、销毁或新建集合。手动回退时删除 LoopingBackgroundContent 并重新启用 BackgroundContent，相机保持双层配置。

## Cue 过渡

1. 退出 Play，在 `StageBackgroundRoot` 上添加 `StageBackgroundController`。
2. 将其 `Camera Rig` 绑定到 CameraRig，`Background Camera` 绑定到 CameraRig 下的 BackgroundCamera，`Strip` 绑定到 LoopingBackgroundContent 上的循环组件。保存场景。禁止绑定战斗相机；工具会验证背景层和层级。
3. 在 Project 中右键 `Create > STG > Background > Cue` 创建两份资产：`SlowTurn` 和 `Cruise`。两份资产的曲线保持默认 EaseInOut。
4. `SlowTurn`：Local Position = (0,8,-10)，Local Euler Angles = (35,10,0)，Field Of View = 48，Scroll Speed = 3，Duration = 2。
5. `Cruise`：Local Position = (0,8,-10)，Local Euler Angles = (30,0,0)，Field Of View = 50，Scroll Speed = 8，Duration = 2。这些值对应默认原型；若你修改过初始镜头，恢复配置应填写自己的参数。
6. 进入 Play，选中根节点，在 Controller Inspector 的 Preview Cue 拖入 SlowTurn，点 Play Cue。过渡结束后改为 Cruise，再点 Play Cue。

过渡过程中从实际当前状态插值，旋转走最短路径，结束时保持目标值。Cue 不会自动串联、自动播放或自动往返。Preview Cue 仅是编辑器临时选择，不保存到场景。再次播放会接管当前过渡，不叠加协程。播放时复制参数，不改写 Cue；在 Project 修改 SO 资产会持久保存，即使处于 Play 模式也一样，当前播放使用启动时快照。

验收：位置、角度、FOV、速度应平滑变化，玩家和弹幕屏幕坐标不受影响；播放一半切换另一份 Cue 应从当前画面继续；把 Duration 设置为 0 后应立即应用目标。过渡中暂停循环组件应同时暂停镜头，恢复后继续；Time.timeScale 为 0 也会暂停。禁用 Controller 结束过渡并保留当前值，循环组件独立继续滚动；禁用整个背景根节点则滚动也停止。无效 Cue 不替换已有过渡。进入 Play 后不应同时手动编辑镜头或让其他动画控制相同参数。

曲线必须从 (0,0) 到 (1,1)，输出进度限制在 0~1；不支持通过曲线超调。测试时先用小幅偏转，位置/FOV 大幅变化可能露出布景边界，需要调整布景覆盖范围。完整复位使用控制器的 Reset Entire Background，关卡接入见下文，Boss 动作接入尚未实现。

源码：[BackgroundCue.cs](./BackgroundCue.cs)、[StageBackgroundController.cs](./StageBackgroundController.cs)。运行时代码不引用 UnityEditor，Inspector 配置使用 Unity 序列化编辑，支持标准 Undo 和场景保存。

## 播放控制

进入 Play 后在根节点 Inspector 使用 Pause Background、Resume Background、Cancel Playback、Reset Entire Background。Playback 显示 Playing、Completed、Cancelled 或 Failed；暂停单独显示，暂停不会结束句柄。运行时禁止在该 Inspector 改绑背景引用。

控制器 Awake 记录初始局部镜头位置/旋转、FOV、速度与暂停状态；如果启动时未配置好，则在首次有效操作前记录。完整重置先取消当前播放，然后恢复这些参数并重置路段排列。默认启动为未暂停，因此重置后会继续滚动；需要静止检查时再点击 Pause。初始状态不随重复播放更新。循环组件自己的 Reset Background 仍仅复位路段，完整复位请使用控制器按钮。

`Play(cue)` 返回独立的 `BackgroundPlaybackHandle`。`IsComplete` 表示任何终态，成功须判断 `Status == BackgroundPlaybackStatus.Completed`；失败可读 Failure。Cancel 幂等，仅终止本次过渡，不停止道路或恢复速度。新播放取消旧句柄并从当前画面接管；再取消旧句柄不会影响新播放。无效请求返回失败句柄，不替换 CurrentPlayback；零时长成功请求立即返回 Completed，即使处于暂停状态。

禁用或销毁控制器取消播放，旧句柄不会继续等待。重新启用不自动恢复已取消的演出；单独禁用控制器时道路独立继续滚动，禁用根节点时整个背景停止。引用失效或曲线求值无效会使正在播放的句柄 Failed。统一 Pause 与 Time.timeScale=0 都保留演出进度；全局时间缩放不由 Resume 修改。

验收：过渡中暂停/恢复；连续播放 A、B 后检查 A 为 Cancelled 且取消 A 不影响 B；播放非法 Cue 时旧播放继续；任意时刻完整重置并检查镜头/速度/路段；完成后再取消仍为 Completed；过渡中禁用控制器后句柄为 Cancelled。退出再进入 Play、保存后重新打开场景也应正常。关卡重开由绑定处理，GameAction 适配尚未实现。

## 关卡绑定

1. 退出 Play，在已有 LevelController 的同一个 GameObject 上添加 `LevelBackgroundBinding`，将 Background 引用拖为 StageBackgroundRoot 上的控制器。一个背景只绑定一个关卡控制器，运行中不要改引用。
2. 打开该 LevelController 使用的 LevelDefinition，在 Entries 的 SR 类型选择中新增 `背景/Play Background Cue`：TriggerTime=5、Cue=SlowTurn；再新增 TriggerTime=10、Cue=Cruise。保持 Duration=0，关卡总时长须大于 12 秒。同一时间多个 Cue 按条目数组顺序触发，后者接管前者。
3. 保存场景和配置，进入 Play。第 5 秒开始减速转向，第 10 秒恢复；若关卡时间轴被 Boss 阻塞，尚未触发的条目随时间轴等待，已经启动的背景动画继续。
4. 在绑定组件右上角菜单执行 Restart Bound Level (Play Mode)，检查背景与关卡从头开始，两个时间点重新触发。该按钮使用 BeginLevel；正常游戏调用 ReloadLevel 同样通过 OnLevelStart 重置背景。
5. 用 Complete Bound Level (Play Mode) 验证结束：当前过渡取消、滚动暂停、镜头停在当前状态；再次重开恢复启动参数和初始暂停状态。

背景 Entry 始终只触发一次，OneShot 字段不改变这一语义；Duration 不控制过渡，应保持 0，时长配置在 Cue 中。不会等待过渡，也不阻塞刷怪；缺绑定或无效 Cue 会记录警告并跳过。旧关卡不添加绑定或条目时行为保持不变。

绑定禁用时解除全部订阅并停止背景；重新启用时，如果关卡运行中则重置背景，如果已经完成则保持停止。不会补播禁用期间错过的 Cue，也不做时间轴追帧。未启动且 AutoStart=false 时背景保留手动预览行为，真正 BeginLevel 后才重置。编辑器关卡 Preview（包含 Play 中使用预览 Runtime）不会触发真实背景；请在实际 Play 关卡中验收。

源码：[LevelBackgroundBinding.cs](./LevelBackgroundBinding.cs)、[PlayBackgroundCueEntry.cs](../Level/SpawnEntries/PlayBackgroundCueEntry.cs)。配置写入使用标准 Inspector，支持 Undo/dirty；本功能不自动修改场景或 LevelDefinition。

## 排查与移除

- 菜单不存在：等待编译结束并查看 Console；代码位于 [Editor/BackgroundPrototypeSetup.cs](./Editor/BackgroundPrototypeSetup.cs)。
- 提示缺少 Layer：检查名称大小写，必须为 `Background3D`。
- 背景看不到：检查原来的全屏不透明 Sprite 或 UI Image 是否遮住背景，不要关闭整个 UI Canvas；检查两台相机是否启用、目标显示器与视口是否一致。
- 方柱挡住玩家：检查 Main Camera 是否排除了 Background3D、Clear Flags 是否为 Depth Only，以及新增背景物体的所有子物体是否都在背景层。
- 重复执行：同一场景已有同名根节点时会跳过，不会覆盖已有原型。
- 多场景编辑：工具配置所选相机所在场景。其它场景的相机可能参与同一屏幕绘制，先单独打开目标场景验收。
- 移除：优先在创建后使用 Undo。若手动删除 StageBackgroundRoot，必须同时恢复战斗相机原来的 Clear Flags 和 Culling Mask，否则清屏不完整可能产生残影。未改动前的 SampleScene 配置分别为 Skybox 和 Everything。

第一阶段由编辑器工具配置真实场景对象，第二阶段由 LoopingBackgroundStrip 在运行时驱动布景；保存后的配置可随场景进入构建。新目录和脚本的 `.meta` 由 Unity 导入时生成，提交时应一并保留。
