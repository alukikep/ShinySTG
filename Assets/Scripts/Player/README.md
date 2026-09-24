# 玩家配置

## 基础计分

玩家根对象会自动补齐 `ScoreManager`。它监听普通敌人和 Boss 的最终死亡事件，以及
`PlayerHealth.OnGraze`，并将分数写入 `PlayerResources`。玩家 `ScoreManager` 上的 `Graze Score`
配置每次有效擦弹的分值，默认是 10；擦弹仍沿用现有碰撞冷却。普通敌人的 `EnemyHealth.ScoreValue`
默认是 100，Boss 的 `BossHealth.ScoreValue` 默认是 10000。只有血量归零触发的死亡才计击杀分，
Kill Enemies 强制死亡同样计分；敌人自毁、出界和无奖励清场不计分，
Clear Enemies 开启死亡特效也不计分。指令配置见[清除普通敌人](../GameActions/README.md#清除普通敌人)。

## Bomb 与动作序列

在玩家根对象上确认已有 PlayerBomb 和 PlayerResources；已有 prefab 请手动检查，
不要依赖 RequireComponent 为旧对象追补组件。在 PlayerResources 设置初始库存。
通过 Create → ShinySTG → Player → Bomb Definition 创建资产，拖入 PlayerBomb 的 Definition。
未指定资产时使用组件上的兼容配置。旧输入驱动 LegacyInputDriver 默认按 X 释放；
其他输入适配器在按下时调用 Player.OnBomb(true)，不要每帧重复提交 true。
PlayerInput 的 Invoke C# Events 需要代码适配回调，不能直接在 Inspector 绑定 bool 方法。

先关闭全场伤害，测试敌弹和敌方激光清除；再启用伤害。Damage Interval 为零时只伤害一次，
大于零时在 Active 期间继续伤害，每次伤害量由 Enemy Damage 配置。
伤害仍遵守敌人和 Boss 的无敌及血管规则。暂停、控制锁、战斗限制和库存不足时不能释放。
当前 Bomb 正常结束会移除自己的无敌，因此无敌时间不要设得比 Bomb 持续时间更长。

动作配置复用 [通用游戏动作](../GameActions/README.md)。在 Start Actions 或 Active Actions
的 Actions 下拉中加入 Game Action/Fire Pattern，指定 Pattern；多次发射时 Interval 必须大于零。
可以在同一序列中组合 Wait、Play Sfx、Execute Commands 和 Parallel。
例如 Active Actions 配置 Wait 后接 Fire Pattern，即可延迟发射。
内置清弹、伤害、音效仍会执行，使用动作配置相同效果时注意避免重复。

当前宿主不会等待 Start Actions 完成再进入 Active，Wait For Completion 尚未接入阶段切换。
End Actions 启动后立即取消未完成动作，只适合瞬时指令或音效。
Fire Pattern 当前跟随全局玩家位置发射；第二次发射可能发生在下一帧，后续才按间隔推进。
不要将它用于需要 Owner 上下文或精确首发间隔的其他宿主。

Return Spawned Bullets On End 当前通过发射前后集合差记录直接生成的子弹，
尚未校验 SpawnVersion，也不追踪后续分裂弹和激光。池复用时存在误回收风险，
需要可靠隔离时暂时关闭此选项。禁用组件目前只清空记录，不会回收记录中的子弹。

## Deathbomb（决死 Bomb）

在 BombDefinition 中设置 Deathbomb Window；设为零关闭决死窗口。
窗口使用游戏秒数而非固定帧数，可先用较长窗口测试，再缩短到适合的操作难度。
未配置 Definition 时，组件仍有代码定义的默认窗口。

有效中弹后先记录待结算受击，窗口内不扣命、不启动死亡演出，重复受击不刷新窗口。
窗口内仍允许移动、射击和拾取。成功释放 Bomb 扣除一枚库存并取消待结算受击；
暂停期间窗口冻结。无库存或 Bomb 尚未结束时也会打开窗口，但不能成功决死。
窗口到期重新检查可交互、无敌和战斗限制，通过后才扣命；禁用 PlayerHealth 会清空待结算受击。
因此禁用、后来获得无敌或关卡限制可能使此次受击不结算，当前不是强制提交机制。

验收：窗口内按键不扣命且仅消耗一枚 Bomb；不按键到期仅扣一命；
连续中弹不延长窗口；暂停恢复后继续计时；窗口为零立即进入死亡流程；
最后一命在实际扣命时才触发 Game Over。另检查低帧率和窗口边缘输入，
当前输入与计时的 Update 顺序可能影响最后一帧是否成功。

## 死亡与重生

在玩家根对象上添加 `PlayerDeathController` 配置死亡表现。旧玩家缺少该组件时，
Player 会在运行时补齐，采用无特效、等待 0.3 秒后回到初始位置的默认行为。
要保存配置，请在非运行状态手动添加组件。

- Death Commands：通过 SR 下拉配置清弹、掉落等指令，每次失去一命执行一次，包含最后一命。
- Death Effect Prefab：拖入一次性特效，例如 [DebugDeath_Sparks](../../Resources/Effects/DebugDeath_Sparks.prefab)。
- Respawn Delay：特效结束后额外等待的时间，不是特效播放总时长。
- Respawn Point：重生位置；留空使用玩家初始位置。
- Blink Interval：重生无敌期间本体闪烁间隔。无敌时长仍在 PlayerHealth 配置。
- Respawn Bombs：复活时将 Bomb 库存重置为该数量；死亡时当前进行中的 Bomb 会立即终止。

实际扣命时立即禁止操作并隐藏本体与子机；下一帧执行指令并在原地播放特效。
启用 Deathbomb 时，中弹先等待决死窗口，见下方说明。
特效完全结束后开始额外等待，有生命则重新出现，否则保持隐藏。
指令推迟一帧是为了避免清弹修改碰撞遍历中的对象池。暂停期间不推进演出。
表现使用 forceRenderingOff，不改 Renderer 的 enabled 或颜色。

子机重生时直接对齐新位置。死亡期间不拾取、不吸附、不擦弹；复活无敌期间可以拾取。
长按攻击可在重生后继续射击，对话控制锁仍保留其松键要求。
最后一命仍立即发出 OnAllLivesLost；Game Over 如果需要等待演出，应监听
PlayerDeathController.OnDeathPresentationComplete，并检查生命是否为零。
续关先调用 AddLife，再调用 PlayerDeathController.Respawn；加命本身不会跳过死亡演出。
禁用组件会取消当前演出；重新启用时若仍处于死亡状态，会重新开始演出，
包括最后一命演出已结束但尚未续关的情况，不重复执行已执行的指令。

死亡音效可以放在特效的 Play Sfx。PlayerHealth 的 Hit Sfx 每次扣命播放，
Death Sfx 仍仅在生命归零播放，避免把相同音效同时配置在两个位置。
循环粒子由特效最长时长兜底回收，参见 [特效说明](../Effects/README.md)。

指令示例：Death Commands 添加 Command/Clear Projectiles，选择清除敌弹并包含激光；
需要死亡掉落时再添加 Command/Spawn Drops 并指定 DropProfile。
这会生成道具，不会自动扣除玩家 Power。玩家死亡不受该指令的 Boss 阶段退出开关限制。
指令使用执行时玩家位置，死亡等待期间不要让其他脚本移动玩家。

生命结算与表现的职责、事件时序见 [玩家架构](../../../docs/architecture/arch-player.md)。

验收：连续中弹只扣一次；最后一命不复活；无特效也能完成等待；清弹包含激光时无遍历异常；
暂停、对话锁、禁用重启、场景切换后无残留演出；子机不从死亡位置滑向重生位置。


## 低速判定点

Player 会在运行时补齐 PlayerHitboxIndicator，按住低速键即可显示默认中心亮点。
需要保存自定义配置时，在玩家根对象上手动添加该组件；无需创建子对象或指定 Renderer，
组件会创建并管理独立的 Hitbox Indicator 子对象。

可以指定 Sprite、颜色、本地尺寸、排序层和淡入淡出时长；Sprite 留空使用程序生成的亮点。
排序层应与本体相同或更高，层内排序应高于本体。图形尺寸只表示视觉提示，
不会自动匹配或修改 PlayerHitbox 的 AABB；实际受伤范围仍以 PlayerHitbox 为准。

显示读取 FocusHeld，静止时按住低速也会显示。松键淡出，对话控制锁生效时同样淡出。
死亡立即隐藏，复活后根据低速状态重新淡入；判定点不参与本体的无敌闪烁。
暂停冻结淡入淡出进度，禁用或重新启用组件会清空过渡状态。
可预先添加组件并禁用，以关闭默认判定点。

验收：低速静止与移动、快速切换、复活无敌、对话锁、暂停及禁用重启；
确认判定点始终位于受伤中心，死亡当帧无残留，判定范围保持不变。
