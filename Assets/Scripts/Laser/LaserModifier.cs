using System;
using UnityEngine;

namespace ShinySTG.Laser
{
    /// <summary>
    /// 激光行为的可插拔修饰(持续旋转 / 跟随 Boss 移动 / 长度波形变化 / ...)。
    /// 纯 C# 类(非 MonoBehaviour、非 ScriptableObject),与 BulletModifier 对仗。
    ///
    /// PR1 阶段:本类只提供最小钩子(OnTick / OnDetach / ResetWindow / Clone),
    /// 让 LaserEntity 编译过 + 修饰器管线跑通。完整的 Delay / Duration / StartTrigger
    /// (对位 BulletModifier 双时钟)留到 PR3,届时把 BulletModifier 的同款模板搬过来,
    /// 并补 LaserRotateModifier / LaserFollowTargetModifier 等实例。
    ///
    /// 字段约定(对齐 BulletModifier):
    ///   - 鼓励值类型(float/int/Vector2/...)—— 默认 Clone 即可,无额外开销
    ///   - 禁止 List&lt;T&gt; / T[] / 自定义类 —— 必须 override Clone 深拷
    ///   - 不要访问 this.transform —— modifier 不是 GameObject
    ///   - 访问激光数据请通过 <see cref="LaserEntity"/> 的公开字段(Position / Angle / Velocity ...)
    /// </summary>
    [Serializable]
    public abstract class LaserModifier
    {
        /// <summary>
        /// 每帧 LateUpdate 中 LaserEntity 状态机推进之前调用。
        /// 子类可在此修改 Position / Angle / Velocity / AngularVelocity / LengthEase 等。
        /// </summary>
        public abstract void OnTick(LaserEntity laser, float dt);

        /// <summary>
        /// 激光被回池(LaserEntity.OnDisable / LaserPool.Return)时调用。
        /// 子类清理订阅 / 计时器等 per-instance 资源。默认无操作。
        /// </summary>
        public virtual void OnDetach(LaserEntity laser) { }

        /// <summary>
        /// 重置时间窗口(对齐 BulletModifier.ResetWindow)。子类可重置自己的 _timer 等。
        /// 默认无操作 —— PR1 阶段没有时间窗口概念。
        /// </summary>
        public virtual void ResetWindow() { }

        /// <summary>
        /// 默认 Clone = MemberwiseClone。子类若含引用类型字段,需 override 深拷。
        /// (对齐 BulletModifier.Clone 注释)
        /// </summary>
        public virtual LaserModifier Clone() => (LaserModifier)MemberwiseClone();
    }
}
