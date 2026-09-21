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


## 低速判定点

PlayerHitboxIndicator 是独立表现组件，读取本对象的 FocusHeld、Health.CanInteract 和
Hitbox 启用状态，在 LateUpdate 对齐受伤中心；不修改碰撞形状或尺寸。
Player 为旧对象运行时补齐组件，贴图为空时生成默认亮点；需要保存外观配置时预先添加组件。
配置见 [玩家操作说明](../../Assets/Scripts/Player/README.md#低速判定点)。

判定点拥有独立渲染器，PlayerDeathController 通过所有权判断将其排除在本体闪烁之外。
死亡通知立即隐藏判定点，复活后重新按低速状态显示；对话锁通过 FocusHeld 自然屏蔽显示。
淡入淡出使用游戏时间，禁用时重置透明度并解除事件订阅，销毁时释放自行生成的图形资源。
外部不应同时控制该渲染器的颜色和可见性。

## 死亡与重生

PlayerHealth 负责生命结算和可交互状态，PlayerDeathController 负责死亡指令、隐藏、
特效等待及重生位置。Player 为旧对象在运行时补齐协调组件；需要保存表现配置时，
在编辑模式添加组件，见 [玩家操作说明](../../Assets/Scripts/Player/README.md)。

IsDead 仍表示生命耗尽；IsDying 覆盖失去一命后的演出和等待，最后一命结束后也保持该状态。
CanInteract 同时检查 Health 启用、生命和死亡流程，供移动、射击、碰撞、擦弹及拾取判断。
无敌与可交互状态分开：复活无敌不禁止移动、射击或拾取。

实际提交受击时先锁定死亡状态，再按原约定发出扣命前 OnLifeLost 和扣命后 OnLivesChanged，
防止事件重入重复扣命或提前复活。OnAllLivesLost 仍在归零时通知，不代表演出已结束。
OnDeathPresentationComplete 在特效及额外等待结束、自动重生之前通知；需要延后 Game Over
的宿主监听它并检查生命。OnRevive 与复活无敌计时只在实际重新出现时开始。
AddLife 只修改生命；等待期间加命影响结束时的重生决定，终局续关通过协调组件的 Respawn 入口。

死亡时本体和独立子机隐藏，根对象保持启用。特效由场景 EffectPool 独立播放，按实例及
播放版本确认本次播放是否结束，避免对象回池复用串扰。重生时子机直接对齐新位置。
演出使用游戏时间，不暂停世界；禁用协调组件取消等待并回收自己仍持有的那次特效，
重新启用时若仍处于死亡状态会重新开始演出，但不重复已执行的死亡指令。

死亡指令使用现有 GlobalCommandExecutor 和 PlayerDeath 上下文，在实际扣命的下一帧执行，
避免清弹修改碰撞正在遍历的池。位置相关指令读取玩家执行时位置，重生移动发生在其后；
外部脚本不应在死亡等待期间移动玩家。死亡状态门控不占用对话控制锁，重生后长按可继续射击，
但仍受对话锁及其松键规则限制。

## Bomb 与决死窗口

PlayerResources 负责库存增减与通知，PlayerBomb 负责释放条件、效果和动作宿主；
BombDefinition 提供共享配置，未指定资产时保留组件字段回退。伤害通过 EnemyHealth 与
BossHealth 的既有入口结算，清弹通过池的阵营过滤回收，不直接销毁对象。

Bomb 复用 GameActionRunner，不另建动作执行体系；序列内部串行，Parallel 提供并行。
当前宿主的 Starting 不等待开始序列，Active 由持续时间推进，Ending 仅支持瞬时结束动作；
ActionSequence.WaitForCompletion 尚未参与 Bomb 阶段切换。正常结束和禁用会释放 Runner。
配置与当前限制见 [玩家操作说明](../../Assets/Scripts/Player/README.md#bomb-与动作序列)。

Deathbomb 的待结算受击由 PlayerHealth 持有，窗口不等于 IsDying 或无敌锁，
不改变 CanInteract。重复受击不刷新窗口；成功消费 Bomb 后取消待结算受击，
不回滚生命或已发送事件。窗口使用游戏时间，暂停冻结；窗口为零沿用立即结算。
窗口开启不检查库存或 Bomb 是否忙碌，成功释放仍遵守正常释放条件。

窗口到期重新检查可交互、无敌和 BattleRestriction 后提交生命结算；禁用 Health
清除待结算状态。因此现有结束流程可能在窗口扣命之前定案，尚无等待待结算受击的协调。
OnLifeLost、OnLivesChanged、OnAllLivesLost 均在实际提交时通知，死亡表现随后开始。
操作与验收见 [Deathbomb](../../Assets/Scripts/Player/README.md#deathbomb决死-bomb)。

## 道具与玩家资源

`PlayerHealth` 以整数百分单位保存 Power，对外显示小数；射击和子机继续读取向下取整的
`PowerLevel`。原有 InitialPower、MaxPower 和 PowerUp 整级接口保留。OnPowerChanged 通知数值变化，
OnPowerUp 仅在整数等级变化时通知。残机仍由 PlayerHealth 管理。

`PlayerResources` 管理分数与 Bomb 库存，不负责 Bomb 释放。Player.Awake 获取该组件，旧 prefab
缺失时在运行时补齐；需要配置初始库存时可预先添加组件。

`PlayerHitbox` 提供独立的拾取和吸附 AABB；原 Size / WorldBounds 仍专用于受伤。
吸附范围按本对象 PlayerMovement.FocusHeld 在高速、低速配置间切换，不按实际移动速度判断；
缺少移动组件时使用高速范围。控制锁屏蔽 Focus 输入时同样使用高速范围。
旧 AttractionSize 通过 FormerlySerializedAs 迁移为高速配置，低速范围独立配置。
模式切换只影响开始吸附的判定，已经吸附的道具继续追踪。配置与可视化见
[道具操作说明](../../Assets/Scripts/Items/README.md)。
无敌不妨碍拾取，死亡或禁用玩家不能拾取；对话控制锁只锁输入，不自动暂停道具。

## HUD 接入

玩家生命总数包含本体，HUD 显示本体之外的备用残机。OnLivesChanged 在加命或扣命写入新值后通知，
OnLifeLost 保持原有扣命前时序。玩家仅提供数据与通知，不持有具体 UI 引用；
分数、Bomb、Power 与擦弹通过现有接口供只读展示。绑定和扩展边界见 [HUD](./arch-hud.md)。

## 对话接入

PlayerControlLock 提供可叠加的控制令牌，持有期间移动与低速输入被屏蔽，主炮和子机共用受限后的 FireHeld。输入源继续提交原始按键状态，避免松键事件丢失；最后一个锁释放当帧仍禁止射击，长按攻击需松开再按。每个宿主只释放自己的令牌，锁覆盖期间新生成的玩家，但不提供无敌或暂停世界。子系统注册时重置静态锁集合。

## 与其他板块的关系

- [hud](./arch-hud.md) — 只读订阅玩家状态，不参与资源结算和生命周期规则。

- [items](./arch-items.md) — 拾取调用玩家资源接口，吸附和拾取读取 PlayerHitbox 的独立范围。

- [dialogue](./arch-dialogue.md) — 对话播放及与战斗的协作边界。

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [hitbox](./arch-hitbox.md) — PlayerHitbox 继承 HitboxComponent,阵营 = Player
- [bounds](./arch-bounds.md) — PlayerMovement 从 BoundsService.PlayableArea 读取活动边界
- [fire-pattern](./arch-fire-pattern.md) — PlayerShooting.MainPatterns 与子机弹幕引用 .asset
- [audio](./arch-audio.md) — PlayerHealth._hitSfx / _deathSfx 等嵌入 SfxCue 字段
