using System;
using SerializeReferenceEditor;
using UnityEngine;
using ShinySTG.Hitbox;

namespace ShinySTG.Laser
{
    /// <summary>
    /// 激光圆周运动修饰器 —— 让激光沿圆周绕一个圆心做匀角速旋转。
    ///
    /// <para>★ 数学原理 ★</para>
    /// <para>
    /// 圆周运动的标准极坐标公式: <c>Position(θ) = Center + R × (cos θ, sin θ)</c>,
    /// 其中 <c>θ(t) = θ₀ + ω × t</c>(<c>ω</c> = AngularSpeed 弧度/秒)。
    /// <b>半径 R 是圆周运动的不变量 —— 一旦 Init 锁定,运行期只累加 θ,无需每帧重设</b>。
    /// 这就是为什么用户"只设角速度、不设半径"的直觉是正确的。
    /// </para>
    ///
    /// <para>★ 两种 Pivot 模式 ★</para>
    /// <list type="bullet">
    ///   <item><b>FixedCenter</b>(默认):圆心 = PresetCenter 或激光 Init 时的位置(<b>世界坐标锁死</b>)。
    ///         owner 移动不影响圆心,激光始终绕同一个世界点转。
    ///         适合:固定场景内圆环武器、场景锚点型弹幕。</item>
    ///   <item><b>FollowOwner</b>:圆心 = owner.currentPosition + OffsetFromOwner(<b>跟随发射者平移</b>)。
    ///         激光相对发射者的初始偏移维持不变,但绕发射者旋转。
    ///         适合:Boss 周围的旋转激光、敌机自机半径弹幕。</item>
    /// </list>
    ///
    /// <para>★ 协作边界 ★</para>
    /// <list type="bullet">
    ///   <item>本 modifier 在 OnTick 里<b>直接重写</b> <see cref="LaserEntity.Position"/> 和 <see cref="LaserEntity.Angle"/>,
    ///         <see cref="LaserEntity.LateUpdate"/> 会在 modifier OnTick 后自动同步 transform 与端点,
    ///         视觉与碰撞段自动跟随圆周运动。</item>
    ///   <item>本 modifier 会<b>清零 laser.Velocity</b>(避免一边圆周一边平移);
    ///         <b>不动 laser.AngularVelocity</b>(本 modifier 直接重写 Angle,不依赖框架累积)。</item>
    ///   <item>本 modifier 写入的 Angle = <b>极坐标相位角</b>(从圆心指向激光的方向)——
    ///         这是「激光从圆心朝外」的直觉,而不是切向(切向需要 +π/2)。
    ///         如果需要切向,后续可加 <c>UseTangentAngle</c> 字段,本类不做。</item>
    ///   <item>owner 被 Destroy 后(<see cref="HitboxComponent"/> 是 MonoBehaviour):FollowOwner 模式
    ///         <b>静默退化</b>到「停止平移、保持最后圆心继续转」,不报错;对齐子弹版 HomingEnemyModifier 的
    ///         <c>_target as UnityEngine.Object == null</c> 防御模式。</item>
    ///   <item>挂多个 Orbit modifier 会互相覆盖 Position/Angle —— <b>约定:只挂一个</b>。
    ///         与 BounceBulletModifier "第一个 true 胜出" 不同(Position 没有 bool 返回机制)。</item>
    /// </list>
    /// </summary>
    [Serializable, SRName("LaserModifier/Orbit")]
    public class LaserOrbitModifier : LaserModifier
    {
        /// <summary>圆心确定模式。</summary>
        public enum PivotMode
        {
            /// <summary>圆心 = PresetCenter(若 NaN 则用激光 Init 时位置)。世界坐标锁死,owner 移动不影响。</summary>
            FixedCenter,

            /// <summary>圆心 = owner.position + OffsetFromOwner。跟随发射者平移,绕发射者旋转。</summary>
            FollowOwner,
        }

        // ─── Inspector 字段 ───

        [Header("Pivot")]
        [Tooltip("圆心确定模式:\n" +
                 "  - FixedCenter(默认):圆心 = PresetCenter 世界坐标(若 NaN 则用激光 Init 时的位置)。锁死后不动。\n" +
                 "  - FollowOwner        :圆心 = owner.currentPosition + OffsetFromOwner。owner 移动时圆心跟着平移。\n" +
                 "典型用例:FixedCenter = 场景内固定圆环武器;FollowOwner = Boss 周围的旋转激光。")]
        public PivotMode Mode = PivotMode.FixedCenter;

