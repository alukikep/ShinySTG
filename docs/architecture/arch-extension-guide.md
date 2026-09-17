# 扩展指南

> 加新功能统一套路 / 反模式 / 数据 vs 逻辑边界

> 本板块对应 ARCHITECTURE § 6. 扩展指南(原 ARCHITECTURE.md 第 1009–1063 行)。
> 本文档面向项目维护者,不复制实现细节,字段 / 数值 / 默认值以源文件为准。

---

## 6. 扩展指南

> **"我要加新功能，改哪里？"** 先看 [`CONTRIBUTING.md`](../../CONTRIBUTING.md) 的扩展入口表，再进入对应子系统文档。
>
> 本节只讲**架构原则**:加新东西时应该走哪条套路,以及为什么。

### 6.1 多态扩展点的统一套路

项目里有 10 个用 `[SerializeReference]` + 子类多态的扩展点,全部走同一个三步套路:

1. 在约定文件夹新建 `<你的>类名.cs`
2. 继承对应的抽象基类 + 加 `[Serializable, SRName("<下拉菜单路径>")]`
3. override 该 override 的方法

| 扩展点 | 基类 | 用途 |
|---|---|---|
| 弹幕形态 | `FirePattern` | 新增螺旋 / 樱花 / 自定义轨迹 |
| 弹幕角度 / 瞄准扩展 | `FireExtension` | 基础发射逻辑的多态扩展(BaseAngle / 瞄准玩家 / 瞄准 Boss / 每发旋转 / 振荡...),挂在 FirePattern 上,详见 §3.1(**累加型扩展**如 `AccumulatingOffsetAngleFireExtension` 详见 §3.1.1) |
| 子弹行为 | `BulletModifier` | 加速 / 转向 / 追踪 / 分裂(追踪类用 `CollisionService.Grid` 查候选,见 §2.5) |
| 敌人行为 | `EnemyAction` | 新增攻击 / 移动 / 自毁 / 容器 |
| 移动方式 | `MoveBehaviour` | 贝塞尔 / 圆形 / 追踪 |
| Boss 阶段 | `BossPhase` | 阶段 = 行为流,或自定义阶段体 |
| Boss 切换条件 | `BossSignal` | 开场过场 / 玩家撞 N 次 / 自定义条件 |
| 子机位置 | `OptionPositionForm` | 子机怎么排队 |
| 关卡条目 | `SpawnEntry` | 配关卡时一行下拉 |
| 子弹出生雾化 | `SpawnFogConfig` | 子弹出生瞬间的多态视觉/行为(不动 + 不参与碰撞 + 不累计 modifier + shader `_FogAmount` 渐变),挂在 FirePattern 上(**单字段**,不是数组),详见 §3.3 |
| 关卡编辑器画法 | `ISpawnEntryDrawer` | 编辑器时间轴 / Scene 视图怎么画 |
| 音效规则 | `SfxRule` | SFX 处理规则(随机抽 clip / pitch 抖动 / 自定义修饰),挂在 SfxCue 上,详见 §11 |
| 开火音模块 | `FireSound` | FirePattern 开火时发声(单 cue / 叠多 cue / 按状态发声),挂在 FirePattern 上,与 FireExtension 并行触发,详见 §3 |

**为什么 `[SerializeReference]` + `[SRName]`(而不是直接子类列表 / enum)**:
- 改 Inspector 不需要重编 / 不动宿主组件代码
- 加新子类 = 加一个 .cs,无需注册中心 / 反射配置
- 资产层与代码层解耦,跨场景 / 跨敌人共享行为流直接拖资产

### 6.2 数据驱动 vs 代码逻辑的边界

- **可配置 → SO 资产**:`FirePattern` / `BehaviorFlow` / `LevelDefinition` 都用 ScriptableObject。改一个 .asset 改全场景所有引用对象。**不要把"美术数值 / 关卡时间表 / 弹幕参数"硬编码进 .cs**。
- **运行时状态 → `[Serializable] class`**:`EnemyAction` / `MoveBehaviour` / `BossPhase` / `BossSignal` / `OptionPositionForm` 全是普通 `[Serializable] class` + `[SerializeReference]`。原因:这些对象要持有运行时状态(`_timer` / `_idx` 等),SO 跨实例共享会出问题。
- **数学 / 算法 / 正交层 → static class**:`HitboxMath` / `LevelEditorMath` 这类纯计算层,不带状态、不需要 Inspector 配置,放 static 类。
- **总控 / 生命周期 → MonoBehaviour**:`Player` / `Enemy` / `BossController` / `BulletPool` / `LevelController` 是 MonoBehaviour,管 Unity 生命周期;它们内部**引用** SO / `[SerializeReference]` 对象,不**继承**它们。

### 6.3 加新功能时的反模式

- ❌ 在 `Enemy` 上加 `if (敌人类型 == A) ... else if (类型 == B) ...`
  → ✅ 加新 `EnemyAction` 子类,配进 `.flow` 资产的 `Actions[]`
- ❌ 在 `Bullet` 上加分支做"加速弹 / 减速弹 / 分裂弹"
  → ✅ 加 `BulletModifier` 子类,外部 `bullet.AddModifier(...)` 挂载
- ❌ 在 `BossController` 上 `if (currentPhase == Phase1 && hp < 50) switch(...)`
  → ✅ 在 `PhaseTrigger[]` 里配 `Signal + Op + Threshold`,Inspector 下拉选条件
- ❌ 把"关卡时间表"写死在 `LevelController.cs`
  → ✅ 建 `LevelDefinition` 资产,在 `Entries[]` 数组里组合条目

---


## 与其他板块的关系

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [fire-pattern](./arch-fire-pattern.md) — FireExtension / FireSound / SpawnFog 是统一套路的典型实例
- [enemy-ai](./arch-enemy-ai.md) — EnemyAction / MoveBehaviour / ActionDurationConfig 同套路
- [boss](./arch-boss.md) — BossPhase / BossSignal 同套路
- [player](./arch-player.md) — OptionPositionForm 同套路
- [level](./arch-level.md) — SpawnEntry / SpawnPositionStrategy 同套路
- [audio](./arch-audio.md) — SfxRule 同套路
- [bullet](./arch-bullet.md) — BulletModifier / ModifierStartTrigger 同套路
