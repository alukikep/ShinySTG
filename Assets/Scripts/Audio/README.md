# Audio System 配置说明

本文只说明如何配置和调用音频系统。架构边界见
[`docs/architecture/arch-audio.md`](../../../docs/architecture/arch-audio.md)，开火音扩展见
[`arch-fire-pattern.md`](../../../docs/architecture/arch-fire-pattern.md)。

## 快速配置

1. 场景中新建 GameObject，添加 `AudioSystem`。它会跨场景保留；总线字段留空时会使用默认配置。
2. `Create -> STG -> Audio -> SFX Cue` 创建音效资产，把一个或多个 `AudioClip` 放入 `Clips`。
3. 将 `SfxCue` 拖到使用者字段：

   | 组件 | 常用字段 |
   |---|---|
   | `PlayerHealth` | `_hitSfx`、`_deathSfx`、`_grazeSfx`、`_powerUpSfx` |
   | `BossHealth` | `_hitSfx`、`_barDepletedSfx`、`_deathSfx` |
   | `EnemyHealth` | `_hitSfx`、`_deathSfx` |
   | `PlayerShooting` | `_shootSfx` |

字段留空表示不播放。

### 关卡自动切歌

为关卡创建 `LevelAudioBinding`，填写 `Playlist`、`BossMusic`、`DefeatMusic`，再挂到
`LevelDefinition.AudioBinding`。当 `AutoSwitchBgm` 勾选时，`LevelController.BeginLevel()` 会自动绑定：

| 事件 | 播放内容 |
|---|---|
| 关卡开始 | `Playlist` |
| Boss 出现 | `BossMusic` |
| Boss 击败 | `DefeatMusic` |

在 `STG -> Level Editor` 打开关卡后，可用工具栏的 `+ Create AudioBinding` 自动创建并关联资产。
完整关卡操作见 [`LEVEL_EDITOR.md`](../../../LEVEL_EDITOR.md#音频集成)。

## 资产速查

### SfxCue

| 分组 | 关键字段 | 说明 |
|---|---|---|
| Clips | `Clips` | 候选音频；为空时静默跳过 |
| Playback | `DefaultVolume`、`DefaultPitch`、`Loop` | 基础播放参数 |
| Routing | `Bus`、空间参数 | 留空总线时走默认 SFX Bus |
| Throttling | `Cooldown`、`MaxVoices` | 高频音效的限流与并发数 |
| Rules | `SfxRule[]` | 按数组顺序处理播放请求 |

常用规则：

- `Rule/Random Pick`：从 `Clips` 随机选择，可避免连续重复。
- `Rule/Pitch Variation`：在指定范围内随机改变音高。
- `Rule/Cooldown`：限制同一 cue 的最小触发间隔。

### 高频声音合并

使用 Cue 的 `Cooldown` 设置固定合并窗口：第一次有效请求立即播放，窗口内同一 Cue 的后续请求不再创建声部，也不会延长窗口、重启已有声音或在窗口结束后补播。多个敌人共用同一 Cue 时会一起合并；不同 Cue 互不影响。窗口使用不受游戏时间缩放影响的时间。

目前敌机射击 `EenmyShot1` 使用 30 毫秒窗口，`Damage` 和 `Graze` 使用 40 毫秒窗口。这三个 Cue 同时使用 `Overflow=DropNewest`，并发满额时保留已有尾音。合并不会随事件数量提高音量，避免密集事件产生音量尖峰。

试听时从 `Cooldown=0.02~0.05` 开始调整；设为 `0` 可关闭时间窗口合并。窗口越长，声音越稀疏；`MaxVoices` 太小时，即使窗口已结束，也可能因尾音仍占用声部而忽略新请求。单次重要提示和循环音不要直接套用这组配置，也无需额外添加 `Rule/Cooldown`。

### BgmTrack / BgmPlaylist

| 资产 | 关键字段 |
|---|---|
| `BgmTrack` | `Clip`、`DefaultVolume`、`Loop`、`Bus`、`StartTime` |
| `BgmPlaylist` | `Tracks`、`Shuffle`、`Loop`、`CrossfadeDuration` |

BGM 通常使用 Streaming 导入方式。`CrossfadeDuration=0` 表示硬切，通常建议使用 1~2 秒。

### AudioBus

推荐创建 `Master`、`Bgm`、`Sfx`、`UI` 四条总线。关键字段：

- `BusName`：总线标识，需与上述名称对应。
- `DefaultVolume`：默认音量。
- `PlayerPrefsKey`：为空时不持久化。
- `MixerGroup`：可选；未配置 AudioMixer 时可留空。

## 常用代码

```csharp
using ShinySTG.Audio;

AudioMix.PlaySfx(hitSfx);
AudioMix.PlaySfx(explodeSfx, position: hitPosition, volumeMul: 0.5f);
AudioMix.StopSfx(loopSfx);
AudioMix.StopAllSfx();

AudioMix.PlayTrack(bossTheme, crossfade: 1.0f);
AudioMix.PlayPlaylist(stagePlaylist);
AudioMix.PauseMusic();
AudioMix.ResumeMusic();
AudioMix.StopMusic();

AudioMix.SetBusVolume(AudioBusKind.Sfx, 0.7f);
float volume = AudioMix.GetBusVolume(AudioBusKind.Bgm);
AudioMix.MuteBus(AudioBusKind.Sfx, true);
AudioMix.MuteAll(true);
```

## FirePattern 开火音

在 `FirePattern` 的 `Fire Sounds` 数组添加 `FireSound/SFX Cue`，再指定 `SfxCue`。
每次最外层 `BulletPool.FireGroup` 触发一次；`CompositeFirePattern` 的子 pattern 不重复发声。

它与 `PlayerShooting._shootSfx` 可以并存：前者属于特定 pattern，后者是玩家整体开火音。
新增 `FireSound` 类型的方法见
[`arch-fire-pattern.md`](../../../docs/architecture/arch-fire-pattern.md#32-开火音多态扩展firesound)。

## 常见问题

### 播放时没有声音

依次检查：

1. 场景是否存在 `AudioSystem` 和 `AudioListener`。
2. `SfxCue.Clips` 或 `BgmTrack.Clip` 是否为空。
3. Master/目标 Bus 音量是否为 0 或被静音。
4. 是否被 `Cooldown` 或 `MaxVoices` 限流。

### 高频音效糊成一片

优先按上面的高频声音合并配置调整 `Cooldown`，需要保留尾音时使用 `Overflow=DropNewest`。不要同时叠加含义重复的 `Rule/Cooldown`，也不要仅靠降低 `MaxVoices` 反复截断声音。

### BGM 切换有咔哒声

将 `CrossfadeDuration` 调到 1~2 秒，并检查音频导入和循环点。

### LevelAudioBinding 没有切歌

检查 `LevelDefinition.AutoSwitchBgm`、`AudioBinding`、绑定资产中的音乐字段、场景中的
`AudioSystem`，以及 `LevelController.BeginLevel()` 是否实际执行。

### 新规则未出现在 Inspector

确认类型可序列化、继承正确、带有对应 `SRName`，并先解决 Unity Console 的编译错误。

## 维护边界

- 本文维护配置流程和公共调用示例，不记录逐行执行流程。
- `SfxRule`、`FireSound` 的扩展契约以架构文档和基类注释为准。
- 字段改名或默认值变化时优先更新 Inspector Tooltip，本文只保留稳定的关键字段。
