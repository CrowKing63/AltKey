# Bigram / 문맥 제안 — 작업 전체 개요

> **이 폴더의 역할**: `docs/auto-complet/findings-overview.md` §관찰만 한 사항의 "Bigram/문맥 제안: 이전 단어 고려한 제안은 현재 없음. 설계 추가 작업으로 분류." 항목을 실제 구현 계획으로 확장한 작업 지시서 모음. 각 지시서는 다른 지시서를 읽지 않고도 독립된 AI 에이전트가 작업을 완수할 수 있도록 자기완결적으로 작성되어 있다. **이 개요 문서는 에이전트가 제일 먼저 읽는다.**
>
> **전제**: 자동완성 핵심 파이프라인(`KoreanInputModule`, `HangulComposer`, `SendAtomicReplace`, 사용자 사전 저장·원자적 쓰기)은 이미 안정화되어 있다. 이번 작업은 그 위에 **"이전에 확정한 단어를 기억해 다음 단어 제안의 품질을 높인다"**는 얇은 레이어를 추가한다.

---

## 0. TL;DR

현재 자동완성은 **현재 조합 중 prefix 하나**만 보고 제안을 만든다. 사용자가 "안녕하세요"를 자주 쓴다고 해도, "안녕"을 확정한 뒤 공백 다음에 "ㅎ"를 누르면 제안은 `_koDict.GetSuggestions("ㅎ")` 일반 랭킹에서 나온다. "이전 단어가 '안녕'이면 '하세요'가 먼저 떠야 한다"는 문맥 정보가 전혀 반영되지 않는다.

이번 이터레이션은 **마지막으로 확정된(flush된) 단어**를 "이전 단어"로 기억하고, 해당 문맥에서 함께 등장한 "다음 단어" 빈도를 별도의 사전(bigram 저장소)에 누적한다. 제안 갱신 시점에 `prev_word + current_prefix` 조합으로 사전을 조회해 상위권으로 끌어올린다.

| 순서 | 기능 | 난이도 | 지시서 |
|---|---|---|---|
| 1 | `BigramFrequencyStore` 신규 서비스 (저장·로드·기록·조회 API + 테스트) | 중간 | [01-bigram-store.md](01-bigram-store.md) |
| 2 | `KoreanDictionary`·`EnglishDictionary`에 문맥 파라미터가 있는 오버로드 추가 + ranking 규칙 | 중간 | [02-dictionary-context.md](02-dictionary-context.md) |
| 3 | `KoreanInputModule`에서 "이전 단어" 추적, `FinalizeComposition`/`AcceptSuggestion`에서 bigram 기록, `HandleKey`에서 문맥 전달 | **높음** (코어 파이프라인 접촉) | [03-module-wire.md](03-module-wire.md) |
| 4 | `AutoCompleteService`·ViewModel 경유 관로 정리, 사용자 사전 편집기에 bigram 탭 추가(선택) | 중간 | [04-service-ui.md](04-service-ui.md) |
| 5 | 엔드투엔드 통합 테스트 매트릭스, 수동 QA 시나리오, 릴리즈 노트 문구 | 낮음 | [05-testing-release.md](05-testing-release.md) |

**권장 순서**: 01 → 02 → 03 → 04 → 05. 01은 다른 작업과 완전히 독립이며 가장 안전하다. 02는 01의 API에 의존한다. 03은 [CORE-LOGIC-PROTECTION](../auto-complet/CORE-LOGIC-PROTECTION.md) §2에 걸리는 영역을 건드리므로 01·02의 테스트가 먼저 녹색이어야 한다.

---

## 1. 현재 상태 (2026-04-18 기준)

### 1.1 이미 되어 있는 것

