using UnityEngine;
using ShinySTG.Stage; // BoundsService —— 活动区配置从场景单例读

namespace ShinySTG.Player
{
    /// <summary>
    /// 八方向移动 + Focus 低速模式。
    /// MoveInput 由 Player.OnPlayer 推过来(-1..1 的 Vector2)。
    /// FocusHeld 由 Player.OnFocus 推过来(true 时速度降到 FocusSpeed)。
    /// 活动范围统一从 <see cref="BoundsService.PlayableArea"/> 读,不再硬编码。
    /// </summary>
    public class PlayerMovement : MonoBehaviour
    {
        // ─── Fallback 默认值(场景里没挂 BoundsService 时用,保证组件独立可跑) ───
        // 与旧字段 MinX/MaxX/MinY/MaxY 默认值 ±3.5/±3.5/±4.5/±4.5 一致 —— 迁移无感。
        const float FALLBACK_MIN_X = -3.5f;
        const float FALLBACK_MAX_X =  3.5f;
        const float FALLBACK_MIN_Y = -4.5f;
        const float FALLBACK_MAX_Y =  4.5f;

        [Header("Speed")]
        [Tooltip("正常移动速度(单位/秒)。")]
        public float Speed = 6f;

        [Tooltip("Focus(集中)模式速度倍率(0..1)。经典 0.5。")]
        [Range(0.05f, 1f)]
        public float FocusSpeedMultiplier = 0.5f;

        // 由 Player.OnPlayer 写入
        Vector2 _moveInput;
        public Vector2 MoveInput
        {
            get => PlayerControlLock.IsLocked ? Vector2.zero : _moveInput;
            set => _moveInput = value;
        }
        // 由 Player.OnFocus 写入
        bool _focusHeld;
        public bool FocusHeld
        {
            get => !PlayerControlLock.IsLocked && _focusHeld;
            set => _focusHeld = value;
        }

        void OnDisable() { _moveInput = Vector2.zero; _focusHeld = false; }

        public float CurrentSpeed => Speed * (FocusHeld ? FocusSpeedMultiplier : 1f);

        void Update()
        {
            Vector2 m = MoveInput;
            if (m.sqrMagnitude > 1f) m.Normalize(); // 八方向圆死区:超过 1 也按 1

            Vector3 delta = new Vector3(m.x, m.y, 0f) * (CurrentSpeed * Time.deltaTime);
            transform.position += delta;

            // 矩形 clamp —— 优先读 BoundsService.Instance;无则用内置默认值,保留历史行为。
            var bs = BoundsService.Instance;
            Vector3 p = transform.position;
            if (bs != null)
            {
                Vector2 clamped = bs.ClampToPlayable((Vector2)p);
                p.x = clamped.x;
                p.y = clamped.y;
            }
            else
            {
                p.x = Mathf.Clamp(p.x, FALLBACK_MIN_X, FALLBACK_MAX_X);
                p.y = Mathf.Clamp(p.y, FALLBACK_MIN_Y, FALLBACK_MAX_Y);
            }
            transform.position = p;
        }
    }
}
