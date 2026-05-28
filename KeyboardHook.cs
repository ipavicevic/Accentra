using System.Runtime.InteropServices;

namespace Accentra;

class KeyboardHook : IDisposable
{
    private IntPtr _hookHandle;
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private readonly AccentEngine _engine;

    public KeyboardHook(AccentEngine engine)
    {
        _engine = engine;
        _proc = HookCallback;
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
        Logger.Log(_hookHandle != IntPtr.Zero ? "Keyboard hook registered" : "Keyboard hook FAILED to register");
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

            if (data.dwExtraInfo != NativeMethods.AccentraSentinel)
            {
                int msg = (int)wParam;
                bool isDown = msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
                bool isUp = msg is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;

                if (_engine.ProcessKey(data.vkCode, data.scanCode, isDown, isUp))
                    return (IntPtr)1;
            }
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }
}
