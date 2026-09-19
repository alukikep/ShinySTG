# 弹幕架构评价报告

评估日期：2026-09-19。本文覆盖上一版评价，基于当前工作区代码及本轮优化结果。
范围：BulletPool、Bullet、FirePattern、FireExtension、Modifier、信号、Composite、运行状态和主要调用入口。

## 当前结论

可以继续关卡、符卡和弹幕行为开发。目前没有发现必须整体重写才能继续开发的架构问题。
Pattern 描述形状，FireExtension 处理发射方向，Modifier 处理出生后的行为，运行状态与配置模板分离，整体分层能够支持当前功能扩展。

本轮已修复信号退订与派发问题，并减少材质实例和发射准备阶段的重复分配。下一阶段以功能开发为主，性能调整由实际场景和 Profiler 数据驱动。这个结论不等于不存在缺陷，也不构成最大弹量或帧率保证。

## 验证依据与边界

| 项目 | 当前依据 |
|---|---|
| 前期集成验收 | 用户反馈测试、验收基本完成；未附逐项 Test Runner 记录，不推定每个边界案例均通过 |
| 信号派发快照修复 | 用户已明确反馈验收完成 |
| 最新优化代码 | 运行时代码和新增测试源码通过独立 C# 编译；这不等同于 Unity 编辑器编译与运行验收 |
| 新增回归测试 | 已补充委托退订、缓存推进与重置、共享材质生命周期测试；本轮未在 Unity Test Runner 中执行 |
| 改动检查 | 本轮优化的 git diff --check 通过 |
| 性能 | 尚无优化前后 Profiler 对照数据，不宣称零 GC 或确定的性能提升幅度 |

## 已形成的基础

- BulletPool 按 prefab 分桶，Return 有活跃成员保护；复用会恢复基础视觉、雾化、阵营、擦弹和 Modifier 状态。
- 碰撞延迟回收使用 SpawnVersion，避免旧回收请求误伤已复用实例。
- FireAction、玩家主炮、子机和母弹分裂使用各自的运行状态；无显式状态的 FireGroup 仍使用池内共享状态。
- 运行扩展通过 Clone 创建；累加角度扩展深拷贝抽样策略并重置运行字段。
- Ring、Arc、Line 使用批次准备和逐弹解析；批次抽样与逐弹角度推进分开。
- Composite 允许兄弟重复引用，当前递归路径阻止循环并限制深度；根音效由 FireGroup 触发。
- 发射计时保留余量并限制单帧补发；全局 Boss 发射计数模块已移除。

## 本轮优化结果

### 信号生命周期

Emit 复制订阅者数组作为本次派发快照，回调内订阅或退订不会改变本次遍历集合。新订阅者从后续 Emit 开始参与；本次快照内已退订的回调仍可能被调用一次。派发顺序不作为业务契约。

Unsubscribe 使用委托相等性而非引用同一性，使重新构造的同一对象方法委托能够正常退订，避免子弹回收后留下订阅。

### 材质管理

默认 Sprites/Default 材质使用共享 BulletTint fallback，不再为每颗子弹创建独立兜底材质。inactive 池对象仍计为使用者，最后一个使用者销毁时释放 fallback。

已有 BulletTint 及其他自定义 shader 材质保留原配置。自定义材质需要自行支持所需的染色与雾化属性；共享材质本身也不保证具体渲染管线一定合批。

### 发射准备缓存

FirePatternRuntimeState 按配置数组缓存运行扩展数组和计数映射，Reset 清空映射和运行实例。首次使用及 Reset 后重建仍有分配；运行中修改配置数组或元素后必须重置状态。

计数映射属于可更新缓存，只供同步批次准备读取，不能当作历史快照保存。不同发射者应持有不同状态，不能把共享缓存视为任意重入或并行执行安全的保证。

批次抽样字典在 FireGroup 结束后清空并复用；嵌套调用使用独立借出的字典，finally 恢复外层状态和抽样上下文。

### 无用统计清理

删除了没有读取方的 _groupSpawnCount。当前 FireGroup 不提供实际生成总数结果；GetFireCount 仍是配置预估，ActiveBullets.Count 是当前存活量，两者都不能直接视为任意复杂发射事务的实际生成总数。

## 扩展时必须保持的契约

- 正常业务发射通过 BulletPool.FireGroup；Composite 子项通过 FireChild，直接 Pattern.Fire 会绕过完整入口行为。
- Composite 不继承父级子弹、速度、Modifier 等配置；调用方 ExtraModifiers 透传到叶子。
- 配置模板只描述配置，运行字段由发射者或子弹实例持有。MemberwiseClone 不会因为 NonSerialized 标记而自动清零状态。
- 新增可变引用字段时显式深拷贝；对象池复用时重置全部可变状态，订阅必须成对释放。
- Synchronized 分裂抽样在当前根发射批次内复用；Independent 按母弹实例抽样。批次抽样不能搬进逐弹角度求值函数。
- 修改运行中的扩展配置后调用 Reset；缓存返回值不得被外部修改或长期当作快照持有。

## 剩余风险与处理时机

| 项目 | 判断与建议 |
|---|---|
| 最新优化的 Unity 验收 | 发布或合入前执行新增测试，确认染色、雾化、自定义材质、关卡反复进出与 Console 状态 |
| 同状态的同步重入 | 字典隔离不等于扩展实例隔离；自定义回调若在批次准备或逐弹求值中重入同一状态，需明确语义并针对该调用链验证 |
| 高频信号与全屏消弹 | Emit 快照数组、ReturnAll 快照列表仍有分配；有实际尖峰再优化，避免为消除分配牺牲派发正确性 |
| 密集弹幕 CPU 与渲染 | 每颗 Bullet 仍走 MonoBehaviour.Update；先测密集环、多 Modifier、持续分裂，再决定是否集中更新 |
| 状态持有时间 | 长寿命状态会保留使用过的配置映射；动态替换大量 Pattern 时需在生命周期边界 Reset |
| 浅拷贝扩展契约 | 新功能最容易重新引入共享状态，代码审查优先检查 Clone、重置和订阅生命周期 |
| 严格回放 | Unity 随机源与 deltaTime 尚不是确定性模拟；有回放需求时再规划统一随机源和步进时钟 |

## 下一阶段建议

完成最新优化的短回归后，继续功能开发，不以再次全面重构为前置条件。新增功能优先复用现有 Pattern、Extension、Modifier 扩展点，并针对新增的生命周期和组合行为补充必要测试。

首次达到目标弹量时保存性能基线：目标设备、分辨率、帧时间、GC Alloc、活跃弹数、材质实例数，以及首次出弹和全屏消弹峰值。仅在数据表明存在瓶颈时推进下一轮优化。

## 代码与参考

- [BulletPool](../../Assets/Scripts/Bullet/BulletPool.cs)
- [Bullet](../../Assets/Scripts/Bullet/Bullet.cs)
- [BulletSignalBus](../../Assets/Scripts/Bullet/BulletSignalBus.cs)
- [FirePatternRuntimeState](../../Assets/Scripts/Bullet/FirePatternRuntimeState.cs)
- [回归测试](../../Assets/Scripts/Bullet/Editor/FirePatternRuntimeStateTests.cs)
- [子弹架构](../architecture/arch-bullet.md)
- [发射模式架构](../architecture/arch-fire-pattern.md)

部分架构说明仍含历史表述，例如共享状态覆盖范围、旧抽样路径和实际计数提交；遇到不一致时应核对当前源码，不将旧说明视为本轮已验证事实。
