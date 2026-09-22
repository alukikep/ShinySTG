# 背景系统

背景与战斗分别由透视相机和固定正交相机绘制，通过 Background3D 层隔离。背景布景及其所有子物体必须位于 Background3D 层，否则会脱离背景相机的透视渲染，出现物体像贴在相机上、远近关系不一致或不可见的现象。背景镜头和布景不能修改战斗坐标、BoundsService 或判定。当前支持内置渲染管线的双相机叠加、静态路段循环、Cue 过渡、背景专属材质效果、Prefab 换景与关卡事件绑定。

## 职责与控制权

- LoopingBackgroundStrip 只推进静态布景，不移动相机。循环使用固定路段；增加粒子、动画等可变状态前，必须扩展复用重置契约。
- BackgroundCue 是共享配置，StageBackgroundController 播放时复制目标参数与曲线。计时、起点和播放句柄属于运行时实例，不能写回资产。
- Controller 统一控制背景镜头和滚动速度。新播放从当前状态接管，旧播放进入取消终态；其它动画不应同时写入这些参数。
- BackgroundPlaybackHandle 独立记录播放结果。IsComplete 包含成功、取消和失败；取消旧句柄不能影响后续播放。无效播放请求返回失败句柄，不替换有效播放。

## 时间与生命周期

关卡时间决定何时触发 Cue；背景使用独立的缩放时间推进。因此 Boss 阻塞刷怪时间轴时，已经启动的背景过渡仍继续。玩家输入锁不等于暂停，Time.timeScale 为零或背景暂停才停止推进。

暂停保留进度；取消保留当前镜头和速度；完成保持目标状态。Controller 重置恢复首次有效初始化记录的镜头、速度、暂停状态，并复位路段排列。循环组件的重置仅处理路段，两者不可混用。

禁用 Controller 取消当前播放，但不停止独立的循环组件；禁用整个背景根节点才会同时停止滚动。引用失效使正在播放的句柄失败。运行时引用保持稳定，不支持中途改绑后继续沿用原快照。

## 换景生命周期

BackgroundDefinition 描述完整循环布景 Prefab 与初始镜头。StageBackgroundController.SwitchBackground 使用背景相机独立遮罩执行淡出、替换运行时实例、应用新镜头和淡入；战斗相机随后绘制，因此换景遮罩不影响战斗画面。取消保留已切换状态，重置销毁临时实例并恢复初始背景。配置和实例不写回共享 Prefab，Prefab 必须符合静态几何、Background3D 层和唯一循环组件约束。

## 关卡接入

PlayBackgroundCueEntry 是一次性时间点指令，经 LevelController.RequestBackgroundCue 广播，由同物体上的 LevelBackgroundBinding 转发给显式绑定的背景。请求校验真实 Runtime，编辑器预览、旧 Runtime 和已结束关卡不会触发背景。

绑定订阅关卡开始、完成和 Cue 请求：开始或重开时完整重置，结束时取消播放并暂停。禁用时退订并停止背景；重新启用按关卡当前状态同步，不补播错过的条目。一个背景只由一个关卡绑定驱动。未配置背景的旧关卡不要求迁移。

扩展新的时间点背景指令时，沿用 SpawnEntry、Controller 事件及绑定转发，运行状态不能存入条目配置。PlayBackgroundCueAction 复用绑定与独立句柄，按实际完成状态等待，取消时只清理本次播放。Encounter 将真实关卡 Runtime 传入 GameActionContext，贯穿开场、阶段、击破与收尾，避免预览或旧上下文操作真实背景。缺绑定、播放失败及外部取消通过 Runner 终止本组动作并记录 Failure；Encounter 将失败视为等待结束，不自动中止遭遇。

## 与其他板块的关系

- [level](./arch-level.md)：负责时间点触发与关卡生命周期，不逐帧驱动背景动画。
- [game-actions](./arch-game-actions.md)：背景动作沿用可等待、可取消的契约，外层 WaitForCompletion 控制宿主等待。
- [操作说明](../../Assets/Scripts/Background/README.md)：场景搭建、Cue 配置、关卡绑定与验收步骤。

## 循环镜头序列

BackgroundLoopCue 保存有序姿态节点，StageBackgroundController.PlayLoop 深复制节点与曲线后接管播放。首次进入节点 0 使用独立进入时间与曲线，随后依次停留、过渡至下一节点，末尾回到节点 0；各节点的过渡时间属于目标节点。循环使用取模时间定位，每帧最多扫描一轮，避免短节点和长帧导致无界追帧。单节点进入后保持姿态；空列表、非法参数或整轮总时长为零的请求失败且不替换已有播放。

循环与 Cue、换景共享唯一播放句柄和控制权，遵循相同暂停、取消、重置与引用失效规则。循环不自然完成，被接管时旧句柄取消，新请求从实际镜头状态继续。循环目标滚动速度在首次进入期间应用，随后保持。

PlayBackgroundLoopEntry 经关卡请求事件和 LevelBackgroundBinding 启动循环。StartBackgroundLoopAction 是启动命令，成功后立即完成，背景控制器继续持有播放；动作 Dispose 不取消已交付的循环，后续镜头请求或关卡生命周期负责结束它。启动失败仍走 GameActionRunner 的失败路径；持续播放期间的失败由背景句柄与控制器日志报告，不回传已完成动作。
