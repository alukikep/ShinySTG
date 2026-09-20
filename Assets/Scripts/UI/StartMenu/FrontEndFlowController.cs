using System.Collections;
using UnityEngine;

namespace ShinySTG.UI
{
    /// <summary>协调标题页与角色选择，将确认交给跨场景开局流程。</summary>
    public sealed class FrontEndFlowController : MonoBehaviour
    {
        [SerializeField, Tooltip("现有开始菜单控制器。")]
        StartMenuController _menu;
        [SerializeField, Tooltip("开始菜单页面根节点。")]
        GameObject _menuPage;
        [SerializeField, Tooltip("角色选择占位页根节点。")]
        GameObject _characterPage;
        [SerializeField, Tooltip("独立于页面的黑幕组件。")]
        ScreenWipeTransition _transition;
        [SerializeField, Tooltip("角色确认后进入的首关。")]
        ShinySTG.GameFlow.StageDefinition _firstStage;
        [SerializeField, Min(0.05f), Tooltip("角色选择长按首次重复延迟。")]
        float _repeatDelay = 0.35f;
        [SerializeField, Min(0.02f), Tooltip("角色选择长按重复间隔。")]
        float _repeatInterval = 0.1f;

        int _heldDirection;
        float _nextRepeat;

        bool _busy;
        bool _showingCharacters;
        bool _returnArmed;
        bool _focused = true;

        bool IsReady => _menu != null && _menuPage != null && _characterPage != null
            && _transition != null && _transition.IsReady;

        void OnEnable()
        {
            if (_menu != null) _menu.Confirmed += OnConfirmed;
            ResetFlow();
        }

        void OnDisable()
        {
            if (_menu != null) _menu.Confirmed -= OnConfirmed;
            StopAllCoroutines();
            ResetFlow();
        }

        void ResetFlow()
        {
            _busy = _showingCharacters = _returnArmed = false;
            _heldDirection = 0;
            if (_transition != null) _transition.ResetTransition();
            if (_characterPage != null) _characterPage.SetActive(false);
            if (_menuPage != null) _menuPage.SetActive(true);
            if (_menu != null) _menu.SetInputLocked(false);
        }

        void OnConfirmed(StartMenuOption option)
        {
            if (option != StartMenuOption.StartGame || _busy || !IsReady) return;
            StartCoroutine(SwitchPage(true));
        }

        IEnumerator SwitchPage(bool characters)
        {
            _busy = true;
            _returnArmed = false;
            _heldDirection = 0;
            _menu.SetInputLocked(true);
            try
            {
                if (characters)
                    while (_menu != null && _menu.IsConfirming) yield return null;
                if (!IsReady) yield break;
                yield return _transition.Play(characters, () =>
                {
                    _menuPage.SetActive(!characters);
                    _characterPage.SetActive(characters);
                    _showingCharacters = characters;
                });
            }
            finally
            {
                _busy = false;
                _returnArmed = false;
                if (_menu != null) _menu.SetInputLocked(false);
            }
        }

        void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            var gameFlow = ShinySTG.GameFlow.GameFlowController.Instance;
            if (gameFlow != null && (gameFlow.IsLoading || gameFlow.Failure != null)) return;
            if (!_focused || _busy || !_showingCharacters || !IsReady) return;
            bool held = Input.GetKey(KeyCode.X) || Input.GetKey(KeyCode.Escape)
                || Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.Return)
                || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow);
            if (!_returnArmed)
            {
                _returnArmed = !held;
                return;
            }
            if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape))
            {
                StartCoroutine(SwitchPage(false));
                return;
            }
            if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))
            {
                _returnArmed = false;
                var view = _characterPage.GetComponent<CharacterSelectView>();
                if (view == null) return;
                var flow = ShinySTG.GameFlow.GameFlowController.EnsureInstance();
                if (!flow.TryStartGame(view.SelectedDefinition, _firstStage, out var error))
                    view.ShowError(error);
                return;
            }
            bool up = Input.GetKey(KeyCode.UpArrow);
            bool down = Input.GetKey(KeyCode.DownArrow);
            int direction = up == down ? 0 : down ? 1 : -1;
            if (direction == 0) { _heldDirection = 0; return; }
            var selection = _characterPage.GetComponent<CharacterSelectView>();
            if (selection == null) return;
            if (direction != _heldDirection)
            {
                _heldDirection = direction;
                _nextRepeat = Time.unscaledTime + Mathf.Max(0.05f, _repeatDelay);
                selection.MoveSelection(direction);
            }
            else if (Time.unscaledTime >= _nextRepeat)
            {
                _nextRepeat = Time.unscaledTime + Mathf.Max(0.02f, _repeatInterval);
                selection.MoveSelection(direction);
            }
#endif
        }

        void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            _returnArmed = false;
            _heldDirection = 0;
        }
    }
}
