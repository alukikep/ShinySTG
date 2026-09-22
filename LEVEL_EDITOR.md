# ShinySTG 关卡编辑器使用说明

本文只讲如何创建、编辑和运行关卡。编辑器扩展见
[`Assets/Scripts/Level/Editor/README.md`](./Assets/Scripts/Level/Editor/README.md)，系统边界见
[`arch-level.md`](./docs/architecture/arch-level.md) 和
[`arch-level-editor.md`](./docs/architecture/arch-level-editor.md)。

## 快速上手

1. 在 Project 窗口选择 `Create -> STG -> Level`，创建 `LevelDefinition`。
2. 打开 `STG -> Level Editor`，载入关卡资产。
3. 用 `+ Add` 添加条目，设置触发时间、Prefab 和出生位置。
4. 场景中新建 `LevelController`，把关卡资产拖到 `Definition`。
5. 进入 Play Mode。`AutoStart` 勾选时关卡自动开始，否则由外部代码调用 `BeginLevel()`。

## 编辑器界面

| 区域 | 用途 |
|---|---|
| Toolbar | 打开资产、新增、复制、删除和音频绑定 |
| 列表 | 浏览和选择全部条目 |
| 时间轴 | 查看触发顺序、拖动时间和持续时间 |
| 详情 | 编辑当前条目的 Inspector 字段 |
| Preview | 在 Editor 中预览触发时序 |
| Scene Gizmo | 查看出生位置和选中项 |

选中条目后可使用 Delete/Backspace 删除；复制、删除和 Scene 聚焦也可从右键菜单执行。
写操作支持 Unity Undo。

## 创建与运行关卡

### LevelDefinition

常用配置：

- `Entries`：按触发时间执行的关卡条目。
- `Pool`：可选的关卡专用 `BulletPool`；留空时由运行时按配置查找。
- `AudioBinding`：关卡音乐绑定。
- `AutoSwitchBgm`：开始关卡时是否自动绑定音乐事件。

字段含义和默认值以当前 Inspector Tooltip 为准。

### LevelController

- `Definition` 指向要运行的关卡。
- `AutoStart=true` 时进入 Play Mode 自动调用 `BeginLevel()`。
- 重新运行当前定义使用已有的 reload 接口，不要在外部重复创建 Runtime。

关卡结束条件和事件由 `LevelController`/`LevelRuntime` 提供；订阅前先确认当前代码中的事件签名。

## 关卡条目

`+ Add` 菜单会列出当前可用的 `SpawnEntry` 子类。常见选择原则：

| 需求 | 条目类型 |
|---|---|
| 出现单个敌人 | Simple/单体出生条目 |
| 同时或按间隔出现一组敌人 | Wave/波次条目 |
| Boss 战 | Boss Encounter |
| 指定时刻播放音效 | Play SFX |
| 占据一段时间的逻辑 | Sustain/持续型条目 |

菜单名称以 `[SRName]` 和当前 Inspector 显示为准；新增类型后无需把完整清单复制到本文。

### Simple 与 Wave

- 单个特殊敌人、独立位置和行为使用 Simple。
- 同构敌群、规则间距或统一波次配置使用 Wave。
- 如果每个敌人的配置差异很大，多个 Simple 通常比一个复杂 Wave 更清晰。

### Boss Encounter

选择 Encounter SO，在条目中配置出生位置、旋转和 BlockTimeline。Boss prefab、血管、阶段、Signals 及演出统一在 Encounter SO 中编辑；每个阶段自带进退场动作，重排时一起移动。操作见 [Boss 配置说明](./Assets/Scripts/Enemy/Boss/README.md)，规则见
[`arch-boss.md`](./docs/architecture/arch-boss.md)。

进入 Boss 战后，关卡事件可驱动音乐切换；Boss 击败后继续执行后续条目还是结束关卡，
以当前关卡定义和 Controller 配置为准。

## 持续型条目与时间轴

### 指定关卡结束点