- **단어(unigram) 빈도 저장**: [`WordFrequencyStore`](../../AltKey/Services/WordFrequencyStore.cs)가 언어별 JSON(`user-words.ko.json`, `user-words.en.json`)에 `단어: 빈도`를 저장. 1초 디바운스 + 원자적 쓰기(tmp + File.Move) + 한글 `UnsafeRelaxedJsonEscaping`. `MaxWords = 5000`에서 하위 20% 프루닝.
- **학습 시점**: `KoreanInputModule.FinalizeComposition()` (separator/구두점/조합키 도달 시) + `KoreanInputModule.AcceptSuggestion()` (제안 수락 시). 토글이 꺼져 있으면 학습 스킵(`_config.Current.AutoCompleteEnabled`).
- **제안 조회**: `KoreanDictionary.GetSuggestions(prefix, count)` → 사용자 사전 빈도순 + 내장 사전으로 보충. 초성 단독(U+3131~U+314E)일 때는 `GetSuggestionsByChoseong`으로 분기. 영어도 동일 구조.
- **제안 수락·제거**: `SuggestionBarViewModel`의 `AcceptSuggestionCommand`/`RemoveSuggestionCommand` + 사용자 사전 편집기(`UserDictionaryEditorWindow`)에서 단어·빈도 CRUD. [`docs/ac-editor/`](../ac-editor/)에서 구현 완료.

### 1.2 비어 있는 것 (이 작업이 채운다)

- **이전 단어 기억 장치 없음**: `FinalizeComposition` 직후 `_composer.Reset()` + `_englishPrefix = ""`로 내부 상태가 사라진다. 다음 자모 입력 시점에 "방금 확정한 게 뭐였지?"를 알 방법이 없다.
- **bigram 누적 저장소 없음**: `WordFrequencyStore`는 키가 `string`(단일 단어). `(prev, next)` 쌍을 저장할 구조가 없다.
- **문맥을 받는 제안 API 없음**: `KoreanDictionary.GetSuggestions(prefix)` 한 가지 시그니처만 있고, "이전 단어"를 파라미터로 넘길 통로가 없다.
- **UI 파이프라인**: `SuggestionsChanged?.Invoke(_koDict.GetSuggestions(...))` 호출 지점이 `HandleKey`/`HandleBackspace` 두 군데에 있는데, 여기서 문맥을 함께 넘겨줘야 한다.

---

## 2. 사용자 시나리오

작업 완료 시점에 사용자는 다음을 체감할 수 있어야 한다.

1. **빈출 인사 시나리오**: "안녕하세요"를 세 번 반복 입력하면, 네 번째부터는 "안녕" + 공백 + "ㅎ"만 눌러도 제안 바 첫 번째가 "하세요"가 된다. 현재는 `_koDict.GetSuggestions("하")`의 일반 빈도 순위에 의존.
2. **도메인 어휘 시나리오**: 회의록에서 "2026년" + 공백 + "4월"을 자주 입력한 사용자는, "2026년" 확정 후 "ㅅ"만 눌러도 "사분기"·"4월" 같은 제안이 상위로 승격됨(초성 경로에서도 동작).
3. **복귀 시나리오**: 사용자가 앱을 종료했다 다시 켜도, 학습된 bigram은 JSON에서 복원되어 즉시 적용된다.
4. **프라이버시 시나리오**: 자동완성 토글이 OFF인 동안에는 bigram도 기록되지 않는다(unigram과 동일 규칙).
5. **제거 시나리오**: 사용자가 사전 편집기에서 bigram 한 쌍을 삭제하거나, 특정 `prev`에 속한 모든 다음 단어를 한꺼번에 지울 수 있다(선택 기능 — 04번 지시서에서 상세).

---

## 3. 핵심 설계 원칙

### 3.1 CORE-LOGIC-PROTECTION 규정을 지킨다

