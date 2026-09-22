# 3D 背景系统

## 多姿态循环镜头

Project 右键 `Create > STG > Background > Loop Cue` 创建循环配置。在 Nodes 中增删、拖动排序镜头节点；各节点的 Duration 表示从前一节点到达此节点的时间，Hold Duration 表示到达后停留时间。Entry Duration / Entry Easing 仅用于首次从当前镜头进入节点 0，同时将背景滚动速度过渡到 Scroll Speed。

播放顺序为“当前镜头 → A → B → C → A → …”。末尾回到 A 使用 A 自身的 Duration / Easing。左右摆动用两个节点；需要原路返回时可排列为 A、B、C、B。位置、角度和 FOV 都按节点过渡，旋转走最短路径。时间允许为 0，但整轮总时长必须大于 0；单节点进入后保持该姿态。空列表或非法参数不会打断已有播放。

- 关卡时间轴添加 `背景/Play Background Loop` 并指定 Cue，触发一次即可持续播放。
- Boss 动作序列添加 `Game Action/Start Background Loop`。动作启动成功即完成，循环由控制器持有；如果紧接另一条镜头动作，它会立即接管。需要持续一段时间时，在后续时间点或阶段再调用新镜头。
- 直接代码调用 `StageBackgroundController.PlayLoop(cue)`，返回的句柄持续 Playing，直到取消或失败。句柄 Cancel 仅终止本次播放。
- Play 模式下，在控制器 Inspector 指定 Loop Cue，点击 Play Loop；Playback Kind 显示当前是 Cue、Loop 还是 Switch。

普通 Cue、另一段循环或换景都会取消旧循环，从当前画面接管。暂停冻结进度，恢复后继续；Boss 阻塞时间轴不暂停镜头。关卡结束停止播放并暂停背景，重开恢复初始状态。配置在启动时深复制，运行中编辑资产不改变已启动循环。

验收建议：使用三个不同位置/角度的节点，分别设置不同过渡时间和停留时间，观察完整循环及末尾回首节点；在进入、移动、停留时分别测试暂停和接管；检查 Boss 后续动作继续执行、关卡重开复位、零时长节点和长帧不锁死。通过 Unity 默认序列化 Inspector 编辑列表，支持 Undo 和资产保存。镜头最大范围仍需在 Game 视图检查，避免露出布景边缘。

提供独立于战斗相机的三维背景、直线循环布景、Cue 过渡、播放句柄与关卡绑定。Boss Encounter 可通过 Play Background Cue 动作等待背景过渡。

职责和扩展边界见 [背景架构](../../../docs/architecture/arch-background.md)。首次配置按下列章节顺序操作。

## 双相机与占位布景

1. 等 Unity 完成脚本导入，确认 Console 没有编译错误。
2. 打开游戏场景。当前项目中的候选场景是 `Assets/Scenes/SampleScene.unity`；先保存已有编辑。
3. 在 `Edit > Project Settings > Tags and Layers` 中，将一个空闲的 User Layer 命名为 `Background3D`。不要覆盖已有层。
4. 在 Hierarchy 选中 `Main Camera`，执行 `STG > Background > Create Prototype For Selected Camera`。
5. 保存场景。工具创建 `StageBackgroundRoot`，并自动选中其 `CameraRig`。

工具只修改所选 Camera 的 Clear Flags 和 Culling Mask，保留位置、旋转、正交尺寸和视口。背景相机复制创建时的视口和 Target Display，并先于战斗相机绘制；以后手动修改战斗视口或输出显示器时，需要同步背景相机。背景使用默认立方体材质，无 Collider、无额外 AudioListener；专用灯光只照射背景层，已有场景灯光仍可能影响背景。

背景物体及其所有子物体必须使用 `Background3D` Layer。若部分物体仍保留在 Default 或其它图层，它们不会由背景相机统一绘制，可能表现为贴在战斗相机画面上的 2D 图片、远近关系异常或完全不可见。修改层级后要检查 Prefab 根节点和每一级子物体，并重新进入 Play 验证。

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

## 从美术路段 Prefab 生成循环背景

