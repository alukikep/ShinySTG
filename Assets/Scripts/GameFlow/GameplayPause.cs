using System;
using ShinySTG.Level;
using ShinySTG.Player;
using UnityEngine;

namespace ShinySTG.GameFlow
{
    /// <summary>暂停菜单的世界冻结；加载黑幕使用自己的时间控制。</summary>
    public sealed class GameplayPause : IDisposable
    {
        public static bool IsPaused { get; private set; }
        public static int LastResumeFrame { get; private set; } = -1;
        public static bool BlocksDialogueInput => IsPaused || Time.frameCount == LastResumeFrame;
        readonly float _previousTimeScale;
        readonly IDisposable _control;
        readonly IDisposable _battle;
        bool _disposed;

        public GameplayPause()
        {
            if (IsPaused) throw new InvalidOperationException("已有暂停菜单持有游戏时间。");
            _previousTimeScale = Time.timeScale;
            _control = PlayerControlLock.Acquire();
            _battle = BattleRestriction.Acquire();
            IsPaused = true;
            Time.timeScale = 0f;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Time.timeScale = _previousTimeScale;
            IsPaused = false;
            LastResumeFrame = Time.frameCount;
            _control.Dispose();
            _battle.Dispose();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { IsPaused = false; LastResumeFrame = -1; }
    }
}
