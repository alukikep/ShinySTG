using SerializeReferenceEditor;
using UnityEngine;
using ShinySTG.Hitbox;

namespace ShinySTG.Laser
{
    /// <summary>
    /// 激光 Pattern 基类。对齐 Bullet.FirePattern 的设计思路(SO 数据驱动 + 子类描述怎么射),
    /// 由 FireLaserAction 通过 LaserPool 触发。
    ///
    /// 关键边界:LaserPattern 不继承 FirePattern(基类持 Speed/Angular/BulletPrefab,
    /// 这些字段对激光无意义)。它走独立的 LaserPool 入口,但保留 SR 多态扩展位
    /// (ModifierPrefabs / FireSounds)与 FirePattern 完全对仗。
    /// </summary>
    public abstract class LaserPattern : ScriptableObject
    {
        [Header("Laser Data")]
        [Tooltip("决定视觉/判定宽度/生命周期/贴图等参数。" +
                 "右键 Project → Create → STG → Laser/Laser Data 创建。")]
        public LaserData Data;

        [Header("Modifiers (激光生成后自动挂载)")]
        [Tooltip("下拉选 LaserModifier 子类(纯 C# 修饰,持续旋转 / 跟随 Boss 等)。\n" +
                 "运行时 Clone 独立实例,不跨激光污染。\n" +
                 "PR1 阶段:留空即可,激光按 Data 默认参数运行。")]
        [SerializeReference, SR]
        public LaserModifier[] ModifierPrefabs;

        [Header("Fire Sounds")]
        [Tooltip("开火音(可叠多个 SFX cue)。与 FirePattern.FireSounds 等价。\n" +
                 "运行时由 FireLaserAction → LaserPool.FireGroup → pattern.Fire 内部触发。")]
        [SerializeReference, SR]
        public FireSound[] FireSounds;

        [Tooltip("本次发射的激光总伤害(命中玩家时一次性扣血)。经典 STG 推荐 1。")]
        public float Damage = 1f;

        /// <summary>
        /// 由 LaserPool.FireGroup 调用,生成一条激光并加入活跃集合。
        /// 子类负责在内部调 LaserPool.Get / GetCurve 并挂 modifier / 播 FireSounds。
        /// </summary>
        /// <param name="position">激光起点(世界坐标)</param>
        /// <param name="angleRad">激光方向(弧度,0=向右,π/2=向上)</param>
        /// <param name="pool">LaserPool(子类从池里 Get 新激光)</param>
        /// <param name="ownerHitbox">发射者 Hitbox(可为 null)。null 时阵营 = Neutral(不参与碰撞)。</param>
        /// <param name="extraModifiers">调用方(FireLaserAction)临时追加的 modifier,在 ModifierPrefabs 之后追加。null = 不追加。</param>
        public abstract LaserEntity Fire(Vector2 position, float angleRad,
                                         LaserPool pool,
                                         HitboxComponent ownerHitbox,
                                         LaserModifier[] extraModifiers = null);

        /// <summary>与 FirePattern.GetFireCount() 对仗,供 Boss 系统统计 ShotsFired 用。默认 1。</summary>
        public virtual int GetFireCount() => 1;
    }
}
