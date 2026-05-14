namespace Accentra;

class AccentEngine
{
    private const int LongPressMs = 500;
    private const int ConfirmMs = 1000;

    private enum State { Idle, WaitingForLongPress, AccentMode }

    public bool Enabled { get; set; } = true;

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
}
