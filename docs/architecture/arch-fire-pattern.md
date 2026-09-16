# 射击模式系统

> FirePattern 资产 + FireExtension/FireSound/SpawnFog 多态扩展

> 本板块对应 ARCHITECTURE § 3. 射击模式系统(FirePattern)(原 ARCHITECTURE.md 第 568–822 行)。
> 本文档面向项目维护者,不复制实现细节,字段 / 数值 / 默认值以源文件为准。

---

## 3. 射击模式系统(FirePattern)

**职责:** 把"**射什么子弹 + 怎么射**"封装进 ScriptableObject 资产。一个 .asset = 一种弹幕形态,改资产即改变所有引用它的对象。

**协作边界:**
- 被 `FireAction`(敌人行为)、`PlayerShooting`(玩家主炮)、`PlayerOptions`(子机开火)、`BossController`(Boss 开火)调用。
- 实际开火委托给 `BulletPool.FireGroup` / `BulletPool.Get`;阵营由发射者透传,prefab 不需要静态配置。

**FirePattern 字段**:
| 字段 | 类型 | 作用 |
|---|---|---|
| `BulletPrefab` | `Bullet` | 该 pattern 发射的子弹 prefab;留空走 BulletPool.DefaultPrefab 兜底 |
| `ModifierPrefabs` | `[SerializeReference, SR] BulletModifier[]` | 每颗子弹自动挂的 modifier(详见 §2.2) |
| `FireExtensions` | `[SerializeReference, SR] FireExtension[]` | 角度管道多态模块数组(详见 §3.1) |
| `FireSounds` | `[SerializeReference, SR] FireSound[]` | 开火音多态模块数组,与 FireExtensions 并行触发(详见 §3.2) |
| `Speed` | `float` | 飞行速度,默认 5 |
| `AngularSpeed` | `float` | 固定角速度(弧度/秒),0 = 不转;等同于 modifier 里 Steer 的效果 |
| `Damage` | `float` | 玩家弹伤害;敌人弹忽略 |
| `SpawnFog` | `[SerializeReference, SR] SpawnFogConfig`(**单字段**) | 子弹出生后短暂雾化(null 或 Duration=0 = 不雾化,详见 §3.3) |

**扩展点:**
- 新增弹幕形态:新建 `FirePattern` 子类 + 创建对应 .asset(详见 `Assets/Scripts/Bullet/FirePattern/`)。
- 新增出生雾化方法:新建 `SpawnFogConfig` 子类 + 加 `[SRName("Spawn Fog/<名字>")]`,自动出现在所有 FirePattern 资产的下拉菜单(详见 §3.3)。
- 子类在生成子弹时**必须**用基类的 `protected Bullet SpawnBullet(...)` helper,**不要直接调** `pool.Get`,否则 modifier 不会挂上。

详见 `Assets/Scripts/Bullet/FirePattern*`。
### 2.7 反弹钩子(TryBounceOnOutOfBounds)

**问题:** Bullet.Update 第 5 步越界回收是「判定 → 立即回收」,中间没有「想反弹」的环节。如果硬把反弹逻辑写在 BounceBulletModifier 的 `ModifyCore` 里,要等下一帧才生效,中间会有一帧「飞出屏幕外但未回收」的诡异视觉。

**方案:** 在 `BulletModifier` 基类加一个**独立于时间窗口调度**的虚拟钩子 `TryBounceOnOutOfBounds(Bullet)`,由 `Bullet.Update` 第 5 步越界时显式调一次:

```csharp
// BulletModifier.cs(基类,默认空实现 —— 99% modifier 不关心反弹)
public virtual bool TryBounceOnOutOfBounds(Bullet bullet) => false;
```

```csharp
// Bullet.cs 第 5 步(改造后)
if (outOfBounds)
{
    if (!TryBounceModifiers())  // ← 新增:问每个 modifier「要不要反弹」
    {
        BulletPool.Instance.Return(this);  // ← 没人反弹才走原回收
    }
}
```

**协作契约:**

