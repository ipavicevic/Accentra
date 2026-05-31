using System.Text;

namespace Accentra;

class AccentEngine
{
    private const int LongPressMs = 500;
    private const int ConfirmMs = 1000;

    private enum State { Idle, WaitingForLongPress, AccentMode }

    public bool Enabled { get; set; } = true;

    private State _state = State.Idle;
    private uint _trackedVk;
    private uint _lastDownVk;  // detects auto-repeat for unmapped keys
    private char[] _variants = [];
    private int _variantIndex;
    private bool _keyIsHeld;   // true while the tracked key hasn't been released yet
    private readonly System.Windows.Forms.Timer _longPressTimer;
    private readonly System.Windows.Forms.Timer _confirmTimer;

    // Reusable buffer for ToUnicodeEx — only called from the hook thread
    private static readonly StringBuilder _uniBuffer = new(4);

    public AccentEngine()
    {
        _longPressTimer = new System.Windows.Forms.Timer { Interval = LongPressMs };
        _longPressTimer.Tick += OnLongPress;

        _confirmTimer = new System.Windows.Forms.Timer { Interval = ConfirmMs };
        _confirmTimer.Tick += OnConfirm;
    }

    // Returns true if the keystroke should be suppressed.
    public bool ProcessKey(uint vkCode, uint scanCode, bool isDown, bool isUp)
    {
        if (!Enabled) return false;
        if (IsCtrlHeld() || IsAltHeld()) return false;

        switch (_state)
        {
            case State.Idle when isDown:
                var baseChar = ResolveChar(vkCode, scanCode);
                var variants = baseChar != '\0' ? AccentMaps.GetVariants(baseChar, IsShiftHeld()) : null;
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
                    if (isDown && vkCode == _lastDownVk && IsMappableKey(vkCode))
                        return true; // suppress auto-repeat for unmapped mappable keys
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

    // Resolves the base (unshifted) character for a vkCode+scanCode pair.
    // A-Z and 0-9 are layout-independent; OEM keys use ToUnicodeEx with Accentra's own thread HKL.
    private static char ResolveChar(uint vkCode, uint scanCode)
    {
        if (vkCode is >= 0x41 and <= 0x5A)
            return (char)(vkCode + 0x20); // A-Z → a-z

        if (vkCode is >= 0x30 and <= 0x39)
            return (char)vkCode; // 0-9

        // OEM/punctuation: resolve via the keyboard layout active on Accentra's own thread.
        // Include actual shift state so Shift+5 → '%' rather than '5'.
        // This follows the system-wide layout (default Windows behaviour). Per-app layout
        // switching is a documented limitation, same as the existing UAC limitation.
        var hkl = NativeMethods.GetKeyboardLayout(0);
        var keyState = new byte[256];
        if (IsShiftHeld()) keyState[NativeMethods.VK_SHIFT] = 0x80;
        _uniBuffer.Clear();
        int result = NativeMethods.ToUnicodeEx(
            vkCode, scanCode, keyState, _uniBuffer, _uniBuffer.Capacity,
            NativeMethods.TOUNICODEEX_NO_DEAD_KEY, hkl);
        return result > 0 ? _uniBuffer[0] : '\0';
    }

    private void OnLongPress(object? sender, EventArgs e)
    {
        _longPressTimer.Stop();
        _state = State.AccentMode;
        _variantIndex = 0;
        _keyIsHeld = true;
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

    private static bool IsCtrlHeld() =>
        (NativeMethods.GetKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;

    private static bool IsAltHeld() =>
        (NativeMethods.GetKeyState(NativeMethods.VK_MENU) & 0x8000) != 0;

    private static bool IsMappableKey(uint vk) =>
        vk is (>= 0x41 and <= 0x5A) or (>= 0x30 and <= 0x39);

    private static bool IsModifierKey(uint vk) => (int)vk is
        NativeMethods.VK_SHIFT or NativeMethods.VK_CONTROL or NativeMethods.VK_MENU or
        NativeMethods.VK_CAPITAL or NativeMethods.VK_LSHIFT or NativeMethods.VK_RSHIFT or
        NativeMethods.VK_LCONTROL or NativeMethods.VK_RCONTROL or
        NativeMethods.VK_LMENU or NativeMethods.VK_RMENU;
}
