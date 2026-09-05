using UnityEngine;
using ShinySTG.EnemyAI;

/// <summary>
/// 敌人行为流播放机:每帧把控制权交给 BehaviorFlowRuntime。
/// 把"行为流"封装成独立 ScriptableObject 资产后,ShooterEnemy 本身只剩一个 Flow 字段,
/// boss prefab 上不再需要挂多个 ShooterEnemy 组件来切换不同的行为流。
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
}

