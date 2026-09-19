# ShinySTG 架构说明

> 本文是**架构地图**(板块索引 + 板块间关系 + 编写规范),不写实现细节。
> 每个板块的字段 / 数值 / 扩展点清单见 `docs/architecture/arch-<name>.md`。
> 源码 / 资产 / 子系统层面的细节由对应 `.cs` 顶部注释 + 子系统 README 给出。

---

## 1. 板块索引

| 板块文件 | 一句话 | 对应原 § |
|---|---|---|
| [bullet](./docs/architecture/arch-bullet.md) | `BulletPool` + `Bullet` + `BulletModifier` + 反弹 + 信号触发 | §2 |
| [fire-pattern](./docs/architecture/arch-fire-pattern.md) | `FirePattern` + 发射运行状态 + Composite 调度 / 逐弹角度 | §3 |
| [enemy-ai](./docs/architecture/arch-enemy-ai.md) | `BehaviorFlow` + `EnemyAction` + `MoveBehaviour` + `ActionDurationConfig` | §4 |
| [boss](./docs/architecture/arch-boss.md) | `BossController` + 多阶段 + 多管血 + `BossSignal` | §5 |
| [game-actions](./docs/architecture/arch-game-actions.md) | Encounter 动作序列、可等待演出与全局指令 | — |
| [dialogue](./docs/architecture/arch-dialogue.md) | 线性对话、播放句柄与玩家控制锁 | — |
| [extension-guide](./docs/architecture/arch-extension-guide.md) | 加新功能统一套路 / 反模式 / 数据 vs 逻辑边界 | §6 |
| [items](./docs/architecture/arch-items.md) | 可配置掉落、撒出与吸附、拾取结算和对象池 | — |
| [player](./docs/architecture/arch-player.md) | `Player` 主控、子机、死亡演出与重生 | §7 |
| [hud](./docs/architecture/arch-hud.md) | 玩家状态只读显示、事件绑定与纯 UI 布局编辑 | — |
| [hitbox](./docs/architecture/arch-hitbox.md) | 统一 AABB + 阵营 + 网格空间索引 | §8 |
| [level](./docs/architecture/arch-level.md) | `LevelDefinition` + `SpawnEntry` 多态 | §9 |
| [level-editor](./docs/architecture/arch-level-editor.md) | `EditorWindow` / 时间轴 / Preview / Gizmo | §10 |
| [audio](./docs/architecture/arch-audio.md) | `AudioSystem` + `SfxCue` + `BgmTrack` + 多态规则(架构视角) | §11 |
| [background](./docs/architecture/arch-background.md) | 双相机背景、循环布景、Cue 播放与关卡绑定 | — |
| [bounds](./docs/architecture/arch-bounds.md) | `BoundsService` 单例 + `PlayableArea` + `CullingArea` | §12 |
| [laser](./docs/architecture/arch-laser.md) | `LaserEntity` 5 段状态机 + 视觉/判定宽度分离 | §13 |

> **音频子系统细节**(Inspector 配置 / SO 创建步骤)见 `Assets/Scripts/Audio/README.md`,与 `arch-audio.md` 互为补充,不重复。

---

## 2. 基础架构地图(运行时流向)

```
游戏场景 (Scene)
├── Player (主控 + Movement + Shooting + Options + Health + Resources + DeathController)
│     → 引用 FirePattern 资产、读 BoundsService.PlayableArea
├── GameplayHudPresenter ─► GameplayHudView（读取玩家状态，更新 UI）
├── Boss / 普通敌人 (总控 + Health + Hitbox + BehaviorFlow)
│     → AI 层: BehaviorFlow SO 持有 EnemyAction[] / MoveBehaviour[]
│     → 通过 BulletPool.FireGroup / LaserPool.Fire 触发弹 / 激光
├── BulletPool (场景单例) ─────► Bullet (对象池复用 + Modifier 多态)
├── LaserPool  (场景单例) ─────► LaserEntity (5 段状态机 + Renderer 多态)
├── EffectPool (按场景自动创建) ─► 独立死亡特效 / 命中残影 / 可选 SfxCue
├── ItemDropService (场景单例) ─► ItemPickup (撒出、吸附、对象池)
├── CollisionService / LaserService ─► HitboxComponent / 阵营过滤
├── BoundsService (单例)        ─► PlayableArea + CullingArea(Gizmo 可视化)
└── AudioSystem (PersistentSingleton) ─► SfxCue / BgmTrack / Bus
```

---

## 3. 板块间关系(简单文字说明)

按"谁依赖谁"列出,完整文字版见每个板块末尾的"## 与其他板块的关系"段。

**核心子体系**(互相平行):

- **bullet ↔ laser**:两套独立运行时子体系,完全平行、互不依赖。阵营过滤共用 `CollisionTeam`,但碰撞数学分开(AABB 网格 vs 点-线段距离)。
- **fire-pattern**:被 `enemy-ai` (`FireAction`) / `player` (`PlayerShooting` / `Options`) / `laser` (`LaserPattern.FireSounds`) 引用。
- **audio**:被几乎所有板块通过"嵌入 `SfxCue` 字段"方式使用(`PlayerHealth` / `BossHealth` / `EnemyHealth` / `FireSound` / `SpawnEntry/PlaySFX` 等)。

