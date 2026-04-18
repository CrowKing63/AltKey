# 05 — 통합 테스트 · 수동 QA · 릴리스 노트

> **목적**: 01~04번에서 구현·확장된 Bigram/문맥 제안 기능을 엔드투엔드 관점에서 검증하고, 릴리스에 필요한 문서(노트·변경 이력) 문구를 작성한다.
>
> **선행 조건**: 01~04번 모두 완료 + 각 단위 테스트 녹색 + 03번 수동 시나리오 통과.

---

## 1. 체크리스트

- [ ] `AltKey.Tests/InputLanguage/BigramIntegrationTests.cs` 신규 — 모듈·사전·저장소를 모두 결합한 엔드투엔드 테스트.
- [ ] 수동 QA 매트릭스 §3 전체 통과.
- [ ] `docs/release-notes-v0.3.md` 또는 다음 버전 릴리스 노트에 기능 추가 항목 반영.
- [ ] `docs/auto-complet/findings-overview.md` §"관찰만 한 사항"의 bigram 항목을 "해결 완료(→ `docs/ac-bigram/`)"로 교체.

---

## 2. 엔드투엔드 통합 테스트

### 2.1 파일 위치

`AltKey.Tests/InputLanguage/BigramIntegrationTests.cs`

### 2.2 테스트 구성 원칙

- **가짜 Input** (`FakeInputService`)·**Testable 사전**·**실제 `KoreanInputModule`**을 결합.
- 임시 디렉터리에서 실제 JSON 파일을 생성·읽기(round-trip 검증).
- 디바운스 타이머를 기다리지 않도록 `Flush()`를 명시적으로 호출.

### 2.3 테스트 스펙

```csharp
using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using System.IO;
using Xunit;

namespace AltKey.Tests.InputLanguage;

public class BigramIntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public BigramIntegrationTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("altkey-bigram-integration").FullName;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private (KoreanInputModule module, KoreanDictionary koDict) Build(bool enabled = true)
    {
        var koStore = new WordFrequencyStore(_tempDir, "ko");
        var enStore = new WordFrequencyStore(_tempDir, "en");
        var koBigram = new BigramFrequencyStore(_tempDir, "ko");
        var enBigram = new BigramFrequencyStore(_tempDir, "en");
        var koDict = new KoreanDictionary(_ => koStore, _ => koBigram);
        var enDict = new EnglishDictionary(_ => enStore, _ => enBigram);
        var input = new FakeInputService();
        var config = new ConfigService();
        config.Current.AutoCompleteEnabled = enabled;
        return (new KoreanInputModule(input, koDict, enDict, config), koDict);
    }

    [Fact]
    public void Round_trip_bigram_survives_process_restart()
    {
        var (module, koDict) = Build();

        // 한 문장 타이핑 시뮬레이션
        FeedSyllables(module, "안녕");
        module.OnSeparator();
        FeedSyllables(module, "하세요");
        module.OnSeparator();

        koDict.Flush();

        // 두번째 인스턴스로 재시작 시뮬레이션
        var koBigram2 = new BigramFrequencyStore(_tempDir, "ko");
        Assert.True(koBigram2.Contains("안녕", "하세요"));
    }

    [Fact]
    public void Suggestion_list_reflects_context_after_finalize()
    {
        var (module, koDict) = Build();

        // 학습 데이터 축적
        for (int i = 0; i < 3; i++)
        {
            FeedSyllables(module, "안녕");
            module.OnSeparator();
            FeedSyllables(module, "하세요");
            module.OnSeparator();
        }

        // 새 문장: "안녕␣ㅎ"
        FeedSyllables(module, "안녕");
        module.OnSeparator();

        IReadOnlyList<string>? captured = null;
        module.SuggestionsChanged += list => captured = list;

        module.HandleKey(TestSlotFactory.Jamo("ㅎ", null, VirtualKeyCode.VK_G),
            new KeyContext(false, false, false, InputMode.Unicode, 0));

        Assert.NotNull(captured);
        Assert.Contains("하세요", captured!);
        Assert.Equal("하세요", captured![0]);     // 문맥 가중으로 최상단
    }

    [Fact]
    public void Toggle_off_then_input_does_not_persist_bigrams()
    {
        var (module, koDict) = Build(enabled: false);

        FeedSyllables(module, "안녕");
        module.OnSeparator();
        FeedSyllables(module, "하세요");
        module.OnSeparator();

        koDict.Flush();

        Assert.Equal(0, koDict.BigramStore.Count);
    }

    [Fact]
    public void Submode_toggle_prevents_cross_language_bigram()
    {
        var (module, koDict) = Build();

        FeedSyllables(module, "안녕");
        module.OnSeparator();
        module.ToggleSubmode();       // 영어로 전환
        FeedEnglish(module, "hello");
        module.OnSeparator();

        Assert.Equal(0, koDict.BigramStore.Count);
    }

    // 헬퍼: "안녕" → (ㅇ,ㅏ,ㄴ,ㄴ,ㅕ,ㅇ) HandleKey
    private static void FeedSyllables(KoreanInputModule module, string text) { /* ... */ }
    private static void FeedEnglish(KoreanInputModule module, string text) { /* ... */ }
}
```

