# TASK-09 — suffix-only rewrite: 조합·해체·수락 시 전체 재작성 제거

> **유형**: 성능 최적화 (단어 길이에 비례하는 입력 딜레이 해소)
> **선행 독해**: [CORE-LOGIC-PROTECTION.md](CORE-LOGIC-PROTECTION.md) §0, §2, §4.3 반드시 완독
> **영향 범위**: Unicode 모드(비관리자)의 HangulJamo / QuietEnglish 서브모드 — 자모 탭, 백스페이스, 제안 수락 3개 경로
> **예상 소요**: 코드 수정 1~2시간 + 회귀 테스트

---

## 1. 문제

현재 Unicode 모드의 모든 화면 갱신은 `SendAtomicReplace(prevLen, newOutput)`로 "이전 텍스트 전체를 백스페이스로 지우고 새 텍스트 전체를 유니코드로 다시 쓴다".

| 경로 | prevLen | newOutput |
|---|---|---|
| 자모 탭 (`HandleKey`) | `ctx.TrackedOnScreenLength` (전 단어 길이) | `_composer.Current` (전체) |
| 백스페이스 (`HandleBackspace`) | `ctx.TrackedOnScreenLength` | `_composer.Current` (전체) |
| 제안 수락 (`AcceptSuggestion` → VM) | 현재 조합 전체 길이 | 제안어 전체 |

비용은 **단어 길이에 제곱급**으로 늘어난다. 조합 알고리즘 특성상 한 타포에 변하는 것은 항상 꼬리 1~2자(종성→다음 초성 전이, 겹받침/겹모음 해소)뿐인데도 매번 전체를 지우고 다시 쓴다.

