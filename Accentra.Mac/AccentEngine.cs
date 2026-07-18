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
    public bool ProcessKey(ushort keyCode, char baseChar, bool isDown, bool isUp, bool isAutoRepeat, bool upper)
    {
        if (!Enabled) return false;

        switch (_state)
        {
            case State.Idle when isDown && !isAutoRepeat:
                var variants = baseChar != '\0' ? AccentMaps.GetVariants(baseChar, upper) : null;
                if (variants == null)
                    return false; // not a mapped key — pass through, macOS handles it natively

                _trackedKey = keyCode;
                _variants = variants;
                _state = State.WaitingForLongPress;
                _longPressTimer.Start();

                // Suppress the physical key-down and re-type the base character ourselves
                // as a discrete keystroke. The app then never sees a held key, so its
                // press-and-hold picker never engages — the picker is what corrupts the
                // text (it takes the character into a pending state that our later
                // backspace and the picker's own resolution both act on, eating a
                // neighbouring character). Nothing downstream means nothing to corrupt.
                CharacterInjector.TypeKey(keyCode, upper);
                return true;

            case State.WaitingForLongPress when isDown && keyCode == _trackedKey:
                // Suppress auto-repeat for the tracked key while waiting for long-press.
                return true;

            case State.WaitingForLongPress when isDown:
                // Different key pressed — cancel tracking and handle it as a fresh press,
                // so a mapped key is suppressed-and-retyped rather than passed through.
                _longPressTimer.Stop();
                _state = State.Idle;
                return ProcessKey(keyCode, baseChar, isDown, isUp, isAutoRepeat, upper);

            case State.WaitingForLongPress when isUp && keyCode == _trackedKey:
                // Quick release — the character is already typed. Suppress the key-up too:
                // the app never saw the matching key-down, so it must not see the up.
                _longPressTimer.Stop();
                _state = State.Idle;
                return true;

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
                // Confirms the accent; handle the new key as a fresh press so a mapped
                // one is suppressed-and-retyped rather than passed through to the picker.
                ExitAccentMode();
                return ProcessKey(keyCode, baseChar, isDown, isUp, isAutoRepeat, upper);

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