### 2.4 헬퍼 구현 팁

- `FeedSyllables`·`FeedEnglish`는 `TestSlotFactory`와 `VirtualKeyCode` 맵핑이 필요하다. `KoreanInputModuleTests`에서 사용하는 슬롯 정의를 `TestHelpers.cs`로 끌어올려 공유하거나, 본 파일 내부에 지역 맵을 둔다. 유지보수 부담을 고려해 두 음절 정도의 짧은 어휘만 쓴다("안녕", "하세요", "hello").
- 음절 → 자모 분해 유틸이 없다면 하드코드된 자모 시퀀스를 받는 형태로 헬퍼를 단순화:
  ```csharp
  FeedJamoSeq(module, new[] { "ㅇ","ㅏ","ㄴ","ㄴ","ㅕ","ㅇ" });
  ```

---

## 3. 수동 QA 매트릭스

실제 빌드를 실행해 사용자 관점에서 재현. 각 항목 "PASS / FAIL"로 체크.

| # | 시나리오 | 조작 | 기대 결과 |
|---|---|---|---|
| M1 | **빈출 쌍 승격** | 자동완성 ON. "안녕하세요␣"를 3회 반복. 이후 "안녕␣" 친 뒤 "ㅎ"만 누름 | 제안 바 1위가 "하세요" |
| M2 | **초성 단독 승격** | M1과 동일 학습 후, "안녕␣ㅎ" (ㅎ 한 자모만) | 제안 바에 "하세요" 상위 포함(OS별로는 1위) |
| M3 | **토글 OFF 시 미학습** | 토글 OFF. "안녕하세요␣" 3회. `user-bigrams.ko.json` 열어 카운트 확인 | 관련 엔트리 없음 또는 기존 카운트 그대로 |
| M4 | **토글 ON→OFF 전환** | 토글 ON 상태에서 M1 수행. OFF로 전환. "안녕␣" + "ㅎ"  | 제안은 문맥 반영(기록된 데이터 활용). 새 기록은 안 됨 |
| M5 | **가/A 토글 격리** | "안녕␣" 후 "가/A" 버튼. "hello␣" | `user-bigrams.en.json`에 "안녕" 키 없음 |
| M6 | **제안 수락 bigram 기록** | "안녕␣" 후 "ㅎ" → "하세요" 제안 수락 (터치/클릭) | `user-bigrams.ko.json`에 "안녕":"하세요" 카운트 +1 |
| M7 | **코어 회귀 1** | "해"+Shift+ㅆ | "했" 정상 |
| M8 | **코어 회귀 2** | "ㄷㅏㄹㄱ" → 완성 | "닭" 정상 |
| M9 | **코어 회귀 3** | "화사"에서 BS 3회 | 빈 필드 |
| M10 | **코어 회귀 4** | 조합 중 엔터 | 다음 줄로, 조합 확정 |
| M11 | **앱 재시작 영속성** | M1 수행 → 앱 종료 → 재시작 → "안녕␣ㅎ" | 재시작 후에도 "하세요" 상위 |
| M12 | **프루닝 동작** | 수천 개 bigram을 강제 주입(디버그 스크립트). 제안 정상 동작 | 크래시 없음, 저장 파일 크기 하한 이하 유지 |
| M13 | **편집기 탭(선택 구현 시)** | 설정 → 사용자 사전 편집기 → 바이그램 탭 | 리스트 로드, 개별 삭제/prev 삭제/전체 비우기 동작 |

M7~M10은 CORE-LOGIC-PROTECTION 회귀 체크. 하나라도 FAIL이면 즉시 03번 변경 롤백 후 원인 분석.

---

## 4. 릴리스 노트 문구

`docs/release-notes-v0.X.md` (다음 버전 파일명은 태그 컨벤션에 맞춤)에 다음 블록 추가:

```markdown
## 자동완성 제안 품질 — 문맥(Bigram) 반영

- 이전에 확정된 단어를 기억해 다음 단어 제안의 품질을 높입니다.
  - 예: "안녕하세요"를 자주 입력한 사용자는 "안녕 " 입력 뒤 "ㅎ"만 눌러도 제안 첫 줄이 "하세요"로 올라옵니다.
- 학습은 자동완성 토글이 켜져 있을 때만 이루어지며, 기존 단어 학습 규칙(최소 2음절/2글자)과 동일하게 적용됩니다.
- 학습 데이터는 `%AppData%\AltKey\user-bigrams.{ko,en}.json`에 저장되며 원자적 쓰기로 손실 방지됩니다.
- 한국어/영어 간 문맥은 분리됩니다(언어 교차 오염 방지).
- 사용자 사전 편집기에 "바이그램" 탭이 추가되어 쌍 단위 삭제·이전 단어 전체 삭제·전체 비우기가 가능합니다. *(편집기 UI가 이번 릴리스에 포함된 경우에만)*
```

번역·어조는 기존 `docs/release-notes-v0.3.md`의 톤을 따른다. 기술 용어("bigram")는 괄호 설명 병기.

---

## 5. findings-overview 갱신

`docs/auto-complet/findings-overview.md` §"관찰만 한 사항" 블록:

```diff
- - **Bigram/문맥 제안**: 이전 단어 고려한 제안은 현재 없음. 설계 추가 작업으로 분류.
+ - **Bigram/문맥 제안**: 2026-04-XX 릴리스에서 구현 완료 — `docs/ac-bigram/` 참조. `KoreanInputModule`이 마지막 확정 단어를 추적하여 `KoreanDictionary.GetSuggestions(prefix, prevWord)` 경로로 문맥 가산을 적용한다.
```

날짜는 실제 머지일자로 교체. 링크는 상대경로.

---

## 6. CORE-LOGIC-PROTECTION 문서 보강 (선택)

`docs/auto-complet/CORE-LOGIC-PROTECTION.md`에 한 줄 항목 추가 (§3 "만져도 되는 영역" 말미):

```markdown
| bigram 기록 호출부(`KoreanInputModule.FinalizeComposition`·`AcceptSuggestion`) | 단, 기존 unigram `RecordWord` 호출 규율(2음절 이상 등)은 유지. `RecordBigram`은 `KoreanDictionary`/`EnglishDictionary` 내부에서 양쪽 피연산자에 동일 필터를 재적용한다. |
```

§2 "절대 건드리지 말 것"에는 **추가하지 않는다** — 새 자유도를 문서화한 뒤에 굳히기는 이르다. 다음 큰 리팩터 이후 고려.

---

## 7. 완료 조건

- [ ] `BigramIntegrationTests.cs` 4개 테스트 녹색.
- [ ] 수동 QA 매트릭스 M1~M11 전부 PASS. (편집기 선택 구현 시 M13 포함).
- [ ] `dotnet build` / `dotnet test` 전체 녹색.
- [ ] 릴리스 노트에 기능 추가 문구 반영.
- [ ] `findings-overview.md`의 bigram 항목 갱신.
- [ ] 커밋 메시지: `docs(ac-bigram): release notes, integration tests, overview patch`.

---

## 8. 이후 고려 사항 (범위 밖, 기록만)

- **trigram 확장**: 이번에는 거부. 향후 데이터 크기·품질 이득이 확인되면 별도 설계 문서로.
- **시간 감쇠 랭킹**: 최근 N일 내 등장 빈도에 가중치. 타임스탬프 필드가 없어 스키마 변경 필요 — 별도 작업.
- **문장 단위 분리**: 마침표·물음표·느낌표 이후 `_lastCommittedWord = null` 초기화. 현재 `IsSeparator`가 이미 구두점을 포함해 finalize를 일으키지만, finalize가 `_lastCommittedWord`를 갱신해 버린다. "문장 끝에서는 문맥을 끊자"는 정책은 일부 UX에서 더 자연스러울 수 있다. 향후 작업.
- **내장 bigram 리소스**: 한국어·영어 말뭉치에서 빈출 bigram 상위 N개를 리소스로 번들. 신규 사용자의 "아직 학습 데이터 없음" 상태를 덮어준다. 라이선스·용량 고려 필요.
- **OS 포커스 전환 감지**: 다른 앱으로 포커스가 이동했다가 돌아왔을 때 `_lastCommittedWord`를 초기화. `UIAutomation` 호출이 필요해 비용이 크고 권한 이슈가 있어 미뤄둔다.

이 기록 자체가 다음 이터레이션 계획의 출발점이 된다.
