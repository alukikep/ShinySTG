using System;
using UnityEngine;

namespace ShinySTG.Level
{
    /// <summary>
    /// 关卡主驱动:场景单例,协调 LevelDefinition + LevelRuntime + 事件总线。
    /// 类比 BossController —— 宿主 MonoBehaviour,持 SO + Runtime 引用,挂事件。
    ///
    /// 用法:
    ///   - 场景里创建一个 GameObject(命名 Level)挂本组件 + 一个 LevelDefinition .asset 拖到 Definition。
    ///   - AutoStart=true → 进入 Play Mode 自动开始关卡;false → 外部调 BeginLevel()(给"按 Start 键开始"之类需求用)。
    ///   - UI / 计分 / 动画系统订阅 OnLevelStart / OnLevelComplete / OnEnemySpawned / OnBossSpawned。
    ///
    /// 事件风格:与 PlayerHealth 一致 —— 实例事件挂在组件上,订阅 / 退订显式调用。
    /// </summary>
    public class LevelController : Singleton<LevelController>
    {
        [Header("Required")]
        [Tooltip("关卡配置资产。右键 Project → Create → STG → Level 创建。")]
        public LevelDefinition Definition;

        [Header("Behavior")]
        [Tooltip("进入场景是否自动开始关卡。false = 由外部脚本调 BeginLevel()(关卡选择菜单 / 按键开始等)。")]
        public bool AutoStart = true;

        [Tooltip("勾上后场景里的 BulletPool 在 BeginLevel 时若 Definition.Pool 为空,会自动 FindObjectOfType 兜底。")]
        public bool AutoFindBulletPool = true;

        // ─── 事件(挂在组件上的实例事件,与 PlayerHealth 风格一致)──────────────
        /// <summary>关卡开始时触发(参数 = 当前 Definition)。</summary>
        public event Action<LevelDefinition> OnLevelStart;
        /// <summary>关卡"自然结束"(Duration 到时),或外部调 CompleteLevel 时触发。</summary>
        public event Action<LevelDefinition> OnLevelComplete;
        /// <summary>任何 SpawnEntry 生成敌人成功后触发(参数 = 实例化出来的 GameObject)。</summary>
        public event Action<GameObject>      OnEnemySpawned;
        /// <summary>任何 SpawnEntry 生成 Boss 成功后触发。</summary>
        public event Action<GameObject>      OnBossSpawned;
        /// <summary>留口子:boss 被击败时触发。给"击败 boss 后解锁下一关"等订阅用。</summary>
        public event Action<GameObject>      OnBossDefeated;

        // ─── 运行时状态 ──
        LevelRuntime _runtime;
        BulletPool   _poolOverride;   // Definition.Pool 配了则用它,否则自动 Find
        bool         _running;
        bool         _completed;

        public bool IsRunning   => _running;
        public bool IsCompleted => _completed;
        public LevelRuntime Runtime => _runtime;
        public float Elapsed => _runtime?.Elapsed ?? 0f;

        void Start()
        {
            if (AutoStart) BeginLevel();
        }

        /// <summary>开始关卡。重复调用会先 Reset 上一轮再开(Reset 会自动销毁上一轮的活跃单位)。</summary>
        public void BeginLevel()
        {
            if (Definition == null)
            {
                Debug.LogWarning("[Level] LevelController.Definition 为空,无法开始。", this);
                return;
            }

            // 1. 解析 BulletPool
            _poolOverride = Definition.Pool;
            if (_poolOverride == null && AutoFindBulletPool)
                _poolOverride = FindObjectOfType<BulletPool>();

            // 2. 重建 Runtime —— Reset() 现在会同时销毁上一轮的活跃单位(敌人/Boss 等)
            _runtime?.Reset();
            _runtime = new LevelRuntime(Definition);
            _running   = true;
            _completed = false;

            // 3. 自动切歌:按 Definition.AutoSwitchBgm + Definition.AudioBinding 把音频系统接上
            //    (AudioSystem 不存在 = 静默跳过,不影响关卡运行)
            ShinySTG.Audio.AudioSystem.Instance?.EventHub?.TryBind(Definition);

            OnLevelStart?.Invoke(Definition);
        }

        /// <summary>
        /// 重置当前关卡到 t=0 状态,并清掉所有已生成的关卡单位。
        /// 等价于"销毁活跃单位 + 重置 Runtime + 重新触发 OnLevelStart",
        /// 区别于 BeginLevel:BeginLevel 会重建 Runtime 实例;ReloadLevel 复用同一个实例。
        ///
        /// 给"按 R 键重置关卡"等调试 / 测试入口用 —— 解决"测试时生成单位结束后仍滞留"的问题。
        /// </summary>
        public void ReloadLevel()
        {
            if (Definition == null) return;

            // 先清空上一轮的活跃单位,再复用 Runtime 实例(保留 _alive/_sustained 列表的引用,避免外部订阅 Runtime 失效)
            _runtime?.Reset();

            // 触发一次 OnLevelStart 让订阅者(UI / 计分 / 音效)知道关卡重开了
            _running   = true;
            _completed = false;

            // 重绑音频(AudioEventHub.TryBind 幂等,会刷新 _activeBinding)
            ShinySTG.Audio.AudioSystem.Instance?.EventHub?.TryBind(Definition);

            OnLevelStart?.Invoke(Definition);
        }

        /// <summary>外部强制结束关卡(玩家死亡、退出按钮、调试按钮等)。</summary>
        public void CompleteLevel()
        {
            if (_completed) return;
            _completed = true;
            _running   = false;
            OnLevelComplete?.Invoke(Definition);
        }

        void Update()
        {
            if (!_running || _runtime == null || _completed) return;

            _runtime.Tick(Time.deltaTime);

            // Duration 到时自然结束
            if (Definition.Duration > 0f && _runtime.Elapsed >= Definition.Duration)
                CompleteLevel();
        }

        // ─── SpawnEntry 调用的事件广播入口 ───────────────────────────────────
        // 把这一对方法暴露给 SpawnEntry 子类用,而不是让子类直接 Invoke 事件
        // —— 保持事件发送权集中在 LevelController(与 EnemyHealth 暴露 OnDamaged 给子组件用法对齐)。
        public void NotifyEnemySpawned(GameObject go)  => OnEnemySpawned?.Invoke(go);
        public void NotifyBossSpawned (GameObject go)  => OnBossSpawned?.Invoke(go);
        public void NotifyBossDefeated(GameObject go)  => OnBossDefeated?.Invoke(go);
    }
}