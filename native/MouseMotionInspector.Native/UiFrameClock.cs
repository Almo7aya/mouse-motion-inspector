namespace MouseMotionInspector.Native;

// Post at most one frame to the UI thread. A slow frame never creates a backlog
// of stale callbacks, and input capture remains on its own listener thread.
internal sealed class UiFrameClock : IDisposable
{
    private readonly Control owner;
    private readonly Action tick;
    private readonly System.Threading.Timer timer;
    private int pending, disposed;
    internal UiFrameClock(Control owner, Action tick)
    {
        this.owner = owner; this.tick = tick;
        timer = new System.Threading.Timer(Post, null, 0, 15);
    }
    internal void SetMinimized(bool minimized) => timer.Change(0, minimized ? 50 : 15);
    private void Post(object? state)
    {
        if (Volatile.Read(ref disposed) != 0 || Interlocked.CompareExchange(ref pending, 1, 0) != 0) return;
        try
        {
            owner.BeginInvoke((Action)(() =>
            {
                try { if (Volatile.Read(ref disposed) == 0 && !owner.IsDisposed) tick(); }
                finally { Volatile.Write(ref pending, 0); }
            }));
        }
        catch (InvalidOperationException) { Volatile.Write(ref pending, 0); }
    }
    public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) timer.Dispose(); }
}
