# 子弹系统

> BulletPool + Bullet + BulletModifier + 反弹 + 信号触发

> 本板块对应 ARCHITECTURE § 2. 子弹系统(原 ARCHITECTURE.md 第 88–567 行)。
> 本文档面向项目维护者,不复制实现细节,字段 / 数值 / 默认值以源文件为准。

---

## 2. 子弹系统

**职责分工:**
- **BulletPool** —— 子弹的工厂 + 回收站(场景单例)。按 prefab 分桶复用,避免 GC;同时是 modifier 的统一挂载入口。
- **Bullet** —— 飞行体 + 总控(自动挂 HitboxComponent,Awake 注入);`Update` 第 1 步调度所有 modifier,再驱动移动/旋转。
- **BulletModifier** —— 子弹行为的可插拔修饰(加速 / 转向 / 减速 / 分裂等),走 `[SerializeReference, SR]` 多态,在 `FirePattern` 或 `FireAction` 里下拉选类型。

**协作边界:**
- 调用方通过 `BulletPool` 拿弹或一步触发一个 FirePattern;`FireGroup` 接收 `ownerHitbox` 透传阵营。
- 子弹不会自动回收 —— 出界 / 命中后由调用方或 modifier 决定 `Return` 时机。
- 全屏消弹统一调用 `BulletPool.ReturnAll`，它先快照活跃集合再逐颗走标准 `Return`，因此会正确解除信号订阅、清理 modifier 并按 prefab 回池；不要由外部直接禁用子弹 GameObject。

**阵营语义:**
- 子弹阵营由**发射者**透传,prefab 不需要手动配 `Hitbox.Team`。
- 玩家弹命中敌人 → 按子弹 `Damage` 扣血;敌人弹命中玩家 → 固定扣 1 命,无视 `Damage`。

### 2.1 子弹朝向约定(美术 / 代码对齐)

- **角度 0°** 在代码里 = 指向 `+X`(右),用 `Mathf.Cos / Sin` 直接算方向。
- 美术贴图默认尖头朝 `+Y`(朝上) —— 这是 **STG 美术资源的惯例**,Bullet 在旋转时统一加 `-90°` 偏移:
  ```csharp
  // Bullet.Init / Bullet.Update 都用这个公式
  transform.rotation = Quaternion.Euler(0, 0, SteerAngle * Mathf.Rad2Deg - 90f);
  ```
- 视觉对照表(美术尖头朝上):

  | SteerAngle | 飞行方向 | 贴图朝向 | 视觉 |
  |---|---|---|---|
  | 0° (右) | (1, 0) | z = -90° | 尖头朝右 ✅ |
  | 90° (上) | (0, 1) | z = 0° | 尖头朝上 ✅ |
  | 180° (左) | (-1, 0) | z = 90° | 尖头朝左 ✅ |
  | 270° (下) | (0, -1) | z = 180° | 尖头朝下 ✅ |

  > 💡 如果某些子弹贴图尖头朝 `+X`(横的),需要单独为它做补偿;当前所有内置 prefab 都按"尖头朝上"制作。

### 2.2 BulletModifier 多态修饰体系

BulletModifier 是子弹行为的**可插拔插件**。每个 modifier 是纯 C# 类(非 MonoBehaviour、非 ScriptableObject),走项目统一的 `[SerializeReference, SR]` 多态扩展套路(与 `EnemyAction` / `MoveBehaviour` / `BossPhase` 等一致)。

**Modifier 何时跑?**
```csharp
// Bullet.Update()
void Update() {
    // 1. modifier 改 b.Speed / b.SteerAngle / b.AngularSpeed 等
    foreach (var m in _modifiers) m.Modify(this, dt);

    // 2. AngularSpeed 累加到飞行方向(SteerAngle)
    SteerAngle += AngularSpeed * dt;

    // 3. 按当前方向移动 + 同步旋转
    Vector2 dir = new Vector2(Mathf.Cos(SteerAngle), Mathf.Sin(SteerAngle));
    transform.position += (Vector3)(dir * Speed * dt);
    transform.rotation = Quaternion.Euler(0, 0, SteerAngle * Mathf.Rad2Deg - 90f);
}
```

**Modifier 在哪里配置?两层入口:**

| 配置位置 | 生效范围 | 字段 | 写法 |
|---|---|---|---|
| `FirePattern.ModifierPrefabs` | 该 pattern 发出的所有子弹 | `[SerializeReference, SR] BulletModifier[]` | Inspector 点 `+` 下拉选类型 |
| `FireAction.ExtraModifierPrefabs` | 仅本次 Fire 调用(覆盖到上面之后) | `[SerializeReference, SR] BulletModifier[]` | 同上 |

**运行时挂载路径**(单颗子弹的完整链路):
```
FireAction.OnTick
  → BulletPool.FireGroup(pattern, pos, rot, hitbox, ExtraModifierPrefabs)
    → pattern.Fire(pos, rot, this, hitbox, ExtraModifierPrefabs)
      → SpawnBullet(...)  // FirePattern 基类的 protected helper,合并 ModifierPrefabs + extras
        → pool.Get(prefab, ..., modifiersToAttach)  // 8 参重载
          → b.Init(...)  // ClearModifiers(回池后清空旧 modifier)
          → AttachModifiers(b, mods)  // 遍历 + Clone() + AddModifier
            → bullet.AddModifier(mod.Clone())  // Clone 出一份独立实例
          → _active.Add(b)
```

**为什么走 Clone 而不是共享?**

