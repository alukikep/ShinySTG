using System;
using ShinySTG.Level;
using ShinySTG.Player;
using ShinySTG.UI;
using UnityEngine;

namespace ShinySTG.GameFlow
{
    [DefaultExecutionOrder(-300)]
    public sealed class GameplayPauseController : MonoBehaviour
    {
        GameplayBootstrap _bootstrap;
        GameFlowController _flow;
        PlayerDeathController _death;
        PauseMenuView _view;
        GameplayPause _pause;
        IDisposable _deathRestriction;
        readonly KeyboardMenuNavigation _navigation = new();
        int _attempt;
        int _selection;
        bool _waiting;
        bool _focused = true;
        bool _escapeReady;
        public bool IsOpen => _pause != null;

        public void Bind(GameplayBootstrap bootstrap, GameFlowController flow)
        {
            _bootstrap = bootstrap;
            _flow = flow;
            _death = bootstrap.SpawnedPlayer.GetComponent<PlayerDeathController>();
            var go = new GameObject("PauseMenu", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _view = go.AddComponent<PauseMenuView>();
        }

        public bool TryOfferContinue(int attempt)
        {
            if (!isActiveAndEnabled || _waiting || _flow == null || _flow.IsLoading || _flow.Failure != null
                || _death == null || !_death.isActiveAndEnabled
                || !_bootstrap.Level.TryAwaitContinue(attempt)) return false;
            _attempt = attempt;
            _waiting = true;
            _deathRestriction = BattleRestriction.Acquire();
            return true;
        }

        void Update()
        {
            if (_bootstrap == null || _flow == null || _view == null) return;
            var level = _bootstrap.Level;
            if (_flow.IsLoading || _flow.IsShowingResults || _flow.IsShowingStageResults || _flow.Failure != null || level == null
                || level.IsCompleted || (_waiting && (level.AttemptId != _attempt || !level.IsAwaitingContinue)))
            {
                CloseForTransition();
                return;
            }
            if (_waiting && (_death == null || !_death.isActiveAndEnabled))
            {
                level.TryEndLevel(LevelEndReason.Failed, _attempt);
                CloseForTransition();
                return;
            }
            if (_waiting && !IsOpen && _death.IsDeathPresentationComplete) Open();
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!_focused) return;
            if (!IsOpen)
            {
                if (!Input.GetKey(KeyCode.Escape)) _escapeReady = true;
                if (!_waiting && level.IsRunning && _escapeReady && Input.GetKeyDown(KeyCode.Escape)) Open();
                return;
            }
            _navigation.Read(Input.GetKey(KeyCode.UpArrow), Input.GetKey(KeyCode.DownArrow),
                Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.Return),
                Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return),
                Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.X),
                Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.X),
                Time.unscaledTime, .35f, .1f, out int direction, out bool confirm, out bool cancel);
            if (cancel) { if (!_waiting) Resume(); return; }
            if (confirm)
            {
                if (_selection == 0) Resume();
                else if (_selection == 1) _flow.ReturnToMenu();
                else _flow.RestartRun();
                return;
            }
            if (direction != 0)
            {
                _selection = (_selection + direction + 3) % 3;
                _view.Show(_waiting, _selection);
            }
#endif
        }

        void Open()
        {
            _pause = new GameplayPause();
            _selection = 0;
            _escapeReady = false;
            _navigation.Reset();
            _view.Show(_waiting, _selection);
        }

        void Resume()
        {
            if (_waiting)
            {
                var health = _bootstrap.SpawnedPlayer.Health;
                if (!_death.IsDeathPresentationComplete || !health.IsDead
                    || !_bootstrap.Level.IsAwaitingContinue || _bootstrap.Level.AttemptId != _attempt) return;
                health.AddLife(3);
                if (!_death.Respawn()) { health.AddLife(-3); return; }
                _bootstrap.Level.TryResumeContinue(_attempt);
            }
            CloseForTransition();
        }

        public void CloseForTransition()
        {
            if (_view != null) _view.Hide();
            _pause?.Dispose();
            _pause = null;
            _deathRestriction?.Dispose();
            _deathRestriction = null;
            _waiting = false;
            _navigation.Reset();
            _escapeReady = false;
        }

        void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            _navigation.Reset();
            _escapeReady = false;
        }

        void OnDisable()
        {
            if (_waiting && _bootstrap != null && _bootstrap.Level != null)
                _bootstrap.Level.TryEndLevel(LevelEndReason.Failed, _attempt);
            CloseForTransition();
        }
    }
}
