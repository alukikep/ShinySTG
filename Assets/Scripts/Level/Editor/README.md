# Level Editor — 扩展指南

> 本文档面向"要给关卡编辑器加新功能 / 自定义画法"的开发者。
> 用户级使用说明见 [`LEVEL_EDITOR.md`](../../../LEVEL_EDITOR.md)。

---

## 目录

1. [总体架构](#总体架构)
2. [扩展点:ISpawnEntryDrawer](#扩展点-ispawnentrydrawer)
3. [扩展点:ILevelEditorPreview](#扩展点-ileveleditorpreview)
4. [用户交互(删除/快捷键/右键菜单)](#用户交互删除快捷键右键菜单)
5. [修改主窗口](#修改主窗口)
6. [调试技巧](#调试技巧)

---

## 总体架构

```
LevelEditorWindow (主窗口:菜单 / 装配 / 协调)
├── LevelEditorContext   ─ 共享数据(Definition / SerializedObject / Selected)
├── LevelEditorCommands  ─ 静态命令类(FocusSceneView / Duplicate / Delete / RefreshViews)
├── Views/
│   ├── LevelEntryListView      ← 左栏(列表 + 右键菜单 + Delete 快捷键)
│   ├── LevelTimelineView       ← 中栏(单轨时间轴 + 拖拽 + 右键 + Delete/Backspace)
│   └── LevelEntryDetailView    ← 右栏(复用 SREditor 渲染)
├── Drawers/
│   ├── ISpawnEntryDrawer       ← ⭐ 关键扩展点(abstract class,virtual 方法可选择性 override)
│   ├── DefaultSpawnEntryDrawer ← 空类,继承基类全部默认实现,Registry 显式跳过
│   └── LevelEditorDrawerRegistry ← 反射自动发现 + 缓存 fallback
├── Gizmos/
│   └── LevelSceneGizmos        ← Scene 视图辅助(选中 X + 未选小圆)
└── Views/Preview/
    ├── ILevelEditorPreview     ← Preview 接口(可换实现)
    ├── LevelEditorPlayer       ← Editor 模式播放器(StubLevelRuntime 支撑)
    └── LevelTimelineCursor     ← 时间游标绘制
```

**关键设计原则**(与项目运行时一致):

- **数据驱动**:编辑器**只读** `LevelDefinition`,不发明新数据格式
- **多态 + 自动发现**:新加 `SpawnEntry` 子类 / 新加 `ISpawnEntryDrawer` 子类 = 自动出现在主窗口,无需改主代码
- **Editor-only**:`Editor/` 目录 + asmdef 的 `includePlatforms = ["Editor"]`,运行时 0 依赖

---

## 扩展点:ISpawnEntryDrawer

> 每个 SpawnEntry 类型在时间轴上的画法 + Scene 视图画法。

### 何时需要加

- 想让 `BossSpawnEntry` 在时间轴上画成特殊图标(目前是矩形块 + 文字)
- 想给某种 entry 在 Scene 视图画箭头 / 范围圈 / 路径预览
- 想给某种 entry 改变默认颜色(默认按类型上色)

### 怎么加(2 步)

#### Step 1: 新建 `Drawers/<YourEntry>Drawer.cs`

```csharp
using UnityEditor;
using UnityEngine;

namespace ShinySTG.Level.Editor.Drawers
{
    public class BossSpawnEntryDrawer : ISpawnEntryDrawer
    {
        // 告诉 Registry 接管哪种类型(必须 override —— 默认实现对所有 entry 返回 true)
        public override bool Handles(SpawnEntry entry) => entry is BossSpawnEntry;

        // 只 override 想改的部分,其余走基类默认;
        // 基类详见 ISpawnEntryDrawer.cs 顶部注释里的契约说明。
        public override string GetLabel(SpawnEntry entry)
        {
            var b = (BossSpawnEntry)entry;
            return $"⚔ Boss @ {b.TriggerTime:F1}s";
        }

        public override Color GetColor(SpawnEntry entry) => new Color(1f, 0.3f, 0.3f);

        // 持续型覆盖:boss 持续出场有专属宽度算法时可以 override GetDuration;
        // 只需要改颜色 / 标签时,其余方法(GetSubLabel / DrawTimelineBlock / DrawSceneGizmo)
        // 都可以继承基类默认,本例里只展示想完全自定义的场景:
        public override void DrawTimelineBlock(Rect rect, SpawnEntry entry, bool selected, LevelEditorContext ctx)
        {
            // 自己画:背景色 + 大号 "BOSS" 标签
            var prev = GUI.color;
            GUI.color = GetColor(entry);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;

            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(rect, "⚔ BOSS", labelStyle);
        }

        public override void DrawSceneGizmo(SpawnEntry entry, LevelEditorContext ctx)
        {
            var b = (BossSpawnEntry)entry;
            var prev = Handles.color;
            Handles.color = Color.red;

            // 画大圆 + 6 道辐射线
            Handles.DrawWireDisc(b.SpawnPosition, Vector3.forward, 1.0f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Handles.DrawLine(b.SpawnPosition,
                                 b.SpawnPosition + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * 1.2f);
            }

            Handles.Label(b.SpawnPosition + Vector3.up * 1.5f, "BOSS");
            Handles.color = prev;
        }
    }
}
```

#### Step 2: 完事

**不需要**任何注册代码。`LevelEditorDrawerRegistry` 在 Domain Reload 时通过 `TypeCache.GetTypesDerivedFrom<ISpawnEntryDrawer>()` 反射自动发现,缓存到 `Dictionary<Type, ISpawnEntryDrawer>`。

> **优先级规则**:`Registry.Resolve(entry)` 按"先扫到的 Handles 返回 true 的抽屉"分派。
> 你自己的 `BossSpawnEntryDrawer` 排在 `DefaultSpawnEntryDrawer` 之前(后者 `Handles` 返回 true 但被显式跳过),所以 Boss 会用你的画法。
> 多个自定义 drawer 处理同一类型时,**先写在文件夹前面的赢**。

---

## 扩展点:ILevelEditorPreview

> 把"模拟关卡运行"这部分替换成自己的实现。

### 何时需要换

- 想让 Preview 直接跑 `LevelRuntime`(运行时类)而不是 StubLevelRuntime
- 想加步进模式(每帧手动推进一步)
- 想在 Preview 期间高亮"已触发的条目块"

### 怎么加

```csharp
public class MyPreviewPlayer : ILevelEditorPreview
{
    // ... 实现接口 6 个方法 ...
}

// 在 LevelEditorWindow 里替换实例:
_preview = new MyPreviewPlayer();  // 改 OnEnable 里那行即可
```

---

## 用户交互(删除/快捷键/右键菜单)

> 所有"对选中 entry 的操作"都走 `LevelEditorCommands`,时间轴 / 列表 / Toolbar 共用同一份逻辑,
> 避免行为发散(比如"Toolbar 删的能撤销,右键删的不能"这种坑)。

### 删除

| 入口 | 行为 | 备注 |
|---|---|---|
| Toolbar `[Delete]` 按钮 | 删除 `ctx.Selected` | 无选中时 DisabledScope 灰色 |
| 时间轴 block 上**右键** → Delete | 删除该 block | 同时把 `ctx.Selected = null` |
| 列表行**右键** → Delete | 删除该行 entry | 同时选中改为 null |
| 时间轴 / 列表内**选中后按 Delete 或 Backspace** | 删除选中 | `GUIUtility.keyboardControl == 0` 时才触发(避开输入框焦点) |

所有删除都走标准编辑器流程:`Undo.RecordObject` + `Array.Copy` 重建数组 + `EditorUtility.SetDirty` + 清 `LastSelectedEntryIndex`。**支持 Ctrl+Z 撤销**。

### 复制 / 聚焦

| 操作 | 入口 | 行为 |
|---|---|---|
| Duplicate | Toolbar 按钮 / 时间轴右键 / 列表右键 | `JsonUtility.ToJson` 浅 clone,瞬时点偏移 +0.5s,持续型偏移 +Duration,插到原 entry 之后,选中改为克隆体 |
| Focus in Scene | 时间轴右键 / 列表右键 | `SceneView.lastActiveSceneView.LookAt(entry 的 SpawnPosition)` |

### 加新交互入口(改哪里)

新入口(比如"快捷键 Ctrl+D 复制"、"工具菜单一项")都遵循同一模式:

```csharp
LevelEditorCommands.Duplicate(_ctx, entry);   // 写操作
LevelEditorCommands.RefreshViews(this);        // 通知 EditorWindow + SceneView 重画
```

**不要**自己写一套 Duplicate/Delete 逻辑绕过 `LevelEditorCommands`,否则 Toolbar / 快捷键 / 右键菜单行为就会发散。

### 时间轴视口裁剪(`ComputeBlockRect`)

时间轴视图(`LevelTimelineView`)用统一的 `ComputeBlockRect` 算出每个 block 的屏幕 Rect,
同时被 `DrawTrack`(绘制)和 `HitTestBlock`(命中测试)调用,保证"看得到的 = 点得到的":

- **完全滚出视口**(左侧外或右侧外)→ 返回 false,**跳过绘制与命中**
- **部分滚出** → 起点钳到 `trackRect.x`,宽度钳到 `trackRect.xMax`,可见宽度 < `BlockMinWidth` 时也跳过
- 瞬时点固定 `BlockPointWidth` (140px),持续型按 `Duration × pxPerSec` 计算(最小 `BlockMinWidth`)

历史上曾有过"右滑太久 block 全堆到最左侧"的 bug——根因是 `Mathf.Max(trackRect.x, x)` 把负坐标的 block 钳到视口左缘,导致视口外 block 被错误绘制。**修法:对完全滚出的 block 直接跳过**,而不是钳到边。

---

## 修改主窗口

主窗口 4 个区域:**Toolbar / 主区域 / StatusBar / EmptyState**。

| 想加什么 | 改哪里 |
|---|---|
| 工具栏新按钮 | `LevelEditorWindow.DrawToolbar`(现有按钮:`Open` / `+ Add ▾` / `Duplicate` / `Delete`) |
| 主区域布局变化 | `LevelEditorWindow.DrawMainArea`(现在 3 列:列表 / 时间轴 / 详情) |
| 状态栏多行信息 | `LevelEditorWindow.DrawStatusBar` |
| 新增 `+ Add` 下拉项 | `LevelEditorWindow.ShowAddMenu`(当前已用反射自动列出所有 SpawnEntry 子类,无需改) |
| 新增"对选中 entry 的操作" | 走 `LevelEditorCommands`,不要在各视图里复制 Duplicate/Delete 逻辑 |
| 双击 .asset 自动打开 | 给 `LevelDefinition` 加 `[CustomEditor]` + `HasOpenInspector<EditorWindow>` 钩子 |

**所有改主窗口的操作都不会破坏运行时**:`Editor/` 是独立 assembly,运行时程序集 0 引用。

---

## 调试技巧

| 问题 | 排查 |
|---|---|
| 新加的 SpawnEntry 没出现在 `+ Add` 菜单 | 检查 `[SRName]` 是否漏了 / 命名空间是否在 SREditor 可发现的 assembly 里(放 `Assets/Scripts/Level/SpawnEntries/`) |
| 时间轴 block 全是灰色 | 自定义 Drawer 的 `Handles(entry)` 返回 false,或没被 Registry 找到 → 检查 namespace + asmdef |
| 拖动 block 没反应 | `_draggingBlock` 没置 true → 检查 `HitTestBlock` 命中逻辑(140f 宽度假设) |
| Scene 视图看不到 Gizmo | 顶部 Scene 视图工具栏勾了"Gizmos"开关?或 `LevelSceneGizmos.CurrentDefinition` 为 null(主窗口没加载) |
| Preview 按 Play 触发没反应 | 检查 `_preview.Start(_definition)` 调用;`SpawnEntry.OnTrigger` 可能抛异常被 catch 静默(打开 console) |

---

## 文件清单(总览)

```
Assets/Scripts/Level/Editor/
├── ShinySTG.Level.Editor.asmdef
├── LevelEditorWindow.cs
├── LevelEditorStyles.cs
├── LevelEditorPrefs.cs
├── LevelEditorContext.cs
├── LevelEditorCommands.cs
├── Views/
│   ├── LevelEntryListView.cs
│   ├── LevelTimelineView.cs
│   ├── LevelEntryDetailView.cs
│   └── Preview/
│       ├── ILevelEditorPreview.cs
│       ├── LevelEditorPlayer.cs
│       └── LevelTimelineCursor.cs
├── Drawers/
│   ├── ISpawnEntryDrawer.cs
│   ├── DefaultSpawnEntryDrawer.cs
│   └── LevelEditorDrawerRegistry.cs
├── Gizmos/
│   └── LevelSceneGizmos.cs
└── README.md  ← 本文档
```

**总 ~1500 行**,14 个源文件 + 1 个 asmdef + 1 个 README。
**对运行时的影响**:**0** —— 只新增 Editor 目录,运行时 7 个文件 1 行未改。
