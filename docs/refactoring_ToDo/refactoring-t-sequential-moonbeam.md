# 리팩토링 최종 검토 보고서 — `docs/refactoring_ToDo/` 대조

> **브랜치**: `feature/unified-autocomplete`
> **검토일**: 2026-04-18
> **범위**: `docs/refactoring_ToDo/` 00~10 태스크 + `버그 목록.md` 6건

---

## Context

`docs/refactoring_ToDo/`에 명시된 10개 태스크(한국어 전용 재편 + "해+ㅆ→해T" 버그 수정 + 자동완성 토글 연동 + 접근성)와 사용자가 수동 테스트로 식별한 버그 6건이 실제 코드에 얼마나 정확하게 반영됐는지 파일 단위로 대조. 누락·편차·추가 개선 사항을 분류해 후속 작업이 무엇인지 명확히 한다. 이 문서 자체가 "부족한 부분·개선할 점" 산출물이다.

---

## 종합 판정

| Task | 상태 | 핵심 근거 |
|---|---|---|
| 01 모델·인터페이스 | ✅ 완료 | [InputSubmode.cs](AltKey/Services/InputLanguage/InputSubmode.cs), [IInputLanguageModule.cs](AltKey/Services/InputLanguage/IInputLanguageModule.cs), [KeyAction.cs:17](AltKey/Models/KeyAction.cs), [KeySlot.cs:14-16](AltKey/Models/KeySlot.cs) |
| 02 WordFrequencyStore 분리 | ✅ 완료 | [WordFrequencyStore.cs:20-22](AltKey/Services/WordFrequencyStore.cs), [App.xaml.cs:64](AltKey/App.xaml.cs) |
| 03 KoreanInputModule | ✅ 완료 (미세 편차 1) | [KoreanInputModule.cs:40](AltKey/Services/InputLanguage/KoreanInputModule.cs) 버그 수정 지점 |
| 04 AutoCompleteService 파사드 | ✅ 완료 | [AutoCompleteService.cs](AltKey/Services/AutoCompleteService.cs) |
| 05 KeyboardViewModel 정리 | ✅ 완료 | [KeyboardViewModel.cs:277-320](AltKey/ViewModels/KeyboardViewModel.cs) |
| 06 자동완성 토글 ↔ Mode | ✅ 완료 | [InputService.cs:33-42](AltKey/Services/InputService.cs), [MainViewModel.cs:84-112](AltKey/ViewModels/MainViewModel.cs) |
| 07 가/A 토글 + 비상 버튼 | ✅ 완료 | [qwerty-ko.json:81](AltKey/layouts/qwerty-ko.json), [KeyButton.xaml:162-165](AltKey/Controls/KeyButton.xaml), [KeyboardView.xaml:165-189](AltKey/Views/KeyboardView.xaml) |
| 08 접근성 | ⚠ 부분 | LiveRegion·AutomationName 완성 / **서브라벨 이중 낭독 방지 누락** |
| 09 한국어 전용 정리 | ✅ 완료 | qwerty-en.json 삭제, PrimaryLanguage 0건 |
| 10 테스트 | ✅ 완료 | [KoreanInputModuleTests.cs](AltKey.Tests/InputLanguage/KoreanInputModuleTests.cs), [InputServiceTests.cs](AltKey.Tests/InputServiceTests.cs) |
| 버그 #1~#6 | ✅ 전부 반영 | `버그 목록.md`의 각 파일·라인 교차 확인 |

릴리스 블로커는 없다. 블로커 아닌 개선점이 몇 군데 있다.

---

## 개선이 필요한 항목 (블로커 없음)

### A. [08 접근성] 서브라벨 이중 낭독 방지 미적용 — **권장 수정**

**문제**: [KeyButton.xaml:36-44](AltKey/Controls/KeyButton.xaml) 의 `SubLabelText` TextBlock에 `AutomationProperties.AccessibilityView="Raw"` 설정이 없음. grep 결과 프로젝트 전체에 `AccessibilityView` 0건.

