# ShinySTG

一个 Unity 弹幕射击(STG)项目的脚手架,基于 **数据驱动 + 多态组合 + 行为流资产化** 的架构。

## ✨ 核心特性

- 🎯 **行为流资产化**:`BehaviorFlow` SO 把一段完整的敌人行为封装成可复用资产,多个敌人/boss 共享
- 🤖 **Boss 多阶段系统**:`BossController` + `BossPhase` + `BossSignal`,Inspector 自由组合符卡/非符/残血
- 💉 **多管血**:`BossHealth` 内置多管血机制,TakeDamage 自动切管
- 🧩 **可组合 Action**:`Parallel` / `Sequence` 容器支持无限嵌套,边移动边射击等复杂行为直接配置
- 🎨 **数据驱动**:`FirePattern` SO 系统(Ring/Line/Arc/Composite 等),改一个资产 = 改全场景
- 🧭 **FireExtension 多态扩展**:`FireExtensions` 是 FirePattern 上的 **模块数组 + Pipeline 模型**——按数组顺序串成"角度管道"(Base → PlayerAim → Offset Angle → ...),拼装出"基础方向 + 瞄准玩家 + 再叠 N°"等复杂逻辑(例:`[PlayerAim, Offset Angle(+180°)]` = 瞄向玩家但飞向玩家背后的"绕后弹")。Inspector 下拉选,新增 = 加一个 .cs,无需改任何现有 FirePattern 子类
- 🔊 **FireSound 开火音多态扩展**:`FireSounds` 是 FirePattern 上的**并行触发器数组**——与 FireExtension 的"角度管道"对仗,FireSound 是"每个模块独立播音"(可叠多 cue / 按状态发声 / 自定义行为)。走 SfxCue 体系(限流/Pipeline/Bus 全继承)。BulletPool.FireGroup 入口自动调一次,Composite 子 pattern 不重复触发。详见 [`ARCHITECTURE.md`](./ARCHITECTURE.md) §3.2 + [`Assets/Scripts/Audio/README.md`](./Assets/Scripts/Audio/README.md) §6.5
- 🌀 **BulletModifier 多态修饰**:子弹行为(加速 / 转向 / 减速 / 追踪 / 分裂 / **染色**)走 `[SerializeReference, SR]` 下拉配置,无需新建 prefab,纯 C# 类零 GC
- 🔌 **多态下拉**:`SerializeReference` + 项目自带 SREditor,所有扩展点在 Inspector 里下拉选
- 🛩️ **玩家系统**:`Player` 主控 + 八方向 + Focus 低速 + 残机/复活无敌 + **活力阈值解锁的子机**,子机位置形态用 `OptionPositionForm` 多态下拉,主炮/子机开火同源同步
- 🔊 **音频音乐系统**:`AudioMix` 静态门面 + `AudioSystem` 场景单例 + `SfxCue` / `BgmTrack` / `BgmPlaylist` / `AudioBus` SO 资产;SFX 多态规则 `SfxRule` 走 `[SerializeReference, SR]` 下拉(随机抽 clip / pitch 抖动 / cooldown);BGM 交叉淡化 + 顺序/随机播放;嵌入到 `PlayerHealth` / `BossHealth` / `EnemyHealth` / `PlayerShooting` 仅增加 `[SerializeField] SfxCue` 字段,其他模块 0 改动。详见 [`ARCHITECTURE.md`](./ARCHITECTURE.md) §11,配置方法见 [`Assets/Scripts/Audio/README.md`](./Assets/Scripts/Audio/README.md)
- 📘 **关卡可视化编辑器**(菜单 `STG → Level Editor`):时间轴 + 列表 + 详情面板 + Preview + Scene Gizmo,支持增/删/复制/撤销(`Ctrl+Z`)+ 快捷键,详见 [`LEVEL_EDITOR.md`](./LEVEL_EDITOR.md);架构见 [`ARCHITECTURE.md`](./ARCHITECTURE.md) §10

## 📐 架构说明

**详细的架构文档请见 [`ARCHITECTURE.md`](./ARCHITECTURE.md)**,包含:

- 子弹系统(BulletPool / Bullet / BulletModifier)原理(含 modifier 多态体系、美术朝向约定、Clone 深拷约定)
- 射击模式 SO 体系(FirePattern)及扩展方法(含 `SpawnBullet` helper 强制使用)
- FireExtension 扩展点(对基础发射逻辑的多态扩展,BaseAngle / 瞄准玩家 / 未来瞄准 Boss / 每发旋转 / ...)
- 敌人 AI 时间轴(BehaviorFlow + EnemyAction)的三层架构
- Boss 系统(BossController + 多阶段 + 多管血)的全部细节
- 玩家系统(Player 主控 + 八方向 + Focus + 残机/火力/无敌 + 子机)
- 子机位置形态多态(OptionPositionForm)与扩展指南
- 关卡系统(LevelDefinition + LevelController + SpawnEntry 多态)与扩展指南
- 关卡可视化编辑器子系统(LevelEditorWindow + Drawer / Preview / Gizmo 扩展点)
- 内置 Action / MoveBehaviour / BossSignal 列表
- 如何新增 Action、MoveBehaviour、FirePattern、Modifier、BossSignal、BossPhase、子机位置形态
- **音频音乐系统(AudioSystem)的数据资产 / 多态扩展 / 嵌入挂点**
- 11 个常见问题与设计决策记录

## 🛠️ 开发约定

**改代码前请阅读 [`CONTRIBUTING.md`](./CONTRIBUTING.md)**,包含:

- 编码规范(UTF-8 无 BOM / 命名 / Git 文件管理)
- 架构扩展入口(新增 Action / MoveBehaviour / FirePattern / BossSignal / 子机形态 等放哪里)
- 与 LLM 协作的约定(任务粒度 / 代码 review / 警惕实现层文档 / 任务结束清理清单)
- 工具链踩坑笔记(PowerShell 必须 `-File` / 临时脚本放 `Temp/` / UTF-8 BOM 触发 .meta 重生成 / 文档行号校验)

> ⚠️ 项目内的 AI 协作约定(shell 落盘、IDE 终端配置)详见下方 `🤖 AI 协作约定` 章节。

## 🗂️ 目录速查

