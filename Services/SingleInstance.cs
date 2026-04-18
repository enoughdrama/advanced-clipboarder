namespace Clipboarder.Services;

public static class SingleInstance
{
    private const string MutexName = "Clipboarder.SingleInstance.v1";
    private const string EventName = "Clipboarder.ShowRequest.v1";

    private static Mutex? _mutex;
    private static EventWaitHandle? _signal;
    private static Thread? _listener;
    private static volatile bool _stopped;

    public static bool AcquireOrSignal()
    {
        _mutex = new Mutex(initiallyOwned: false, name: MutexName, createdNew: out var created);
        if (!created)
        {
            try
            {
                var evt = EventWaitHandle.OpenExisting(EventName);
                evt.Set();
            }
            catch { }
            return false;
        }
        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        return true;
    }

    public static void ListenForShowRequest(Action onRequested)
    {
        if (_signal is null) return;
        _listener = new Thread(() =>
        {
            while (!_stopped)
            {
                try { _signal.WaitOne(); }
                catch { return; }
                if (_stopped) return;
                onRequested();
            }
        }) { IsBackground = true, Name = "Clipboarder.ShowListener" };
        _listener.Start();
    }

    public static void Release()
    {
        _stopped = true;
        try { _signal?.Set(); } catch { }
        try { _signal?.Dispose(); } catch { }
        try { _mutex?.ReleaseMutex(); } catch { }
        try { _mutex?.Dispose(); } catch { }
    }
}
