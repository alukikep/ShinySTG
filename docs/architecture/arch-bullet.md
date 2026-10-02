# 子弹系统

> BulletPool、Bullet、Modifier、反弹与信号的协作契约。
> 配置字段、默认值和方法签名以源码及 Inspector 为准。

## 职责与入口

- [BulletPool](../../Assets/Scripts/Bullet/BulletPool.cs) 按 prefab 分桶复用子弹，负责克隆并挂载 modifier、透传阵营和管理根发射批次。
- [Bullet](../../Assets/Scripts/Bullet/Bullet.cs) 管理飞行、雾化、视觉基准、碰撞入口和 modifier 生命周期。
- [BulletModifier](../../Assets/Scripts/Bullet/BulletModifier.cs) 是通过 `SerializeReference` 配置的纯 C# 类型，统一管理启动、时间窗口与脱离清理。

具体行为位于 `Assets/Scripts/Bullet/Modifiers/`，启动条件位于 `Triggers/`，
母弹到子弹的信息传递及采样策略位于 `Extras/`。每个主要类型独立成文件。
移动文件时保留原 `.meta`；保持类型名、命名空间、程序集和序列化字段不变，避免破坏存量资产。

配置有两层：FirePattern 的默认 modifier，以及 FireAction 的追加 modifier。
追加项排在默认项之后；数组按顺序执行，并非独立并行。多个行为写同一属性时要明确覆盖关系。

调用方通过 BulletPool 获取子弹或触发 FirePattern。子弹阵营从发射者透传，
不依赖 prefab 预设；中性子弹不参与常规敌我伤害。玩家弹伤害读取 Damage，敌弹命中玩家按玩家受击规则结算。

## 飞行与调度

代码中角度零指向世界坐标 +X，飞行角以弧度存储。贴图默认朝 +Y，显示旋转统一补偿 -90°。

雾化期间子弹不移动、不参与碰撞，也不推进 modifier 时间窗口。雾化结束帧只把剩余时间交给飞行逻辑。
因此时间型 modifier 从雾化结束后累计，与 Bullet 的累计存活时间不同；雾化中收到的信号可先锁存。
挂载钩子不受雾化等待影响，消弹防御等常驻属性在首帧更新前已经生效。

正常飞行帧先依次运行 modifier，再应用转角、移动并处理越界。
modifier 执行期间若子弹被回收或同一实例已重新生成，旧一轮 Update 停止继续处理。
移动采用当前速度和方向按帧更新；时间窗口裁剪不等于对整条运动轨迹进行分段积分。

`OrbitBulletModifier` 是位置型运动 modifier，配置入口为 `Modifier/Orbit`。`Mode=FixedPosition`
时围绕 `FixedCenter` 做圆周运动；`Mode=FiringEnemy` 时围绕发射者 Transform 做圆周运动。
两种模式都只需设置 `AngularSpeed`（度/秒，正值逆时针），半径在 modifier 首次生效时按
子弹当前位置到圆心的距离自动计算。`RadiusBehavior=Initial` 保持该兼容行为；也可选择
`Constant` 使用指定半径，或选择 `Linear` / `Curve` 在 `RadiusDuration` 内从初始半径变化到
`TargetRadius`。半径每帧重算但最终位置仍由 Orbit modifier 统一提交，避免多个位置 modifier
之间产生隐式覆盖。发射者引用沿 FirePattern → BulletPool → Bullet 传递，
并在回池时清理。若发射者已销毁，`FiringEnemy` 模式会直接请求回收子弹，不会退回固定坐标。

内置 Steer 与 Homing 通过 `SetModifierTurn` 提交按有效时长计算的本帧转角，
后调用的转向覆盖之前的提交。没有提交时，Bullet 才按 AngularSpeed 对整帧积分。
这保证窗口末帧的转角不会被退出清理吞掉，也不会因使用整帧时长而多转。
自定义转向行为应沿用这个入口，避免混用直接写 AngularSpeed 与提交转角造成覆盖歧义。

## 克隆、重置与回收

每颗子弹持有独立的 modifier 运行实例，FirePattern 中的配置实例不应被运行逻辑修改。
Clone 仍会产生对象分配，子弹 GameObject 复用并不表示 modifier 已池化。

`MemberwiseClone` 会复制运行状态，`NonSerialized` 仅控制序列化，不会清零克隆结果。
基类 Clone 深拷 StartTrigger 后调用 ResetWindow；子类通过 OnResetWindow 显式重置计时、目标、缓存等状态。
可变集合、自定义引用对象由子类 override Clone 深拷；只读 Unity 资产引用可以共享。
重置钩子不得修改浅拷过来的共享对象，否则可能污染原实例。