```
Assets/Scripts/
├── Singleton.cs                              # 单例基类
├── Bullet/                                   # 子弹 + 射击模式(详见 ARCHITECTURE.md §2)
│   ├── BulletPool.cs (+ BossShotCounter 钩子)
│   ├── Bullet.cs                             # 飞行体 + modifier 调度
│   ├── BulletModifier.cs                     # 多态修饰基类 + Accelerate / Steer / Homing Enemy 内置
│   ├── BulletColorModifier.cs                # 视觉修饰:暗部染色 / 渐变 / 闪烁(Modifier/Color,配套 Shaders/BulletTint.shader)
│   ├── FirePattern.cs (+ GetFireCount + FireExtension 扩展点 + FireSound 扩展点)
│   ├── FireExtension/                          # 基础发射逻辑的多态扩展(详见 ARCHITECTURE.md §3.1)
│   │   ├── FireExtension.cs                    # 基类 + BaseAngleFireExtension / PlayerAimFireExtension 内置
│   │   ├── FireExtensionResolver.cs            # 静态 helper(Null-safe 解析中心方向)
│   │   └── FireSound.cs                        # 开火音多态基类 + SfxCueFireSound / NullFireSound 内置(详见 ARCHITECTURE.md §3.2)
│   └── FirePattern/{Ring,Line,Arc,Composite}/...
├── Hitbox/                                   # 统一 AABB + 网格索引(详见 ARCHITECTURE.md §8)
│   ├── HitboxComponent.cs                    # 通用 AABB 组件
│   ├── HitboxMath.cs                         # AABB×AABB 静态数学
│   ├── UniformGrid.cs                        # 均匀网格空间索引(Query3x3 / QueryRadius)
│   ├── CollisionService.cs                   # 场景单例 + 共享 Grid 维护
│   └── CollisionTeam.cs                      # Neutral/Player/Enemy 阵营
├── Enemy/
│   ├── ShooterEnemy.cs                       # 行为流播放机(28 行)
│   ├── AI/                                   # 行为流 + 行为系统
│   │   ├── BehaviorFlow.cs                   # 行为流 SO 资产
│   │   ├── BehaviorFlowRuntime.cs            # 运行时驱动器
│   │   ├── EnemyAction.cs / MoveBehaviour.cs
│   │   ├── MoveBehaviours/LinearMove.cs
│   │   └── Actions/                          # Fire/Move/Wait/SelfDestruct/Parallel/Sequence
│   └── Boss/                                 # Boss 多阶段系统(对齐 Enemy 子树风格)
│       ├── Boss.cs                            # 总控(对齐 Enemy.cs)+ [RequireComponent] 自动挂
│       ├── BossController.cs                  # 协调器:阶段 / Signal / Stop()
│       ├── BossHealth.cs                      # 多管血 + static Alive 池 + IHomingTarget
│       ├── BossHitbox.cs                      # HitboxComponent 子类(Team=Enemy)
│       ├── BossShotCounter.cs                 # 全局开火计数
│       ├── BossPhase.cs / PhaseTrigger.cs
│       ├── Phases/ShooterPhase.cs             # 行为流阶段(持 BehaviorFlow)
│       └── Signals/                           # BossSignal + 6 个内置信号
└── Player/                                   # 玩家系统(详见 ARCHITECTURE.md §7)
    ├── Player.cs                             # 主控单例 + 输入分发
    ├── PlayerMovement.cs                     # 八方向 + Focus 低速
    ├── PlayerShooting.cs                     # 主炮按住即喷(复用 FirePattern)
    ├── PlayerHealth.cs                       # 残机 + 火力级 + 复活无敌
    ├── PlayerHitbox.cs                       # 子物体判定点
    ├── PlayerOptions.cs                      # 子机系统(活力阈值 + 跟随 + 开火)
    ├── OptionPositionForm.cs                 # 子机位置多态抽象
    ├── OptionForms.cs                        # 3 个内置位置形态
    └── LegacyInputDriver.cs                  # 旧 Input.GetKey 兜底(可选)
└── Level/                                   # 关卡系统(详见 ARCHITECTURE.md §9)
    ├── LevelDefinition.cs                    # 关卡 SO 资产(Ctrl+N:Create → STG → Level)
    ├── LevelController.cs                    # 场景单例(继承 Singleton<T>)
    ├── LevelRuntime.cs                       # 运行时状态(时间推进 + 活跃单位追踪)
    ├── SpawnEntry.cs                         # 多态抽象([SerializeReference, SR])
    └── SpawnEntries/                         # 3 个内置条目类型
        ├── SimpleSpawnEntry.cs               # [SRName("Entry/Simple")] 时间+位置+单 prefab
        ├── WaveSpawnEntry.cs                 # [SRName("Entry/Wave")]   时间+中心点+多 prefab 自动铺
        └── BossSpawnEntry.cs                 # [SRName("Entry/Boss")]   时间+位置+boss prefab(留壳)
└── Audio/                                   # 音频音乐系统(详见 ARCHITECTURE.md §11;配置方法见 Assets/Scripts/Audio/README.md)
    ├── AudioMix.cs                           # 静态门面(唯一调用入口:PlaySfx/PlayTrack/SetBusVolume/...)
    ├── AudioSystem.cs                        # PersistentSingleton 总控 + AudioHelper
    ├── Sfx/
    │   ├── SfxRouter.cs                      # SFX 池 + 同 cue 限流(MaxVoices/Cooldown)+ 调度
    │   ├── SfxPlayer.cs                      # 池化 AudioSource 封装(支持跟随 Parent / 世界坐标)
    │   └── SfxRule.cs                        # 多态 Pipeline 基类 + 内置 RandomPick/PitchVariation/Cooldown 规则
    ├── Music/
    │   ├── MusicPlayer.cs                    # 交叉淡化 + Playlist 顺序/随机播放 + 暂停恢复
    │   ├── MusicChannel.cs                   # 单 BGM AudioSource(A/B 双通道交叉淡化)
    │   ├── BgmTrack.cs                       # SO:单首 BGM(Clip + 默认音量 + Loop + 路由)
    │   └── BgmPlaylist.cs                    # SO:BGM 列表(Tracks + Shuffle + Loop + CrossfadeDuration)
    ├── Mixer/
    │   ├── AudioBus.cs                       # SO:总线配置(兼容 AudioMixerGroup + PlayerPrefs 持久化)
    │   ├── AudioBusKind.cs                   # 总线枚举(Master/Bgm/Sfx/UI)
    │   └── BusMixer.cs                       # 总线音量管理 + PlayerPrefs
    ├── Bank/
    │   ├── SfxCue.cs                         # SO:单个 SFX(Clips + 限流参数 + Rules Pipeline)
    │   └── AudioBank.cs                      # SO:SfxCue 分组容器(可选,纯 Inspector 组织)
    └── Event/
        ├── AudioEventHub.cs                  # 关卡事件桥接(默认禁用;后续 LevelEditor 扩展 BGM 切换时启用)
        └── LevelAudioBinding.cs              # SO:关卡↔BGM 绑定(预留接口)
```

