# 音频音乐系统

> AudioSystem + SfxCue + BgmTrack + 多态规则 (与 Assets/Scripts/Audio/README.md 互为补充)

> 本板块对应 ARCHITECTURE § 11. 音频音乐系统(AudioSystem)(原 ARCHITECTURE.md 第 1171–1229 行)。
> 本文档面向项目维护者,不复制实现细节,字段 / 数值 / 默认值以源文件为准。

---

## 11. 音频音乐系统(AudioSystem)

负责把「什么时机播什么音」封装成可复用资产,并在场景里按需调度 SFX + BGM。**与既有层完全正交** —— 不修改 Player / Enemy / Bullet / Level 任何代码,仅复用项目已有的「数据驱动 + 多态 + Pipeline」套路。

**职责分工:**

- `AudioMix`(静态门面) —— 唯一调用入口,所有调用方走 `AudioMix.PlaySfx(...)` / `AudioMix.PlayTrack(...)`,不直接拿单例。
- `AudioSystem`(场景单例,`PersistentSingleton`) —— 持 `SfxRouter` / `MusicPlayer` / `BusMixer` / `AudioEventHub`,挂场景里一个 GameObject(命名 Audio)。
- `SfxRouter`(普通 class) —— SFX 池 + 同 cue 限流 + Pipeline 调度。
- `MusicPlayer`(普通 class) —— 交叉淡化 + Playlist 顺序/随机播放。
- `BusMixer`(普通 class) —— 总线音量(兼容/不兼容 Unity AudioMixer 两条路径)+ PlayerPrefs 持久化。

**数据资产(SO):**

- `SfxCue` —— 单个 SFX(Clips 数组 + 默认音量/pitch + 限流参数 + Rules Pipeline)。
- `BgmTrack` —— 单首 BGM(Clip + 默认音量 + Loop + 路由 Bus + StartTime)。
- `BgmPlaylist` —— BGM 列表(Tracks + Shuffle + Loop + CrossfadeDuration)。
- `AudioBus` —— 总线配置(兼容 Unity AudioMixerGroup + PlayerPrefs 持久化)。
- `AudioBank` —— SfxCue 分组容器(纯 Inspector 组织用,不强制走)。
- `LevelAudioBinding` —— 关卡↔BGM 绑定(**接口预留,运行时默认不启用**,供 LevelEditor 后续扩展「切换 BGM」方法时使用)。

**多态扩展点(走 `[Serializable, SRName]` 下拉,与 FireExtension / EnemyAction / BossPhase 同套路):**

- `SfxRule`(`Assets/Scripts/Audio/Sfx/`) + 内置 `RandomPickRule` / `PitchVariationRule` / `CooldownRule`。Pipeline 模型:每个规则 `Process(SfxRequest)` 串行处理。
- 后续新增规则 = 新建 `xxxRule.cs : SfxRule` + `[SRName("Rule/<名字>")]`,Inspector 自动出现。

**协作边界:**

- **不侵入其他模块**:所有挂点都是「在调用方已有的逻辑里加一行 `AudioMix.PlaySfx(...)`」,不修改 Player / Enemy / Bullet / Boss 任何接口/事件/字段。
- **嵌入方式**:在 `PlayerHealth` / `BossHealth` / `EnemyHealth` / `PlayerShooting` 上加 `[SerializeField] SfxCue` 字段 + 在 TakeDamage / TakeHit / OnDeath 处调 `AudioMix.PlaySfx(...)`。
- **静态门面 null-safe**:`AudioSystem.Instance == null` 时调用静默返回,不报错 —— 调用方不需要 null check。
- **场景单例**:场景里挂一个 `AudioSystem` MonoBehaviour,`PersistentSingleton` 跨场景保留,菜单切关卡不重置音量。
- **时间暂停**:`Time.timeScale = 0` 时 `AudioSystem.Update` 自动停 SFX + Pause BGM(对齐 STG 暂停菜单)。

**与既有层的关系:**

| 既有层 | 音频怎么用它 | 是否修改它 |
|---|---|---|
| `PlayerHealth` / `BossHealth` / `EnemyHealth` | 加 `[SerializeField] SfxCue` 字段,在 TakeDamage / OnDeath 调 `AudioMix.PlaySfx(...)` | ✅ 改动但**只增字段 + 只增调用,不改签名/事件/字段顺序** |
| `PlayerShooting` | 加 `[SerializeField] SfxCue _shootSfx` 字段,FireGroup 后调 `AudioMix.PlaySfx(...)` | ✅ 同上 |
| `Bullet` / `CollisionService` / `Enemy` | 0 改动 —— SFX 由宿主在自己的 TakeDamage 处触发 | ❌ 完全不动 |
| `LevelController` / `BehaviorFlow` | `LevelController.BeginLevel` 调一次 `AudioEventHub.TryBind(definition)`(3 行内,无侵入) | ✅ 改动但只增 1 行调用 |
| `DOTween` | 后续可复用于音量淡入淡出(`DOFloat` / `DOVolume`),已集成 | 复用 |

**自动切歌机制(按关卡启用):**

- 启用入口:`LevelController.BeginLevel()` 末尾调一次 `AudioEventHub.TryBind(Definition)`(在 `OnLevelStart` invoke 之前)。
- 启用条件:`Definition.AutoSwitchBgm == true` 且 `Definition.AudioBinding != null`(关卡自带决定)。
- 启用后:`AudioEventHub` 订阅 `LevelController.OnLevelStart / OnBossSpawned / OnBossDefeated`,按 `AudioBinding.Playlist / BossMusic / DefeatMusic` 自动切歌。
- 关卡编辑器便利:`STG → Level Editor` 工具栏 `+ Create AudioBinding` 一键创建同名 `_AudioBinding.asset` + 双向反引用。
- 关卡级开关:`Definition.AutoSwitchBgm`(默认 `true`),可单独关掉(过场关 / 静音关)。
- 向后兼容:若 `Definition.AudioBinding == null`,回退到 `AudioSystem.LevelBindings[]` 全局查表模式(老用法仍工作)。
- 关卡间切换:ReloadLevel / 切下一关时调 `TryBind` 是幂等的,自动解订旧订阅 + 订阅新 LevelController。
- 无 AudioSystem 时静默跳过(`AudioSystem.Instance == null` → TryBind 不跑,关卡正常运行不受影响)。



---


## 与其他板块的关系

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [fire-pattern](./arch-fire-pattern.md) — FireSound 走 SfxCue
- [bullet](./arch-bullet.md) — BulletModifier 染色 / 命中可挂 SfxCue(可选)
- [player](./arch-player.md) — PlayerHealth / PlayerShooting 嵌入 SfxCue
- [boss](./arch-boss.md) — BossHealth 嵌入 SfxCue
- [level](./arch-level.md) — SpawnEntry/PlaySFX 与 LevelAudioBinding
- [Assets/Scripts/Audio/README.md](../../Assets/Scripts/Audio/README.md) — 本板块是架构视角;子系统 Inspector / SO 创建步骤见 Audio 子系统 README
