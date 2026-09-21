using UnityEngine;

namespace ShinySTG.UI
{
    /// <summary>开始菜单与暂停菜单共用的松键保护、确认优先和长按重复。</summary>
    public sealed class KeyboardMenuNavigation
    {
        bool _armed;
        int _heldDirection;
        float _nextRepeat;

        public void Reset() { _armed = false; _heldDirection = 0; _nextRepeat = 0f; }

        public void Read(bool up, bool down, bool confirmHeld, bool confirmDown,
            bool cancelHeld, bool cancelDown, float now, float repeatDelay, float repeatInterval,
            out int direction, out bool confirm, out bool cancel)
        {
            direction = 0;
            confirm = cancel = false;
            if (!_armed)
            {
                _armed = !up && !down && !confirmHeld && !cancelHeld;
                return;
            }
            if (cancelDown || confirmDown)
            {
                cancel = cancelDown;
                confirm = !cancel && confirmDown;
                Reset();
                return;
            }
            int held = up == down ? 0 : down ? 1 : -1;
            if (held == 0) { _heldDirection = 0; return; }
            if (held != _heldDirection)
            {
                _heldDirection = direction = held;
                _nextRepeat = now + Mathf.Max(0.05f, repeatDelay);
            }
            else if (now >= _nextRepeat)
            {
                direction = held;
                _nextRepeat = now + Mathf.Max(0.02f, repeatInterval);
            }
        }
    }
}