## 🚀 快速上手

### 1. 场景准备

- 场景里创建一个 GameObject,挂 `BulletPool` 组件,设置 `DefaultPrefab`。
- (Boss 场景)另起一个 GameObject(推荐命名 `BossShotCounter`),挂 `BossShotCounter` 组件。
  - 这是**场景级单例**(`Singleton<BossShotCounter>`),全场景一份。**不要**在 Boss prefab 上挂,否则同场景多 Boss 会互相污染 Instance。

### 2. 创建行为流资产

```
Project 窗口右键 → Create → STG → Behavior Flow
创建若干 .asset(如 "小怪基础移动.flow", "符卡A_攻击.flow")
```

每个 .flow 资产上配 `Actions` 数组(类型下拉选 Fire / Move / Wait / Parallel / ...)。

### 3. 普通敌人

- 创建敌人 prefab,挂 `ShooterEnemy`。
- 把 `.flow` 资产拖到 `ShooterEnemy.Flow` 字段。

### 4. Boss

- 创建 boss prefab,挂 `Boss` 总控(`[RequireComponent]` 自动加挂 `BossHealth` + `BossHitbox` + `BossController`,无需手填)。
- 在 `BossHealth.Bars` 配置多管血。
- 在 `BossController.Phases` 数组里下拉选 `Phase/Shooter`,把不同 .flow 资产拖到每个 phase 的 `Flow` 字段。
- 在 `BossController.Signals` 数组里下拉选内置信号(`HP %` / `Current Bar %` / `Current Bar Index` / `Total HP %` / `Phase Time` / `Shots Fired`)。
- 在每个 phase 的 `ExitTriggers` 数组里配退出条件(`BossSignal` + `Op` + `Threshold` 三元组);内置算子:`LessThan` / `LessOrEqual` / `Equal` / `GreaterOrEqual` / `GreaterThan`。
  - 推荐配置范式和 6 种 Signal 的适用场景详见 [`ARCHITECTURE.md`](./ARCHITECTURE.md) §5.1。
- **玩家弹可打 Boss**:`BossHitbox` Team=Enemy,CollisionService 走同 EnemyHealth 同构的查询路径;
- **追踪弹可锁 Boss**:`BossHealth` 实现 `IHomingTarget`,`HomingEnemyModifier` 统一识别。

### 5. 创建 FirePattern 资产

```
Project 窗口右键 → Create → STG → FirePattern → Ring/Line/Arc/Composite
```

在 FireAction 里引用即可开火。

**想让子弹加速 / 转向 / 追踪玩家?** 在 FirePattern 资产的 `Modifiers` 数组里点 `+`,下拉选 `Modifier/Accelerate` / `Modifier/Steer` / 自己写的 `Modifier/Homing Player` 等,直接在 Inspector 里设字段,无需新建任何 prefab。详见 [`ARCHITECTURE.md`](./ARCHITECTURE.md) §2.2。

### 6. 玩家

- 创建一个 GameObject(命名 `Player`,自动设 tag 为 `Player`)
- Add Component → `Player`(它会自动 RequireComponent 加上 `PlayerMovement` / `PlayerShooting` / `PlayerHealth` / `PlayerOptions`)
- 选输入方案:
  - 装了 `com.unity.inputsystem` → 挂 `PlayerInput`,Action Asset 配 `Player` / `Attack` / `Focus`,Behavior = Invoke C# Events
  - 没装 → 挂 `LegacyInputDriver`(默认键:方向键/WASD 移动,Z/Space 开火,Left Shift Focus)
