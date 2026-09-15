using UnityEngine;

namespace ShinySTG.Laser
{
    /// <summary>
    /// 激光视觉抽象基类。与碰撞完全解耦(参考材料 §12):
    ///   - 本组件只负责把 LaserEntity 的几何状态画到屏幕上。
    ///   - 改变 Renderer 实现(Sprite 拉伸 / LineRenderer / Mesh) 不影响 LaserService 的碰撞判定。
    ///   - 子类挂在 prefab 上(与 SpriteRenderer / LineRenderer 同性质),由 LaserEntity 在 Awake 时 GetComponent 抓取。
    ///
    /// 内置实现:
    ///   - <see cref="SpriteStretchLaserRenderer"/>:用 Body Sprite 按 VisualWidth + CurrentLength 拉伸;
    ///     用 Head Sprite 在激光起点固定不动(头);
    ///     预警阶段单独画一条细线。
    /// 扩展:新建子类 + override OnLaserInit / OnLaserTick 即可,无需改 LaserEntity。
    /// </summary>
    public abstract class LaserRendererBase : MonoBehaviour
    {
        /// <summary>
        /// 激光 Init 时调用一次。子类抓 Renderer 引用、记录 baseLocalScale 等。
        /// </summary>
        public abstract void OnLaserInit(LaserEntity laser);

        /// <summary>
        /// 每帧 LateUpdate 末尾调用。子类根据 laser.Position / Angle / CurrentLength / State 刷新视觉。
        /// </summary>
        public abstract void OnLaserTick(LaserEntity laser);
    }
}
