using UnityEngine;

namespace ShinySTG.Player
{
    /// <summary>
    /// 玩家持续射击。直接复用项目现有的 FirePattern / BulletPool 体系:
    /// 每帧根据 FireRate 调用 BulletPool.FireGroup(pattern, position, rotation)。
    ///
    /// 设计要点:
    ///   - MainPatterns: 自机主弹数组,按火力级取对应索引(或取全部轮流喷,见循环模式)
    ///   - LoopThroughPatterns: true = 所有主弹每帧轮流喷一次(适合"火力升高 → 多发")
    ///   - LoopThroughPatterns: false = 按 PowerLevel 索引取唯一一个主弹
    ///   - BaseOffset / SpreadAngle: 备用,适合"双发并列 / 扇形开火"
    ///
    /// 与子机的关系:子机的开火完全由 PlayerOptions 各自负责,这里只管本体弹幕。
    /// </summary>
    public class PlayerShooting : MonoBehaviour
    {
        [Header("Audio (optional — 留空则不播放)")]
        [Tooltip("开火音 SFX cue(留空 = 不播)。\n" +
                 "建议在 cue 上设 Cooldown≈0.02s 防止「哒哒哒」一片,或 MaxVoices=2~3。")]
        [SerializeField] ShinySTG.Audio.SfxCue _shootSfx;

        [Header("Fire Patterns (复用现有 FirePattern 体系)")]
        [Tooltip("自机主弹。索引 = 火力级对应的弹幕槽位。")]
        public FirePattern[] MainPatterns;

        [Tooltip("true = 每帧按 MainPatterns 顺序各喷一次(火力级越高喷得越多)。" +
        "false = 只喷 MainPatterns[min(PowerLevel, length-1)]。")]
        public bool LoopThroughPatterns = true;

        [Tooltip("按键按下时才发射(由 Player.OnAttack 推过来)。")]
        public bool FireHeld { get; set; }

        [Tooltip("每秒发射轮数。LoopThroughPatterns=true 时即\"每秒多少轮\"。" +
        "设大一点(STG 经典 12~20)。")]
        public float FireRate = 12f;

        float _cooldown;

        void Update()
        {
            if (!FireHeld) return;
            if (MainPatterns == null || MainPatterns.Length == 0) return;

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f) return;
            _cooldown = 1f / Mathf.Max(0.0001f, FireRate);

            if (LoopThroughPatterns)
            {
                // 玩家发射 → 子弹阵营由 Player.Instance.Hitbox.Team 自动透传(通常是 Player)
                var ownerHb = Player.Instance?.Hitbox;
                foreach (var p in MainPatterns)
                    if (p != null) BulletPool.Instance.FireGroup(p, transform.position, 0f, ownerHb);
            }
            else
            {
                // PlayerHealth 可能还没 Awake,默认 PowerLevel = 0 也合法
                var pl = Player.Instance != null && Player.Instance.Health != null
                    ? Player.Instance.Health.PowerLevel : 0;
                var p = MainPatterns[Mathf.Clamp(pl, 0, MainPatterns.Length - 1)];
                var ownerHb = Player.Instance?.Hitbox;
                if (p != null) BulletPool.Instance.FireGroup(p, transform.position, 0f, ownerHb);
            }

            // 开火音 —— 放在 FireGroup 之后,不影响子弹创建;只在「确实开火」时触发。
            // Cooldown / MaxVoices 在 cue 上设。
            if (_shootSfx != null)
                ShinySTG.Audio.AudioMix.PlaySfx(_shootSfx, position: (Vector2)transform.position);
        }
    }
}