- `PlayerShooting.MainPatterns` 拖 1~N 个 FirePattern 资产
- `PlayerOptions.OptionPrefab` 拖一个子机视觉 prefab,`OptionFirePatterns` 拖各号子机弹幕
- 想换子机形态:`PlayerOptions.PositionForm` 下拉选 `Form/Touhou Symmetric` / `Linear Row` / `Rear Line`

**详细步骤、扩展指南、设计决策见 `ARCHITECTURE.md` §7。**

### 7. 音频与音乐(详见 [`Assets/Scripts/Audio/README.md`](./Assets/Scripts/Audio/README.md))

- 场景里创建一个 GameObject(命名 `Audio`),Add Component → `AudioSystem`(它会自动 `PersistentSingleton`,跨场景保留音量)。
- 在 Project 窗口右键 → `Create → STG → Audio` 创建 `SfxCue` / `BgmTrack` / `BgmPlaylist` / `AudioBus` / `AudioBank` 资产。
- 把 SfxCue 拖到对应宿主的字段:
  - `PlayerHealth._hitSfx / _deathSfx / _grazeSfx / _powerUpSfx`
  - `BossHealth._hitSfx / _barDepletedSfx / _deathSfx`
  - `EnemyHealth._hitSfx / _deathSfx`
  - `PlayerShooting._shootSfx`
- 切 BGM:在场景脚本里调 `AudioMix.PlayTrack(track)` 或 `AudioMix.PlayPlaylist(playlist)`。
- 设总线音量:`AudioMix.SetBusVolume(AudioBusKind.Sfx, 0.7f)`。
- **FirePattern 开火音**:在 FirePattern 资产 Inspector 的 `Fire Sounds` 数组里点 `+` → 下拉选 `FireSound/SFX Cue` → 拖 SfxCue。可与 `PlayerShooting._shootSfx` 并存(后者是玩家整体开火音,前者是各 pattern 特征音)。详见 [`Assets/Scripts/Audio/README.md`](./Assets/Scripts/Audio/README.md) §6.5
- 留空字段 = 不播放,场景里没挂 `AudioSystem` = 调用静默返回,不报错。

**架构与扩展见 `ARCHITECTURE.md` §11;详细 Inspector 配置步骤见 [`Assets/Scripts/Audio/README.md`](./Assets/Scripts/Audio/README.md)。**

<!-- AI_SECTION_ANCHOR -->
## 🤖 AI 协作约定

> 📘 **本节是项目内置的速查版**;更完整的版本(任务粒度、代码 review 清单、工具链踩坑详解)见 [`CONTRIBUTING.md`](./CONTRIBUTING.md) §3 §4。

本仓库会与 AI 编码代理协作,为了避免代理在 shell 命令链路上踩坑,以下约定**作者与代理共同遵守**。

### 复杂命令请落盘脚本再执行

`powershell -Command "..."` / `cmd /c "..."` 在 Windows 上经过两层转发后,中文路径、`$` 变量、嵌套引号、多行脚本极易被 cmd 误吃、编码错乱、变量替换,出现"命令找不到 / 输出乱码 / 中文 NRE"。**因此**:

- ✅ **单行 ASCII 命令**(如 `dir`、`findstr`、`copy`):可以直接通过 shell 调用。
- ❌ **多行 / 含中文 / 含 `$` 变量 / 含嵌套引号**:请把命令写进 `.py` 或 `.ps1` 文件,然后执行该文件。
- ✅ **修改源码、文档**:用 IDE 编辑器直接改文件,不要试图用 sed/awk/PowerShell 在 shell 里就地修改——对齐宽度不可见字符会失真。

### 推荐 IDE / 终端配置

`.vscode/settings.json` 已预设:

- VS Code 默认终端 = PowerShell 7(`pwsh`),UTF-8 输出,无中文乱码。
- `PYTHONIOENCODING=utf-8`,确保 Python 脚本的中文 IO 不出错。
- 旧版 Windows PowerShell 5.1 仍能用,但中文 + 变量 + 嵌套引号场景务必落盘脚本。

### 为什么有这些约定

实际踩过的坑汇总在 [`CONTRIBUTING.md`](./CONTRIBUTING.md) §4(工具链踩坑笔记)。**修通这些问题的成本 >> 绕过它们的成本**(落盘一个临时脚本即可)。代理看到中文 + 多行场景应当自觉落盘,不要硬塞。

