using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShinySTG.Level
{
    /// <summary>叠加式战斗限制；不改变移动、低速、游戏时间或对话控制锁。</summary>
    public static class BattleRestriction
    {
        static readonly HashSet<Token> _tokens = new();
        public static bool IsActive => _tokens.Count > 0;

        public static IDisposable Acquire()
        {
            var token = new Token();
            _tokens.Add(token);
            return token;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => _tokens.Clear();

        sealed class Token : IDisposable
        {
            public void Dispose() => _tokens.Remove(this);
        }
    }
}
