# TASK-11: 제안 수락 꼬리 공백 + 구분자 키 환원 (trailing-space-on-accept)

상태: 계획 (2026-11-10)

## 1. 배경 (현재 동작)

| 경로 | 현재 동작 |
|---|---|
| Bigram 모드 수락 (`AcceptBigramSuggestion`) | `" " + 단어` — **선두** 공백 + 단어를 전송 |
| 일반 모드 수락 (`AcceptSuggestion`) | 단어만, 공백 없음 |
| 스페이스 키 | `KeyboardViewModel.IsSeparatorKey`(SPACE 전용) → `_autoComplete.OnSeparator()` → 키 통과 |
| 구두점/Enter/Tab/Esc/Delete | `KoreanInputModule.HandleKey` → `IsSeparator` → `FinalizeComposition` → 키 통과(미소비) |

문제: 단어 수락 후 그 단어를 이어서 쓰려면 사용자가 스페이스를 한 번 더 눌러야 한다.
수락 직후 구두점을 누르면 "단어 ."처럼 공백이 남는다.

## 2. 목표

1. **모든 제안 수락에 꼬리 공백 추가**: `"나는"` 수락 → 화면 `나는 `.
2. **구분자 키 환원(retract)**: 꼬리 공백이 pending인 상태에서 onseparator 키
   (스페이스, 구두점, Enter, Tab, Esc, Delete — `IsSeparator` 집합)를 누르면
   먼저 꼬리 공백을 지우고 그 키를 전송한다.
   - `"말했다 "` + `.` → 화면 `말했다.`
   - `"나는 "` + 스페이스 → 화면 `나는 ` (새 스페이스가 구 것을 대체 — 결과 1개)

## 3. 설계

### 3.1 pending 상태 (`_pendingTrailingSpace`)

`KoreanInputModule`의 필드: "제안 수락으로 보낸 꼬리 공백이 아직 pending이다."
함께 `수락 시점 포그라운드 HWND`를 기록한다(§3.2 안전장치).

| 이벤트 | 상태 전이 |
|---|---|
| `AcceptSuggestion` (일반/bigram, 두 서브모드 모두) | true + 포그라운드 HWND 기록 |
| pending 상태에서 구분자 키 (스페이스/구두점/Enter/Tab/Esc/Delete) | **환원**(꼬리 공백 삭제) → false |
| pending 상태에서 백스페이스 | false (백스페이스가 꼬리 공백을 자연삭제) |
| 그 외 모든 키 (자모, 영문, 기호, 토글) | false (꼬리 공백이 실제 텍스트로 확정) |
| `CancelComposition` / `Reset` / `ToggleSubmode` / `CommitCurrentWord` | false |

### 3.2 환원(retract) 메커니즘

```csharp
private void RetractPendingTrailingSpace()
{
    if (!_pendingTrailingSpace) return;
    _pendingTrailingSpace = false;
    // 포커스가 다른 앱으로 이동했다면 꼬리 공백이 더 이상 그 앱 커서에 없으므로 환원하지 않음.
    if (_input.GetForegroundHwnd() != _pendingTrailingSpaceTarget) return;
    _input.SendAtomicReplace(1, string.Empty, 0); // 백스페이스 ×1, 새 문자 없음
}
```

- **§2 규율 준수**: 모듈 입력은 `SendAtomicReplace`/`SendUnicode`만 사용 —
  `(1, "", 0)` = "백스페이스 1개 + 빈 텍스트"의 단일 원자 배치. `SendKeyPress` 금지.
- **포커스 안전장치**: 수락 후 포그라운드가 바뀌면(다른 앱 클릭 등) 그 앱에서
  의미 없는 백스페이스가 텍스트를 지우는 것을 방지한다. 플래그만 정리.
- **TrackedOnScreenLength 정합성**: 수락 직후 이미 0으로 리셋됨
  (`SuggestionBarViewModel`의 `ResetTrackedLength`)이고, 환원도 0으로 설정 —
  suffix-only rewrite 불변식과 충돌 없음.

### 3.3 훅 포인트

| 위치 | 변경 |
|---|---|
| `KoreanInputModule.HandleKey` 상단 (서브모드 dispatch 이전) | pending && 키가 구분자·백스페이스가 아니면 플래그만 정리 (두 서브모드 모두 커버) |
| `HandleBackspace` | 플래그 정리 (백스페이스가 꼬리 공백을 지움) |
| `HandleKey` separator 분기 | `FinalizeComposition` 전에 `RetractPendingTrailingSpace()` |
| `HandleQuietEnglish` separator 분기 | 동일 |
| `OnSeparator()` (스페이스 경로, ViewModel이 호출) | 동일 |
| `AcceptSuggestion` | `suggestion + " "` 반환 + 플래그 설정 + HWND 기록 |
| `AutoCompleteService.AcceptBigramSuggestion` | `suggestion + " "` 반환 + 플래그 관리 (§3.4 순서) |
| `CancelComposition` / `Reset` / `ToggleSubmode` / `CommitCurrentWord` | 플래그 정리 |

