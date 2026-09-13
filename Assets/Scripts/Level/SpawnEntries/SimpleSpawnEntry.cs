using System;
using SerializeReferenceEditor;
using ShinySTG.EnemyAI;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries
{
    /// <summary>
    /// 最常用的关卡条目:在指定时间点于指定位置生成一个敌人 prefab。
    /// 满足题目要求"设置敌人出现时间位置以及具体的敌人单位"。
    ///
    /// 复用约定:
    ///   - prefab 必须挂 ShooterEnemy(且 Flow 字段已配),由 ShooterEnemy.OnEnable 自动驱动行为流。
    ///   - Hitbox / Health / Enemy 总控在 prefab 上配好即可,本类不做任何额外装配。
    ///   - 若 OverrideFlow 非空,会临时覆盖 prefab 上 ShooterEnemy.Flow(优先级 OverrideFlow > prefab)。
    ///     —— 实现方式:覆盖前 prefab 自带的 Flow 已经被 ShooterEnemy.OnEnable 读走,
    ///     所以必须先覆盖再让 ShooterEnemy 启用。简单做法:Instantiate 后立刻 SetActive(false),
    ///     覆盖 Flow 后再 SetActive(true)。详见 OnTrigger。
    /// </summary>
    [Serializable, SRName("敌人生成/Simple")]
    public class SimpleSpawnEntry : SpawnEntry
    {
        [Tooltip("敌人 prefab。要求挂 ShooterEnemy(+ Health + Hitbox + Enemy 总控);Flow 字段可空,由 OverrideFlow 覆盖。")]
        public GameObject EnemyPrefab;

        [Tooltip("触发时的生成位置(世界坐标)。")]
        public Vector2 SpawnPosition = Vector2.zero;

        [Tooltip("初始朝向(度,顺时针)。0=右,90=下,180=左,270=上。STG 通常 270(屏幕上方为正 Y)。\n" +
                 "实际效果取决于 prefab 的 sprite 朝向 + 行为流的 MoveBehaviour 约定,本字段给一个默认值兜底。")]
        public float InitialRotation = 0f;

        [Tooltip("可选:覆盖 prefab 上 ShooterEnemy.Flow 的行为流资产。null = 用 prefab 自己配的。")]
        public BehaviorFlow OverrideFlow;

        public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
        {
            if (EnemyPrefab == null)
            {
                Debug.LogWarning("[Level] SimpleSpawnEntry 缺少 EnemyPrefab,已跳过。", def);
                return;
            }

            // 1. 先 Instantiate 但禁用,避免 ShooterEnemy.OnEnable 立即读走 prefab 自带的 Flow。
            var go = UnityEngine.Object.Instantiate(EnemyPrefab);
            go.SetActive(false);
            go.transform.position   = SpawnPosition;
            go.transform.rotation   = Quaternion.Euler(0f, 0f, InitialRotation);

            // 2. 覆盖 Flow(若有 OverrideFlow)
            if (OverrideFlow != null)
            {
                var shooter = go.GetComponent<ShooterEnemy>();
                if (shooter != null) shooter.Flow = OverrideFlow;
            }

            // 3. 启用,ShooterEnemy.OnEnable 自动 Flow.Instantiate() 跑起来
            go.SetActive(true);

            // 4. 登记活跃单位(便于后续清理 / 统计 / 失败判定)
            runtime.Track(go);
        }
    }
}