# 符卡宣言

符卡宣言使用独立 UI 显示立绘与名称，由现有 GameActions / BossEncounter 控制等待和取消。
不暂停整个游戏、不自动消弹，也不自动锁定玩家操作。动画使用游戏时间，Time.timeScale 为 0 时暂停。

场景只需放一个宣言 prefab，所有 Boss 阶段共用这套视图。每张符卡创建自己的
SpellDeclarationDefinition 配置，在对应阶段的播放动作中引用它，即可切换名称、立绘、音效和动画参数。
不需要为每张符卡复制场景对象，也不需要将宣言 prefab 挂到 Boss 上。

## 配置

1. 在战斗场景执行 `STG > UI > Create Spell Declaration`，创建带服务的宣言 prefab 实例，然后保存场景。
   也可以将 `Assets/Prefabs/UI/SpellDeclaration.prefab` 直接拖进 Hierarchy；两种方式选一种即可。
   prefab 自带 Canvas、视图和服务。仅在 Project 中创建 prefab 或配置资产，不会让场景拥有播放器。
   多场景同时加载时也应只启用一个服务。菜单不会重复创建当前场景已有的服务，场景创建支持 Undo。
2. 使用 `Create > STG > Spell Declaration` 创建配置，填写符卡名称、立绘和非循环短音效。
   可先复制 [示例配置](../../../SO/Presentation/SpellDeclarationSample.asset)。示例只有英文横幅，立绘与音效留空。
3. 在 BossEncounter 的 `PhasePresentations` 中找到对应 `PhaseIndex`，给 `EnterActions` 添加
   `Game Action/Play Spell Declaration`，在 Declaration 中引用该符卡配置，保持外层 `WaitForCompletion` 开启。
   其他符卡阶段引用各自的配置；播放完成后视图清理本次内容，下次播放读取新的配置。
4. 需要演出期间无敌时，用 `Invincibility Scope` 包裹宣言动作并勾选 `ProtectOwner`；玩家保护按需开启。
   消弹通过现有 `Execute Commands` 配置。无敌作用域完成、失败或取消后释放自己的锁。

也可以选中 Encounter 资产，执行 `Assets > STG > Add Sample Spell Declaration to First Phase`。
这会给索引 0 的空进入动作添加 Boss 无敌作用域和示例宣言，支持 Undo，标记资产 dirty 后由用户保存。
已有进入动作时不会覆盖。使用前需完成 Encounter 的 Boss prefab 和阶段配置。

示例资产缺失时执行 `STG > UI > Create Spell Declaration Sample Assets`，由 Unity 生成缺失的
prefab 和示例配置，保留已有资产。这个菜单只创建 Project 资产；仍需按第 1 步把视图加入战斗场景。

## 视觉调整

[宣言 prefab](../../../Prefabs/UI/SpellDeclaration.prefab) 使用 1200 × 900 参考分辨率的 Overlay Canvas。
调整 Portrait 与 Banner 的 RectTransform 可改变最终停留位置；服务只改变二者的水平偏移和整体透明度。
可以在 prefab 模式暂时将 CanvasGroup Alpha 设为 1、填入预览文字和立绘检查布局，播放时会重置。
调整 Canvas 排序以适配项目其他 UI。宣言不接收鼠标射线。

默认字体与现有 HUD 一致；显示中文或日文符卡名时，应为 Title 指定包含相应字符的 TMP 字体或 fallback。
立绘可以留空，视图仍需保留 Portrait Image 引用。没有随代码提供角色美术或音频资源。
宣言名称来自 Declaration 配置，不读取 PhasePresentation.DisplayName。
不同配置共用相同布局与滑入/淡出形式；完全不同的布局或动画形式需要扩展视图与播放逻辑。

## 场景接入排查

- 报“缺少启用的宣言服务”：检查 Hierarchy 是否有宣言 prefab 实例，以及服务组件、所在对象和父对象是否启用。
  只添加独立的 SpellDeclarationService 组件不够，它还需要绑定完整视图。
- 报“存在多个启用的宣言服务”：检查已加载的所有场景，移除重复实例或手动添加的多余服务。
- 报视图不可用：按日志指出的具体对象检查 Service 的 View 引用，以及 View 的 Root、Title、Banner、Portrait 引用。
  使用完整 prefab 实例会自带这些绑定；Title 文本组件和相关子对象必须启用。
- 未提供立绘时，可以让 Portrait 的 Sprite 留空。Portrait 的 Image 组件关闭是正常状态，
  播放时会按配置启用；不要禁用 Portrait 整个 GameObject。
- 未播放时 CanvasGroup Alpha 为 0 是正常状态，服务会在播放时驱动透明度。

在非 Play 模式下完成接入并保存场景，避免退出运行后丢失临时创建的对象。

## 生命周期

同一服务同时只接受一个播放请求；重复请求失败，不打断正在播放的宣言。
每次 Play 返回独立句柄，可检查 IsComplete / IsCancelled / Failure，并通过 Cancel 取消自己的播放。
缺服务、多个服务、缺视图、无效时长和循环音效会让动作失败；Encounter 沿用原有逻辑，把失败句柄视为结束后继续推进。
阶段退出、Boss 死亡或遭遇释放会取消其持有的动作；服务禁用和场景销毁也会清理宣言。
短音效自然结束，不使用 StopSfx 停止其他调用者播放的同一 Cue。

运行 Unity Test Runner 的 EditMode 测试 `SpellDeclarationTests` 检查播放完成、重复调用隔离、取消、视图失效、
无敌释放以及 prefab 引用。再在 Play Mode 验证阶段等待、死亡/重开、暂停和不同分辨率下的视觉效果。

动作组合见 [GameActions 操作说明](../../GameActions/README.md)。
