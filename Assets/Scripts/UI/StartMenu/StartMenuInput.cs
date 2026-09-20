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
        bool _armed;
        bool _focused = true;
        int _heldDirection;
        float _nextRepeat;

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
            _armed = false;
            _heldDirection = 0;
            _nextRepeat = 0f;
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
            if (!_armed)
            {
                _armed = !up && !down && !confirm;
                return;
            }
            // 确认优先，避免同一帧移动并确认另一个选项。
            if (Input.GetKeyDown(_confirmKey) || Input.GetKeyDown(_alternateConfirmKey))
            {
                ResetInput();
                _controller.Confirm();
                return;
            }
            int direction = up == down ? 0 : down ? 1 : -1;
            if (direction == 0)
            {
                _heldDirection = 0;
                return;
            }
            float now = Time.unscaledTime;
            if (direction != _heldDirection)
            {
                _heldDirection = direction;
                _nextRepeat = now + Mathf.Max(0.05f, _repeatDelay);
                _controller.MoveSelection(direction);
            }
            else if (now >= _nextRepeat)
            {
                _nextRepeat = now + Mathf.Max(0.02f, _repeatInterval);
                _controller.MoveSelection(direction);
            }
#endif
        }
    }
}
