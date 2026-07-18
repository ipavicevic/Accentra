namespace Accentra;

static class CharacterInjector
{
    // Setting kCGEventSourceUserData directly on an event does not survive CGEventPost
    // on macOS 15; the field must come from the event's source. Create one private
    // source tagged with the sentinel and use it for every injected event.
    private static readonly IntPtr _source = CreateTaggedSource();

    private static IntPtr CreateTaggedSource()
    {
        var source = MacNativeMethods.CGEventSourceCreate(-1); // kCGEventSourceStatePrivate
        MacNativeMethods.CGEventSourceSetUserData(source, MacNativeMethods.AccentraSentinel);
        return source;
    }

    // Replace: backspace the current character, then inject c.
    // Used when cycling through accent variants (something was already typed).
    public static void Replace(char c)
    {
        PostKey(MacNativeMethods.kVK_Delete, keyDown: true);
        PostKey(MacNativeMethods.kVK_Delete, keyDown: false);
        PostUnicode(c, keyDown: true);
        PostUnicode(c, keyDown: false);
    }

    // TypeKey: type the base character by synthesizing a discrete key-down/up on its
    // real virtual keycode.
    //
    // We suppress the user's physical key at the tap so the app never sees a *held*
    // key and never starts its press-and-hold picker. Re-typing it here as a complete
    // down+up gives the app a clean keystroke it commits immediately. Using the real
    // keycode (not a Unicode-string event with keycode 0) keeps single-letter
    // shortcuts working for mapped keys — apps that read the keycode still see the key.
    // The shift flag is stamped so the app produces the correct case.
    public static void TypeKey(ushort keyCode, bool shift)
    {
        ulong flags = shift ? MacNativeMethods.kCGEventFlagMaskShift : 0;
        PostKey(keyCode, keyDown: true, flags);
        PostKey(keyCode, keyDown: false, flags);
    }

    private static void PostKey(ushort vk, bool keyDown, ulong flags = 0)
    {
        var ev = MacNativeMethods.CGEventCreateKeyboardEvent(_source, vk, keyDown);
        MacNativeMethods.CGEventSetFlags(ev, flags);
        MacNativeMethods.CGEventPost(MacNativeMethods.CGEventTapLocation.SessionEventTap, ev);
        // CFRelease(ev) — intentionally omitted for V1; the event is consumed after post
    }

    private static void PostUnicode(char c, bool keyDown)
    {
        var ev = MacNativeMethods.CGEventCreateKeyboardEvent(_source, 0, keyDown);
        MacNativeMethods.CGEventKeyboardSetUnicodeString(ev, (UIntPtr)1, [c]);
        MacNativeMethods.CGEventPost(MacNativeMethods.CGEventTapLocation.SessionEventTap, ev);
    }
}
