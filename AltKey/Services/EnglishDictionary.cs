using System.IO;
using System.Reflection;
using System.Text;

namespace AltKey.Services;

public class EnglishDictionary
{
    private readonly WordFrequencyStore _userStore;
    private readonly IReadOnlyList<string> _builtIn;

    public EnglishDictionary(WordFrequencyStore userStore)
    {
        _userStore = userStore;
        _builtIn = LoadBuiltIn();
    }

    public IReadOnlyList<string> GetSuggestions(string prefix, int count = 5)
    {
        if (prefix.Length < 2) return [];

        var userSuggestions = _userStore.GetSuggestions(prefix, count);

        var needed = count - userSuggestions.Count;
        if (needed <= 0) return userSuggestions;

        var userSet = new HashSet<string>(userSuggestions);
        var builtInSuggestions = _builtIn
            .Where(w => w.StartsWith(prefix) && w.Length > prefix.Length
                        && !userSet.Contains(w))
            .Take(needed)
            .ToList();

        return [..userSuggestions, ..builtInSuggestions];
    }

    private static IReadOnlyList<string> LoadBuiltIn()
    {
        var asm = Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream("AltKey.Assets.Data.en-words.txt");
        if (stream is null) return [];
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
    }
}
