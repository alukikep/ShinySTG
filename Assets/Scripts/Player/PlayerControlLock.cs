using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShinySTG.Player
{
    /// <summary>控制锁按持有者叠加，覆盖期间生成的玩家；释放旧令牌不会影响新令牌。</summary>
    public static class PlayerControlLock
    {
        static readonly HashSet<Token> _tokens = new();
        public static bool IsLocked => _tokens.Count > 0;
        public static int Revision { get; private set; }
        public static int LastReleaseFrame { get; private set; } = -1;

        public static IDisposable Acquire()
        {
            var token = new Token();
            _tokens.Add(token);
            Revision++;
            return token;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            _tokens.Clear();
            Revision = 0;
            LastReleaseFrame = -1;
        }

        sealed class Token : IDisposable
        {
            public void Dispose()
            {
                if (!_tokens.Remove(this)) return;
                Revision++;
                if (!IsLocked) LastReleaseFrame = Time.frameCount;
            }
        }
    }
}
