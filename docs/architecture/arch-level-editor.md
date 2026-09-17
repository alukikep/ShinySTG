# 关卡编辑器

> EditorWindow / 时间轴 / Preview / Gizmo

> 本板块对应 ARCHITECTURE § 10. 关卡编辑器子系统(原 ARCHITECTURE.md 第 1145–1170 行)。
> 本文档面向项目维护者,不复制实现细节,字段 / 数值 / 默认值以源文件为准。

---

## 10. 关卡编辑器子系统

> **本节只讲"它在架构里的位置"**,不讲怎么用 / 怎么扩展。
>
> - **使用者**(配关卡):见 [`LEVEL_EDITOR.md`](../../LEVEL_EDITOR.md)
> - **扩展者**(加新 Drawer / Preview):见 [`Assets/Scripts/Level/Editor/README.md`](../../Assets/Scripts/Level/Editor/README.md)

### 在架构里的位置

- **与运行时完全正交**:`Editor/` 是独立 asmdef(`includePlatforms = ["Editor"]`),不修改运行时任何类型,不反向依赖运行时的可序列化细节。运行时程序集对编辑器 0 感知。
- **只读运行时 SO 资产**:打开编辑器 = 拖一份 `LevelDefinition.asset` 进来;Editor 程序集不发明新数据格式,关卡数据 100% 在运行时侧描述。
- **多态扩展走两条独立通道**:`SpawnEntry` 子类通过 SREditor 的 `[SRName]` 在 Inspector 下拉;`ISpawnEntryDrawer` 子类通过 `LevelEditorDrawerRegistry`(反射枚举)被 Editor 窗口拿到。两条通道都用"反射枚举 + 第一个匹配胜出"的同一套路。
- **Preview 与运行时解耦**:编辑器自己定义 `ILevelEditorPreview` 接口 + 默认实现 `LevelEditorPlayer`,不依赖 `LevelRuntime` 的时间推进逻辑;持续条目用 Editor 侧的 `StubLevelRuntime` 接管,Stop 时统一销毁临时 GameObject,不污染场景。

### 与既有层的关系

| 既有层 | 编辑器怎么用 |
|---|---|
| `LevelDefinition` (SO) | 主窗口直接 `SerializedObject` 渲染,Editor 侧**不复制**字段 |
| `SpawnEntry` (多态条目) | 反射枚举所有子类,加 `+ Add ▾` 下拉;`ISpawnEntryDrawer` 按 type 分派 |
| SREditor 插件 | 右栏 `LevelEntryDetailView` 直接 `EditorGUILayout.PropertyField`,**0 行自定义 IMGUI**;下拉 / 折叠 / missing type 检测全由 SREditor 接管 |
| `BehaviorFlow` / `FirePattern` | 菜单平行,扩展点在它们各自的 Inspector 里配,编辑器不重复配 |

**总 ~1500 行**,14 个源文件 + 1 个 asmdef,**对运行时的影响 = 0**。


## 与其他板块的关系

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [level](./arch-level.md) — 编辑的就是 LevelDefinition,字段含义见 arch-level.md
