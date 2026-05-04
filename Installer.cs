using Microsoft.Win32;
using Windows.ApplicationModel;

namespace Accentra;

static class Installer
{
    private const string AppName = "Accentra";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static readonly bool IsPackaged = DetectPackaged();

    private static bool DetectPackaged()
    {
        try { return Package.Current is not null; }
        catch { return false; }
    }

    public static readonly string AccentMapsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

    public static bool EnsureAccentMapsJson()
    {
        var dir = AccentMapsDir;
        Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, "accent-maps.json");
        if (File.Exists(dest)) return false;
        using var stream = typeof(Installer).Assembly.GetManifestResourceStream("Accentra.accent-maps.json")!;
        using var file = File.Create(dest);
        stream.CopyTo(file);
        return true;
    }

    public static bool IsAutoStartEnabled()
    {
        if (IsPackaged)
        {
            var task = StartupTask.GetAsync("AccentraStartupTask").AsTask().GetAwaiter().GetResult();
            return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) is not null;
    }

    public static void ToggleAutoStart()
    {
        if (IsPackaged)
        {
            var task = StartupTask.GetAsync("AccentraStartupTask").AsTask().GetAwaiter().GetResult();
            if (task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy)
                task.Disable();
            else
                task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
            return;
        }
        if (IsAutoStartEnabled())
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)!;
            key.SetValue(AppName, $"\"{Environment.ProcessPath}\"");
        }
    }
}
