# 关卡系统

> LevelDefinition + SpawnEntry 多态

> 本板块对应 ARCHITECTURE § 9. 关卡系统(LevelDefinition + LevelController)(原 ARCHITECTURE.md 第 1120–1144 行)。
> 本文档面向项目维护者,不复制实现细节,字段 / 数值 / 默认值以源文件为准。

---

## 9. 关卡系统(LevelDefinition + LevelController)

负责按时间轴生成敌人并协调 Boss 遭遇。普通生成复用 prefab 自身行为，Encounter 通过 Boss 的公开生命周期接口协调阶段与演出。

**职责分工:**
- `LevelDefinition`(SO 资产) —— 持有 `Entries: SpawnEntry[]`(多态下拉)+ `Duration`(总时长)+ `Pool`(可选专用 BulletPool)+ `AutoSwitchBgm`(是否参与自动切歌)+ `AudioBinding`(关卡级 BGM 绑定,可选)。
- `LevelController`(场景单例,`Singleton<T>`) —— 持有 `Definition` + `Runtime`,提供关卡事件(OnLevelStart / OnLevelEnded / OnLevelComplete / OnEnemySpawned / OnBossSpawned / OnBossDefeated)。`BeginLevel` 调 `AudioEventHub.TryBind(definition)` 启用关卡级自动切歌。

**协作边界:**

- **职责边界**：普通生成由 prefab 驱动 AI；Encounter 持有动作运行器，通过 BossController 的等待接口协调战斗启动，通过 Boss 的保留标记协调击破后的销毁。
- **不侵入 Audio**:BGM 自动切换由 `AudioEventHub.TryBind` 触发,关卡只是数据源(Definition.AudioBinding),不直接调 `AudioMix.PlayTrack`。
- **总控统一挂载**:`LevelController` 场景里只挂一份。
- **事件发送权集中在 Controller**:`SpawnEntry` 子类不直接 Invoke 事件,而是通过 `LevelController.Instance` 的公开方法触发 —— 事件层与表现层分离。
- **数据驱动一致性**:`LevelDefinition.Entries` 与 `BehaviorFlow.Actions` 同样走 `[SerializeReference, SR]`,Inspector 下拉体验完全一致。
- **Boss 战以 Encounter 为边界**：Entry 生成 Boss、绑定 Encounter 并登记运行时过程后才通知 Boss 出场。Encounter 在资产内配置开场、阶段进入/退出、击破和完成动作，最终释放时间轴。

**扩展点:**

- **新条目类型**(等玩家到位 / 周期性 / 全清触发 / 概率触发 / **时间点 SFX** / ...):新建 `SpawnEntry` 子类 + `[SRName("Entry/<名字>")]`,在 `ShouldTrigger` / `OnTrigger` 两个钩子实现,无需改 `LevelController`(详见 `Assets/Scripts/Level/`)。
- **Boss Encounter 扩展**:血管、阶段、Signals 与演出统一配置在 `BossEncounterDefinition`,运行时协调放在 `BossEncounterRuntime`;旧 `BossSpawnEntry` 已弃用,不作为新配置入口，旧 prefab 战斗配置不再回退。
- **可视化时间轴编辑器**:已实现时间轴 / 列表 / 详情面板 + Preview + Scene Gizmos。详见 [关卡编辑器](./arch-level-editor.md)。
- **关卡级 BGM 自动切歌**:在 `LevelDefinition.AudioBinding` 挂 `LevelAudioBinding` 资产,`BeginLevel` 时 `AudioEventHub.TryBind` 自动订阅事件切歌。详见 [audio](./arch-audio.md)。


---


## 开局流程与本局状态

`StageSequenceDefinition` 配置有序关卡；角色选择和场景直开优先使用序列，未配置时沿用单关入口。开局校验整个列表，当前要求共享同一个 Gameplay 场景。
`GameStartRequest` 固定本局关卡顺序，但不复制关卡资产内容。`GameFlowController` 持有 `RunSession`，返回标题时清空；实时分数仍由玩家资源组件持有，本局结果仅保存快照。

### 结束与结算

`CompleteLevelEntry`（流程/关卡通关）只向所属 Runtime 登记请求，不直接访问场景单例。请求后停止条目与持续条目推进，但继续 Tick 已启动的运行时过程；已登记的阻塞过程全部结束后由 Controller 请求 Cleared，保留原有同帧失败优先与延迟结算规则。Reset 清除请求，Preview 的独立 Runtime 不影响真实关卡。同时间条目按数组顺序，通关条目之后的条目不再触发。

`LevelController` 区分成功通关 `Cleared`、残机耗尽 `Failed` 和主动中止 `Aborted`。整体 Duration 大于零且关卡时间达到上限，或通关条目的请求满足结束条件时，请求通关；所有条目播放完本身不是结束条件。`StageSettlement` 监听本次玩家的死亡通知请求失败；返回标题和背景调试停止请求中止。旧 `CompleteLevel()` 兼容为中止，不能用于判断通关。

