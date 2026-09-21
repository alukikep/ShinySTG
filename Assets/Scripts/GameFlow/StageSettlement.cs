using System;
using ShinySTG.Level;
using PlayerController = ShinySTG.Player.Player;

namespace ShinySTG.GameFlow
{
    /// <summary>本场景玩家与本局数据间的订阅；只结算，不推进、不清场。</summary>
    public sealed class StageSettlement : IDisposable
    {
        readonly LevelController _level;
        readonly PlayerController _player;
        readonly RunSession _session;
        long _startingScore;
        int _attemptId;
        bool _disposed;
        readonly Func<int, bool> _offerContinue;

        public StageSettlement(LevelController level, PlayerController player, RunSession session,
            Func<int, bool> offerContinue = null)
        {
            _offerContinue = offerContinue;
            _level = level != null ? level : throw new ArgumentNullException(nameof(level));
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            if (player.Health == null || player.Resources == null)
                throw new ArgumentException("玩家缺少生命或资源组件。", nameof(player));
            _level.OnLevelStart += OnStart;
            _level.OnLevelEnded += OnEnded;
            _player.Health.OnAllLivesLost += OnFailed;
        }

        void OnStart(LevelDefinition definition)
        {
            _attemptId = _level.AttemptId;
            _startingScore = _player.Resources.Score;
            _session.ResetCurrentStageResult();
            if (_player.Health.IsDead) OnFailed();
        }

        void OnFailed()
        {
            if (_disposed || (_offerContinue?.Invoke(_attemptId) ?? false)) return;
            _level.TryEndLevel(LevelEndReason.Failed, _attemptId);
        }

        void OnEnded(LevelDefinition definition, LevelEndReason reason)
        {
            if (_disposed || reason != LevelEndReason.Cleared || _player == null
                || _level.AttemptId != _attemptId || definition != _session.CurrentStage.Level) return;
            var health = _player.Health;
            var resources = _player.Resources;
            if (health == null || resources == null || health.IsDead) return;
            var result = new StageResult(_session.CurrentStageIndex, _session.CurrentStage.Id,
                Math.Max(0L, resources.Score - _startingScore), resources.Score,
                health.Lives, resources.Bombs, health.PowerUnits, health.GrazeCount);
            _session.TryRecordResult(result);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_level != null)
            {
                _level.OnLevelStart -= OnStart;
                _level.OnLevelEnded -= OnEnded;
            }
            if (_player != null && _player.Health != null) _player.Health.OnAllLivesLost -= OnFailed;
        }
    }
}
