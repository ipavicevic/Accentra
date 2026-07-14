namespace Accentra;

class AccentEngine
{
    private const int LongPressMs = 500;
    private const int ConfirmMs = 1000;

    private enum State { Idle, WaitingForLongPress, AccentMode }

    public bool Enabled { get; set; } = true;

    private State _state = State.Idle;
    private ushort _trackedKey;
    private char[] _variants = [];
    private int _variantIndex;
    private bool _keyIsHeld;
    private readonly MacTimer _longPressTimer;
    private readonly MacTimer _confirmTimer;

    public AccentEngine()
    {
        _longPressTimer = new MacTimer { Interval = LongPressMs };
        _longPressTimer.Tick += OnLongPress;

        _confirmTimer = new MacTimer { Interval = ConfirmMs };
        _confirmTimer.Tick += OnConfirm;
    }

    // Returns true if the keystroke should be suppressed.
    public bool ProcessKey(ushort keyCode, char baseChar, bool isDown, bool isUp, bool isAutoRepeat, bool shiftHeld)
    {
        if (!Enabled) return false;

        switch (_state)
        {
            case State.Idle when isDown && !isAutoRepeat:
                var variants = baseChar != '\0' ? AccentMaps.GetVariants(baseChar, shiftHeld) : null;
                if (variants != null)
                {
                    _trackedKey = keyCode;
                    _variants = variants;
                    _state = State.WaitingForLongPress;
                    _longPressTimer.Start();
                }
                // Always pass through — the character appears in the app normally.
                return false;

            case State.WaitingForLongPress when isDown && keyCode == _trackedKey:
                // Suppress auto-repeat for the tracked key while waiting for long-press.
                return true;

            case State.WaitingForLongPress when isDown:
                // Different key pressed — cancel tracking; both the original character and
                // this new key pass through naturally.
                _longPressTimer.Stop();
                _state = State.Idle;
                return false;

            case State.WaitingForLongPress when isUp && keyCode == _trackedKey:
                // Quick release — character is already in the text field; just cancel.
                _longPressTimer.Stop();
                _state = State.Idle;
                return false;

            case State.AccentMode when isDown && keyCode == _trackedKey && _keyIsHeld:
                return true; // suppress auto-repeat

            case State.AccentMode when isDown && keyCode == _trackedKey:
                _keyIsHeld = true;
                CycleVariant();
                return true;

            case State.AccentMode when isUp && keyCode == _trackedKey:
                _keyIsHeld = false;
                return true;

            case State.AccentMode when isDown && !IsModifierKeyCode(keyCode):
                ExitAccentMode();
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
        _keyIsHeld = true;
        Logger.Log($"OnLongPress: replacing with '{_variants[0]}' (key={_trackedKey})");
        // The base character is already in the text field — replace it with the first accent.
        CharacterInjector.Replace(_variants[0]);
        RestartConfirmTimer();
    }

    private void OnConfirm(object? sender, EventArgs e)
    {
        // Don't exit while the key is physically held — auto-repeats would leak through.
        if (_keyIsHeld)
            RestartConfirmTimer();
        else
            ExitAccentMode();
    }

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

    private static bool IsModifierKeyCode(ushort kc) => kc is
        0x38 or 0x3C or // Shift, RShift
        0x3B or 0x3E or // Control, RControl
        0x3A or 0x3D or // Option, ROption
        0x37 or 0x36 or // Command, RCommand
        0x39;           // CapsLock
}