| 行为 | 谁负责 |
|---|---|
| 判定「越界与否」(读 BoundsService.CullingArea 或 fallback ±10/±20) | `Bullet.Update`(与原越界回收共用同一判定,**绝不重复**判) |
| 决定「翻转哪条边 / 翻哪个分量 / 扣几次 / 改不改 Speed」 | BounceBulletModifier 自己 |
| 实际位置 Clamp 回区内 | BounceBulletModifier 自己写 `bullet.transform.position`(modifier 唯一允许改 `transform.position` 的场景) |
| 多个 modifier 同时挂、都返回 true | Bullet 第一个 return true 的 modifier 胜出,后续跳过(防互相覆盖 SteerAngle) |
| 反弹成功 → 不回收 | Bullet.Update 收到 true,跳过 `Return` |
| 没 modifier / modifier 不处理 | Bullet 走原 `BulletPool.Return` 回收 |

**反射数学(经典物理公式):**

```
法线 n(墙内法向,指向区内):
  右墙 n = (-1, 0), 左墙 n = (1, 0), 上墙 n = (0, -1), 下墙 n = (0, 1)
反射公式(恢复系数 e ∈ [0, 1]):
  v' = v - (1 + e) * (v · n) * n
完全弹性(e = 1,默认):v' = v - 2 * (v · n) * n  ← 入射角 = 反射角
实现等价(直接改 SteerAngle):
  水平翻转(R / L):SteerAngle' = π - SteerAngle  →  v = (cos α, sin α) → (-cos α, sin α)
  垂直翻转(T / B):SteerAngle' = -SteerAngle      →  v = (cos α, sin α) → (cos α, -sin α)
  角上碰两边(任意顺序):两次翻转 = SteerAngle' = α - π  →  v → -v(完全反向)
速率(标量):
  Speed' = max(0, Speed × e)  ← e=1 速率不变(完全弹性),e<1 损失能量,e>1 加速(STG 偶尔需要)
```

**§2.7.1 可反弹边集合(BounceWalls 枚举,4 边独立判断):**

| 枚举值 | L | R | T | B | 典型场景 |
|---|:-:|:-:|:-:|:-:|---|
| `HorizontalOnly` | ✅ | ✅ | ❌ | ❌ | 子弹只能在左右擂台间来回弹(水平弹幕) |
| `VerticalOnly` | ❌ | ❌ | ✅ | ✅ | 子弹只能在上下两侧间来回弹(垂直弹幕) |
| `All`(默认) | ✅ | ✅ | ✅ | ✅ | 经典 STG 反弹弹,四边都弹 |
| `ExceptBottom` | ✅ | ✅ | ✅ | ❌ | **玩家朝下打的反弹弹**:撞顶/撞墙弹回,撞地直接回收 —— 避免「从屏幕下方反复弹回来烦人」的体验 |

**实现说明:** 判断从「2 个全局 bool(canHorizontal/canVertical)」升级到「4 个 per-edge bool(canBounceLeft/Right/Top/Bottom)」,switch 一次映射到位。未来加 `ExceptTop` / `ExceptLeft` / `ExceptRight` 时只需在 switch 加 case,主判断逻辑(2 个 if)不动。

**碰撞分支:**

```
撞左/右 → 看 canBounceLeft / canBounceRight  ← 任意一边允许 + 越界在该边 → 水平翻(SteerAngle' = π - α)
撞上/下 → 看 canBounceTop / canBounceBottom  ← 任意一边允许 + 越界在该边 → 垂直翻(SteerAngle' = -α)
两边都越界(角上)→ 两个 if 各判各的,可能都翻 → 两次翻转 = 完全反向
```

**§2.6 时间窗口与反弹的交互:**

- `Delay > 0`:子弹出生后先飞 N 秒直线,才允许反弹(`IsActive == false` 时 `TryBounceOnOutOfBounds` 返回 false,走原回收)。
- `Duration > 0`:窗口期外(到达上限后)不再反弹 —— 子弹飞够了,该回收了。
- `OneShot = true`:进入窗口瞬间调 `OnWindowEnter`,基类立刻 `OnWindowExit`,`IsActive` 回到 false —— 反弹 modifier 的 OneShot 用法是「自杀弹」:进入窗口第一次碰边反弹一次即销毁。
- 窗口进入时 `OnWindowEnter` 重置 `_remaining = MaxBounces`,窗口退出时 `OnWindowExitCleanup` 清 `_initialized`,子弹复用 + Delay>0 时能再次激活。

