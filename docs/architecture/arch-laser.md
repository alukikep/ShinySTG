# 激光系统

> LaserEntity 5 段状态机 + 视觉/判定宽度分离

> 本板块对应 ARCHITECTURE § 13. 激光系统(东方风格直线 / 曲线)(原 ARCHITECTURE.md 第 1304–1413 行)。
> 本文档面向项目维护者,不复制实现细节,字段 / 数值 / 默认值以源文件为准。

---

## 13. 激光系统(东方风格直线 / 曲线)

> 把"具有位置、方向、长度、宽度、运动参数和生命周期状态的线段 / 节点集合"封装成与子弹平行的子体系。
> 视觉与判定严格分离;碰撞走"点到线段距离"而不是 AABB 矩形;生命周期走五段式状态机。
> 参考实现资料:东方 Project 风格激光系统参考说明 §1-§22。

### 13.1 为什么需要独立子系统

子弹系统(`Bullet` + `BulletPool` + `CollisionService`)的整个碰撞管线是为 AABB + `UniformGrid` 设计的,激光不行:

| 维度 | Bullet 现状 | 激光需求 | 兼容性 |
|---|---|---|---|
| 碰撞形状 | 统一 AABB(`HitboxComponent.WorldBounds` + `HitboxMath.AABBOverlap`) | 点到线段距离(`(P-Q)² < (r_p + r_l)²`) | ❌ AABB 框不住(激光长 20 单位、AABB 会吞半个屏幕) |
| 空间索引 | `UniformGrid` cell 默认 4 单位 | 激光跨 N 个 cell | ❌ 网格要么插入失真、要么查询退化为全扫 |
| 生命周期 | 子弹无状态机,过期靠 `BoundsService.ContainsCulling` 回收 | `Warning → Expanding → Active → Shrinking → Dead` 五段式 | ❌ Bullet 缺状态机字段 |
| 视觉 | 单 `SpriteRenderer` | 条状主体 + 头尾发光 + 残影 + 透明度 + 长度插值 | ❌ 单 Renderer 不够 |

结论:激光必须新建独立子系统(Laser 实体 + LaserEmitter/Pool + LaserService),**不继承** Bullet,**不污染** BulletPool。但复用现有架构的"语言":
- 走与 Bullet 平行的 `MonoBehaviour` + 对象池(对齐 §1 整体架构)
- 走 `[SerializeReference, SR]` 多态扩展(对齐 §3.1 FireExtension / §2.6 BulletModifier 套路)
- 走 SO 数据驱动 + `BehaviorFlow` Action 触发(对齐 §4 敌人 AI)
- 走 `CollisionTeam` 阵营过滤(不破坏现有碰撞协议)

### 13.2 职责分工

- **`LaserState`(enum)** —— 五段式状态机:`Warning / Expanding / Active / Shrinking / Dead`。纯枚举,零行为。

- **`LaserGeometry`(static class)** —— 激光几何 + 碰撞判定数学,零分配纯函数:
  - `EndPoint(origin, angleRad, length)` → 端点(`(cos θ, sin θ) * length`)
  - `DistanceSqPointToSegment(p, a, b)` → 距离平方(`Clamp01` 把投影点限制在线段内,退化时退化为点到 a)
  - `CheckCurvedHit(playerPos, nodes, laserR, playerR)` → 节点数组任一段命中即 true
  - `CheckCurvedGraze(playerPos, nodes, maxR)` → 擦弹版本(用更大的膨胀半径)

- **`LaserData`(SO 资产)** —— 可复用视觉 + 判定参数集合:
  - **几何**: `VisualWidth`(贴图宽,默认 0.8)/ `CollisionWidth`(判定半径,默认 0.15)/ `MaxLength`(默认 24)
  - **生命周期**: `WarningTime`(0.6) / `ExpandTime`(0.3) / `ActiveTime`(1.5) / `ShrinkTime`(0.3),四段时间全部独立
  - **生命周期碰撞策略**: `CollideInWarning / Expanding / Shrinking` 三个 bool,默认全 false(只在 Active 命中,与东方正作常见行为一致)
  - **视觉**: `Sprite`(条状贴图)/ `HeadSprite`(激光头)/ `WarningColor` / `BodyColor` / `HeadColor`
  - **关键**: 视觉宽度与判定宽度**严格分离**(参考材料 §6),屏幕看到的是 `VisualWidth`,实际判定只用 `CollisionWidth`

- **`LaserEntity`(MonoBehaviour,主控)** —— 单条激光的运行时实体,职责:
  - **状态机**:每帧 `LateUpdate` 按时间推进 `Warning → Expanding → Active → Shrinking → Dead`,依据 `LaserData` 设置 `CollisionEnabled`
  - **几何**: `Position += Velocity * dt`、`Angle += AngularVelocity * dt`、`CurrentLength` 按状态机 + `LengthEase` delegate 插值(默认 `LinearEase`,可在 modifier 里切换)
  - **运动**: 直线模式直接重算端点;曲线模式按 `BaseCurveNodes[i] + Position` 每帧重算世界节点(让节点跟随激光移动)
  - **碰撞查询**: `CheckStraightHit(playerPos, playerRadius)` 一行入口(直线 / 曲线统一),内部走 `LaserGeometry`
  - **修饰器**: `_modifiers` 私有列表,提供 `AddModifier` / `ClearModifiers` / `ResetAllModifierWindows`(对齐 `Bullet` 的同名 API)
  - **生命周期**: `OnDisable` 自动 `ClearModifiers`(`LaserPool.Return` 触发),死亡由状态机切到 `Dead` 后自己调 `SourcePool.Return`

