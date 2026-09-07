using UnityEngine;

namespace ShinySTG.Player
{
    /// <summary>
    /// 旧版输入兜底驱动(可选组件)。
    ///
    /// 项目装了新版 InputSystem package 时:用 PlayerInput 组件 + Action Asset,
    /// 走 Player.OnPlayer / OnAttack / OnFocus → 推给 Movement / Shooting。
    ///
    /// 项目没装 InputSystem 时:挂上这个组件,它会每帧读键盘,
    /// 直接把输入灌给 Movement.MoveInput / Movement.FocusHeld / Shooting.FireHeld。
    /// 注意:此时 PlayerInput 组件不要挂(避免双重输入)。
    ///
    /// 默认按键:
    ///   移动 = 方向键 / WASD
    ///   开火 = Z / Space
    ///   Focus = Left Shift
    /// </summary>
    public class LegacyInputDriver : MonoBehaviour
    {
        public KeyCode FireKey     = KeyCode.Z;
        public KeyCode FireKeyAlt  = KeyCode.Space;
        public KeyCode FocusKey    = KeyCode.LeftShift;

        void Update()
        {
            if (Player.Instance == null) return;

            float h = 0f, v = 0f;
            if (Input.GetKey(KeyCode.LeftArrow)  || Input.GetKey(KeyCode.A)) h -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) h += 1f;
            if (Input.GetKey(KeyCode.DownArrow)  || Input.GetKey(KeyCode.S)) v -= 1f;
            if (Input.GetKey(KeyCode.UpArrow)    || Input.GetKey(KeyCode.W)) v += 1f;

            if (Player.Instance.Movement != null)
            {
                Player.Instance.Movement.MoveInput = new Vector2(h, v);
                Player.Instance.Movement.FocusHeld = Input.GetKey(FocusKey);
            }

            if (Player.Instance.Shooting != null)
            {
                Player.Instance.Shooting.FireHeld =
                    Input.GetKey(FireKey) || Input.GetKey(FireKeyAlt);
            }
        }
    }
}