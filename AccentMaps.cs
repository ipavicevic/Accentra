using System.Text.Json;

namespace Accentra;

static class AccentMaps
{
    private const string ResourceName = "Accentra.accent-maps.json";

    private static volatile Dictionary<char, char[]> _maps;
    private static FileSystemWatcher? _watcher;
    private static readonly object _debounceLock = new();
    private static System.Threading.Timer? _debounce;

    public static string? LoadError { get; private set; }

    // null = success, error string = failure
    public static event Action<string?>? Reloaded;

    static AccentMaps()
    {
        _maps = LoadInitial();
        StartWatcher();
    }

    private static Dictionary<char, char[]> LoadInitial()
    {
        var dataPath = Path.Combine(Installer.AccentMapsDir, "accent-maps.json");
        if (File.Exists(dataPath))
        {
            try
            {
                var result = Parse(File.ReadAllText(dataPath));
                Logger.Log($"AccentMaps loaded from {dataPath}");
                return result;
            }
            catch (Exception ex)
            {
                LoadError = ex.Message;
                Logger.Log($"AccentMaps parse failed for {dataPath}: {ex.Message}");
            }
        }
        else
        {
            Logger.Log($"AccentMaps data file not found: {dataPath}");
        }

        // Fallback: EXE directory (plain EXE dev/testing without prior install).
        var exePath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? ".", "accent-maps.json");
        if (File.Exists(exePath))
        {
            try
            {
                var result = Parse(File.ReadAllText(exePath));
                Logger.Log($"AccentMaps loaded from EXE dir: {exePath}");
                return result;
            }
            catch { }
        }

        Logger.Log("AccentMaps using embedded default");
        try { return Parse(ReadEmbedded()); }
        catch (Exception ex) { Logger.Log($"AccentMaps embedded JSON invalid: {ex.Message}"); }

        return [];
    }

    private static void StartWatcher()
    {
        var dir = Installer.AccentMapsDir;
        if (!Directory.Exists(dir)) return;
        _watcher = new FileSystemWatcher(dir, "accent-maps.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
    }

    private static void OnFileChanged(object _, FileSystemEventArgs __)
    {
        // Debounce: editors often fire multiple change events for a single save.
        lock (_debounceLock)
        {
            _debounce?.Dispose();
            _debounce = new System.Threading.Timer(_ => Reload(), null, 400, Timeout.Infinite);
        }
    }

    private static void Reload()
    {
        var path = Path.Combine(Installer.AccentMapsDir, "accent-maps.json");
        try
        {
            var newMaps = Parse(File.ReadAllText(path));
            _maps = newMaps;
            LoadError = null;
            Logger.Log($"AccentMaps reloaded: {path}");
            Reloaded?.Invoke(null);
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
            Logger.Log($"AccentMaps reload failed: {ex.Message}");
            Reloaded?.Invoke(ex.Message);
        }
    }

    private static Dictionary<char, char[]> Parse(string json)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        return raw
            .Where(kv => kv.Key.Length == 1)
            .ToDictionary(kv => kv.Key[0], kv => kv.Value.ToCharArray());
    }

    private static char ShiftedChar(uint vkCode, char unshifted)
    {
        var keyState = new byte[256];
        keyState[NativeMethods.VK_SHIFT] = 0x80;
        var buf = new char[2];
        uint scan = NativeMethods.MapVirtualKey(vkCode, NativeMethods.MAPVK_VK_TO_VSC);
        int result = NativeMethods.ToUnicode(vkCode, scan, keyState, buf, buf.Length, 0);
        return result == 1 ? buf[0] : unshifted;
    }

    private static string ReadEmbedded()
    {
        using var stream = typeof(AccentMaps).Assembly.GetManifestResourceStream(ResourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static char[]? GetVariants(uint vkCode, bool shifted)
    {
        // For letter keys derive the base char from the VK code directly.
        // For everything else use MapVirtualKey to get the unshifted character.
        char baseChar;
        if (vkCode is >= 0x41 and <= 0x5A)
        {
            baseChar = (char)(vkCode + 0x20); // always lowercase; shifted variants uppercased below
        }
        else
        {
            uint ch = NativeMethods.MapVirtualKey(vkCode, NativeMethods.MAPVK_VK_TO_CHAR);
            if (ch == 0) return null;
            // For non-letter keys, use the actual character produced (shift-aware).
            // MapVirtualKey returns the unshifted char; check shifted state via GetKeyState.
            char unshifted = (char)ch;
            baseChar = shifted ? ShiftedChar(vkCode, unshifted) : unshifted;
        }

        if (!_maps.TryGetValue(baseChar, out var variants))
            return null;

        // For letters, uppercase the variants when shifted. For other keys, variants are literal.
        return (shifted && vkCode is >= 0x41 and <= 0x5A)
            ? Array.ConvertAll(variants, char.ToUpper)
            : variants;
    }
}
