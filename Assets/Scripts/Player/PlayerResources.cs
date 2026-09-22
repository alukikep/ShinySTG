using System;
using UnityEngine;

namespace ShinySTG.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerResources : MonoBehaviour
    {
        [Min(0), SerializeField, Tooltip("出生时的 Bomb 库存；本模块不负责释放 Bomb。")]
        int _initialBombs;
        public long Score { get; private set; }
        public int Bombs { get; private set; }
        public event Action<long> OnScoreChanged;
        public event Action<int> OnBombsChanged;

        void Awake() => Bombs = Mathf.Max(0, _initialBombs);

        public void AddScore(int amount)
        {
            if (amount <= 0) return;
            Score += Math.Min((long)amount, long.MaxValue - Score);
            OnScoreChanged?.Invoke(Score);
        }

        public void AddBombs(int amount = 1)
        {
            if (amount <= 0) return;
            Bombs += Math.Min(amount, int.MaxValue - Bombs);
            OnBombsChanged?.Invoke(Bombs);
        }

        /// <summary>将 Bomb 库存设置为指定数量，并通知 HUD。</summary>
        public void SetBombs(int amount)
        {
            int next = Mathf.Max(0, amount);
            if (next == Bombs) return;
            Bombs = next;
            OnBombsChanged?.Invoke(Bombs);
        }

        public bool TryConsumeBomb()
        {
            if (Bombs <= 0) return false;
            Bombs--;
            OnBombsChanged?.Invoke(Bombs);
            return true;
        }
    }
}
