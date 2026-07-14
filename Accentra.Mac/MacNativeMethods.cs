using System.Runtime.InteropServices;
using System.Text;

namespace Accentra;

static class MacNativeMethods
{
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string AppServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string ObjC = "/usr/lib/libobjc.dylib";
    // On macOS 12+, system dylibs live in the dyld shared cache and are not accessible
    // by path or bare name from Homebrew .NET.  dlopen/dlsym still work via libSystem.
    private const string LibSystem = "libSystem";

    // ── Enums ────────────────────────────────────────────────────────────────

    public enum CGEventTapLocation : uint
    {
        HIDEventTap = 0,
        SessionEventTap = 1,
        AnnotatedSessionEventTap = 2,
    }

    public enum CGEventTapPlacement : uint
    {
        HeadInsertEventTap = 0,
        TailAppendEventTap = 1,
    }

    public enum CGEventTapOptions : uint
    {
        Default = 0,
        ListenOnly = 1,
    }

    public enum CGEventType : uint
    {
        Null = 0,
        KeyDown = 10,
        KeyUp = 11,
        FlagsChanged = 12,
        // Sent to callback when macOS auto-disables the tap (callback was too slow)
        TapDisabledByTimeout = 0xFFFFFFFE,
        TapDisabledByUserInput = 0xFFFFFFFD,
    }

    public enum CGEventField : uint
    {
        KeyboardEventAutorepeat = 8,
        KeyboardEventKeycode = 9,
        SourceUserData = 40,
        SourceUnixProcessID = 41,
    }

    // ── Constants ────────────────────────────────────────────────────────────

    public const ulong kCGEventFlagMaskShift = 0x00020000;
    public const ulong kCGEventFlagMaskControl = 0x00040000;
    public const ulong kCGEventFlagMaskAlternate = 0x00080000;
    public const ulong kCGEventFlagMaskCommand = 0x00100000;

    public const ushort kVK_Delete = 0x33;

    public const long AccentraSentinel = 0xACCE0001;

    public static ulong EventMask(CGEventType type) => 1UL << (int)type;

    // ── CGEvent tap ──────────────────────────────────────────────────────────

    public delegate IntPtr CGEventTapCallBack(IntPtr proxy, CGEventType type, IntPtr @event, IntPtr userInfo);

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGEventTapCreate(
        CGEventTapLocation tap, CGEventTapPlacement place, CGEventTapOptions options,
        ulong eventsOfInterest, CGEventTapCallBack callback, IntPtr userInfo);

    [DllImport(CoreGraphics)]
    public static extern void CGEventTapEnable(IntPtr tap, bool enable);

    [DllImport(CoreGraphics)]
    public static extern long CGEventGetIntegerValueField(IntPtr @event, CGEventField field);

    [DllImport(CoreGraphics)]
    public static extern void CGEventSetIntegerValueField(IntPtr @event, CGEventField field, long value);

    [DllImport(CoreGraphics)]
    public static extern ulong CGEventGetFlags(IntPtr @event);