실측 시뮬레이션(`HangulComposer` C# 알고리즘을 그대로 이식해 `tools/hangul/hangul_calc.py` ground truth와 대조):

| 단어 | 음절 | 현재 방식 (이벤트 / settle sleep) | suffix 방식 | 절감 |
|---|--:|--:|--:|--:|
| 안녕하세요 | 5 | 114 / 275ms | 46 / 225ms | 2.5x |
| 한국어 입력은 정말 편합니다 | 15 | 1,090 / 875ms | 122 / 575ms | 8.9x |
| 접근성을 위한 가상 키보드를 만들고 있습니다 | 24 | 2,564 / 1,325ms | 196 / 925ms | 13.1x |

- 총 SendInput 이벤트: **O(n²) → O(n)** (타포당 ≤1 BS + ≤2 Unicode, 상수)
- `BackspaceSettleDelayMs`(25ms)는 BS 배치가 있을 때만 걸린다. suffix 방식에서 순수 추가 타포(0 BS)는 sleep 자체를 건너뜀 → 체감 딜레이 직접 감소
- 백스페이스 노출 개수가 n−1 → ≤1로 줄면, Win32 EDIT 이벤트 드랍 버그(settle 지연 도입의 원인)에 노출되는 기회도 급감

## 2. 기각된 대안 (왜 안 하는가)

### 2.1 단어 단위 선택 삭제 (Ctrl+Shift+화살표 + BS)
- Ctrl+Shift+Left로 "단어 선택"은 **앱마다 동작이 다름** (Win32 EDIT, 게임, 비밀번호 필드, 브라우저 토큰화). AltKey가 이미 앱별 이벤트 처리 차이로 고난을 겪은 영역.
- 삭제 1회에 Ctrl/Shift down·up + 화살표 + BS = 8개 이벤트. 4자 이하 단어는 N번 백스페이스보다 느림.
- 선택→삭제→재입력이 3단계 비원자 연산. 중간에 끼어들면 화면에 선택된 텍스트가 남음.

### 2.2 클립보드 붙여넣기
- 매 키스트로크마다 **글로벌 클립보드를 오염**시킴.
- `SetClipboardData` → Ctrl+V는 두 번의 별도 연산(비원자). 일부 앱은 패스트 차단.
- 결정적으로, 붙여넣기 **전** 기존 단어를 지우는 O(n) 백스페이스 문제가 그대로 남아 핵심 비용(삭제)을 해결 못 함.

## 3. 설계 — suffix-only rewrite

### 3.1 핵심 불변 조건 (invariant)

Unicode 모드에서 `_composer.Current`는 **OS 화면 텍스트와 1:1 동기화**된다 (CORE-LOGIC-PROTECTION §0).
Feed/Backspace/Reset의 각 경로가 `SendAtomicReplace`(TrackedOnScreenLength 갱신) 또는 Reset 쌍(`FinalizeComposition`, `CancelComposition`, `CommitCurrentWord`)과 짝을 이루므로, 새 상태 추적 없이도 "Feed 이전 화면 텍스트"를 `_composer.Current`로 알 수 있다.

### 3.2 알고리즘

```
old = _composer.Current          // Feed/Backspace 이전 (== 화면)
_composer.Feed(jamo)             // 또는 Backspace()
new = _composer.Current
p = CommonPrefixLength(old, new)
SendAtomicReplace(old.Length - p, new[p..], new.Length)   // 3-인자 오버로드
```

- `CommonPrefixLength`: 두 문자열의 공통 접두사 길이 (O(n), n은 음절 수 — 수십 이하)
- **3-인자 오버로드** `SendAtomicReplace(prevLen, newText, fullNewLength)`: 추적 길이는
  suffix 길이(`newText.Length`)가 아니라 **작업 후 화면 전체 길이**여야 한다.
  `TrackedOnScreenLength`는 백스페이스 가드와 조합 확정 판단의 기준이므로
  suffix만 기준으로 두면 "한"→"하"처럼 지우고 남는 글자가 있을 때 추적 0으로 깨진다.
- 기존 2-인자 버전은 `fullNewLength == newOutput.Length` 특수케이스로 위임 (CommitCurrentWord 등 호환)
- Backspace 경로는 new가 old의 접두사이므로 항상 `new[p..] == ""` → `(oldLen - newLen, "", newLen)`

### 3.3 경로별 변경

| 경로 | 현재 | 변경 후 |
|---|---|---|
| `HandleKey` 자모 (Unicode) | `SendAtomicReplace(prevLen, newOutput)` | `SendAtomicReplace(oldLen - p, newOutput[p..], newOutput.Length)` |
| `HandleBackspace` (Unicode) | `SendAtomicReplace(prevLen, newOutput)` | `SendAtomicReplace(oldLen - p, "", newOutput.Length)` |
| `AcceptSuggestion` (Unicode) | `(bs=oldLen, fullWord)` → VM이 전체 전송 | `(bs=oldLen-p, fullWord, p)` → VM이 `fullWord[p..]`만 전송 |

VirtualKey(관리자) 모드: **변경 없음** (OS IME가 조합을 처리하므로 화면 문자열 비교 기반 최적화 불가. 기존 `CompletedLength + CompositionDepth` BS + 전체 Unicode 유지, prefixLength=0).

### 3.4 계약 변경

```csharp
// IInputLanguageModule — 현재
(int backspaceCount, string fullWord) AcceptSuggestion(string suggestion);

// 변경 후
(int backspaceCount, string fullWord, int prefixLength) AcceptSuggestion(string suggestion);
```

- `prefixLength`: 현재 조합(old)과 fullWord의 공통 접두사 길이
- Unicode 모드 호출부: `SendAtomicReplace(bs, fullWord[prefixLength..], prefixLength + suffix.Length)`
- VirtualKey 모드 호출부: 기존 그대로 (`VK_BACK × bs` + `SendUnicode(fullWord)`, prefixLength 무시)

## 4. 변경 파일 목록

| 파일 | 변경 |
|---|---|
| `AltKey/Services/InputLanguage/KoreanInputModule.cs` | HandleKey/HandleBackspace/AcceptSuggestion suffix 적용, `CommonPrefixLength` 헬퍼 추가 |
| `AltKey/Services/InputService.cs` | `SendAtomicReplace` 3-인자 오버로드 추가 (2-인자는 위임) |
| `AltKey/Services/InputLanguage/IInputLanguageModule.cs` | AcceptSuggestion 시그니처 + 문서 주석 |
| `AltKey/Services/AutoCompleteService.cs` | 3-튜플 패스스루 (학습 로직은 fullWord 사용 그대로) |
| `AltKey/ViewModels/SuggestionBarViewModel.cs` | Unicode 분기에서 `fullWord[prefixLength..]` 전송 |
| `AltKey.Tests/InputLanguage/TestHelpers.cs` | FakeInputService 3-인자 오버로드 (추적 길이 = fullNewLength) |
| `AltKey.Tests/InputLanguage/KoreanInputModuleSuggestionTests.cs` | 4개 디스트럭처 갱신 + prefixLength 어설션 |
| `AltKey.Tests/InputLanguage/KoreanInputModuleQuietEnglishTests.cs` | QuietEnglish AcceptSuggestion 디스트럭처 갱신 |
| `AltKey.Tests/KeyboardViewModelTests.cs` | 가짜 IInputLanguageModule 구현 갱신 |
| `AltKey.Tests/SwitchScanSuggestionTests.cs` | 가짜 IInputLanguageModule 구현 갱신 |
| `AltKey.Tests/InputLanguage/KoreanInputModuleHangulTests.cs` | suffix 어설션 신규 테스트 |
| `AltKey.Tests/InputLanguage/KoreanInputModuleBackspaceTests.cs` | suffix 어설션 신규 테스트 |

## 5. 변경하지 않는 것 (보호 영역)

- `InputService.SendAtomicReplace` — BS 배치 + Unicode 배치 + settle 구조 그대로 (Hard Freeze)
- `TrackedOnScreenLength` 의미론 — 최종 화면 텍스트 길이. 수락 후 `ResetTrackedLength()` 흐름(TASK-01) 그대로
- `ReleaseTransientModifiers` 시점 — SendAtomicReplace 말미, 불변
- bigram 학습/조회 — `fullWord` 기준이라 suffix와 무관
- QuietEnglish 영어 추가 경로 (`SendUnicode`) — 이미 append-only

## 6. 가정과 리스크

| # | 가정 | 위반 시 영향 | 대응 |
|---|---|---|---|
| A1 | Unicode 모드에서 `_composer.Current` == 화면 텍스트 | 접두사 계산이 틀려 오타 발생 | 수동 회귀 체크리스트(§8)로 검증. 문제가 확인되면 `TrackedOnScreenLength`와 길이 불일치 시 기존 전체 교체 방식으로 폴백하는 가드 추가 |
| A2 | 조합 알고리즘에서 Feed/Backspace가 앞 음절을 바꾸지 않음 (CORE-LOGIC-PROTECTION §0) | — | 알고리즘이 frozenset 상태. 변경 불가 |
| A3 | `SendAtomicReplace(0, "")` 호출은 무해 (BS 0 + Unicode 0 → 이벤트 없음) | — | InputService가 빈 배열을 그대로 전송하므로 무해 |

**롤백**: 변경이 단일 커밋에 묶이면 revert로 완전 복귀 가능. 데이터/설정 스키마 변경 없음.

## 7. 테스트 매트릭스

### 7.1 기존 테스트 갱신
- `AcceptSuggestion_in_HangulJamo_Unicode_returns_onscreen_char_count`: `(1, "해달")` → `(0, "해달", 1)` (bs = oldLen - prefix)
- `AcceptSuggestion_in_HangulJamo_VirtualKey_returns_jamo_depth`: `(2, "해달")` → `(2, "해달", 0)` (VK 모드 bs 불변, prefix=0)
- `AcceptSuggestion_then_new_jamo_preserves_accepted_word_on_screen`, `AcceptSuggestion_then_type_new_word_with_realistic_tracked_length`: suffix 전송 반영
- `AcceptSuggestion_in_QuietEnglish_returns_correct_bsCount`: `(5, "help")` → `(2, "help", 3)` ("hello"/"help" 공통 접두사 "hel")

### 7.2 신규 테스트
- **자모 탭**: "안녕" 입력 시 AtomicReplaces가 `[(0,"ㅇ"), (1,"아"), (1,"안"), (0,"ㄴ"), (1,"녀"), (1,"녕")]` — BS 총합 4회 (기존: 15회)
- **백스페이스**: "한" → BS 탭 후 `(1, "하")` (음절이 달라져 접두사 없음 — 기존 구현도 `(1, "하")`이라 비차별 케이스). 차별화 케이스: "안녕" → "안녀" (종성 제거, 접두사 보존. 기존 `(2, "안녀")` vs 신규 `(1, "녀")`)
- **수락**: "안녕" 상태에서 "하세요" 수락 → `(4, "하세요", 0)` — old="안녕", new="하세요" 접두사 없음

## 8. 수동 회귀 체크리스트 (DEBUG 빌드)

CORE-LOGIC-PROTECTION §5의 4개 시나리오 + suffix 전용 추가:

1. `ㅎㅏ` → `ㅅ`(Shift) 탭 → "핫"이 아니라 **"했"**
2. "화사" 입력 후 백스페이스 3회 → **빈 필드** (잔여 자모 없음)
3. 자동완성 ON, "해" 입력 후 제안 "해달" 클릭 → 화면에 **"해달"**만 남고, 곧바로 `ㅎㅏ` 탭 시 **"해달하"**
4. QuietEnglish: "hello" 입력 후 "help" 수락 → 화면에 **"help"**만 (기존 5BS+전체 vs 신규 3BS+"lp")
5. **신규**: 긴 단어("접근성을 위한 가상 키보드를 만들고 있습니다") 입력 시 딜레이 체감 — 마지막 자모 탭까지 BS가 거의 없음
6. **신규**: 제안 수락 직후 다음 자모 탭 — 수락된 단어가 지워지지 않음 (TASK-01 회귀)

## 9. 롤아웃

- **DEBUG 빌드만** 적용 → 사용자 직접 수동 테스트
- 커밋은 사용자 승인 후에만
- Release/스토어 배포 영향 없음 (런타임 로직만, 설정 스키마 불변)
