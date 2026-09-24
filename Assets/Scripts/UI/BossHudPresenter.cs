using ShinySTG.EnemyAI.Boss;
using ShinySTG.Level;
using UnityEngine;

namespace ShinySTG.UI
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(BossHudView))]
    public sealed class BossHudPresenter : MonoBehaviour
    {
        [SerializeField, Tooltip("可指定 Boss；留空时自动绑定存活 Boss，并保持当前目标直到其不可用。")]
        Boss _boss;

        BossHudView _view;
        BossHealth _health;
        BossController _controller;
        LevelController _level;
        int _barIndex = -1;
        int _segmentIndex = -1;
        float _normalized = -1f;
        int _remainingBars = -1;
        int _resetFrame = -1;

        void OnEnable()
        {
            _view = GetComponent<BossHudView>();
            _view.Clear();
            // 首次绑定延后，确保 Boss 的 Awake 和关卡初始化已完成。
        }

        void LateUpdate()
        {
            var level = LevelController.Instance;
            if (!ReferenceEquals(level, _level))
            {
                UnbindLevel();
                _level = level;
                if (_level != null)
                {
                    _level.OnBossSpawned += HandleSpawned;
                    _level.OnLevelStart += HandleLevelStart;
                }
            }
            // Reset 使用延迟 Destroy，本帧不重新选择即将销毁的旧 Boss。
            if (_resetFrame == Time.frameCount) return;
            if (_boss != null)
            {
                Bind(_boss.isActiveAndEnabled && IsAvailable(_boss.Health) ? _boss.Health : null);
                if (_health != null) RefreshIfChanged();
                RefreshTimer();
                return;
            }
            if (IsAvailable(_health))
            {
                var currentBoss = _health.GetComponent<Boss>();
                if (currentBoss != null && currentBoss.isActiveAndEnabled)
                {
                    // 事件是主路径；轮询作为 prefab/碰撞替换或事件时序异常时的兜底。
                    RefreshIfChanged();
                    RefreshTimer();
                    return;
                }
            }
            Bind(null);
            foreach (var health in BossHealth.Alive)
            {
                if (!IsAvailable(health)) continue;
                var boss = health.GetComponent<Boss>();
                if (boss == null || !boss.isActiveAndEnabled) continue;
                Bind(boss.Health);
                break;
            }
        }

        static bool IsAvailable(BossHealth health) => health != null && health.isActiveAndEnabled && !health.IsDead;

        void HandleSpawned(GameObject go)
        {
            if (_boss != null || IsAvailable(_health) || _resetFrame == Time.frameCount || go == null) return;
            var boss = go.GetComponent<Boss>();
            if (boss != null && boss.isActiveAndEnabled && IsAvailable(boss.Health)) Bind(boss.Health);
        }

        void HandleLevelStart(LevelDefinition definition)
        {
            Bind(null);
            _resetFrame = Time.frameCount;
        }

        void Bind(BossHealth health)
        {
            if (ReferenceEquals(health, _health)) return;
            UnbindHealth();
            _view.Clear();
            _health = health;
            if (_health == null) return;
            _controller = _health.GetComponent<BossController>();
            _health.OnHealthChanged += Refresh;
            _health.OnDeath += HandleDeath;
            Refresh();
        }

        void Refresh()
        {
            if (!IsAvailable(_health)) { HandleDeath(); return; }
            int index = _health.CurrentBarIndex;
            float normalized = _health.CurrentHpNormalized;
            int remainingBars = _health.RemainingBarCount;
            if (index != _barIndex)
            {
                if (_health.UsesSegments)
                {
                    var segments = _health.Bars[index].Segments;
                    var widths = new float[segments.Length];
                    var colors = new Color[segments.Length];
                    for (int i = 0; i < segments.Length; i++)
                    {
                        widths[i] = segments[i].MaxHp / _health.MaxHp;
                        colors[i] = segments[i].Color;
                    }
                    _view.ConfigureSegments(widths, colors);
                }
                else _view.ConfigureSegments(null, null);
            }
            _view.SetHealth(normalized, remainingBars, index != _barIndex ||
                _health.CurrentSegmentIndex != _segmentIndex || _health.IsCurrentSegmentEmpty);
            _segmentIndex = _health.CurrentSegmentIndex;
            _barIndex = index;
            _normalized = normalized;
            _remainingBars = remainingBars;
            RefreshTimer();
        }

        void RefreshTimer()
        {
            float seconds = 0f;
            bool available = _controller != null && _controller.TryGetBarRemainingSeconds(out seconds);
            _view.SetTimer(seconds, available);
        }

        void RefreshIfChanged()
        {
            if (!IsAvailable(_health)) { HandleDeath(); return; }
            float normalized = _health.CurrentHpNormalized;
            int remainingBars = _health.RemainingBarCount;
            if (_health.CurrentBarIndex != _barIndex ||
                _health.CurrentSegmentIndex != _segmentIndex ||
                !Mathf.Approximately(normalized, _normalized) || remainingBars != _remainingBars)
                Refresh();
        }

        void HandleDeath()
        {
            UnbindHealth();
            _view.Clear();
        }

        void OnDisable()
        {
            UnbindHealth();
            UnbindLevel();
            _resetFrame = -1;
            if (_view != null) _view.Clear();
        }

        void UnbindHealth()
        {
            if (!ReferenceEquals(_health, null))
            {
                _health.OnHealthChanged -= Refresh;
                _health.OnDeath -= HandleDeath;
            }
            _health = null;
            _controller = null;
            _barIndex = -1;
            _segmentIndex = -1;
            _normalized = -1f;
            _remainingBars = -1;
        }

        void UnbindLevel()
        {
            if (!ReferenceEquals(_level, null))
            {
                _level.OnBossSpawned -= HandleSpawned;
                _level.OnLevelStart -= HandleLevelStart;
            }
            _level = null;
        }
    }
}