- 同一 FirePattern 资产是 SO,**被 N 个敌人引用**;
- 如果 modifier 是单例被 N 颗子弹共享,任何 modifier 上的状态字段(如 `计时器` / `追踪冷却`)会被所有子弹同时读写,行为完全错乱;
- **Clone 出独立实例**(默认 `MemberwiseClone`,纯值类型字段无开销,引用类型字段需手动 override),保证 per-instance state 安全。

### 2.3 BulletModifier 字段约定(写 modifier 时遵守)

| 字段类型 | 默认 Clone 行为 | 是否需要 override Clone |
|---|---|---|
| `float` / `int` / `bool` / `Vector2` 等值类型 | ✅ 浅拷贝足够,无共享问题 | ❌ 不需要 |
| `Transform` / `GameObject`(UnityEngine.Object 引用) | ⚠️ 浅拷贝共享引用(基本安全,很少写) | 通常不需要 |
| `List<T>` / `T[]` / 字典 | ⚠️ **共享同一集合**,Add/Remove 会污染 | ✅ **必须 override Clone 深拷** |
| 自定义 `class` 字段 | ⚠️ 共享同一对象 | ✅ **必须 override Clone 深拷** |

**Modifier 不可做的事:**
- ❌ 访问 `this.transform`(modifier 不是 GameObject,会 NRE)
- ❌ 用 `MonoBehaviour` 派生(违反本架构一致性原则)
- ❌ 持有场景级单例的"硬引用"(会让 modifier 跟场景耦合)

### 2.4 内置 BulletModifier 示例