1. 制作一个路段：根节点位置/旋转归零、缩放为 1，模型放到子物体；沿局部 Z 拼接，长度例如 32，接缝位于 Z=-16 和 Z=16。可先将现有 Segment 复制出来并归零，再拖到 Project 保存为 Prefab。
2. 本版仅支持静态几何（MeshRenderer、SpriteRenderer、LODGroup 及必要的 Transform/MeshFilter）。移除碰撞体、Animator、粒子、脚本和 Missing Script，包括非激活子物体。不要把整个背景根节点保存为路段。
3. 退出 Play，选中 LoopingBackgroundContent，打开 `STG > Background > Build Loop From Prefab`。检查 Target Strip，拖入 Segment Prefab，填写 Segment Length、Rear Edge 和 Segment Count。
4. 点击 Replace Segments From Prefab。目标组件下全部子物体会被替换；旧路段不另行备份，但可一次 Undo 恢复。组件自身、速度、暂停设置、相机和外部绑定不变。
5. 保存场景，进入 Play，测试连续滚动、最大速度、所有镜头 Cue、重置与关卡重开。生成后在进入 Play 前验证 Undo/Redo，并保存重开场景确认 Prefab 引用及布局参数持久化。

路段保留 Prefab 连接，生成实例的所有子物体覆盖为 Background3D 层并取消 Static 标记，因为运行时会移动。源 Prefab 不被修改。造型更新可直接修改源 Prefab；若增添新子物体，需再次生成以统一层与 Static 标记，改变长度也需重新生成。禁止后续给这些循环 Prefab 添加本版不支持的可变状态组件。

长度由明确的拼接约定决定，不从树枝等装饰物包围盒自动估算。工具校验结构和数值，但不能判定美术接缝是否吻合或镜头是否露出边缘；需在 Game 视图检查。起始中心为 Rear Edge 减半段长度，后续每段相隔一段长度；增加数量主要扩展前方覆盖，Rear Edge 用于保证回收发生在视野后方。目标支持普通场景对象，以及 Prefab 编辑模式中的本地循环容器。场景实例、嵌套实例和 Variant 的继承容器需打开原始 Prefab 编辑，避免删除继承内容。

源码：[BackgroundPrefabBuilder.cs](./Editor/BackgroundPrefabBuilder.cs)。

## Cue 过渡

1. 退出 Play，在 `StageBackgroundRoot` 上添加 `StageBackgroundController`。
2. 将其 `Camera Rig` 绑定到 CameraRig，`Background Camera` 绑定到 CameraRig 下的 BackgroundCamera，`Strip` 绑定到 LoopingBackgroundContent 上的循环组件。保存场景。禁止绑定战斗相机；工具会验证背景层和层级。
3. 在 Project 中右键 `Create > STG > Background > Cue` 创建两份资产：`SlowTurn` 和 `Cruise`。两份资产的曲线保持默认 EaseInOut。
4. `SlowTurn`：Local Position = (0,8,-10)，Local Euler Angles = (35,10,0)，Field Of View = 48，Scroll Speed = 3，Duration = 2。
5. `Cruise`：Local Position = (0,8,-10)，Local Euler Angles = (30,0,0)，Field Of View = 50，Scroll Speed = 8，Duration = 2。这些值对应默认原型；若你修改过初始镜头，恢复配置应填写自己的参数。
6. 进入 Play，选中根节点，在 Controller Inspector 的 Preview Cue 拖入 SlowTurn，点 Play Cue。过渡结束后改为 Cruise，再点 Play Cue。

过渡过程中从实际当前状态插值，旋转走最短路径，结束时保持目标值。Cue 不会自动串联、自动播放或自动往返。Preview Cue 仅是编辑器临时选择，不保存到场景。再次播放会接管当前过渡，不叠加协程。播放时复制参数，不改写 Cue；在 Project 修改 SO 资产会持久保存，即使处于 Play 模式也一样，当前播放使用启动时快照。

