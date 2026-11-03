using System.IO;
using System.Reflection;
using System.Text;

namespace AltKey.Services;

/// 내장 빈도 사전 + 사용자 학습을 결합한 한국어 단어 제안 서비스
public class KoreanDictionary
{
    private static readonly string Choseong19 = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
    private const string Jungseong21 = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
    private const string Jongseong28 = "ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";
    private const int BigramBoost = 3;

    private readonly WordFrequencyStore _userStore;
    private readonly BigramFrequencyStore _bigramStore;
    private readonly IReadOnlyList<string> _builtIn;
    private readonly Dictionary<char, List<string>> _builtInByChoseong = new();

    public KoreanDictionary(
        Func<string, WordFrequencyStore> userStoreFactory,
        Func<string, BigramFrequencyStore> bigramStoreFactory)
    {
        _userStore = userStoreFactory("ko");
        _bigramStore = bigramStoreFactory("ko");
        _builtIn = LoadBuiltIn();
        IndexBuiltInByChoseong();
    }

    private void IndexBuiltInByChoseong()
    {
        foreach (var w in _builtIn)
        {
            if (w.Length == 0) continue;
            var first = w[0];
            if (first < 0xAC00 || first > 0xD7A3) continue;
            int choIdx = (first - 0xAC00) / (21 * 28);
            char choChar = Choseong19[choIdx];
            if (!_builtInByChoseong.TryGetValue(choChar, out var list))
            {
                list = new List<string>();
                _builtInByChoseong[choChar] = list;
            }
            list.Add(w);
        }
    }

    private static bool IsCompatibleJamoChoseong(char c)
    {
        return c >= '\u3131' && c <= '\u314E';
    }

    /// prefix로 시작하는 한국어 단어 제안 (사용자 학습 우선, 그 다음 내장 사전)
    public IReadOnlyList<string> GetSuggestions(string prefix, int count = 20)
    {
        if (prefix.Length < 1 || count <= 0) return [];

        // 초성만 입력된 경우 (호환 자모 U+3131~U+314E, 길이 1)
        if (prefix.Length == 1 && IsCompatibleJamoChoseong(prefix[0]))
            return GetSuggestionsByChoseong(prefix[0], count);

        if (prefix.Length > 1 && Choseong19.Contains(prefix[^1]))
        {
            var precedingPrefix = prefix[..^1];
            return GetSuggestionsWithChoseongAfterPrefix(precedingPrefix, prefix[^1], prefix.Length, count);
        }

        var continuationPrefixes = GetContinuationPrefixes(prefix);
        var codaMove = GetCodaMovedPrefix(prefix);
        // 첫 완성 음절은 사용자 사전의 정확히 일치하는 1음절 단어도 찾도록 통합 검색을 사용합니다.
        if (continuationPrefixes.Count > 1 || codaMove.HasValue
            || (prefix.Length == 1 && prefix[0] >= '\uAC00' && prefix[0] <= '\uD7A3'))
            return GetSuggestionsWithContinuations(prefix, continuationPrefixes, codaMove, count);

        var userSuggestions = _userStore.GetSuggestions(prefix, count);

        var needed = count - userSuggestions.Count;
        if (needed <= 0) return userSuggestions;

        var userSet = new HashSet<string>(userSuggestions);
        var builtInSuggestions = _builtIn
            .Where(w => w.StartsWith(prefix, StringComparison.Ordinal) && w.Length > prefix.Length
                        && !userSet.Contains(w))
            .Take(needed)
            .ToList();

        return [..userSuggestions, ..builtInSuggestions];
    }

    private IReadOnlyList<string> GetSuggestionsWithChoseongAfterPrefix(
        string precedingPrefix, char choseong, int minimumLength, int count)
    {
        var userWords = _userStore.GetSuggestionsByChoseongAfterPrefix(
            precedingPrefix, choseong, minimumLength, count);
        var result = new List<string>(userWords);
        var added = new HashSet<string>(userWords, StringComparer.Ordinal);

        foreach (var word in _builtIn)
        {
            if (word.Length <= minimumLength || word.Length <= precedingPrefix.Length
                || !word.StartsWith(precedingPrefix, StringComparison.Ordinal)) continue;

            char nextSyllable = word[precedingPrefix.Length];
            if (GetChoseong(nextSyllable) == choseong && added.Add(word))
                result.Add(word);
            if (result.Count >= count) break;
        }
        return result.Take(count).ToList();
    }

    private static char GetChoseong(char syllable)
    {
        if (syllable < '\uAC00' || syllable > '\uD7A3') return '\0';
        int index = (syllable - '\uAC00') / (21 * 28);
        return Choseong19[index];
    }

