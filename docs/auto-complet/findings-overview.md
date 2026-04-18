# 2026-04-18 한국어 자동완성 분석 — 발견 목록 요약

> 한 번의 전수 조사(AutoCompleteService, HangulComposer, KoreanInputModule, InputService, 두 사전, 스토어, 관련 VM)에서 드러난 버그·개선점·품질 이슈 요약. 상세 수정 지침은 각 `TASK-XX-*.md` 파일에 분리되어 있다.

---

## 우선순위 행렬

| ID | 제목 | 심각도 | 재현성 | 작업지시서 |
|---|---|---|---|---|
| F-1 | 제안 수락 직후 이어쓰기 시 제안 텍스트 전체 삭제 | **높음** | 높음 (Unicode 모드, 한국어 조사·어미 입력 시마다) | [TASK-01](TASK-01-accept-tracked-length-reset.md) |
| F-2 | `InputService.IsImeKorean()` + IMM32 P/Invoke 호출자 없음 (dead code) | 낮음 | 항상 | [TASK-02](TASK-02-dead-code-imm-ime.md) |
| F-3 | 단일 자모/1글자 한글이 사용자 사전에 저장되어 제안 품질 저하 | 중간 | 높음 | [TASK-03](TASK-03-dictionary-quality.md) |
| F-4 | 초성만 조합 중일 때 제안이 거의 안 뜸 (내장 사전이 완성 음절로 구성) | 중간 | 높음 | [TASK-04](TASK-04-composer-prefix-fallback.md) |
| F-5 | `WordFrequencyStore.Save()`가 매 단어마다 파일 I/O + 실패 시 조용히 삼킴 | 중간 | 항상 | [TASK-05](TASK-05-save-performance.md) |
| F-6 | `WordFrequencyStore.PruneLowest()` 동점 경계에서 대량 삭제 가능 | 낮음 | 낮음 (5000개 초과 시) | [TASK-06](TASK-06-prune-lowest-safety.md) |
| F-7 | 회귀 방지용 테스트 공백 (Accept 후 재입력, 토글 중 학습, BS 후 composer/화면 싱크 등) | 중간 | - | [TASK-07](TASK-07-test-coverage-gaps.md) |
| F-8 | `HangulComposer`에 호출되지 않는 private 메서드(`IsHangulSyllable`, `Decompose`) | 낮음 | 항상 | [TASK-08](TASK-08-composer-dead-code.md) |

---

## 각 항목 한 줄 설명

### F-1 — 제안 수락 후 `TrackedOnScreenLength` 누수
`SuggestionBarViewModel.AcceptSuggestion`이 `TrackedOnScreenLength = fullWord.Length`로 설정한 뒤, 모듈은 다음 자모 입력 때 이 값을 `prevLen`으로 받아 **제안 전체를 BS로 지워버림**. 한국어처럼 단어 뒤에 조사·어미가 바로 붙는 언어에서 상시 발생한다. 영어 QuietEnglish에서도 동일.

### F-2 — IMM32 API dead code
`InputService.IsImeKorean()`은 `ime-korean-detection-problem.md §6.5`에서 Unicode 우회를 채택한 뒤로 호출자가 전부 제거되었다. 전역 검색 결과 AltKey 내부에서 이 메서드를 부르는 코드 0건. `Platform/Win32.cs`의 `GetGUIThreadInfo`, `ImmGetDefaultIMEWnd`, `AttachThreadInput` P/Invoke도 동반 dead code 후보.

### F-3 — 단일 자모 학습 오염
`KoreanInputModule.FinalizeComposition`은 `_composer.Current.Length > 0`만 보고 `_koDict.RecordWord`를 호출한다. 사용자가 실수로 "ㄱ"만 치고 Space를 누르면 `"ㄱ"`이 사전에 저장되어 이후 `ㄱ` 입력 시 제안 1순위로 올라온다. 최소 완성 음절 1개(유니코드 AC00~D7A3) 이상이어야 학습하도록 강화 필요.

