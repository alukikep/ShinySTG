# 玩家系统

> Player 主控 + 子机 + OptionPositionForm

> 本板块对应 ARCHITECTURE § 7. 玩家系统(Player + 子机 + 多态位置)(原 ARCHITECTURE.md 第 1064–1083 行)。
> 本文档面向项目维护者,不复制实现细节,字段 / 数值 / 默认值以源文件为准。

---

## 7. 玩家系统(Player + 子机 + 多态位置)

**职责:** 玩家主控(单例 + 输入分发) + 八方向移动 + 按住攻击 + 残机/火力级/无敌 + 子机系统。整体走"**数据驱动 + 多态位置**"的套路,与项目既有扩展机制一致。

**协作边界:**
- 主炮与子机开火**共用同一触发源**,松开攻击键时同步停。
- 子机数量由 `PlayerHealth.PowerLevel` 单向驱动;子机不会反向改 PowerLevel,避免循环依赖。
- 子机开火形态也是 FirePattern 资产(`OptionFirePatterns[]`),直接复用敌人那套弹幕体系。
- 玩家事件只发通知,不硬编码死亡动画 / 特效 —— "事件层与表现层分离"。

**输入方案(双重兼容):**
- 装了 `com.unity.inputsystem` → 用 `PlayerInput` 组件(Behavior=Invoke C# Events)。
- 没装 → 直接挂 `LegacyInputDriver`,每帧 `Input.GetKey` 灌给 Movement / Shooting。
- **二选一,不要两个都挂,会双重输入。**

**扩展点:**
- 新增子机位置形态:新建 `OptionPositionForm` 子类 + `[Serializable, SRName("Form/<名字>")]`(详见 `Assets/Scripts/Player/`)。

---


## 对话接入

PlayerControlLock 提供可叠加的控制令牌，持有期间移动与低速输入被屏蔽，主炮和子机共用受限后的 FireHeld。输入源继续提交原始按键状态，避免松键事件丢失；最后一个锁释放当帧仍禁止射击，长按攻击需松开再按。每个宿主只释放自己的令牌，锁覆盖期间新生成的玩家，但不提供无敌或暂停世界。子系统注册时重置静态锁集合。

## 与其他板块的关系

- [dialogue](./arch-dialogue.md) — 对话播放及与战斗的协作边界。

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [hitbox](./arch-hitbox.md) — PlayerHitbox 继承 HitboxComponent,阵营 = Player
- [bounds](./arch-bounds.md) — PlayerMovement 从 BoundsService.PlayableArea 读取活动边界
- [fire-pattern](./arch-fire-pattern.md) — PlayerShooting.MainPatterns 与子机弹幕引用 .asset
- [audio](./arch-audio.md) — PlayerHealth._hitSfx / _deathSfx 等嵌入 SfxCue 字段
