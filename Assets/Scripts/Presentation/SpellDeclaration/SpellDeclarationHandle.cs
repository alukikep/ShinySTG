using System;

namespace ShinySTG.Presentation.SpellDeclaration
{
    public sealed class SpellDeclarationHandle : IDisposable
    {
        Action _cancel;
        public bool IsComplete { get; private set; }
        public bool IsCancelled { get; private set; }
        public Exception Failure { get; private set; }

        internal void Bind(Action cancel) => _cancel = cancel;
        internal void Finish(bool cancelled = false, Exception failure = null)
        {
            if (IsComplete) return;
            IsComplete = true;
            IsCancelled = cancelled;
            Failure = failure;
            _cancel = null;
        }
        public void Cancel() { if (!IsComplete) _cancel?.Invoke(); }
        public void Dispose() => Cancel();
    }
}
