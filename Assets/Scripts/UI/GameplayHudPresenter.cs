using UnityEngine;
using ShinySTG.Player;
using PlayerController = ShinySTG.Player.Player;

namespace ShinySTG.UI
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(GameplayHudView))]
    public sealed class GameplayHudPresenter : MonoBehaviour
    {
        [SerializeField, Tooltip("留空时跟随当前 Player.Instance。")]
        PlayerController _player;
        GameplayHudView _view;
        PlayerHealth _health;
        PlayerResources _resources;

        void Awake() => _view = GetComponent<GameplayHudView>();
        void OnEnable()
        {
            if (_view == null) _view = GetComponent<GameplayHudView>();
            _view.Clear();
            // 首次读取放在 LateUpdate，确保玩家所有 Awake 已完成。
        }

        void LateUpdate()
        {
            var player = _player != null ? _player : PlayerController.Instance;
            var health = player != null && player.isActiveAndEnabled ? player.Health : null;
            var resources = player != null && player.isActiveAndEnabled ? player.Resources : null;
            if (ReferenceEquals(health, _health) && ReferenceEquals(resources, _resources)) return;
            Unbind();
            _view.Clear();
            _health = health;
            _resources = resources;
            if (_health != null)
            {
                _health.OnLivesChanged += _view.SetLives;
                _health.OnPowerChanged += UpdatePower;
                _health.OnGraze += _view.SetGraze;
                _view.SetLives(_health.Lives);
                UpdatePower(_health.PowerUnits);
                _view.SetGraze(_health.GrazeCount);
            }
            if (_resources != null)
            {
                _resources.OnScoreChanged += _view.SetScore;
                _resources.OnBombsChanged += _view.SetBombs;
                _view.SetScore(_resources.Score);
                _view.SetBombs(_resources.Bombs);
            }
        }

        void UpdatePower(int units)
        {
            if (_health != null) _view.SetPower(units, _health.MaxPower);
        }

        void OnDisable() => Unbind();

        void Unbind()
        {
            // CLR 引用检查也允许从已销毁的 Unity 对象解除托管事件。
            if (!ReferenceEquals(_health, null))
            {
                _health.OnLivesChanged -= _view.SetLives;
                _health.OnPowerChanged -= UpdatePower;
                _health.OnGraze -= _view.SetGraze;
            }
            if (!ReferenceEquals(_resources, null))
            {
                _resources.OnScoreChanged -= _view.SetScore;
                _resources.OnBombsChanged -= _view.SetBombs;
            }
            _health = null;
            _resources = null;
        }
    }
}
