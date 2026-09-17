using ShinySTG.Audio;
using ShinySTG.EnemyAI.Boss;
using UnityEngine;

namespace ShinySTG.Level.Encounter
{
    /// <summary>
    /// 单场 Boss 遭遇的运行时协调器。监听 Boss 的战斗事实并播放表现，
    /// 最终向关卡时间轴报告完成；不参与 Boss 的阶段判定与伤害逻辑。
    /// </summary>
    public sealed class BossEncounterRuntime : ILevelTimelineProcess
    {
        readonly BossEncounterDefinition _definition;
        readonly GameObject _bossObject;
        readonly BossHealth _health;
        readonly BossController _controller;
        readonly bool _blocksTimeline;

        bool _defeated;
        bool _complete;
        bool _disposed;
        float _outroElapsed;

        public bool IsComplete => _complete;
        public bool BlocksTimeline => _blocksTimeline && !_complete;

        public BossEncounterRuntime(BossEncounterDefinition definition, GameObject bossObject, bool blocksTimeline)
        {
            _definition = definition;
            _bossObject = bossObject;
            _blocksTimeline = blocksTimeline;
            _health = bossObject != null ? bossObject.GetComponent<BossHealth>() : null;
            _controller = bossObject != null ? bossObject.GetComponent<BossController>() : null;

            if (_health == null || _controller == null)
            {
                Debug.LogError("[Level] Boss Encounter prefab 缺少 BossHealth 或 BossController，时间轴不会阻塞。", bossObject);
                _complete = true;
                return;
            }

            _health.OnDeath += HandleDefeated;
            _controller.OnPhaseEntered += HandlePhaseEntered;
            _controller.OnPhaseExited += HandlePhaseExited;
            AudioMix.PlaySfx(_definition?.EncounterStartSfx);
        }

        public void Tick(float dt)
        {
            if (_complete) return;

            // 非死亡路径被外部销毁时避免关卡永久软锁。
            if (_bossObject == null && !_defeated)
            {
                Debug.LogWarning("[Level] Boss Encounter 目标在未触发死亡事件时消失，已解除时间轴阻塞。");
                _complete = true;
                return;
            }

            if (!_defeated) return;
            _outroElapsed += dt;
            if (_outroElapsed >= Mathf.Max(0f, _definition != null ? _definition.DefeatOutroDelay : 0f))
                _complete = true;
        }

        void HandleDefeated()
        {
            if (_defeated) return;
            _defeated = true;
            _outroElapsed = 0f;
            AudioMix.PlaySfx(_definition?.DefeatSfx);
        }

        void HandlePhaseEntered(int phaseIndex, BossPhase phase)
        {
            var presentation = FindPresentation(phaseIndex);
            if (presentation != null) AudioMix.PlaySfx(presentation.EnterSfx);
        }

        void HandlePhaseExited(int phaseIndex, BossPhase phase)
        {
            var presentation = FindPresentation(phaseIndex);
            if (presentation != null) AudioMix.PlaySfx(presentation.ExitSfx);
        }

        PhasePresentation FindPresentation(int phaseIndex)
        {
            var presentations = _definition?.PhasePresentations;
            if (presentations == null) return null;
            for (int i = 0; i < presentations.Length; i++)
                if (presentations[i] != null && presentations[i].PhaseIndex == phaseIndex)
                    return presentations[i];
            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_health != null) _health.OnDeath -= HandleDefeated;
            if (_controller != null)
            {
                _controller.OnPhaseEntered -= HandlePhaseEntered;
                _controller.OnPhaseExited -= HandlePhaseExited;
            }
        }
    }
}
