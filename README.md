# ShinySTG

一个 Unity 弹幕射击(STG)项目的脚手架,基于 **数据驱动 + 多态组合 + 行为流资产化** 的架构。

## ✨ 核心特性

- 🎯 **行为流资产化**:`BehaviorFlow` SO 把一段完整的敌人行为封装成可复用资产,多个敌人/boss 共享
- 🤖 **Boss 多阶段系统**:`BossController` + `BossPhase` + `BossSignal`,Inspector 自由组合符卡/非符/残血
- 💉 **多管血**:`BossHealth` 内置多管血机制,TakeDamage 自动切管
- 🧩 **可组合 Action**:`Parallel` / `Sequence` 容器支持无限嵌套,边移动边射击等复杂行为直接配置
- 🎨 **数据驱动**:`FirePattern` SO 系统(Ring/Line/Arc/Composite 等),改一个资产 = 改全场景
- 🔌 **多态下拉**:`SerializeReference` + 项目自带 SREditor,所有扩展点在 Inspector 里下拉选
- 🛩️ **玩家系统**:`Player` 主控 + 八方向 + Focus 低速 + 残机/复活无敌 + **活力阈值解锁的子机**,子机位置形态用 `OptionPositionForm` 多态下拉,主炮/子机开火同源同步

## 📐 架构说明

**详细的架构文档请见 [`ARCHITECTURE.md`](./ARCHITECTURE.md)**,包含:

- 子弹系统(BulletPool / Bullet / BulletModifier)原理
- 射击模式 SO 体系(FirePattern)及扩展方法
- 敌人 AI 时间轴(BehaviorFlow + EnemyAction)的三层架构
- Boss 系统(BossController + 多阶段 + 多管血)的全部细节
- 玩家系统(Player 主控 + 八方向 + Focus + 残机/火力/无敌 + 子机)
- 子机位置形态多态(OptionPositionForm)与扩展指南
- 内置 Action / MoveBehaviour / BossSignal 列表
- 如何新增 Action、MoveBehaviour、FirePattern、Modifier、BossSignal、BossPhase、子机位置形态
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
├── Bullet/                                   # 子弹 + 射击模式
│   ├── BulletPool.cs (+ BossShotCounter 钩子)
│   ├── FirePattern.cs (+ GetFireCount)
│   └── FirePattern/{Ring,Line,Arc,Composite}/...
├── Enemy/
│   ├── ShooterEnemy.cs                       # 行为流播放机(28 行)
│   ├── AI/                                   # 行为流 + 行为系统
│   │   ├── BehaviorFlow.cs                   # 行为流 SO 资产
│   │   ├── BehaviorFlowRuntime.cs            # 运行时驱动器
│   │   ├── EnemyAction.cs / MoveBehaviour.cs
│   │   ├── MoveBehaviours/LinearMove.cs
│   │   └── Actions/                          # Fire/Move/Wait/SelfDestruct/Parallel/Sequence
│   └── Boss/                                 # Boss 多阶段系统
│       ├── BossController.cs                 # 主驱动
│       ├── BossHealth.cs                     # 多管血
│       ├── BossShotCounter.cs                # 全局开火计数
│       ├── BossPhase.cs / PhaseTrigger.cs
│       ├── Phases/ShooterPhase.cs            # 行为流阶段(持 BehaviorFlow)
│       └── Signals/                          # BossSignal + 6 个内置信号
└── Player/                                   # 玩家系统(详见 ARCHITECTURE.md 第 8 章)
    ├── Player.cs                             # 主控单例 + 输入分发
    ├── PlayerMovement.cs                     # 八方向 + Focus 低速
    ├── PlayerShooting.cs                     # 主炮按住即喷(复用 FirePattern)
    ├── PlayerHealth.cs                       # 残机 + 火力级 + 复活无敌
    ├── PlayerHitbox.cs                       # 子物体判定点
    ├── PlayerOptions.cs                      # 子机系统(活力阈值 + 跟随 + 开火)
    ├── OptionPositionForm.cs                 # 子机位置多态抽象
    ├── OptionForms.cs                        # 3 个内置位置形态
    └── LegacyInputDriver.cs                  # 旧 Input.GetKey 兜底(可选)
```

## 🚀 快速上手

### 1. 场景准备

- 场景里创建一个 GameObject,挂 `BulletPool` 组件,设置 `DefaultPrefab`。
- (Boss 场景)另起一个 GameObject,挂 `BossShotCounter` 组件。

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

- 创建 boss prefab,挂 `BossHealth` + `BossShotCounter` + `BossController`。
- 在 `BossHealth.Bars` 配置多管血。
- 在 `BossController.Phases` 数组里下拉选 `Phase/Shooter`,把不同 .flow 资产拖到每个 phase 的 `Flow` 字段。
- 在 `BossController.Signals` 数组里下拉选内置信号(HP / Bar / Total / Phase Time / Shots)。
- 在每个 phase 的 `ExitTriggers` 数组里配退出条件(Signal + Op + Threshold)。

### 5. 创建 FirePattern 资产

```
Project 窗口右键 → Create → STG → FirePattern → Ring/Line/Arc/Composite
```

在 FireAction 里引用即可开火。

### 6. 玩家

- 创建一个 GameObject(命名 `Player`,自动设 tag 为 `Player`)
- Add Component → `Player`(它会自动 RequireComponent 加上 `PlayerMovement` / `PlayerShooting` / `PlayerHealth` / `PlayerOptions`)
- 选输入方案:
  - 装了 `com.unity.inputsystem` → 挂 `PlayerInput`,Action Asset 配 `Player` / `Attack` / `Focus`,Behavior = Invoke C# Events
  - 没装 → 挂 `LegacyInputDriver`(默认键:方向键/WASD 移动,Z/Space 开火,Left Shift Focus)
- `PlayerShooting.MainPatterns` 拖 1~N 个 FirePattern 资产
- `PlayerOptions.OptionPrefab` 拖一个子机视觉 prefab,`OptionFirePatterns` 拖各号子机弹幕
- 想换子机形态:`PlayerOptions.PositionForm` 下拉选 `Form/Touhou Symmetric` / `Linear Row` / `Rear Line`

**详细步骤、扩展指南、设计决策见 `ARCHITECTURE.md` 第 8 章。**

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

实际踩过的坑:

1. cmd 默认 GBK 代码页 + PowerShell 5.1 接收 UTF-8 字符串 → 中文路径乱码 / `找不到文件`。
2. cmd 在转发参数前对 `$` 做变量展开 → PowerShell 脚本里的 `$i` / `$_` 被吞。
3. 大段中文 + box-drawing 字符对齐宽度不可见 → 精确字符串匹配老失败,被迫改用 Python 正则做原子化替换。

**修通这些问题的成本 >> 绕过它们的成本**(落盘一个临时脚本即可)。代理看到中文 + 多行场景应当自觉落盘,不要硬塞。

