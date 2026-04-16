using System.Text;

namespace AltKey.Services;

/// 한글 자모를 순서대로 받아 현재 입력 문자열(음절 단위 조합 결과)을 추적한다.
/// 두벌식 자판 기준으로 초성·중성·종성 조합을 처리한다.
public class HangulComposer
{
    static readonly string[] Choseong =
        ["ㄱ","ㄲ","ㄴ","ㄷ","ㄸ","ㄹ","ㅁ","ㅂ","ㅃ","ㅅ","ㅆ","ㅇ","ㅈ","ㅉ","ㅊ","ㅋ","ㅌ","ㅍ","ㅎ"];
    static readonly string[] Jungseong =
        ["ㅏ","ㅐ","ㅑ","ㅒ","ㅓ","ㅔ","ㅕ","ㅖ","ㅗ","ㅘ","ㅙ","ㅚ","ㅛ","ㅜ","ㅝ","ㅞ","ㅟ","ㅠ","ㅡ","ㅢ","ㅣ"];
    static readonly string[] Jongseong =
        ["","ㄱ","ㄲ","ㄳ","ㄴ","ㄵ","ㄶ","ㄷ","ㄹ","ㄺ","ㄻ","ㄼ","ㄽ","ㄾ","ㄿ","ㅀ","ㅁ","ㅂ","ㅄ","ㅅ","ㅆ","ㅇ","ㅈ","ㅊ","ㅋ","ㅌ","ㅍ","ㅎ"];

    // 겹받침: (첫째자음, 둘째자음) → 종성인덱스
    static readonly Dictionary<(string, string), int> CompoundJongseongMap = new()
    {
        {("ㄱ","ㅅ"), 3},   // ㄳ
        {("ㄴ","ㅈ"), 5},   // ㄵ
        {("ㄴ","ㅎ"), 6},   // ㄶ
        {("ㄹ","ㄱ"), 9},   // ㄺ
        {("ㄹ","ㅁ"), 10},  // ㄻ
        {("ㄹ","ㅂ"), 11},  // ㄼ
        {("ㄹ","ㅅ"), 12},  // ㄽ
        {("ㄹ","ㅌ"), 13},  // ㄾ
        {("ㄹ","ㅍ"), 14},  // ㄿ
        {("ㄹ","ㅎ"), 15},  // ㅀ
        {("ㅂ","ㅅ"), 18},  // ㅄ
    };

    // 겹모음: (첫모음, 둘째모음) → 중성인덱스
    static readonly Dictionary<(string, string), int> CompoundJungseong = new()
    {
        {("ㅗ","ㅏ"), 9},   // ㅘ
        {("ㅗ","ㅐ"), 10},  // ㅙ
        {("ㅗ","ㅣ"), 11},  // ㅚ
        {("ㅜ","ㅓ"), 14},  // ㅝ
        {("ㅜ","ㅔ"), 15},  // ㅞ
        {("ㅜ","ㅣ"), 16},  // ㅟ
        {("ㅡ","ㅣ"), 19},  // ㅢ
    };

    // 종성 → 초성 매핑
    static readonly Dictionary<string, int> JongToCho = new()
    {
        {"ㄱ", 0}, {"ㄲ", 1}, {"ㄴ", 2}, {"ㄷ", 3}, {"ㄹ", 5},
        {"ㅁ", 6}, {"ㅂ", 7}, {"ㅅ", 9}, {"ㅆ", 10}, {"ㅇ", 11},
        {"ㅈ", 12}, {"ㅊ", 14}, {"ㅋ", 15}, {"ㅌ", 16}, {"ㅍ", 17}, {"ㅎ", 18},
    };

    // 겹받침 분해: 종성문자 → (첫째, 둘째)
    static readonly Dictionary<string, (string first, string second)> JongseongDecomposition = new()
    {
        {"ㄳ", ("ㄱ", "ㅅ")},
        {"ㄵ", ("ㄴ", "ㅈ")},
        {"ㄶ", ("ㄴ", "ㅎ")},
        {"ㄺ", ("ㄹ", "ㄱ")},
        {"ㄻ", ("ㄹ", "ㅁ")},
        {"ㄼ", ("ㄹ", "ㅂ")},
        {"ㄽ", ("ㄹ", "ㅅ")},
        {"ㄾ", ("ㄹ", "ㅌ")},
        {"ㄿ", ("ㄹ", "ㅍ")},
        {"ㅀ", ("ㄹ", "ㅎ")},
        {"ㅄ", ("ㅂ", "ㅅ")},
    };

    private string _completed = "";
    private int? _choseongIdx;
    private int? _jungseongIdx;
    private int? _jongseongIdx;

    public string Current => _completed + ComposeCurrentSyllable();

    /// 조합 중인 문자가 있는지 확인
    public bool HasComposition => _choseongIdx.HasValue || _jungseongIdx.HasValue || _jongseongIdx.HasValue;

    /// 완성된 음절 수 (_completed 길이)
    public int CompletedLength => _completed.Length;

    public void Feed(string jamo)
    {
        if (string.IsNullOrEmpty(jamo)) return;

        int choIdx = Array.IndexOf(Choseong, jamo);
        int jungIdx = Array.IndexOf(Jungseong, jamo);

        if (choIdx >= 0)
            FeedChoseong(choIdx, jamo);
        else if (jungIdx >= 0)
            FeedJungseong(jungIdx, jamo);
        else
        {
            FinalizeCurrent();
            _completed += jamo;
        }
    }

