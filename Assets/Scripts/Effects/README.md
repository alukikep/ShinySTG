# 战斗特效

## 临时测试特效

将以下任意 prefab 拖到 Enemy 的 Death Effect Prefab 即可测试：

- [DebugDeath_Explosion](../../Resources/Effects/DebugDeath_Explosion.prefab)：橙色爆炸，白色闪光、圆环和 12 道火花。
- [DebugDeath_Ring](../../Resources/Effects/DebugDeath_Ring.prefab)：青色扩散光环。
- [DebugDeath_Sparks](../../Resources/Effects/DebugDeath_Sparks.prefab)：粉色放射碎片火花。

它们使用 DebugDeathEffect 在首次播放时创建 LineRenderer，并随特效池复用，
不需要贴图或额外插件。通过现有特效池播放，直接拖进场景不会自动播放。
可在 prefab 上调整颜色、持续时间、半径及排序层；若被背景挡住，将 Sorting Layer
设为项目中敌人使用的排序层，并适当提高 Sorting Order。
这些是程序绘制的调试表现，Prefab 静态预览为空，实际形状在运行时生成。

## 敌人死亡

在普通敌人 prefab 的 Enemy 组件上，将一次性特效 prefab 拖到 Death Effect Prefab。
留空不播放。只有击杀触发，出界、行为自毁与清场不会播放。
死亡特效使用世界位置独立播放，敌人仍立即走原有销毁流程。

粒子 prefab 可直接使用，池会自动补上 PooledEffect。建议关闭 Loop；
需要调整最长播放时间时，预先在 prefab 根节点挂 PooledEffect。
所有子粒子结束后回收，最长播放时间兜底处理循环或没有粒子的特效。
不要挂自动 Destroy/Disable 脚本；ParticleSystem 的 Stop Action 由池接管。
池保证粒子的停止和清空；自定义动画脚本的可变状态需自行实现复用重置。

## 玩家弹命中残影

普通敌人和 Boss 命中共用 Bullet.PlayHitAfterimage，在伤害回调之前复制外观。
通用资源是 [BulletAfterimage.prefab](../../Resources/Effects/BulletAfterimage.prefab)，
Sprite 在命中时动态填充，因此在 Prefab 预览里为空是正常的。
现有玩家弹默认开启，无需修改场景；在 Bullet 上关闭 Show Hit Afterimage 可按弹种禁用，
也可将自定义的 BulletAfterimage prefab 拖到 Hit Afterimage Prefab。

在残影 prefab 上调整时长、移动距离、移动曲线和透明度曲线。
残影只复制 Bullet.Renderer 指向的单个 SpriteRenderer，包含材质和 MaterialPropertyBlock，
不包含子弹行为、碰撞和 Modifier。多层视觉、TrailRenderer、激光需要专门的表现。
STG/BulletTint 通过独立的 _EffectOpacity 淡出，默认值 1 不改变正常子弹；
其他材质使用 SpriteRenderer.color 的 alpha，需要 shader 支持该透明度。
使用旋转与非均匀缩放组合产生剪切的父层级时，独立 Sprite 无法精确复现剪切。

## 特效附带音效

在 prefab 的 PooledEffect、BulletAfterimage 或 DebugDeathEffect 组件上，将 SfxCue
拖到 Play Sfx。纯粒子 prefab 需先在根节点添加 PooledEffect，才能保存该配置。
留空不播放，不需要另挂 AudioSource。场景须有已初始化的 AudioSystem；缺失时静默跳过。
SfxCue 的创建与配置见 [音频说明](../Audio/README.md)。

每次从池取出正式播放时提交一次音效，使用播放起点的世界位置，不跟随特效移动。
禁用或回收特效不会截断声音；音频系统的暂停、清场与声部抢占规则仍然生效。
请选择关闭 Loop 的短音效，特效不会替循环声音安排停止。
密集命中建议配置 SfxCue 的 Cooldown、Max Voices 和 Overflow；若希望保留已有尾音，
可用 Drop Newest。复用后会重新请求播放，是否接受仍由音频限流规则决定。

Health 上已有的受击、死亡音效不会自动修改。同一种声音迁移到特效 prefab 后，
应清空 EnemyHealth 或 BossHealth 对应的音效引用，避免重复播放。
敌人独有叫声和特效爆炸声可以分别保留。关闭子弹残影也会一起关闭其附带命中音效。

## 生命周期

EffectPool 按所属场景和 prefab 分池，首次播放自动创建，无需场景挂载。
每个 prefab 最多缓存 64 个闲置实例，超出的回收时销毁。
特效不挂在敌人或子弹下面；场景卸载时池和所有实例一同销毁。
粒子与残影使用游戏时间，暂停时同步暂停。Release 可提前回收，重复调用无副作用。