**协作边界:**

- BounceBulletModifier **不读 / 不写其他 modifier 的状态** —— 只改 `bullet.SteerAngle` / `bullet.Speed` / `bullet.transform.position`,与现有 modifier(改 Speed/AngularSpeed/SteerAngle/加分裂弹)正交不冲突。
- `bullet.AngularSpeed` **反弹时不动**(设计确认)—— 反弹不打断 modifier 状态。子类想要「反弹后切直线」可在自己扩展里改 `bullet.AngularSpeed = 0`。
- 反弹触发的越界判定与 `Bullet.Update` 第 5 步**共用同一 `BoundsService.CullingArea` + 同一 fallback**(无 BoundsService 时 ±10/±20),绝不重复判 —— 一个反射判定,一份边界源。
- 与 §2.6 时间窗口无缝衔接 —— `Delay` / `Duration` / `OneShot` / `AutoSkipOutsideWindow` 全部由基类统一管理,BounceBulletModifier 只关心「窗口内要不要反弹 + 反弹几次 + 怎么反」三件事。

详见 `Assets/Scripts/Bullet/BounceBulletModifier.cs`(实现)+ `Assets/Scripts/Bullet/BulletModifier.cs` 第 185 行附近的 `TryBounceOnOutOfBounds` 钩子 + `Assets/Scripts/Bullet/Bullet.cs` 第 245-275 行附近的反弹调用。



### 3.1 FireExtension 扩展点(模块数组 + Pipeline 模型)

**职责:** 对 FirePattern 子类的"中心方向解析"做可插拔、可组合的多态扩展。任何"决定本轮发射方向"的策略(BaseAngle / 瞄准玩家 / 瞄准 Boss / 每发旋转 / 振荡...)都属于 FireExtension,而非 FirePattern 本身 —— FirePattern 负责"怎么射"的几何(几颗 / 扇形 / 环形 / 容器),FireExtension 负责"朝哪儿射"。

**★ 核心架构:模块数组 + 角度管道(Pipeline)**

FirePattern 基类持有 `FireExtensions : FireExtension[]` 数组,数组里每个模块按顺序串成一条"角度管道":

```
center = rotationRad                                       ← 起点(外部累积角)
for ext in FireExtensions (按数组顺序遍历):
    center = ext.ProcessAngle(from, rotationRad, center)   ← 上一步 → 下一步
return center
```

每个模块接收"上一步产出的角度"和"起点 rotationRad",产出"下一步要用的角度"。

**模块类型分类(语义)**

| 类型 | 行为 | 内置实例 |
|---|---|---|
| **覆盖型** | 直接把角度设为某值(忽略上一步的 currentAngleRad) | `BaseAngleFireExtension`(锚点)、`PlayerAimFireExtension`(瞄玩家时) |
| **透传型** | 直接返回 currentAngleRad,不动 | (未来的"过滤/检测"型) |
| **累加型** | 在 currentAngleRad 上叠加偏移 | `OffsetAngleFireExtension`(叠 N°,常配合 PlayerAim 实现"绕后弹":瞄向玩家但飞向玩家背后) |

**★ 拼装示例 ★**

```
[]                                            → 兜底:270° + rotationRad(空数组等价旧版 default)
[Base(270°)]                                  → 始终向下开火
[Base(270°), PlayerAim]                       → 默认向下,瞄得到玩家时改指向玩家(等价旧版"瞄准 + 兜底")
[Base(0°), PlayerAim]                         → 默认向右,瞄得到时改指向玩家
[PlayerAim]                                   → 瞄得到指向玩家;瞄不到 → 透传 rotationRad(等于默认方向)
[PlayerAim, Offset Angle(+180°)]              → 玩家方向的反方向("绕后弹":瞄准玩家但飞向玩家背后)
[Base(270°), PlayerAim, Offset Angle(+180°)]  → 瞄得到:玩家反方向;瞄不到:默认向下
```

