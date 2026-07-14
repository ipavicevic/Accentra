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

    // Insert: inject c without a preceding backspace.
    public static void Insert(char c)
    {
        PostUnicode(c, keyDown: true);
        PostUnicode(c, keyDown: false);
    }

    private static void PostKey(ushort vk, bool keyDown)
    {
        var ev = MacNativeMethods.CGEventCreateKeyboardEvent(_source, vk, keyDown);
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