验收：位置、角度、FOV、速度应平滑变化，玩家和弹幕屏幕坐标不受影响；播放一半切换另一份 Cue 应从当前画面继续；把 Duration 设置为 0 后应立即应用目标。过渡中暂停循环组件应同时暂停镜头，恢复后继续；Time.timeScale 为 0 也会暂停。禁用 Controller 结束过渡并保留当前值，循环组件独立继续滚动；禁用整个背景根节点则滚动也停止。无效 Cue 不替换已有过渡。进入 Play 后不应同时手动编辑镜头或让其他动画控制相同参数。

曲线必须从 (0,0) 到 (1,1)，输出进度限制在 0~1；不支持通过曲线超调。测试时先用小幅偏转，位置/FOV 大幅变化可能露出布景边界，需要调整布景覆盖范围。完整复位使用控制器的 Reset Entire Background，关卡接入见下文，Boss 动作接入见下文。

源码：[BackgroundCue.cs](./BackgroundCue.cs)、[StageBackgroundController.cs](./StageBackgroundController.cs)。运行时代码不引用 UnityEditor，Inspector 配置使用 Unity 序列化编辑，支持标准 Undo 和场景保存。

## 播放控制

进入 Play 后在根节点 Inspector 使用 Pause Background、Resume Background、Cancel Playback、Reset Entire Background。Playback 显示 Playing、Completed、Cancelled 或 Failed；暂停单独显示，暂停不会结束句柄。运行时禁止在该 Inspector 改绑背景引用。

控制器 Awake 记录初始局部镜头位置/旋转、FOV、速度与暂停状态；如果启动时未配置好，则在首次有效操作前记录。完整重置先取消当前播放，然后恢复这些参数并重置路段排列。默认启动为未暂停，因此重置后会继续滚动；需要静止检查时再点击 Pause。初始状态不随重复播放更新。循环组件自己的 Reset Background 仍仅复位路段，完整复位请使用控制器按钮。

`Play(cue)` 返回独立的 `BackgroundPlaybackHandle`。`IsComplete` 表示任何终态，成功须判断 `Status == BackgroundPlaybackStatus.Completed`；失败可读 Failure。Cancel 幂等，仅终止本次过渡，不停止道路或恢复速度。新播放取消旧句柄并从当前画面接管；再取消旧句柄不会影响新播放。无效请求返回失败句柄，不替换 CurrentPlayback；零时长成功请求立即返回 Completed，即使处于暂停状态。

禁用或销毁控制器取消播放，旧句柄不会继续等待。重新启用不自动恢复已取消的演出；单独禁用控制器时道路独立继续滚动，禁用根节点时整个背景停止。引用失效或曲线求值无效会使正在播放的句柄 Failed。统一 Pause 与 Time.timeScale=0 都保留演出进度；全局时间缩放不由 Resume 修改。

验收：过渡中暂停/恢复；连续播放 A、B 后检查 A 为 Cancelled 且取消 A 不影响 B；播放非法 Cue 时旧播放继续；任意时刻完整重置并检查镜头/速度/路段；完成后再取消仍为 Completed；过渡中禁用控制器后句柄为 Cancelled。退出再进入 Play、保存后重新打开场景也应正常。关卡重开由绑定处理，Boss 动作配置见下文。

## 关卡绑定

1. 退出 Play，在已有 LevelController 的同一个 GameObject 上添加 `LevelBackgroundBinding`，将 Background 引用拖为 StageBackgroundRoot 上的控制器。一个背景只绑定一个关卡控制器，运行中不要改引用。
2. 打开该 LevelController 使用的 LevelDefinition，在 Entries 的 SR 类型选择中新增 `背景/Play Background Cue`：TriggerTime=5、Cue=SlowTurn；再新增 TriggerTime=10、Cue=Cruise。保持 Duration=0，关卡总时长须大于 12 秒。同一时间多个 Cue 按条目数组顺序触发，后者接管前者。
3. 保存场景和配置，进入 Play。第 5 秒开始减速转向，第 10 秒恢复；若关卡时间轴被 Boss 阻塞，尚未触发的条目随时间轴等待，已经启动的背景动画继续。
4. 在绑定组件右上角菜单执行 Restart Bound Level (Play Mode)，检查背景与关卡从头开始，两个时间点重新触发。该按钮使用 BeginLevel；正常游戏调用 ReloadLevel 同样通过 OnLevelStart 重置背景。
5. 用 Complete Bound Level (Play Mode) 验证结束：当前过渡取消、滚动暂停、镜头停在当前状态；再次重开恢复启动参数和初始暂停状态。

