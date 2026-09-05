using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 一段可复用的"行为流"资产。包含完整的 Actions 时间轴 + Loop + StartDelay。
    /// 可被 ShooterEnemy / ShooterPhase 等引用。运行时自动深拷贝为独立实例,
    /// 多个敌人/boss 共享同一个 SO 资产不会互相干扰。
    ///
    /// 工作流:
    ///   1. 右键 Project → Create → STG → Behavior Flow 创建资产
    ///   2. 在资产上配 Actions / Loop / StartDelay
    ///   3. 把资产拖到 ShooterEnemy.Flow 或 ShooterPhase.Flow 字段
    /// </summary>
    [CreateAssetMenu(menuName = "STG/Behavior Flow")]
    public class BehaviorFlow : ScriptableObject
    {
        [Header("Timeline")]
        [Tooltip("行为序列。每条持续 Duration 秒后自动切换。")]
        public EnemyAction[] Actions;

        [Header("Loop & Delay")]
        [Tooltip("序列执行完毕后是否从头循环。")]
        public bool  Loop = false;

        [Tooltip("启动时延迟多少秒再开始执行。")]
        public float StartDelay = 0f;

        /// <summary>
        /// 创建一份独立的运行时实例。
        /// Unity 的 Object.Instantiate(SO) 会自动深拷贝 [SerializeReference] 字段,
        /// 包括 Actions 数组里的每个 EnemyAction 对象。
        /// </summary>
        public BehaviorFlowRuntime Instantiate()
        {
            var copy = UnityEngine.Object.Instantiate(this);
            copy.name = name + " (Runtime)"; // 便于在 Inspector 调试时辨认
            return new BehaviorFlowRuntime(copy);
        }
    }
}