- **items**：敌人死亡与 Boss 阶段指令读取 DropProfile 生成道具；CollisionService 检测拾取，奖励通过 PlayerHealth / PlayerResources 结算。

- **background ↔ level**：关卡广播 Cue 请求及生命周期事件，显式背景绑定负责转发；背景独立推进，不改变战斗视角。

- **hud → player**：Presenter 读取资源并订阅事件，View 只负责显示；布局编辑只修改 UI，不控制相机、世界边界或游戏流程。

- **战斗特效**：Enemy 击杀、玩家死亡和玩家弹命中生成独立表现，EffectPool 管理复用，附带声音交给 AudioMix；配置见 [Effects README](./Assets/Scripts/Effects/README.md)。

**运行时支撑层**:

- **hitbox**:bullet / laser / boss / player / enemy 全部挂 `HitboxComponent`,阵营(`Player` / `Enemy` / `Neutral`)由发射者透传。
- **bounds**:`Bullet` 出界 / 反弹、`PlayerMovement` 玩家活动边界都从这里读。

**AI 编排层**:

- **game-actions**：Encounter 持有 Runner，调用全局指令或适配 BehaviorFlow；BossController 根据动作句柄等待阶段推进，动作能力可由其他宿主复用。

- **boss ⊂ enemy-ai**:`BossController` 的每个 Phase 本质是 `BehaviorFlow`(通过 `ShooterPhase` 复用),AI 子类(EnemyAction / MoveBehaviour / BossPhase / BossSignal)统一走"SR 多态"扩展套路。
- **level → 一切资产**:`SpawnEntry` 在运行时按时间轴 instantiate enemy / boss prefab、播放 SFX,这些 prefab 间接引用 `BehaviorFlow` / `FirePattern` / `SfxCue`。

- **dialogue → player**：DialogueService 持有玩家控制锁；PlayDialogueAction 将播放句柄接入 Encounter，UI 不依赖 Boss 对象。

**编辑器层**:

- **level-editor**:编辑 `LevelDefinition`(`level` 板块的数据资产),Editor only,运行时无依赖。

**横切关注点**:

- **extension-guide**:跨所有板块的"加新功能统一套路"——`SerializeReference` + `[SRName("分组/名字")]` + 子类 override 即可,无需改宿主字段。
- **audio 子系统 README**(`Assets/Scripts/Audio/README.md`):保留不动,与 `arch-audio.md` 互为补充(架构视角 vs Inspector 操作视角)。

---

## 4. 编写架构说明的规范

适用于 `docs/architecture/arch-*.md` 的编写与维护:

1. **单子体系 = 单文件**。一个板块文件 = 一个自洽子体系,边界由"被哪些文件直接依赖"决定;若出现跨两个文件的双向强依赖,合并。
2. **文件 ≤ 600 行**。超过即再拆细(例:`arch-bullet.md` 489 行已接近上限,新增 modifier 时建议拆 `arch-bullet-modifier.md`)。
3. **不写实现细节**。字段默认值、数值公式、伪代码逐行解释一律不放;要写"为什么这样设计"和"扩展时改哪里"。
4. **扩展点必须给"新增 = 加一行 + 加一个 .cs"的具体例子**。每个 SR 多态字段(Modifier / Action / MoveBehaviour / FireExtension / FireSound / SpawnFog / BossPhase / BossSignal / OptionPositionForm / SpawnPositionStrategy / SfxRule / ActionDurationConfig / ModifierStartTrigger / BaseOffsetStrategy)至少配 1 个最小示例。
5. **每个文件末尾必带"## 与其他板块的关系"**。简单文字即可,不用 ASCII 图 / Mermaid。
6. **不在本文件复制字段**。Tooltip 字段说明由源 `[Tooltip("...")]` 给出,板块文档只写"字段语义 + 推荐值范围"。
7. **跨板块引用用相对路径**:`./arch-fire-pattern.md`,不要用绝对路径或 GitHub blob URL。
8. **加新板块**:在主文件 §1 表格加一行 + §3 关系段补一句 + 在相关板块文件的"## 与其他板块的关系"加反向引用。**不**在本文件重复板块细节。

---

## 5. 子系统 README 与板块文档的关系

| 类型 | 位置 | 职责 |
|---|---|---|
| 架构板块文档 | `docs/architecture/arch-<子系统>.md` | 架构视角:为什么这样设计 / 子体系边界 / 扩展套路 |
| 子系统 README | `Assets/Scripts/<子系统>/README.md` | Inspector / SO 创建 / 字段配置操作步骤 |

Audio 与 Level Editor 同时拥有架构板块和操作/扩展 README：前者讲边界，后者讲具体操作。其他子系统若配置流程明显复杂，也可按同一原则补充 README。

---

## 6. 兼容旧引用

旧源码注释中可能仍有 `ARCHITECTURE §X.Y` 形式的历史引用。新文档不要继续使用章节号；应直接链接对应的 `docs/architecture/arch-*.md` 文件或标题。触及旧引用附近代码时再顺手修正，无需为此批量改源码。