    private static (string Prefix, char Choseong)? GetCodaMovedPrefix(string prefix)
    {
        if (prefix.Length == 0) return null;
        char lastSyllable = prefix[^1];
        if (lastSyllable < '\uAC00' || lastSyllable > '\uD7A3') return null;

        var map = BuildCompatibilityJamoMap();
        var nfd = lastSyllable.ToString().Normalize(NormalizationForm.FormD);
        if (nfd.Length != 3 || !map.TryGetValue(nfd[0], out var choseong)
            || !map.TryGetValue(nfd[1], out var jungseong)
            || !map.TryGetValue(nfd[2], out var jongseong)
            || jongseong.Length != 1 || !Choseong19.Contains(jongseong[0]))
            return null;

        var openSyllable = CreateComposer(choseong, jungseong, null).Current;
        return openSyllable.Length == 1
            ? (prefix[..^1] + openSyllable, jongseong[0])
            : null;
    }

    private static bool MatchesContinuation(
        string word, IReadOnlyList<string> candidatePrefixes,
        (string Prefix, char Choseong)? codaMove)
    {
        if (candidatePrefixes.Any(p => word.StartsWith(p, StringComparison.Ordinal))) return true;
        if (codaMove is not { } move || !word.StartsWith(move.Prefix, StringComparison.Ordinal)
            || word.Length <= move.Prefix.Length) return false;
        return GetChoseong(word[move.Prefix.Length]) == move.Choseong;
    }

    private IReadOnlyList<string> GetSuggestionsWithContinuations(
        string prefix, IReadOnlyList<string> candidatePrefixes,
        (string Prefix, char Choseong)? codaMove, int count)
    {
        var result = new List<string>(count);
        var added = new HashSet<string>(StringComparer.Ordinal);

        foreach (var word in _userStore.GetSuggestionsForContinuations(
                     candidatePrefixes, prefix, codaMove?.Prefix, codaMove?.Choseong,
                     prefix.Length, count))
        {
            if (added.Add(word)) result.Add(word);
            if (result.Count >= count) return result;
        }

        // 내장 사전은 빈도 대신 기존 리소스 순위를 사용하고, 정확 prefix를 먼저 배치합니다.
        foreach (var word in _builtIn)
        {
            if (word.StartsWith(prefix, StringComparison.Ordinal)
                && word.Length > prefix.Length && added.Add(word))
                result.Add(word);
            if (result.Count >= count) return result;
        }

        foreach (var word in _builtIn)
        {
            if (word.Length > prefix.Length && !word.StartsWith(prefix, StringComparison.Ordinal)
                && MatchesContinuation(word, candidatePrefixes, codaMove)
                && added.Add(word))
                result.Add(word);
            if (result.Count >= count) break;
        }
        return result;
    }

    /// <summary>
    /// 마지막 입력 음절에서 조합기로 이어질 수 있는 종성·겹중성 후보를 생성합니다.
    /// </summary>
    private static IReadOnlyList<string> GetContinuationPrefixes(string prefix)
    {
        if (prefix.Length == 0) return [prefix];
        char lastSyllable = prefix[^1];
        if (lastSyllable < '\uAC00' || lastSyllable > '\uD7A3') return [prefix];

        var map = BuildCompatibilityJamoMap();
        var nfd = lastSyllable.ToString().Normalize(NormalizationForm.FormD);
        if (nfd.Length < 2 || !map.TryGetValue(nfd[0], out var choseong)
            || !map.TryGetValue(nfd[1], out var jungseong))
            return [prefix];

        string? jongseong = null;
        if (nfd.Length > 2)
        {
            if (!map.TryGetValue(nfd[2], out jongseong)) return [prefix];
            if (IsCompoundJongseong(jongseong))
                return GetCompoundCodaVowelPrefixes(prefix[..^1], lastSyllable, choseong, jungseong);
        }
        var variants = new HashSet<char> { lastSyllable };
        var initial = CreateComposer(choseong, jungseong, jongseong);

        if (jongseong is null)
        {
            foreach (char vowel in Jungseong21)
            {
                var vowelComposer = CreateComposer(choseong, jungseong, null);
                vowelComposer.Feed(vowel.ToString());
                AddSingleSyllable(vowelComposer.Current, variants);
                AddCompoundCodaVariants(vowelComposer.Current, variants, map);
            }
        }
        AddCompoundCodaVariants(initial.Current, variants, map);

        string stablePrefix = prefix[..^1];
        var candidatePrefixes = new HashSet<string>(
            variants.Select(syllable => stablePrefix + syllable), StringComparer.Ordinal);
        return candidatePrefixes.ToList();
    }

    private static bool IsCompoundJongseong(string jongseong) =>
        "ㄳㄵㄶㄺㄻㄼㄽㄾㄿㅀㅄ".Contains(jongseong, StringComparison.Ordinal);

