namespace Accentra;

static class Installer
{
    private const string BundleId = "com.accentra.Accentra";

    public static readonly string AccentMapsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Application Support", "Accentra");

    public static bool EnsureAccentMapsJson()
    {
        Directory.CreateDirectory(AccentMapsDir);
        var dest = Path.Combine(AccentMapsDir, "accent-maps.json");
        if (File.Exists(dest)) return false;
        using var stream = typeof(Installer).Assembly.GetManifestResourceStream("Accentra.accent-maps.json")!;
        using var file = File.Create(dest);
        stream.CopyTo(file);
        return true;
    }

    // Auto-start uses SMAppService (macOS 13+): the system launches Accentra.app as a
    // proper login item, honoring LSUIElement, so Accentra stays a menu-bar agent and
    // never enters the Dock. This replaces a pre-1.0.7 hand-written LaunchAgent that
    // bare-exec'd the binary — bypassing LaunchServices, which defeated LSUIElement and
    // put Accentra in the Dock's "recent applications" list.

    // SMAppServiceStatus values.
    private const nint StatusEnabled = 1;

    private static IntPtr MainAppService()
    {
        MacNativeMethods.LoadServiceManagement();
        var cls = MacNativeMethods.objc_getClass("SMAppService");
        if (cls == IntPtr.Zero) return IntPtr.Zero;
        return MacNativeMethods.objc_msgSend(cls, MacNativeMethods.sel_registerName("mainAppService"));
    }

    public static bool IsAutoStartEnabled()
    {
        var svc = MainAppService();
        if (svc == IntPtr.Zero) return false;
        var status = MacNativeMethods.objc_msgSend_nint(svc, MacNativeMethods.sel_registerName("status"));
        return status == StatusEnabled;
    }

    public static void ToggleAutoStart() => SetAutoStart(!IsAutoStartEnabled());

    public static void SetAutoStart(bool enabled)
    {
        RemoveLegacyLaunchAgent();
        if (enabled == IsAutoStartEnabled()) return;

        var svc = MainAppService();
        if (svc == IntPtr.Zero) { Logger.Log("SMAppService unavailable — cannot set auto-start"); return; }

        var sel = MacNativeMethods.sel_registerName(enabled ? "registerAndReturnError:" : "unregisterAndReturnError:");
        IntPtr err = IntPtr.Zero;
        bool ok = MacNativeMethods.objc_msgSend_bool_ref(svc, sel, ref err);
        if (ok)
            Logger.Log($"Auto-start {(enabled ? "enabled" : "disabled")} via SMAppService");
        else
            Logger.Log($"SMAppService {(enabled ? "register" : "unregister")} failed: {MacNativeMethods.ErrorDescription(err)}");
    }

    // If a pre-1.0.7 install left the old bare-exec LaunchAgent behind, migrate it:
    // register with SMAppService (if it was enabled) and remove the stale plist so the
    // app is never launched the old way again. Safe to call on every startup.
    public static void MigrateLegacyAutoStart()
    {
        if (!File.Exists(LegacyLaunchAgentPath)) return;
        Logger.Log("Migrating legacy LaunchAgent auto-start to SMAppService");
        SetAutoStart(true); // registers with SMAppService and removes the legacy plist
    }

    private static void RemoveLegacyLaunchAgent()
    {
        try
        {
            if (File.Exists(LegacyLaunchAgentPath))
            {
                File.Delete(LegacyLaunchAgentPath);
                Logger.Log("Removed legacy LaunchAgent plist");
            }
        }
        catch { }
    }

    private static string LegacyLaunchAgentPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", $"{BundleId}.plist");
}
