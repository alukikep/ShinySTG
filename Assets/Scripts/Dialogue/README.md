# 对话本体

提供线性台词、左右立绘、说话者高亮、逐字显示、确认与按住快进。
运行时数据不写回对话资产；播放服务返回可以等待和取消的句柄。
默认播放期间锁定玩家移动、低速操作及主炮/子机射击。无敌、关卡等待和音乐由宿主配置。

## 最小场景配置

1. 创建一个 Screen Space - Overlay Canvas，配置 CanvasScaler 为 Scale With Screen Size。
2. 在 Canvas 下创建对话面板，挂 CanvasGroup 和 `DialogueView`。建议面板占屏幕底部，
   姓名和正文使用 TextMeshProUGUI，左右各放一个 Image 作为立绘。
   立绘 Image 可以在对话面板外布局，但必须放在同一个 CanvasGroup 下。
3. 将 CanvasGroup、正文、姓名、两张立绘和可选继续提示拖到 View 对应槽位。
   正文必须启用自动换行并留足高度；第一版不自动分页，长台词请拆为多句。
   TMP 使用覆盖中文字符的字体或回退字体。首次使用 TMP 时按 Unity 提示导入 Essential Resources。
4. 创建一个保持启用的 `DialogueSystem` 对象，挂 `DialogueService` 和 `DialogueInput`。
   将 View 赋给 Service，将 Service 赋给 Input。每个场景配置一组播放器即可。
   UI 通过 CanvasGroup 透明度隐藏，不要禁用面板、正文或服务对象。
   继续提示应是独立子对象，不能引用整个面板或正文对象。
5. 在 Project 中选择 Create → STG → Dialogue → Character 创建角色；
   再选择 Conversation 创建台词资产，配置角色、左右位置和正文。
   立绘纹理需要使用 Sprite 类型；暂时没有美术时可留空。
6. 给 `DialogueSystem` 挂 `DialoguePreview`，指定 Service 和对话资产，进入 Play Mode 自动试播。
   也可以关闭 Play On Start，在运行时使用组件菜单 `Play Dialogue (Play Mode)`。

默认 Z / Enter：未显示完时补全文字，显示完后进入下一句。
按住左 Ctrl：立即显示全文，并按配置间隔逐句快进。
开始播放时需先松开上述按键，防止之前的长按直接推进对话。
左右立绘会保留到被该侧下一句替换；每句未配置表情时重新使用角色默认立绘。
角色为空时姓名为空，当前侧立绘取可选覆盖图，未配置覆盖图则隐藏。

## 调用与生命周期

```csharp
using ShinySTG.Dialogue;

// service 是场景中已绑定 View 的 DialogueService 引用。
DialogueHandle handle = service.Play(definition);
// 每帧检查 handle.IsComplete；正常结束、取消和失败均会变成 true。
// handle.IsCancelled 区分取消，handle.Failure 区分配置或播放失败。
// 宿主退出时只取消自己持有的句柄，不影响之后的新会话。
handle.Cancel();
```

- 空台词数组或全为空条目的资产立即完成。缺少资产、服务未启用、UI 引用无效时返回失败句柄并记录异常。
- 同一个 Service 的重复 Play 请求返回失败句柄，当前会话继续播放。第一版不排队。
- 正常结束、取消、服务禁用或销毁都会清理 UI 和运行状态。View 在播放期间失效时，服务在下一次 Update 结束失败会话。
- `Cancel` / `Dispose` 可以重复调用；已结束的旧句柄不能取消新会话。
- 打字机和快进使用 unscaledDeltaTime；游戏 timeScale 为零时仍然推进对话。
  将来接入暂停菜单时需由输入宿主明确协调，不应假定 timeScale 会暂停此模块。
- 键盘适配使用项目当前启用的 Legacy Input Manager。其他输入源可调用 `Confirm()` 与
  `SetFastForward(bool)`；同一会话只配置一种输入来源。控制锁释放后，射击需松键再按。
- 场景服务和 View 不使用对象池，也不跨场景保留；每次播放清空左右立绘、文本和输入状态。

Boss 接入使用 `Game Action/Play Dialogue`，等待此句柄，并在动作 Dispose 时取消。
`Play(definition, false)` 可明确关闭玩家控制锁；试播组件默认也会锁定控制。
锁可以叠加，每次播放只释放自己的令牌，覆盖对话期间新生成的玩家。
消弹和临时无敌复用已有动作，遵循
[GameActions 生命周期契约](../../../docs/architecture/arch-game-actions.md)。

## Play Mode 验收

- 创建左右交替的三句台词，检查姓名颜色、说话者高亮、表情替换和继续提示。
- 使用中文和 TMP 富文本检查逐字显示；第一次确认补全、第二次确认换句。
- 按住 Z 进入会话，确认不会自动跳句；长按 Ctrl 快进后，下一次播放需松键才能操作。
- 结束后重新播放，确认没有残留立绘或快进计时；尝试旧句柄 Cancel，确认不影响新会话。
- 分别验证空数组、空条目、缺立绘、缺 View、重复 Play，避免卡在等待状态。
- 播放中禁用 Service、禁用 View、销毁 UI 或切换场景，确认句柄终止，UI 清理。

独立试播不会暂停敌人或消除伤害，应在没有战斗风险的测试场景中进行。

## Boss 战前与战后配置

场景只保留一个启用的 DialogueService，并关闭 DialoguePreview 的自动试播，避免抢占会话。
在 BossEncounterEntry 开启 BlockTimeline；以下动作序列均保持 WaitForCompletion 开启。

1. 战前：StartActions 添加 Invincibility Scope，勾选 ProtectPlayer 与 ProtectOwner。
   在其 Sequence.Actions 依次添加 Execute Commands（Command/Clear Projectiles）和
   Game Action/Play Dialogue，指定战前台词并保持 LockPlayerControls 开启。
2. 战后：DefeatActions 添加 Invincibility Scope，勾选 ProtectPlayer。
   在内部先消弹，再 Play Dialogue 指定战后台词。等待结束之前 Boss 对象会保留。
3. 若 Boss 应先消失，将战后对话和玩家保护放在 CompleteActions；对话显示不依赖 Boss 对象。

不配置新动作的旧 Encounter 行为不变。输入锁不提供无敌，一次消弹不阻止道中敌人继续发射。
音乐仍按已有 Boss 出场/击败事件切换，不等待台词结束。
缺服务、多个服务、播放失败或外部取消会终止本组动作并记录 Failure；当前 Encounter 会把失败视为结束继续流程。

验收时检查战前结束前 Boss 不启动首阶段，战后结束前时间轴不释放；
按住 Z 确认最后一句后主炮与子机均保持停火，松开再按恢复；
取消动作、重开关卡和禁用服务均应释放本次控制锁，不能解除其他宿主的锁。