| 类型名 | SRName | 行为 | 关键字段 |
|---|---|---|---|
| `AccelerateModifier` | `Modifier/Accelerate` | `b.Speed += Acceleration * dt` | `Acceleration` (float, 默认 5) |
| `SteerTowardModifier` | `Modifier/Steer` | 每帧把 `b.AngularSpeed = TurnRate * Deg2Rad`(由 `Bullet.Update` 第 2 步自动累加到 `SteerAngle`) | `TurnRate` (度/秒, 默认 90) |
| `HomingEnemyModifier` | `Modifier/Homing Enemy` | 通过 `CollisionService.Grid.QueryRadius(...)` 查最近敌人,按 `TurnRate` 限速转向 | `SearchRadius` / `TurnRate` / `LockOnDelay` / `MaxHomingTime` |
| `BulletColorModifier` | `Modifier/Color` | 走 **MaterialPropertyBlock** 给 shader 的 `_TintColor` 染色(Solid / FadeByLifetime / Flash 三模式);**必须配套 `Assets/Shaders/BulletTint.shader`** —— 该 shader 只对暗像素染色,白色高光像素保持纯白(经典 STG 效果)。需 `Bullet.Renderer` 字段(Awake/Reset 自动 `GetComponentInChildren<SpriteRenderer>(true)` 抓取)。需把 bullet prefab 的 SpriteRenderer.Material 切到 `STG/BulletTint` | `Mode` / `Color` / `FadeOutColor` / `FadeStart` / `ReferenceLifetime` / `FlashFrequency` / `FlashMinAlpha` |
| ~~`SpawnRingOnDelayModifier`~~ | ~~`Modifier/Spawn Ring on Delay`~~ | **已弃用删除** —— 由 `FireOnEnterBulletModifier` 取代(通用 + 任意 FirePattern + 阵营可继承) | — |
| `FireOnEnterBulletModifier` | `Modifier/Fire Pattern On Delay` | **OneShot 分裂**:子弹飞 `Delay` 秒后,在自身位置按 `Pattern` 再开一次火(走 `BulletPool.FireGroup` 完整链路:FireExtensions / ModifierPrefabs / SpawnFog / FireSounds 全生效),然后本 modifier 结束。分裂弹阵营可继承母弹(默认)或设为中性。 | `Pattern`(FirePattern 资产)/ `InheritOwnerTeam` / `ExtraModifiers` |
| `FireOnDurationBulletModifier` | `Modifier/Fire Pattern While Active` | **持续型分裂**:窗口期内(Delay 之后 Duration 秒内)每 `Interval` 秒在自身位置按 `Pattern` 开一次火,最多 `MaxShots` 次。典型用法:弹尾粒子、激光拼接、Boss 散弹母弹持续生子弹。 | `Pattern` / `Interval` / `MaxShots` / `InheritOwnerTeam` / `ExtraModifiers` |
| `AngleOffsetFirePatternBulletExtra` | `Extra/Angle Offset` | **FirePatternBulletExtra 子类**:挂在上面两个分裂 modifier 的 `Extra` 字段上,让母弹每次 FireOnce 时按「本批次偏移 + 累加偏移」调整 rotationRad。第 N 次偏移 = `BaseOffset.Sample() + (N-1) * StepOffset`(度)。`BaseOffset` 本身是 SR 多态(可下拉选 `Base Offset/Fixed` 精确值 / `Base Offset/Random Range` 区间随机),抽样时机 = 每颗母弹第一次 FireOnce 时抽一次,窗口期内保持。典型用法:旋转喷射、扇形铺开、抖动扩散。 | `BaseOffset`(SR 多态)/ `StepOffset` |
| `BounceBulletModifier` | `Modifier/Bounce` | **反弹 modifier**:子弹飞出 `BoundsService.CullingArea`(无 BoundsService 时 fallback ±10/±20,与 Bullet 越界回收共用同一判定)时按物理规律翻转方向 + Clamp 位置 + 扣次数。遵守 §2.6 的 Delay/Duration/OneShot 时间窗口。可配「可反弹边集合」(HorizontalOnly / VerticalOnly / All / **ExceptBottom** 枚举 —— 4 边独立判断,可表达「撞顶/撞墙弹,撞地不弹」的常见玩法)+ 反弹次数(0=立刻回收 / 正数=N 次 / 负数=无限)+ 恢复系数 Restitution(0=完全非弹性贴墙滑行 / 1=完全弹性速率不变 / >1=反弹后加速反物理但 STG 偶尔需要)。**不打断 AngularSpeed**——反弹后继续按原旋转状态飞,真要切直线的子类自己改 `b.AngularSpeed = 0`。**反射数学只翻 SteerAngle**(SteerAngle' = π - α 或 -α,与 `Bullet.Update` 第 2-3 步自然衔接)。详见 §2.7。 | `MaxBounces` / `Walls` / `Restitution` + §2.6 时间窗口字段 |

**§2.4 辅助类:`BaseOffsetStrategy` SR 多态**(挂在 `AngleOffsetFirePatternBulletExtra.BaseOffset` 上):

| 类 | SRName | 用途 | 字段 |
|---|---|---|---|
| `FixedBaseOffsetStrategy` | `Base Offset/Fixed` | 精确值(对齐旧版 `BaseOffset = X` 行为) | `Value` |
| `RandomRangeBaseOffsetStrategy` | `Base Offset/Random Range` | 区间随机抽样(每颗母弹第一次 FireOnce 时抽一次,之后保持) | `Min` / `Max`(float,度;对称区间如 Min = -15, Max = 15 = ±15° 抖动) |

**追踪玩家(敌人弹挂的 modifier)是常见扩展**,但当前项目未内置 `HomingPlayerModifier`(因玩家是单例,目标解析无需走网格);按下方"扩展点"小节的三步套路自行实现即可,大致套路是用 `ShinySTG.Player.Player.Instance?.transform` 拿到目标,其余转向逻辑与 `HomingEnemyModifier` 同源。

> ★ 上表所有内置 modifier 都自动支持 §2.6 的 `Delay` / `Duration` / `OneShot` 时间窗口,基类统一管理,无需子类手动实现。

#### 2.4.1 BulletColorModifier 首次使用步骤(★ 4 步)

> BulletColorModifier 是个 **跨美术 / shader / C# 三层的视觉系统**,首次配置必须在 Unity Editor 里走一遍 4 步:
>
> | 步骤 | 在哪儿 | 做什么 |
> |---|---|---|
> | **1. 创建材质资产** | Project 窗口右键 → `Create → Material` | 命名 `BulletTint`(放在 `Assets/Shaders/` 旁边或 `Assets/Materials/`)|
> | **2. 指 shader** | 选中新材质 → Inspector 顶部 Shader 下拉框 | 选 `STG → BulletTint`(配套 shader `Assets/Shaders/BulletTint.shader`)|
> | **3. 挂到 bullet prefab** | 打开每个 bullet prefab(目前 `Assets/Prefabs/Bullet/` 下 + `NatsuhaAOptionBullet`)| SpriteRenderer.Material 字段 → 拖入 `BulletTint.mat` |
> | **4. 配 FirePattern modifier** | FirePattern.ModifierPrefabs 数组点 `+` → 选 `Modifier/Color` | 设 `Mode = Solid`、`Color = 主色`(主炮红 / 子机蓝 / Boss 紫 ...) |
>
> **典型调试图:**
> - 子弹全红没高光 → 美术 sprite 灰度分布不对(白色像素太少)。改 sprite 或调 shader 的 `_LuminanceMax`(0~1,默认 0.65)
> - 子弹染色太淡 → `Color` 字段 alpha 调到 1.0
> - 子弹不变色 → bullet prefab 的 SpriteRenderer.Material 没指向 `BulletTint.mat`,或 shader 编译失败(看 Console)
> - 想彻底关闭染色恢复原图 → `Color.a = 0`(透传 alpha=0 到 shader,等价于不染色)
>
> **每个 prefab 只需挂一次 material**(prefab 引用,所有 instance 共享)。**每个 FirePattern 需挂自己的 BulletColorModifier**(主炮红 / 子机蓝各自配一份,Inspector 右键 Duplicate 即可)。

### 2.5 扩展点

**新增 modifier 类型**:三步套路(与 EnemyAction 等完全一致)
1. 在 `Assets/Scripts/Bullet/` 新建 `<你的>Modifier.cs`
2. 继承 `BulletModifier`(`[Serializable]` abstract class),加 `[SRName("Modifier/<名字>")]`
3. override `ModifyCore(Bullet b, float dt)`,通过修改 `b.Speed` / `b.SteerAngle` / `b.AngularSpeed` / `b.Lifetime` 等子弹字段影响行为
   - ★ `Modify` 是 `sealed`(基类统一管时间窗口,不可 override),子类**必须** override `ModifyCore`,否则编译失败

需要引用类型字段深拷时 override `Clone()`,默认 `MemberwiseClone` 对值类型字段够用。

**Modifier 如何查询空间信息(追踪 / 找最近敌人 / 任何需要"看到"其他 hitbox 的逻辑)**
- `CollisionService` 公开只读属性 `Grid`(类型 `UniformGrid`),每帧 `LateUpdate` 开头 Clear + 重建,内含所有活跃 hitbox(玩家 + 敌人 + 子弹)。
- 在 modifier 内调 `CollisionService.Instance.Grid.QueryRadius(point, radius)`,拿半径 r 内的所有 `HitboxComponent`,自己按 `hb.Team` 过滤(玩家阵营 / 敌人阵营 / 中立),按距离 / 角度等选目标。
- `QueryRadius` 是 `UniformGrid` 暴露的公开 API,基于均匀网格索引,O(候选数) 选目标;同 `Query3x3(center)` 是 `QueryRadius(center, cellSize)` 的薄封装。
- **生命周期约定**:modifier 的 `ModifyCore` 由基类的 `Modify` 调度,`Modify` 由 `Bullet.Update` 调用(`Update` 阶段),`CollisionService.LateUpdate` 才建网格 —— 同一帧内 modifier 读到的网格是**上一帧**的快照。STG 帧率下两帧差异 < 1/60s,实际无感知,但**不能**假设"本帧新生成的目标立刻可见"。


**新增「子弹再发射」modifier**(分裂类)走 FirePatternBulletModifier 抽象基类(`Assets/Scripts/Bullet/FirePatternBulletModifier.cs`):
- 子类只需 override `ComputeCenterAngle(Bullet)`(默认返回 `b.SteerAngle`,母弹当前朝向) + 决定触发时机(OnWindowEnter / ModifyCore)
- 走 `BulletPool.FireGroup(Pattern, b.Position, angle, ownerHitbox, extraModifiers)` 完整链路,自动透传 `Pattern` 的全部能力
- `ownerHitbox` 取值由 `InheritOwnerTeam` 字段决定: true → `b.Hitbox`(继承母弹阵营),false → null(中性)
- 想加新的分裂模式(如「按玩家方向分裂」「按母弹反方向分裂」)→ 新建 `FirePatternBulletModifier` 子类 + `[SRName("Modifier/<名字>")]`,无需改任何 Bullet / FirePattern / BulletPool

**母弹 → 分裂弹 信息传递**(挂在 `FirePatternBulletModifier.Extra` 的 `[SerializeReference, SR]` 多态字段):
- 已内置:`Extra/None`(显式不传递占位)、`Extra/Angle Offset`(本批次偏移 + 累加偏移)
- `Angle Offset` 的 `BaseOffset` 本身也是 SR 多态(`Base Offset/Fixed` 精确值 / `Base Offset/Random Range` 区间随机)
- 接口:`GetRotationOffset()` 返回本次旋转角增量 + `OnFireTriggered(Bullet host)` 钩子 + `SampleBatch()` Synchronized 入口

**本批 BaseOffset 共享**(挂在 `AngleOffsetFirePatternBulletExtra.BatchSample` 字段):
- `Independent`(默认):每颗母弹独立 Sample(模糊抖动、不规则分裂)
- `Synchronized`:本批只 Sample 一次,本批所有母弹共用(精准扇形、节拍同步)
- 机制:`BulletPool.FireGroup` 入口遍历本批 `extraModifiers`(BulletModifier[]),找挂有 AngleOffset 的 `FirePatternBulletModifier` 子类(FireOnEnter / FireOnDuration),调其 `Extra` 字段的 `OnBatchFire()` 提前抽样一次
  - 抽样结果(`_sampledBaseOffset` + `_fireCount` 归 0 + `_baseOffsetSampled=true`)通过 `MemberwiseClone` 字段浅拷贝传到本批每颗母弹 modifier 上 → 每颗母弹飞行 N 秒后 OnFireTriggered 直接用 `_sampledBaseOffset`,跳过抽样
  - 仅当 `BatchSample = Synchronized` 才生效;Independent 模式 OnBatchFire 直接 return,每颗母弹 AngleOffset 在 OnFireTriggered 时各自 Sample(旧行为,完全兼容)
- 字段位置刻意放在 `AngleOffset` 内部(而非 FirePattern 上),因为同步语义只影响有 AngleOffset 的母弹配置 —— 用户只在 AngleOffset 字段配置即可,不必双层切换
- `CompositeFirePattern` 自动兼容:无论 AngleOffset 挂在 FirePattern.ModifierPrefabs 还是 FireAction.ExtraModifierPrefabs 或子 pattern 上,BulletPool 入口遍历 extraModifiers 都能找到
- 旧 .asset 反序列化时 `BatchSample` 字段不存在 → 走枚举默认值 `Independent`,行为 100% 兼容历史

### 2.6 时间窗口(Delay / Duration / OneShot)

**所有 BulletModifier 自动支持时间窗口**,由基类统一管理,子类只需 override `ModifyCore`(不要 override `Modify`,它是 sealed)。

#### 字段语义

| 字段 | 默认 | 含义 |
|---|---|---|
| `Delay` | `0` | 从子弹生成开始,等多少秒后才进入窗口。子弹生成时刻 = `BulletPool.AttachModifiers` 调完 `ResetAllModifierWindows()` 那一刻。 |
| `Duration` | `0`(≤0 = 永久) | 进入窗口后持续多少秒。`<=0` 视为**永久生效**(与历史行为一致)。 |
| `AutoSkipOutsideWindow` | `true` | 窗口外是否直接 return。`false` 时仍每帧调 `ModifyCore`,子类通过 `IsActive` 自行判断。 |
| `OneShot` | `false` | 一次性触发:进入窗口瞬间调一次 `OnWindowEnter`,然后立刻退出(`ModifyCore` 不再被调用)。 |

#### 时序图(双时钟模型,自 vX 起)

```
两个独立时钟:
  - 时钟 A = _elapsed(子弹出生至今,一直累加) ── 供 StartTrigger.ShouldActivate 判断
  - 时钟 B = _windowElapsed(窗口内累计)  ── 与 Duration 直接比较,触发 OnWindowEnter/Exit

时间轴  0 ──────────────────── triggerReady ────────── triggerReady+Duration ────── ∞
        │                       │                       │                       │
        │   ┌─ 时钟 A 视角 ─────┼───────┬───────────────┼───────┐               │
        │   │ 出生至今          │       │               │       │               │
        │   │                   │ triggerReady         │       │               │
        │   ├─ 时钟 B 视角 ─────┼───────┼───────────────┼───────┤               │
        │   │ 窗口内累计        │ 进入窗口(_windowElapsed=0 重置,开始累加) │  超期退出 │
        │                       │                       │                       │
        ├──────── 窗外 ────────┴──── 窗口内 ───────────┴──────── 窗外 ───────────┤
        │ 不调 ModifyCore                    │  不调 ModifyCore
        │ (除非 AutoSkip=false)              │
                                          (或者 OneShot=true: 进入瞬间调 OnWindowEnter 一次,
                                           立刻调 OnWindowExit, _windowExhausted=true 锁死)

★ 关键不变量:
  - triggerReady = true 那一刻,_windowElapsed 立刻从 0 开始累加,Duration 从此刻起算。
  - 信号型 trigger(Trigger/On Signal):收到信号即 triggerReady → 窗口立刻激活,持续 Duration。
  - triggerReady 之前,信号没到也无所谓 —— 窗口不在内,_windowElapsed 不增。
```

#### 为什么不把 Delay 和 Duration 放在同一个时间轴上?

旧实现(2026-09 之前):窗口判定用「出生后 [Delay, Delay+Duration)」这一个区间,trigger 信号型也用 `_elapsed < Delay+Duration` 做预算。这有两个严重问题:

1. **信号型 + Duration>0 时,信号若在 Delay+Duration 之后到达 → 永远进不了窗口**(策划期望「信号到即激活 + 持续 N 秒」完全失效)。
2. **基类 Delay 与 trigger 内部 Delay 字段重复**,ResetWindow 同步时会用基类 Delay(默认 0)覆盖 trigger 配置值。

修复:把「**何时进入窗口**」(trigger 决定)和「**进入后持续多久**」(Duration 决定)解耦为两个独立维度 + 两个独立时钟。trigger 决定的是时钟 A 上的一个时刻,Duration 决定的是从那一刻起时钟 B 上能跑多远。两者各自有独立的边界判定,互不干扰。

#### 默认行为与历史兼容性

| 默认字段组合 | 行为 | 与历史关系 |
|---|---|---|
| `Delay=0, Duration=0, AutoSkip=true` | 每帧调 ModifyCore | **与历史 100% 等价**(旧 .asset 完全无感) |
| `Delay=0, Duration=2` | 出生 2 秒后 modifier 停止工作 | 新增能力:追踪 2 秒后切直线、闪烁 1.5s 后熄灭 |
| `Delay=0.5, Duration=0` | 前 0.5 秒不工作,之后永久工作 | 新增能力:子弹先直线 0.5 秒,再开始追踪 |
| `OneShot=true` | 进入窗口瞬间调一次 OnWindowEnter | 新增能力:定时分裂、定时音效、定时换贴图 |

#### 钩子(override 即可)

| 钩子 | 何时调 | 用途 |
|---|---|---|
| `OnWindowEnter(Bullet)` | 窗口从非激活 → 激活的瞬间 | **OneShot 主战场**(生成弹/播音效/切贴图);非 OneShot 也可做"进追踪瞬间播 lock-on 音效" |
| `OnWindowExit(Bullet)` | 窗口从激活 → 非激活的瞬间(含 OneShot 触发后立刻退出、Duration 到期、子弹回池) | **基类默认**:清零 `b.AngularSpeed`(让子弹切直线,这是几乎所有 modifier 的共同期望)。子类需要追加清理时**不要 override 这个**,应 override `OnWindowExitCleanup` —— 这样 AngularSpeed 一定会被清,不会忘 |
| `OnWindowExitCleanup(Bullet)` | 在基类 `OnWindowExit` 清完 `AngularSpeed` 后调用 | 子类追加自己的清理(恢复原色 / 清引用 / 停粒子)。**不要在这里改 AngularSpeed**(基类已清) |
| `ModifyCore(Bullet, dt)` | 窗口内每帧(除非 OneShot) | 持续性逻辑(加速/转向/闪烁) |

#### 与 HomingEnemyModifier 自身字段的关系(共存,方案 A)

`HomingEnemyModifier` 内部已有 `LockOnDelay` / `MaxHomingTime`,与基类新字段正交共存:

| 场景 | 用哪个字段 |
|---|---|
| 想让 modifier 整体"前 0.5 秒什么都不做" | 基类 `Delay = 0.5` |
| 想让 modifier "前 0.5 秒工作但不搜索目标" | `LockOnDelay = 0.5`(HomingEnemy 自己的字段) |
| 想让 modifier "2 秒后整体停用" | 基类 `Duration = 2` |
| 想让 modifier "2 秒后停止搜索,但仍按当前方向飞" | `MaxHomingTime = 2` |

两个 Delay / 两个 Duration 可叠加使用(基类外层 + HomingEnemy 内层),编译器 / 运行时都无冲突。

#### 典型用法示例

```csharp
// 1. 延迟激活追踪
HomingEnemyModifier h = new HomingEnemyModifier {
    Delay = 0.5f,   // 前 0.5s 直线飞(整体不工作)
    TurnRate = 360,
};

// 2. 燃料耗尽
HomingEnemyModifier h = new HomingEnemyModifier {
    Duration = 2f,  // 追踪 2 秒后整体停用(子弹保持最后方向直线)
};

// 3. OneShot:延迟 N 秒后按 FirePattern 再开火(走完整链路:FireExtensions + 子 modifier + SpawnFog 全生效)
FireOnEnterBulletModifier s = new FireOnEnterBulletModifier {
    Delay = 0.8f,        // 飞 0.8 秒后爆开
    Pattern = myPattern, // 拖一个 FirePattern 资产(Ring / Line / Arc / Composite / ...)
    InheritOwnerTeam = true, // 分裂弹继承母弹阵营(可撞人)
    // OneShot 默认 true,ModifyCore 留空
};

// 4. 自定义 OneShot modifier
[Serializable, SRName("Modifier/Play Sound Once")]
public class PlaySoundOnceModifier : BulletModifier {
    public SfxCue Sound;
    public PlaySoundOnceModifier() { OneShot = true; }
    protected override void OnWindowEnter(Bullet b) {
        AudioMix.PlaySfx(Sound, b.Position);
    }
    public override void ModifyCore(Bullet b, float dt) { }
}
```

#### 不变性 / 边界

- `Duration == 0` 和 `Duration < 0` 都视为**永久生效**(不退出窗口)
- `Time.timeScale = 0`(暂停菜单) → `Time.deltaTime = 0` → `_elapsed` 不增,modifier 不会"凭空结束"
- 子弹回池复用 → `BulletPool.Return` 调 `ClearModifiers` → 下次 `AttachModifiers` 调 `ResetAllModifierWindows` → OneShot 重新具备触发机会
- `[NonSerialized]` 字段 → 序列化进 .asset 文件时只存用户字段,`_elapsed` / `_isActive` 不存,符合预期

**为什么不在 BulletPool.Get 里直接传 prefab 数组?**
- 当前架构只走 `AttachModifiers` 单条挂载路径(在 pool 8 参重载里),保证 modifier 挂载的唯一入口;
- 如果未来需要"modifier 列表"或"modifier 池化",改 `AttachModifiers` 一处即可,FirePattern 子类不动。

**新增视觉 shader / visual modifier**(参考 `BulletTint.shader` + `BulletColorModifier`):
- 想加"染色 + 描边"、"染色 + 残影"、"染色 + 自发光"等组合效果,推荐**沿用 BulletColorModifier 的 MPB 套路**:
  1. 新建 shader `Assets/Shaders/<你的>.shader`,属性约定:
     - `[PerRendererData] _MainTex` —— SpriteRenderer 自动填
     - 自定义 `_XxxColor` 字段(RGB = 颜色,A = 强度),用 `[Range]` / `Color` 类型
     - 用 `Tags { "Queue"="Transparent" "RenderType"="Transparent" }` + `Blend One OneMinusSrcAlpha`(预乘 alpha,跟 Unity 内置 Sprites 一致)
     - `Fallback "Sprites/Default"` —— shader 编译失败时退回,不黑屏
  2. 新建 modifier 类 `Assets/Scripts/Bullet/<你的>Modifier.cs`,继承 `BulletModifier`,加 `[SRName("Modifier/<名字>")]`
  3. 在 modifier 里走 **`MaterialPropertyBlock`**(不是 `Renderer.color` / `material.instance`),因为:
     - `Renderer.color` 走内置 `tex * color` 公式,做不到"亮像素保持白"
     - `material.instance` 会破坏 batching(STG 高弹量场景下掉帧)
     - MPB 走 per-instance 覆盖,不破坏 batching
  4. modifier.ApplyPattern:
     ```csharp
     // 标准三件套:
     if (_mpb == null) _mpb = new MaterialPropertyBlock();
     b.Renderer.GetPropertyBlock(_mpb);
     _mpb.SetColor("_XxxColor", c);
     b.Renderer.SetPropertyBlock(_mpb);
     ```
  5. 新建材质 `.mat`,把 shader 指给它,挂到 prefab(同 §2.4.1 步骤 1-3)

**Shader 性能底线:**
- 1 个 Pass(不要用 multi-pass,STG 子弹数量大)
- 不用 `tex2Dlod` / 多重采样(SpriteRenderer 不会传 mip)
- 不用 `discard`(除非真的需要 alpha cutout,会破坏 early-Z)
- 顶点变换用 `UnityObjectToClipPos`(标准),不要写自己的矩阵

**不要做的事:**
- ❌ 在 modifier 里 `b.Renderer.material = ...`(会 instance 化,破坏 batching)
- ❌ 在 modifier 里 `b.Renderer.color = ...`(走乘法,无法"保留高光")
- ❌ 在 runtime 创建 .mat 资产(`new Material(...)` 内存泄漏,改用 `Shader.Find` + MPB)
- ❌ 给 visual modifier 加 `[RequireComponent]`(modifier 是纯 C#,不是 MonoBehaviour)

### 2.8 BulletModifier 信号触发机制(替代纯 Delay)

**职责:** 把 modifier「何时进入时间窗口」从**单一时间维度**扩展为**时间 + 信号两个维度**,让 BehaviorFlow 的 AI 节奏能直接驱动子弹 modifier 激活。详见 [`Assets/Scripts/Bullet/BulletSignalBus.cs`](../../Assets/Scripts/Bullet/BulletSignalBus.cs) + [`Assets/Scripts/Bullet/ModifierStartTrigger.cs`](../../Assets/Scripts/Bullet/ModifierStartTrigger.cs) + [`Assets/Scripts/Enemy/AI/Actions/EmitSignalAction.cs`](../../Assets/Scripts/Enemy/AI/Actions/EmitSignalAction.cs)。

#### 2.8.1 为什么需要

旧实现(`§2.6` 时间窗口):modifier 激活时机 = `Delay` 秒后,与敌人 AI 节奏完全脱节 —— Boss 喊话、阶段切换、Parallel 容器内某条 Action 完成 等场景下,策划只能「凑 Delay 时间」做对位,既不准又难维护。

新实现:modifier 激活时机 = `ModifierStartTrigger.ShouldActivate(...)`,抽象成 SR 多态字段,可下拉选:
- `Trigger/Delay`(默认,与历史 100% 等价,老 .asset 兜底)
- `Trigger/On Signal`(订阅 `BulletSignalBus`,收到即激活)
- `Trigger/Delay Or Signal`(Delay 与信号二选一)

#### 2.8.2 三件套关系图

```
                    ┌──────────────────────────┐
                    │ BehaviorFlow.Asset       │
                    │   Actions:               │
                    │   [Fire, Move, …,        │
                    │    EmitSignal("fire!")]  │ ◄── 新增 Action
                    └──────────┬───────────────┘
                               │ OnEnter/OnTick
                               ▼
                    ┌──────────────────────────┐
                    │  BulletSignalBus.Emit()  │ ◄── 静态门面
                    │  "fire!", origin         │
                    └──────────┬───────────────┘
                               │  派发给所有订阅者(全局广播)
                               ▼
                ┌────────────────────────────────┐
                │ Bullet (active)                │
                │  _modifiers:                   │
                │   [Modifier A:StartTrigger=Delay]      ◄── 老路径,行为不变
                │   [Modifier B:StartTrigger=OnSignal]  ◄── 新路径
                └────────────────────────────────┘
```

#### 2.8.3 `BulletSignalBus`(静态门面)

位置:`Assets/Scripts/Bullet/BulletSignalBus.cs`,namespace `ShinySTG.BulletCore`。

```csharp
public static class BulletSignalBus
{
    public static void Subscribe(string signalName, Action<Vector2> handler);
    public static void Unsubscribe(string signalName, Action<Vector2> handler);
    public static void Emit(string signalName, Vector2 origin);
    public static void ClearAll();  // PlayMode 切换时自动调
}
```

**关键设计**(与项目其他基础设施对齐):
- **静态门面**:与 `AudioMix.cs` 同套路,无需 MonoBehaviour 单例,场景里没挂任何东西也能用
- **字典派发**:`Dictionary<string, List<Action<Vector2>>>`,O(1) Emit,O(订阅者数) 派发
- **全局广播**:Emit 不区分发射源 / 阵营(典型 STG 「Boss 喊话全场响应」语义);隔离需求未来可通过 `string SourceTag` 扩展,不破坏现有 API
- **无名 / null 静默**:信号名为 null / 空 / 全空白时直接 return,不会因为策划拼错信号名而崩溃(对照 BoundsService 无单例时的硬编码 fallback 风格)
- **无引用泄漏**:modifier 退订统一走 `Unsubscribe`,由 `Bullet.DetachSignalTriggers` 在 BulletPool.Return 与 `Bullet.OnDestroy` 兜底触发
- **场景切换清空**:`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` 在启动清空字典,避免 PlayMode 重启后残留

**为什么不用 Unity 的 Timeline SignalReceiver**:那套走反射 + 序列化资产枚举(必须在 timeline 资产里登记信号),对 STG 「策划自由命名、运行时即时订阅」太重。本类用 string 名 + 静态字典,零反射、零资产登记。

#### 2.8.4 `ModifierStartTrigger`(SR 多态字段)

位置:`Assets/Scripts/Bullet/ModifierStartTrigger.cs`,与 `BaseOffsetStrategy` / `FirePatternBulletExtra` 同套路。

```csharp
[SerializeReference, SR]
public ModifierStartTrigger StartTrigger;  // BulletModifier 上的新字段
```

**三个内置子类**:

| SRName | 用途 | 关键字段 |
|---|---|---|
| `Trigger/Delay`(默认) | `elapsed >= Delay` 激活,与历史 100% 等价 | `Delay` |
| `Trigger/On Signal` | 订阅具名信号,收到即激活 | `SignalName` / `MaxWait`(兜底)/ `RequireInRange` + `MaxDistanceFromOrigin` |
| `Trigger/Delay Or Signal` | Delay 与信号任一先到即激活 | `Delay` / `SignalName` |

**生命周期契约**:
1. `OnAttach(Bullet)`:modifier 挂到子弹时调一次(`BulletPool.AttachModifiers` → `bullet.AttachSignalTriggers()`),订阅型 trigger 在这里调 `BulletSignalBus.Subscribe`
2. `ShouldActivate(Bullet, elapsed)`:每帧由 `BulletModifier.Modify` 调一次,返回 true 表示进入窗口
3. `OnDetach(Bullet)`:子弹回收或销毁时调一次,订阅型 trigger 在这里调 `BulletSignalBus.Unsubscribe` —— **不调 = 内存泄漏**

#### 2.8.5 `EmitSignalAction`(BehaviorFlow Action)

位置:`Assets/Scripts/Enemy/AI/Actions/EmitSignalAction.cs`,与 `FireAction` / `MoveAction` 同套路(SRName 下拉)。

```csharp
[Serializable, SRName("Action/Emit Signal")]
public class EmitSignalAction : EnemyAction
{
    public string SignalName;
    public EmitMode Mode;        // OnEnterOnly / EveryTick / OnInterval
    public float EmitInterval;
    public Vector2 OriginLocalOffset;
    public bool DebugLog;
}
```

**典型用法**:
- Boss 蓄力 Action 的 OnEnter 触发 `"boss_charge_finished"` → 子弹分裂 modifier 立即开火(替代「凑 Delay 时间」对位)
- 残血阶段切换时:`EmitSignalAction.Emit("boss_enrage")` → 全场追踪弹切直线 + 染色红
- 与 `FireAction` 协作:`Parallel` 容器内同帧 `Fire + EmitSignal`,所有「那批新生成的弹」立刻激活
- `EveryTick` 模式:持剑敌人挥剑动作期间每帧触发 `"swinging"` → 弹幕随剑势摆动(慎用,信号风暴)

#### 2.8.6 集成点(谁在哪儿调谁)

| 调用点 | 文件 | 行为 |
|---|---|---|
| 子弹从池取出 | `BulletPool.AttachModifiers` 末尾 | `bullet.AttachSignalTriggers()` → 触发 `StartTrigger.OnAttach` → 订阅型 trigger 订阅 BulletSignalBus |
| 子弹回池 | `BulletPool.Return` 开头(在 `ClearModifiers` **之前**) | `bullet.DetachSignalTriggers()` → 触发 `StartTrigger.OnDetach` → 订阅型 trigger 退订 |
| 子弹被 Destroy | `Bullet.OnDestroy` | 兜底调 `DetachSignalTriggers`,防走 BulletPool.Return 之外的销毁路径漏退订 |
| 每帧 modify | `BulletModifier.Modify` 改造后 | 调 `StartTrigger?.ShouldActivate(this, _elapsed)` 判断进入窗口(同时保留原 `Delay` 字段向后兼容路径) |
| 子弹 ResetWindow | `BulletModifier.ResetWindow` | `StartTrigger == null` → 兜底 `new DelayStartTrigger { Delay = this.Delay }`,保证老 .asset 行为 100% 等价 |
| BulletPool Get 收尾 | 同上 | ★ **vX 起已不再做同步**。基类 `Delay` 字段仅在「StartTrigger=null 兜底分支」生效(把值拷过去给新建的 DelayStartTrigger)。trigger 非 null 时 trigger 内部 Delay 自己说了算,不会被基类 Delay 静默覆盖。 |
| BulletPool Return 顺序 | 同上 | **先 Detach 再 Clear**,Detach 需要遍历 `_modifiers`,Clear 后列表空就遍历不到 |

#### 2.8.7 向后兼容

| 存量资产 | 行为 |
|---|---|
| 老 `.asset`(`StartTrigger == null`)|`ResetWindow()` 兜底 → `DelayStartTrigger { Delay = 原 Delay 值 }` → 100% 等价 |
| 老代码 `bullet.Delay = X` 直接赋值 | 字段保留;**vX 起 `ResetWindow` 仅在 StartTrigger=null 时才把基类 Delay 拷给新建的 DelayStartTrigger**。trigger 非 null 时 trigger 内部 Delay 字段独立,基类 Delay 不再覆盖它 |
| 不订阅 `BulletSignalBus` 的场景 | `Emit` 空分派,无副作用 |

★ **破坏性变更警告**(vX 起):
  旧实现 ResetWindow 会把基类 Delay 强制同步进 trigger 内部 Delay 字段。这在以下场景下会导致策划配置被静默篡改:
  ```csharp
  // Inspector 配置: DelayOrSignalStartTrigger.Delay = 2.0(基类 Delay 默认 0)
  // 旧行为:ResetWindow 后 trigger.Delay 被改成 0 → 立即激活
  // 新行为:trigger.Delay 保持 2.0,基类 Delay 仅在 StartTrigger=null 兜底分支生效
  ```
  修复方式:把 trigger 内部 Delay 字段视为权威,策划配 trigger.Delay 时直接配 trigger,不依赖基类 Delay。

#### 2.8.8 扩展指南

- **新 trigger 类型**(例如「子弹收到 N 次碰撞后激活」):新建 `ModifierStartTrigger` 子类 + 加 `[SRName("Trigger/<名字>")]`,在 `OnAttach` / `OnDetach` / `ShouldActivate` 里实现自定义激活条件
- **新发射时机**(例如「同伴死亡时激活」):建新 `EnemyAction` 子类调 `BulletSignalBus.Emit` 即可,无需改 BulletModifier
- **同源过滤**(例如「只激活由本 Boss 发射的弹」):后续可给 `BulletSignalBus.Emit` 加可选 `int SourceInstanceID` 参数 + `OnSignalStartTrigger` 加 `int RequiredSourceId` 字段(默认 -1 = 任意源),向下兼容老调用方
- **完全本地**(场景级隔离):用 `Dictionary<Collider, List<Action>>` 替换全局字典;但这会牺牲「Boss 喊话全场响应」语义,按需使用

---


## 与其他板块的关系

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [hitbox](./arch-hitbox.md) — 复用 AABB + 阵营;每个 Bullet 自动挂 HitboxComponent
- [fire-pattern](./arch-fire-pattern.md) — Bullet 由 FirePattern.SpawnBullet 创建,modifier 由 FirePattern.ModifierPrefabs 挂载;**BulletPool.FireGroup 入口维护 per-FireExtension 字典 `_fireCounts`**(供 `AccumulatingOffsetAngleFireExtension` 等批次累加型 FireExtension 读取本批开火序号),Ring/Line/Arc 三个 FirePattern 子类的 `Fire()` 入口从 `pool.GetFireExtensionFireCounts()` 取字典传给 Resolver(详见 [fire-pattern §3.1.1](./arch-fire-pattern.md#311-批次累加型accumulatingoffsetanglefireextension))
- [bounds](./arch-bounds.md) — 出界回收 / 反弹判定都基于 BoundsService.CullingArea
- [enemy-ai](./arch-enemy-ai.md) — BulletSignalBus 接收 EmitSignalAction 的信号,驱动 BulletModifier 激活
- [audio](./arch-audio.md) — Hit 音效 / 染色 modifier 与 SfxCue 体系可联动(按需)
