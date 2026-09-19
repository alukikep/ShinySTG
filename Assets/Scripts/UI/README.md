# HUD 配置与编辑

当前显示分数、备用残机（不含本体）、Bomb、Power、Graze，以及 Boss 当前管血量和后续血管数量。
架构职责与扩展契约见 [HUD 架构](../../../docs/architecture/arch-hud.md)。

## 编辑布局

通过 **STG > UI > HUD Layout Editor** 打开窗口，选中 GameplayHud。
“编辑 UI”可拖入 Sidebar、Row、标签或数值文本。拖紫色框移动，拖白色手柄缩放，
也可输入位置、尺寸；滚轮缩放，中键平移。“适应窗口”只影响编辑窗口的显示。
数值以 LayoutRoot 的实际 UI 单位为准，左上角为原点。编辑支持 Undo 和 prefab override。

窗口只修改所选 UI 的 RectTransform，保留其锚点，不修改相机、玩家范围、游戏逻辑或对话。
HUD 运行时通过各自的 Presenter 读取玩家和 Boss 状态；布局组件仅保存编辑引用，无 Update 或启动重排。


CanvasScaler 是唯一的缩放配置入口，LayoutRoot 默认拉伸填满 Canvas。
位置和响应分辨率变化的方式由原生 RectTransform 锚点决定，不再额外拟合参考矩形。
比较编辑和 Play 时使用 Game 窗口相同分辨率、宽高比，关闭 Maximize On Play 避免窗口比例切换。
若希望面板贴右侧，请在 RectTransform 设置右侧锚点；窗口移动会保留该锚点。
窗口显示矩形示意，最终美术效果以 Game 窗口为准。

新场景执行 **STG > UI > Create Gameplay HUD**，无需选择相机。菜单会一次实例化玩家 HUD 和 Boss HUD：
BossHud 位于 Canvas 顶部，包含背景、填充 Image、数量 TMP 文本，并自动连接 `BossHudView` 的引用。
如果场景已有 Gameplay HUD，菜单仍会跳过重复创建；需要重新生成时先删除旧 HUD。
美化请编辑 Sidebar 下的 ScoreRow、LivesRow、BombRow、PowerRow、GrazeRow。
中文标签需要支持中文的 TMP 字体。图标槽增减后同步更新 GameplayHudView 的引用。

进入 Play 后分数文本长度和残机图标状态可能因真实数值改变，但布局脚本不会重新定位。
已有场景或 prefab override 由 Unity 保留，编辑器不会自动覆盖手动配置。

## 接入与扩展

GameplayHudPresenter 可指定玩家，留空时跟随 Player.Instance。HUD 或玩家重新启用后会刷新显示，
玩家不可用时显示占位。修改布局无需修改玩家组件，新增数据来源通过 Presenter 绑定。

编辑预览数值、统一显示开关、玩家数值变化动画、最高分存档仍是后续功能，不属于当前编辑窗口。
新增显示时先确认游戏系统已提供真实数据；美术效果与显示格式留在 View 或 UI 资产中。

## Boss 血条接入

通过上述生成菜单创建时，Boss HUD 已配置完成。直接拖入原始 GameplayHud prefab 不会执行菜单的补建逻辑。
已有 HUD 不会自动升级，可保留原布局并按以下步骤手动接入：
在现有 Canvas 的游戏区域顶部新建独立的 `BossHud` UI 节点，添加 `BossHudPresenter`；
Unity 会自动补上 `BossHudView` 和 `CanvasGroup`。将血条和数量文本放在该节点的子层级。

1. 创建背景 Image 和填充 Image。填充图必须指定有效 Sprite（生成菜单使用 Unity 内置 UISprite），Type 设为 Filled，
   Fill Method 设为 Horizontal，Origin 设为 Left；拖到 View 的 Fill。
2. 创建 TMP 文本，拖到 View 的 Bar Count。中文显示需要支持中文的 TMP 字体。
3. Presenter 的 Boss 留空时自动绑定存活 Boss，也可指定场景中的 Boss。
   同时存在多个 Boss 时保持当前目标，目标死亡或禁用后才选择下一位。
4. 使用 RectTransform 设置位置和宽度；运行时代码不调整布局。

长条显示当前管比例，“剩余 N 管”只计后续血管，不包含当前管：3 管血依次显示 2 → 1 → 0。
显示 0 表示正在打最后一管，不表示 Boss 已死亡。普通扣血平滑缩短，切管立即显示下一管实际血量，
死亡直接清零隐藏。无目标时 CanvasGroup 隐藏显示，节点保持启用以继续监听出生事件。
HUD 禁用重启后会重新绑定；血量由 BossHealth 管理，HUD 不会在 Boss 重新启用时恢复其血量。
该组件不改变现有 Boss 的销毁或复用流程。

Boss 出生后即显示，包括战前对话期间。数量由血管数据决定，与行为阶段数无关。
生成菜单在场景实例中补建 Boss HUD，不改写原始 GameplayHud prefab，创建操作支持 Undo。
在 Unity Test Runner 的 EditMode 中运行 `BossHudTests` 检查伤害事件、溢出和兼容模式；
Play 模式另检查 UI 中途启用、目标替换、关卡重开及实际布局。

## 布局检查

在相同 Game 分辨率下对比编辑与 Play，检查长分数、残机与 Bomb 超出图标槽时的数字，
以及 HUD 禁用重启和玩家替换后的刷新。编辑操作检查 Undo、场景保存和 prefab override。
