using Microsoft.Win32;
using System.Diagnostics;

namespace Accentra;

static class Installer
{
    private const string AppName = "Accentra";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // Extract accent-maps.json from embedded resources the first time the app runs.
    // Preserves any user edits on subsequent starts and after upgrades.
    public static void EnsureAccentMapsJson()
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
        var dest = Path.Combine(exeDir, "accent-maps.json");
        if (File.Exists(dest)) return;
        try
        {
            using var stream = typeof(Installer).Assembly.GetManifestResourceStream("Accentra.accent-maps.json")!;
            using var file = File.Create(dest);
            stream.CopyTo(file);
            Logger.Log("Extracted accent-maps.json");
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to extract accent-maps.json: {ex.Message}");
        }
    }

    public static void Uninstall()
    {
        var productCode = FindMsiProductCode();
        if (productCode is null)
        {
            Logger.Log("MSI product code not found in registry");
            MessageBox.Show(
                "Could not find the uninstall entry. Please uninstall from Settings → Apps.",
                "Uninstall Accentra", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Logger.Log($"Invoking msiexec /x {productCode} /quiet");
        Process.Start(new ProcessStartInfo("msiexec.exe", $"/x {productCode} /quiet")
        {
            UseShellExecute = true,
        });
    }

    private static string? FindMsiProductCode()
    {
        using var root = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall");
        if (root is null) return null;
        foreach (var name in root.GetSubKeyNames())
        {
            using var sub = root.OpenSubKey(name);
            if (sub?.GetValue("DisplayName") as string == AppName)
                return name;
        }
        return null;
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
            key.SetValue(AppName, $"\"{Environment.ProcessPath}\"");
        }
    }
}
