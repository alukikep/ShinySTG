# Level Editor 扩展指南

本文面向需要扩展关卡编辑器的开发者。日常关卡配置见
[`LEVEL_EDITOR.md`](../../../../LEVEL_EDITOR.md)，架构边界见
[`arch-level-editor.md`](../../../../docs/architecture/arch-level-editor.md)。

## 结构与职责

```text
LevelEditorWindow          窗口装配与协调
├─ LevelEditorContext      当前资产、序列化状态和选中项
├─ LevelEditorCommands     Duplicate/Delete/Focus 等共享命令
├─ Views/                  列表、时间轴、详情与 Preview
├─ Drawers/                不同 SpawnEntry 的显示策略
└─ Gizmos/                 Scene 视图辅助绘制
```

编辑器只读写 `LevelDefinition`，不创建第二套关卡数据格式。所有代码位于 Editor assembly，
不应被运行时程序集引用。

## 自定义 SpawnEntry 画法

仅当某个 `SpawnEntry` 需要不同的时间轴标签、颜色或 Scene Gizmo 时才新增 Drawer。

在 `Drawers/` 新建类型并继承 `ISpawnEntryDrawer`：

```csharp
public sealed class BossEncounterEntryDrawer : ISpawnEntryDrawer
{
    public override bool Handles(SpawnEntry entry)
        => entry is BossEncounterEntry;

    public override string GetLabel(SpawnEntry entry)
        => "Boss Encounter";

    public override Color GetColor(SpawnEntry entry)
        => new Color(1f, 0.3f, 0.3f);
}
```

必须实现 `Handles`；其他方法按需覆盖。Registry 会通过反射自动发现，无需手工注册。
具体契约以 `ISpawnEntryDrawer.cs` 的基类注释为准。

若多个 Drawer 同时处理一种类型，结果会依赖发现顺序，因此应保证职责互斥，不要依赖文件名排序。

## 替换 Preview

需要步进、特殊模拟环境或自定义触发高亮时，实现 `ILevelEditorPreview`，并在
`LevelEditorWindow` 的初始化位置替换默认实现。一般显示功能不应修改 Preview。

## 添加编辑器操作

选中项操作统一放在 `LevelEditorCommands`，列表、时间轴、Toolbar 和快捷键只调用命令：

```csharp
LevelEditorCommands.Duplicate(context, entry);
LevelEditorCommands.RefreshViews(window);
```

写操作应支持 Undo、标记资产 dirty，并同步清理或更新选中状态。不要在各 View 中复制
Duplicate/Delete 实现。

## 修改界面的位置

| 需求 | 入口 |
|---|---|
| Toolbar 按钮 | `LevelEditorWindow.DrawToolbar` |
| 主区域布局 | `LevelEditorWindow.DrawMainArea` |
| 状态栏 | `LevelEditorWindow.DrawStatusBar` |
| 新建条目菜单 | `LevelEditorWindow.ShowAddMenu` |
| 列表交互 | `LevelEntryListView` |
| 时间轴绘制与命中 | `LevelTimelineView` |
| Scene 辅助图形 | `LevelSceneGizmos` 或自定义 Drawer |

新增 `SpawnEntry` 子类通常会自动进入 `+ Add` 菜单，不需要修改窗口代码。

## 时间轴绘制约束

绘制和点击命中必须共用同一个 block Rect 计算结果：

- 完全离开视口的 block 不绘制、也不参与命中。
- 部分离开视口时只裁剪可见部分。
- 不要把完全在左侧的负坐标 block 强制钳到视口左缘，否则滚动后会堆叠在边界。

## 排查清单

| 问题 | 优先检查 |
|---|---|
| 新条目未出现在菜单 | `SRName`、编译错误、类型所在 assembly |
| 自定义 Drawer 未生效 | `Handles`、命名空间、Editor asmdef |
| 时间轴能看见但点不到 | 绘制与命中是否使用同一 Rect |
| Scene 中没有 Gizmo | Scene Gizmos 开关、当前 Definition、窗口是否加载资产 |
| Preview 无触发 | Preview 是否 Start、条目触发是否抛异常 |

## 维护边界

- 用户操作只写在根目录 `LEVEL_EDITOR.md`。
- 本文只记录稳定的扩展入口，不维护完整文件树、文件数或代码行数。
- 系统职责和依赖关系只写在 `docs/architecture/arch-level-editor.md`。
