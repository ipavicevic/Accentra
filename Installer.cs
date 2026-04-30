using Microsoft.Win32;
using System.Diagnostics;

namespace Accentra;

static class Installer
{
    private const string AppName = "Accentra";

    private static readonly string InstallDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

    public static readonly string InstallPath = Path.Combine(InstallDir, $"{AppName}.exe");

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Accentra";

    public static bool IsInstalledLocation() =>
        string.Equals(Environment.ProcessPath, InstallPath, StringComparison.OrdinalIgnoreCase);

    public static void Install()
    {
        Logger.Log($"Installing from {Environment.ProcessPath}");
        Directory.CreateDirectory(InstallDir);
        File.Copy(Environment.ProcessPath!, InstallPath, overwrite: true);

        // Extract embedded accent-maps.json on first install only — preserve user edits on upgrade.
        var destJson = Path.Combine(InstallDir, "accent-maps.json");
        if (!File.Exists(destJson))
        {
            using var stream = typeof(Installer).Assembly.GetManifestResourceStream("Accentra.accent-maps.json")!;
            using var file = File.Create(destJson);
            stream.CopyTo(file);
        }

        using (var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)!)
            run.SetValue(AppName, $"\"{InstallPath}\"");

        using (var uninstall = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            uninstall.SetValue("DisplayName", AppName);
            uninstall.SetValue("UninstallString", $"\"{InstallPath}\" --uninstall");
            uninstall.SetValue("DisplayIcon", InstallPath);
            uninstall.SetValue("Publisher", AppName);
            uninstall.SetValue("DisplayVersion", Application.ProductVersion);
            uninstall.SetValue("NoModify", 1, RegistryValueKind.DWord);
            uninstall.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }

        AddToUserPath();

        Logger.Log($"Install complete — launching {InstallPath}");
        Process.Start(InstallPath, "--first-run");
    }

    public static void Uninstall()
    {
        using (var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
            run?.DeleteValue(AppName, throwOnMissingValue: false);

        Registry.CurrentUser.DeleteSubKey(UninstallKey, throwOnMissingSubKey: false);

        RemoveFromUserPath();

        // Schedule folder deletion after process exits (cmd waits 2s then removes the directory)
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c timeout /t 2 /nobreak & rd /s /q \"{InstallDir}\"")
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
        });
    }

    private static void AddToUserPath()
    {
        var current = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
        var parts = current.Split(';', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(p => string.Equals(p.TrimEnd('\\'), InstallDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
        {
            Logger.Log("InstallDir already in user PATH");
            return;
        }
        var newPath = current.TrimEnd(';') + ";" + InstallDir;
        Environment.SetEnvironmentVariable("Path", newPath, EnvironmentVariableTarget.User);
        Logger.Log($"Added {InstallDir} to user PATH");
    }

    private static void RemoveFromUserPath()
    {
        var current = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
        var parts = current.Split(';', StringSplitOptions.RemoveEmptyEntries)
                           .Where(p => !string.Equals(p.TrimEnd('\\'), InstallDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        Environment.SetEnvironmentVariable("Path", string.Join(';', parts), EnvironmentVariableTarget.User);
        Logger.Log($"Removed {InstallDir} from user PATH");
    }

    public static bool IsAutoStartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) is not null;
    }

    public static void ToggleAutoStart()
    {
        if (IsAutoStartEnabled())
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)!;
            key.SetValue(AppName, $"\"{InstallPath}\"");
        }
    }
}