### 3.4 bigram 수락 시 순서가 중요한 이유

`"나는 "` → `"말했다"` 수락 체인에서, 이전 단어의 꼬리 공백은 이미 **실제 텍스트**
(단어 사이 공백)이므로 환원하면 안 된다. `FlushEngineState()`(= `OnSeparator()`)가
구 플래그 pending 상태에서 발동되면 이전 단어 공백이 지워져 `나는말했다`가 된다.

순서:
1. `ClearPendingTrailingSpace()` — 이전 단어 꼬리 공백을 실제 텍스트로 확정
2. `NotifyWordCommitted(suggestion)`
3. `FlushEngineState()` — 플래그 이미 false라 환원 없음
4. `MarkTrailingSpacePending()` — 새 단어 꼬리 공백 플래그 설정

### 3.5 `SuggestionBarViewModel` 변경 불필요

- Unicode 경로: `SendAtomicReplace(bsCount, fullWord.Substring(prefixLength), fullWord.Length)` —
  `fullWord`에 꼬리 공백이 포함되면 그대로 전송됨.
- VirtualKey 경로: `bsCount`회 백스페이스 + `SendUnicode(fullWord)` — 동일.
- Bigram 경로: `SendUnicode(insertion)` — `insertion = 단어 + " "`.

## 3.6 재진입(re-entry) 안전 분석 — 시스템 공백이 onseparator를 트리거하지 않는 보증

과거 선두 공백 구현에서 시스템적으로 보낸 공백이 전역 키보드 훅에 다시 캡처되어
onseparator 경로를 트리거한 부작용이 있었다. 현재 아키텍처는 이 문제를 구조적으로 막는다:

1. **태그**: `InputService`의 모든 SendInput 빌더(`MakeKeyDown`/`MakeKeyUp`/
   `MakeUnicodeKeyDown`/`MakeUnicodeKeyUp`, `InputService.cs:700-724`)가
   `DwExtraInfo = INPUT_EXTRAINFO_ALTKEY`(0xA17A11)를 붙인다.
   - 꼬리 공백(`SendUnicode`), 환원 백스페이스(`SendAtomicReplace` → `MakeKeyDown(VK_BACK)`) 모두 해당 경로 경유.
2. **필터**: 전역 `WH_KEYBOARD_LL` 훅(`AccessibilityNavigationService.HookProc:50-51`)은
   태그가 붙은 이벤트를 그대로 통과시키고 처리하지 않는다.
3. 이 훅은 입력 파이프라인(`KeyboardViewModel.KeyPressed`)으로 키를 넣지 않고
   AltKey 자체 UI 탐색(Tab/Enter/Space)·스위치 스캔 전용이다 — onseparator는
   가상 키보드 버튼 이벤트만 트리거한다.

**회귀 방지**: `InputService`의 VK/Unicode 입력이 항상 `INPUT_EXTRAINFO_ALTKEY`
태그를 붙인다는 단위 테스트 1개를 추가한다 (태그 누락 = 재진입 버그 재생).

## 4. 제약 (CORE-LOGIC-PROTECTION §2)

| 제약 | 준수 |
|---|---|
| 모듈 입력은 `SendAtomicReplace`/`SendUnicode`만 | 환원은 `SendAtomicReplace(1, "", 0)` |
| `IsSeparatorKey`(SPACE 전용) 불변 | 불변 — 스페이스 경로는 계속 ViewModel → `OnSeparator` |
| `SendAtomicReplace` 단일 배치 구조 불변 | 불변 |
| bigram 기록 경로(`RecordWord`) 불변 | 불변 — 공백 없는 단어로 기록 |
| suffix-only rewrite / tracked length 불변식 | 수락 후 tracked=0, 환원도 0 설정 |

## 5. 수정 파일

