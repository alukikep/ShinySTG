using System;
using UnityEngine;

namespace ShinySTG.UI
{
    public enum StartMenuOption { StartGame, Settings, Quit }

    /// <summary>菜单状态与确认出口；具体游戏流程由后续订阅者处理。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(StartMenuView))]
    public sealed class StartMenuController : MonoBehaviour
    {
        [SerializeField, Min(0.05f), Tooltip("确认反馈期间暂时锁定菜单的秒数。")]
        float _confirmDuration = 0.35f;

        StartMenuView _view;
        float _confirmRemaining;
        bool _inputLocked;

        public StartMenuOption SelectedOption { get; private set; }
        public bool CanInteract => isActiveAndEnabled && !_inputLocked && _view != null && _view.IsReady && _confirmRemaining <= 0f;
        public bool IsConfirming => _confirmRemaining > 0f;
        public void SetInputLocked(bool locked) => _inputLocked = locked;
        public event Action<StartMenuOption> Confirmed;

        void Awake() => _view = GetComponent<StartMenuView>();

        void OnEnable()
        {
            if (_view == null) _view = GetComponent<StartMenuView>();
            SelectedOption = StartMenuOption.StartGame;
            _confirmRemaining = 0f;
            if (_view != null) _view.ResetView(SelectedOption);
        }

        void Update()
        {
            if (_confirmRemaining <= 0f) return;
            _confirmRemaining = Mathf.Max(0f, _confirmRemaining - Time.unscaledDeltaTime);
            if (_view != null) _view.ShowConfirmFlash(_confirmRemaining > 0f,
                Mathf.FloorToInt(_confirmRemaining * 16f) % 2 == 0);
        }

        public void MoveSelection(int direction)
        {
            if (!CanInteract || direction == 0) return;
            SelectedOption = (StartMenuOption)(((int)SelectedOption + (direction > 0 ? 1 : 2)) % 3);
            _view.ShowSelection(SelectedOption);
        }

        public void Confirm()
        {
            if (!CanInteract) return;
            _confirmRemaining = Mathf.Max(0.05f, _confirmDuration);
            _view.ShowConfirmation(SelectedOption);
            Confirmed?.Invoke(SelectedOption);
        }

        void OnDisable()
        {
            _confirmRemaining = 0f;
            if (_view != null) _view.ResetView(SelectedOption);
        }
    }
}