背景 Entry 始终只触发一次，OneShot 字段不改变这一语义；Duration 不控制过渡，应保持 0，时长配置在 Cue 中。不会等待过渡，也不阻塞刷怪；缺绑定或无效 Cue 会记录警告并跳过。旧关卡不添加绑定或条目时行为保持不变。

绑定禁用时解除全部订阅并停止背景；重新启用时，如果关卡运行中则重置背景，如果已经完成则保持停止。不会补播禁用期间错过的 Cue，也不做时间轴追帧。未启动且 AutoStart=false 时背景保留手动预览行为，真正 BeginLevel 后才重置。编辑器关卡 Preview（包含 Play 中使用预览 Runtime）不会触发真实背景；请在实际 Play 关卡中验收。

源码：[LevelBackgroundBinding.cs](./LevelBackgroundBinding.cs)、[PlayBackgroundCueEntry.cs](../Level/SpawnEntries/PlayBackgroundCueEntry.cs)。配置写入使用标准 Inspector，支持 Undo/dirty；本功能不自动修改场景；关卡 SO 可配置 InitialBackground，在加载揭幕后立即应用。

## 排查与移除

- 菜单不存在：等待编译结束并查看 Console；代码位于 [Editor/BackgroundPrototypeSetup.cs](./Editor/BackgroundPrototypeSetup.cs)。
- 提示缺少 Layer：检查名称大小写，必须为 `Background3D`。
- 背景看不到：检查原来的全屏不透明 Sprite 或 UI Image 是否遮住背景，不要关闭整个 UI Canvas；检查两台相机是否启用、目标显示器与视口是否一致。
- 背景物体像贴在相机上或只有部分物体有伪 3D：优先检查物体及其子物体是否全部位于 `Background3D` Layer；再确认它们由 BackgroundCamera 绘制，而不是被 Main Camera、Canvas 或其它相机单独绘制。
- 方柱挡住玩家：检查 Main Camera 是否排除了 Background3D、Clear Flags 是否为 Depth Only，以及新增背景物体的所有子物体是否都在背景层。
- 重复执行：同一场景已有同名根节点时会跳过，不会覆盖已有原型。
- 多场景编辑：工具配置所选相机所在场景。其它场景的相机可能参与同一屏幕绘制，先单独打开目标场景验收。
- 移除：优先在创建后使用 Undo。若手动删除 StageBackgroundRoot，必须同时恢复战斗相机原来的 Clear Flags 和 Culling Mask，否则清屏不完整可能产生残影。未改动前的 SampleScene 配置分别为 Skybox 和 Everything。

第一阶段由编辑器工具配置真实场景对象，第二阶段由 LoopingBackgroundStrip 在运行时驱动布景；保存后的配置可随场景进入构建。新目录和脚本的 `.meta` 由 Unity 导入时生成，提交时应一并保留。

## Boss 背景演出（4B）

保留已有 LevelBackgroundBinding。在 BossEncounter 资产的 StartActions.Actions 中选择 `Game Action/Play Background Cue` 并指定 SlowTurn，开启该序列的 WaitForCompletion。BossEncounterEntry 的 BlockTimeline 可开启，镜头过渡期间背景仍会独立推进。

在对应 Phases 阶段 的 EnterActions 中配置 Cruise 并开启 WaitForCompletion，即可在进入该阶段前等待恢复镜头。首阶段进入动作紧接开场动作；若希望减速画面保持至后续阶段，请将 Cruise 配置到后续阶段。DefeatActions 和 CompleteActions 同样支持；CompleteActions 不依赖仍然存在的 Boss 对象。