    public void Backspace()
    {
        if (_jongseongIdx.HasValue)
        {
            string jongJamo = Jongseong[_jongseongIdx.Value];
            if (JongseongDecomposition.TryGetValue(jongJamo, out var parts))
            {
                _jongseongIdx = Array.IndexOf(Jongseong, parts.first);
            }
            else
            {
                _jongseongIdx = null;
            }
            return;
        }
        if (_jungseongIdx.HasValue)
        {
            string jungJamo = Jungseong[_jungseongIdx.Value];
            foreach (var ((first, second), idx) in CompoundJungseong)
            {
                if (idx == _jungseongIdx.Value)
                {
                    _jungseongIdx = Array.IndexOf(Jungseong, first);
                    return;
                }
            }
            _jungseongIdx = null;
            return;
        }
        if (_choseongIdx.HasValue)
        {
            _choseongIdx = null;
            return;
        }

        if (_completed.Length > 0)
        {
            _completed = _completed[..^1];
        }
    }

    public void Reset()
    {
        _completed = "";
        _choseongIdx = null;
        _jungseongIdx = null;
        _jongseongIdx = null;
    }

    // ── 내부 메서드 ──────────────────────────────────────────────────────────

    private void FeedChoseong(int choIdx, string jamo)
    {
        if (!_jungseongIdx.HasValue)
        {
            // 중성 없음
            if (!_choseongIdx.HasValue)
            {
                _choseongIdx = choIdx;
            }
            else
            {
                FinalizeCurrent();
                _choseongIdx = choIdx;
            }
        }
        else if (!_jongseongIdx.HasValue)
        {
            // 초성+중성 상태에서 자음 → 종성 시도
            if (IsJongseongCompatible(jamo))
            {
                _jongseongIdx = Array.IndexOf(Jongseong, jamo);
            }
            else
            {
                FinalizeCurrent();
                _choseongIdx = choIdx;
            }
        }
        else
        {
            // 초성+중성+종성 상태에서 새 자음
            // 1. 겹받침 형성 시도 (현재 종성 + 새 자음)
            string currentJong = Jongseong[_jongseongIdx.Value];
            if (CompoundJongseongMap.TryGetValue((currentJong, jamo), out var compoundIdx))
            {
                _jongseongIdx = compoundIdx;
            }
            else
            {
                // 겹받침 불가 → 현재 음절 확정(종성 포함), 새 초성 시작
                FinalizeCurrent();
                _choseongIdx = choIdx;
            }
        }
    }

    private void FeedJungseong(int jungIdx, string jamo)
    {
        if (!_jungseongIdx.HasValue)
        {
            if (!_choseongIdx.HasValue)
            {
                // 홀모음
                FinalizeCurrent();
                _completed += jamo;
            }
            else
            {
                // 초성 + 중성 조합
                _jungseongIdx = jungIdx;
            }
        }
        else if (!_jongseongIdx.HasValue)
        {
            // 초성+중성 상태에서 모음 → 겹모음 시도
            string prevJung = Jungseong[_jungseongIdx.Value];
            if (CompoundJungseong.TryGetValue((prevJung, jamo), out var compoundIdx))
            {
                _jungseongIdx = compoundIdx;
            }
            else
            {
                FinalizeCurrent();
                _jungseongIdx = jungIdx;
            }
        }
        else
        {
            // 초성+중성+종성 상태에서 모음
            // 종성을 다음 음절의 초성으로 이동
            string jongJamo = Jongseong[_jongseongIdx.Value];

            if (JongseongDecomposition.TryGetValue(jongJamo, out var parts))
            {
                // 겹받침: 첫째는 이전 음절의 종성, 둘째는 다음 음절의 초성
                int? firstJongIdx = Array.IndexOf(Jongseong, parts.first) is int fi and > 0 ? fi : null;
                int? newChoIdx = JongToCho.TryGetValue(parts.second, out var pci) ? pci : (int?)null;

                _jongseongIdx = firstJongIdx;
                FinalizeCurrent();
                _choseongIdx = newChoIdx;
                _jungseongIdx = jungIdx;
            }
            else if (JongToCho.TryGetValue(jongJamo, out var newChoIdx))
            {
                // 단일 종성을 초성으로 이동
                _jongseongIdx = null;
                FinalizeCurrent();
                _choseongIdx = newChoIdx;
                _jungseongIdx = jungIdx;
            }
            else
            {
                FinalizeCurrent();
                _jungseongIdx = jungIdx;
            }
        }
    }

    private bool IsJongseongCompatible(string jamo)
    {
        return Array.IndexOf(Jongseong, jamo) > 0;
    }

    private void FinalizeCurrent()
    {
        string syllable = ComposeCurrentSyllable();
        if (syllable.Length > 0)
        {
            _completed += syllable;
        }
        _choseongIdx = null;
        _jungseongIdx = null;
        _jongseongIdx = null;
    }

    private string ComposeCurrentSyllable()
    {
        if (_choseongIdx.HasValue && _jungseongIdx.HasValue)
        {
            int cho = _choseongIdx.Value;
            int jung = _jungseongIdx.Value;
            int jong = _jongseongIdx ?? 0;
            char ch = (char)(0xAC00 + (cho * 21 + jung) * 28 + jong);
            return ch.ToString();
        }
        if (_choseongIdx.HasValue)
            return Choseong[_choseongIdx.Value];
        if (_jungseongIdx.HasValue)
            return Jungseong[_jungseongIdx.Value];
        return "";
    }

    private static bool IsHangulSyllable(char ch)
    {
        return ch >= 0xAC00 && ch <= 0xD7A3;
    }

    private static (int cho, int jung, int jong) Decompose(char ch)
    {
        int code = ch - 0xAC00;
        int jong = code % 28;
        int jung = (code / 28) % 21;
        int cho = code / (28 * 21);
        return (cho, jung, jong);
    }
}