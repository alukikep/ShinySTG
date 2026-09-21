using UnityEngine;

namespace ShinySTG.UI
{
    /// <summary>只接受键盘；失焦、重新启用和确认反馈后等待松键。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(StartMenuController))]
    public sealed class StartMenuInput : MonoBehaviour
    {
        [SerializeField, Tooltip("向上选择。")]
        KeyCode _upKey = KeyCode.UpArrow;
        [SerializeField, Tooltip("向下选择。")]
        KeyCode _downKey = KeyCode.DownArrow;
        [SerializeField, Tooltip("确认当前选项。")]
        KeyCode _confirmKey = KeyCode.Z;
        [SerializeField, Tooltip("备用确认键。")]
        KeyCode _alternateConfirmKey = KeyCode.Return;
        [SerializeField, Min(0.05f), Tooltip("长按首次重复的等待秒数。")]
        float _repeatDelay = 0.35f;
        [SerializeField, Min(0.02f), Tooltip("长按后续重复的间隔秒数。")]
        float _repeatInterval = 0.1f;

        StartMenuController _controller;
        readonly KeyboardMenuNavigation _navigation = new();
        bool _focused = true;

        void Awake() => _controller = GetComponent<StartMenuController>();
        void OnEnable() => ResetInput();
        void OnDisable() => ResetInput();

        void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            ResetInput();
        }

        void ResetInput()
        {
            _navigation.Reset();
        }

        void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!_focused || _controller == null || !_controller.CanInteract)
            {
                ResetInput();
                return;
            }
            bool up = Input.GetKey(_upKey);
            bool down = Input.GetKey(_downKey);
            bool confirm = Input.GetKey(_confirmKey) || Input.GetKey(_alternateConfirmKey);
            _navigation.Read(up, down, confirm,
                Input.GetKeyDown(_confirmKey) || Input.GetKeyDown(_alternateConfirmKey),
                false, false, Time.unscaledTime, _repeatDelay, _repeatInterval,
                out int direction, out bool accepted, out _);
            if (accepted) _controller.Confirm();
            else if (direction != 0) _controller.MoveSelection(direction);
#endif
        }
    }
}
