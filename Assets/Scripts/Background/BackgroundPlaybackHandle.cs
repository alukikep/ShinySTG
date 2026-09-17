namespace ShinySTG.Background
{
    public enum BackgroundPlaybackStatus { Playing, Completed, Cancelled, Failed }

    /// <summary>只终止本次播放，不回调控制器，因此旧句柄不会取消新播放。</summary>
    public sealed class BackgroundPlaybackHandle
    {
        public BackgroundPlaybackStatus Status { get; private set; } = BackgroundPlaybackStatus.Playing;
        public bool IsComplete => Status != BackgroundPlaybackStatus.Playing;
        public string Failure { get; private set; }

        internal BackgroundPlaybackHandle() { }

        public void Cancel()
        {
            if (!IsComplete) Status = BackgroundPlaybackStatus.Cancelled;
        }

        internal void Complete()
        {
            if (!IsComplete) Status = BackgroundPlaybackStatus.Completed;
        }

        internal void Fail(string reason)
        {
            if (IsComplete) return;
            Failure = reason;
            Status = BackgroundPlaybackStatus.Failed;
        }
    }
}
