using System;
using ShinySTG.Level;
using UnityEngine;
using PlayerController = ShinySTG.Player.Player;

namespace ShinySTG.GameFlow
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-200)]
    public sealed class GameplayBootstrap : MonoBehaviour
    {
        [SerializeField, Tooltip("本场景唯一的关卡控制器；由本组件关闭自动启动。")]
        LevelController _level;
        [SerializeField, Tooltip("直接打开场景测试时使用的角色。")]
        CharacterDefinition _defaultCharacter;
        [SerializeField, Tooltip("直接打开场景测试时使用的关卡。")]
        StageDefinition _defaultStage;
        [SerializeField, Tooltip("直接打开场景时优先使用的关卡序列；留空兼容默认单关。")]
        StageSequenceDefinition _defaultSequence;

        public LevelController Level => _level;
        public PlayerController SpawnedPlayer { get; private set; }
        StageSettlement _settlement;
        public GameplayPauseController PauseMenu { get; private set; }

        void Awake()
        {
            if (_level != null) _level.AutoStart = false;
        }

        void Start()
        {
            var flow = GameFlowController.EnsureInstance();
            if (!flow.IsLoading)
                flow.StartInPlace(this, _defaultSequence != null
                    ? GameStartRequest.FromSequence(_defaultCharacter, _defaultSequence)
                    : new GameStartRequest(_defaultCharacter, _defaultStage));
        }

        public void Prepare(GameStartRequest request)
        {
            if (request == null || !request.Validate(out _))
                throw new InvalidOperationException("开局参数无效。");
            if (_level == null || !_level.isActiveAndEnabled || _level.gameObject.scene != gameObject.scene)
                throw new InvalidOperationException("GameplayBootstrap 缺少本场景启用的 LevelController。");
            if (_level.IsRunning || SpawnedPlayer != null || PlayerController.Instance != null)
                throw new InvalidOperationException("场景已经运行或预放了玩家，请只通过 Bootstrap 创建玩家。");
            if (BulletPool.Instance == null || ShinySTG.Hitbox.CollisionService.Instance == null)
                throw new InvalidOperationException("游戏场景缺少 BulletPool 或 CollisionService。");
            _level.Definition = request.Stage.Level;
            SpawnedPlayer = Instantiate(request.Character.PlayerPrefab, request.Stage.SpawnPosition, Quaternion.identity);
        }

        public void Begin()
        {
            if (SpawnedPlayer == null || SpawnedPlayer.Health == null || SpawnedPlayer.Health.IsDead)
                throw new InvalidOperationException("玩家初始化失败或初始生命为零。");
            _settlement?.Dispose();
            if (PauseMenu == null)
            {
                PauseMenu = gameObject.AddComponent<GameplayPauseController>();
                PauseMenu.Bind(this, GameFlowController.Instance);
            }
            _settlement = new StageSettlement(_level, SpawnedPlayer, GameFlowController.Instance.CurrentSession,
                PauseMenu.TryOfferContinue);
            _level.BeginLevel();
            if (!_level.IsRunning) throw new InvalidOperationException("关卡启动失败。");
        }

        /// <summary>换关沿用本次玩家和结算订阅，不重新初始化资源。</summary>
        public void PrepareNextStage(StageDefinition stage)
        {
            if (stage == null || stage.Level == null || _level == null || _level.IsRunning
                || _level.IsBattleCleanupPending || SpawnedPlayer == null || SpawnedPlayer.Health.IsDead)
                throw new InvalidOperationException("下一关准备条件不满足。");
            _level.Definition = stage.Level;
        }

        void OnDestroy() => _settlement?.Dispose();
    }
}