ResetWindow 用于未挂载实例。运行中的实例应先 Detach；它不会替调用方重新订阅 trigger。
Modifier 的 OnAttach 负责挂载即生效的属性，与时间窗口进入及 trigger 的 OnAttach 分开。
Clone 重置运行状态，AddModifier 执行挂载钩子；标准池路径随后统一重置窗口、挂载启动触发器。
重置窗口不得解除已登记的常驻属性；常驻 Modifier 不推进自身时间窗口，也不订阅启动触发器。

ClearModifiers 会先对各 modifier 调用幂等的 Detach，再清空列表。
Detach 解除 trigger 订阅；若窗口仍激活则先退出窗口，随后调用子类 OnDetach。
已经结束的窗口不会重复退出，尚未启动的窗口不会伪造退出事件，但两者都需要脱离清理。

窗口退出与脱离是不同责任：

- OnWindowExit 默认只转交 OnWindowExitCleanup，不修改速度、转向或颜色。
- Steer、Homing 自行清理角速度；染色、加速、分裂结束不会清除其他行为的角速度。
- OnDetach 用于最终释放引用或资源，即使 modifier 从未激活也会调用。

回池通过 ResetForPool 执行上述清理，并恢复缩放、材质属性块、雾化、擦弹及运动状态。
销毁路径也通过 ClearModifiers 兜底。`Return` 不检查普通消弹防御，但遵守“仅强消弹回收”限制；
`ReturnAll` 是关卡生命周期的强制清场入口，不受这两种保护限制。不要直接禁用子弹对象代替回收。
重复 Return 不会重复入池。

## 玩法消弹与防御

玩法消弹使用 BulletPool.ClearAll，先按阵营筛选，再按消弹等级检查防御。
一级（Normal）仅消除无防御子弹；二级（Strong）同时消除普通弹和防御弹。
被一级消弹保留的防御弹不消耗防御、不改变运动、碰撞或伤害状态。

[ClearDefenseBulletModifier](../../Assets/Scripts/Bullet/Modifiers/ClearDefenseBulletModifier.cs)
通过挂载钩子登记防御，因此根 Modifier 从生成首帧起生效，包括雾化期间。
它不使用自身 Timing，脱离时解除防御；Bullet 按运行实例登记来源，多个防御来源不会相互覆盖。
回池完整清空防御来源，克隆与配置模板不共享防御运行状态。
容器内的防御从子节点阶段开始生效，阶段结束或容器脱离时解除；分裂弹按自身配置获得防御。
OnlyStrongClear 是防御 Modifier 的可选回收限制，默认关闭。开启后，越界、命中回收及
Modifier 的 RequestReturn 均不能回收子弹；被阻止的请求会清除，不打断后续运动。
越界时仍先尝试 Bounce，未反弹时保留当前位置继续飞行，不自动夹回场内。
二级 ClearAll（含强消弹指令和二级 Bomb）绕过该限制；关卡 ReturnAll 和场景销毁仍完整清理。
限制与防御来源绑定，来源脱离或容器阶段结束时解除，回池后不残留。保留的子弹仍参与常规碰撞伤害，
只是命中后不回收；单颗带表现的普通 Return 被阻止时也不生成消弹表现。

