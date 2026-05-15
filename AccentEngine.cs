namespace Accentra;

class AccentEngine
{
    private const int LongPressMs = 500;
    private const int ConfirmMs = 1000;

    private enum State { Idle, WaitingForLongPress, AccentMode }

    public bool Enabled { get; set; } = true;
    public bool IsLatinLayout => IsCurrentLayoutLatin();

    private State _state = State.Idle;
    private uint _trackedVk;
    private uint _lastDownVk;  // detects auto-repeat for unmapped letter keys
    private char[] _variants = [];
    private int _variantIndex;
    private bool _keyIsHeld;   // true while the tracked key hasn't been released yet
    private readonly System.Windows.Forms.Timer _longPressTimer;
    private readonly System.Windows.Forms.Timer _confirmTimer;

    public AccentEngine()
    {
        _longPressTimer = new System.Windows.Forms.Timer { Interval = LongPressMs };
        _longPressTimer.Tick += OnLongPress;

        _confirmTimer = new System.Windows.Forms.Timer { Interval = ConfirmMs };
        _confirmTimer.Tick += OnConfirm;
    }

    // Returns true if the keystroke should be suppressed.
    public bool ProcessKey(uint vkCode, bool isDown, bool isUp)
    {
        if (!Enabled) return false;
        if (!IsCurrentLayoutLatin()) return false;

        switch (_state)
        {
            case State.Idle when isDown:
                var variants = AccentMaps.GetVariants(vkCode, IsShiftHeld());
                if (variants != null)
                {
                    _trackedVk = vkCode;
                    _variants = variants;
                    _lastDownVk = vkCode;
                    _state = State.WaitingForLongPress;
                    _longPressTimer.Start();
                }
                else
                {
                    if (isDown && vkCode == _lastDownVk && IsLetterKey(vkCode))
                        return true; // suppress auto-repeat for unmapped letter keys
                    _lastDownVk = vkCode;
                }
                return false;

            case State.WaitingForLongPress when isDown && vkCode == _trackedVk:
                return true; // suppress auto-repeat while waiting

            case State.WaitingForLongPress when isDown:
                _longPressTimer.Stop();
                _state = State.Idle;
                return false;

            case State.WaitingForLongPress when isUp && vkCode == _trackedVk:
                _longPressTimer.Stop();
                _state = State.Idle;
                _lastDownVk = 0;
                return false;

            case State.AccentMode when isDown && vkCode == _trackedVk && _keyIsHeld:
                return true; // suppress auto-repeat until the key is released

            case State.AccentMode when isDown && vkCode == _trackedVk:
                _keyIsHeld = true;
                CycleVariant();
                return true;

            case State.AccentMode when isUp && vkCode == _trackedVk:
                _keyIsHeld = false;
                return false;

            case State.AccentMode when isDown && !IsModifierKey(vkCode):
                ExitAccentMode();
                return false;

            case State.Idle when isUp:
                _lastDownVk = 0;
                return false;

            default:
                return false;
        }
    }

    private void OnLongPress(object? sender, EventArgs e)
    {
        _longPressTimer.Stop();
        _state = State.AccentMode;
        _variantIndex = 0;
        _keyIsHeld = true;  // key is still physically held when the timer fires
        CharacterInjector.Replace(_variants[0]);
        RestartConfirmTimer();
    }

    private void OnConfirm(object? sender, EventArgs e) => ExitAccentMode();

    private void ExitAccentMode()
    {
        _confirmTimer.Stop();
        _state = State.Idle;
    }

    private void CycleVariant()
    {
        _variantIndex = (_variantIndex + 1) % _variants.Length;
        CharacterInjector.Replace(_variants[_variantIndex]);
        RestartConfirmTimer();
    }

    private void RestartConfirmTimer()
    {
        _confirmTimer.Stop();
        _confirmTimer.Start();
    }

    private static bool IsShiftHeld() =>
        (NativeMethods.GetKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;

    private static bool IsLetterKey(uint vk) => vk is (>= 0x41 and <= 0x5A) or (>= 0x30 and <= 0x39); // VK_A–VK_Z, VK_0–VK_9

    private static bool IsModifierKey(uint vk) => (int)vk is
        NativeMethods.VK_SHIFT or NativeMethods.VK_CONTROL or NativeMethods.VK_MENU or
        NativeMethods.VK_CAPITAL or NativeMethods.VK_LSHIFT or NativeMethods.VK_RSHIFT or
        NativeMethods.VK_LCONTROL or NativeMethods.VK_RCONTROL or
        NativeMethods.VK_LMENU or NativeMethods.VK_RMENU;

    private IntPtr _cachedHkl = IntPtr.Zero;
    private bool _cachedIsLatin = true;

    // Checks whether the foreground window's keyboard layout produces Latin characters.
    // The HKL pointer is checked on every call (three cheap Win32 reads); ToUnicodeEx
    // is only invoked when the layout actually changes.
    private bool IsCurrentLayoutLatin()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        var tid = NativeMethods.GetWindowThreadProcessId(hwnd, IntPtr.Zero);
        var hkl = NativeMethods.GetKeyboardLayout(tid);

        if (hkl == _cachedHkl) return _cachedIsLatin;

        var keyState = new byte[256]; // all keys up, no modifiers
        var buf = new char[4];
        int result = NativeMethods.ToUnicodeEx(0x41, 0, keyState, buf, buf.Length, 0, hkl);
        // result > 0 means a character was produced; check it's within Latin Extended-B (≤ U+024F)
        _cachedHkl = hkl;
        _cachedIsLatin = result <= 0 || buf[0] <= 'ɏ';
        Logger.Log($"Layout changed: hkl=0x{hkl:X} ToUnicodeEx result={result} char={(result > 0 ? $"U+{(int)buf[0]:X4} '{buf[0]}'" : "none")} isLatin={_cachedIsLatin}");
        return _cachedIsLatin;
    }
}
