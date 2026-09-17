# 道具掉落与拾取

## 职责与数据边界

ItemDefinition 定义道具外观与效果，DropProfile 定义一次掉落的组成和散布。
两者作为只读资产复用，运动、吸附与已拾取状态保存在每个 ItemPickup 实例中。
道具数量表示实体个数，与单个道具的奖励价值分离。

ItemDropService 是场景级生成与对象池服务。敌人死亡入口和 SpawnDropsCommand 只提交配置及位置，
不持有生成后道具的生命周期。道具根对象归属服务所在场景，不继承服务或敌人的 Transform。
服务缺失或禁用时跳过生成并警告；不自动创建跨场景服务。

## 检测与结算

PlayerHitbox 提供受伤、拾取、吸附三个独立范围。ItemDropService 推进撒出、下落和吸附，
CollisionService 使用现有 HitboxMath 检测接触并调用 TryCollect，玩家组件负责保存资源。
单玩家情况下直接遍历活跃道具即可，不向伤害网格插入道具，也不维护第二套网格。

拾取在 CollisionService 自身的子弹伤害之后进行，并再次检查玩家资格；无敌允许拾取，死亡或禁用不允许。
这不约束其他独立服务的执行顺序。出生帧不拾取；TryCollect 在奖励回调前标记已收取，防止重入，
遍历结束后统一回收。吸附进入范围后持续追踪，直到拾取、关闭吸附或玩家不可拾取；离开范围本身不取消追踪。

Power 使用整数百分单位累积，保留原整数等级供射击与子机使用；满 Power 拾取不换分。
分数和 Bomb 由 PlayerResources 保存，残机仍由 PlayerHealth 保存。当前 Bomb 仅增加库存。
完整配置与效果示例见 [操作说明](../../Assets/Scripts/Items/README.md)。

## 生命周期与触发语义

敌人被击杀时提交死亡掉落，自毁和离场不提交。Boss 奖励按实际阶段退出结算，
指令上下文区分正常结束、死亡和停止；重复 Stop 不重复结算，跳过的阶段不补发。
最终阶段必须保持到死亡，才能通过该阶段退出指令发放最终击破奖励。

道具超时或低于服务配置的回收线时回池；该回收线目前独立配置，不读取 BoundsService。
禁用服务清空活跃道具，销毁服务清理池与占位图资源。每次复用重置定义、Transform、显示、
速度、计时、吸附与拾取标记，不把前次运行状态写回资产。

## 扩展入口

调整种类组合或数量只创建、编辑 DropProfile。相同效果更换外观可创建另一份 ItemDefinition。
当前五类效果通过 ItemKind 和 ItemPickup.TryCollect 分派；新增效果需增加类型并接入对应玩家资源接口，
无需改变 CollisionService。新增调用场景优先复用 SpawnDropsCommand，例如在行为流的
ExecuteGlobalCommandsAction 中添加该指令并引用 DropProfile。

## 与其他板块的关系

- [enemy-ai](./arch-enemy-ai.md)：普通敌人总控提供被击杀入口。
- [boss](./arch-boss.md)：阶段退出提供结束原因并执行掉落指令。
- [game-actions](./arch-game-actions.md)：瞬时指令可由演出与行为流复用。
- [player](./arch-player.md)：保存奖励资源并提供拾取、吸附范围。
- [hitbox](./arch-hitbox.md)：CollisionService 统一接触检测，复用 AABB 数学。