- **`LaserModifier`(abstract `[Serializable]` class)** —— 激光行为修饰器(纯 C#,非 MonoBehaviour),对齐 `BulletModifier`:
  - `OnTick(laser, dt)` 每帧调用(状态机推进之前),子类修改 Position / Angle / Velocity / AngularVelocity / LengthEase
  - `OnDetach(laser)` / `ResetWindow()` 默认空,子类 override
  - `Clone()` 默认 `MemberwiseClone`,子类含引用字段需 override 深拷
  - **PR1 阶段**:只提供最小钩子(对齐 BulletModifier 完整双时钟体系留到 PR3)

- **`LaserPool`(MonoBehaviour,场景单例)** —— 激光对象池,严格对齐 `BulletPool` 拓扑:
  - **分桶**: key 用 `LaserData`(同一份 Data 配不同 Renderer 时共用桶效率最高)
  - **懒扩容**: `Awake` 只为 `DefaultPrefab` 预热一份,其他 Data 按需 Instantiate
  - **路由回池**: `Return(laser)` 按 `laser.Data` 路由到正确桶(不按 SourcePrefab 因为激光不分 prefab 桶)
  - **中心化开火**: `FireGroup(pattern, pos, angleRad, ownerHitbox, extraModifiers)` 与 `BulletPool.FireGroup` 形似,内部调 `pattern.Fire(...)`

- **`LaserPattern`(abstract SO)** —— 激光描述,与 `FirePattern` 平行:
  - 字段: `Data: LaserData` / `ModifierPrefabs: LaserModifier[]`(SR 多态)/ `FireSounds: FireSound[]`(SR 多态,与 `FirePattern.FireSounds` 复用)/ `FireExtensions: LaserFireExtension[]`(SR 多态,**Pipeline 角度管道**,对齐 `FirePattern.FireExtensions`,见 §13.3)/ `Damage: float`
  - 虚方法 `PlayFireSounds(pos, ownerHitbox)`(对齐 `FirePattern.PlayFireSounds`,由 `LaserPool.FireGroup` 入口中心化触发)
  - 抽象方法 `Fire(position, angleRad, pool, ownerHitbox, extraModifiers)` 返回 `LaserEntity`
  - **关键边界**: **不继承 `FirePattern`**(基类持 Speed/Angular/BulletPrefab,对激光无意义),但保留 SR 多态扩展位与 `FirePattern` 完全对仗

- **`StraightLaserPattern: LaserPattern`** —— PR1 默认 Pattern,**单向直线激光**:
  - 字段: `LengthMultiplier: float`(默认 1)
  - **几何语义**: `position` = 激光起点,端点 = `position + (cos angle, sin angle) * length`(碰撞 `DistanceSqPointToSegment(player, Position, EndPoint)` 与几何一致)
  - **视觉语义**(PR1 Renderer 修正后): Body 与 Warning 子物体 pivot 视觉上落在起点端,从起点沿 Angle 方向延伸 → **视觉单向,与几何/碰撞完全对齐**
  - `Fire` 内部:`pool.Get(data, pos, angleRad, length, ownerHitbox)` → 挂 modifier → 触发 FireSounds

- **`BidirectionalStraightLaserPattern: LaserPattern`** —— **双向直线激光**(同点反向双发):
  - 字段: `LengthMultiplier: float`(默认 1)
  - **几何语义**: `position` = 两条激光的共享起点,正向沿 `angleRad`、反向沿 `angleRad + π`,长度相同。两条**独立 `LaserEntity`**,各自走完五段状态机
  - **视觉语义**: Body 从起点各向相反方向延伸 → ✕ 形 / 十字交叉
  - `GetFireCount() = 2`(对齐 CompositeFirePattern 语义,Boss 系统 `ShotsFired` 统计按 2 算)
  - **不做位置偏移**:两条激光共享同一个 `position`。若需肩炮等"两个不同发射点"的双向,Action 层用两个 FireLaserAction 各偏一点,或未来加 MultiAngleLaserPattern
  - **共享 FireExtensions 累加字典**: 一次 Fire 走一次 Resolver,fireCount +=1;正反两条同角度、同长度、同 modifier、同阵营

- **`RingLaserPattern: LaserPattern`** —— **单向环形激光**(同点等分扇出 N 条):
  - 字段: `Count: int`(默认 16)/ `Radius: float`(默认 0,每条激光沿自身方向的外推距离)/ `LengthMultiplier: float`(默认 1)
  - **几何语义**: `position` = 共用起点(可被 Base.PositionOffset 修正),每条激光 `rad = centerRad + (2π/N)·i`,端点 = `origin + (cos rad, sin rad)·length`。N 条**独立 `LaserEntity`**,各自走完五段状态机
  - **视觉语义**: N 条等分环形 → 圆盘形 / 多向散射
  - **对齐 Bullet 端**: 复用 `RingFirePattern` 的均分算法(`step = 360°/Count`)与 `Radius` 外推语义;激光版每条独占生命周期
  - **与 BidirectionalStraightLaserPattern 的边界**: Ring 是 N 条等分,**全部单向**,无 `rad + π` 的反向第二条;Bidirectional 是固定 2 条**正反对称**
  - `GetFireCount() = Count`(对齐 CompositeFirePattern 语义,Boss `ShotsFired` 按 N 算)
  - **共享 FireExtensions 中心方向**: 一次 Fire 走一次 Resolver,fireCount +=1;N 条共用同一个 `centerRad` 与同一个 `from`(已含 Base.PositionOffset)

- **`ArcLaserPattern: LaserPattern`** —— **单向弧形激光**(同点弧长内扇出 N 条):
  - 字段: `Count: int`(默认 8)/ `ArcLengthDeg: float`(默认 60,弧长)/ `Radius: float`(默认 0)/ `LengthMultiplier: float`(默认 1)
  - **几何语义**: 中线由 FireExtensions 解析,每条 `rad = centerRad - ArcLength/2 + (ArcLength/(N-1))·i`,N 条独立 `LaserEntity`。`Count == 1` 退化为沿中线的单条
  - **视觉语义**: N 条扇形展开 → ◣ 形 / 锥形散射
  - **对齐 Bullet 端**: 复用 `ArcFirePattern` 的弧长均分算法(`start = centerRad - ArcLength/2`、`step = ArcLength/(N-1)`)与 `Radius` 外推语义
  - **与 RingLaserPattern 的边界**: Arc 是 ArcLengthDeg 范围内局部扇出,默认扇形 60°;Ring 是全 360° 等分
  - **与 BidirectionalStraightLaserPattern 的边界**: Arc 全部单向,无反向第二条
  - `GetFireCount() = Count`(对齐 CompositeFirePattern 语义)
  - **共享 FireExtensions 中心方向**: 同 RingLaserPattern 套路

- **`LaserRendererBase`(abstract MonoBehaviour)** + **`SpriteStretchLaserRenderer`** —— 视觉抽象:
  - `LaserRendererBase` 暴露 `OnLaserInit(laser)` / `OnLaserTick(laser)` 两个钩子
  - `SpriteStretchLaserRenderer`(PR1 默认实现)抓三个子 SpriteRenderer:
    - `Body`(按 `CurrentLength` 拉伸 X,按 `VisualWidth` 设 Y)
    - `Head`(激光头,固定不动)
    - `Warning`(Warning 期显示一条细预警线,长度 `WarningLineLength` 可配)
  - **Body 拉伸公式**: `Body.localScale.x = length`(世界长度),**不**乘任何 `BaseLocalX`。这是为了**对象池复用安全**:若公式带 `_bodyBaseLocalX` 系数,`OnLaserInit` 读 `Body.localScale.x` 会被前一次发射的 `Shrinking` 末期残留污染(接近0),导致每次池复用时 Active 期长度都比上一次短(参考 PROGRESS.md PR1 修复 2 轮的 bug 记录)
  - **单向视觉修正**: Body / Warning 子物体的 Sprite pivot 默认 `(0.5, 0.5)` 居中,直接 `localScale.x = length` 会从中点向两边展开,看似"双向"。Renderer 每帧把 `localPosition.x = ls.x * 0.5f`,把 pivot 视觉拉到起点端,Body 看起来从 `laser.Position` 沿 Angle 方向延伸。Warning 子物体同理。这样**视觉 / 几何 / 碰撞三向完全对齐**(均为"从起点单向延 length")
  - **与碰撞完全解耦**:换 Renderer 实现(`LineRenderer` / 自定义 Mesh)不影响 `LaserService` 判定

- **`LaserService`(MonoBehaviour,场景单例)** —— 中心化碰撞服务,**独立于 `CollisionService`**:
  - **为什么不在 CollisionService 加分支**: AABB 不适合激光 + UniformGrid 失真;中心化服务避免给 CollisionService 加大量特例判断
  - `LateUpdate` 遍历 `LaserPool.ActiveLasers`,按 `CollisionTeam.Enemy` 阵营过滤,逐条 `laser.CheckStraightHit(pPos, PlayerRadius)`
  - **命中 → 不立刻回收**(对齐东方正作 + 子弹碰撞行为):激光是"持续判定",玩家撞到激光只扣血,激光继续走完五段状态机自然回池。**帧内去重**:同一条激光一帧内只扣 1 次血(`_hitThisFrame: HashSet<LaserEntity>`,每帧 `LateUpdate` 开头 `Clear`)—— 玩家若持续站在激光上,**每帧扣 1 次血**(典型"激光穿身")
  - 命中 → 非无敌期 `ShinySTG.Player.Player.Instance.OnHit(1f)` + 触发 `OnPlayerHitByLaser` 事件(每条激光每帧最多触发 1 次,见帧内去重)
  - **无敌期不再吞噬激光**:`ConsumeLasersWhenInvincible` 字段保留(向后兼容旧资产),但当前实现下其分支退化为 no-op —— 无敌时激光继续走生命周期,玩家不扣血
  - 擦弹:借鉴 `CollisionService` 的膨胀环思路,激光擦过判定外圈时触发 `OnPlayerGrazedByLaser` 事件,激光继续飞行(每条激光整个生命周期最多擦 1 次,`_hasGrazed` 标志位)
  - 出界: 读 `BoundsService.Instance.ContainsCulling(laser.Position)`,出界立刻加入回收队列(避免长期占用池)
  - **玩家自动注册**: `Awake` 自动抓 `ShinySTG.Player.Player.Instance.Hitbox`,无需手动拖

- **`FireLaserAction: EnemyAction`**(`[SRName("Action/Fire Laser")]`)** —— BehaviorFlow 接入层:
  - 字段: `Pattern: LaserPattern` / `FireRate: float` / `AimOffsetDeg: float` / `ExtraModifierPrefabs: LaserModifier[]`
  - 与 `FireAction` **完全对仗**: 同级 SR 标签、相同 OnEnter/OnTick/OnExit 生命周期、相同 ownerHitbox 透传、相同 `FireRate` 节拍
  - Boss / 敌人在 BehaviorFlow 时间轴上挂一条 `Action/Fire Laser` 即可定时放激光,与已有 `Action/Fire` 并行不冲突

### 13.3 协作边界

| 既有层 | 怎么协作 | 是否修改它 |
|---|---|---|
| `Bullet` / `BulletPool` / `CollisionService` / `UniformGrid` | **完全不动**,激光走独立子系统(不污染子弹代码) | ❌ 0 改动 |
| `HitboxComponent` | 激光实体不挂(避免 AABB 插入网格);阵营透传走 `LaserEntity.Team` 字段,值类型与 `HitboxComponent.CollisionTeam` 共用 enum | ❌ 0 改动 |
| `CollisionTeam`(enum) | 复用:`LaserService.LateUpdate` 阵营过滤用 `Team != CollisionTeam.Enemy` continue | ❌ 0 改动 |
| `BoundsService` | 激光出界判定读 `BoundsService.Instance.ContainsCulling(laser.Position)`,沿用已有 Culling Area;无单例时跳过出界检查 | ❌ 0 改动(只读) |
| `Player` / `PlayerHealth` | 命中通过 `ShinySTG.Player.Player.Instance.OnHit(1f)`(与 `CollisionService` 同款,避免 namespace-同名陷阱);无敌判定读 `player.IsInvincible`(全限定名) | ❌ 0 改动 |
| `EnemyAction` / `BehaviorFlow` | 新增 `FireLaserAction`,作为同级 Action 类型(对齐 `FireAction`);BehaviorFlow.Actions 数组 `[SerializeReference, SR]` 字段**自动**支持新 Action,无需改 `BehaviorFlow` | ❌ 0 改动 |
| `FireSound` / `FirePattern.FireSounds` | 激光 `LaserPattern.FireSounds` 直接复用 `FireSound[]`(走 `[SerializeReference, SR]`);开火音触发由 `LaserPool.FireGroup` 入口中心化调用 `pattern.PlayFireSounds(...)`(对齐 `BulletPool.FireGroup` 触发点) | ❌ 0 改动(复用) |
| 子弹 Modifier(`BulletModifier` / `ModifierStartTrigger` / `BulletSignalBus`) | **不互通**(子弹 modifier 引用 `Bullet`,激光 modifier 引用 `LaserEntity`),但 API 形态完全对齐,PR3 起激光 modifier 也会补 `StartTrigger` SR 多态 + 信号总线订阅 | ❌ 0 改动 |
| `FireExtension` / `BaseOffsetStrategy`(子弹版,全局命名空间) | 激光 `LaserFireExtension` 与子弹 `FireExtension` **同套路、同 pipeline 模型**(模块数组 + `ProcessAngle` 串联),但**类独立**(避免全局类同名冲突),`BaseOffsetStrategy` 直接复用子弹版(全局命名空间,FIXED / RandomRange 两条策略够用) | ❌ 0 改动(复用 `BaseOffsetStrategy`) |

### 13.4 激光发射扩展点(LaserFireExtension)

LaserPattern 拥有第三个多态模块数组 `FireExtensions[]`,**与子弹 `FireExtension` 完全同套路**(模块数组 + Pipeline 角度管道),用于在"基础发射方向"上做可插拔扩展(覆盖型 / 累加型 / 批次累加型 / 位置偏移)。

> **设计动机**: PR1 阶段,激光发射方向完全由 `FireLaserAction.AimOffsetDeg` 静态传入,无法在 SO 资产里描述"瞄准玩家 → +180° 绕后""每发旋转 5° 的旋转激光"等节奏型配置。LaserFireExtension 把"动态方向计算"作为可插拔模块暴露给资产,设计师在 Inspector 拼装即可,无需写代码。

**与子弹 `FireExtension` 的差异**(激光系统专门精简):

| 维度 | 子弹 `FireExtension` | 激光 `LaserFireExtension` |
|---|---|---|
| 每批生成数 | N 颗子弹(每发独立方向) | **1 条激光**(单方向) |
| `ProcessAngleForBullet(bulletIndex, totalCount, ...)` | ✅ 有(每发独立方向) | ❌ **无**(激光只有1 个方向) |
| `AccumulatingOffsetAngle.OffsetPerBullet` 字段 | ✅ 有(每发累加) | ❌ **无**(激光没有 bulletIndex) |
| `BaseAngleFireExtension.PositionOffset` 字段 | ✅ 有 | ✅ 有(同名同语义) |
| `BaseOffsetStrategy`(SR 多态,Fixed / RandomRange) | 复用(全局命名空间) | **复用子弹版**,不新建 |
| Pipeline 模型 + Resolver + FireSounds + Pool fireCounts 字典 | 子弹版架构 | **1:1 对齐**,只是类加 `Laser` 前缀避免全局同名冲突 |

**Pipeline 模型(与子弹版完全一致)**:

```
angle = rotationRad                                                  ← 起点(FireLaserAction.AimOffsetDeg)
for ext in FireExtensions (按数组顺序遍历):
    angle = ext.ProcessAngle(from, rotationRad, angle)                ← 上一步角度 → 下一步角度
return angle
```

`LaserFireExtensionResolver.ResolvePipeline(...)` 把数组串成"角度管道",空数组 / null fallback 到 270° + rotationRad。

**位置偏移(`PositionOffset`)**:

- 字段挂在 `BaseAngleLaserFireExtension` 上
- Resolver 入口处(`ResolvePipelineWithOffset`)把 `from += BaseAngle.PositionOffset` 后,再用修正后的 `from` 串 pipeline
- 影响所有后续模块(包括瞄准参考点),与子弹版语义完全对齐

**内置模块**(全部 `[SRName("LaserFireExtension/<名字>")]`,Inspector 下拉可见):

| 子类 | 类型 | 用途 | 关键字段 |
|---|---|---|---|
| `BaseAngleLaserFireExtension` | 覆盖型 | 锚点方向 + 可选位置偏移 | `BaseAngle`(度) / `PositionOffset`(世界坐标 Vector2) |
| `PlayerAimLaserFireExtension` | 覆盖型 | 瞄得到玩家 → 指向玩家;瞄不到 → 透传上游 | (无字段) |
| `OffsetAngleLaserFireExtension` | 单次累加 | `currentAngleRad + N°`(不随开火批次变化) | `OffsetAngle`(度) |
| `AccumulatingOffsetAngleLaserFireExtension` | 批次累加 | `currentAngleRad + (BaseOffset + (fireCount-1)*StepOffset)`,支持随机 BaseOffset | `BaseOffset`(`[SerializeReference, SR] BaseOffsetStrategy`,复用 Fixed / RandomRange) / `StepOffset`(度) |

**拼装示例**:

```
[Base(270°)]                                → 始终向下(单根激光向下)
[Base(0°), PlayerAim]                       → 瞄不到玩家时向右,瞄得到时改指向玩家
[PlayerAim, Offset Angle(+180°)]            → 玩家方向的反方向(绕后激光)
[Base(0°), Accumulating Offset(0°, 5°)]     → 每次开火旋转 5° 的旋转激光(Boss 节奏型)
[Base(0°), Accumulating Offset(Random Range(-15,15), 0°)] → 抖动扩散激光
```

**Pool fireCounts 字典**:

- `LaserPool._fireCounts: Dictionary<LaserFireExtension, int>`(由 `LaserPool.FireGroup` 入口维护,与 `BulletPool._fireCounts` 1:1 对齐)
- key = `pattern.FireExtensions` 数组里的具体元素 ref,value = 累加序号(从 1 起)
- `LaserPool.GetFireExtensionFireCounts()` 暴露给 `LaserPattern.Fire` 内部调 Resolver 时传入
- per-instance 隔离:同一份 LaserPattern SO 被多敌人共用 → 字典按元素 ref 累加,跨敌人共享(项目想要的"Boss 旋转激光跨多次开火累加"行为)

**中心化触发点**:

- `FireSounds` 与 `FireExtensions` 的累加计数都在 `LaserPool.FireGroup` 入口完成(对齐 `BulletPool.FireGroup`)
- `StraightLaserPattern.Fire` 内部不再调用 `PlayFireSounds`(已上移到 Pool 入口),避免重复触发

**类型**(`Assets/Scripts/Laser/FireExtension/`):

| 文件 | 内容 |
|---|---|
| `LaserFireExtension.cs` | 基类 + `BaseAngleLaserFireExtension` + `PlayerAimLaserFireExtension` |
| `LaserOffsetAngleFireExtension.cs` | 单次累加型 |
| `LaserAccumulatingOffsetAngleFireExtension.cs` | 批次累加型(复用子弹版 `BaseOffsetStrategy`) |
| `LaserFireExtensionResolver.cs` | 静态管线调度器(4 个 API + 旧版兼容) |

**★ 命名空间注意**: 所有 `LaserFireExtension*` 文件都放**全局命名空间**(与子弹版同款),否则 `LaserPattern.cs` 看不到(踩坑记录见 `CONTRIBUTING.md §4.7`)。

**扩展**: 新建 `LaserFireExtension` 子类 + 加 `[SRName("LaserFireExtension/<名字>")]` → 自动出现在所有 LaserPattern 资产的 FireExtensions 数组下拉菜单,无需改任何现有 LaserPattern 子类。

---

### 13.5 LaserModifier 实例 + 时间窗/触发器体系

> 板块维护者写自定义 LaserModifier 的参考章节。详细实现以源文件 + 本节为准。
>
> ★ 自 PR3 升级后,`LaserModifier` 已对齐 `BulletModifier` 的完整双时钟 + 信号触发体系(详见 §13.5.4)。

`LaserModifier` 当前**内置实例**:`LaserOrbitModifier` (§13.5.1)。基类时间窗/触发器 API 详见 §13.5.4。

#### 13.5.1 `LaserOrbitModifier` —— 激光圆周运动

`[SRName("LaserModifier/Orbit")]`,文件 `Assets/Scripts/Laser/LaserOrbitModifier.cs`。

**数学原理**: 极坐标公式 `Position(θ) = Center + R × (cos θ, sin θ)`,θ 每帧累加 `AngularSpeed × dt`。**半径 R 是圆周运动的不变量** —— Init 一次后锁死,运行期只累加 θ,无需策划每帧重设半径(这正是用户原问题"应该不能设置半径只需要设置角速度"的物理解释)。

**两种 Pivot 模式**:

| 模式 | 圆心 | 适用 |
|---|---|---|
| `FixedCenter`(默认) | `PresetCenter`(NaN 时 = 激光 Init 时的位置)。**世界坐标锁死**,owner 移动不影响 | 固定场景内圆环武器、场景锚点型弹幕 |
| `FollowOwner` | `owner.position + OffsetFromOwner`。**跟随发射者平移**,绕发射者旋转 | Boss 周围旋转激光、敌机自机半径弹幕 |

**关键字段**:

| 字段 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `Mode` | `PivotMode` enum | `FixedCenter` | 圆心确定模式 |
| `AngularSpeed` | float | 90(度/秒) | **唯一需要策划调的运行时参数**。正值逆时针,负值顺时针。Inspector 用度,内部换算弧度 |
| `PresetCenter` | Vector2 | (NaN, NaN) | FixedCenter 模式专用。NaN 表示用 Init 位置 |
| `OffsetFromOwner` | Vector2 | (0, 0) | FollowOwner 模式专用。圆心相对 owner 的偏移 |

**关键协作点**:

1. **本 modifier 直接重写 `LaserEntity.Position` 和 `LaserEntity.Angle`**(`internal set`,同 Assembly-CSharp 可见),不是改 `Velocity` / `AngularVelocity`。`LaserEntity.LateUpdate` 末尾会自动再同步一次 `transform`,所以视觉立即生效(无 1 帧延迟)。
2. **清零 `laser.Velocity`**: 避免一边圆周一边平移;**不动 `laser.AngularVelocity`**: 本 modifier 不依赖框架累积。
3. **写入的 Angle = 极坐标相位角**(从圆心指向激光),而非切向(切向需要 `+π/2`)。这是「激光从圆心射出」的策划直觉。若未来需要切向,加 `UseTangentAngle` 字段切换即可。
4. **owner Destroy 防御**: FollowOwner 模式下 `_ownerTransform as UnityEngine.Object == null` 检测"伪 null"(对齐子弹版 `HomingEnemyModifier.IsTargetValid`);owner 死后**静默退化**为「保持最后圆心继续转」,激光自然走完生命周期。
5. **池复用安全**: per-instance 状态(`_center / _radius / _phaseAngle / _initialized / _ownerTransform`)全部 `[NonSerialized]`,`Clone()` 走默认 `MemberwiseClone`,池复用时 `ResetWindow()` 把 `_initialized = false` 触发 lazy 重 Init。

**Inspector 用法**(挂到 `Straight Laser Pattern.asset`):

```
Modifiers 数组 → + → 下拉选 LaserModifier/Orbit
  Mode              = FixedCenter          (或 FollowOwner)
  AngularSpeed      = 90                   (度/秒)
  PresetCenter      = (-2, 0)              (任意分量 NaN = 用 Init 位置)
  OffsetFromOwner   = (0, 0)               (FollowOwner 才用)
```

**典型拼装示例**:

| 用例 | 配置 |
|---|---|
| 场景中心画圆、自转 | `Mode=FixedCenter`, `PresetCenter=(0,0)`, `AngularSpeed=90` |
| Boss 周围 8 秒一圈旋转激光 | `Mode=FollowOwner`, `OffsetFromOwner=(1.5,0)`, `AngularSpeed=45`(=360/8) |
| Boss 自机半径弹幕(圆心=Boss 自身) | `Mode=FollowOwner`, `OffsetFromOwner=(0,0)`, `AngularSpeed=任意` |
| 绕某固定世界点 30°/秒逆时针转 | `Mode=FixedCenter`, `PresetCenter=(该点)`, `AngularSpeed=30` |

**约束与未来扩展**:

- **只挂一个 Orbit modifier**: 多个会互相覆盖 Position/Angle(Position 没有 BounceBulletModifier "第一个 true 胜出" 的 bool 返回机制)。
- **未来可继承拓展**: `EllipseOrbitModifier`(椭圆: x/y 半轴分离)、`Figure8OrbitModifier`(8 字形: 两个圆心插值)、`SpiralOrbitModifier`(半径随时间变化) —— 都基于"每帧重写 Position/Angle"的同套路,只需 override `ModifyCore`。
- **Delay/Duration 时序控制**: ★ PR3 起基类已自带(`Delay` / `StartTrigger` / `Duration` / `OneShot`),详见 §13.5.4。本 modifier 不需要自实现计时器。
- **常见时序配置示例**(基类字段直接配即可,无需 override):

  | 场景 | 配置 |
  |---|---|
  | 出生 0.5s 后开始转 | `Delay = 0.5` |
  | 出生 1s 后开始转,转 3s 后停 | `Delay = 1`, `Duration = 3` |
  | Boss 蓄力吼后才开始转 | `StartTrigger = LaserTrigger/On Signal`, SignalName="boss_charge_done" |
  | 转 1 圈后停下(180°/秒) | `AngularSpeed = 180`, `Duration = 2` |

#### 13.5.2 对 `LaserEntity` 的必要改造(配套)

为了支持 `LaserOrbitModifier` 重写 `Position` / `Angle`,`LaserEntity` 做了三处最小改动:

1. **字段访问性**:`Position` / `Angle` 的 setter 从 `private` 放宽为 `internal`(同程序集可见,外部仍只读)。理由:modifier 与 `LaserEntity` 同 Assembly-CSharp,`internal` 已足够。
2. **新增 `OwnerHitbox` 字段**:`Init` 时除了透传 `Team`,也保留 `HitboxComponent` 引用,供 `FollowOwner` 模式读 `owner.position`。
3. **`LateUpdate` 补一次 transform 同步**: step 1 后、step 3 前的 modifier OnTick 阶段,modifier 可能改 Position/Angle,在 modifier 调用结束后、状态机推进前再 `transform.position = Position; transform.rotation = ...`,确保 Renderer 拿到的是 modifier 写完后的最新值(避免视觉延迟 1 帧)。

这三处改动**完全向后兼容**: 现有 LaserPattern 资产 / LaserModifier 子类 / Renderer 子类 / 碰撞逻辑零修改(它们只读 `Position` / `Angle`,从不写)。

#### 13.5.3 扩展指南

新建 `LaserOrbitModifier` 之外的 modifier(波形、跟随玩家、生命周期翻转 ...):

1. 新建 `Assets/Scripts/Laser/<YourModifier>.cs`
2. 继承 `LaserModifier` + 加 `[Serializable, SRName("LaserModifier/<名字>")]`
3. override **`ModifyCore(LaserEntity laser, float dt)`**(不是 `OnTick` —— PR3 起 `OnTick` 已重命名,基类 `Modify()` 统一管时间窗)
4. per-instance 状态用 `[NonSerialized]`,池复用时基类 `ResetWindow` 会调 **`protected override void OnResetWindow()`** 虚钩子(对齐子弹版 `OnWindowEnter` 钩子模型),子类 override 这个钩子清自己的状态即可(基类 `OnResetWindow` 默认空,**不需要 super 调 base**)。`ResetWindow` 本身仍是 `public void`(非 virtual,子类不能 override)
5. 引用类型字段(`Transform` 等)override `Clone()` 深拷;值类型字段用默认 `MemberwiseClone`
6. owner Destroy 防御走 `_owner as UnityEngine.Object == null`(对齐子弹版)
7. 如需「进入窗口瞬间一次」逻辑:override `protected override void OnWindowEnter(LaserEntity laser)`,配合 `OneShot = true` 实现一次性触发
8. 如需「退出窗口清理」逻辑:override `protected override void OnWindowExitCleanup(LaserEntity laser)`(基类 `OnWindowExit` 默认 noop 后调到这里)

详见 CONTRIBUTING.md §2 "新 BulletModifier" 行(架构套路 1:1 对仗,只把 Bullet 换成 LaserEntity、把 FireExtension 换成 LaserFireExtension)。

#### 13.5.4 时间窗/触发器体系(PR3 升级核心)

自 PR3 起,`LaserModifier` 对齐 `BulletModifier` 的完整双时钟 + 信号触发体系,激光 modifier **自动**获得以下能力,无需任何额外代码:

**Inspector 字段**(对所有 modifier 子类生效,默认值与 PR1 旧行为 100% 等价):

| 字段 | 类型 | 默认 | 等价 PR1 行为 |
|---|---|---|---|
| `Delay` | float | 0 | 旧版不存在该概念,默认 0 = 出生即生效 |
| `StartTrigger` | `[SerializeReference, SR] LaserModifierStartTrigger` | null | 旧版不存在;null → 兜底 `DelayLaserModifierStartTrigger{Delay=this.Delay}` |
| `Duration` | float | 0 | 旧版不存在;0 = 永久生效 |
| `AutoSkipOutsideWindow` | bool | true | 旧版没有窗口概念,默认行为等价 |
| `OneShot` | bool | false | 旧版不存在;false = 持续型 |

**双时钟模型**(对齐 `BulletModifier`):

- `_elapsed` (时钟 A):激光出生至今,一直累加,供 `StartTrigger.ShouldActivate` 判断
- `_windowElapsed` (时钟 B):窗口内累计时间,只在 `_isActive=true` 时累加,直接对接 `Duration`
- `_windowStarted` (粘性位):窗口是否「已触发过」,OneShot 锁死 + 信号型 trigger 不重复触发
- `_windowExhausted` (OneShot 锁死位):触发过一次后,后续不再激活

**Modify 流程**(基类 `public void Modify(LaserEntity laser, float deltaTime)` 统一调度):

```
1. _elapsed += dt
2. StartTrigger.ShouldActivate(laser, _elapsed) → triggerReady
3. durationExpired = Duration > 0 && _windowElapsed >= Duration
4. 计算 nowActive(未触发 / OneShot 已结束 / 持续 中 三阶段)
5. 边缘触发:enter / exit(OneShot 触发后立刻 exit + 锁死)
6. AutoSkipOutsideWindow 时,窗口外直接 return(子类 ModifyCore 不会被调)
7. 子类 ModifyCore(laser, dt) ← 子类 override 这个
```

**向后兼容性**: 老 .asset 反序列化后 `StartTrigger` 字段为 null,`ResetWindow` 兜底为 `DelayLaserModifierStartTrigger{Delay = this.Delay}`,基类 `Delay=0` 时立即激活,**行为 100% 等价**。

#### 13.5.5 LaserModifierStartTrigger 实例

`LaserModifierStartTrigger` 基类(全局命名空间,与 `ModifierStartTrigger` 同款 —— 详见 CONTRIBUTING.md §4.7)位于 `Assets/Scripts/Laser/LaserModifierStartTrigger.cs`,提供 **3 个 SR 多态子类**:

| 子类 | SRName | 用途 | 关键字段 |
|---|---|---|---|
| `DelayLaserModifierStartTrigger` | `LaserTrigger/Delay` | `elapsed >= Delay` 即激活。默认,等价基类 `Delay` 字段 | `Delay`(float) |
| `OnSignalLaserModifierStartTrigger` | `LaserTrigger/On Signal` | 订阅 `BulletSignalBus` 信号,收到即激活。可配 MaxWait 兜底 + 距离判定 | `SignalName` / `MaxWait` / `RequireInRange` / `MaxDistanceFromOrigin` |
| `DelayOrSignalLaserModifierStartTrigger` | `LaserTrigger/Delay Or Signal` | Delay 与信号任一先到即激活 | `Delay` / `SignalName` |

**★ 跨子体系联动 ★**

激光 signal trigger **复用 `BulletSignalBus`**(不新建 `LaserSignalBus`)。同一信号名可同时被子弹版 `OnSignalStartTrigger` + 激光版 `OnSignalLaserModifierStartTrigger` 订阅 —— Emit 一次全场响应。

典型场景: Boss 蓄力吼(`EmitSignalAction.Emit("boss_charge_done")`) → 所有挂 `OnSignalStartTrigger` 的子弹 + 所有挂 `OnSignalLaserModifierStartTrigger` 的激光**同时**激活,零额外代码。

**订阅生命周期**:

- `OnAttach(LaserEntity laser)`:modifier 挂到激光时调一次,订阅型 trigger 在这里 `Subscribe`
- `OnDetach(LaserEntity laser)`:激光回池/销毁时调一次,订阅型 trigger 在这里 `Unsubscribe`
- 调用入口:
  - 订阅:`LaserPattern.AttachModifiers` → `laser.AttachSignalTriggers()`(在 `ResetAllModifierWindows` **之后**)
  - 摘除:`LaserEntity.OnDisable` → `DetachSignalTriggers()`(在 `ClearModifiers` **之前**)→ 同时被 `LaserPool.Return` 同步触发(`SetActive(false)` 同步 OnDisable)

**Owner Destroy 防御**: `OnSignalLaserModifierStartTrigger` 的 `_attachedLaser as UnityEngine.Object == null` 检测(对齐子弹版 `HomingEnemyModifier.IsTargetValid`),owner 死后 trigger 静默失效。

**深拷**: `LaserModifier.Clone` 自动 `StartTrigger.Clone()` 深拷,确保每条激光的 trigger 独立(否则订阅型 trigger 会被多条激光共享,导致订阅泄漏 + 状态污染)。

#### 13.5.6 对 `LaserEntity` 的 PR3 进一步改造

为支持时间窗/触发器体系,`LaserEntity` 在 §13.5.2 三处改动基础上再追加 **3 处**:

1. **LateUpdate 调度入口改名**: `_modifiers[i].OnTick(this, dt)` → `_modifiers[i].Modify(this, dt)`(基类 `Modify` 统一管时间窗 + 调 `ModifyCore`)。
2. **新增 `AttachSignalTriggers()` 方法**:遍历 `_modifiers` 调每个 `StartTrigger.OnAttach(this)`,由 `LaserPattern.AttachModifiers` 在 `ResetAllModifierWindows` 之后调用。
3. **新增 `DetachSignalTriggers()` 方法**:遍历 `_modifiers` 调每个 `StartTrigger.OnDetach(this)`,由 `LaserEntity.OnDisable` 在 `ClearModifiers` 之前调用,实现订阅生命周期闭环。
4. **OnDisable 兜底**: `DetachSignalTriggers()` + `ClearModifiers()`(顺序关键,Detach 先于 Clear),防「激光已回池但 handler 还在 `_subs` 字典里」导致下次 `Emit` 时 NRE。

**Pool.Return 路径差异**(对齐子弹版但不强制双调):

- 子弹版:`BulletPool.Return` 显式调 `bullet.DetachSignalTriggers + ClearModifiers`(因为 `Bullet.OnDestroy` 才触发 `OnDisable`)
- 激光版:`LaserPool.Return` 只调 `SetActive(false)` —— `SetActive(false)` **同步触发** `LaserEntity.OnDisable`,OnDisable 内部已调 `DetachSignalTriggers + ClearModifiers`,不再重复调(避免遍历两次 `_modifiers`)

这是激光版 vs 子弹版的差异,**不是 bug**。

#### 13.5.7 对 `LaserOrbitModifier` 的升级(向后兼容 breaking change)

PR3 升级后,`LaserOrbitModifier` 必须做以下改动(其他 modifier 子类若编写需遵循):

- `public override void OnTick(LaserEntity, float)` → **`public override void ModifyCore(LaserEntity, float)`**(重命名 + 语义保留)
- ~~`public override void ResetWindow()` 必须在头部加 `base.ResetWindow()`~~(已废弃 —— `ResetWindow` 在基类是非 virtual 的,override 它会触发 CS0506)。
  ★ 正确做法:override **`protected override void OnResetWindow()`** 虚钩子(对齐子弹版 `OnWindowEnter` 钩子模型)。基类 `ResetWindow` 由 `LaserEntity.ResetAllModifierWindows` 显式调用,内部会自动跑完双时钟清零 → 兜底 StartTrigger → 调 `OnResetWindow()` 形成完整链路;子类 override `OnResetWindow` 只需要清自己的 lazy Init 状态,不需要 super 调 base(基类 `OnResetWindow` 默认空)。

**用户视角**:已挂 `LaserOrbitModifier` 的 `.asset` **零修改** —— `Delay=0` / `StartTrigger=null` / `Duration=0` 默认值与 PR1 行为 100% 等价;新功能(Delay / Duration / StartTrigger / OneShot)**自动可用**,策划只需在 Inspector 配字段,无需改代码。

---

### 13.6 与既有层的关系

```
┌──────────────────────────────────────────────────────────────────┐
│ 游戏场景 (Scene)                                                  │
│  ├─ 玩家:挂 Player + PlayerMovement + PlayerShooting + Options  │


## 与其他板块的关系

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [bullet](./arch-bullet.md) — 与 Bullet 子体系平行、互不依赖
- [fire-pattern](./arch-fire-pattern.md) — LaserPattern 复用 FireSound 数组;LaserFireExtension 架构对齐 FireExtension(pipeline 模型 + Resolver + per-FireExtension fireCount 字典),复用 BaseOffsetStrategy
- [enemy-ai](./arch-enemy-ai.md) — FireLaserAction 接入 BehaviorFlow,与 FireAction 对仗
- [hitbox](./arch-hitbox.md) — 阵营过滤共用 CollisionTeam,但不走 AABB 网格
- [bounds](./arch-bounds.md) — 出界判定复用 BoundsService.ContainsCulling
