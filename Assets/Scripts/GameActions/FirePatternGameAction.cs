using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.GameActions
{
    [Serializable, SRName("Game Action/Fire Pattern")]
    public sealed class FirePatternGameAction : GameAction
    {
        public FirePattern Pattern;
        [Min(1), Tooltip("发射次数。")]
        public int RepeatCount = 1;
        [Min(0f), Tooltip("两次发射之间的间隔。")]
        public float Interval;
        public override GameActionRuntime CreateRuntime(GameActionContext context) => new Runtime(Pattern, RepeatCount, Interval);

        sealed class Runtime : GameActionRuntime
        {
            readonly FirePattern _pattern;
            readonly int _repeatCount;
            readonly float _interval;
            int _fired;
            float _timer;
            public Runtime(FirePattern pattern, int repeatCount, float interval)
            { _pattern = pattern; _repeatCount = Mathf.Max(1, repeatCount); _interval = Mathf.Max(0f, interval); }
            public override bool IsComplete => _fired >= _repeatCount;
            public override void Start() => Fire();
            public override void Tick(float dt)
            {
                if (IsComplete || _interval <= 0f) return;
                _timer -= dt;
                while (_timer <= 0f && !IsComplete) { Fire(); _timer += _interval; }
            }
            void Fire()
            {
                var player = ShinySTG.Player.Player.Instance;
                var bomb = player != null ? player.Bomb : null;
                bomb?.CaptureBulletsBeforeBombFire();
                if (_pattern != null && player != null && player.Hitbox != null && BulletPool.Instance != null)
                    BulletPool.Instance.FireGroup(_pattern, player.transform.position, 0f, player.Hitbox);
                bomb?.CaptureBulletsAfterBombFire();
                _fired++;
            }
        }
    }
}
