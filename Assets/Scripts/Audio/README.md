# Audio System — 配置说明

> 音频音乐系统的 **Inspector 配置步骤 + 资产使用指南**。
>
> 架构与扩展点 (`SfxRule` 多态 / Pipeline 模型 / 与既有层关系) 见 [`ARCHITECTURE.md`](../../../ARCHITECTURE.md) §11。
> 扩展者(加新 SFX 规则)看本文件「扩展点」一节;使用者(配关卡音效用)看「快速配置」「SfxCue 配置」「BGM 配置」三节。

---

## 目录

1. [快速配置(5 分钟上手)](#1-快速配置5-分钟上手)
2. [SfxCue 配置(单音效的完整 Inspector 字段)](#2-sfxcue-配置单音效的完整-inspector-字段)
3. [SfxRule 多态 Pipeline 配置(随机抽 clip / pitch 抖动 / cooldown)](#3-sfxrule-多态-pipeline-配置随机抽-clip--pitch-抖动--cooldown)
4. [BGM 配置(BgmTrack / BgmPlaylist / 交叉淡化)](#4-bgm-配置bgmtrack--bgmplaylist--交叉淡化)
5. [AudioBus 配置(总线音量 / PlayerPrefs 持久化)](#5-audiobus-配置总线音量--playerprefs-持久化)
6. [宿主挂点:把 SfxCue 拖到 PlayerHealth / BossHealth / EnemyHealth / PlayerShooting](#6-宿主挂点把-sfxcue-拖到-playerhealth--bosshealth--enemyhealth--playershooting)
7. [代码调用(场景脚本切 BGM / 暂停音乐 / 设音量)](#7-代码调用场景脚本切-bgm--暂停音乐--设音量)
8. [扩展点:加新 SfxRule(随机抽 clip / pitch 抖动 / cooldown)](#8-扩展点加新-sfxrule)
9. [常见问题](#9-常见问题)

---

## 1. 快速配置(5 分钟上手)

### 1.1 场景里挂总控

1. 在场景 Hierarchy 里 `Create Empty`,命名 `Audio`(或任意)。
2. `Add Component → AudioSystem`。
   - 该组件继承 `PersistentSingleton`,会自动 `DontDestroyOnLoad`,跨场景保留。
   - Inspector 里有 4 个 AudioBus 字段(MasterBus / BgmBus / SfxBus / UIBus)与若干行为开关,**全部留空也能跑**(代码会自动创建临时 fallback)。
3. Play Mode 启动后 Hierarchy 会出现自动创建的子物体:
   - `SfxPlayer_0 / SfxPlayer_1 / ...`(SFX 池,首次播放时按需增长)
   - `MusicA / MusicB`(BGM 双通道,做交叉淡化用)

### 1.2 创建 SFX 资产

```
Project 窗口右键 → Create → STG → Audio → SFX Cue
```

命名建议按"使用场景"而非"声音内容",如 `PlayerHit.asset` / `EnemyDeath.asset` / `BossBarDeplete.asset`。

把 AudioClip 拖到 Inspector 的 `Clips` 数组(可多个,搭配 `RandomPickRule` 随机抽)。

### 1.3 把 SfxCue 拖到宿主

| 宿主脚本 | 字段 | 触发时机 |
|---|---|---|
| `PlayerHealth.cs` | `_hitSfx / _deathSfx / _grazeSfx / _powerUpSfx` | 受伤 / 全死 / 擦弹 / 火力提升 |
| `BossHealth.cs` | `_hitSfx / _barDepletedSfx / _deathSfx` | 受击 / 某管打空 / 全死 |
| `EnemyHealth.cs` | `_hitSfx / _deathSfx` | 受击 / 死亡 |
| `PlayerShooting.cs` | `_shootSfx` | 每次开火 |

留空字段 = 不播放。

### 1.4 切 BGM(可选)

在场景脚本(如 Boss 总控 / 关卡控制器)里:

```csharp
using ShinySTG.Audio;
AudioMix.PlayTrack(bossThemeBgm);                // 切单首
AudioMix.PlayPlaylist(stage1Playlist);            // 列表(顺序/随机/循环由资产决定)
```

详见 [§7](#7-代码调用场景脚本切-bgm--暂停音乐--设音量)。

---

## 2. SfxCue 配置(单音效的完整 Inspector 字段)

选中一个 `SfxCue.asset`,Inspector 显示以下分组:

### 2.1 Clips

| 字段 | 类型 | 说明 |
|---|---|---|
| `Clips` | `AudioClip[]` | 候选 clip 数组。**单 clip 也合法**(配合 `Rule/Random Pick` 才有意义)。**数组为空时该 cue 不播放**(但不报错),方便临时禁用某个音效 |

### 2.2 Playback

| 字段 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `DefaultVolume` | `[0..1]` | `1` | 默认音量倍率。**调用方可用 `volumeMul` 临时覆盖**(叠加乘,例如 `PlaySfx(cue, volumeMul: 0.5f)` 把音量砍半) |
| `DefaultPitch` | `float` | `1` | 默认 pitch(`1=原始`,`2=升八度`,`0.5=降八度`)。调用方可用 `pitch` 临时覆盖 |
| `Loop` | `bool` | `false` | 是否循环。**用于「机枪持续声」「引擎轰鸣」**。循环音调用方需自己调 `AudioMix.StopSfx(cue)` 停止(目前 `StopSfx` 会停该 cue 所有 voice) |

### 2.3 Routing

| 字段 | 类型 | 说明 |
|---|---|---|
| `Bus` | `AudioBus` | 路由到哪个总线。**留空 = 走 Master**。详见 [§5](#5-audiobus-配置总线音量--playerprefs-持久化) |
| `Priority` | `[0..255]` | 路由优先级。**池满时低优先级先丢**。建议:UI=200,玩家射击=160,敌人爆炸=140,环境音=80 |

### 2.4 Throttling(限流)

| 字段 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `MaxVoices` | `int` | `4` | **同 cue 同时最多 voice 数**。`0` = 不限。超出时丢最老的 voice(保持最新一次播放)。STG 高弹量场景必备 —— 子弹命中音 1 秒可能触发几十次,默认 4 防止「哒哒哒哒」一片 |
| `Cooldown` | `float`(秒) | `0` | **同 cue 全局最小播放间隔**。`0` = 无冷却。适合「密集小事件」全局节流 |

> **节流策略推荐**:密集小事件(子弹命中、玩家擦弹)用 `Cooldown≈0.02~0.05s`;少量但需要叠播(连击、爆炸)用 `MaxVoices=2~4`。

### 2.5 Rules(多态 Pipeline)

`Rules` 字段是 `SfxRule[]`,点 `+` 后下拉选内置规则,详见 [§3](#3-sfxrule-多态-pipeline-配置随机抽-clip--pitch-抖动--cooldown)。

---

## 3. SfxRule 多态 Pipeline 配置

### 3.1 Pipeline 模型

SfxCue.Rules 是一个 `SfxRule[]` 数组,数组里每个模块按顺序串行处理 SFX 请求(已选定 clip → 改字段 → 交给 SfxRouter 播放)。

**数组顺序就是执行顺序,改顺序 = 改语义。**

例:
- `[RandomPick, PitchVariation]` → 先抽 clip → 再在抽到的 clip 上叠 pitch 抖动
- `[PitchVariation, RandomPick]` → pitch 抖动(所有 clip 共用)→ 再抽 clip

### 3.2 内置规则

#### Rule/Random Pick

| 字段 | 默认 | 说明 |
|---|---|---|
| `AvoidRepeatLast` | `true` | 避开上一次播放的 clip,避免连抽同一个。Clips 长度 ≤ 1 时无效 |

**用法**:让「玩家擦弹」「敌人爆炸」这种"高频但听感要变"的事件有变化。

#### Rule/Pitch Variation

| 字段 | 默认 | 说明 |
|---|---|---|
| `Range` | `0.05` | 抖动幅度。`0.1` = `[0.9, 1.1]` 范围随机。`0` = 不变 |

**用法**:让「子弹命中」「UI 点击」这种短促音有微妙变化,听感更自然。

#### Rule/Cooldown

| 字段 | 默认 | 说明 |
|---|---|---|
| `MinInterval` | `0.05` | 最小播放间隔(秒)。`0.1` = 100ms 内同 cue 最多播一次 |

**用法**:比 `SfxCue.Cooldown` 更精细的节流 —— `SfxCue.Cooldown` 字段是全局最小间隔,本规则可以叠在 Pipeline 任何位置(例如放在 `RandomPick` 之后做"按节奏分组的同拍只播一次")。

### 3.3 Pipeline 执行流程

```
调用:AudioMix.PlaySfx(cue, position, parent, volumeMul, pitch)
  ↓
SfxRouter.Play():
  1. 检查 cue.IsEmpty → return
  2. 检查 cue.Cooldown 全局冷却 → return
  3. 检查 cue.MaxVoices → 超出则丢最老的 voice
  4. 构造 SfxRequest { Clip = cue.Clips[0], Volume = cue.DefaultVolume × volumeMul, ... }
  5. for rule in cue.Rules: req = rule.Process(req)  ← Pipeline
  6. 从 SfxPlayer 池取一个 → Play(req)
```

---

## 4. BGM 配置

### 4.1 BgmTrack(单首 BGM)

```
Project 窗口右键 → Create → STG → Audio → BGM Track
```

| 字段 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `Clip` | `AudioClip` | (必填) | BGM 音频。一般 ogg/mp3,Inspector 里建议选 `Load Type: Streaming` |
| `DefaultVolume` | `[0..1]` | `1` | 默认音量倍率 |
| `Loop` | `bool` | `true` | 是否循环 |
| `Bus` | `AudioBus` | (空) | 路由到哪个总线。**留空 = 走 BgmBus**(由 `BusMixer` 决定) |
| `StartTime` | `float`(秒) | `0` | 从指定时间点开始播放。**用于「跳过 intro 直接进 loop 段」** |
| `DisplayName` | `string` | (空) | 友好名称,Inspector 显示用 |

### 4.2 BgmPlaylist(BGM 列表)

```
Project 窗口右键 → Create → STG → Audio → BGM Playlist
```

| 字段 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `Tracks` | `BgmTrack[]` | (必填) | BGM 轨道列表 |
| `Shuffle` | `bool` | `false` | 是否随机播放(Fisher-Yates 洗牌) |
| `Loop` | `bool` | `true` | 播完整个列表后是否从头再来 |
| `CrossfadeDuration` | `[0..5]`(秒) | `1.5` | 两首 BGM 之间的交叉淡化时长。`0` = 硬切 |

### 4.3 交叉淡化实现

- 内部维护 2 个 `MusicChannel`(A/B 双通道)。
- 切歌时:当前正在播放的 channel 标为「淡出」,新 track 放到另一个 channel → 「淡入」。
- 在 `CrossfadeDuration` 秒内,每帧从 `BusMixer` 读 Bgm 总线音量 × 各自进度写入 `_source.volume`。

**注意**:如果未来需要"硬切 / 停止再淡出 / 音高滑降"等其他切歌方式,在 `Assets/Scripts/Audio/Music/` 加 `xxxTransition.cs : BgmTransition` + `[SRName]` 即可(与 `SfxRule` 同套路)。

---

## 5. AudioBus 配置(总线音量 / PlayerPrefs 持久化)

### 5.1 创建总线

```
Project 窗口右键 → Create → STG → Audio → Audio Bus
```

推荐创建 4 条总线:`Master` / `Bgm` / `Sfx` / `UI`(项目默认这四条,**BusName 字段必须填对应名字**,否则 `BusMixer.ResolveKind` 无法识别)。

### 5.2 Inspector 字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `MixerGroup` | `AudioMixerGroup` | (可选)Unity AudioMixer 中的目标 Group。**项目目前未建 AudioMixer,先留空** —— 留空时 `BusMixer` fallback 到「按 AudioSource.volume 缩放」,功能完全等价。后续要启用 AudioMixer 时,先 `Create → Audio Mixer`,把每个 Group 拖到 `MixerGroup` 字段 |
| `BusName` | `string` | 总线标识符。`AudioBusKind` 枚举按此名字解析 |
| `PlayerPrefsKey` | `string` | 持久化键。**空 = 不持久化**(每次启动用默认音量)。建议:`audio.master` / `audio.bgm` / `audio.sfx` / `audio.ui` |
| `DefaultVolume` | `[0..1]` | 默认音量 |

### 5.3 把总线拖到 AudioSystem

AudioSystem Inspector 的 4 个字段:
- `MasterBus / BgmBus / SfxBus / UIBus`

**全部留空也能跑**(代码自动建临时 fallback,使用默认 PlayerPrefs 键)。

### 5.4 持久化行为

设置音量:
```csharp
AudioMix.SetBusVolume(AudioBusKind.Sfx, 0.7f);  // → 写 PlayerPrefs("audio.sfx.vol", 0.7)
```

下次启动:
```csharp
float vol = AudioMix.GetBusVolume(AudioBusKind.Sfx);  // 0.7(自动从 PlayerPrefs 读)
```

静音独立于音量(不写入 `vol`):
```csharp
AudioMix.MuteBus(AudioBusKind.Sfx, true);  // → 写 PlayerPrefs("audio.sfx.mute", 1)
```

---

## 6. 宿主挂点:把 SfxCue 拖到 PlayerHealth / BossHealth / EnemyHealth / PlayerShooting

### 6.1 PlayerHealth

| 字段 | 触发时机 | 推荐 cue |
|---|---|---|
| `_hitSfx` | `TakeHit()` 入口(无敌状态除外) | 短促的"啪"声,`MaxVoices=2`,`Cooldown=0.05` |
| `_deathSfx` | `Lives <= 0` 全死时 | 长一点的爆炸音,`Loop=false` |
| `_grazeSfx` | `HandleGraze()`(擦弹 +1) | 极短促的"叮",`Cooldown=0.03` 防止连擦时糊 |
| `_powerUpSfx` | `PowerUp()` 火力提升 | 上扬音,`Loop=false` |

### 6.2 BossHealth

| 字段 | 触发时机 | 推荐 cue |
|---|---|---|
| `_hitSfx` | 每次扣血(打到 Boss 的瞬间) | 短"铛"声,`MaxVoices=2`,`Cooldown=0.04` |
| `_barDepletedSfx` | 某管血打空(`TriggerOnEmpty=true`) | 切管音(类似"叮咚"),`Loop=false` |
| `_deathSfx` | Boss 死亡 | 长爆炸音,`Loop=false` |

### 6.3 EnemyHealth

| 字段 | 触发时机 | 推荐 cue |
|---|---|---|
| `_hitSfx` | `TakeDamage()` 入口 | 短"啪"声,`MaxVoices=2`,`Cooldown=0.05` |
| `_deathSfx` | 敌人死亡 | 短爆炸音,`Loop=false` |

### 6.4 PlayerShooting

| 字段 | 触发时机 | 推荐 cue |
|---|---|---|
| `_shootSfx` | FireGroup 后(每次开火) | 持续"哒哒"声,`MaxVoices=2~3`,`Cooldown=0.02` |

---

## 7. 代码调用(场景脚本切 BGM / 暂停音乐 / 设音量)

调用方**完全不感知**池、限流、路由、衰减 —— 这些是 AudioSystem 内部的事。

### 7.1 SFX

```csharp
using ShinySTG.Audio;

// 最常用:在宿主事件里调
AudioMix.PlaySfx(playerHitSfx);                                 // 走 cue 默认规则
AudioMix.PlaySfx(enemyHitSfx, position: hitPos);                // 世界坐标定位
AudioMix.PlaySfx(enemyDeathSfx, parent: enemyTransform);        // 跟随 GameObject 移动
AudioMix.PlaySfx(explodeSfx, volumeMul: 0.5f);                  // 临时压低音量
AudioMix.PlaySfx(shootSfx, pitch: 1.2f);                        // 临时加速

// 停止某 cue 所有 voice
AudioMix.StopSfx(loopSfx);

// 停止所有 SFX(暂停菜单用)
AudioMix.StopAllSfx();
```

### 7.2 BGM

```csharp
// 切单首
AudioMix.PlayTrack(bossThemeBgm);                              // 默认 1.5s 交叉淡化
AudioMix.PlayTrack(winTheme, crossfade: 0.5f);                  // 快速切

// 列表(顺序/随机/循环由资产决定)
AudioMix.PlayPlaylist(stage1Playlist);

// 暂停 / 恢复(timeScale=0 时 AudioSystem 自动调,这里给手动暂停菜单用)
AudioMix.PauseMusic();
AudioMix.ResumeMusic();

// 停止
AudioMix.StopMusic();                                          // 默认 1.5s 淡出
AudioMix.StopMusic(fadeOut: 0);                                // 立即停止
```

### 7.3 总线音量

```csharp
// 设置
AudioMix.SetBusVolume(AudioBusKind.Sfx, 0.7f);
AudioMix.SetBusVolume(AudioBusKind.Master, 0f);  // 全部静音

// 读取
float vol = AudioMix.GetBusVolume(AudioBusKind.Bgm);

// 单独静音某总线(不影响其他)
AudioMix.MuteBus(AudioBusKind.Sfx, true);

// 全局静音(所有总线)
AudioMix.MuteAll(true);
```

### 7.4 自动切歌(可选,默认关闭)

**当前不自动启用** —— 用户后续打算在 LevelEditor 加"切换 BGM"方法时手动调:

```csharp
// 启用:订阅 LevelController.OnLevelStart / OnBossSpawned / OnBossDefeated,
//       按 LevelAudioBinding 资产自动切歌
AudioSystem.Instance.EventHub.EnableAutoSwitch();

// 禁用
AudioSystem.Instance.EventHub.DisableAutoSwitch();

// 或者直接手动切(无需 EnableAutoSwitch)
AudioMix.PlayTrack(myBgm);                                      // 立刻切
```

---

## 8. 扩展点:加新 SfxRule / FireSound

### 8.1 何时需要加 SfxRule

- 想让 SFX 在播放前做某种处理(随机化、低通滤波、3D 定位、混响、...)。
- 想让 Inspector 里能下拉选,无需改 SfxRouter。

### 8.2 怎么加(2 步)

1. 在 `Assets/Scripts/Audio/Sfx/` 新建 `XxxRule.cs`:

```csharp
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.Audio
{
    [Serializable, SRName("Rule/My Cool Effect")]
    public class MyCoolRule : SfxRule
    {
        [Tooltip("我自己的字段,Inspector 配置")]
        public float SomeParam = 1f;

        public override SfxRequest Process(SfxRequest request)
        {
            // 改 request 字段(Clip / Volume / Pitch)
            request.Volume *= SomeParam;
            return request;
        }
    }
}
```

2. 保存,Unity 自动编译。**SfxCue Inspector 的 Rules 数组点 `+` → 下拉自动出现 `Rule/My Cool Effect`**。

### 8.3 引用类型字段:Clone 深拷约定

如果你的 SfxRule 持有引用类型字段(`AudioClip[]` 之类,**不是**值类型),要 override `Clone()` 深拷 —— 否则多颗子弹 / 多次播放共享同一规则实例导致状态污染。

当前内置的 `RandomPickRule._lastIndex` / `PitchVariationRule.Range` / `CooldownRule._nextAllowedTime`(static 字典)已处理:前两个字段值类型自动 Clone,第三个用 static 字典 + cue instance id 做 key,避免实例共享。

> **未来扩展约定**:新增 `SfxRule` 子类若引入**引用类型实例字段**(非 static、非值类型),需在 SfxRule 基类加 `virtual SfxRule Clone()` 并 override,再让 SfxRouter 在 attach 时深拷(与 `BulletModifier.Clone()` 同套路 —— 见 ARCHITECTURE §2)。

### 8.4 何时需要加 FireSound

- 想让 FirePattern 在播放时执行"非默认"开火音行为(例如"按 PowerLevel 切音调"、"叠双 cue"、"按 Boss 当前 HP 变低沉")。
- 想让 Inspector 里能下拉选,无需改 BulletPool / FirePattern。

### 8.5 怎么加 FireSound

1. 在 `Assets/Scripts/Bullet/FireExtension/` 新建 `XxxFireSound.cs`:

```csharp
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG
{
    [Serializable, SRName("FireSound/My Cool Sound")]
    public class MyCoolFireSound : FireSound
    {
        [Tooltip("我的字段,Inspector 配置")]
        public float SomeParam = 1f;

        public override void OnFireTriggered(Vector2 position, ShinySTG.Hitbox.HitboxComponent ownerHitbox)
        {
            // 在这里播音,或读 ownerHitbox.Team 区分敌我
            // ShinySTG.Audio.AudioMix.PlaySfx(myCue, position: position);
        }
    }
}
```

2. 保存,Unity 自动编译。**FirePattern Inspector → Fire Sounds 数组点 `+` → 下拉自动出现 `FireSound/My Cool Sound`**。

### 8.6 与 SfxRule 的区别

| 维度 | SfxRule | FireSound |
|---|---|---|
| 触发时机 | 每次 `AudioMix.PlaySfx` | 每次 `BulletPool.FireGroup` |
| 触发位置 | SfxRouter 内部 Pipeline | FirePattern 字段 |
| 模块间关系 | 串联(上一步输出 → 下一步输入) | 并行(各模块独立) |
| 用途 | SFX 处理(随机化、pitch、cooldown) | 开火时发声(单 cue / 叠多 cue / 按状态发声) |

---

## 9. 常见问题

### Q1:播放了但没声音

**检查顺序**:
1. AudioSystem 是否在场景里?(Hierarchy 应该有 `Audio` GameObject)
2. SfxCue.Clips 是否为空?
3. TotalAudioMixerGroup 是否被静音?(Project Settings → Audio)
4. AudioListener 是否在场景里?(Main Camera 默认带)
5. `AudioMix.SetBusVolume(AudioBusKind.Master, 0)` 被调过?调 `GetBusVolume` 检查

### Q2:开火音连成一片

1. SfxCue 的 `MaxVoices` 调到 2~3(默认 4 已偏多)。
2. SfxCue 的 `Cooldown` 设 `0.02~0.05` 秒。
3. 添加 `Rule/Cooldown`,`MinInterval=0.05`。

### Q3:BGM 切歌有"咔哒"声

1. `BgmPlaylist.CrossfadeDuration` 不能设 `0`(会硬切)。建议 `1~2` 秒。
2. 检查 BGM clip 的 `Compression Format`:Vorbis 比 PCM 切歌平滑(项目默认应该是 Vorbis)。

### Q4:Boss 死亡音被 Boss 立刻销毁 GameObject 打断

不会 —— `BossHealth._deathSfx` 在 `OnDeath` 触发**前**播放(`BossHealth.TakeDamage` 内),GameObject 销毁后 SfxSource 还在 AudioSystem 子物体上继续播。

### Q5:菜单切场景后 BGM 重置了

AudioSystem 继承 `PersistentSingleton`,**会** `DontDestroyOnLoad`。检查:
1. AudioSystem 是不是被动态 Destroy 了?(不要在 ExitGame 等地方 Destroy 它)
2. 是不是有多个 AudioSystem 场景单例?(`AudioSystem.Instance != null` 但不是当前这个)

### Q6:LevelAudioBinding 资产配好了但 BGM 没切

`AudioEventHub.EnableAutoSwitch()` **默认未调用**。如果你期望"关卡开始 → 自动切 BGM",在场景脚本里调一次 `EnableAutoSwitch()`。或者干脆不用自动订阅,在关卡事件订阅者里手动调 `AudioMix.PlayTrack(...)`。

### Q7:Inspector 里 Rules 下拉没有出现新加的规则

1. 确认 .cs 编译通过(Unity Console 无 error)。
2. 确认加了 `[Serializable]` + `[SRName("Rule/xxx")]` 两个 attribute。
3. 确认 namespace 是 `ShinySTG.Audio`(与 SfxCue.Rules 字段同 namespace)。
4. 选中 SfxCue.asset → Inspector 右键 `Rules` 数组的 `+` → 下拉里找 `Rule/<你的名字>`。

### Q8:FirePattern 加了 FireSounds[] 但播放时没声音

1. 确认 BulletPool.FireGroup(...) 真的被调用了(检查 SpawnBullet / Fire() 是否跑通)。
2. 确认 FireSounds[] 数组里 `SfxCueFireSound.Cue` 字段非空。
3. 确认 SfxCue.Clips 非空且 AudioSystem 在场景里。
4. **Composite 内的子 pattern 不会触发** —— 触发只在最外层 BulletPool.FireGroup 入口一次。若想让每个子 pattern 各发一次,override `CompositeFirePattern.PlayFireSounds` 改成"遍历 Children 各触发一次"。

### 6.5 FirePattern 开火音(`FireSounds[]` 多态模块)

**位置**:`FirePattern` 资产 Inspector → `Fire Sounds` 数组。

**触发时机**:`BulletPool.FireGroup(...)` 入口自动调一次 `pattern.PlayFireSounds(pos, ownerHitbox)`,每次"开火组"播一次。**CompositeFirePattern 的子 pattern 不重复触发**(只在最外层触发一次,Composite 作为整体发声)。

**用法**:
1. 点 `+` → 下拉选 `FireSound/SFX Cue`。
2. 把 `SfxCue` 资产拖到 `Cue` 字段(同一个 FirePattern 在不同 context 可挂不同 cue,但通常同一个 pattern 共用)。
3. 可选:设 `VolumeMul`(临时音量倍率)/ `Pitch`(临时 pitch)。

**与 PlayerShooting._shootSfx 的区别(可并存)**:

| 维度 | `PlayerShooting._shootSfx` | `FirePattern.FireSounds[]` |
|---|---|---|
| 触发位置 | 玩家主控(无论哪个 pattern) | 特定 FirePattern 资产 |
| 敌人 / Boss 用同一个 pattern 时 | 不影响(只在玩家处触发) | 自动不带这个音 |
| 不同 pattern 想不同音 | 不支持(只能一个 cue) | 支持(各 pattern 配各的) |
| 多 cue 叠加 | 不支持 | 支持(数组里堆多个模块) |
| 限流 / Pipeline / Bus | 走 SfxCue 体系 | 走 SfxCue 体系 |

**推荐用法**:**保留** `_shootSfx` 作为"玩家整体开火"的兜底音(便宜、不区分 pattern),再用 `FireSounds[]` 配各 pattern 的特征音(贵、精细)。

**扩展**:加新开火音模块 = `Assets/Scripts/Bullet/FireExtension/` 新建 `XxxFireSound.cs : FireSound` + `[SRName("FireSound/<名字>")]`,Inspector 自动下拉出现。