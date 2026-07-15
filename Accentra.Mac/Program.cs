using System.Diagnostics;

namespace Accentra;

static class Program
{
    static void Main(string[] args)
    {
        // Shows the system permission dialog and adds Accentra to the
        // Accessibility list on first run. We do NOT block on the result:
        // AXIsProcessTrusted caches "not trusted" for the life of the process,
        // so the app launches fully regardless and the keyboard hook retries
        // tap creation until permission is granted (see MacKeyboardHook).
        if (!MacNativeMethods.RequestAccessibilityTrust())
            ShowAccessibilityHint();

        // Kill any existing instance
        var others = Process.GetProcessesByName("Accentra")
                            .Where(p => p.Id != Environment.ProcessId)
                            .ToArray();
        foreach (var p in others)
        {
            try { p.Kill(); p.WaitForExit(2000); }
            catch { }
        }

        Logger.Prune();
        Logger.Log($"Startup args=[{string.Join(", ", args)}] path={Environment.ProcessPath}");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Logger.Log($"Unhandled exception: {ex}");
        };

        bool firstRun = Installer.EnsureAccentMapsJson();
        using var app = new MacTrayApp(firstRun);
        app.Run();
    }

    private static void ShowAccessibilityHint()
    {
        // Bring app to foreground briefly for the alert
        var NSApp = MacNativeMethods.objc_msgSend(
            MacNativeMethods.objc_getClass("NSApplication"),
            MacNativeMethods.sel_registerName("sharedApplication"));
        MacNativeMethods.objc_msgSend_void_nint(NSApp,
            MacNativeMethods.sel_registerName("setActivationPolicy:"), 0); // NSApplicationActivationPolicyRegular
        MacNativeMethods.objc_msgSend_void_bool(NSApp,
            MacNativeMethods.sel_registerName("activateIgnoringOtherApps:"), true);

        var alert = MacNativeMethods.objc_msgSend(
            MacNativeMethods.objc_getClass("NSAlert"),
            MacNativeMethods.sel_registerName("new"));
        MacNativeMethods.objc_msgSend_void_id(alert,
            MacNativeMethods.sel_registerName("setMessageText:"),
            MacNativeMethods.ToNSString("Accessibility Permission Required"));
        MacNativeMethods.objc_msgSend_void_id(alert,
            MacNativeMethods.sel_registerName("setInformativeText:"),
            MacNativeMethods.ToNSString(
                "Accentra needs Accessibility access to intercept keystrokes.\n\n" +
                "Enable Accentra in System Settings → Privacy & Security → Accessibility " +
                "(it has been added to the list). Accentra will start automatically " +
                "as soon as access is granted."));
        MacNativeMethods.objc_msgSend(alert, MacNativeMethods.sel_registerName("runModal"));
    }
}
