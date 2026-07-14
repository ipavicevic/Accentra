namespace Accentra;

class MacKeyboardHook : IDisposable
{
    private static AccentEngine? _engine;
    private static MacNativeMethods.CGEventTapCallBack? _callbackRef;
    private static IntPtr _tap; // static so the callback can re-enable it

    public MacKeyboardHook(AccentEngine engine)
    {
        _engine = engine;
        _callbackRef = HookCallback;

        ulong mask = MacNativeMethods.EventMask(MacNativeMethods.CGEventType.KeyDown)
                   | MacNativeMethods.EventMask(MacNativeMethods.CGEventType.KeyUp)
                   | MacNativeMethods.EventMask(MacNativeMethods.CGEventType.FlagsChanged);

        // kCGSessionEventTap with HeadInsert: intercept before the text system sees events.
        // Self-injected events (CharacterInjector) are tagged with AccentraSentinel so we
        // can skip them when they loop back through the tap.
        _tap = MacNativeMethods.CGEventTapCreate(
            MacNativeMethods.CGEventTapLocation.SessionEventTap,
            MacNativeMethods.CGEventTapPlacement.HeadInsertEventTap,
            MacNativeMethods.CGEventTapOptions.Default,
            mask,
            _callbackRef,
            IntPtr.Zero);

        if (_tap == IntPtr.Zero)
        {
            Logger.Log("CGEventTap creation failed — accessibility permission likely not granted");
            return;
        }

        var source = MacNativeMethods.CFMachPortCreateRunLoopSource(IntPtr.Zero, _tap, 0);
        MacNativeMethods.CFRunLoopAddSource(
            MacNativeMethods.CFRunLoopGetMain(),
            source,
            MacNativeMethods.kCFRunLoopDefaultMode);

        Logger.Log("Keyboard hook registered");
    }

    // Ring buffer for in-callback diagnostics — written without file I/O to avoid tap timeouts
    private static readonly string[] _ring = new string[64];
    private static int _ringHead;
    private static int _ringCount;

    private static void RingLog(string s)
    {
        _ring[_ringHead] = s;
        _ringHead = (_ringHead + 1) % _ring.Length;
        if (_ringCount < _ring.Length) _ringCount++;
    }

    // Dump ring buffer to the log file; called from the main thread outside the callback
    public static void FlushRing()
    {
        if (_ringCount == 0)
        {
            Logger.Log("FlushRing: idle");
            return;
        }
        var start = _ringCount < _ring.Length ? 0 : _ringHead;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < _ringCount; i++)
            sb.AppendLine(_ring[(start + i) % _ring.Length]);
        Logger.Log("=== hook ring ===\n" + sb + "=================");
        _ringCount = 0;
    }

    private static IntPtr HookCallback(IntPtr proxy, MacNativeMethods.CGEventType type, IntPtr @event, IntPtr userInfo)
    {
        // macOS disables the tap if our callback is too slow; re-enable it immediately
        if (type is MacNativeMethods.CGEventType.TapDisabledByTimeout
                 or MacNativeMethods.CGEventType.TapDisabledByUserInput)
        {
            RingLog($"TAP DISABLED ({type}), re-enabling");
            MacNativeMethods.CGEventTapEnable(_tap, true);
            return @event;
        }

        // Skip our own injected events so they don't loop back through the engine.
        // The window server stamps every posted event with the poster's PID — more
        // reliable than user-data tagging, which does not survive CGEventPost.
        if (MacNativeMethods.CGEventGetIntegerValueField(@event, MacNativeMethods.CGEventField.SourceUnixProcessID)
            == Environment.ProcessId)
        {
            RingLog($"skip injected ({type})");
            return @event;
        }

        if (type == MacNativeMethods.CGEventType.FlagsChanged)
            return @event;

        bool isDown = type == MacNativeMethods.CGEventType.KeyDown;
        bool isUp   = type == MacNativeMethods.CGEventType.KeyUp;

        var keyCode = (ushort)MacNativeMethods.CGEventGetIntegerValueField(@event, MacNativeMethods.CGEventField.KeyboardEventKeycode);
        bool isAutoRepeat = MacNativeMethods.CGEventGetIntegerValueField(@event, MacNativeMethods.CGEventField.KeyboardEventAutorepeat) != 0;

        ulong flags   = MacNativeMethods.CGEventGetFlags(@event);
        bool shiftHeld = (flags & MacNativeMethods.kCGEventFlagMaskShift) != 0;
        bool ctrlHeld  = (flags & MacNativeMethods.kCGEventFlagMaskControl) != 0;
        bool altHeld   = (flags & MacNativeMethods.kCGEventFlagMaskAlternate) != 0;
        bool cmdHeld   = (flags & MacNativeMethods.kCGEventFlagMaskCommand) != 0;

        char baseChar = ResolveChar(@event, shiftHeld);

        // Log every non-repeat key-down into the ring buffer (no file I/O here)
        if (isDown && !isAutoRepeat)
            RingLog($"kc={keyCode} ch=U+{(int)baseChar:X4}('{baseChar}') ctrl={ctrlHeld} alt={altHeld} cmd={cmdHeld}");

        if (ctrlHeld || altHeld || cmdHeld)
            return @event;

        if (baseChar == '\0')
            return @event;

        if (_engine!.ProcessKey(keyCode, baseChar, isDown, isUp, isAutoRepeat, shiftHeld))
            return IntPtr.Zero; // suppress

        return @event;
    }

    private static unsafe char ResolveChar(IntPtr @event, bool shiftHeld)
    {
        var keyCode = (ushort)MacNativeMethods.CGEventGetIntegerValueField(
            @event, MacNativeMethods.CGEventField.KeyboardEventKeycode);

        var layoutData = MacNativeMethods.GetKeyboardLayoutData();
        if (layoutData == IntPtr.Zero) return '\0';

        uint deadKeyState = 0;
        char c = '\0';
        MacNativeMethods.UCKeyTranslate(
            (byte*)layoutData, keyCode,
            0,                            // kUCKeyActionDown
            0,                            // no modifiers — get base character
            MacNativeMethods.LMGetKbdType(),
            0, ref deadKeyState, 1, out _, &c);

        if (c == '\0') return '\0';
        return char.IsLetter(c) ? char.ToLowerInvariant(c) : c;
    }

    public void Dispose()
    {
        if (_tap != IntPtr.Zero)
        {
            MacNativeMethods.CGEventTapEnable(_tap, false);
            _tap = IntPtr.Zero;
        }
    }
}
