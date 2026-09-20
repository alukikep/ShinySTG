# 首版开局流程

Unity 2022.3：保存当前场景，运行 `STG > Game Flow > Setup First Playable`。
工具从已保存的 [SampleScene](../../Scenes/SampleScene.unity) 复制正式游戏场景，提取玩家 prefab，创建角色与首关资产，接入现有开始菜单，并在 Build Settings 启用两个场景。原测试场景不修改。

打开 [StartMenu](../../Scenes/StartMenu.unity) 进入 Play，松键后按 Z / Enter 进入角色页，再次确认开始游戏。方向键移动、Z 射击、Shift 低速。没有立绘时仍使用文字占位，不影响开局。

首次生成的内容：

- `Assets/Scenes/Gameplay.unity`：复制测试场景的相机、背景、对象池、HUD 等，移除预放玩家和敌人；敌人由关卡时间轴生成。
- `Assets/Prefabs/Player/NatsuhaA.prefab`：沿用测试场景的机体、主弹和子机配置。
- `Assets/SO/GameFlow/NatsuhaA.asset` 与 `FirstStage.asset`：角色与入口配置。

在角色资产中调整展示信息与玩家 prefab；在首关资产中选择 `LevelDefinition`、出生位置及场景路径。初始背景沿用游戏场景，后续背景演出、音乐、刷怪继续通过现有关卡配置驱动。第一版不增加难度、结算或连续关卡。

`GameplayBootstrap` 关闭关卡自动开始，读取本次请求后生成玩家；初始化期间暂停游戏时间并持有玩家控制锁，等待玩家 Start 与 HUD 绑定后揭幕，再开始关卡。直接打开 Gameplay 时使用 Bootstrap 的默认配置。启动参数只引用资产，不保留旧场景对象。

工具不会覆盖已有角色、首关资产或正式游戏场景；重复执行会补齐菜单首关引用并启用 Build Settings 场景。已有角色列表只要配置过任一角色资产，就保留其内容。场景组件编辑使用 Undo 并标记 dirty，工具明确保存其目标场景；新文件生成与 Build Settings 修改不属于场景 Undo，需通过版本控制管理。配置异常时保留 dirty 场景供检查，不自动丢弃编辑。

加载前的配置错误显示在人物简介区域；加载后的初始化错误显示返回标题按钮，也可按 Esc。目标场景必须包含唯一且启用的 Bootstrap、LevelController，以及 BulletPool 和 CollisionService。不要预放第二个玩家。重试完整游戏应重新加载场景，`LevelController.ReloadLevel()` 仅是关卡时间轴重置。

验证：从标题进入游戏、直接运行游戏场景、长按确认不重复加载、出生无敌不在加载期间消耗、HUD 绑定新玩家、时间轴和背景/音乐正常启动；缺少 prefab / 场景 / Bootstrap 时能看到错误并恢复。Unity Test Runner 的 EditMode 中可运行 `GameFlowTests`，覆盖无效开局参数和音频跨控制器重绑。