    [DllImport(CoreGraphics)]
    public static extern void CGEventPost(CGEventTapLocation tap, IntPtr @event);

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, bool keyDown);

    // kCGEventSourceStatePrivate = -1
    [DllImport(CoreGraphics)]
    public static extern IntPtr CGEventSourceCreate(int stateID);

    [DllImport(CoreGraphics)]
    public static extern void CGEventSourceSetUserData(IntPtr source, long userData);

    // CharSet.Unicode is required: the API takes UniChar* (UTF-16); the default
    // ANSI marshaling converts char[] to UTF-8 bytes, producing garbage characters.
    [DllImport(CoreGraphics, CharSet = CharSet.Unicode)]
    public static extern void CGEventKeyboardSetUnicodeString(IntPtr @event, UIntPtr stringLength, char[] unicodeString);

    [DllImport(CoreGraphics, CharSet = CharSet.Unicode)]
    public static extern void CGEventKeyboardGetUnicodeString(
        IntPtr @event, UIntPtr maxLength, out UIntPtr actualLength, char[] unicodeString);

    // ── CoreFoundation run loop ───────────────────────────────────────────────

    // CGEventTap returns a CFMachPortRef — use CFMachPortCreateRunLoopSource (not CGEventTapCreateRunLoopSource)
    [DllImport(CoreFoundation)]
    public static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, int order);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFRunLoopGetMain();

    [DllImport(CoreFoundation)]
    public static extern void CFRunLoopAddSource(IntPtr rl, IntPtr source, IntPtr mode);

    // ── Dynamic linking (to read CFRunLoop mode constants) ───────────────────

    private const int RTLD_LAZY = 0x1;
    private const int RTLD_NOLOAD = 0x10;

    [DllImport(LibSystem)]
    private static extern IntPtr dlopen(string path, int mode);

    [DllImport(LibSystem)]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);

    private static IntPtr GetConstant(string framework, string symbol)
    {
        // RTLD_LAZY loads the library if not yet loaded; don't use RTLD_NOLOAD here
        var handle = dlopen(framework, RTLD_LAZY);
        if (handle == IntPtr.Zero) return IntPtr.Zero;
        var sym = dlsym(handle, symbol);
        return sym == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(sym);
    }

    public static readonly IntPtr kCFRunLoopDefaultMode =
        GetConstant(CoreFoundation, "kCFRunLoopDefaultMode");

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFDataGetBytePtr(IntPtr data);

    // ── Carbon / TIS — keyboard layout for UCKeyTranslate ────────────────────
    //
    // CGEventKeyboardGetUnicodeString is not populated at kCGSessionEventTap on
    // macOS 15 (Sequoia).  UCKeyTranslate is the authoritative way to convert a
    // virtual key code to a Unicode character using the active keyboard layout.

    private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";

    [DllImport(Carbon)]
    public static extern IntPtr TISCopyCurrentKeyboardLayoutInputSource();

    [DllImport(Carbon)]
    private static extern IntPtr TISGetInputSourceProperty(IntPtr source, IntPtr propertyKey);

    [DllImport(Carbon)]
    public static extern byte LMGetKbdType();

    [DllImport(Carbon)]
    public static extern unsafe int UCKeyTranslate(
        byte* keyLayoutPtr, ushort virtualKeyCode, ushort keyAction,
        uint modifierKeyState, uint keyboardType, uint keyTranslateOptions,
        ref uint deadKeyState, int maxStringLength,
        out int actualStringLength, char* unicodeString);

    public static readonly IntPtr kTISPropertyUnicodeKeyLayoutData =
        GetConstant(Carbon, "kTISPropertyUnicodeKeyLayoutData");

    // Returns the raw byte pointer into the current keyboard's UCKeyboardLayout.
    // The pointer is valid as long as the TIS source is alive (process lifetime).
    private static IntPtr _cachedLayoutData;

    public static unsafe IntPtr GetKeyboardLayoutData()
    {
        if (_cachedLayoutData != IntPtr.Zero) return _cachedLayoutData;
        var source = TISCopyCurrentKeyboardLayoutInputSource();
        if (source == IntPtr.Zero) return IntPtr.Zero;
        var data = TISGetInputSourceProperty(source, kTISPropertyUnicodeKeyLayoutData);
        if (data == IntPtr.Zero) return IntPtr.Zero;
        _cachedLayoutData = CFDataGetBytePtr(data);
        return _cachedLayoutData;
    }

    // ── Accessibility ─────────────────────────────────────────────────────────

    [DllImport(AppServices)]
    public static extern bool AXIsProcessTrusted();

    [DllImport(AppServices)]
    private static extern bool AXIsProcessTrustedWithOptions(IntPtr options);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFDictionaryCreate(
        IntPtr allocator, IntPtr[] keys, IntPtr[] values, nint numValues,
        IntPtr keyCallBacks, IntPtr valueCallBacks);

    // Like AXIsProcessTrusted, but when not trusted it shows the standard macOS
    // permission dialog AND registers the app in the Accessibility list, so the
    // user only has to flip the toggle instead of adding the app manually.
    public static bool RequestAccessibilityTrust()
    {
        var key = GetConstant(AppServices, "kAXTrustedCheckOptionPrompt");
        var val = GetConstant(CoreFoundation, "kCFBooleanTrue");
        if (key == IntPtr.Zero || val == IntPtr.Zero)
            return AXIsProcessTrusted();
        var options = CFDictionaryCreate(IntPtr.Zero, [key], [val], 1, IntPtr.Zero, IntPtr.Zero);
        return AXIsProcessTrustedWithOptions(options);
    }

    // Promotes a background/CLI process to a foreground application so it can use the window server.
    // Required when the process is launched from a terminal rather than via LaunchServices.
    [StructLayout(LayoutKind.Sequential)]
    public struct ProcessSerialNumber { public uint highLongOfPSN, lowLongOfPSN; }

    public const uint kCurrentProcess = 2;
    public const uint kProcessTransformToForegroundApplication = 1;

    [DllImport(AppServices)]
    public static extern int TransformProcessType(ref ProcessSerialNumber psn, uint transformState);

    public static void EnsureForegroundApp()
    {
        var psn = new ProcessSerialNumber { highLongOfPSN = 0, lowLongOfPSN = kCurrentProcess };
        TransformProcessType(ref psn, kProcessTransformToForegroundApplication);
    }

    // ── AppKit bootstrap ─────────────────────────────────────────────────────

    // AppKit is not loaded by default in a .NET process; load it explicitly before
    // calling any NSApplication or NSStatusBar APIs.
    public static void LoadAppKit()
    {
        dlopen("/System/Library/Frameworks/AppKit.framework/AppKit", RTLD_LAZY);
    }

    // ── Objective-C runtime ───────────────────────────────────────────────────

    [DllImport(ObjC)]
    public static extern IntPtr objc_getClass(string name);

    [DllImport(ObjC)]
    public static extern IntPtr sel_registerName(string name);

    // Plain send (returns id)
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    // Send with one id arg
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend_id(IntPtr receiver, IntPtr selector, IntPtr arg);

    // Send with two id args
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend_id_id(IntPtr receiver, IntPtr selector, IntPtr a1, IntPtr a2);

    // Send with three id args (initWithTitle:action:keyEquivalent:)
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend_id_id_id(IntPtr receiver, IntPtr selector, IntPtr a1, IntPtr a2, IntPtr a3);

    // Send with CGFloat arg (statusItemWithLength:)
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend_cgfloat(IntPtr receiver, IntPtr selector, double arg);

    // Void sends
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern void objc_msgSend_void(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern void objc_msgSend_void_id(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern void objc_msgSend_void_nint(IntPtr receiver, IntPtr selector, nint arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern void objc_msgSend_void_bool(IntPtr receiver, IntPtr selector, bool arg);

    // IntPtr arg (for passing byte* as IntPtr)
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend_ptr(IntPtr receiver, IntPtr selector, IntPtr arg);

    // long (NSInteger) return
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern nint objc_msgSend_nint(IntPtr receiver, IntPtr selector);

    // ── ObjC class creation ───────────────────────────────────────────────────

    [DllImport(ObjC)]
    public static extern IntPtr objc_allocateClassPair(IntPtr superclass, string name, nuint extraBytes);

    [DllImport(ObjC)]
    public static extern void objc_registerClassPair(IntPtr cls);

    [DllImport(ObjC)]
    public static extern bool class_addMethod(IntPtr cls, IntPtr name, IntPtr imp, string types);

    // ── Dispatch ──────────────────────────────────────────────────────────────
    //
    // libdispatch is in the dyld shared cache and cannot be found by name via
    // dlopen on Homebrew .NET / macOS 15.  After LoadAppKit() the symbols are
    // already in process memory; resolve them through RTLD_DEFAULT (-2).

    public delegate void DispatchFunction(IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DispatchAsyncFProc(IntPtr queue, IntPtr context, DispatchFunction work);

    private static IntPtr _mainQueue;
    private static DispatchAsyncFProc? _dispatchAsyncFProc;

    private static void LoadDispatch()
    {
        if (_dispatchAsyncFProc != null) return;
        var rtldDefault = new IntPtr(-2); // RTLD_DEFAULT — searches all loaded images

        // dispatch_get_main_queue() is a C macro = &_dispatch_main_q.
        // dlsym gives us the address of that global, which IS the queue pointer value.
        var pMainQ = dlsym(rtldDefault, "_dispatch_main_q");
        var pAsync  = dlsym(rtldDefault, "dispatch_async_f");
        Logger.Log($"LoadDispatch: _dispatch_main_q=0x{pMainQ:x} asyncF=0x{pAsync:x}");
        _mainQueue = pMainQ;
        if (pAsync != IntPtr.Zero)
            _dispatchAsyncFProc = Marshal.GetDelegateForFunctionPointer<DispatchAsyncFProc>(pAsync);
    }

    public static IntPtr dispatch_get_main_queue()
    {
        LoadDispatch();
        return _mainQueue;
    }

    public static void dispatch_async_f(IntPtr queue, IntPtr context, DispatchFunction work)
    {
        LoadDispatch();
        _dispatchAsyncFProc?.Invoke(queue, context, work);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static IntPtr ToNSString(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s + "\0");
        unsafe
        {
            fixed (byte* ptr = bytes)
                return objc_msgSend_ptr(
                    objc_getClass("NSString"),
                    sel_registerName("stringWithUTF8String:"),
                    (IntPtr)ptr);
        }
    }

    public static string FromNSString(IntPtr nsString)
    {
        if (nsString == IntPtr.Zero) return "";
        var utf8ptr = objc_msgSend(nsString, sel_registerName("UTF8String"));
        return utf8ptr == IntPtr.Zero ? "" : Marshal.PtrToStringUTF8(utf8ptr) ?? "";
    }
}
