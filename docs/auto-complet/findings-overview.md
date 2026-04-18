# 2026-04-18 한국어 자동완성 분석 — 발견 목록 (전체 해결 완료)

> 한 번의 전수 조사(AutoCompleteService, HangulComposer, KoreanInputModule, InputService, 두 사전, 스토어, 관련 VM)에서 드러난 버그·개선점·품질 이슈 요약.
>
> **상태**: 이 문서에 나열된 F-1 ~ F-8은 **2026-04-18에 모두 해결 완료**되었다. 아래 표는 완료 이력 보존 목적이며, AI 에이전트가 새로운 작업 계획 단계에서 이 목록을 기본으로 순회할 필요는 없다.

---

## 완료 이력

| ID | 제목 | 심각도 | 해결 커밋 | 작업지시서 |
|---|---|---|---|---|
| F-1 | 제안 수락 직후 이어쓰기 시 제안 텍스트 전체 삭제 | 높음 | cfc87a9 | [TASK-01](TASK-01-accept-tracked-length-reset.md) |
| F-2 | `InputService.IsImeKorean()` + IMM32 P/Invoke 호출자 없음 (dead code) | 낮음 | 145d621 | [TASK-02](TASK-02-dead-code-imm-ime.md) |
| F-3 | 단일 자모/1글자 한글이 사용자 사전에 저장되어 제안 품질 저하 | 중간 | b222690 | [TASK-03](TASK-03-dictionary-quality.md) |
| F-4 | 초성만 조합 중일 때 제안이 거의 안 뜸 (내장 사전이 완성 음절로 구성) | 중간 | 7ba5009 | [TASK-04](TASK-04-composer-prefix-fallback.md) |
| F-5 | `WordFrequencyStore.Save()`가 매 단어마다 파일 I/O + 실패 시 조용히 삼킴 | 중간 | 0aba873 | [TASK-05](TASK-05-save-performance.md) |
| F-6 | `WordFrequencyStore.PruneLowest()` 동점 경계에서 대량 삭제 가능 | 낮음 | 6594c99 | [TASK-06](TASK-06-prune-lowest-safety.md) |
| F-7 | 회귀 방지용 테스트 공백 (Accept 후 재입력, 토글 중 학습, BS 후 composer/화면 싱크 등) | 중간 | 602afa1 | [TASK-07](TASK-07-test-coverage-gaps.md) |
| F-8 | `HangulComposer`에 호출되지 않는 private 메서드(`IsHangulSyllable`, `Decompose`) | 낮음 | de40720 | [TASK-08](TASK-08-composer-dead-code.md) |

---

## 각 항목 한 줄 설명 (원본 분석 보존)

### F-1 — 제안 수락 후 `TrackedOnScreenLength` 누수
`SuggestionBarViewModel.AcceptSuggestion`이 `TrackedOnScreenLength = fullWord.Length`로 설정한 뒤, 모듈은 다음 자모 입력 때 이 값을 `prevLen`으로 받아 **제안 전체를 BS로 지워버림**. 한국어처럼 단어 뒤에 조사·어미가 바로 붙는 언어에서 상시 발생한다. 영어 QuietEnglish에서도 동일. → **해결**: `SuggestionBarViewModel.AcceptSuggestion`이 `SendAtomicReplace` 직후 `ResetTrackedLength()`를 호출.

### F-2 — IMM32 API dead code
`InputService.IsImeKorean()`은 `ime-korean-detection-problem.md §6.5`에서 Unicode 우회를 채택한 뒤로 호출자가 전부 제거되었다. → **해결**: 메서드와 연관 P/Invoke 모두 삭제.

### F-3 — 단일 자모 학습 오염
`KoreanInputModule.FinalizeComposition`은 `_composer.Current.Length > 0`만 보고 `_koDict.RecordWord`를 호출했음. → **해결**: `KoreanDictionary.RecordWord`에서 완성 한글 음절(U+AC00~U+D7A3) 2개 미만이면 저장 skip.

### F-4 — 초성 단독 prefix 매칭 공백
내장 `ko-words.txt`는 완성 음절 단어("가족", "간다"...)로 채워져 있는데, 조합 중 `ㄱ`(초성만, U+3131 자모)은 `"가족".StartsWith("ㄱ")`이 false. → **해결**: 내장 사전을 초성별 리스트로 사전 인덱싱하고, 호환 자모 1글자 prefix일 때 `GetSuggestionsByChoseong` 경로로 분기.

### F-5 — 저장 I/O 빈도 + 에러 삼킴
`WordFrequencyStore.RecordWord` → 매번 `Save()` → 매번 전체 Dictionary JSON 직렬화 + 파일 쓰기. → **해결**: 1초 디바운스 타이머 + 앱 종료 시 `Flush()` + `tmp + File.Move`로 원자적 쓰기 + `LastSaveError` 노출 + `Debug.WriteLine` 로깅.

### F-6 — `PruneLowest` 동점 대량 삭제
`threshold = 빈도 하위 20% 경계치`로 잡고 `_freq[k] <= threshold`인 항목 전부 제거. → **해결**: `OrderBy(value).ThenBy(key).Take(count/5)`로 정확히 N개만 제거.

### F-7 — 테스트 공백
Accept 후 즉시 재입력, ToggleSubmode 중 학습, BS 분기, OnSeparator 학습 조건 등 회귀 방지 테스트 부재. → **해결**: `KoreanInputModuleTests`에 G-1~G-6 계열 테스트 추가, `WordFrequencyStoreTests`·`KoreanDictionaryTests` 신설.

### F-8 — HangulComposer dead code
`private static bool IsHangulSyllable(char)`와 `private static (int,int,int) Decompose(char)`가 선언되어 있지만 호출자 없음. → **해결**: 두 메서드 삭제.

---

## 관찰만 한 사항 (당장 작업 대상 아님)

- **동시성**: 현재 키 이벤트는 WPF Dispatcher 하나에서만 처리되므로 `HangulComposer`/`WordFrequencyStore` 모두 안전. 향후 백그라운드 학습·자동 저장 타이머 도입 시 락 필요. (F-5의 디바운스 저장에서는 `_saveLock` 도입으로 일부 대응됨.)
- **Bigram/문맥 제안**: 이전 단어 고려한 제안은 현재 없음. 설계 추가 작업으로 분류.
- **`KeyboardViewModel`의 `UpdateModifierState`에서 `IsSeparatorKey`가 구두점을 포함하지 않는다는 사실**: 구두점은 모듈 내부에서 `IsSeparator`로 처리됨. 현재 동작은 정상.

---

## 당시 회귀 없이 안전하게 고칠 수 있는 순서 제안 (이력 보존)

1. **F-8 → F-2** (dead code 두 건) — 동작 변화 없음, 가장 낮은 리스크.
2. **F-7** (테스트 보강) — 구현 변경 없이 현 코드의 계약을 고정.
3. **F-3** (1글자 학습 차단) — 조건 한 줄, 테스트로 고정 가능.
4. **F-5** (저장 I/O + 에러 로깅) — 배치 저장 or 디바운스로 독립 개선.
5. **F-6** (PruneLowest) — 경계 가드 한 줄.
6. **F-1** (Accept 후 TrackedOnScreenLength) — UX상 핵심 버그지만 F-7 테스트가 있어야 안전히 고친다.
7. **F-4** (prefix fallback) — 품질 개선, 실험적. 마지막에.

실제 커밋도 이 순서에 가깝게 진행되었다.
