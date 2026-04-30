using System.Text.Json;

namespace Accentra;

static class AccentMaps
{
    private const string ResourceName = "Accentra.accent-maps.json";

    private static readonly Dictionary<char, char[]> Maps = LoadMaps();

    private static Dictionary<char, char[]> LoadMaps()
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
        var jsonPath = Path.Combine(exeDir, "accent-maps.json");

        if (File.Exists(jsonPath))
        {
            try { return Parse(File.ReadAllText(jsonPath)); }
            catch { /* malformed user JSON — fall through to embedded defaults */ }
        }

        return Parse(ReadEmbedded());
    }

    private static Dictionary<char, char[]> Parse(string json)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        return raw
            .Where(kv => kv.Key.Length == 1)
            .ToDictionary(kv => kv.Key[0], kv => kv.Value.ToCharArray());
    }

    private static string ReadEmbedded()
    {
        using var stream = typeof(AccentMaps).Assembly.GetManifestResourceStream(ResourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // vkCode is always in the A-Z range (0x41-0x5A); shift determines case.
    public static char[]? GetVariants(uint vkCode, bool shifted)
    {
        if (vkCode < 0x41 || vkCode > 0x5A)
            return null;

        char lower = (char)(vkCode + 0x20);
        if (!Maps.TryGetValue(lower, out var variants))
            return null;

        return shifted ? Array.ConvertAll(variants, char.ToUpper) : variants;
    }
}
