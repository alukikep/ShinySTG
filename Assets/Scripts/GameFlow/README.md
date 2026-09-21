# 开局、关卡推进与整局结果

Unity 2022.3：保存当前场景，运行 `STG > Game Flow > Setup First Playable`。
工具从已保存的 [SampleScene](../../Scenes/SampleScene.unity) 复制正式游戏场景，提取玩家 prefab，创建角色与首关资产，接入现有开始菜单，并在 Build Settings 启用两个场景。原测试场景不修改。

打开 [StartMenu](../../Scenes/StartMenu.unity) 进入 Play，松键后按 Z / Enter 进入角色页，再次确认开始游戏。方向键移动、Z 射击、Shift 低速。没有立绘时仍使用文字占位，不影响开局。

首次生成的内容：

- `Assets/Scenes/Gameplay.unity`：复制测试场景的相机、背景、对象池、HUD 等，移除预放玩家和敌人；敌人由关卡时间轴生成。
- `Assets/Prefabs/Player/NatsuhaA.prefab`：沿用测试场景的机体、主弹和子机配置。
- `Assets/SO/GameFlow/NatsuhaA.asset` 与 `FirstStage.asset`：角色与入口配置。

在角色资产中调整展示信息与玩家 prefab；在首关资产中选择 `LevelDefinition`、出生位置及场景路径。初始背景沿用游戏场景，后续背景演出、音乐、刷怪继续通过现有关卡配置驱动。

## 关卡序列入口

通过 `Create > STG > Stage Sequence` 创建序列资产，按游玩顺序拖入 Stage 资产。
在开始菜单的 FrontEndFlowController 上配置 Stage Sequence；直接打开 Gameplay 测试时，
在 GameplayBootstrap 上配置 Default Sequence。序列优先，留空继续使用原 First Stage / Default Stage。
已经指定但内容无效的序列会报错，不会静默退回单关。

开局会检查所有关卡的时间轴、出生位置和场景路径，当前要求整个序列使用同一个 Gameplay 场景。
GameStartRequest 固定本局关卡列表顺序，Stage 仍表示首关以兼容原调用方。
GameFlowController.CurrentSession 暴露本局进度和只读结果集合，返回标题时清空。
实时分数仍由 PlayerResources 持有；StageResult 保存本关得分与累计分数、资源快照，Power 使用整数单位保存。

