using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using System.Collections.Concurrent;

namespace AltKey.Tests.InputLanguage;

/// <summary>
/// 테스트용 가짜 InputService — 실제 SendInput을 호출하지 않고 기록만 남김.
/// </summary>
public sealed class FakeInputService : InputService
{
    public ConcurrentBag<string> SentUnicodes { get; } = new();
    public ConcurrentBag<(int prevLen, string next)> AtomicReplaces { get; } = new();
    public List<VirtualKeyCode> KeyPresses { get; } = new();

    public override void SendUnicode(string text) => SentUnicodes.Add(text);

    public override void SendAtomicReplace(int prevLen, string next)
    {
        AtomicReplaces.Add((prevLen, next));
        TrackedOnScreenLength = next.Length;
    }

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

    public new IReadOnlyList<string> GetSuggestionsByChoseong(char choseong, int count = 5)
    {
        return _freq
            .Where(kv => kv.Key.Length > 0
                         && kv.Key[0] >= '\uAC00' && kv.Key[0] <= '\uD7A3'
                         && GetChoseongChar(kv.Key[0]) == choseong)
            .OrderByDescending(kv => kv.Value)
            .Take(count)
            .Select(kv => kv.Key)
            .ToList();
    }

    private static char GetChoseongChar(char syllable)
    {
        const string ch = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
        int idx = (syllable - 0xAC00) / (21 * 28);
        return ch[idx];
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
public sealed class KoreanDictionaryTestable : KoreanDictionary
{
    private readonly WordFrequencyStoreInMemory _store;
    public int UserWordCount => _store.UserWordCount;

    public KoreanDictionaryTestable()
        : base(lang => CreateStore(lang), lang => new BigramFrequencyStore(lang))
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
public sealed class EnglishDictionaryTestable : EnglishDictionary
{
    public EnglishDictionaryTestable()
        : base(_ => new WordFrequencyStoreInMemory(), lang => new BigramFrequencyStore(lang))
    { }
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
    internal static (KoreanInputModule module, FakeInputService input, KoreanDictionaryTestable dict) CreateModuleWithInput(
        bool autoCompleteEnabled = true)
    {
        var input = new FakeInputService();
        var koDict = new KoreanDictionaryTestable();
        var enDict = new EnglishDictionaryTestable();
        var config = new ConfigService();
        config.Current.AutoCompleteEnabled = autoCompleteEnabled;
        var module = new KoreanInputModule(input, koDict, enDict, config);
        return (module, input, koDict);
    }

    /// <summary>Bigram 테스트용: 양쪽 사전 모두 접근 가능한 팩토리</summary>
    internal static (KoreanInputModule module, FakeInputService input, KoreanDictionaryTestable koDict, EnglishDictionaryTestable enDict) CreateModuleWithBothDicts(
        bool autoCompleteEnabled = true)
    {
        var input = new FakeInputService();
        var koDict = new KoreanDictionaryTestable();
        var enDict = new EnglishDictionaryTestable();
        var config = new ConfigService();
        config.Current.AutoCompleteEnabled = autoCompleteEnabled;
        var module = new KoreanInputModule(input, koDict, enDict, config);
        return (module, input, koDict, enDict);
    }

    /// <summary>한글 음절 문자열을 자모로 분해해 HandleKey로 주입하는 헬퍼.</summary>
    internal static void FeedSyllables(KoreanInputModule module, string text, KeyContext ctx)
    {
        foreach (char syllable in text)
        {
            if (syllable < '\uAC00' || syllable > '\uD7A3')
            {
                // 한글 음절이 아니면 자모 그대로 시도
                var directSlot = Jamo(syllable.ToString(), null, (VirtualKeyCode)(0x41 + syllable % 26));
                module.HandleKey(directSlot, ctx);
                continue;
            }

            int offset = syllable - '\uAC00';
            int choIdx = offset / (21 * 28);
            int jungIdx = (offset % (21 * 28)) / 28;
            int jongIdx = offset % 28;

            const string choseong = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
            const string jungseong = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
            const string jongseong = "\0ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";

            // VK 코드는 충돌만 없으면 아무거나 OK — Label이 실제 자모를 결정
            VirtualKeyCode baseVk = (VirtualKeyCode)(0x41 + (choIdx * 7 + jungIdx) % 26);

            module.HandleKey(Jamo(choseong[choIdx].ToString(), null, baseVk), ctx);

            VirtualKeyCode jungVk = (VirtualKeyCode)(0x42 + (choIdx * 7 + jungIdx) % 26);
            module.HandleKey(Jamo(jungseong[jungIdx].ToString(), null, jungVk), ctx);

            if (jongIdx > 0)
            {
                VirtualKeyCode jongVk = (VirtualKeyCode)(0x43 + (choIdx * 7 + jungIdx + jongIdx) % 26);
                module.HandleKey(Jamo(jongseong[jongIdx].ToString(), null, jongVk), ctx);
            }
        }
    }

    /// <summary>영어 문자열을 한 글자씩 QuietEnglish 경로로 주입하는 헬퍼.</summary>
    internal static void FeedEnglish(KoreanInputModule module, string text, KeyContext ctx)
    {
        foreach (char ch in text)
        {
            var vk = (VirtualKeyCode)(0x41 + (char.ToUpperInvariant(ch) - 'A'));
            var slot = English(ch.ToString(), null, vk);
            module.HandleKey(slot, ctx);
        }
    }
}
