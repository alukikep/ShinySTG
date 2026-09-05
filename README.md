# ShinySTG

一个 Unity 弹幕射击(STG)项目的脚手架,基于 **数据驱动 + 多态组合 + 行为流资产化** 的架构。

## ✨ 核心特性

- 🎯 **行为流资产化**:`BehaviorFlow` SO 把一段完整的敌人行为封装成可复用资产,多个敌人/boss 共享
- 🤖 **Boss 多阶段系统**:`BossController` + `BossPhase` + `BossSignal`,Inspector 自由组合符卡/非符/残血
- 💉 **多管血**:`BossHealth` 内置多管血机制,TakeDamage 自动切管
- 🧩 **可组合 Action**:`Parallel` / `Sequence` 容器支持无限嵌套,边移动边射击等复杂行为直接配置
- 🎨 **数据驱动**:`FirePattern` SO 系统(Ring/Line/Arc/Composite 等),改一个资产 = 改全场景
- 🔌 **多态下拉**:`SerializeReference` + 项目自带 SREditor,所有扩展点在 Inspector 里下拉选

## 📐 架构说明

**详细的架构文档请见 [`ARCHITECTURE.md`](./ARCHITECTURE.md)**,包含:

- 子弹系统(BulletPool / Bullet / BulletModifier)原理
- 射击模式 SO 体系(FirePattern)及扩展方法
- 敌人 AI 时间轴(BehaviorFlow + EnemyAction)的三层架构
- Boss 系统(BossController + 多阶段 + 多管血)的全部细节
- 内置 Action / MoveBehaviour / BossSignal 列表
- 如何新增 Action、MoveBehaviour、FirePattern、Modifier、BossSignal、BossPhase
- 11 个常见问题与设计决策记录

## 🗂️ 目录速查

```
Assets/Scripts/
├── Singleton.cs                              # 单例基类
├── Bullet/                                   # 子弹 + 射击模式
│   ├── BulletPool.cs (+ BossShotCounter 钩子)
│   ├── FirePattern.cs (+ GetFireCount)
│   └── FirePattern/{Ring,Line,Arc,Composite}/...
└── Enemy/
    ├── ShooterEnemy.cs                       # 行为流播放机(28 行)
    ├── AI/                                   # 行为流 + 行为系统
    │   ├── BehaviorFlow.cs                   # 行为流 SO 资产
    │   ├── BehaviorFlowRuntime.cs            # 运行时驱动器
    │   ├── EnemyAction.cs / MoveBehaviour.cs
    │   ├── MoveBehaviours/LinearMove.cs
    │   └── Actions/                          # Fire/Move/Wait/SelfDestruct/Parallel/Sequence
    └── Boss/                                 # Boss 多阶段系统
        ├── BossController.cs                 # 主驱动
        ├── BossHealth.cs                     # 多管血
        ├── BossShotCounter.cs                # 全局开火计数
        ├── BossPhase.cs / PhaseTrigger.cs
        ├── Phases/ShooterPhase.cs            # 行为流阶段(持 BehaviorFlow)
        └── Signals/                          # BossSignal + 6 个内置信号
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

**详细步骤、扩展指南、设计决策见 `ARCHITECTURE.md`。**