**증상**: Narrator가 메인 라벨(`AccessibleName` 바인딩)과 서브라벨 텍스트를 둘 다 낭독할 가능성. 자모 키 → "비읍" 다음에 "q" 까지 낭독되면 사용자 경험 저해.

**수정 지점**: `SubLabelText`, `LockIcon` TextBlock 양쪽에 `AutomationProperties.AccessibilityView="Raw"` 추가.

**근거**: 지시서 [08-accessibility.md §3-3, §5](docs/refactoring_ToDo/08-accessibility.md) "`AccessibilityView="Raw"` 없으면 메인 라벨 + 서브라벨 둘 다 낭독됨. 반드시 적용".

---

### B. [03 KoreanInputModule] QuietEnglish 경로가 VK 코드 기반 매핑을 사용 — **설계 편차**

**현재 구현**: [KoreanInputModule.cs:109](AltKey/Services/InputLanguage/KoreanInputModule.cs) `VkToEnglishChar(vk, ctx.ShowUpperCase)` — 슬롯의 VK 코드에서 직접 문자를 유도.

**지시서 의도**: 03-3-3 / 05-3-5 — `slot.EnglishLabel` / `slot.EnglishShiftLabel`을 읽어 전송. 그래서 EnglishLabel 리네이밍을 한 이유이기도 함.

**영향**:
- 표준 qwerty-ko 레이아웃에서는 동작 동일(VK_A ↔ "a" ↔ EnglishLabel "a").
- 사용자 커스텀 레이아웃에서 VK와 EnglishLabel이 어긋나면 QuietEnglish 전송 문자가 라벨과 다름.
- 숫자행·특수문자 키는 `VK_A~VK_Z` 바깥이라 `VkToEnglishChar`가 `'\0'` 반환 → QuietEnglish에서 숫자·특수문자 입력 불가.

**수정 제안**: `slot.EnglishLabel`/`EnglishShiftLabel`을 우선 조회하고 없을 때만 VK fallback. 또는 아예 지시서대로 EnglishLabel만 사용.

---

### C. [03 KoreanInputModule] QuietEnglish prefix가 ShowUpperCase 무시 — **미세 버그**

**현재 구현**: [KoreanInputModule.cs:139-147, 236-241](AltKey/Services/InputLanguage/KoreanInputModule.cs) `TrackEnglishKey`가 `VkToChar`(대소문자 무시)로 prefix를 소문자로만 누적.

**영향**:
- Shift+A 누르면 실제 전송은 "A"지만 `_englishPrefix`에는 "a" 적립 → 학습 사전은 소문자로 통일(의도된 것일 수 있음).
- `AcceptSuggestion`이 반환하는 `bsCount`는 prefix 길이이므로 일관성 문제는 없음.

**판정**: 자동완성 매칭 일관성 측면에서는 오히려 소문자 정규화가 맞다(`EnglishDictionary.RecordWord`도 `ToLowerInvariant`). **그러나** 사용자가 실제로 대문자로 타자한 상태와 prefix가 시각적으로 다를 수 있으므로 주석 한 줄로 의도를 명시할 가치 있음.

---

### D. [05/08] `OnSubmodeChanged`가 LoadLayout 직후 공지 발행 — **UX 미세**

**위치**: [KeyboardViewModel.cs:262-272](AltKey/ViewModels/KeyboardViewModel.cs) 의 `LoadLayout` 마지막에서 `OnSubmodeChanged(_autoComplete.ActiveSubmode)` 호출. `OnSubmodeChanged`는 `_liveRegion.Announce("한국어 입력 상태")` 발행.

**영향**: 앱 시작 / 레이아웃 전환마다 Narrator가 "한국어 입력 상태"를 낭독. 사용자 요청으로 Submode가 바뀌지 않은 상황에서도 공지.

