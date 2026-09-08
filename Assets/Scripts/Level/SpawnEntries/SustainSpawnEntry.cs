using System;
using SerializeReferenceEditor;
using ShinySTG.EnemyAI;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries
{
    /// <summary>
    /// 在指定点位 + 起止时间内,持续周期性生成敌人的条目。
    ///
    /// 用法:
    ///   - TriggerTime = 开始时间(秒)
    ///   - Duration = 持续时间(秒)。例如 2.0 表示从 1s 持续到 3s,期间每 SpawnInterval 生成一只
    ///   - SpawnInterval = 两次生成之间的间隔(秒)。<=0 只生成一次
    ///   - EnemyPrefab / SpawnPosition / InitialRotation / OverrideFlow 同 SimpleSpawnEntry
    ///
    /// 编辑器表现:
    ///   - 时间轴 block 宽度 = Duration × 像素/秒
    ///   - block 视觉:左 8px 实色(标识起点) + 主体半透明(GetColor 同色 + alpha 减半)
    ///
    /// 实现说明:
    ///   - Duration 在基类 SpawnEntry 上;本类只 override OnTick
    ///   - 触发时 OnTrigger 调 RegisterSustained 注册自己,然后 LevelRuntime 每帧调 OnTick
    ///   - 到 Duration:LevelRuntime 调 OnEnd
    /// </summary>
    [Serializable, SRName("Entry/Sustain")]
    public class SustainSpawnEntry : SpawnEntry
    {
        [Tooltip("每 SpawnInterval 秒生成一只 EnemyPrefab 的 prefab。\n" +
                 "<=0 只在 TriggerTime 生成一次(退化成 Simple)。")]
        public float SpawnInterval = 0.5f;

        [Tooltip("敌人 prefab。要求挂 ShooterEnemy(+ Health + Hitbox + Enemy 总控)。")]
        public GameObject EnemyPrefab;

        [Tooltip("生成位置(世界坐标)。")]
        public Vector2 SpawnPosition = Vector2.zero;

        [Tooltip("初始朝向(度)。")]
        public float InitialRotation = 0f;

        [Tooltip("可选:覆盖 prefab 上 ShooterEnemy.Flow 的行为流资产。null = 用 prefab 自己配的。")]
        public BehaviorFlow OverrideFlow;

        float _intervalTimer;
        bool _running;

        public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
        {
            _running = true;
            _intervalTimer = 0f;

            // 持续型条目必须注册到 runtime,LevelRuntime 才会每帧调 OnTick
            // (基类 Tick 也会兜底自动注册,但显式调用语义更清楚)
            runtime.RegisterSustained(this);

            // 立刻生成第一只(不要等第一个 Interval)
            SpawnOne(runtime);
        }

        public override void OnTick(LevelRuntime runtime, float t, float dt)
        {
            if (!_running || EnemyPrefab == null) return;

            // SpawnInterval <= 0:只在 OnTrigger 生成一次,OnTick 不做事
            if (SpawnInterval <= 0f) return;

            _intervalTimer += dt;
            while (_intervalTimer >= SpawnInterval)
            {
                _intervalTimer -= SpawnInterval;
                SpawnOne(runtime);
            }
        }

        public override void OnEnd(LevelRuntime runtime)
        {
            _running = false;
            // 子类未来要加清理(清场 / 销毁最后一次的引用)在这里
        }

        void SpawnOne(LevelRuntime runtime)
        {
            if (EnemyPrefab == null) return;

            // 同 SimpleSpawnEntry 的 SetActive(false) → 覆盖 Flow → SetActive(true) 流程
            var go = UnityEngine.Object.Instantiate(EnemyPrefab);
            go.SetActive(false);
            go.transform.position = SpawnPosition;
            go.transform.rotation = Quaternion.Euler(0f, 0f, InitialRotation);

            if (OverrideFlow != null)
            {
                var shooter = go.GetComponent<ShooterEnemy>();
                if (shooter != null) shooter.Flow = OverrideFlow;
            }

            go.SetActive(true);
            runtime.Track(go);
        }
    }
}