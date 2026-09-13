using System;
using SerializeReferenceEditor;
using ShinySTG.EnemyAI;
using ShinySTG.Level.SpawnEntries.PositionStrategies;
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
    ///   - SpawnPositionStrategy = 每次生成时,相对 SpawnPosition 的偏移"算法"(可插拔)。
    ///     下拉选 Fixed(累加偏移)或 Random(范围随机),详细见 PositionStrategies 子类。
    ///
    /// 编辑器表现:
    ///   - 时间轴 block 宽度 = Duration × 像素/秒
    ///   - block 视觉:左 8px 实色(标识起点) + 主体半透明(GetColor 同色 + alpha 减半)
    ///
    /// 实现说明:
    ///   - Duration 在基类 SpawnEntry 上;本类只 override OnTick
    ///   - 触发时 OnTrigger 调 RegisterSustained 注册自己,然后 LevelRuntime 每帧调 OnTick
    ///   - 到 Duration:LevelRuntime 调 OnEnd
    ///   - SpawnPositionStrategy 是多态对象:OnTrigger 时调 Reset() 重置内部状态(如累加器),
    ///     每次 SpawnOne 内 GetOffset(t) 拿本次偏移。Strategy 可为 null(老 entry / 用户手动清空),
    ///     此时退化为无偏移(等同 strategy.Offset/Range 都为 (0,0) 的行为)。
    /// </summary>
    [Serializable, SRName("敌人生成/Sustain")]
    public class SustainSpawnEntry : SpawnEntry
    {
        [Tooltip("每 SpawnInterval 秒生成一只 EnemyPrefab 的 prefab。\n" +
                 "<=0 只在 TriggerTime 生成一次(退化成 Simple)。")]
        public float SpawnInterval = 0.5f;

        [Tooltip("敌人 prefab。要求挂 ShooterEnemy(+ Health + Hitbox + Enemy 总控)。")]
        public GameObject EnemyPrefab;

        [Tooltip("生成位置(世界坐标)。")]
        public Vector2 SpawnPosition = Vector2.zero;

        [Tooltip("每次生成时相对 SpawnPosition 的偏移策略。\n" +
                 "下拉选 Fixed(累加偏移)或 Random(范围随机)。\n" +
                 "为空时退化为无偏移(等同 strategy 参数全零)。\n" +
                 "OnTrigger 时会自动 Reset(),持续结束时 strategy 内部状态被丢弃。")]
        [SerializeReference, SR]
        public SpawnPositionStrategy SpawnPositionStrategy = new FixedSpawnPositionStrategy();

        [Tooltip("初始朝向(度)。")]
        public float InitialRotation = 0f;

        [Tooltip("可选:覆盖 prefab 上 ShooterEnemy.Flow 的行为流资产。null = 用 prefab 自己配的。")]
        public BehaviorFlow OverrideFlow;

        float _intervalTimer;
        float _elapsed;
        bool _running;

        public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
        {
            _running = true;
            _intervalTimer = 0f;
            _elapsed = 0f;

            // 重置 strategy 内部状态(累加器 / 历史位置等),保证同一条 entry 多次触发时从原点开始
            SpawnPositionStrategy?.Reset();

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

            _elapsed += dt;
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

            // 计算本次生成的世界坐标:SpawnPosition + strategy.GetOffset(_elapsed)
            // strategy 为 null 时退化:offset = (0,0)
            Vector2 offset = SpawnPositionStrategy != null
                ? SpawnPositionStrategy.GetOffset(_elapsed)
                : Vector2.zero;
            Vector2 spawnAt = SpawnPosition + offset;

            // 同 SimpleSpawnEntry 的 SetActive(false) → 覆盖 Flow → SetActive(true) 流程
            var go = UnityEngine.Object.Instantiate(EnemyPrefab);
            go.SetActive(false);
            go.transform.position = spawnAt;
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