动作本身始终等待实际句柄完成，外层 WaitForCompletion 决定是否阻塞 Boss 阶段；关闭后动作仍由 Encounter 持续管理，并可能在阶段退出或遭遇结束时被取消。不要在同一 Parallel 中同时播放两个背景 Cue。关卡时间点或手动 Cue 可接管当前播放，原动作将报告取消并终止其后续序列。

缺绑定、禁用背景、无效 Cue、旧/缺失关卡上下文或外部取消均通过 Runner 记录 Failure，并结束本组动作；当前 Encounter 将失败视为等待结束，不会自动中止整场遭遇。动作 Dispose 只取消自己持有的句柄，不停止道路、不撤销新 Cue。关卡重开仍由绑定完整重置背景。

编辑模式的 Encounter Preview 将背景动作作为无操作完成；Play 中的预览 Runtime 会被拒绝，不能影响真实背景。自定义动作宿主需要在 GameActionContext 传入当前 LevelController.Runtime，不能只传 Owner。

验收：开场镜头完成后才开始战斗；阶段恢复镜头后再进入战斗；阻塞时间轴时背景正常运动；中途重开恢复初始状态；手动 Cue 打断时旧序列报告失败且新 Cue 继续；禁用背景或移除绑定后不无限等待；CompleteActions 在 Boss 销毁后仍可播放。失败测试会有预期的 Console 异常日志。运行时配置不应在播放中修改。

源码：[PlayBackgroundCueAction.cs](../GameActions/PlayBackgroundCueAction.cs)。

## 背景专属距离雾

在 Project 右键 Create > Material，命名 BackgroundFog，Shader 选择 `ShinySTG/Background/Distance Fog Unlit`。先以 Fog Start=35、Fog End=100、Fog Strength=1 测试；Fog Color 与 BackgroundCamera 的 Background 颜色设为相同（默认原型约为 RGB 0.06/0.08/0.12），相机使用 Solid Color。Fog End 必须大于 Fog Start；Shader 对错误范围有防除零保护，但不代表错误配置能产生自然过渡。

选中 LoopingBackgroundContent，打开 `STG > Background > Apply Fog Material`，指定新材质，点击 Apply To Background Meshes。工具替换所选层级下 Background3D 层的所有 MeshRenderer 材质槽（包含非激活对象），保存场景。应用前可通过缩小选择范围只处理道路或方柱；多贴图场景应分别制作材质并手动赋值。旧材质资产不会改变，批量赋值可一次 Undo；Prefab 实例记录材质覆盖，源 Prefab 未被修改。若之后重新从路段 Prefab 生成布景，请在源 Prefab 手动配置雾材质，或重新执行批量赋值。

距离按当前绘制相机到像素世界位置计算，远处渐变为雾色。Shader 不读取全局雾，工具不写 RenderSettings、战斗材质或相机。隔离依赖只把材质赋给背景；手动将此材质赋给玩家也会产生雾。无需给循环段添加脚本或每帧更新材质，循环复用无新增可变状态。

当前为不透明无光照材质，支持主贴图、Tint、UV Tiling/Offset；不保留 Standard 材质的灯光、法线、金属度、阴影效果，不支持透明云、树叶裁切或 SpriteRenderer。它不是屏幕后处理，不会给天空空白区域叠雾，也不能自动隐藏尚在近处回收的路段。Fog Strength=0 用于对照；不同雾色需要同步背景相机底色，当前 Cue 不驱动雾参数。

验收：近处清楚、远处渐隐；将 Strength 设为 0/1 对比，并确认玩家、弹幕、UI 不变；播放转向 Cue 检查露边；检查循环接缝、重开和暂停；编辑模式检查 Undo/Redo、保存重开后的材质与 Prefab 覆盖。最终需在 Unity Console 检查 Shader 编译无错误且模型不呈粉色。Play 模式修改材质资产会保存，请在退出后确认参数。

源码：[BackgroundDistanceFog.shader](../../Shaders/BackgroundDistanceFog.shader)、[BackgroundFogMaterialWindow.cs](./Editor/BackgroundFogMaterialWindow.cs)。