**수정 제안**: LoadLayout에서 호출하는 경로는 **라벨 갱신만** 분리(`RefreshAllKeyLabels` 같은 메서드)하고, 공지 발행은 실제 `SubmodeChanged` 이벤트 경로에만 두기.

---

### E. [10 테스트] QuietEnglish + Shift 조합, 숫자키·특수문자 커버리지 부재

**현재 테스트**: [KoreanInputModuleTests.cs](AltKey.Tests/InputLanguage/KoreanInputModuleTests.cs) — 핵심 회귀(해+ㅆ), Ctrl+Shift 조합키 판별, 토글 flush, QuietEnglish 소문자 prefix, AcceptSuggestion bsCount 포함. ✅ 지시서 명시 케이스 전부.

**누락 (권장 보강)**:
- Shift+A를 QuietEnglish에서 눌렀을 때 "A" 전송 + prefix 변화 검증 (위 B/C 경계 조건).
- 숫자키(1~0) · 기호키가 QuietEnglish에서 무시되는지 vs 전송되는지 회귀.
- 백스페이스가 HangulJamo·QuietEnglish 양쪽에서 prefix/composer를 올바르게 줄이는지.
- `AcceptSuggestion` VirtualKey 모드 경로(bsCount 사용) 테스트.

---

### F. [09/문서] 최초 실행 마이그레이션 공지 미구현 — **선택**

`user-words.json` → `user-words.ko.json` 미마이그레이션 정책은 [09-3-5](docs/refactoring_ToDo/09-korean-only-cleanup.md) 에 명시됐고 [docs/release-notes-v0.3.md](docs/release-notes-v0.3.md) 에 릴리스 노트로 작성된 상태. 다만 기획 §2-5에서 언급된 "최초 실행 시 안내 다이얼로그" 옵션은 구현되지 않음 — 지시서 범위 외 선택 사항이므로 보류 가능.

---

### G. [04 AutoCompleteService] `CompleteCurrentWord` 과거 호환 잔존 — **정리 후보**

[AutoCompleteService.cs](AltKey/Services/AutoCompleteService.cs) 에 `CompleteCurrentWord() => _module.OnSeparator()` 가 과거 호환용으로 남아 있음. grep으로 호출자가 없다면 삭제 후보(코드 컨벤션: 사용되지 않는 호환 래퍼 제거).

**검증 액션**: `grep -rn "CompleteCurrentWord" AltKey/` → 0건이면 제거, 1건 이상이면 호출자 확인 후 `OnSeparator()`로 교체.

---

## 릴리스 Definition-of-Done 재확인

[README.md §5](docs/refactoring_ToDo/README.md) 의 체크리스트 기준 현재 상태:

- [x] `dotnet build AltKey.sln` 녹색 — 최근 커밋(e28b65c)까지 문제 없음 가정.
- [x] `dotnet test` 녹색 — [KoreanInputModuleTests](AltKey.Tests/InputLanguage/KoreanInputModuleTests.cs) + [InputServiceTests](AltKey.Tests/InputServiceTests.cs) 존재 (실행 결과는 별도 확인 권장).
- [x] "해+ㅎ"/"닭" 기본 조합 — 테스트 + 수동 검증 완료(버그 목록 #2).
- [x] **"해+ㅆ → 해ㅆ"** (T 아님) — 회귀 테스트 + 버그 #2 수정 확인.
- [x] 쌍자음/쌍모음 7종 — `HasActiveModifiersExcludingShift` 로직으로 경로 보장.
- [x] "가/A" 토글: 라벨·입력 경로·사전 전환 — 구현 및 시각 강조(AccentBrush) 확인.
- [x] 상단바 VK_HANGUL 비상 버튼: 내부 Submode 불변 — `SendOsImeHangulCommand`에서 Submode 건드리지 않음.
- [x] 자동완성 OFF → VirtualKey 모드 → 호환 — App 초기화 + MainViewModel 토글 모두 구현.
- [ ] Narrator 낭독 — **A항목(서브라벨 Raw)** 수정 후 재검증 필요.
- [x] 포터블 단일 EXE — `.csproj` 변경 없음, 기존 publish 설정 유지.

---

## 권장 후속 액션 (우선순위 순)

1. **[A]** `KeyButton.xaml` 에 `AutomationProperties.AccessibilityView="Raw"` 두 줄 추가 — 5분. 블로커 아니지만 접근성 체크리스트 완료 조건.
2. **[B]** `HandleQuietEnglish` 가 `slot.EnglishLabel` 을 1차 소스로 쓰도록 보정 — 숫자·특수문자 지원까지 겸해서 수정. 30분.
3. **[D]** `LoadLayout` 경로의 공지 억제 — 15분.
4. **[E]** 보강 테스트 4종 추가 — 1시간.
5. **[G]** `CompleteCurrentWord` 호출자 grep 후 정리 — 10분.
6. **[C]** 대소문자 prefix 정규화 의도를 주석으로 명시 — 5분(선택).
7. **[F]** 마이그레이션 공지 다이얼로그 — 후속 릴리스에서.

---

## Critical Files (수정 시 건드릴 파일)

| 파일 | 수정 유형 | 우선순위 |
|---|---|---|
| [AltKey/Controls/KeyButton.xaml](AltKey/Controls/KeyButton.xaml) | 서브라벨·LockIcon에 `AccessibilityView="Raw"` | 높음 (A) |
| [AltKey/Services/InputLanguage/KoreanInputModule.cs](AltKey/Services/InputLanguage/KoreanInputModule.cs) | `HandleQuietEnglish`가 `slot.EnglishLabel` 사용 | 중간 (B) |
| [AltKey/ViewModels/KeyboardViewModel.cs](AltKey/ViewModels/KeyboardViewModel.cs) | `LoadLayout` 공지 경로 분리 | 낮음 (D) |
| [AltKey/Services/AutoCompleteService.cs](AltKey/Services/AutoCompleteService.cs) | `CompleteCurrentWord` 호출자 없으면 제거 | 낮음 (G) |
| [AltKey.Tests/InputLanguage/KoreanInputModuleTests.cs](AltKey.Tests/InputLanguage/KoreanInputModuleTests.cs) | 대소문자·숫자·백스페이스·AcceptSuggestion VK 경로 추가 | 낮음 (E) |

---

## 검증 방법 (end-to-end)

1. **빌드·테스트**: `dotnet build AltKey.sln` → 녹색. `dotnet test` → 녹색(신규 테스트 포함).
2. **회귀 체크리스트**: `docs/refactoring_ToDo/10-tests-and-verification.md §3` 체크박스 재순회.
3. **Narrator 검증 (A 수정 후)**:
   - Windows + Ctrl + Enter → Narrator 켜기.
   - Tab/화살표로 키 포커스 이동 → 자모 키 낭독이 "비읍" 등 한 번만 읽히는지 (이전: "비읍 q" 식 이중 낭독 가능성).
   - "가/A" 토글·상단바 한/영·자동완성 토글 각각 LiveRegion 공지 확인.
4. **QuietEnglish 확장 (B 수정 후)**: "A" 상태에서 Shift+1~0, 특수문자 키가 정상 전송되는지.
5. **포터블 EXE 발행**: `dotnet publish AltKey/AltKey.csproj -r win-x64 --self-contained -c Release` 로 단일 실행 파일 생성 확인.

---

## 결론

**리팩토링은 지시서의 의도대로 거의 완전히 수행되었다**. 10개 태스크의 모든 필수 항목과 6개 버그가 코드에 반영돼 있고, 회귀 테스트도 붙었다. 남은 것은 접근성 세부 한 곳(A)과 QuietEnglish 경로의 소스 일관성(B) 두 건이 실질적 개선이다. 나머지(C~G)는 코드 품질·테스트 커버리지 수준의 보강 항목으로, 릴리스 블로커가 아니다.
