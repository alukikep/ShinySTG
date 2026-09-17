# ShinySTG 工程进度

最简单的清单:每条一行,只说做了什么 / 要做什么。
要看架构 / 字段 / 怎么扩展 → `ARCHITECTURE.md`。
要看编码规范 / AI 协作 → `CONTRIBUTING.md`。
要看关卡编辑器怎么用 → `LEVEL_EDITOR.md`。

---

## 已实现

### 子弹系统
1. 子弹 prefab + 对象池(`BulletPool` 按 prefab 分桶复用)
2. `Bullet` 总控(阵营透传 / 自动挂载 modifier / 朝向约定)
3. `BulletModifier` 多态(加速 / 转向 / 追踪 / 染色 ...)+ 时间窗口(Delay / Duration / OneShot)
4. 染色 modifier 走 MaterialPropertyBlock(不破坏 batching)

### 激光系统(东方风格直线 / 曲线,PR1)
1. `LaserEntity` 主控(五段式状态机 `Warning → Expanding → Active → Shrinking → Dead`)+ 直线 / 曲线模式 + Velocity / AngularVelocity 运动
2. `LaserGeometry` 纯函数数学(点到线段距离平方 `DistanceSqPointToSegment` + 曲线分段 `CheckCurvedHit` / `CheckCurvedGraze`)
3. `LaserData` SO 资产(VisualWidth / CollisionWidth 视觉判定分离 + 四段时间 + 碰撞策略 + 贴图)
4. `LaserPool` 对象池(对齐 BulletPool:按 Data 分桶 / 懒扩容 / SourceData 路由回池 / `FireGroup` 中心化入口)
5. `LaserService` 中心化碰撞(独立于 CollisionService,阵营过滤 + 出界回收 + 擦弹事件)
6. `LaserPattern` SO 基类 + `StraightLaserPattern` 内置实现(SR 多态 modifier + FireSounds 复用)
7. `LaserRendererBase` 视觉抽象 + `SpriteStretchLaserRenderer` 默认实现(Body 拉伸 + Head + Warning 预警线)
8. `LaserModifier` 修饰器抽象基类(PR1 最小钩子,完整 Delay/Duration 体系留 PR3)
9. `FireLaserAction` `[SRName("Action/Fire Laser")]` 接入 BehaviorFlow,与 `FireAction` 完全对仗
10. 视觉宽度 / 判定宽度分离(参考材料 §6,屏幕 0.8 / 实际 0.15)+ 距离平方碰撞 + 阵营透传复用 CollisionTeam
11. 出界判定复用 `BoundsService.ContainsCulling`,无敌判定复用 `PlayerHealth.IsInvincible`
12. 全部 12 个新文件 / ~1100 行,UTF-8 无 BOM,通过文件层 + API 互调用层两层验证
13. PR1 修复 1 轮 + 修复 2 轮的根因 / 修复细节 / 自检清单已迁移到 [`CONTRIBUTING.md`](./CONTRIBUTING.md) §4.9(对象池复用 + transform 字段残留污染)

### 射击模式
1. 4 个内置 FirePattern(Ring / Line / Arc / Composite)
2. `FireExtension` 角度管道(基础方向 / 瞄准玩家 / 叠角度 / ...)
3. `FireSound` 开火音(SfxCue + Pipeline)
4. `SpawnFog` 出生雾化(None / Default)

### 敌人 AI
1. `BehaviorFlow` SO 资产化
2. 6 个内置 `EnemyAction`(Fire / Move / Wait / SelfDestruct / Parallel / Sequence)
3. 9 个内置 `MoveBehaviour`(Linear / Accelerate / Bezier / Circular / Homing / Patrol / Ease / Sine / **RandomWalkInRegion**)
4. `Parallel` / `Sequence` 容器无限嵌套

### Boss
1. 多管血 + 自动切管 + `OnBarDepleted` / `OnDeath` 事件
2. 多阶段 + `ExitTriggers`(SignalIndex + Op + Threshold)
3. 6 个内置 `BossSignal`(HP% / Bar% / BarIndex / TotalHP% / PhaseTime / ShotsFired)
4. `ShooterPhase` 阶段体(复用 BehaviorFlow)
5. 死亡收尾单一路径(防重入 + 自动 Destroy)
6. `BossShotCounter` 场景级单例