## 背景纹理 UV 滚动

现有背景雾材质新增 UV Speed：X/Y 分别为每秒偏移的贴图周期，Z/W 不使用，默认 0 保持静止。设置 Texture 为有明显纹理的无缝贴图，在纹理导入设置中将 Wrap Mode 设为 Repeat 并 Apply。建议先设 X=0、Y=0.1；负值反向。方向取决于模型 UV，白色贴图看不出运动。材质的原始 Tiling/Offset 仍然有效。

无需新增组件。LoopingBackgroundStrip 初始化时收集自身层级下 Background3D 层 MeshRenderer 的背景雾材质槽。纹理时间由循环组件推进，通过 MaterialPropertyBlock 更新每槽偏移，不修改共享材质，不使用 Shader 全局时间。所有路段共享播放相位，回收路段不会单独归零；不同材质可设置不同速度。速度采用“累计时间 × 材质速度”，Play 中改变 UV Speed 会重新计算相位，可能跳变；建议退出 Play 后配置，它不是平滑变速接口。

道路 Speed=0 时 UV 仍播放。背景 Pause、禁用循环组件、禁用背景根节点以及 Time.timeScale=0 均冻结纹理；Resume 从原相位继续；循环组件 Reset、控制器完整 Reset 和关卡重开均恢复基础材质 Offset。关卡结束的 Pause 同时停止纹理。单独禁用镜头控制器仍允许道路和纹理继续。

使用 Repeat 是必要条件：运行时以完整周期取模避免长时间浮点精度下降，不适合 Clamp/Mirror。每个材质槽由本系统维护 UV 偏移；保留读取到的其它属性，但不要同时用其它脚本驱动同一网格的属性块，尤其 Renderer 级动画属性可能被每槽覆盖遮蔽。运行中不支持替换材质、增删网格或改变 Layer；修改后退出并重新进入 Play 以重新收集。范围只覆盖循环组件下的背景网格，外部远景和 SpriteRenderer 不自动滚动。

验收：Y=0.1 可见持续流动，负值反向，两个材质速度不同仍各自运动；Speed=0 时纹理继续；暂停、恢复、Cue 过渡、关卡结束和重开均按上述规则工作；保持初始材质 Offset，重开第一帧恢复同一图案位置；雾效、玩家和弹幕不变。材质参数在 Play 中修改会持久保存。需在 Unity 确认 Shader 无编译错误且渲染正常。

## 背景切换与淡入淡出

BackgroundDefinition 的“覆盖背景底色”默认关闭：换景保留当前相机底色和清屏模式，旧配置中保存的 Clear Color 不会自动应用。需要改变底色时显式开启，换景将应用该颜色并设为 Solid Color。Inspector 提示与材质雾色的差异，不自动修改材质。完整重置仍恢复启动时的底色与清屏模式。修改配置后需重新触发换景。

已验收的 Prefab 生成工具可用于制作换景布景：每个换景 Prefab 根节点必须包含唯一且启用的 `LoopingBackgroundStrip`，至少两个路段，所有物体使用 Background3D 层；不能包含相机、灯光、其它脚本、粒子或碰撞体。Project 中右键 `Create > STG > Background > Definition` 创建配置，指定 Content Prefab、镜头位置/角度、FOV、滚动速度和相机底色。

在 `StageBackgroundController` Inspector 的 Play 模式中拖入 `Next Background`，填写 Fade Out/Fade In 秒数，点击 Switch Background。背景相机先淡到黑色，再实例化新布景、应用镜头/FOV/速度和底色，最后淡入；遮罩由背景相机绘制，战斗相机随后绘制，因此玩家、弹幕和 UI 不会被遮住。淡出和淡入各为非负秒数，设为 0 可立即完成对应阶段；建议先使用 1 秒。

切换时新布景为运行时实例，不写回 Prefab 或场景。旧初始布景暂时隐藏，旧的临时布景在下一次换景或重置时销毁；Reset Entire Background 恢复初始布景、镜头、FOV、速度、相机底色及路段状态。Cancel Playback 会终止遮罩并保留已切换的布景/镜头，不回滚到旧场景。暂停时切换计时停止，Time.timeScale=0 同样停止；切换中的新/旧播放仍遵循当前独立句柄规则。

