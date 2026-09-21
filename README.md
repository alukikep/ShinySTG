# ShinySTG

ShinySTG 是一个 Unity 弹幕射击项目，采用数据驱动、组合式多态和资产化行为流。

## 开始使用

1. 使用 `ProjectSettings/ProjectVersion.txt` 指定的 Unity 版本打开项目。
2. 正式开局和连续关卡按 [游戏流程说明](./Assets/Scripts/GameFlow/README.md) 配置，从 StartMenu 进入；Gameplay 由 Bootstrap 创建玩家，不要预放第二个玩家。独立测试场景按测试需要配置玩家、对象池、舞台边界及关卡控制器。
3. 通过 ScriptableObject 配置敌人行为、弹幕、关卡和音频，避免把关卡内容硬编码进 MonoBehaviour。

关卡制作从 [`LEVEL_EDITOR.md`](./LEVEL_EDITOR.md) 开始；修改代码前阅读
[`CONTRIBUTING.md`](./CONTRIBUTING.md)。

## 主要系统

| 系统 | 核心类型 | 文档 |
|---|---|---|
| 子弹与 Modifier | `BulletPool`、`BulletModifier` | [bullet](./docs/architecture/arch-bullet.md) |
| 射击模式 | `FirePattern`、`FireExtension`、`FireSound` | [fire-pattern](./docs/architecture/arch-fire-pattern.md) |
| 敌人行为 | `BehaviorFlow`、`EnemyAction`、`MoveBehaviour` | [enemy-ai](./docs/architecture/arch-enemy-ai.md) |
| Boss | `BossController`、`BossPhase`、`BossSignal` | [boss](./docs/architecture/arch-boss.md) |
| 玩家 | `Player`、`PlayerShooting`、`OptionPositionForm` | [player](./docs/architecture/arch-player.md) |
| Hitbox | 统一 AABB、阵营和空间索引 | [hitbox](./docs/architecture/arch-hitbox.md) |
| 关卡 | `LevelDefinition`、`LevelController`、`SpawnEntry` | [level](./docs/architecture/arch-level.md) |
| 关卡编辑器 | 时间轴、列表、Preview、Gizmo | [操作手册](./LEVEL_EDITOR.md) / [架构](./docs/architecture/arch-level-editor.md) |
| 音频 | `AudioSystem`、`SfxCue`、BGM 和 Bus | [配置](./Assets/Scripts/Audio/README.md) / [架构](./docs/architecture/arch-audio.md) |
| 舞台边界 | `BoundsService` | [bounds](./docs/architecture/arch-bounds.md) |
| 激光 | `LaserEntity`、`LaserPool`、`LaserModifier` | [laser](./docs/architecture/arch-laser.md) |

完整依赖地图与文档职责见 [`ARCHITECTURE.md`](./ARCHITECTURE.md)。当前功能状态见
[`PROGRESS.md`](./PROGRESS.md)。

## 常用资产入口

| 目的 | Unity 菜单或字段 |
|---|---|
| 创建敌人行为 | `Create -> STG -> Behavior Flow` |
| 创建弹幕 | `Create -> STG -> Fire Pattern` 下对应类型 |
| 创建关卡 | `Create -> STG -> Level` |
| 打开关卡编辑器 | `STG -> Level Editor` |
| 创建音效 | `Create -> STG -> Audio -> SFX Cue` |
| 创建 BGM | `Create -> STG -> Audio -> BGM Track/Playlist` |

具体菜单可能随类型扩展而增加，以 Unity 当前菜单和 Inspector 为准。

## 目录

```text
Assets/
├─ Scripts/Bullet/       子弹、FirePattern 与发射扩展
├─ Scripts/Enemy/        敌人行为
├─ Scripts/Boss/         Boss 阶段与血条
├─ Scripts/Player/       玩家、射击与子机
├─ Scripts/Level/        关卡运行时与编辑器
├─ Scripts/Audio/        SFX、BGM、Bus 与关卡音频绑定
├─ Scripts/Hitbox/       统一碰撞系统
├─ Scripts/Laser/        激光系统
└─ Scripts/Stage/        舞台边界
```

不要把逐文件清单维护在 README；查找实现时使用文件搜索或对应架构文档。

## 文档规则

- README 是项目入口，不记录实现细节。
- `ARCHITECTURE.md` 是系统索引；各子系统边界写在 `docs/architecture/`。
- 操作复杂的系统可在对应 `Assets/Scripts/<System>/README.md` 提供配置说明。
- 代码签名、字段默认值和逐步执行流程以源码、Inspector Tooltip 为准。
- AI 协作和代码提交约定见 [`CONTRIBUTING.md`](./CONTRIBUTING.md)。
