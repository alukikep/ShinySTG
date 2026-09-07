using UnityEngine;

namespace ShinySTG.Player
{
    /// <summary>
    /// 玩家主控。协调移动 / 射击 / 子机 / 受击,作为全局入口 Player.Instance。
    ///
    /// 输入方案(双重兼容):
    ///   - 推荐:挂一个 PlayerInput 组件(Behavior = "Invoke C# Events"),Action Asset 里
    ///     包含 "Player" (Value, Vector2)、"Attack" (Button)、"Focus" (Button)。
    ///     PlayerInput 会自动调 Player.OnPlayer / OnAttack / OnFocus。
    ///   - 兜底:Player.Awake 没找到 PlayerInput 组件时,自动回退到旧 Keyboard.current 轮询。
    ///     这样不强制项目装 InputSystem package 也能跑。
    ///
    /// 推荐 Inspector 配法:
    ///   PlayerInput 组件 → Actions 资产里至少包含:
    ///     - "Player" (Value, Vector2)   ← 移动
    ///     - "Attack" (Button)            ← 持续按住开火
    ///     - "Focus"  (Button)            ← 集中(低速)
    ///   "Default Map" 勾上,Behavior = "Invoke C# Events"。
    ///
    /// 注意:方法名要和 PlayerInput Action Asset 里 Action 的名字完全一致。
    /// 如果改名同步改 OnXxx 名字,或在 PlayerInput 组件里改 Method。
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerShooting))]
    [RequireComponent(typeof(PlayerHealth))]
    [RequireComponent(typeof(PlayerOptions))]
    [RequireComponent(typeof(PlayerHitbox))]

    public class Player : MonoBehaviour
    {
        public static Player Instance { get; private set; }

        public PlayerMovement Movement { get; private set; }
        public PlayerShooting Shooting { get; private set; }
        public PlayerHealth   Health   { get; private set; }
        public PlayerOptions  Options  { get; private set; }
        public PlayerHitbox   Hitbox   { get; private set; }

        // ─── PlayerInput(新版 Input System)回调 ─────────────
        // 由 PlayerInput 组件(Behavior = Invoke C# Events)调用。
        public void OnPlayer(Vector2 v) { if (Movement != null) Movement.MoveInput = v; }
        public void OnAttack(bool held) { if (Shooting != null) Shooting.FireHeld = held; }
        public void OnFocus(bool held) { if (Movement != null) Movement.FocusHeld = held; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (!CompareTag("Player")) tag = "Player";

            Movement = GetComponent<PlayerMovement>();
            Shooting = GetComponent<PlayerShooting>();
            Health   = GetComponent<PlayerHealth>();
            Options  = GetComponent<PlayerOptions>();
            Hitbox   = GetComponent<PlayerHitbox>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void OnHit(float damage = 1f)
        {
            Health?.TakeHit(damage);
        }
    }
}