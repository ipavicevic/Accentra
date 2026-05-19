using System.Text.Json;
using System.Text.Json.Serialization;

namespace Accentra;

static class AccentMaps
{
    private const string ResourceName = "Accentra.accent-maps.json";
    private const string FileName = "accent-maps.json";

    private record Section(string Name, bool Enabled, Dictionary<char, char[]> Maps);

    private class FileModel
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 2;
        [JsonPropertyName("sections")]
        public List<SectionModel> Sections { get; set; } = [];
    }

    private class SectionModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
        [JsonPropertyName("maps")]
        public Dictionary<string, string> Maps { get; set; } = [];
    }

    private static volatile Dictionary<char, char[]> _maps = [];
    private static List<Section> _sections = [];
    private static readonly object _sectionsLock = new();
    private static FileSystemWatcher? _watcher;
    private static readonly object _debounceLock = new();
    private static System.Threading.Timer? _debounce;

    public static string? LoadError { get; private set; }

    // null = success, error string = failure; only fires on external file reload
    public static event Action<string?>? Reloaded;

    static AccentMaps()
    {
        var (sections, maps) = LoadInitial();
        _sections = sections;
        _maps = maps;
        StartWatcher();
    }

    public static IReadOnlyList<(string Name, bool Enabled)> GetSections()
    {
        lock (_sectionsLock)
            return _sections.Select(s => (s.Name, s.Enabled)).ToList();
    }

    // Toggles the enabled state of a section, re-merges maps, and persists the file.
    // Does not fire Reloaded — callers handle UI updates directly.
    public static void ToggleSection(string name)
    {
        lock (_sectionsLock)
        {
            var idx = _sections.FindIndex(s => s.Name == name);
            if (idx < 0) return;
            var s = _sections[idx];
            _sections[idx] = s with { Enabled = !s.Enabled };
            _maps = Merge(_sections);
        }
        Save();
        Logger.Log($"AccentMaps section '{name}' toggled");
    }

    private static (List<Section> sections, Dictionary<char, char[]> maps) LoadInitial()
    {
        var dataPath = Path.Combine(Installer.AccentMapsDir, FileName);
        if (File.Exists(dataPath))
        {
            try
            {
                var sections = ParseFile(File.ReadAllText(dataPath), out bool migrated);
                if (migrated)
                {
                    SaveSections(sections, dataPath);
                    Logger.Log($"AccentMaps migrated v1→v2: {dataPath}");
                }
                else
                {
                    Logger.Log($"AccentMaps loaded from {dataPath}");
                }
                return (sections, Merge(sections));
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

        var exePath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? ".", FileName);
        if (File.Exists(exePath))
        {
            try
            {
                var sections = ParseFile(File.ReadAllText(exePath), out _);
                Logger.Log($"AccentMaps loaded from EXE dir: {exePath}");
                return (sections, Merge(sections));
            }
            catch { }
        }

        Logger.Log("AccentMaps using embedded default");
        try
        {
            var sections = ParseFile(ReadEmbedded(), out _);
            return (sections, Merge(sections));
        }
        catch (Exception ex) { Logger.Log($"AccentMaps embedded JSON invalid: {ex.Message}"); }

        return ([], []);
    }

    private static List<Section> ParseFile(string json, out bool migrated)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("version", out _))
        {
            migrated = true;
            return MigrateV1(root);
        }

        migrated = false;
        var model = JsonSerializer.Deserialize<FileModel>(json)!;
        return model.Sections
            .Select(s => new Section(s.Name, s.Enabled, ParseMaps(s.Maps)))
            .ToList();
    }

    private static List<Section> MigrateV1(JsonElement root)
    {
        var lettersMap = new Dictionary<string, string>();
        foreach (var prop in root.EnumerateObject())
            if (prop.Name.Length == 1)
                lettersMap[prop.Name] = prop.Value.GetString()!;

        // Bring in the non-letters sections from the embedded v2 default
        var embeddedModel = JsonSerializer.Deserialize<FileModel>(ReadEmbedded())!;
        var result = new List<Section> { new("letters", true, ParseMaps(lettersMap)) };
        result.AddRange(embeddedModel.Sections
            .Where(s => s.Name != "letters")
            .Select(s => new Section(s.Name, s.Enabled, ParseMaps(s.Maps))));
        return result;
    }

    private static Dictionary<char, char[]> ParseMaps(Dictionary<string, string> raw) =>
        raw.Where(kv => kv.Key.Length == 1)
           .ToDictionary(kv => kv.Key[0], kv => kv.Value.ToCharArray());

    private static Dictionary<char, char[]> Merge(List<Section> sections)
    {
        var result = new Dictionary<char, List<char>>();
        foreach (var section in sections.Where(s => s.Enabled))
        {
            foreach (var (key, variants) in section.Maps)
            {
                if (!result.TryGetValue(key, out var list))
                    result[key] = list = [];
                foreach (var c in variants)
                    if (!list.Contains(c))
                        list.Add(c);
            }
        }
        return result.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    private static void Save()
    {
        var path = Path.Combine(Installer.AccentMapsDir, FileName);
        // Suppress the watcher event triggered by our own write
        if (_watcher != null) _watcher.EnableRaisingEvents = false;
        try
        {
            lock (_sectionsLock)
                SaveSections(_sections, path);
        }
        finally
        {
            if (_watcher != null) _watcher.EnableRaisingEvents = true;
        }
    }

    private static void SaveSections(List<Section> sections, string path)
    {
        var model = new FileModel
        {
            Sections = sections.Select(s => new SectionModel
            {
                Name = s.Name,
                Enabled = s.Enabled,
                Maps = s.Maps.ToDictionary(kv => kv.Key.ToString(), kv => new string(kv.Value))
            }).ToList()
        };
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, JsonSerializer.Serialize(model, options));
    }

    private static void StartWatcher()
    {
        var dir = Installer.AccentMapsDir;
        if (!Directory.Exists(dir)) return;
        _watcher = new FileSystemWatcher(dir, FileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
    }

    private static void OnFileChanged(object _, FileSystemEventArgs __)
    {
        lock (_debounceLock)
        {
            _debounce?.Dispose();
            _debounce = new System.Threading.Timer(_ => Reload(), null, 400, Timeout.Infinite);
        }
    }

    private static void Reload()
    {
        var path = Path.Combine(Installer.AccentMapsDir, FileName);
        try
        {
            var sections = ParseFile(File.ReadAllText(path), out _);
            lock (_sectionsLock)
            {
                _sections = sections;
                _maps = Merge(sections);
            }
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

    private static string ReadEmbedded()
    {
        using var stream = typeof(AccentMaps).Assembly.GetManifestResourceStream(ResourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static char[]? GetVariants(char baseChar, bool shifted)
    {
        if (!_maps.TryGetValue(baseChar, out var variants))
            return null;
        return shifted ? Array.ConvertAll(variants, char.ToUpper) : variants;
    }
}