| 파일 | 변경 |
|---|---|
| `AltKey/Services/InputLanguage/IInputLanguageModule.cs` | `MarkTrailingSpacePending()`, `ClearPendingTrailingSpace()` 추가 |
| `AltKey/Services/InputLanguage/KoreanInputModule.cs` | `_pendingTrailingSpace`(+target HWND), 환원, 훅 포인트, `fullWord + " "` |
| `AltKey/Services/AutoCompleteService.cs` | bigram 반환 `" " + w` → `w + " "`, 플래그 관리 순서 |
| `AltKey/Services/InputService.cs` | `public virtual IntPtr GetForegroundHwnd()` 추가 |
| `AltKey.Tests/InputLanguage/TestHelpers.cs` | `FakeInputService` 오버라이드 (설정 가능한 포그라운드 HWND) |
| `AltKey.Tests/SwitchScanSuggestionTests.cs` | `FakeInputLanguageModule`에 신규 인터페이스 메서드 추가 |
| `AltKey.Tests/InputLanguage/KoreanInputModuleSuggestionTests.cs` | 수락 어설션 갱신 (`"해달"` → `"해달 "` 등) |
| `AltKey.Tests/InputLanguage/AutoCompleteServiceBigramTests.cs` | `" 확인"` → `"확인 "` |
| `AltKey.Tests/InputLanguage/KoreanInputModuleTrailingSpaceTests.cs` | **신규** — 꼬리 공백/환원 테스트 (§6) |

## 6. 테스트 계획 (신규 파일: `KoreanInputModuleTrailingSpaceTests.cs`)

1. `AcceptSuggestion_returns_full_word_with_trailing_space`
2. `Separator_key_after_accept_retracts_trailing_space` — 마침표 키 → `AtomicReplaces`에 `(1, "")` 포함, `HandleKey` false 반환(키 미소비)
3. `OnSeparator_after_accept_retracts_trailing_space` — 스페이스 경로
4. `NonSeparator_key_after_accept_clears_flag` — 자모 키 후 구분자 → 환원 없음
5. `Backspace_after_accept_clears_flag_no_extra_bs`
6. `Retract_skipped_when_foreground_changed`
7. `Bigram_accept_returns_trailing_space_and_sets_flag`
8. `Bigram_accept_after_previous_accept_keeps_previous_space` — 체인에서 의미 없는 BS 없음
9. `CancelComposition_ClearsFlag` / `Reset_ClearsFlag` / `ToggleSubmode_ClearsFlag`
10. `AcceptSuggestion_in_QuietEnglish_separator_retracts`
11. `InputService_vk_and_unicode_inputs_carry_altkey_tag` — 재진입 방지: 모든 SendInput이 `INPUT_EXTRAINFO_ALTKEY` 태그를 붙인다는 회귀 테스트

## 7. 수동 검증 체크리스트 (사용자 실행)

1. 메모장: `나는` 입력 → 수락 → 화면에 `나는 `.
2. 그 상태에서 `.` 누름 → 화면 `나는.` (공백 없음).
3. 수락 → 스페이스 → 화면 `나는 ` 유지(공백 1개), 다음 단어 계속 입력 가능.
4. 수락 → 백스페이스 → 꼬리 공백만 삭제 (이중 삭제 없음).
5. 단어 수락 → 다른 앱 클릭 → 그 앱에서 `.` → 다른 앱에 의미 없는 백스페이스 없음.
6. Bigram 체인: `나는` 수락 → `말했다` 수락 → 화면 `나는 말했다 ` → `.` → `나는 말했다.`
7. 관리자(VirtualKey) 세션: 1–2 동일 플로우 확인.

## 7.5 (유보) 옵션: 온/오프 토글 — 이번 구현에 포함하지 않음

사용자 결정(2026-11-10): 공백 자동 추가의 온/오프 옵션은 **나중에** 한다.
이번 TASK-11 구현에서는 꼬리 공백 + 환원이 항상 켜진 단일 동작으로 넣는다.

나중에 옵션을 추가할 때 참고할 설계 결정(당시 검토 내용 보존):

- **앞뒤 위치 선택(3-state)은 넣지 않는다.** 선두 공백은 문장 중간에서 구조적으로
  이중 공백("안녕 나는" + `말했다` → "안녕  말했다")을 만들고, pending 상태가 없어
  환원(retract) 기능도 불가하다.
- **OFF** = 공백 자동 추가 없음 (기존 일반 모드 동작). 기존 bigram 선두 공백 동작은
  복원하지 않는다 — 이중 공백 버그의 근원이었으므로.
- 구현 방식: `AppConfig` bool 필드(기본 true) + `MainViewModel` 속성
  (기존 `AutoCompleteEnabled` 패턴) + `KeyboardView.xaml` 토글,
  `AcceptSuggestion`/`AcceptBigramSuggestion`에 분기 파라미터.

## 8. 가정 / 오픈 질문

1. **일반 모드 수락에도 꼬리 공백 적용** — 사용자 설명은 bigram 중심이었으나
   "모든 수락"으로 가정했다. bigram만 원하면 `AcceptSuggestion`에서 제외 가능.
2. **Esc/Delete도 환원 대상** — `IsSeparator` 집합 기준. 제외를 원하면 1줄 변경.
3. **스페이스 키 환원 지연**: `SendAtomicReplace`의 25ms settle delay가 스페이스
   presses당 1회 추가(약 25–30ms). 스위치 스캔/시선 입력 속도에서는 무관.
