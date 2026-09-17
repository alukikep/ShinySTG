# ShinySTG AI 工作入口

## 默认读取顺序

1. 本文件。
2. `CONTRIBUTING.md`。
3. 与任务直接相关的一个 `docs/architecture/arch-*.md`。
4. 相关源码；仅在需要配置操作时再读对应子系统 README。

不要为单一任务默认读取全部架构文档、Unity 场景、`Library/`、二进制资产或第三方插件说明。

## 项目约束

- Unity 版本以 `ProjectSettings/ProjectVersion.txt` 为准。
- 运行时代码位于 `Assets/Scripts/`；不得引用 `UnityEditor`。
- 编辑器代码放在 `Editor/` 或 Editor-only asmdef。
- 私有字段使用 `_camelCase`；类型和公开成员使用 PascalCase。
- 修改序列化字段前检查 prefab/asset 兼容性，必要时使用 `FormerlySerializedAs`。
- 不要无故修改 `.meta`、场景、prefab、asset、生成文件或第三方插件。
- 对象池对象在复用时必须重置全部可变状态。

## 按任务选文档

| 任务 | 先读 |
|---|---|
| 子弹/Modifier | `docs/architecture/arch-bullet.md` |
| FirePattern/发射扩展 | `docs/architecture/arch-fire-pattern.md` |
| 敌人行为 | `docs/architecture/arch-enemy-ai.md` |
| Boss | `docs/architecture/arch-boss.md` |
| 对话/战前战后演出 | `docs/architecture/arch-dialogue.md` |
| 关卡运行时 | `docs/architecture/arch-level.md` |
| 关卡编辑器 | `LEVEL_EDITOR.md` 或 `Assets/Scripts/Level/Editor/README.md` |
| 音频配置 | `Assets/Scripts/Audio/README.md` |
| 音频架构 | `docs/architecture/arch-audio.md` |
| 激光 | `docs/architecture/arch-laser.md` |

## 完成前检查

- 查看 `git diff`/`git status`，确保改动范围准确。
- 验证 Unity 编译或明确说明未能在 Unity 中验证。
- 编辑器写操作检查 Undo 和 dirty 状态；运行时改动检查对象池与空引用路径。
- 文档改动检查相对链接，不复制源码可直接表达的字段表和逐行流程。