通关后 GameFlowController 等待结算及清场完成，再自动推进下一关。换关沿用同一玩家、当前位置和全部资源；Stage.SpawnPosition 仅在首次生成玩家时使用。渐变期间可移动，战斗限制持续到下一关揭幕完成，时间轴在此之前不推进。
场景内已有启用且有效的 StageFadePrototype / BattleArea 配置时，正式流程复用其局部渐变与机体绘制；未配置时自动使用全屏渐变，无需修改已有场景。下一关通过关卡开始事件重置背景并重新绑定音乐，具体换景仍由时间轴条目控制。
最后一关清场后显示整局结果页，包括总分和各关得分。上下键选择，Z / Enter 确认，Esc 返回标题，也可用鼠标。重新开始本局会使用本次固定的角色和关卡顺序重新加载场景，创建新玩家和 Session；返回标题清空本局状态。结果尚未写入磁盘存档。
GameplayBootstrap 在启动前绑定 StageSettlement，记录分数基线；成功通关才将结果写入 CurrentSession.Results。
整体 Duration 大于零且时间达到上限，或“流程/关卡通关”条目的请求满足结束条件时，为 Cleared；最后一命耗尽时正式流程先等待续关，未接入续关宿主时为 Failed；返回标题和背景调试停止为 Aborted。所有条目播放完本身不会自动通关。结束条目的配置见 [关卡编辑器说明](../../../LEVEL_EDITOR.md#指定关卡结束点)。
显式通关使用 LevelController.EndLevel(LevelEndReason.Cleared)。旧 CompleteLevel() 兼容为中止，不再代表通关。
通关和失败请求下一帧发布，同帧最后一命耗尽优先进入等待续关或失败；退出立即中止待定结果。
OnLevelEnded 携带结束原因，原 OnLevelComplete 继续通知背景等生命周期订阅者，所有原因均会触发。
调试重开会撤销当前关结果并重设分数基线，但不会重置玩家资源或已结算的前序关卡。
延迟结束回调必须保存本轮 AttemptId，通过 TryEndLevel(reason, attemptId) 拒绝过期请求。
结束定案后限制战斗，随后由 LevelController 在 Update 无奖励清场；等待续关尚未定案，不触发结束事件或清场。存活玩家在结束定案后保留移动和低速，重开时恢复战斗。结束通知可能早于实际回收，宿主可检查 IsBattleCleanupPending。
序列资产在游戏进行时只允许调整后续新开局配置，不应修改当前引用的 Stage 资产内容。

局部渐变的配置与手动预览见 [转场说明](StageFadePrototype.md)。单关入口同样会在通关后进入结果页；结果页期间玩家移动被锁定，背景保持结束状态。

关卡结束和结算的权威规则见 [关卡架构](../../../docs/architecture/arch-level.md#结束与结算)。独立测试场景若没有通过 GameplayBootstrap 建立本局 Session，只有 LevelController 的结束与清场，不会自动换关或展示整局结果。

`GameplayBootstrap` 关闭关卡自动开始，读取本次请求后生成玩家；初始化期间暂停游戏时间并持有玩家控制锁，等待玩家 Start 与 HUD 绑定，在黑幕下启动关卡，揭幕后恢复游戏时间和操作。直接打开 Gameplay 时使用 Bootstrap 的默认配置。启动参数只引用资产，不保留旧场景对象。

工具不会覆盖已有角色、首关资产或正式游戏场景；重复执行会补齐菜单首关引用并启用 Build Settings 场景。已有角色列表只要配置过任一角色资产，就保留其内容。场景组件编辑使用 Undo 并标记 dirty，工具明确保存其目标场景；新文件生成与 Build Settings 修改不属于场景 Undo，需通过版本控制管理。配置异常时保留 dirty 场景供检查，不自动丢弃编辑。

加载前的配置错误显示在人物简介区域；加载后的初始化错误显示返回标题按钮，也可按 Esc。目标场景必须包含唯一且启用的 Bootstrap、LevelController，以及 BulletPool 和 CollisionService。不要预放第二个玩家。重试完整游戏应重新加载场景，`LevelController.ReloadLevel()` 仅是关卡时间轴重置。

验证：从标题进入游戏、直接运行游戏场景、长按确认不重复加载、出生无敌不在加载期间消耗、HUD 绑定新玩家、时间轴和背景/音乐正常启动；缺少 prefab / 场景 / Bootstrap 时能看到错误并恢复。Unity Test Runner 的 EditMode 中可运行 `GameFlowTests`，覆盖无效开局参数和音频跨控制器重绑。

连续关卡验收：

- 配置两关，各自设置通关条目；确认首关清场后进入第二关，位置与资源继承，第二关得分从新基线计算。
- 分别验证有效 BattleArea 和未配置局部渐变的场景；揭幕前不能射击，揭幕后时间轴继续，背景和音乐切换符合配置。
- 最后一关与单关入口均显示结果页；重开回到首关并重置资源，返回标题后再次开局无旧成绩或控制锁。
- 通关同帧耗尽残机时先等待续关；确认复活后才允许待定通关结算。尚有残机时等待自动复活完成后继续。

EditMode 的 `RunSessionTests` 覆盖序列校验、连续两关结算与资源继承、重复结算及宿主战斗限制；实际渐变和结果页交互仍需在 Play Mode 验收。

## 暂停与死亡续关

正式开局时 Bootstrap 自动创建暂停菜单，无需重新配置场景或玩家 prefab。游戏中 Esc 打开；上下键循环选择，Z / Enter 确认，Esc / X 继续游戏。加载、换关和结果页不接受暂停。菜单使用与开始菜单共用的键盘导航，打开、确认和失焦后等待松键。

生命耗尽后保留当前关卡与 Boss Encounter，停止关卡和敌人行为，等待死亡演出结束才冻结游戏时间并显示死亡菜单。首项为“获得 2 个残机复活”：增加 3 条总生命（本体加两架备用机），复用重生位置与复活无敌，保留分数、火力、Bomb 和关卡进度。普通掉命仍自动复活。死亡菜单 Esc / X 不关闭，必须选择复活、返回主菜单或从头开始。

从头开始会重新加载本局第一关并重置资源与成绩；返回主菜单中止当前关卡。菜单冻结期间对话、碰撞、弹幕、激光和道具停止推进，BGM 继续。暂停持有独立的控制锁和战斗限制，恢复原时间倍率，保留对话等其他宿主的锁。暂停界面中文使用操作系统字体（优先 Microsoft YaHei / SimHei）；非 Windows 发布需检查字体可用性。

`PauseContinueTests` 覆盖待定通关与续关、原时间轴保留、中止与过期轮次、暂停锁与时间恢复、三条总生命复活以及键盘防误触。Play Mode 还需检查 Boss 战和对话中暂停、长按确认、死亡演出、连续续关、从第二关重开回第一关、返回主菜单后重新开局。