        [Tooltip("角速度(度/秒)。STG 常用 30~180 度/秒。\n" +
                 "  - 正值 = 逆时针(atan2 方向,STG 惯例)\n" +
                 "  - 负值 = 顺时针\n" +
                 "  - 0 = 不旋转(等价没挂这个 modifier)\n" +
                 "★ 内部换算成弧度 ★\n" +
                 "为什么 Inspector 用度不用弧度:与项目里所有 AngularSpeed / AimOffsetDeg 等字段单位一致,策划零学习成本。")]
        public float AngularSpeed = 90f;

        [Tooltip("FixedCenter 模式专用 —— 圆心的世界坐标。\n" +
                 "  - 任意分量 NaN(默认值) = 用激光 Init 时的位置作为圆心\n" +
                 "  - (x, y)                   = 圆心锁死为 (x, y) 世界坐标\n" +
                 "注意:跨场景运行时,如果 PresetCenter 是固定数值(非 NaN),场景边界外的坐标可能导致激光" +
                 "飞出视口 —— 这是预期行为,策划自己确保坐标在场景内。")]
        public Vector2 PresetCenter = new Vector2(float.NaN, float.NaN);

        [Tooltip("FollowOwner 模式专用 —— 圆心相对 owner 当前位置的偏移(世界坐标)。\n" +
                 "  - (0, 0)    = 圆心 = owner.position(激光绕 Boss 转,Boss 即圆心)\n" +
                 "  - (1.5, 0)  = 圆心 = owner.position + (1.5, 0)(Boss 右侧 1.5 单位)\n" +
                 "  - (0, 1.0)  = 圆心 = owner.position + (0, 1.0)(Boss 上方 1 单位)\n" +
                 "典型用法:OffsetFromOwner = (radius, 0) + AngularSpeed = 360/period 即可做「绕 Boss 转一圈」" +
                 "的标准圆周弹幕,period = 360° / AngularSpeed 度/秒。")]
        public Vector2 OffsetFromOwner = Vector2.zero;

        // ─── per-instance 运行时状态 ───
        // [NonSerialized]:不参与 Inspector 序列化;Clone 时自动重置(每颗激光独立)。
        // ★ 故意不复用 laser.Timer:见 BulletModifier.cs 注释里「自我修改自己计时器」的鸡生蛋问题。
        [NonSerialized] Vector2  _center;           // 当前帧圆心(世界坐标)
        [NonSerialized] float    _radius;           // Init 时锁定的半径(运行期不变)
        [NonSerialized] float    _phaseAngle;       // 当前极坐标相位角(弧度,从 Init 起累加)
        [NonSerialized] bool     _initialized;      // lazy Init 标志:首帧 OnTick 触发初始化
        [NonSerialized] Transform _ownerTransform;  // FollowOwner 模式缓存 owner Transform(每帧直接读 .position)

        // ────────────────────────────────────────────────────────────
        //  Init:首帧 OnTick 自动调用,锁定圆心 / 半径 / 初始相位角
        // ────────────────────────────────────────────────────────────
        void LazyInit(LaserEntity laser)
        {
            // ── 1. 锁定圆心 ──
            if (Mode == PivotMode.FollowOwner)
            {
                // 拿 owner Transform。优先用 Hitbox.transform(GameObject 死了 Transform 必死的隐式约束,
                //   用 Hitbox 比 owner GameObject 更明确"敌人已销毁"语义);
                //   拿不到时 fallback 到 FixedCenter 语义(用 Init 位置)。
                if (laser.OwnerHitbox != null)
                {
                    _ownerTransform = laser.OwnerHitbox.transform;
                    _center = (Vector2)_ownerTransform.position + OffsetFromOwner;
                }
                else
                {
                    // 没有任何 owner(FireLaserAction 测试模式 / 中性阵营激光)
                    // → FollowOwner 没有意义,降级为 FixedCenter 用激光 Init 位置
                    _center = laser.Position;
                }
            }
            else // FixedCenter
            {
                // PresetCenter 含 NaN = 用激光 Init 时位置;否则用 Inspector 配的固定坐标
                _center = float.IsNaN(PresetCenter.x) || float.IsNaN(PresetCenter.y)
                    ? laser.Position
                    : PresetCenter;
            }

            // ── 2. 锁定半径 = 激光 Init 时位置到圆心的距离 ──
            //   ★ 不暴露半径给策划 ★:半径是圆周运动的不变量,Init 一次就锁死,策划无需关心。
            //   若想精确控制半径,可改用 PresetCenter + 把激光从精确半径处发射(让 Position = Center + R*(cos, sin))。
            _radius = Vector2.Distance(_center, laser.Position);

            // ── 3. 计算初始相位角 ──
            //   激光当前所在位置相对圆心的极坐标角,作为累加起点
            //   atan2(y - cy, x - cx) → 返回 [-π, π] 弧度
            //   ★ 用激光当前 Position(已被 Velocity / AngularVelocity 之前的 LateUpdate step 1 更新过),
            //     但本 modifier 在 OnTick 阶段才跑,所以激光 Position 是 Init 时的位置 —— 正确。★
            Vector2 toLaser = laser.Position - _center;
            _phaseAngle = Mathf.Atan2(toLaser.y, toLaser.x);

            // ── 4. 清零 Velocity(避免一边圆周一边平移) ──
            //   不动 AngularVelocity:本 modifier 直接重写 Angle,不依赖框架累积角速度。
            laser.Velocity = Vector2.zero;
        }

