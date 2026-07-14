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

    public static bool IsAutoStartEnabled() => File.Exists(LaunchAgentPath);

    public static void ToggleAutoStart()
    {
        if (IsAutoStartEnabled())
        {
            File.Delete(LaunchAgentPath);
            Logger.Log("Auto-start disabled (LaunchAgent removed)");
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LaunchAgentPath)!);
            var exe = Environment.ProcessPath!;
            File.WriteAllText(LaunchAgentPath, $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <key>Label</key>
                    <string>{BundleId}</string>
                    <key>ProgramArguments</key>
                    <array>
                        <string>{exe}</string>
                    </array>
                    <key>RunAtLoad</key>
                    <true/>
                </dict>
                </plist>
                """);
            Logger.Log($"Auto-start enabled (LaunchAgent written for {exe})");
        }
    }

    private static string LaunchAgentPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", $"{BundleId}.plist");
}