### F-4 — 초성 단독 prefix 매칭 공백
내장 `ko-words.txt`는 완성 음절 단어("가족", "간다"...)로 채워져 있는데, 조합 중 `ㄱ`(초성만, U+3131 자모)은 `"가족".StartsWith("ㄱ")`이 false다. 즉 초성 1개만 쳤을 때는 **사용자 학습에 1글자 단어가 없는 한** 제안이 거의 비어 있다. 초성+중성이 되어야 첫 제안이 뜬다. 초성만 쳤을 때의 fallback 검색(초성 분해 매칭 또는 `HangulComposer.ComposeCurrentSyllable`의 임시 음절로 prefix) 필요.

### F-5 — 저장 I/O 빈도 + 에러 삼킴
`WordFrequencyStore.RecordWord` → 매번 `Save()` → 매번 전체 Dictionary JSON 직렬화 + 파일 쓰기. 공백 한 번에 수~수십 ms 지연 가능. 또한 `Save()`의 `catch { /* 무시 */ }`가 디스크 풀·권한 오류를 조용히 흡수해서 사용자 학습 유실 원인 파악 불가.

### F-6 — `PruneLowest` 동점 대량 삭제
`threshold = 빈도 하위 20% 경계치`로 잡고 `_freq[k] <= threshold`인 항목 전부 제거. 빈도 1인 단어가 전체의 80%를 차지하는 초기 상태에서 threshold=1이 되면 **거의 전부 삭제**될 수 있다. 5000개 상한이 높아 실전에서 당장 터질 위험은 낮지만, 경계 테스트 필요.

### F-7 — 테스트 공백
- Accept 후 즉시 자모/알파벳 입력 회귀 없음.
- ToggleSubmode 전에 조합된 단어의 학습 여부 검증 없음.
- VirtualKey 모드의 AcceptSuggestion 경로 테스트 없음.
- `SendAtomicReplace`가 Shift sticky를 해제하는지 검증 없음(내부 로직은 있으나 회귀 방지 테스트 미존재).

### F-8 — HangulComposer dead code
`private static bool IsHangulSyllable(char)`와 `private static (int,int,int) Decompose(char)`가 선언되어 있지만 호출자 없음. 이전 설계 잔재로 보이며 유지비 없이 삭제 가능.

---

## 관찰만 한 사항 (당장 작업 대상 아님)

- **동시성**: 현재 키 이벤트는 WPF Dispatcher 하나에서만 처리되므로 `HangulComposer`/`WordFrequencyStore` 모두 안전. 향후 백그라운드 학습·자동 저장 타이머 도입 시 락 필요.
- **Bigram/문맥 제안**: 이전 단어 고려한 제안은 현재 없음. 설계 추가 작업으로 분류. 이번 문서에서는 작업지시서 미포함.
- **Accept 후 Unicode 모드의 TrackedOnScreenLength 중복 설정**: `SendAtomicReplace`가 이미 설정하는데 `SuggestionBarViewModel`이 한 번 더 함. 정합성 정리 여지는 있으나 F-1 해결 과정에서 같이 정리될 가능성 높음.
- **`KeyboardViewModel`의 `UpdateModifierState`에서 `IsSeparatorKey`가 구두점을 포함하지 않는다는 사실**: 구두점은 모듈 내부에서 `IsSeparator`로 처리됨. 설계상 경계가 뚜렷하지 않지만 현재 동작은 정상.

---

## 회귀 없이 안전하게 고칠 수 있는 순서 제안

1. **F-8 → F-2** (dead code 두 건) — 동작 변화 없음, 가장 낮은 리스크.
2. **F-7** (테스트 보강) — 구현 변경 없이 현 코드의 계약을 고정.
3. **F-3** (1글자 학습 차단) — 조건 한 줄, 테스트로 고정 가능.
4. **F-5** (저장 I/O + 에러 로깅) — 배치 저장 or 디바운스로 독립 개선.
5. **F-6** (PruneLowest) — 경계 가드 한 줄.
6. **F-1** (Accept 후 TrackedOnScreenLength) — UX상 핵심 버그지만 F-7 테스트가 있어야 안전히 고친다.
7. **F-4** (prefix fallback) — 품질 개선, 실험적. 마지막에.

각 단계 종료 시 `HangulComposerTests` + `KoreanInputModuleTests` 전체 녹색 + 수동 4종 재현 시나리오(해+ㅆ, ㄷㅏㄹㄱ, 화사 BS3, 엔터 후 줄바꿈) 확인.