通过 `+ Add → 流程/关卡通关` 添加结束标记，设置 `TriggerTime`。触发后停止后续条目，等待此前已启动的阻塞式 Boss Encounter（包括收尾）完成，再请求成功通关并走结算和清场。它不等待普通敌人全灭；条目自身的 Duration 和 OneShot 不影响通关语义。

将它放在最后，Boss 条目应位于它之前；同时间条目按列表顺序执行。建议在 Project 选中 LevelDefinition 资产，将整体 `Duration` 设为 `0`，避免定时通关提前结束。所有条目播放完本身仍不代表通关。编辑器 Preview 只停止自己的时间轴，不触发真实关卡结算。

通过 GameplayBootstrap 启动的正式流程会在通关结算及清场后自动转场进入下一关，末关显示整局结果页，支持重开本局或返回标题。换关保留玩家位置和资源，揭幕后恢复战斗。只有 LevelController 的独立测试场景仍在清场后停留，不驱动整局流程。序列配置见 [开局流程](./Assets/Scripts/GameFlow/README.md#关卡序列入口)。

### 持续区间操作

带 `Duration` 的条目在时间轴显示为区间，瞬时条目显示为固定宽度标记。

- 拖动区间主体改变开始时间。
- 若当前条目支持调整持续时间，可拖动区间边缘。
- 重叠区间会分配到不同 lane，避免视觉覆盖。
- 完全位于当前视口外的条目不会堆在视口边缘。

持续型条目的实际运行语义由其类型决定，时间轴宽度只负责表达时间范围。

## 音频集成

### 关卡自动切歌

点击工具栏 `+ Create AudioBinding` 可在关卡资产旁创建同名绑定并自动建立引用。填写：

| 字段 | 时机 |
|---|---|
| `Playlist` | 关卡开始 |
| `BossMusic` | Boss 出现 |
| `DefeatMusic` | Boss 击败 |

当 `LevelDefinition.AutoSwitchBgm=true` 且场景有 `AudioSystem` 时，`BeginLevel()` 会自动绑定，
不需要手动调用 `EnableAutoSwitch()`。

### 时间点音效

在时间轴添加 `Entry/Play SFX`，设置 `TriggerTime` 和 `SfxCue`。它适合剧情提示、警报或
关卡节奏音；敌人受击和开火等对象事件应配置在对应组件或 `FirePattern` 上。

音频资产配置见 [`Assets/Scripts/Audio/README.md`](./Assets/Scripts/Audio/README.md)。

## 配置校验

编辑器状态栏会显示当前关卡的校验结果：

- `Error`：通常表示条目无法按预期运行，例如空条目、负时间、缺少敌人 Prefab、Boss Encounter 或演出配置。
- `Warning`：表示可以运行但需要确认，例如缺少通关条目、Play SFX 没有 Cue、条目超出关卡 Duration，或自动切歌没有 AudioBinding。
- `validation: OK`：当前内置检查没有发现问题。

校验是只读的，不会自动修改或保存资产。它只覆盖编辑器当前已知的通用配置问题；条目内部更深层的 Encounter、Prefab、行为流和演出服务依赖仍需要在 Preview 或运行场景中确认。

## 常见问题

### Play 后没有生成内容

检查 `LevelController.Definition`、`AutoStart`、条目 Prefab、触发时间，以及 Unity Console。
若 `AutoStart=false`，确认外部确实调用了 `BeginLevel()`。

### Scene 中看不到位置标记

打开 Scene 视图的 Gizmos，确认关卡编辑器已加载资产，并重新选择条目。

### 时间轴条目无法操作

先确认详情输入框没有占用键盘焦点；拖拽问题同时检查当前缩放和条目是否位于可见区域。

### 自动音乐没有切换

检查 `AudioSystem`、`AutoSwitchBgm`、`AudioBinding`、绑定中的音乐字段，以及
`BeginLevel()` 是否执行。

## 文档边界

- 本文不包含新增 `SpawnEntry`、Drawer 或 Preview 的代码教程。
- 新条目类型的运行时契约见 `arch-level.md`；编辑器画法见 Editor 扩展指南。
- 精确字段和事件签名以源码和 Inspector 为准，避免在多处维护易过期的完整字段表。
