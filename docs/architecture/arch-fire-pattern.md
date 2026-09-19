# 射击模式系统

## 职责与入口

FirePattern 资产定义弹幕形状与子弹配置；FireExtension 处理角度，BulletModifier 处理出生后的行为。
BulletPool 同时承担实例复用和当前发射调度。外部正常开火使用 FireGroup；Composite 子项使用 FireChild。
直接调用 Pattern.Fire 不构成完整发射事务，会绕过计数推进、根音效与统计，不应作为正常业务入口。

源码入口：[FirePattern](../../Assets/Scripts/Bullet/FirePattern.cs)、[BulletPool](../../Assets/Scripts/Bullet/BulletPool.cs)、
[运行状态](../../Assets/Scripts/Bullet/FirePatternRuntimeState.cs)。

## 配置与运行状态

带 FirePatternRuntimeState 的入口隔离批次计数和扩展运行实例。映射按配置扩展对象引用建立；
同一上下文重复引用同一扩展时共享实例，不同上下文得到不同实例。
Reset 清空计数与实例映射，后续使用重新 Clone。现有实例不会自动跟随 Inspector 中的配置修改，修改后应重置。

FireAction 每次进入和退出都会重置自己的状态，包括循环重新进入。当前没有“重入继续累加”的配置项。
玩家主炮、子机和母弹分裂仍使用无状态重载，共享池内计数并直接执行配置扩展；不要将局部隔离理解为全系统隔离。
ResetFireCounts 仅清共享计数，不重置独立上下文或共享扩展内部缓存。

FireExtension.Clone 默认浅拷贝。新增带可变引用的扩展必须深拷贝；新增带计时、抽样等运行字段的扩展必须显式重置。
NonSerialized 不会让 MemberwiseClone 自动清零。累加角度扩展已深拷贝抽样策略并清理自身运行字段。

## 批次与逐弹角度

Ring、Arc、Line 先获取运行扩展及对应计数，调用 PrepareBatch 准备位置偏移和批次抽样，再逐颗调用 ResolveBulletPipeline。
内置累加扩展的 BaseOffset 每次准备时抽样，逐弹求值复用抽样结果；OffsetPerBullet 叠加在逐弹管道中。
Ring/Arc 的几何等分偏移最后叠加，避免覆盖型 Base/PlayerAim 将弹幕形状抹去。

新增扩展必须让批次抽样发生在准备钩子中，逐弹函数只读取批次状态；否则随机调用次数会随弹数变化。
空扩展数组回退至向下加整体旋转。数组顺序有意义，覆盖型和累加型模块不能随意交换。

## Composite 语义

子项同步执行，共享当前上下文；同一子 Pattern 在多个槽位出现时按每次执行推进同一序列，并非每槽独立。
Composite 使用 Children 和根层 FireSounds，不继承父级子弹、速度、角度、Modifier、伤害或雾化配置。
调用方 ExtraModifiers 透传至叶子，由叶子与自身 Modifier 组合。
当前没有父级继承开关；父级扩展可能被调度计数，但不用于子项角度计算。

运行时仅检测当前递归路径，允许兄弟重复引用；循环及超过深度上限的分支跳过。
GetFireCount 使用同类保护返回配置预计数量；空 prefab、池获取失败等情况下预计值不等于实际生成数。
Count 非正的叶子不发射；单颗弹走中线，Ring/Arc 保留半径偏移。

## 音效与统计

FireGroup 在发射前触发根 Pattern 的 FireSounds。Composite 子项不会再次触发；空发射仍可播放意图音效。
成功 Get 的实例数量累计进当前发射组，根调用完成后提交一次实际弹数，避免 Composite 重复统计。
BossShotCounter 名称虽然含 Boss，但当前挂钩没有按发射者筛选：玩家、敌人、分裂的 FireGroup 都可能贡献计数。
需要“仅当前 Boss”的机制必须先增加归属过滤，不能直接依赖当前全局计数。

## 校验与边界

[Inspector](../../Assets/Scripts/Bullet/Editor/FirePatternInspector.cs) 提示循环/深度、空子项、弹数边界和重复扩展；提示不自动修复资产。
默认 Inspector 承担序列化编辑，新提示只读，不额外写入 dirty 状态。
同一个扩展对象在数组内重复出现，计数和准备钩子会重复执行，不保证仍然只抽样一次。

分裂 Extra 的 Synchronized 抽样是另一条路径：当前仅扫描根调用的额外 Modifier 中首个匹配项，
持久抽样标记也尚未按批次重置。它不能被视为所有默认 Modifier、子 Pattern 和跨批抽样都已正确覆盖。

## 相关文档

- [子弹系统](./arch-bullet.md)：复用、Modifier 与信号。
- [敌人行为](./arch-enemy-ai.md)：Action 生命周期。
- [Boss](./arch-boss.md)：阶段与信号。
- [架构评价报告](../reviews/bullet-architecture-review-2026-09-19.md)：当前风险、验证范围和改进顺序。
