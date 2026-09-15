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
3. 5 个内置 `SpawnEntry`(Simple / Wave / Sustain / Boss / PlaySFX)
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

### 子弹系统
- [ 
    modifier信号触发系统还有问题，duration职责非常怪异，记得排查
]

### 射击模式
- [ ]

### 敌人 AI
- [ ]

### Boss
- [ ]

### 玩家
- [ ]

### Hitbox / 碰撞
- [ ]

### 关卡系统
- [ ]

### 关卡可视化编辑器
- [ ]

### 音效系统
- [ ]

### 内容资产
- [ ]

### UI / 主菜单 / 暂停 / 计分
- [ ]

### 存档 / 排行榜 / 设置菜单
- [ ]

### 测试 / CI
- [ ]