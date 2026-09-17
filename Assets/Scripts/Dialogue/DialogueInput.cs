using UnityEngine;

namespace ShinySTG.Dialogue
{
    /// <summary>项目当前使用旧输入系统；其他输入源可直接调用服务的 Confirm/SetFastForward。</summary>
    public sealed class DialogueInput : MonoBehaviour
    {
        [SerializeField, Tooltip("接收输入的对话服务。")]
        DialogueService _service;
        [SerializeField, Tooltip("补全文字或进入下一句。")]
        KeyCode _confirmKey = KeyCode.Z;
        [SerializeField, Tooltip("备用确认键。")]
        KeyCode _alternateConfirmKey = KeyCode.Return;
        [SerializeField, Tooltip("按住快进。")]
        KeyCode _fastForwardKey = KeyCode.LeftControl;

        DialogueHandle _observed;
        bool _armed;

        void Update()
        {
            if (_service == null) return;
            var handle = _service.ActiveHandle;
            if (handle != _observed)
            {
                _observed = handle;
                _armed = false;
            }
            if (handle == null) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            bool confirmHeld = Input.GetKey(_confirmKey) || Input.GetKey(_alternateConfirmKey);
            bool fastForwardHeld = Input.GetKey(_fastForwardKey);
            if (!_armed)
            {
                // 会话开始时所有对话键必须先松开，避免射击长按或上次快进穿透。
                _armed = !confirmHeld && !fastForwardHeld;
                _service.SetFastForward(false);
                return;
            }
            _service.SetFastForward(fastForwardHeld);
            if (!fastForwardHeld && (Input.GetKeyDown(_confirmKey) || Input.GetKeyDown(_alternateConfirmKey)))
                _service.Confirm();
#endif
        }

        void OnDisable()
        {
            if (_service != null) _service.SetFastForward(false);
            _observed = null;
            _armed = false;
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            if (_service != null) _service.SetFastForward(false);
            _armed = false;
        }
    }
}
