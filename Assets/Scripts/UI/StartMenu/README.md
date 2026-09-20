# 开始菜单

实际开局先按 [首版开局配置](../../GameFlow/README.md) 执行一次配置工具，关联角色 prefab、首关与游戏场景。

打开 [StartMenu 场景](../../../Scenes/StartMenu.unity) 后进入 Play，先松开操作键，再使用上下方向键选择、Z 或 Enter 确认。上下首尾循环，长按连续选择；同时按上下不移动。鼠标不参与菜单操作。

选择开始游戏后，选项先闪烁，黑幕从右向左覆盖屏幕，全黑时切换至角色选择页，再继续向左退出。角色页支持上下键选择，首尾循环和长按重复；切换时更新箭头、角色名称、简介及立绘，按 Z / Enter 确认配置好的角色后进入首关。按 X / Esc 经反方向黑幕返回开始菜单。设置和退出仍只显示占位确认提示。

`FrontEndFlowController` 订阅 `StartMenuController.Confirmed`，协调页面与输入锁；`ScreenWipeTransition` 只负责黑幕。确认事件在按下时发出，按 `StartMenuOption` 区分选项，不根据中文文本判断功能。转场全过程锁定输入，新页面启用后需先松键。

场景的 FrontEnd 根节点承载 CanvasScaler、流程和转场组件；StartMenu 与 CharacterSelectPage 是独立页面，ScreenWipe 是最后绘制的全屏黑幕。保持流程和黑幕在页面外。外层 CanvasScaler 使用 1200 × 900 参考尺寸，Expand 保留完整菜单；原菜单上的 CanvasScaler 已禁用。

Text、Image、RectTransform 可直接调整标题、背景、文字和排版。View 可调整选中颜色与偏移；Input 可调整按键及长按间隔。ScreenWipeTransition 可调整覆盖、全黑停留、揭幕时长，默认 0.3 / 0.05 / 0.3 秒；动画不受 Time.timeScale 影响。菜单独立于游戏 HUD；首版配置工具会注册开始场景与游戏场景。

需要在其他场景创建完整菜单流程时使用 `STG > UI > Create Start Menu`。工具支持 Undo，创建后标记场景 dirty；已有菜单时只选中原菜单，不重复生成或自动升级旧菜单。保存场景由用户完成。

中文使用随项目附带的 [Noto Sans CJK SC](../../../Fonts/StartMenu/NotoSansCJKsc-Regular.otf)，许可证见 [SIL OFL](../../../Fonts/StartMenu/LICENSE.txt)。使用 uGUI Text 的动态字体，无需额外生成 TMP 字库。

验收：默认选中开始游戏；首尾循环；长按重复；长按确认只触发一次；转场中连续按键不重复跳转；全黑时切换页面；X / Esc 返回后可继续操作；失焦/恢复和 FrontEnd 禁用/启用不会误触或残留黑幕；Time.timeScale 为 0 时仍能操作。检查 4:3、16:9 和转场中窗口缩放下黑幕完整覆盖、文字无裁切。配置首关后，还需验证角色确认、场景切换、HUD、刷怪与音乐。

在 CharacterSelectPage 的 CharacterSelectView 中编辑 Characters 数组以配置角色名称、简介、立绘。未关联角色资产的条目仅用于展示，不能开局；每次进入页面默认选择第一项。未配置立绘时显示占位文字；空列表显示暂无角色。列表最多显示当前项附近的三项。角色输入的长按延迟和间隔在 FrontEndFlowController 中调整，X / Esc 优先返回。