        // ────────────────────────────────────────────────────────────
        //  OnTick:每帧调用(LaserEntity.LateUpdate step 2)
        // ────────────────────────────────────────────────────────────
        public override void OnTick(LaserEntity laser, float dt)
        {
            // 1. lazy Init —— LaserModifier PR1 没有显式 Init 钩子(对齐 BulletModifier 的设计:
            //   用 ResetWindow 触发,这里简化为首帧 OnTick 触发,池复用时 ResetWindow 清 _initialized)
            if (!_initialized)
            {
                LazyInit(laser);
                _initialized = true;
            }

            // 2. FollowOwner 模式:每帧重算圆心(若 owner 还活着)
            //   ★ 防御:owner GameObject 被 Destroy 后,Transform 引用变"伪 null",直接读 .position 会抛 MissingReferenceException
            //     必须先 cast 到 UnityEngine.Object 触发 Unity 的"伪 null"重载识别 —— 对齐子弹版 HomingEnemyModifier.IsTargetValid
            if (Mode == PivotMode.FollowOwner && _ownerTransform != null)
            {
                // ★ 关键防御:UnityEngine.Object 引用需走"伪 null"检测
                if (_ownerTransform as UnityEngine.Object == null)
                {
                    // owner 已死 → 退化为"固定最后圆心继续转",不再跟随
                    _ownerTransform = null;
                    // 不 return:让圆周继续(角度继续累加),激光自然走完生命周期
                }
                else
                {
                    _center = (Vector2)_ownerTransform.position + OffsetFromOwner;
                }
            }

            // 3. 累加相位角(每帧推进)
            //   ★ AngularSpeed 单位是度/秒,内部换算成弧度
            _phaseAngle += AngularSpeed * Mathf.Deg2Rad * dt;

            // 4. 重算 Position —— 极坐标公式
            //   Position = Center + R * (cos θ, sin θ)
            //   ★ laser.Position 是 internal set(同 Assembly-CSharp 可见,见 LaserEntity.cs 字段注释),可写。
            //   因为 LaserEntity.LateUpdate 会在 modifier OnTick 末尾再同步一次 transform,
            //   这里写完 Position 后,视觉立刻生效,不需要我们手动改 transform。
            Vector2 newPos = _center + new Vector2(
                Mathf.Cos(_phaseAngle) * _radius,
                Mathf.Sin(_phaseAngle) * _radius);
            laser.Position = newPos;

            // 5. 重算 Angle —— 让激光「端点」沿圆周切向之外的方向(这里设为极坐标相位角,即激光从圆心朝外的方向)
            //   选「极坐标相位角」而非「切向」:符合策划「激光从圆心射出来」的直觉;
            //   若策划要切向(激光沿圆弧走),加一个 UseTangentAngle 字段切换即可,本类不做。
            //   ★ Angle 必须随 Position 重算,否则 Renderer 仍按原方向画,视觉错位 90°。
            laser.Angle = _phaseAngle;
        }

        // ────────────────────────────────────────────────────────────
        //  ResetWindow:池复用时调用,重置 lazy Init 标志
        // ────────────────────────────────────────────────────────────
        public override void ResetWindow()
        {
            // ★ 池复用时必须重置,否则从池里取出来的激光会用上一发的圆心/半径/相位 —— 状态污染!
            _initialized = false;
            _center = default;
            _radius = 0f;
            _phaseAngle = 0f;
            _ownerTransform = null;
        }

        // ────────────────────────────────────────────────────────────
        //  OnDetach:激光回池时调用,本 modifier 没有需要释放的资源(无订阅 / 无计时器),默认无操作即可
        // ────────────────────────────────────────────────────────────
        // public override void OnDetach(LaserEntity laser) { } // 用基类默认空实现

        // ────────────────────────────────────────────────────────────
        //  Clone:每颗激光独立 instance —— 默认 MemberwiseClone 已够用(字段全是值类型 + Vector2)
        // ────────────────────────────────────────────────────────────
        // public override LaserModifier Clone() => (LaserModifier)MemberwiseClone(); // 用基类默认
    }
}