**协作边界:**
- 字段挂在 FirePattern 基类上(`[SerializeReference, SR] FireExtension[] FireExtensions`),所有 FirePattern 子类(Arc / Line / Ring / Composite / 未来)自动支持 Inspector 下拉。
- 子类通过静态 helper `FireExtensionResolver.ResolvePipeline(extensions, from, rotationRad)` 拿方向;helper 与基类解耦,可被其他系统复用。
- 走 SpawnBullet 路径的"必须走基类 helper"约束不变(详见 §3)。
- `CompositeFirePattern` 是纯容器(只有 `Children` + 透传 + 递归 GetFireCount),自己不持任何发射字段 —— 它对 FireExtension 的处理是"透传给 children,各自解析"。

**类型:**
- 基类:`FireExtension`(纯 C# `[Serializable] abstract class`),核心接口 `ProcessAngle(from, baseRotationRad, currentAngleRad)` 是抽象方法。
- 内置子类:
  - `BaseAngleFireExtension` `[SRName("FireExtension/Base")]` —— 覆盖型,角度 = `BaseAngle + baseRotationRad`(作为管道锚点)
  - `PlayerAimFireExtension` `[SRName("FireExtension/Player Aim")]` —— 覆盖型,瞄得到玩家 → 角度 = atan2(player - from);瞄不到 → **透传** currentAngleRad(兜底交给上游 Base)
  - `OffsetAngleFireExtension` `[SRName("FireExtension/Offset Angle")]` —— 累加型,角度 = currentAngleRad + OffsetAngle(逆时针为正);常配合 PlayerAim 实现"绕后弹"
  - `AccumulatingOffsetAngleFireExtension` `[SRName("FireExtension/Offset Angle Accumulating")]` —— **批次累加型**,在 `OffsetAngle` 基础上增加"每次开火后累加偏移" + SR 多态基础偏移(详见下方 §3.1.1)
- Helper:`FireExtensionResolver`(静态,Null-safe,`extensions == null` 或空数组时 fallback 到默认 270° + rotationRad)。

**PlayerAim 的兜底语义变化(从单字段 → 数组)**
- 旧版:`PlayerAimFireExtension.BaseAngle = 270`,瞄不到时退回 BaseAngle —— 字段既是"瞄准偏移"又是"兜底",语义脏。
- 新版:删掉 `BaseAngle` 字段,瞄不到时直接透传 `currentAngleRad`。**兜底职责交给上游 Base 模块**(把 Base 放数组第一位即可)。

**Base 模块的位置偏移(`PositionOffset` 字段)**

`BaseAngleFireExtension` 除了 `BaseAngle` 外还持有一个 `PositionOffset: Vector2` 字段,**世界坐标**,用于设置"实际发射位置"相对"标准位置"的偏移:

```
标准位置 = FirePattern 调用方传入的 position(敌人/Boss/玩家所在位置)
实际位置 = 标准位置 + BaseAngleFireExtension.PositionOffset
```

**生效时机:Resolver 入口处**(pipeline 开始之前)。
- 调用方用 `FireExtensionResolver.ResolvePipelineWithOffset(extensions, ref from, rotationRad)`(3 个内置 FirePattern 子类都已改用此版本)。
- 进入 pipeline 之前:如果 `extensions[0]` 是 `BaseAngleFireExtension`,先把 `PositionOffset` 加到 `from`,**所有后续模块(包括 PlayerAim 等瞄准类)都用修正后的 from**。
- `ResolvePipeline`(不带 WithOffset):不应用位置偏移,仅返回角度。保留给"只想要方向、不想被位置影响"的场景。

**典型用法:**
- `PositionOffset = (0, 0.5)` —— 肩扛炮口在头顶(向上偏移 0.5)
- `PositionOffset = (0.3, 0)` —— 双管炮右管在右侧(向右偏移 0.3)
- `PositionOffset = (0, 0)` —— 默认值,**等价旧版行为**(所有现有资产零行为变更)

**正交叠加**:`BaseAngleFireExtension.PositionOffset` 与具体 FirePattern 的"局部偏移"(`RingFirePattern.Radius` / `ArcFirePattern.Radius`)正交叠加 —— 前者是"全局炮口偏移",后者是"扇形/环形的几何起点偏移"。

**扩展点:**
- 新增"瞄准 X / 旋转 / 振荡"等策略 = 新建 `FireExtension` 子类 + 加 `[SRName("FireExtension/<名字>")]`,自动出现在所有 FirePattern 资产的下拉菜单。
- 实现 pipeline 模块时,需要"覆盖"用 `ProcessAngle` 直接返回新角度;需要"透传"返回 `currentAngleRad`;需要"累加"用 `currentAngleRad + offset`。
- "每发独立方向"场景(每发旋转 N° / 延迟扇形)override `ProcessAngleForBullet`。

详见 `Assets/Scripts/Bullet/FireExtension/`(顶部有详细 pipeline 用法图示)。

#### §3.1.1 批次累加型:`AccumulatingOffsetAngleFireExtension`

在 `OffsetAngleFireExtension`(恒定偏移)基础上新增"每次开火后累加"能力,并把"本批次基础偏移"从 float 升级为 SR 多态字段(`BaseOffsetStrategy`,与现有 `AngleOffsetFirePatternBulletExtra.BaseOffset` 共用同一套子类)。

**字段语义:**

| 字段 | 类型 | 作用 |
|---|---|---|
| `BaseOffset` | `[SerializeReference, SR] BaseOffsetStrategy`(默认 `FixedBaseOffsetStrategy { Value = 0f }`) | 本批次第一次开火施加的恒定偏移,SR 多态下拉:`Base Offset/Fixed`(精确值)/ `Base Offset/Random Range`(区间随机) |
| `StepOffset` | `float`(度,默认 0) | 每次 FireGroup 触发后,下一次再叠加的偏移量。`0` = 不累加,等价 `OffsetAngleFireExtension`;`5` = 每发顺时针转 5° |
| `OffsetPerBullet` | `float`(度,默认 0) | **每发独立方向版累加**:`bulletIndex × OffsetPerBullet`,与 `StepOffset` 正交叠加。`0` = 关闭,所有子弹共用中线(Ring/Arc 等分仍由各自 FirePattern 子类决定);`22.5` = Ring 8 颗第 i 颗再偏 22.5°×i(配合 `StepOffset` 形成旋转螺旋环) |

**累加公式(角度,度):**

```
第 N 次开火(每批 FireGroup 触发 1 次):
  batchOffset = BaseOffset.Sample() + (N - 1) × StepOffset
  perBulletOffset = bulletIndex × OffsetPerBullet      // 仅 ProcessAngleForBullet 路径
  totalOffset = batchOffset + perBulletOffset          // 累加后转弧度参与角度管道
```

**触发时机:每批 FireGroup** —— 由 `BulletPool.FireGroup` 入口维护 per-FireExtension 字典 `_fireCounts`(`Dictionary<FireExtension, int>`),对每个非 null 元素 `++` 后调用 `FireExtension.OnFireGroupTriggered(fireCount)` 钩子(基类新增,默认空实现,对所有现有子类零侵入)。子类在钩子里写自己的累加状态:本类即在钩子中重抽 `BaseOffset.Sample()` 并把 `fireCount` 缓存到 `_currentFireCount`,后续 `ProcessAngle` / `ProcessAngleForBullet` 用这个值。

**per-instance 隔离:字典 key = `pattern.FireExtensions` 数组里的具体元素 ref**(不是 SO 资产本身),确保:
- 同一份 FirePattern SO 被敌人 A / B 共用 → 数组里同一元素引用,字典累加跨敌人累计 —— 项目想要的行为("Boss 散弹母弹开火 60 次旋转 300°" 跨多次开火累加)
- 用户复制一份 FirePattern 资产(Ctrl+D) → 新资产的 FireExtensions 数组是新元素,字典独立累加
- 同一份资产上挂多个累加型模块 → 字典按 ref 区分,各自累加(例:`[Base, Accumulating Offset(0, 5°), Accumulating Offset(0, 10°)]` 第 N 次开火偏 `(N-1) × 15°`)

**典型用法:**

| 配置 | 效果 |
|---|---|
| `[Base(270°), Accumulating Offset(Fixed(0), 5°)]` | Boss 散弹母弹旋转喷射(Duration=3s, Interval=0.05s → 60 次开火,旋转 300°) |
| `[Base(270°), Accumulating Offset(Random Range(-15, 15), 0°)]` | 抖动扩散(每次开火基础偏移随机,无累加,等价旧版 `OffsetAngle` + 随机) |
| `[Base(270°), Accumulating Offset(Fixed(0), 3°)]` + `OffsetPerBullet=22.5` | Ring 8 颗 × 22.5° 螺旋环,每次开火再转 3° → 旋转扩散 |

**与 `AngleOffsetFirePatternBulletExtra`(母弹 → 分裂弹 Extra 路径)的区别:**

- `AccumulatingOffsetAngleFireExtension` = FireExtension pipeline 模块,挂在 `FirePattern.FireExtensions[]` 数组上,影响本 FirePattern 每次开火的方向
- `AngleOffsetFirePatternBulletExtra` = `FirePatternBulletExtra` 子类,挂在 `FirePatternBulletModifier.Extra` 上,只影响"母弹分裂那一刻传给分裂弹的 rotationRad"
- 两者**完全独立**,可叠加使用:Boss 散弹母弹挂 `FirePatternBulletModifier`(`Modifier/Fire Pattern While Active`)→ 它的 `Extra` 可挂 `AngleOffsetFirePatternBulletExtra`(控制分裂弹方向),而分裂弹自身用的 FirePattern 资产也可挂 `AccumulatingOffsetAngleFireExtension`(控制分裂弹每次开火方向)
- "本批次共享 BaseOffset 抽样值"语义:FireExtension 路径**不提供**(`Accumulating` 每次开火重抽);Extra 路径通过 `BatchSample = Synchronized` + `OnBatchFire()` 钩子提供,详见 [bullet §2.5](./arch-bullet.md#25-扩展点) 中 `本批 BaseOffset 共享` 段

**协作边界:**
- `FireExtension` 基类新增 `public virtual void OnFireGroupTriggered(int fireCount)`(默认空实现,对现有 `BaseAngle` / `PlayerAim` / `OffsetAngle` 子类零侵入)
- `FireExtensionResolver` 新增字典重载(`ResolvePipeline` / `ResolvePipelineWithOffset` 各 +1,接收 `IReadOnlyDictionary<FireExtension, int>`),旧单值重载 `int fireCount = 0` 保留,旧调用方传 0 → 不调钩子 → 行为 100% 不变
- `BulletPool` 公开 `GetFireExtensionFireCounts()` 给 Ring/Line/Arc 三个 FirePattern 子类的 `Fire()` 入口调用(它们改用字典重载),CompositeFirePattern 透传到 children → 各自走 children 的 FireExtension

详见 `Assets/Scripts/Bullet/FireExtension/AccumulatingOffsetAngleFireExtension.cs` 顶部注释。

### 3.2 开火音多态扩展(FireSound)

FirePattern 还有第二个多态模块数组 `FireSounds[]`,**与 FireExtensions 并行触发**(不是角度管道,是"并行触发器")。

- **触发时机**:`BulletPool.FireGroup` 入口 → `pattern.PlayFireSounds(pos, ownerHitbox)` → 每次"开火组"播一次。CompositeFirePattern 的子 pattern **不重复触发**(只在最外层触发一次)。
- **与 FireExtension 区别**:
  - `FireExtension[]` = 角度管道(上一步输出角度 → 下一步输入)
  - `FireSound[]`     = 并行触发器(每个模块独立播一个音,可叠播多个 cue)
- **基类**:`FireSound`(`Assets/Scripts/Bullet/FireExtension/FireSound.cs`)
- **内置**:`SfxCueFireSound`(走 SfxCue 体系,含限流/Pipeline/Bus)、`NullFireSound`(显式静音)
- **扩展**:新建 `XxxFireSound.cs : FireSound` + `[SRName("FireSound/<名字>")]`,Inspector 自动下拉出现
- **与 PlayerShooting._shootSfx 关系**(可并存):`_shootSfx` 是"玩家整体开火"(不论哪个 pattern),`FireSounds[]` 是"特定 pattern 的特征音"(同一 pattern 在玩家 vs 敌人可配不同 cue)

详见 `Assets/Scripts/Bullet/FireExtension/FireSound.cs` 顶部注释 + `Assets/Scripts/Audio/README.md` §6.5。

### 3.3 出生雾化多态扩展(SpawnFogConfig)

**职责:** FirePattern 上的"出生雾化"多态配置字段(**单字段,不是数组**),控制子弹生成后短暂雾化期内的视觉 / 行为。

**雾化期内的子弹行为**(由 `Bullet.cs` 统一执行,`Bullet.Update` 内 `FogDuration > 0` 时 early-return):

- 位置固定(不按 Speed 移动)
- 不参与碰撞(`HitboxComponent.IsFogged=true` → `CollisionService` 各 Tick 阵营过滤后会 continue)
- modifier 时间窗口计时器不累计(雾化结束 → 时间窗口从 0 开始)
- 视觉由子类 `override` 的 `ApplyVisual(Bullet b, float t01, Vector3 baseLocalScale)` 决定;默认走 STG/BulletTintFog shader,通过 `MaterialPropertyBlock` 写 `_FogAmount=1→0` + `_FogColor`

**字段语义:**

- `SpawnFog` 是单字段(不是数组),`[SerializeReference, SR]` 多态下拉,可选择:
  - 字段为 `null`(默认,与历史行为 100% 等价)
  - `[SRName("Spawn Fog/None")]` —— 显式"不使用雾化"占位选项(`Duration=0`,与 `null` 行为等价)
  - `[SRName("Spawn Fog/Default")]` —— 默认基础雾化(染色 + 缩放 + 缓动)
  - `[SRName("Spawn Fog/<中雾化>")]` —— 后续扩展位

**协作边界:**

- 字段挂在 `FirePattern` 基类上,所有 `FirePattern` 子类(Arc / Line / Ring / Composite / 未来)自动支持 Inspector 下拉。
- `CompositeFirePattern` 的子 pattern **各自带自己的 SpawnFog**,互不影响。
- `Bullet.cs` 提供 `public ApplyFogMaterialParams(float fogAmount, Color fogColor)` helper 给子类调用,**MPB 生命周期由 Bullet 统一管理**(懒分配 `_fogMpb`,与 `BulletColorModifier` 的 `_TintColor` 不冲突)。子类不要直接访问 `Bullet._fogMpb` 私有字段。
- 子类若做缩放,务必走 `baseLocalScale × fogScale` 乘法,不要覆盖 `transform.localScale`,避免破坏 prefab 美术基准。

**类型:**

- 基类:`SpawnFogConfig`(`Assets/Scripts/Bullet/FirePattern/SpawnFog/SpawnFogConfig.cs`,纯 C# `[Serializable] abstract class`)
- 内置子类:
  - `NoSpawnFog` `[SRName("Spawn Fog/None")]` —— `Duration=0`,与 `null` 行为等价(显式占位,便于保留"选了 None"的意图可追溯)
  - `DefaultSpawnFog` `[SRName("Spawn Fog/Default")]` —— 染色 + 缩放 + 缓动三件套(走 `STG/BulletTintFog` shader)
- 共享枚举 / Helper:`FogEasing`(None / EaseOut / EaseIn / EaseInOut 缓动曲线) + `FogEasingUtil.Apply(t, easing)`(缓动函数,供所有子类的 `ApplyVisual` 调用)

**扩展点:**

- 新增"中雾化方法"(波形雾化 / 径向膨胀 / 颜色渐变 / 拖尾 / ...) = 新建 `SpawnFogConfig` 子类 + 加 `[SRName("Spawn Fog/<名字>")]`,**自动出现在所有 FirePattern 资产的下拉菜单**。
- 子类需 `override`:
  - `ApplyVisual(Bullet b, float t01, Vector3 baseLocalScale)` —— 雾化期内每帧调用(`t01=0` 出生瞬间,`t01=1` 雾化结束)
  - `ClearVisual(Bullet b, Vector3 baseLocalScale)` —— 雾化结束调用一次,复位视觉

详见 `Assets/Scripts/Bullet/FirePattern/SpawnFog/SpawnFogConfig.cs` 顶部注释。

---


## 与其他板块的关系

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [bullet](./arch-bullet.md) — FirePattern.Fire 最终调用 BulletPool.FireGroup,生成 Bullet 并挂 Modifier
- [enemy-ai](./arch-enemy-ai.md) — FireAction 在 BehaviorFlow 时间轴上引用 FirePattern 资产
- [audio](./arch-audio.md) — FireSound 走 SfxCue 体系,见 AudioSystem 与本板块 §3.2
