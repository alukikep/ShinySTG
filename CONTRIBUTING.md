# ShinySTG 开发约定

本文只保留稳定、跨任务适用的开发规则。架构和扩展入口从
[`ARCHITECTURE.md`](./ARCHITECTURE.md) 查找。

## 编码规范

- 文本文件使用 UTF-8；不要无故重写 `.meta`、`.asset`、`.prefab` 或场景文件。
- 类型、公开方法和属性使用 PascalCase；私有字段使用 `_camelCase`。
- Unity Inspector 字段添加简短、准确的 `[Tooltip]`，不要在 Tooltip 复刻整篇文档。
- 一个 `.cs` 文件通常只放一个主要类型，文件名与主要类型一致。
- 运行时代码不得引用 `UnityEditor`；编辑器代码放入 `Editor/` 或 Editor-only asmdef。
- 新增多态类型时沿用所在系统的 `Serializable`、`SRName`、Clone 和命名空间约定。

修改现有序列化字段的名称或类型可能破坏 prefab/asset 引用。确需修改时，先确认迁移策略，
必要时使用 `FormerlySerializedAs`。

## 查找正确的扩展入口

| 需求 | 权威文档或目录 |
|---|---|
| BulletModifier、时间窗和信号 | [`arch-bullet.md`](./docs/architecture/arch-bullet.md) |
| FirePattern、FireExtension、FireSound | [`arch-fire-pattern.md`](./docs/architecture/arch-fire-pattern.md) |
| EnemyAction、MoveBehaviour、Duration | [`arch-enemy-ai.md`](./docs/architecture/arch-enemy-ai.md) |
| BossPhase、BossSignal、多管血 | [`arch-boss.md`](./docs/architecture/arch-boss.md) |
| 对话、玩家控制锁与 Boss 对话接入 | [`arch-dialogue.md`](./docs/architecture/arch-dialogue.md) |
| SpawnEntry 与关卡运行时 | [`arch-level.md`](./docs/architecture/arch-level.md) |
| 关卡编辑器 Drawer/Preview | [`Assets/Scripts/Level/Editor/README.md`](./Assets/Scripts/Level/Editor/README.md) |
| 音频配置 | [`Assets/Scripts/Audio/README.md`](./Assets/Scripts/Audio/README.md) |
| 通用多态扩展方法 | [`arch-extension-guide.md`](./docs/architecture/arch-extension-guide.md) |

优先阅读对应子系统，不要为单一任务一次加载全部架构文档。

## 实现原则

- 一次改动聚焦一个完整功能，避免顺手重构无关系统。
- 优先复用现有数据流和扩展点，不为局部需求创建第二套平行体系。
- 多个界面入口执行同一操作时，共享命令层，不复制业务逻辑。
- 对象池中的状态必须在获取或回收时完整重置，尤其是 transform、颜色、计时和引用字段。
- 数组中的多态模块是否串行、并行或共享状态，以基类契约和对应架构文档为准。
- 不依赖反射发现顺序解决优先级；处理范围应互斥或有显式规则。

## 验证

提交前至少完成与改动相称的检查：

1. 查看 `git diff` 和 `git status`，确认没有无关文件、临时脚本、生成文件或意外 `.meta` 改动。
2. 确认 Unity Console 无新增编译错误。
3. 编辑器功能验证 Undo、资产 dirty 状态、Domain Reload 后行为。
4. 运行时功能验证对象池复用、场景切换和空引用路径。
5. 修改文档后检查相对链接、旧名称和互相矛盾的描述。

## 文档维护

- `README.md` 只做项目入口。
- `ARCHITECTURE.md` 只做地图、职责边界和文档索引。
- `docs/architecture/arch-*.md` 记录设计原因、边界和扩展契约。
- 子系统 README 记录用户操作、配置和最小公共 API 示例。
- 字段表、默认值、方法签名和逐行流程能从代码直接得到时，不在多份文档重复维护。
- 历史 bug、一次性工具问题和实施过程不要加入常驻上下文；只有形成稳定规则后才写入本文。

## 与 AI 协作

- 先给出目标和允许修改的范围；跨系统大改先对齐方案和风险。
- AI 应先读本文件、目标子系统文档和相关代码，不默认读取场景、Library、二进制资产或全部文档。
- 超过约 100 行或涉及序列化资产的修改，应先说明可能影响的文件与兼容风险。
- 生成代码必须人工 review，重点检查序列化兼容、生命周期、对象池状态和命名风格。
- 任务结束时清理临时文件，并总结修改、验证结果和仍需人工在 Unity 中确认的内容。
