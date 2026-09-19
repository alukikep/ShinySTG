# 玩家配置

## 死亡与重生

在玩家根对象上添加 `PlayerDeathController` 配置死亡表现。旧玩家缺少该组件时，
Player 会在运行时补齐，采用无特效、等待 0.3 秒后回到初始位置的默认行为。
要保存配置，请在非运行状态手动添加组件。

- Death Commands：通过 SR 下拉配置清弹、掉落等指令，每次失去一命执行一次，包含最后一命。
- Death Effect Prefab：拖入一次性特效，例如 [DebugDeath_Sparks](../../Resources/Effects/DebugDeath_Sparks.prefab)。
- Respawn Delay：特效结束后额外等待的时间，不是特效播放总时长。
- Respawn Point：重生位置；留空使用玩家初始位置。
- Blink Interval：重生无敌期间本体闪烁间隔。无敌时长仍在 PlayerHealth 配置。

中弹立即禁止操作并隐藏本体与子机；下一帧执行指令并在原地播放特效。
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
