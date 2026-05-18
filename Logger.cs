namespace Accentra;

static class Logger
{
    private const long MaxBytes = 1 * 1024 * 1024; // 1 MB
    private const int KeepLines = 500;

    public static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Accentra", "accentra.log");

    public static void Prune()
    {
        try
        {
            if (!File.Exists(LogPath)) return;
            if (new FileInfo(LogPath).Length <= MaxBytes) return;
            var lines = File.ReadAllLines(LogPath);
            if (lines.Length <= KeepLines) return;
            File.WriteAllLines(LogPath, lines[^KeepLines..]);
        }
        catch { }
    }

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
        }
        catch { }
    }
}
