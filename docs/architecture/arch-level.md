# 关卡系统

> LevelDefinition + SpawnEntry 多态

> 本板块对应 ARCHITECTURE § 9. 关卡系统(LevelDefinition + LevelController)(原 ARCHITECTURE.md 第 1120–1144 行)。
> 本文档面向项目维护者,不复制实现细节,字段 / 数值 / 默认值以源文件为准。

---

## 9. 关卡系统(LevelDefinition + LevelController)

负责把"什么时间点生成什么敌人"封装成可复用的 SO 资产,并在场景里按时间轴驱动执行。**与现有层完全正交** —— 不修改 Player / Enemy / Bullet 任何代码,仅复用 `ShooterEnemy` + `BehaviorFlow` + `BulletPool` 等既有资产。

**职责分工:**
- `LevelDefinition`(SO 资产) —— 持有 `Entries: SpawnEntry[]`(多态下拉)+ `Duration`(总时长)+ `Pool`(可选专用 BulletPool)+ `AutoSwitchBgm`(是否参与自动切歌)+ `AudioBinding`(关卡级 BGM 绑定,可选)。
- `LevelController`(场景单例,`Singleton<T>`) —— 持有 `Definition` + `Runtime`,提供关卡事件(OnLevelStart / OnLevelComplete / OnEnemySpawned / OnBossSpawned / OnBossDefeated)。`BeginLevel` 调 `AudioEventHub.TryBind(definition)` 启用关卡级自动切歌。

**协作边界:**

- **不侵入 Enemy/Bullet/Player**:关卡只 `Instantiate(prefab)`,prefab 自己带 `ShooterEnemy` + `BehaviorFlow` + Hitbox/Health,沿用既有 AI 链。
- **不侵入 Audio**:BGM 自动切换由 `AudioEventHub.TryBind` 触发,关卡只是数据源(Definition.AudioBinding),不直接调 `AudioMix.PlayTrack`。
- **总控统一挂载**:`LevelController` 场景里只挂一份。
- **事件发送权集中在 Controller**:`SpawnEntry` 子类不直接 Invoke 事件,而是通过 `LevelController.Instance` 的公开方法触发 —— 事件层与表现层分离。
- **数据驱动一致性**:`LevelDefinition.Entries` 与 `BehaviorFlow.Actions` 同样走 `[SerializeReference, SR]`,Inspector 下拉体验完全一致。
- **Boss 战以 Encounter 为边界**:`BossEncounterEntry` 启动一份 `BossEncounterDefinition`,由 Encounter 生成 Boss、监听阶段/死亡事件并在整场遭遇完成后释放时间轴。关卡层不直接编辑符卡内部演出。

**扩展点:**

- **新条目类型**(等玩家到位 / 周期性 / 全清触发 / 概率触发 / **时间点 SFX** / ...):新建 `SpawnEntry` 子类 + `[SRName("Entry/<名字>")]`,在 `ShouldTrigger` / `OnTrigger` 两个钩子实现,无需改 `LevelController`(详见 `Assets/Scripts/Level/`)。
- **Boss Encounter 扩展**:表现配置放在 `BossEncounterDefinition`,运行时协调放在 `BossEncounterRuntime`;旧 `BossSpawnEntry` 已弃用,仅用于已有资产反序列化。
- **可视化时间轴编辑器**:已实现完整的时间轴 / 列表 / 详情面板 + Preview + Scene Gizmos。详见 [§10](#10-关卡编辑器子系统)。
- **关卡级 BGM 自动切歌**:在 `LevelDefinition.AudioBinding` 挂 `LevelAudioBinding` 资产,`BeginLevel` 时 `AudioEventHub.TryBind` 自动订阅事件切歌。详见 [§11](#11-音频音乐系统audiosystem)。


---


## 与其他板块的关系

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [enemy-ai](./arch-enemy-ai.md) — 生成的敌人 prefab 引用 BehaviorFlow 资产
- [boss](./arch-boss.md) — BossEncounter 生成 Boss prefab,监听阶段进入/退出与真实死亡
- [level-editor](./arch-level-editor.md) — 关卡编辑器直接编辑 LevelDefinition.Entries
- [audio](./arch-audio.md) — SpawnEntry/PlaySFX 触发 SfxCue(由 AudioSystem 播放)
