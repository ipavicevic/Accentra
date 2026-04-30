using System.Text.Json;

namespace Accentra;

static class AccentMaps
{
    private static readonly Dictionary<char, char[]> Maps = LoadMaps();

    private static Dictionary<char, char[]> LoadMaps()
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
        var jsonPath = Path.Combine(exeDir, "accent-maps.json");

        if (File.Exists(jsonPath))
        {
            try
            {
                var json = File.ReadAllText(jsonPath);
                var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (raw != null)
                    return raw
                        .Where(kv => kv.Key.Length == 1)
                        .ToDictionary(kv => kv.Key[0], kv => kv.Value.ToCharArray());
            }
            catch { }
        }

        return Defaults;
    }

    private static readonly Dictionary<char, char[]> Defaults = new()
    {
        ['a'] = ['á', 'à', 'â', 'ä', 'ã', 'å', 'a'],
        ['c'] = ['č', 'ć', 'ç', 'c'],
        ['d'] = ['đ', 'ď', 'ð', 'd'],
        ['e'] = ['é', 'è', 'ê', 'ë', 'ě', 'e'],
        ['g'] = ['ğ', 'g'],
        ['i'] = ['í', 'ì', 'î', 'ï', 'i'],
        ['l'] = ['ł', 'ľ', 'l'],
        ['n'] = ['ñ', 'ň', 'n'],
        ['o'] = ['ó', 'ò', 'ô', 'ö', 'õ', 'ø', 'o'],
        ['r'] = ['ř', 'r'],
        ['s'] = ['š', 'ś', 'ş', 's'],
        ['t'] = ['ť', 'þ', 't'],
        ['u'] = ['ú', 'ù', 'û', 'ü', 'ů', 'u'],
        ['y'] = ['ý', 'ÿ', 'y'],
        ['z'] = ['ž', 'ź', 'ż', 'z'],
    };

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