ClearAll 与带表现的 ReturnAll 共用批量表现路径，但只有通过玩法筛选的子弹参与消弹表现。
`Silent` 直接回池，`BurstEffect` 按世界网格聚合特效，`ConvertToItems` 按成功消除数量比例生成有限道具。
表现配置有最大特效数（运行时硬上限 128）和最大道具数（运行时硬上限 256），超出部分只回收不追加表现；
缺少特效、道具或场景服务时自动退化为静默回收。`Return` / `ReturnAll(filter)` 保持静默模式。
等级只作用于 Bullet，激光仍由调用方的阵营筛选与 IncludeLasers 控制。
指令、Bomb 和演出配置见[通用游戏动作操作说明](../../Assets/Scripts/GameActions/README.md#最小配置)。

## 时间窗口

启动时刻与持续时间是两个独立概念。StartTrigger 决定何时首次启动，Duration 从激活后计算，
重复信号不会重启或延长窗口。StartTrigger 为空时，在第一次有效更新立即进入窗口；
需要延迟时显式配置 DelayStartTrigger，不创建兜底 trigger。

跨过延迟阈值的首帧只计入阈值后的时间；Duration 末帧只计入剩余时长。
ModifyCore 接收裁剪后的有效 dt，结束钩子在最后一次有效执行之后调用。
时间型 trigger 通过 GetActivationDelta 裁剪首帧；信号没有亚帧时间戳，按检测帧生效。
自定义 trigger 默认获得检测帧全部时间，需要精确时间阈值时自行实现该裁剪钩子。

Duration 非正表示持续生效。无效 dt 或暂停不会推进 modifier。
OneShot 只调用进入和退出钩子，不调用 ModifyCore，即使关闭 AutoSkipOutsideWindow 也是如此。
普通 modifier 默认跳过窗口外的帧；关闭该开关后，完全处于窗口外的帧仍会收到 ModifyCore，
子类需检查 IsActive。跨窗口边界的帧只执行有效部分，不会额外补一次窗外回调。

Homing 的内部锁定延迟和追踪期限在收到 ModifyCore 时推进，通常从外层窗口开始后计算，
并与外层 Delay、Duration 叠加。退出时间窗口不会自动恢复之前的颜色或速度；具体保留或恢复行为由子类决定。

## 追踪与空间查询

[HomingEnemyModifier](../../Assets/Scripts/Bullet/Modifiers/HomingEnemyModifier.cs) 通过 IHomingTarget 识别普通敌人和 Boss。
目标需存活、组件启用并处于搜索半径内。Unity 对象经接口引用后不能依靠普通 null 比较检测销毁，
需要转换为 UnityEngine.Object 判定。

UniformGrid.QueryRadius 返回覆盖网格的候选集合，并非精确圆形范围；调用方必须再按实际距离过滤。
网格在 CollisionService.LateUpdate 重建，modifier 在 Update 查询到的是上一帧快照。
返回列表是复用缓冲，不可长期持有；新生成目标也不保证在当帧可见。

## 反弹

[BounceBulletModifier](../../Assets/Scripts/Bullet/Modifiers/BounceBulletModifier.cs) 在移动后的越界检查中工作，
仅当窗口仍激活且次数可用时处理。与回收共用 BoundsService.CullingArea；没有服务时使用相同的后备区域。

零次表示不反弹，正数限制次数，负数允许无限次。越过任何禁止反弹的边就不处理，
即使同一帧也越过允许反弹的侧边；例如 ExceptBottom 在底角越界时仍允许正常回收。
多个反弹 modifier 按顺序尝试，第一个成功处理者阻止本次回收。

恢复系数缩放整体速度，不是只缩放法向分量：零意味着停止，不是沿墙滑行。
反弹翻转飞行方向并把位置限制回区域，不打断角速度。

## 染色、透明度与命中残影

[BulletColorModifier](../../Assets/Scripts/Bullet/Modifiers/BulletColorModifier.cs) 使用 MaterialPropertyBlock，
配合 [BulletTint.shader](../../Assets/Shaders/BulletTint.shader) 对暗部染色并保留亮部外观。
默认 Sprites/Default 材质可由 Bullet 替换为共享染色兜底；自定义材质需自行支持相关属性。
MPB 避免逐弹复制材质，但是否满足渲染批处理条件需要按实际渲染管线测量。

染色强度与可见透明度分离：Solid 使用 Color.a 作为染色强度；
Fade、Flash 通过独立的 `_BulletOpacity` 改变可见透明度，染色强度保持起始 Color.a。
因此 Solid 的 Color.a 为零表示不染色，而 Fade、Flash 中零 alpha 会使子弹不可见。
Fade 的参考时间使用 modifier 累计有效时长，并非 Bullet 总寿命。
窗口结束后保留最后外观，回池再恢复视觉基准。

`_BulletOpacity` 与特效用 `_EffectOpacity` 相乘，避免子弹淡出与命中残影淡出互相覆盖。
碰撞服务在伤害回调前请求残影，先保存外观再处理可能清场的回调。
残影不参与碰撞或 modifier 调度，由特效池单独管理；参见[战斗特效](../../Assets/Scripts/Effects/README.md)。

## 分裂与批次抽样

[FirePatternBulletModifier](../../Assets/Scripts/Bullet/Modifiers/FirePatternBulletModifier.cs) 通过完整 FireGroup 路径再发射，
保留 FirePattern 扩展、出生雾化、音效与 modifier 配置，并可继承母弹阵营。
进入窗口分裂和窗口期间持续分裂分别由独立子类决定触发时机。
持续分裂按有效 dt 累计间隔，卡顿补发有每帧上限，不承诺无限追赶所有积压次数。

Extra 先处理 OnFireTriggered，再提供旋转偏移；每颗母弹持有独立的 Extra 运行状态。
Independent 在母弹首次分裂时抽样。Synchronized 由 BulletPool 在同一次 FireGroup 内，
按 Extra 配置对象身份共享抽样，并只写入克隆后的运行实例；下一次 FireGroup 重新抽样。
默认和追加 modifier，以及该发射调用内经统一挂载入口生成的 Composite 子项均走此规则。
不同配置对象不因数值相同而共享；直接 Get 没有发射批次上下文时独立抽样。
这与 FireExtension 的 PrepareBatch 是不同机制，参见[射击模式](./arch-fire-pattern.md)。

## 信号触发

[BulletSignalBus](../../Assets/Scripts/Bullet/BulletSignalBus.cs) 是按名称派发的全局广播，
不区分阵营或发射者，不缓存供未来订阅者重放。先发信号后挂载的子弹不会收到过去的信号。
派发使用订阅快照；回调中的订阅变更不改变本次快照，不应依赖订阅执行顺序。

[启动触发器](../../Assets/Scripts/Bullet/Triggers/ModifierStartTrigger.cs) 在挂载时订阅，
只在尚未启动时接受 ShouldActivate 查询。激活资格一旦消费立即退订，回池或销毁再次退订应安全。
订阅型 trigger 必须记录实际订阅名，保证配置名变化后仍能解除原订阅；Clone 不应继承订阅状态。

OnSignal 在收到信号瞬间检查距离，通过后锁存资格，随后移出范围不会取消激活。
超距信号被忽略，MaxWait 可独立兜底。DelayOrSignal 选择信号或正数延迟中先满足的条件，
其 Delay 为零表示等待信号，而非立即启动；不要与 DelayStartTrigger 的零延迟混淆。

信号源可通过 [EmitSignalAction](../../Assets/Scripts/Enemy/AI/Actions/EmitSignalAction.cs) 接入敌人行为。
重复发送信号不能让已结束的同一 modifier 再次启动，需要重新创建或按生命周期重置。

## 扩展与验证

新增行为在 Modifiers 中继承 BulletModifier，沿用 Serializable、SRName 与 SerializeReference 约定。
Modify 是非虚方法，不应隐藏它；持续行为实现 ModifyCore，一次性行为实现 OnWindowEnter。
新增启动条件放在 Triggers，新增分裂信息传递和采样策略放在 Extras。

循环行为使用 `Modifier/Sequence` 或 `Modifier/Parallel` 容器，并在容器上勾选 `Loop`。
Sequence 按顺序执行 Entries，Parallel 同帧执行所有 Entries；循环只重置容器内的子实例，
不会重置容器自身的启动条件或持续时长。它们都对嵌套子 Modifier 做 Clone，避免不同子弹共享运行状态。
Parallel 子项达到自己的阶段时长后立即退出并脱离，不等待其他子项或整个容器结束。

容器内的 Entry 采用 `BulletModifierEntry { Modifier, Duration }`。Entry.Duration 是唯一的阶段时长；
子 Modifier 的 StartTrigger、Duration、OneShot 不参与 Sequence/Parallel 调度。需要停顿时使用
`Modifier/Wait`，其停顿时长直接填写在 Entry.Duration 中。这样默认时间模型接近 BehaviorFlow：
容器负责时间轴，Modifier 只负责行为，Wait 明确表达中途暂停。

扩展时同时检查 Clone 深拷、OnResetWindow、窗口退出及 OnDetach，避免把配置与运行状态共享。
新增视觉属性必须能随子弹回池恢复；需要订阅或持有资源的行为必须覆盖从未激活就被回收的路径。

回归用例见 [BulletFoundationTests](../../Assets/Scripts/Bullet/Editor/BulletFoundationTests.cs)，
覆盖窗口裁剪、转向、信号、反弹、颜色及回收等边界。
[BulletClearDefenseTests](../../Assets/Scripts/Bullet/Editor/BulletClearDefenseTests.cs) 覆盖两级消弹、
首帧与雾化防御、重叠来源、容器循环、对象池复用、指令与 Bomb、道具转换及关卡强制清场。
代码编译成功不代表测试已经执行，
资产重导入、雾化与淡出组合和实际弹幕仍需在 Unity 中确认。

## 与其他板块的关系

- [碰撞](./arch-hitbox.md)：AABB、阵营和延迟回收。
- [射击模式](./arch-fire-pattern.md)：FirePattern、扩展和发射上下文。
- [边界](./arch-bounds.md)：出界回收与反弹区域。
- [敌人行为](./arch-enemy-ai.md)：信号发送和发射入口。
- [音频](./arch-audio.md)：发射及命中特效音频。
