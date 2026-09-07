using UnityEngine;

namespace ShinySTG.Player
{
    /// <summary>
    /// 八方向移动 + Focus 低速模式。
    /// MoveInput 由 Player.OnPlayer 推过来(-1..1 的 Vector2)。
    /// FocusHeld 由 Player.OnFocus 推过来(true 时速度降到 FocusSpeed)。
    /// 活动范围用矩形 X / Y 范围 clamp,避免飞出屏幕。
    /// </summary>
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Speed")]
        [Tooltip("正常移动速度(单位/秒)。")]
        public float Speed = 6f;

        [Tooltip("Focus(集中)模式速度倍率(0..1)。经典 0.5。")]
        [Range(0.05f, 1f)]
        public float FocusSpeedMultiplier = 0.5f;

        [Header("Bounds (世界坐标矩形活动区)")]
        public float MinX = -3.5f;
        public float MaxX =  3.5f;
        public float MinY = -4.5f;
        public float MaxY =  4.5f;

        // 由 Player.OnPlayer 写入
        public Vector2 MoveInput { get; set; }
        // 由 Player.OnFocus 写入
        public bool FocusHeld { get; set; }

        public float CurrentSpeed => Speed * (FocusHeld ? FocusSpeedMultiplier : 1f);

        void Update()
        {
            Vector2 m = MoveInput;
            if (m.sqrMagnitude > 1f) m.Normalize(); // 八方向圆死区:超过 1 也按 1

            Vector3 delta = new Vector3(m.x, m.y, 0f) * (CurrentSpeed * Time.deltaTime);
            transform.position += delta;

            // 矩形 clamp
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, MinX, MaxX);
            p.y = Mathf.Clamp(p.y, MinY, MaxY);
            transform.position = p;
        }
    }
}