[`docs/auto-complet/CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md) §2 전체는 이 작업에서도 **불가침**. 특히:

- `HangulComposer` 내부 (`Feed`, `Backspace`, `FinalizeCurrent`, `ComposeCurrentSyllable`) — 전혀 접촉하지 않는다.
- `KoreanInputModule.HandleKey()`의 분기 구조(`isComboKey`, `jamo==null`, `TrackedOnScreenLength` 관리) — 제어 흐름을 바꾸지 않는다. **호출 파라미터만** 문맥으로 확장한다.
- `InputService.SendAtomicReplace()`의 단일 `SendInput` 호출 — 접촉하지 않는다.
- `WordFrequencyStore`의 `UnsafeRelaxedJsonEscaping`·원자적 쓰기·1초 디바운스 — 신규 `BigramFrequencyStore`도 같은 규율을 따르되 기존 파일을 변경하지 않는다.

### 3.2 Bigram은 별도 저장소로 분리

- **같은 파일에 키 포맷으로 섞지 않는다**. `user-words.ko.json`은 `{단어: 빈도}` 스키마를 유지한다. bigram은 새 파일 `user-bigrams.ko.json`에 저장한다.
- 사유: (a) 기존 JSON 파서·마이그레이션 코드를 건드리지 않는다. (b) 프루닝 정책이 unigram과 다르다(O(n²) 증가율). (c) 편집기 UI에서 두 개념을 분리해 보여주기 쉽다.

### 3.3 Bigram 기록은 학습 토글을 **한 개**로 재사용

- 별도 `BigramLearningEnabled` 같은 플래그를 만들지 않는다. 이미 존재하는 `AppConfig.AutoCompleteEnabled`가 OFF면 unigram·bigram 모두 학습 스킵이다.
- UX 일관성: 사용자가 "자동완성 꺼짐" 상태에서 타이핑했는데 자신도 모르게 bigram이 쌓이면 놀란다. 동일 토글이 둘을 동시 제어.

### 3.4 제안 ranking — 보수적으로 가산만

- 기존 `GetSuggestions`의 반환 리스트(사용자 빈도 + 내장 사전) 위에 **상위 가산**으로 얹는다. 기존 제안을 밀어내지 않고, bigram 후보를 1~2개 앞쪽에 삽입하거나 점수 가중치로 재정렬.
- 이유: bigram 데이터가 아직 없는 새 사용자에게 회귀 느낌이 들지 않게 한다. "이상한 단어가 추천된다"는 불만을 피한다.
- 상세 공식은 [02-dictionary-context.md](02-dictionary-context.md) §3 "Ranking 규칙" 참고.

### 3.5 문맥은 "직전에 확정된 단어 한 개"까지만

- n-gram 일반화(trigram·4-gram)는 **범위 외**. 저장·랭킹 복잡도가 기하급수로 오르고, 데이터 희소성이 심해져 비용 대비 효용이 떨어진다.
- "직전 단어"의 정의: `FinalizeComposition()`이 마지막으로 `RecordWord()`를 호출한 단어, 또는 `AcceptSuggestion()`이 수락한 단어. 두 경로 모두에서 동일한 "마지막 확정 단어" 슬롯을 갱신한다.
- 서브모드 토글(`ToggleSubmode`)·레이아웃 전환(`Reset`)·앱 재시작 시 "직전 단어"는 초기화한다(=문맥 없음).
- 사용자가 마우스로 포커스를 옮기거나 다른 앱으로 전환한 경우는 **감지할 방법이 없으므로 허용한다**. 과하게 정교하게 만들면 CORE-LOGIC을 침범한다.

### 3.6 MVVM / DI 패턴 유지

- `BigramFrequencyStore`는 싱글톤으로 DI 등록(`App.xaml.cs`).
- `KoreanDictionary`/`EnglishDictionary` 생성자에 `BigramFrequencyStore` 생성자 주입 추가(언어별 인스턴스).
- UI에 노출되는 편집 기능은 `UserDictionaryEditorViewModel`에 탭 추가로 흡수(04번 지시서).

---

## 4. 범위 밖 (이번에 하지 않는 것)

- Trigram·n-gram 일반화.
- 내장(빌트인) bigram 리소스 배포. 순수 사용자 학습만 다룬다.
- 다국어 (언어 간) bigram. 언어별로 완전히 분리.
- 시간 감쇠(time decay) 기반 랭킹. 단순 빈도만.
- 문장 단위 학습(문장 경계 감지, 마침표·물음표 이후 리셋 등). 현재 `IsSeparator`가 이미 구두점을 포함하므로 "직전 단어가 문장의 마지막 단어일 수도 있다"는 얕은 근사로 충분하다.
- OS 포커스/앱 전환 감지로 문맥 리셋. 감지 비용·권한 이슈가 크고 자동완성 끄기로 갈음 가능.

---

## 5. 빌드·테스트 환경

- **빌드**: `AltKey/AltKey.csproj` (`dotnet build`)
- **테스트**: `AltKey.Tests/AltKey.Tests.csproj` (`dotnet test`)
- **PowerShell 주의**: `&&` 대신 `;` 사용 또는 명령 분리
- **디렉터리**:
  - 데이터 파일은 `PathResolver.DataDir` 하위(기존과 동일). 파일명은 `user-bigrams.ko.json`, `user-bigrams.en.json`.
  - 기존 unigram 테스트(`AltKey.Tests/Services/WordFrequencyStoreTests.cs`, `AltKey.Tests/InputLanguage/KoreanDictionaryTests.cs`)가 녹색인 채로 남아야 한다.

---

## 6. 파일 구조 요약 (작업 후)

```
AltKey/
├── Services/
│   ├── BigramFrequencyStore.cs          (신규 — 01번 지시서)
│   ├── WordFrequencyStore.cs            (변경 없음)
│   ├── KoreanDictionary.cs              (변경 — 02번: GetSuggestions 오버로드, RecordBigram 위임)
│   ├── EnglishDictionary.cs             (변경 — 02번: 동일 확장)
│   └── InputLanguage/
│       ├── IInputLanguageModule.cs      (변경 — 03번: CurrentContextPrev 속성 or HandleKey 호출은 기존, 내부 상태 추가)
│       └── KoreanInputModule.cs         (변경 — 03번: _lastWord 필드, RecordBigram 호출, GetSuggestions 인자 확장)
├── ViewModels/
│   ├── SuggestionBarViewModel.cs        (변경 최소 or 무 — 03번에서 결정)
│   └── UserDictionaryEditorViewModel.cs (변경 선택 — 04번: Bigram 탭)
├── Views/
│   └── UserDictionaryEditorWindow.xaml  (변경 선택 — 04번: Bigram 탭 UI)
└── App.xaml.cs                          (변경 — 01번: BigramFrequencyStore 팩토리 DI)

AltKey.Tests/
├── Services/
│   ├── WordFrequencyStoreTests.cs       (변경 없음)
│   └── BigramFrequencyStoreTests.cs     (신규 — 01번)
└── InputLanguage/
    ├── KoreanDictionaryTests.cs         (변경 — 02번: context 오버로드 테스트)
    ├── KoreanInputModuleTests.cs        (변경 — 03번: bigram 기록·제안 회귀 테스트)
    └── BigramIntegrationTests.cs        (신규 — 05번: 엔드투엔드)
```

---

## 7. 작업 시 에이전트가 지켜야 할 원칙

1. **먼저 [`docs/auto-complet/CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md) §2를 읽는다**. 거기에 명시된 코드 경로는 어떤 핑계로도 건드리지 않는다.
2. **현재 파일 내용을 반드시 읽는다**. `ac-editor` 이터레이션 이후 `WordFrequencyStore`에 `SetFrequency`·`RemoveWord`·`GetAllWords`·`Clear`·`GetSuggestionsByChoseong`이 이미 존재한다. 중복 구현 금지.
3. **01부터 순차 진행**한다. 01의 테스트가 녹색이 아니면 02로 넘어가지 않는다. 03은 특히 위험하므로 01·02 테스트 스위트 전체가 녹색이어야 착수한다.
4. **테스트부터 쓸 수 있는 부분은 테스트부터 쓴다**(저장소·사전 레벨). ViewModel·UI는 수동 시나리오로 검증한다.
5. **한 파일 수정 범위를 이 지시서 §파일 구조 요약의 "변경" 목록 바깥으로 확대하지 않는다**. 범위 밖 리팩터링 금지.
6. **빌드가 깨지면 즉시 멈추고 수정한다**. "나중에 해결"로 미루지 않는다.
7. **커밋 메시지 스코프**: `feat(ac-bigram): ...` / `test(ac-bigram): ...` / `refactor(ac-bigram): ...`. 최근 커밋 스타일(`feat(ac-editor): ...`) 참고.

---

## 8. 참고 문서

- [`docs/auto-complet/CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md) — **필독**.
- [`docs/auto-complet/findings-overview.md`](../auto-complet/findings-overview.md) — 이 작업의 출발점.
- [`docs/ac-editor/00-overview.md`](../ac-editor/00-overview.md) — 사용자 사전 편집기 구조(04번 참고).
- [`docs/feature-korean-autocomplete.md`](../feature-korean-autocomplete.md) — 자동완성 원래 설계.
- [`AGENTS.md`](../../AGENTS.md) — 리포 전역 에이전트 가이드.

---

## 9. 다음 단계

- 단일 작업만 할당받은 에이전트는 해당 번호 지시서로 이동한다.
- 전 단계 할당받은 에이전트는 **01 → 02 → 03 → 04 → 05** 순으로 진행한다.
- 작업 중 개요에 없는 충돌이 생기면 즉시 멈추고 사용자에게 보고한다. 임의로 범위 확장 금지.
