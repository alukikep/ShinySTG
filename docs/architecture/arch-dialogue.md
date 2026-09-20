# 对话系统

对话采用线性资产、场景播放器和 GameAction 适配器。台词与人物不依赖场上的 Boss 对象，
因此同一套 UI 可以用于入场、击破保留和 Boss 销毁后的收尾。

## 职责边界

DialogueDefinition 与 DialogueCharacter 保存配置；DialogueService 为每次播放复制台词数据，
管理进度并返回 DialogueHandle，不把播放状态写回共享资产。
DialogueView 负责 TMP 文本与左右立绘，DialogueInput 将当前项目的旧版键盘输入转为确认和快进。
DialoguePreview 仅用于独立试播，正式 Encounter 应关闭其自动播放。

Service 每次只接受一个会话，重复请求失败，不覆盖或排队。它不是跨场景单例；
PlayDialogueAction 要求场景中唯一启用的 Service，避免隐式选择错误 UI。
打字机与快进使用未缩放时间，timeScale 为零不会暂停对话；接入暂停菜单时需协调输入和播放推进。

## 生命周期与控制

句柄的 IsComplete 包含正常结束、取消和失败，调用方需结合 IsCancelled 与 Failure 判断结果。
取消幂等，旧句柄不能取消新会话。服务禁用或销毁会清理会话、UI 与自身持有的控制锁；
播放期间 View 失效会在下一次更新结束失败会话。空台词直接完成，不要求有效 UI。

播放默认持有 PlayerControlLock，试播和直接调用也适用；可由调用方明确关闭控制锁。
锁按令牌叠加，覆盖移动、低速操作及主炮/子机射击。解除自身锁不会解除其他宿主的限制。
结束当帧防止确认键穿透；玩家长按射击需要松键再按才恢复。
控制锁不等于无敌或世界暂停，残留弹幕和道中敌人仍需由关卡编排处理。

## Encounter 适配与扩展

在 ActionSequence 的 SR 下拉加入 Game Action/Play Dialogue，即可等待真实播放结束；
无需增加 Encounter 专用对话字段。适配器的 Dispose 只取消其持有的播放句柄。
缺服务、多个服务、失败或外部取消会终止本组动作并报告 Failure，等待式开场或阶段对话失败会停止战斗并结束遭遇；击破或完成对话失败则继续收尾。

StartActions 适合战前对话，DefeatActions 适合保留 Boss 的战后对话，CompleteActions 适合 Boss 消失后的立绘对话。
等待条件和关卡时间轴阻塞由原有 Encounter/Level 契约决定；临时无敌与消弹复用已有动作。
音乐事件的时机保持不变，不因台词结束而自动切歌。

扩展输入时调用 Service 的 Confirm / SetFastForward，并替换原输入适配器，避免重复输入。
新增演出动作仍遵循 GameAction 的独立运行实例和 Dispose 契约。
当前不包含选项分支、历史回看、自动分页或存档恢复。

## 与其他板块的关系

- [game-actions](./arch-game-actions.md)：提供可等待、可取消的动作适配契约。
- [boss](./arch-boss.md)：提供首阶段等待和击破对象保留。
- [level](./arch-level.md)：决定 Encounter 是否阻塞关卡时间轴。
- [player](./arch-player.md)：提供控制锁与统一射击状态。
- [操作说明](../../Assets/Scripts/Dialogue/README.md)：场景搭建、资产配置、调用与验收。
