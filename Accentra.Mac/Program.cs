using System.Diagnostics;

namespace Accentra;

static class Program
{
    static void Main(string[] args)
    {
        // Become a menu-bar agent immediately — before any other startup work.
        // Until this is set, the process is a regular (Dock) app, and our startup
        // takes several seconds; during that window macOS shows Accentra in the Dock
        // and records it in the Dock's "recent applications" list, where it lingers
        // even after we revert to an agent. Setting the policy first keeps Accentra
        // out of the Dock entirely. (MacTrayApp sets it again, harmlessly.)
        var nsApp = MacNativeMethods.objc_msgSend(
            MacNativeMethods.objc_getClass("NSApplication"),
            MacNativeMethods.sel_registerName("sharedApplication"));
        MacNativeMethods.objc_msgSend_void_nint(nsApp,
            MacNativeMethods.sel_registerName("setActivationPolicy:"), 1); // Accessory

        // Trigger the system Accessibility prompt and register Accentra in the
        // Accessibility list on first run. We do NOT block on the result or show
        // our own alert: the app launches fully regardless (the tray shows a dimmed
        // "waiting" state), and the keyboard hook retries tap creation until
        // permission is granted (see MacKeyboardHook). A modal alert here would
        // promote Accentra to a regular Dock app, which then lingers in the Dock's
        // "recent applications" list even after it reverts to a menu-bar agent.
        MacNativeMethods.RequestAccessibilityTrust();

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

        // Upgraders may have the pre-1.0.7 bare-exec LaunchAgent; switch them to SMAppService.
        Installer.MigrateLegacyAutoStart();

        bool firstRun = Installer.EnsureAccentMapsJson();
        if (firstRun)
            // Accentra is meant to run all the time, so enable Start at Login by default
            // (parity with the Windows startup task). Only on genuine first run — if the
            // user later turns it off, it stays off. Toggleable from the tray menu.
            Installer.SetAutoStart(true);

        using var app = new MacTrayApp(firstRun);
        app.Run();
    }

}
