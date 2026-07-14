using System.Runtime.InteropServices;

namespace Accentra;

// Fires Tick on the main thread (via GCD main queue), one-shot per Start() call.
// Mimics System.Windows.Forms.Timer semantics: start → tick → stop in callback.
class MacTimer : IDisposable
{
    private System.Threading.Timer? _timer;
    private static readonly MacNativeMethods.DispatchFunction _dispatcher = DispatchTick;

    public int Interval { get; set; } = 100;
    public event EventHandler? Tick;

    private static void DispatchTick(IntPtr context)
    {
        var handle = GCHandle.FromIntPtr(context);
        var timer = (MacTimer)handle.Target!;
        handle.Free();
        timer.Tick?.Invoke(timer, EventArgs.Empty);
    }

    private void OnElapsed(object? _)
    {
        var handle = GCHandle.Alloc(this);
        MacNativeMethods.dispatch_async_f(
            MacNativeMethods.dispatch_get_main_queue(),
            GCHandle.ToIntPtr(handle),
            _dispatcher);
    }

    public void Start()
    {
        _timer?.Dispose();
        _timer = new System.Threading.Timer(OnElapsed, null, Interval, Timeout.Infinite);
    }

    public void Stop() => _timer?.Change(Timeout.Infinite, Timeout.Infinite);

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
