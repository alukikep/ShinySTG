# HUD 编辑

通过 **STG > UI > HUD Layout Editor** 打开窗口，选中 GameplayHud。
“编辑 UI”可拖入 Sidebar、Row、标签或数值文本。拖紫色框移动，拖白色手柄缩放，
也可输入位置、尺寸；滚轮缩放，中键平移。“适应窗口”只影响编辑窗口的显示。
数值以 LayoutRoot 的实际 UI 单位为准，左上角为原点。编辑支持 Undo 和 prefab override。

窗口只修改所选 UI 的 RectTransform，保留其锚点，不修改相机、玩家范围、游戏逻辑或对话。
HUD 运行时只通过 Presenter 读取并显示玩家数值；布局组件仅保存编辑引用，无 Update 或启动重排。
旧 Playfield 与四边遮罩已在预制体中停用，保留对象 ID 以免破坏已有引用。

CanvasScaler 是唯一的缩放配置入口，LayoutRoot 默认拉伸填满 Canvas。
位置和响应分辨率变化的方式由原生 RectTransform 锚点决定，不再额外拟合参考矩形。
比较编辑和 Play 时使用 Game 窗口相同分辨率、宽高比，关闭 Maximize On Play 避免窗口比例切换。
若希望面板贴右侧，请在 RectTransform 设置右侧锚点；窗口移动会保留该锚点。
窗口显示矩形示意，最终美术效果以 Game 窗口为准。

新场景执行 **STG > UI > Create Gameplay HUD**，无需选择相机，菜单仅实例化 UI。
美化请编辑 Sidebar 下的 ScoreRow、LivesRow、BombRow、PowerRow、GrazeRow。
中文标签需要支持中文的 TMP 字体。图标槽增减后同步更新 GameplayHudView 的引用。

进入 Play 后分数文本长度和残机图标状态可能因真实数值改变，但布局脚本不会重新定位。
自定义复制的旧 HUD 可停用其旧遮罩，并将 LayoutRoot 用原生锚点拉伸填满 Canvas；
已有场景或 prefab override 仍由 Unity 保留，编辑器不会自动覆盖你的手动配置。