### 玩家
1. 八方向移动 + Focus 低速
2. 残机 / 火力级 / 复活无敌
3. 主炮 + 子机(`PlayerShooting` / `PlayerOptions`)
4. 子机位置多态(`OptionPositionForm`:对称 / 横排 / ...)
5. 擦弹计数(Graze)
6. 两种输入方案:`LegacyInputDriver`(键盘)或 `PlayerInput`(InputSystem)

### Hitbox / 碰撞
1. 数学 AABB(不走 Physics2D)
2. `UniformGrid` 空间分桶
3. Scene 视图可视化
4. 阵营语义(玩家弹按 Damage 扣血 / 敌人弹扣 1 命)
5. `IHomingTarget` 接口(Boss / Player)

### 关卡系统
1. `LevelDefinition` SO 资产
2. `LevelRuntime` 按时间轴驱动
3. 5 个正式 `SpawnEntry`(Simple / Wave / Sustain / Boss Encounter / PlaySFX);旧 `BossSpawnEntry` 仅保留资产兼容
4. 2 个内置 `SpawnPositionStrategy`(Fixed / Random)
5. 关卡事件(OnLevelStart / OnLevelComplete / OnBossSpawned / OnBossDefeated)
6. `BulletPool` 自动兜底

### 关卡可视化编辑器
1. 菜单 `STG → Level Editor`
2. 时间轴 + 列表 + 详情面板 三视图
3. 增 / 删 / 复制 + `Ctrl+Z` 撤销
4. 持续型 entry 自动堆叠 lane
5. 右边缘拖动改 Duration
6. Preview(`LevelEditorPlayer` 不依赖运行时)
7. Scene Gizmo
8. `+ Create AudioBinding` 一键创建 + 反引用
9. 对运行时的影响 = 0(独立 Editor asmdef)

### 音效系统
1. 播放(静态门面 `AudioMix.PlaySfx` / `PlayTrack` / `PlayPlaylist`)
2. 创建 SO(SfxCue / BgmTrack / BgmPlaylist / AudioBus / AudioBank / LevelAudioBinding)
3. SFX 多态规则(`SfxRule` Pipeline:RandomPick / PitchVariation / Cooldown)
4. BGM 交叉淡化 + 顺序 / 随机播放
5. 总线音量 + PlayerPrefs 持久化
6. 嵌入 Player / Boss / Enemy / PlayerShooting(零侵入,只增字段)
7. 时间暂停时自动停 SFX + Pause BGM

### SREditor 插件
1. `[SerializeReference]` 多态下拉菜单
2. 分类 / missing type 检测 / 折叠

### 内容资产
1. 1 个场景(`SampleScene`)
2. 4 个 FirePattern SO 模板
3. 3 个 SfxCue 占位
4. Player SO ×3(NatsuhaA 系列)
5. Enemy / Boss SO ×6
6. Stage test 草稿 + AudioBinding

---

## 待办(勾选式)
目前反魂蝶有部分逻辑有补下问题，比如发射弹幕重叠
### 子弹系统
- [ ] modifier信号触发系统(`ModifierStartTrigger` SR 多态 + `BulletSignalBus` + `EmitSignalAction`)的 Duration 职责仍有怪异之处,记得排查
  - 历史决策、已实现的多态字段见 [ARCHITECTURE.md](./ARCHITECTURE.md) §2.6 / §2.8

### 激光系统
- [ ] PR2: 完整状态机视觉(预警线显隐 + alpha 渐变 + 头尾发光)
- [ ] PR3: `CurvedLaserPattern` 曲线激光 + 完整 `LaserModifier` Delay/Duration 体系 + `LaserRotateModifier` 等实例
- [ ] PR4: 多 Renderer 后端(`LineRendererLaserRenderer` / `MeshLaserRenderer`)+ 激光性能优化(独立稀疏网格)+ 关卡事件集成
- [ ] (可选) 激光 modifier 接入 `BulletSignalBus`(`StartTrigger` SR 多态,照 BulletModifier 套路)

> 其余子系统(射击模式 / 敌人 AI / Boss / 玩家 / Hitbox / 关卡 / 关卡编辑器 / 音效 / 内容资产 / UI / 存档 / 测试 CI)目前无明确进行中任务;新增任务时按本节模板补条目即可。
