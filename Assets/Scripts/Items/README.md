# 道具 MVP

架构职责、生命周期与扩展入口见 [道具架构](../../../docs/architecture/arch-items.md)。

## 最小配置

1. 执行 **Tools → STG → Items → Create MVP Definitions**，创建五种道具与示例掉落资产。
   每次使用独立目录，不覆盖已有配置；此资产创建操作不加入场景 Undo。
2. 执行 **Add Scene Service**，在场景添加 `ItemDropService`（支持 Undo）。场景还需要已有的
   `CollisionService` 和玩家；拾取不依赖 `BulletPool`。服务的 Transform 不影响道具运动。
3. 普通敌人的 `Enemy` 组件填写 **Death Drops**；Boss 的阶段 **Exit Commands** 添加
   **Command/Spawn Drops** 并指定 Profile。
4. 在 `DropProfile` 的 Entries 中逐项选择道具、数量和掉落概率，调整撒出方向与速度。
5. 在 `PlayerHitbox` 的 Items 分组配置拾取范围，以及 `Fast Attraction Size`（高速）和
   `Slow Attraction Size`（低速 / Focus）吸附范围，两个分量分别表示宽和高。
   选中玩家时，青色框表示拾取；编辑模式下黄色框表示高速吸附、紫色框表示低速吸附，
   运行时黄色框显示当前模式的吸附范围。范围相同时两个编辑框重合。

旧吸附范围自动保留为高速配置；低速范围需要独立调整。按住 Focus 使用低速范围，
松开使用高速范围，与玩家是否正在移动无关。已开始吸附的道具不会因切换模式而停止追踪。

道具定义未配置 Sprite 时使用彩色方块占位；小 P 红、大 P 浅红且较大、点数蓝、Bomb 绿、1UP 紫。
可直接替换 Sprite，不需要道具 prefab。道具定义与掉落配置在运行期间作为只读模板。

每个 `Entry` 独立进行一次掉落概率抽样；命中后生成该条目的全部 `Count` 个道具。
概率为 1 时必定掉落，旧资产新增字段默认值为 1，因此保持原有行为。

## 效果与触发规则

小 P 增加 0.01 Power，大 P 增加 1.00；内部以百分单位保存，射击和子机继续读取向下取整的
`PowerLevel`。沿用旧 InitialPower/MaxPower 配置。每个 `ItemDefinition` 的 `ScoreValue` 都在成功拾取
后生效，适用于小 P、大 P、点数、Bomb 和 1UP；设为 0 表示不计分。满火力仍收取 Power，道具分数仍照常发放。
Bomb 和 1UP 分别增加一个库存和残机。Bomb 不包含释放逻辑。

`PlayerResources` 保存分数和 Bomb 库存；旧玩家在 Awake 自动补齐组件。需要配置初始 Bomb 时，
可事先手动添加该组件。已有 `GrazeCountDisplay` 可显示 Power 小数、分数和 Bomb。

阶段撒道具指令默认允许正常结束和 Boss 死亡，两项可单独关闭。手动 Stop 不掉落。
普通敌人自毁/离场不掉落；Boss 仅结算实际退出的阶段，不为跳过阶段补发奖励。
阶段已经正常退出后，在过渡期间死亡不会再执行同一阶段奖励。
没有进入任何阶段时死亡、所有阶段退出后再死亡，均没有当前阶段奖励；最终阶段应持续到死亡。
同一指令放在行为流或阶段进入命令中时是显式调用，不受阶段退出开关限制。

## 运行边界

道具服务负责运动、吸附与对象池；`CollisionService` 在自身子弹伤害结算之后检测玩家独立的拾取 AABB，
调用 `TryCollect`，遍历结束后统一回收。出生当帧不拾取。无敌可拾取，死亡或禁用玩家不可拾取。
吸附启动后持续追踪；关闭吸附或玩家不可拾取时恢复下落。吸附使用 MoveTowards 防止越过玩家。
玩家受伤框仍是原有 Size；道具不会进入伤害阵营或污染伤害网格。
此顺序不约束独立 LaserService；对话控制锁也不会自动暂停道具或禁止拾取。

道具超时或低于服务的 DespawnBelowY 时回收；禁用服务清空活跃道具，销毁服务同时清理对象池。
服务缺失时记录警告并跳过掉落。对象池复用重置定义、位置、缩放、颜色、速度、计时和拾取/吸附状态。

## 验证

进入 Play Mode，选中 DropProfile 后执行 **Spawn Selected Profile (Play Mode)**，在玩家上方生成。
示例资产含小 P ×10，其他各 ×1；未封顶时收齐增加 1.10 Power、各道具配置的分数、1 Bomb、1 残机。

- 小 P 累计 100 个增加精确 1.00 Power，仅跨整数时变更射击等级。
- 调整拾取/吸附范围，不改变受伤范围；吸附关闭后仍可直接拾取。
- 将低速吸附范围设得比高速大，在两者之间放置道具；高速时不吸附，按住 Focus 后开始吸附，
  松开后继续追踪。静止时切换模式也应生效，运行时黄色框应随之变化。
- 击杀敌人只撒一次，自毁不撒；Boss 正常退出和最终死亡各按开关执行一次。
- 同帧最后一命耗尽时不能拾取 1UP；无敌玩家可正常拾取。
- 重复生成/拾取、禁用重启服务、切换场景，检查回收和状态重置。
- 在没有 BulletPool 的场景也能拾取。

相关实现：[掉落配置](./DropProfile.cs)、[道具服务](./ItemDropService.cs)、
[拾取检测](../Hitbox/CollisionService.cs)、[撒道具指令](../GameplayCommands/SpawnDropsCommand.cs)。
