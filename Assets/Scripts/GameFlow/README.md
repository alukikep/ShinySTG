# 首版开局流程

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

当前阶段已接入单关结算，尚未自动推进下一关或写入磁盘存档。
通关后停留在当前场景，战斗对象清空，存活玩家仍可移动和低速，但不能射击。这是当前流程的预期终点；配置了多关序列也不会自动进入第二关，尚无整局结果页或自动返回标题。
下一阶段需串联通关结算、关间渐变、下一关准备与启动，并在准备完成后恢复战斗；最后一关另接整局结束流程。
GameplayBootstrap 在启动前绑定 StageSettlement，记录分数基线；成功通关才将结果写入 CurrentSession.Results。
整体 Duration 大于零且时间达到上限，或“流程/关卡通关”条目的请求满足结束条件时，为 Cleared；最后一命耗尽为 Failed；返回标题和背景调试停止为 Aborted。所有条目播放完本身不会自动通关。结束条目的配置见 [关卡编辑器说明](../../../LEVEL_EDITOR.md#指定关卡结束点)。
显式通关使用 LevelController.EndLevel(LevelEndReason.Cleared)。旧 CompleteLevel() 兼容为中止，不再代表通关。
通关和失败请求下一帧发布，同帧最后一命耗尽优先判失败；退出立即中止待定结果。
OnLevelEnded 携带结束原因，原 OnLevelComplete 继续通知背景等生命周期订阅者，所有原因均会触发。
调试重开会撤销当前关结果并重设分数基线，但不会重置玩家资源或已结算的前序关卡。
延迟结束回调必须保存本轮 AttemptId，通过 TryEndLevel(reason, attemptId) 拒绝过期请求。
结束定案后限制战斗，随后由 LevelController 在 Update 无奖励清场；失败不暂停死亡演出，尚未增加 Game Over 界面。存活玩家保留移动和低速，重开时恢复战斗。结束通知可能早于实际回收，宿主可检查 IsBattleCleanupPending。
序列资产在游戏进行时只允许调整后续新开局配置，不应修改当前引用的 Stage 资产内容。

关卡结束和结算的权威架构说明位于项目根目录 `docs/architecture/arch-level.md` 的“结束与结算”小节。

`GameplayBootstrap` 关闭关卡自动开始，读取本次请求后生成玩家；初始化期间暂停游戏时间并持有玩家控制锁，等待玩家 Start 与 HUD 绑定后揭幕，再开始关卡。直接打开 Gameplay 时使用 Bootstrap 的默认配置。启动参数只引用资产，不保留旧场景对象。

工具不会覆盖已有角色、首关资产或正式游戏场景；重复执行会补齐菜单首关引用并启用 Build Settings 场景。已有角色列表只要配置过任一角色资产，就保留其内容。场景组件编辑使用 Undo 并标记 dirty，工具明确保存其目标场景；新文件生成与 Build Settings 修改不属于场景 Undo，需通过版本控制管理。配置异常时保留 dirty 场景供检查，不自动丢弃编辑。

加载前的配置错误显示在人物简介区域；加载后的初始化错误显示返回标题按钮，也可按 Esc。目标场景必须包含唯一且启用的 Bootstrap、LevelController，以及 BulletPool 和 CollisionService。不要预放第二个玩家。重试完整游戏应重新加载场景，`LevelController.ReloadLevel()` 仅是关卡时间轴重置。

验证：从标题进入游戏、直接运行游戏场景、长按确认不重复加载、出生无敌不在加载期间消耗、HUD 绑定新玩家、时间轴和背景/音乐正常启动；缺少 prefab / 场景 / Bootstrap 时能看到错误并恢复。Unity Test Runner 的 EditMode 中可运行 `GameFlowTests`，覆盖无效开局参数和音频跨控制器重绑。
