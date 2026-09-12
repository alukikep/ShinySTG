# ShinySTG 关卡编辑器使用说明

> 本文档是 [README §7](./README.md#7-关卡关卡编辑器) 的独立展开。
> 关卡的**完整设计 / 扩展点 / 与既有层对齐说明**见 [`ARCHITECTURE.md`](./ARCHITECTURE.md) §9。
> 本文档面向"使用关卡编辑器配关卡的人",讲操作流程,不重复架构决策。

---

## 目录

1. [快速上手 30 秒](#快速上手-30-秒)
2. [创建关卡资产](#创建关卡资产)
3. [在场景里跑关卡](#在场景里跑关卡)
4. [编辑关卡条目](#编辑关卡条目)
5. [可订阅的关卡事件](#可订阅的关卡事件)
6. [加新条目类型(扩展指南)](#加新条目类型扩展指南)
7. [持续型条目(Duration)与时间轴堆叠](#持续型条目duration与时间轴堆叠)

---

## 快速上手 30 秒

```
1.  Project 窗口右键 → Create → STG → Level  →  命名 Stage1.asset
2.  点开 Stage1.asset 的 Entries 数组,+ 几条:
       TriggerTime=0.0   Simple   SpawnPosition=(0, 4)    EnemyPrefab=教学小怪
       TriggerTime=2.0   Wave     CenterPosition=(0, 4)  SpacingX=0.8  Prefabs=[小怪,小怪,小怪]
3.  场景里建 GameObject,挂 LevelController,把 Stage1.asset 拖到 Definition
4.  Play → 关卡自动开跑
```

---

## 创建关卡资产

1. Project 窗口右键 → **Create → STG → Level**
2. 输入关卡名(如 `Stage1.asset`),生成一份 `LevelDefinition` 资产
3. 选中资产,在 Inspector 里编辑即可

资产会被自动写入 Project 视图;`*.meta` 文件自动生成,记得进库。

---

## 在场景里跑关卡

1. 场景里新建一个 GameObject(命名 `LevelController`)
2. Add Component → `LevelController`(自动用项目自带 `Singleton<T>` 基类,无需手动设单例)
3. 把刚才的 `.asset` 拖到 `Definition` 字段
4. 进 Play Mode → 关卡自动按 `AutoStart=true` 开始(若不需要自动开始,把 `AutoStart` 勾掉,运行时调 `LevelController.Instance.BeginLevel()`)

> 关卡运行时需要在场景里有 `BulletPool`(普通关卡共用场景默认池即可;Boss 关卡配专属弹 prefab 时,把它拖到关卡资产的 `Pool` 字段)。
> 若没在 `Pool` 字段配且场景里有 `BulletPool`,`LevelController` 会在 `BeginLevel` 时自动 `FindObjectOfType` 兜底(`AutoFindBulletPool` 默认开)。

### 关卡结束方式

- **自然结束**:`LevelDefinition.Duration > 0` 时,时间到自动调 `CompleteLevel()`,触发 `OnLevelComplete`。
- **强制结束**:外部脚本调 `LevelController.Instance.CompleteLevel()`(玩家死亡 / 退出按钮 / 调试热键)。

---

## 编辑关卡条目

点开 `.asset` 的 `Entries` 数组,点 `+` 号新增条目。每个条目**通过下拉菜单选类型**(由项目自带 `SREditor` 插件提供,继承 `SpawnEntry` 的子类会自动出现在下拉里)。

### 内置 5 种条目

| 类型 | 下拉路径 | 用途 |
|---|---|---|
| **Simple** | `Entry/Simple` | 单点生成 — 配 `TriggerTime` / `SpawnPosition` / `EnemyPrefab`,可选 `OverrideFlow` 临时换行为流 |
| **Wave** | `Entry/Wave` | 横排生成 — 配 `Prefabs[]` 数组 + `CenterPosition` + `SpacingX`,自动沿 X 轴等距铺 |
| **Boss** | `Entry/Boss` | Boss 出场 — 配 `BossPrefab` + `SpawnPosition`(当前为留壳,后续接入多阶段) |
| **Sustain** | `Entry/Sustain` | 在点位持续刷敌 — 配 `Duration` + `SpawnInterval`,期间每 N 秒生成一次(详见 §7) |
| **Play SFX** | `Entry/Play SFX` | 时间点音效 — 配 `Cue` + 可选 `Position`,在 `TriggerTime` 播 SFX(详见[音频集成](#音频集成自动切歌--时间点-sfx)) |

### 典型配法示例

```
时间 = 0.0  → Simple   TriggerTime=0.0, SpawnPosition=(0, 4),   EnemyPrefab=教学小怪
时间 = 2.0  → Wave     TriggerTime=2.0, CenterPosition=(0, 4), SpacingX=0.8,
                     Prefabs=[红小怪, 蓝小怪, 红小怪, 蓝小怪, 红小怪]   ← 5 只横排交错
时间 = 5.0  → Simple   TriggerTime=5.0, SpawnPosition=(-3, 3), EnemyPrefab=精英怪, OverrideFlow=精英行为.flow
时间 = 8.0  → Simple   TriggerTime=8.0, SpawnPosition=( 3, 3), EnemyPrefab=精英怪, OverrideFlow=精英行为.flow
时间 = 12.0 → Boss     TriggerTime=12.0, SpawnPosition=(0, 5), BossPrefab=一阶段boss
时间 = 60.0 → Simple   TriggerTime=15.0, SpawnPosition=(0, -4), EnemyPrefab=自毁冲锋怪, OverrideFlow=向下冲.flow
```

> **同 prefab 配不同行为流的小技巧**:`SimpleSpawnEntry` 的 `OverrideFlow` 字段会临时覆盖 prefab 自带的 Flow —— 适合"同一个敌人 prefab,在不同时间点以不同行为出场"。

### 增 / 删 / 复制 / 快捷键

> 这些操作都走 `LevelEditorCommands`,Toolbar / 时间轴 / 列表 / 快捷键共用一份逻辑,支持 **Ctrl+Z 撤销**。

| 操作 | 入口 |
|---|---|
| 加 | Toolbar `+ Add ▾` 下拉(自动列出所有 `SpawnEntry` 子类) |
| 删 | Toolbar `Delete` / 时间轴右键 → Delete / 列表右键 → Delete / **选中后按 Delete 或 Backspace** |
| 复制 | Toolbar `Duplicate` / 时间轴右键 → Duplicate / 列表右键 → Duplicate(瞬时点偏移 +0.5s,持续型偏移 +Duration) |
| 跳到 Scene | 时间轴右键 → Focus in Scene / 列表右键 → Focus in Scene(`SceneView.LookAt`) |

> 快捷键(Delete / Backspace)只在焦点不在输入框(`GUIUtility.keyboardControl == 0`)时生效,避免误删正在编辑的字段值。

### Simple vs Wave 怎么选

- **只有 1 只**:`Simple`
- **2 只以上,大致沿 X 一行 / 一列**:`Wave`(中心点 + 间距一行搞定)
- **2 只以上,每只位置都要单独定(弧线/斜线/不规则)**:`Simple` 写 N 条
- **2 只以上,需要"每只不同 prefab + 不同位置 + 不同行为流"**:`Simple` 写 N 条(`Wave` 整波共用 InitialRotation,不支持 Flow 覆盖)

---

## 可订阅的关卡事件

`LevelController` 暴露以下实例事件,UI / 计分 / 动画 / 章节选择等系统可订阅:

```csharp
LevelController.Instance.OnLevelStart    += def => { /* 关卡开始 */ };
LevelController.Instance.OnLevelComplete += def => { /* Duration 到 / 外部调 CompleteLevel */ };
LevelController.Instance.OnEnemySpawned  += go  => { /* 每只敌人生成时 */ };
LevelController.Instance.OnBossSpawned   += go  => { /* boss 生成时 */ };
LevelController.Instance.OnBossDefeated  += go  => { /* boss 击败时(留口) */ };
```

风格与 `PlayerHealth` 的实例事件一致(`OnLifeLost` / `OnRevive` / `OnAllLivesLost` 同款)。

---

## 音频集成(自动切歌 + 时间点 SFX)

关卡编辑器提供两种把音频接进关卡的方式:**关卡级自动切歌**(`LevelAudioBinding` 配法)和**时间轴 SFX 条目**(`Entry/Play SFX`)。两种完全正交,可混用。

### 关卡级自动切歌(`LevelAudioBinding`)

每个关卡可以挂一个 `LevelAudioBinding` 资产,`LevelController.BeginLevel` 时 AudioEventHub 会自动启用切歌:

| 事件 | 切到 |
|---|---|
| `OnLevelStart` | `AudioBinding.Playlist` |
| `OnBossSpawned` | `AudioBinding.BossMusic`(交叉淡化 `ToBossCrossfade` 秒) |
| `OnBossDefeated` | `AudioBinding.DefeatMusic`(交叉淡化 `ToDefeatCrossfade` 秒) |

**两种用法**:
1. **关卡资产一站式**(推荐):在 `LevelDefinition.AudioBinding` 字段挂 binding 资产,关卡自带决定切什么 BGM
2. **全局模板**(向后兼容):多个关卡共用同一套 binding 时,在 `AudioSystem.LevelBindings[]` 集中配,关卡 AudioBinding 留空

**三步配法**:
1. 打开关卡编辑器 → 选中关卡资产
2. 工具栏点 `+ Create AudioBinding`(自动创建同名 `_AudioBinding.asset` 并双向反引用)
3. 选中新生成的 binding 资产,在 Inspector 里拖 BGM 资产到 `Playlist` / `BossMusic` / `DefeatMusic` 字段

之后点 `♪ Open AudioBinding` 可以一键回到 binding 配置。

#### 关卡级开关

`LevelDefinition.AutoSwitchBgm`(默认 `true`):
- `true` + AudioBinding 非空 → 自动切歌启用
- `false` → 此关卡不参与自动切歌(BGM 由调用方手动控制,适合过场关 / 静音关)

### 时间点 SFX 条目(`Entry/Play SFX`)

按时间轴触发一次性 SFX(UI 警告、阶段切换音、剧情音效等)。`+ Add ▾` → `Entry/Play SFX` 添加。

| 字段 | 用途 |
|---|---|
| `TriggerTime` | 触发时间(秒) |
| `Cue` | SfxCue 资产(必填;空 = 跳过) |
| `UsePosition` | 勾上 = 在 `Position` 世界坐标发声;不勾 = 2D 监听(跟随 Listener) |
| `VolumeMul` / `Pitch` | 临时覆盖,与 SfxCue 默认值叠加乘 |

Editor Preview 期间也会播(走 `LevelEditorPlayer.TriggerOne` → `OnTrigger` 路径);无 AudioSystem 时静默返回。

### 时序示例

```
TriggerTime=0.0   AudioBinding.Playlist = StageTheme          ← 关卡开始
TriggerTime=2.0   Simple   prefab=小怪                            ← 小怪入场
TriggerTime=15.0  Play SFX Cue=WarnSound                       ← 警告音
TriggerTime=30.0  Boss   BossPrefab=Boss1                        ← AudioBinding.BossMusic 自动切
TriggerTime=60.0  BossDefeated → AudioBinding.DefeatMusic 自动切
```

详见 [`Assets/Scripts/Audio/README.md`](./Assets/Scripts/Audio/README.md) 的「BGM 切换」章节。

---

## 加新条目类型(扩展指南)

按项目惯例,加新关卡条目类型 = 在 `Assets/Scripts/Level/SpawnEntries/` 下新建子类:

```csharp
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries
{
    [Serializable, SRName("Entry/Conditional")]   // 下拉菜单里会出现 "Entry/Conditional"
    public class ConditionalSpawnEntry : SpawnEntry
    {
        // 自己加字段,例如"等玩家到达某 X 才触发"
        public float PlayerXThreshold;

        public override bool ShouldTrigger(bool alreadyFired)
        {
            if (!base.ShouldTrigger(alreadyFired)) return false;
            return Player.Instance != null && Player.Instance.transform.position.x >= PlayerXThreshold;
        }

        public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
        {
            // 生成逻辑
        }
    }
}
```

新条目会自动出现在 `Entries` 数组的下拉里,无需改 `LevelController`。

### 三个常用钩子

- **`ShouldTrigger(alreadyFired)`** — 每帧调用,问"现在该不该触发"。默认实现处理 OneShot。
- **`OnTrigger(runtime, def)`** — 触发时的实际操作(生成 prefab / 启动 boss / 调关事件)。
- **可选 override** `TriggerTime`(继承自基类)或加自己的字段(比如示例里的 `PlayerXThreshold`)。

### 常见条目类型速查

| 想做的事 | 加什么子类 |
|---|---|
| 玩家到达某 X 才生成 | `ConditionalSpawnEntry`,override `ShouldTrigger` |
| 周期性每 N 秒生成一波 | `RepeatSpawnEntry`,基类 `OneShot = false` 即可 |
| 等一波清完再出下一波 | `OnClearedEntry`,override `ShouldTrigger` 读 `runtime.ActiveUnits` |
| 概率触发 | `ChanceSpawnEntry`,override `ShouldTrigger` 里 `Random.value < Chance` |
| V 字 / 弧形 / 螺旋阵 | `CurvedWaveSpawnEntry : WaveSpawnEntry`,override `OnTrigger` |
| 在点位持续刷敌(已实现) | `SustainSpawnEntry`(`[SRName("Entry/Sustain")]`),配 Duration + SpawnInterval |

---

## 持续型条目(Duration)与时间轴堆叠

> 关卡里"短时间内多个 entry 同时作用"很容易互相遮挡 —— 时间轴**自动把重叠的 entry 堆到不同 lane**,并且任何 `SpawnEntry` 都可设 `Duration` 让它"占据一段时间"而不是"一个时间点"。

### 字段位置

`Duration` 是 `SpawnEntry` 基类字段,**所有 entry 都能用**:

- `Duration <= 0`:瞬时点(原有行为,block 宽度固定 140px)
- `Duration > 0`:持续型,block 宽度 = `Duration × 像素/秒`

### 时间轴交互

| 操作 | 效果 |
|---|---|
| 拖动 block **中部** | 改 `TriggerTime`(瞬时点 / 持续型都一样) |
| 拖动 block **右边缘 6px** | 改 `Duration`(只持续型生效;瞬时点无边缘可拖) |
| Hover 右边缘 | 鼠标变 ↔ 形状,提示可拖 |
| 右侧详情面板 | `TriggerTime` / `Duration` / 其他字段都在那里,改完时间轴实时更新 |

### 视觉

- **瞬时点 block**:`Time` 起点,固定 140px 宽,标签 = "类型 @ 时间"
- **持续型 block**:左 8px 实色(标识起点)+ 主体半透明同色 + 右边缘 2px 暗线(暗示可拖)
- **重叠堆叠**:多个 entry 时间区间重叠时,自动分配到不同 lane(纵向上下排开),不遮挡

### `Sustain` 条目(`Entry/Sustain`)

最常见的持续型用法:**在指定点位周期性刷敌**。

| 字段 | 说明 |
|---|---|
| `TriggerTime` | 开始时间(秒) |
| `Duration` | 持续时间(秒)。例如 3.0 表示从 t 持续到 t+3 |
| `SpawnInterval` | 两次生成之间的间隔(秒)。<= 0 = 只生成一次(退化成 Simple) |
| `EnemyPrefab` / `SpawnPosition` / `InitialRotation` / `OverrideFlow` | 同 Simple |

**典型配法**(关卡编辑器中的"+ Add ▾" → Entry/Sustain):
```
时间 = 5.0   Duration = 3.0   SpawnInterval = 0.5
SpawnPosition = (0, 4)   EnemyPrefab = 刷怪A

→ 从 5.0s 到 8.0s,每 0.5s 生成一只刷怪A,共 7 只。
```

### 自定义持续型条目

任何 `SpawnEntry` 子类都能"持续"——override `OnTick(runtime, t, dt)` 即可,`t` 是从触发起已过时间(`0..Duration`):

```csharp
[Serializable, SRName("Entry/CountDown")]
public class CountDownSpawnEntry : SpawnEntry
{
    public Vector2 SpawnPosition;

    public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
    {
        // 给自己画个倒计时提示之类的初始动作
        runtime.RegisterSustained(this);
    }

    public override void OnTick(LevelRuntime runtime, float t, float dt)
    {
        if (t >= 0f && t < dt) /* 第一帧:开始动画 */
        if (t >= Duration - dt) /* 最后一帧:结束动画 */
    }
}
```

`RegisterSustained` 必须在 `OnTrigger` 第一行调一次(防止漏调,基类 `LevelRuntime.Tick` 也会兜底自动注册)。

### 时间轴堆叠 lane 的工作机制

```
entry[0] t=1.0 dur=0   lane 0  ← 顶层
entry[1] t=2.0 dur=0   lane 0  ← 同 lane 不重叠
entry[2] t=3.0 dur=2.0 lane 0  ← 占 [3..5]
entry[3] t=4.0 dur=1.0 lane 1  ← 与 entry[2] 重叠 → 堆到 lane 1
entry[4] t=5.5 dur=0   lane 0  ← entry[2] 已结束,回到 lane 0
```

贪心分配:每个 entry 找"最早可容纳它的 lane",找不到就新开 lane。**所有 entry 自动堆叠,不需要手动画轨道**。