    private static IReadOnlyList<string> GetCompoundCodaVowelPrefixes(
        string stablePrefix, char currentSyllable, string choseong, string jungseong)
    {
        var prefixes = new HashSet<string>(StringComparer.Ordinal) { stablePrefix + currentSyllable };

        // 겹받침을 만드는 실제 자모 입력을 재현하고, 이어지는 모음을 조합기에 전달합니다.
        foreach (char firstCoda in Choseong19)
        foreach (char secondCoda in Choseong19)
        {
            var probe = CreateComposer(choseong, jungseong, null);
            probe.Feed(firstCoda.ToString());
            probe.Feed(secondCoda.ToString());
            if (!probe.Current.Equals(currentSyllable.ToString(), StringComparison.Ordinal)) continue;

            foreach (char vowel in Jungseong21)
            {
                var composer = CreateComposer(choseong, jungseong, null);
                composer.Feed(firstCoda.ToString());
                composer.Feed(secondCoda.ToString());
                composer.Feed(vowel.ToString());
                if (composer.Current.Length > 1)
                    prefixes.Add(stablePrefix + composer.Current);
            }
        }

        return prefixes.ToList();
    }

    private static HangulComposer CreateComposer(string choseong, string jungseong, string? jongseong)
    {
        var composer = new HangulComposer();
        composer.Feed(choseong);
        composer.Feed(jungseong);
        if (jongseong is not null) composer.Feed(jongseong);
        return composer;
    }

    private static void AddCompoundCodaVariants(
        string syllable, HashSet<char> variants, IReadOnlyDictionary<char, string> map)
    {
        // 겹받침을 조합기가 표현하지 못해 음절+자모가 된 경우, 다른 종성 후보로 오인하지 않습니다.
        if (syllable.Length != 1 || syllable[0] < '\uAC00' || syllable[0] > '\uD7A3') return;

        var nfd = syllable.Normalize(NormalizationForm.FormD);
        if (nfd.Length < 2 || !map.TryGetValue(nfd[0], out var cho)
            || !map.TryGetValue(nfd[1], out var jung)) return;
        string? jong = nfd.Length > 2 && map.TryGetValue(nfd[2], out var value) ? value : null;

        foreach (char consonant in Choseong19)
        {
            var composer = CreateComposer(cho, jung, jong);
            composer.Feed(consonant.ToString());
            AddSingleSyllable(composer.Current, variants);
        }
    }

    private static void AddSingleSyllable(string value, HashSet<char> variants)
    {
        if (value.Length == 1 && value[0] >= '\uAC00' && value[0] <= '\uD7A3')
            variants.Add(value[0]);
    }

    private static Dictionary<char, string> BuildCompatibilityJamoMap()
    {
        var map = new Dictionary<char, string>();
        foreach (char jamo in Choseong19 + Jungseong21 + Jongseong28)
        {
            string decomposed = jamo.ToString().Normalize(NormalizationForm.FormKD);
            if (decomposed.Length == 1) map[decomposed[0]] = jamo.ToString();
        }

        // 종성의 표준 분해 문자는 초성 호환 자모와 별도이므로 조합기로 표본을 생성해 연결합니다.
        foreach (char consonant in Choseong19)
        {
            var composer = CreateComposer("ㄱ", "ㅏ", consonant.ToString());
            var decomposed = composer.Current.Normalize(NormalizationForm.FormD);
            if (decomposed.Length == 3) map[decomposed[2]] = consonant.ToString();
        }
        return map;
    }

    private IReadOnlyList<string> GetSuggestionsByChoseong(char choseong, int count)
    {
        var result = new List<string>(count);
        var added = new HashSet<string>();

        // 1) 사용자 학습 사전에서 해당 초성으로 시작하는 단어 (빈도순)
        var userWords = _userStore.GetSuggestionsByChoseong(choseong, count);
        foreach (var w in userWords)
        {
            if (added.Add(w))
                result.Add(w);
            if (result.Count >= count) return result;
        }

        // 2) 내장 사전에서 해당 초성으로 시작하는 단어 보충
        if (_builtInByChoseong.TryGetValue(choseong, out var builtInList))
        {
            foreach (var w in builtInList)
            {
                if (added.Add(w))
                    result.Add(w);
                if (result.Count >= count) break;
            }
        }

        return result;
    }

    /// 한국어 단어를 사용자 빈도 저장소에 기록 (완성 음절 1개 이상 + 독립 자모 없음)
    public void RecordWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;

        word = word.Trim();

        // 학습 적격성: 완성 한글 음절(U+AC00~D7A3)을 세고, 독립 자모가 섞이면 거부.
        // "ㄱ가" 같은 미완성 조합은 사전에 혼입시키면 안 된다.
        if (!IsLearnableKoreanWord(word)) return;

