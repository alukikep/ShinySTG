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
                 "运行时由 FireLaserAction → LaserPool.FireGroup 入口触发(对齐 BulletPool.FireGroup 中心化触发)。")]
        [SerializeReference, SR]
        public FireSound[] FireSounds;

        [Header("Fire Extensions (Pipeline 模块数组,按顺序串成角度管道)")]
        [Tooltip("基础发射逻辑的模块数组 —— 每个模块是 LaserFireExtension 子类实例,按数组顺序串成\"角度管道\":\n" +
                 "  - 空数组 / null = 默认模式:激光方向 = 270° + rotationRad(等价旧版 default)\n" +
                 "  - LaserFireExtension/Base:覆盖型,把角度设为 BaseAngle + rotationRad(通常放数组第一位作\"锚点\")\n" +
                 "  - LaserFireExtension/Player Aim:覆盖型 —— 瞄得到玩家 → 角度设为指向玩家;瞄不到 → 透传上游角度\n" +
                 "  - LaserFireExtension/Offset Angle:累加型 —— 在上一步角度上叠加 N°(逆时针为正);常配合 PlayerAim 实现 \"绕后激光\"\n" +
                 "  - LaserFireExtension/Offset Angle Accumulating:批次累加型 —— 每次开火后 StepOffset 累加,BaseOffset 走 SR 多态\n" +
                 "\n" +
                 "★ 拼装示例 ★\n" +
                 "  [Base(270°)]                              → 始终向下\n" +
                 "  [Base(0°), PlayerAim]                     → 瞄不到玩家时向右,瞄得到时改指向玩家\n" +
                 "  [PlayerAim, Offset Angle(+180°)]          → 玩家方向的反方向(瞄准玩家但激光从玩家背后射出 —— \"绕后激光\")\n" +
                 "  [Base(0°), Accumulating Offset(0°, 5°)]   → 每次开火旋转 5° 的旋转激光\n" +
                 "\n" +
                 "★ 位置偏移(PositionOffset)★\n" +
                 "  在 Base 模块上配 PositionOffset=(x, y) 即可让激光从\"标准位置偏移 (x, y)\"射出\n" +
                 "  —— Resolver 在 pipeline 入口处应用,作用于所有后续模块(包括瞄准参考点)。\n" +
                 "\n" +
                 "扩展方法:新建 LaserFireExtension 子类 + 加 [SRName(\"LaserFireExtension/<名字>\")] —— 自动出现在所有 LaserPattern 资产的下拉菜单。\n" +
                 "Pipeline 细节:见 Assets/Scripts/Laser/FireExtension/LaserFireExtension.cs 顶部注释。\n" +
                 "对齐 Bullet.FireExtensions 架构,设计师切换零学习成本。")]
        [SerializeReference, SR]
        public LaserFireExtension[] FireExtensions;

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

        /// <summary>
        /// 触发 FireSounds 数组里的所有开火音模块。
        /// 由 <see cref="LaserPool.FireGroup"/> 在调 pattern.Fire(...) 之前调用一次(对齐 BulletPool.FireGroup 入口触发)。
        ///
        /// 子类可 override 此方法做更复杂行为(例如 Composite 改成"遍历所有子 pattern 各触发一次"),
        /// 默认实现 = 按数组顺序串行触发每个模块。留空数组 / null 跳过(零开销)。
        ///
        /// 注意:此方法与 pattern.Fire(...) 解耦 —— 即使某次 Fire() 因为 Data 缺失等条件没真的发射激光,
        /// 已经调过本方法播过音。这是预期行为(开火意图已发生,与激光是否成功生成无关)。
        /// </summary>
        public virtual void PlayFireSounds(Vector2 position, ShinySTG.Hitbox.HitboxComponent ownerHitbox)
        {
            if (FireSounds == null) return;
            for (int i = 0; i < FireSounds.Length; i++)
            {
                var s = FireSounds[i];
                if (s != null) s.OnFireTriggered(position, ownerHitbox);
            }
        }

        /// <summary>
        /// 挂 modifier(ModifierPrefabs + extraModifiers 拼接),并重置所有 modifier 的窗口。
        /// 子类 <see cref="StraightLaserPattern"/> / <see cref="BidirectionalStraightLaserPattern"/> 共用。
        ///
        /// ★ 与 BulletPool.AttachModifiers 同思路,但因 LaserEntity modifier 体系 PR1 只暴露最小钩子
        ///   (完整 Delay/Duration 体系留 PR3),所以这里也只调 AddModifier + ResetAllModifierWindows。
        /// </summary>
        protected static void AttachModifiers(LaserEntity laser, LaserModifier[] prefabs, LaserModifier[] extras)
        {
            if (laser == null) return;
            if (prefabs != null)
            {
                for (int i = 0; i < prefabs.Length; i++)
                {
                    var m = prefabs[i];
                    if (m != null) laser.AddModifier(m.Clone());
                }
            }
            if (extras != null)
            {
                for (int i = 0; i < extras.Length; i++)
                {
                    var m = extras[i];
                    if (m != null) laser.AddModifier(m.Clone());
                }
            }
            laser.ResetAllModifierWindows();
        }
    }
}
