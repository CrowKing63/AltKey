using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using System.Collections.Concurrent;

namespace AltKey.Tests.InputLanguage;

/// <summary>
/// 테스트용 가짜 InputService — 실제 SendInput을 호출하지 않고 기록만 남김.
/// </summary>
internal sealed class FakeInputService : InputService
{
    public ConcurrentBag<string> SentUnicodes { get; } = new();
    public ConcurrentBag<(int prevLen, string next)> AtomicReplaces { get; } = new();
    public List<VirtualKeyCode> KeyPresses { get; } = new();

    public override void SendUnicode(string text) => SentUnicodes.Add(text);

    public override void SendAtomicReplace(int prevLen, string next) =>
        AtomicReplaces.Add((prevLen, next));

    public override void SendKeyPress(VirtualKeyCode vk) => KeyPresses.Add(vk);
}

/// <summary>
/// 테스트용 인메모리 WordFrequencyStore — 파일 I/O를 차단.
/// </summary>
internal sealed class WordFrequencyStoreInMemory : WordFrequencyStore
{
    private readonly Dictionary<string, int> _freq = new();
    public int UserWordCount => _freq.Count;

    public WordFrequencyStoreInMemory() : base("test") { }

    public new IReadOnlyList<string> GetSuggestions(string prefix, int count = 5)
    {
        if (string.IsNullOrEmpty(prefix)) return [];
        return _freq
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                         && kv.Key.Length > prefix.Length)
            .OrderByDescending(kv => kv.Value)
            .Take(count)
            .Select(kv => kv.Key)
            .ToList();
    }

    public new void RecordWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        word = word.Trim();
        if (word.Length == 0) return;
        _freq[word] = (_freq.TryGetValue(word, out var c) ? c : 0) + 1;
    }
}

/// <summary>
/// 테스트용 KoreanDictionary — 인메모리 스토어 사용.
/// </summary>
internal sealed class KoreanDictionaryTestable : KoreanDictionary
{
    private readonly WordFrequencyStoreInMemory _store;
    public int UserWordCount => _store.UserWordCount;

    public KoreanDictionaryTestable() : base(lang => CreateStore(lang))
    {
        _store = s_lastCreated!;
    }

    private static WordFrequencyStoreInMemory? s_lastCreated;

    private static WordFrequencyStore CreateStore(string lang)
    {
        s_lastCreated = new WordFrequencyStoreInMemory();
        return s_lastCreated;
    }
}

/// <summary>
/// 테스트용 EnglishDictionary — 인메모리 스토어 사용.
/// </summary>
internal sealed class EnglishDictionaryTestable : EnglishDictionary
{
    public EnglishDictionaryTestable() : base(_ => new WordFrequencyStoreInMemory()) { }
}

/// <summary>
/// 테스트 헬퍼: 자주 쓰는 KeySlot 팩토리.
/// </summary>
internal static class TestSlotFactory
{
    public static KeySlot Jamo(string label, string? shiftLabel = null, VirtualKeyCode vk = VirtualKeyCode.VK_A) =>
        new(Label: label, ShiftLabel: shiftLabel, Action: new SendKeyAction(vk.ToString()));

    public static KeySlot English(string label, string? shiftLabel = null, VirtualKeyCode vk = VirtualKeyCode.VK_A) =>
        new(Label: label, ShiftLabel: shiftLabel, Action: new SendKeyAction(vk.ToString()), EnglishLabel: label.ToLowerInvariant(), EnglishShiftLabel: shiftLabel?.ToLowerInvariant());

    /// <summary>숫자키(1~0)용 팩토리 — Label에 숫자 문자열 전달</summary>
    public static KeySlot Number(string label, string? shiftLabel = null, VirtualKeyCode vk = VirtualKeyCode.VK_0) =>
        new(Label: label, ShiftLabel: shiftLabel, Action: new SendKeyAction(vk.ToString()), EnglishLabel: label, EnglishShiftLabel: shiftLabel);

    /// <summary>기호키용 팩토리 — Label에 기호 문자열 전달</summary>
    public static KeySlot Symbol(string label, string? shiftLabel = null, VirtualKeyCode vk = VirtualKeyCode.VK_OEM_MINUS) =>
        new(Label: label, ShiftLabel: shiftLabel, Action: new SendKeyAction(vk.ToString()), EnglishLabel: label, EnglishShiftLabel: shiftLabel);

    /// <summary>백스페이스 키용 팩토리</summary>
    public static KeySlot Backspace() =>
        new(Label: "⌫", ShiftLabel: null, Action: new SendKeyAction(VirtualKeyCode.VK_BACK.ToString()));

    /// <summary>G-테스트용: (KoreanInputModule, FakeInputService, KoreanDictionaryTestable) 튜플 팩토리</summary>
    internal static (KoreanInputModule module, FakeInputService input, KoreanDictionaryTestable dict) CreateModuleWithInput()
    {
        var input = new FakeInputService();
        var koDict = new KoreanDictionaryTestable();
        var enDict = new EnglishDictionaryTestable();
        var module = new KoreanInputModule(input, koDict, enDict);
        return (module, input, koDict);
    }
}