通关和失败请求先停止时间轴推进，在下一帧发布结束事件；同帧最后一命耗尽优先于待定通关。中止立即定案，覆盖待定结果。定案后取消时间轴过程，每轮只发布一次。原 `OnLevelComplete` 保留给背景等生命周期订阅者，所有结束原因都会通知；结算使用带原因的 `OnLevelEnded`，不能将原事件等同于成功。

`StageSettlement` 由 `GameplayBootstrap` 在启动前绑定到本次玩家和关卡，关卡开始时记录分数基线。通关时以累计分数差值生成不可变的 `StageResult`，保存分数及玩家资源快照，写入 `RunSession.Results`；失败和中止不记录通关结果。不通过 Boss 击破事件直接结算，避免跳过 Encounter 收尾。

开始或调试重开会更新 `AttemptId`、清除待定结束，并重设分数基线、撤销当前关结果；前序关卡结果和玩家资源不重置。延迟结束回调应保存开始时的轮次，通过 `TryEndLevel(reason, attemptId)` 拒绝过期请求。销毁 Bootstrap 时解除结算订阅。

当前已完成序列首关启动、结束原因、内存结算和战斗限制／清场，尚未自动推进下一关或写入磁盘存档。失败不暂停游戏时间，死亡演出可以继续；Game Over 界面尚未接入。独立转场原型使用显式 BattleArea UI 矩形裁剪，不从相机视口推断主画面范围。

通关后的稳定状态是保留当前场景与玩家，完成清场并持有战斗限制；不会自动播放渐变、推进序列、展示整局结果或返回标题。`RunSession.TryAdvanceStage()` 目前只是进度接口，尚无正式流程调用。后续换关宿主需协调结算、转场、下一关准备和启动，再恢复战斗；末关需单独进入整局结束流程。

### 战斗限制与清场

结束定案时 Controller 持有 `BattleRestriction` 令牌，屏蔽射击、伤害、擦弹、拾取与新增掉落，停止敌人行为及新弹幕生成；存活玩家仍可移动和低速。它不修改游戏时间、玩家资源或对话控制锁。Bomb 当前只有库存，未来释放入口必须检查同一限制。

待定通关期间不提前限制受伤，保留同帧失败优先规则。结束通知不代表已经清场：`IsBattleCleanupPending` 为真时，Controller 在 Update 执行 `BattleCleanup`，避免结束回调发生在碰撞遍历中时修改池集合。清场取消关卡及场景独立 Encounter、结束持续条目、无奖励清除敌人和 Boss，再回收场景内各池的全部阵营子弹、激光与道具，也覆盖关卡显式指定的子弹池。普通死亡演出特效不清除。

重开先清场再释放 Controller 自己的令牌；销毁 Controller 同样释放，其他宿主的令牌不受影响。清场本身幂等且临时持有限制，取消回调不能生成新的弹幕或掉落。调用方必须在碰撞遍历之外使用清场／重开入口。渐变原型仍未接入正式换关流程。

配置及验证入口见 [开局流程](../../Assets/Scripts/GameFlow/README.md)，视觉原型见 [转场说明](../../Assets/Scripts/GameFlow/StageFadePrototype.md)。

## 背景接入

PlayBackgroundCueEntry 通过 LevelController.RequestBackgroundCue 发出一次性请求。Controller 校验真实 Runtime，避免预览操作真实背景；LevelBackgroundBinding 订阅请求和生命周期事件，将其转发到显式绑定的背景控制器。关卡开始或重开时重置背景，结束时取消演出并暂停。背景自身计时不依赖关卡 Elapsed，Encounter 阻塞时间轴不会冻结已启动的过渡。详见 [背景架构](./arch-background.md)。

## 对话接入

Boss 对话通过 Encounter 动作接入。BlockTimeline 控制整场遭遇是否阻塞关卡，WaitForCompletion 控制动作是否延迟阶段或收尾，两者职责不同。对话控制锁不暂停场上其他单位，音乐仍按现有出场/击败事件切换。

## 与其他板块的关系

- [dialogue](./arch-dialogue.md) — 对话播放及与战斗的协作边界。

- [game-actions](./arch-game-actions.md) — 统一 SR 动作配置与跨帧执行器。

LevelRuntime 先推进运行时过程，再判断时间轴阻塞，因此等待演出不会阻止 Encounter 自身 Tick。
死亡后的旧收尾延迟与击破动作并行等待，两者满足后运行 CompleteActions。
非等待动作不会延长遭遇生命周期；完成、取消、关卡重置或 Boss 意外消失会清理剩余动作。
阶段退出也会清理 Encounter 当前动作，包括尚未结束的非等待开场动作。
旧音效字段先执行，再启动对应动作，配置同一个音效两次会重复播放；Boss 配置应在 Encounter SO 中重建，不提供旧配置迁移。

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [enemy-ai](./arch-enemy-ai.md) — 生成的敌人 prefab 引用 BehaviorFlow 资产
- [boss](./arch-boss.md) — BossEncounter 注入独立配置实例，通过阶段等待接口和真实死亡协调战斗与收尾
- [level-editor](./arch-level-editor.md) — 关卡编辑器直接编辑 LevelDefinition.Entries
- [audio](./arch-audio.md) — SpawnEntry/PlaySFX 触发 SfxCue(由 AudioSystem 播放)