        _userStore.RecordWord(word);
    }

    public WordFrequencyStore UserStore => _userStore;
    public BigramFrequencyStore BigramStore => _bigramStore;

    /// 사용자 학습 저장소에서 단어를 제거. 내장 사전은 건드리지 않음.
    /// 단어가 없거나 내장 전용이면 false.
    public bool TryRemoveUserWord(string word) =>
        !string.IsNullOrWhiteSpace(word) && _userStore.RemoveWord(word.Trim());

    /// (prev, next) 쌍을 bigram 저장소에 기록. prev·next 중 하나라도 학습 부적격이면 no-op.
    public void RecordBigram(string prevWord, string nextWord)
    {
        if (!IsBigramEligible(prevWord) || !IsBigramEligible(nextWord)) return;
        _bigramStore.Record(prevWord.Trim(), nextWord.Trim());
    }

    /// bigram 적격성 판단. RecordBigram 내부에서 양쪽 피연산자에 동일 필터를 재적용한다.
    private static bool IsBigramEligible(string w) =>
        !string.IsNullOrWhiteSpace(w) && IsLearnableKoreanWord(w.Trim());

    /// 학습 적격성: 완성된 한글 음절(U+AC00~D7A3)이 1개 이상이고 독립 자모가 하나도 없어야 한다.
    private static bool IsLearnableKoreanWord(string word)
    {
        int syllableCount = 0;
        foreach (var ch in word)
        {
            if (ch >= '\uAC00' && ch <= '\uD7A3')
                syllableCount++;
            else if (IsRawJamo(ch))
                return false; // 독립 자모가 섞인 미완성 조합 — 학습 건너뜀
        }
        return syllableCount >= 1;
    }

    /// 독립 자모 문자 판단 (초성 U+3131~314E, 중/종성 U+3161~3175).
    private static bool IsRawJamo(char ch) =>
        (ch >= '\u3131' && ch <= '\u314E') || (ch >= '\u3161' && ch <= '\u3175');

    /// 이전 확정 단어(prevWord)가 있을 때의 문맥 반영 제안.
    /// prevWord가 null·빈 문자열이면 기존 GetSuggestions와 동일 결과.
    public IReadOnlyList<string> GetSuggestions(string prefix, string? prevWord, int count = 20)
    {
        var baseList = GetSuggestions(prefix, count);

        if (string.IsNullOrEmpty(prevWord)) return baseList;

        var bigramHits = _bigramStore.GetNexts(prevWord, prefix, count * 2);
        if (bigramHits.Count == 0) return baseList;

        var baseIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < baseList.Count; i++)
            baseIndex[baseList[i]] = i;

        // 빈 prefix + 이전 단어 문맥이 있으면 bigram 후보를 최대 count개까지 보여줍니다.
        int maxNewInserts = string.IsNullOrEmpty(prefix) ? count : count / 2;
        int newInserts = 0;

        var scored = new List<(string Word, double Score)>();
        foreach (var w in baseList)
        {
            double rank = baseList.Count - baseIndex[w];
            double boost = 0;
            foreach (var (next, c) in bigramHits)
            {
                if (next.Equals(w, StringComparison.Ordinal)) { boost = c * BigramBoost; break; }
            }
            scored.Add((w, rank + boost));
        }

        foreach (var (next, c) in bigramHits)
        {
            if (baseIndex.ContainsKey(next)) continue;
            if (newInserts >= maxNewInserts) break;
            scored.Add((next, c * BigramBoost));
            newInserts++;
        }

        return scored
            .OrderByDescending(t => t.Score)
            .ThenBy(t => t.Word, StringComparer.Ordinal)
            .Take(count)
            .Select(t => t.Word)
            .ToList();
    }

    /// 편집기에서 호출: 사용자가 특정 쌍 삭제.
    public bool TryRemoveBigramPair(string prev, string next) =>
        _bigramStore.RemovePair(prev, next);

    /// 앱 종료 시 호출 — 사용자 학습 데이터 즉시 저장
    public void Flush()
    {
        _userStore.Flush();
        _bigramStore.Flush();
    }

    /// <summary>
    /// 사용자 단어 저장소를 파일 기준으로 다시 읽어옵니다.
    /// </summary>
    public void ReloadUserWords() => _userStore.ReloadFromDisk();

    /// <summary>
    /// bigram 저장소를 파일 기준으로 다시 읽어옵니다.
    /// </summary>
    public void ReloadBigrams() => _bigramStore.ReloadFromDisk();

    private static IReadOnlyList<string> LoadBuiltIn()
    {
        var asm = Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream("AltKey.Assets.Data.ko-words.txt");
        if (stream is null) return [];
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
    }
}
