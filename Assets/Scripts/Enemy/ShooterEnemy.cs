using UnityEngine;
using ShinySTG.EnemyAI;

/// <summary>
/// 敌人行为流播放机:每帧把控制权交给 BehaviorFlowRuntime。
/// 把"行为流"封装成独立 ScriptableObject 资产后,ShooterEnemy 本身只剩一个 Flow 字段,
/// boss prefab 上不再需要挂多个 ShooterEnemy 组件来切换不同的行为流。
///
/// 生命周期说明:
///   - 自毁路径 1:行为流跑完,SelfDestructAction 直接 Destroy(gameObject)。
///   - 自毁路径 2:HP 打空,Enemy 总控订阅 EnemyHealth.OnDeath 后调 Stop() + Destroy(gameObject)。
/// 本组件本身不监听 Health,也不在死亡时自毁 —— 一切交给总控层处理。
/// </summary>
public class ShooterEnemy : MonoBehaviour
{
    [Header("Behavior Flow")]
    [Tooltip("拖入一个 BehaviorFlow 资产(右键 Project → Create → STG → Behavior Flow)。" +
             "运行时自动深拷贝为独立实例,多个敌人共享同一资产也不会互相干扰。")]
    public BehaviorFlow Flow;

    BehaviorFlowRuntime _runtime;

    void OnEnable()
    {
        _runtime = Flow != null ? Flow.Instantiate() : null;
    }

    void Update()
    {
        _runtime?.Tick(transform, Time.deltaTime);
    }

    /// <summary>
    /// 外部(Enemy 总控)在死亡时调用:强制退出当前 action,清空 runtime 引用,不再 tick。
    /// 不会自毁 —— 后续 Destroy 由调用方决定,便于未来加死亡动画 / 撒豆。
    /// </summary>
    public void Stop()
    {
        _runtime?.ForceExit(transform);
        _runtime = null;
    }
}

