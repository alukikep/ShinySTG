using ShinySTG.Audio;
using ShinySTG.EnemyAI.Boss;
using ShinySTG.GameActions;
using UnityEngine;

namespace ShinySTG.Level.Encounter
{
    public sealed class BossEncounterRuntime : ILevelTimelineProcess
    {
        readonly BossEncounterDefinition _definition;
        readonly GameObject _bossObject;
        readonly Boss _boss;
        readonly BossHealth _health;
        readonly BossController _controller;
        readonly bool _blocksTimeline;
        readonly GameActionRunner _runner = new();
        GameActionContext _context;
        GameActionHandle _phaseActions, _startActions, _defeatActions, _completeActions;
        bool _defeated, _complete, _disposed, _completionStarted;
        float _outroElapsed;
        public bool IsComplete => _complete;
        public bool BlocksTimeline => _blocksTimeline && !_complete;

        public BossEncounterRuntime(BossEncounterDefinition definition, GameObject bossObject, bool blocksTimeline)
        {
            _definition = definition;
            _bossObject = bossObject;
            _blocksTimeline = blocksTimeline;
            _boss = bossObject != null ? bossObject.GetComponent<Boss>() : null;
            _health = bossObject != null ? bossObject.GetComponent<BossHealth>() : null;
            _controller = bossObject != null ? bossObject.GetComponent<BossController>() : null;
            if (_health == null || _controller == null || _boss == null)
            {
                Debug.LogError("[Level] Boss Encounter 缺少 Boss、BossHealth 或 BossController。", bossObject);
                _complete = true;
                return;
            }
            _context = new GameActionContext(bossObject.transform, _controller);
            _boss.RetainForDefeatActions = true;
            _controller.PhaseActions = PlayPhaseActions;
            _health.OnDeath += HandleDefeated;
            AudioMix.PlaySfx(_definition?.EncounterStartSfx);
            _startActions = _runner.Play(_definition?.StartActions, _context);
            _controller.StartGate = Gate(_definition?.StartActions, _startActions);
        }

        static GameActionHandle Gate(ActionSequence sequence, GameActionHandle handle) =>
            sequence != null && sequence.WaitForCompletion ? handle : null;

        public void Tick(float dt)
        {
            if (_complete || _disposed) return;
            if (_bossObject == null && !_defeated) { Dispose(); return; }
            _runner.Tick(dt);
            if (!_defeated) return;
            _outroElapsed += dt;
            var gate = Gate(_definition?.DefeatActions, _defeatActions);
            if (gate != null && !gate.IsComplete) return;
            if (_bossObject != null) Object.Destroy(_bossObject);
            if (_outroElapsed < Mathf.Max(0f, _definition != null ? _definition.DefeatOutroDelay : 0f)) return;
            if (!_completionStarted)
            {
                _completionStarted = true;
                _completeActions = _runner.Play(_definition?.CompleteActions, _context);
            }
            gate = Gate(_definition?.CompleteActions, _completeActions);
            if (gate == null || gate.IsComplete) _complete = true;
        }

        void HandleDefeated()
        {
            if (_defeated || _disposed) return;
            _defeated = true;
            _runner.Dispose();
            _context = new GameActionContext(_bossObject != null ? _bossObject.transform : null, _controller,
                _controller != null ? _controller.CurrentPhaseIndex : -1);
            AudioMix.PlaySfx(_definition?.DefeatSfx);
            _defeatActions = _runner.Play(_definition?.DefeatActions, _context);
            if (Gate(_definition?.DefeatActions, _defeatActions) == null || _defeatActions.IsComplete)
                if (_bossObject != null) Object.Destroy(_bossObject);
        }

        GameActionHandle PlayPhaseActions(int index, bool entering)
        {
            if (_disposed || _defeated) return null;
            // 非等待的退出动作可与新阶段进入动作并行，统一在下一次阶段退出时清理。
            if (!entering) { _phaseActions?.Cancel(); _runner.Dispose(); }
            var presentation = FindPresentation(index);
            if (presentation != null) AudioMix.PlaySfx(entering ? presentation.EnterSfx : presentation.ExitSfx);
            if (_health == null || _health.IsDead) return null;
            var sequence = entering ? presentation?.EnterActions : presentation?.ExitActions;
            _phaseActions = _runner.Play(sequence, new GameActionContext(_bossObject.transform, _controller, index));
            return Gate(sequence, _phaseActions);
        }

        PhasePresentation FindPresentation(int index)
        {
            var presentations = _definition?.PhasePresentations;
            if (presentations == null) return null;
            foreach (var presentation in presentations)
                if (presentation != null && presentation.PhaseIndex == index) return presentation;
            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _complete = true;
            _runner.Dispose();
            if (_health != null) _health.OnDeath -= HandleDefeated;
            if (_controller != null) { _controller.PhaseActions = null; _controller.StartGate = null; }
            if (_boss != null)
            {
                _boss.RetainForDefeatActions = false;
                if (_defeated) Object.Destroy(_bossObject);
            }
        }
    }
}