配置资产被修改时保留字段快照；运行时不替换材质或修改共享 Prefab。换入布景的 LoopingBackgroundStrip 会自行收集 UV 播放状态；换景前确保其材质、层和循环布局已在 Prefab 内配置。换景 Prefab 不应包含 `StageBackgroundController`、背景相机或过渡 Canvas。工具会拒绝层级、组件和布局不符合要求的配置。

验收：玩家/弹幕/UI 在淡出、全黑、淡入全程可见；切换后新道路循环、UV 滚动、雾效和 Cue 可用；暂停/恢复保持进度；取消不留下遮罩；完整重置回到初始布景；重复切换不增长场景对象；关卡重开恢复初始背景。请在 Play 后观察 Hierarchy 的运行时实例，退出 Play 后确认 Prefab 和场景无意外持久化。当前仅支持单背景相机和单遮罩，不支持两套背景交叉溶解、透明云层；关卡与 Boss 自动换景配置见下文。

源码：[BackgroundDefinition.cs](./BackgroundDefinition.cs)、[StageBackgroundController.Transition.cs](./StageBackgroundController.Transition.cs)。


### 在 Prefab 编辑模式替换路段

双击 Project 中的完整背景 Prefab，在 Prefab Mode 的 Hierarchy 选中包含 LoopingBackgroundStrip 的物体，打开 Build Loop From Prefab。窗口已打开时点击 Use Selected Strip 更新目标，再选择路段并生成。保存 Prefab 后返回场景；已有背景实例及 BackgroundDefinition 引用继续使用同一个资产。

工具修改当前 Prefab Stage 的内容并标记 dirty，不直接覆盖 Project 资产。Auto Save 开启时由 Unity 自动保存；建议关闭 Auto Save 后测试生成、一次 Undo、Redo，再保存并重新打开确认路段数量、布局和嵌套 Prefab 连接。路段源 Prefab 不会被修改。工具禁止修改其它场景残留目标，并拒绝当前背景自身或依赖当前背景的路段，避免循环 Prefab 引用。

## 关卡与 Boss 自动换景

保持当前 LevelBackgroundBinding 配置。关卡 Entries 添加 `背景/Switch Background`，指定 BackgroundDefinition、TriggerTime、FadeOut、FadeIn；Duration 保持 0。条目始终只触发一次，不等待、不阻塞刷怪；相同时间按条目数组顺序执行，后发起的换景/Cue 接管前者。

BossEncounter 的 StartActions 或阶段 EnterActions 中添加 `Game Action/Switch Background`，指定背景及淡出淡入时间，开启外层 WaitForCompletion 才会在淡入完成后继续阶段战斗。BlockTimeline 可开启，已启动换景仍随背景缩放时间推进。动作本身按实际句柄等待，不用淡出加淡入时长猜测；换景保留全遮罩帧，时长为 0 也非同步完成。

缺绑定、无效配置、外部接管或取消将使动作序列记录 Failure 并终止后续动作；Encounter 按原规则将失败视为等待结束，不自动终止整场遭遇。Dispose 只取消自身句柄，不能取消新播放。取消后遮罩由控制器下次 Update 清除；关卡结束或重开直接调用统一取消/重置。编辑模式预览跳过，Play 中预览 Runtime 被拒绝。旧场景/资产无需迁移，新功能需显式添加条目或动作。

验收：第 5 秒自动换景且敌人继续生成；Boss 等淡入完成后开战；换景中暂停/恢复、重开、结束均正常；Cue 接管后旧动作失败但新播放继续；重复重开不重复订阅。测试无效配置时 Console 记录失败属于预期。背景底色覆盖开关仍默认关闭。

源码：[SwitchBackgroundEntry.cs](../Level/SpawnEntries/SwitchBackgroundEntry.cs)、[SwitchBackgroundAction.cs](../GameActions/SwitchBackgroundAction.cs)。

