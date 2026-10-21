# 05 — AutoCompleteService 통합 + fallback

## 목표
기존 자동완성 후보가 생성된 **이후**에 AI 재평가를 끼워 넣고, AI 실패 시 기존 후보로 즉시 fallback한다. `KoreanDictionary`/`EnglishDictionary` 내부 코드는 건드리지 않는다.

## 배경 (레포 기준)
- 후보 흐름: `KoreanInputModule`(또는 module)의 `SuggestionsChanged` → `AutoCompleteService.SuggestionsChanged` → `SuggestionBarViewModel.OnSuggestionsChanged`.
- `AutoCompleteService`가 module의 `SuggestionsChanged`를 구독해 재방출한다(생성자 line 26). **여기가 AI 끼워 넣기 지점.**
- `KoreanDictionary.GetSuggestions(prefix, prevWord, count)`가 baseList + prevWord 문맥을 만든다. AI 입력에 쓸 `prevWord`(문맥)는 module의 `_suggestionContext`/`_lastCommittedWord`에 있다.
- `SuggestionMode`(Normal/Bigram)가 있다. AI 후보 표시를 위해 **새 enum 값**(예: `AiReranked`)을 추가할지, 아니면 Normal로 둔 채 순서만 바꾸는지를 결정한다(아래 "결정").

## 결정 (1차 구현)
- **AI 후보 표시 방식**: 1차는 `AiPriority`(앞에 배치)만 사용 — `SuggestionMode`에 새 값을 추가하지 않고 **Normal 모도의 순서만 바꾼다**. 정렬 방식 선택(`AiBoost`)은 v2. (별도 ★표시 UI는 향후.)
- AI off(`AiAutoCompleteEnabled=false`)면 이 통합 코드는 **무조건 no-op** → 기존 동작과 byte-identical.
- **AI 입력 문맥**: 1차는 `AutoCompleteService._lastCommittedWord`(기존 자체 필드, 직전 1단어)를 prevWord로 사용 — 인터페이스 변경 불필요. 다단어(3~5) 이력은 향후 `AutoCompleteService` 내부에 N개 이력을 추가해 확장(인터페이스가 아니라 서비스 자체에서). 토큰 한도 초과 시 앞쪽부터 버림.

## 작업
1. `LayaReranker`(3번) + `ModelStore`(2번) + `ConfigService`를 `AutoCompleteService`에 주입(생성자 4개 → 7개). `App.xaml.cs` DI 등록(104~150행)에 신규 싱글턴을 추가하고, `AltKey.Tests`의 관련 모킹도 함께 갱신.
2. `AutoCompleteService.OnModuleSuggestions(IReadOnlyList<string> baseList, SuggestionMode mode)` (module 이벤트 핸들러)에:
   - `AiAutoCompleteEnabled`가 false면 → 그대로 방출하고 return.
   - true면:
     - 문맥(`prevWord`) + baseList를 `LayaReranker.RerankAsync(..., topN: AiSuggestionCount)`로 보낸다.
     - 성공 → `ConfigService.Current.AiSortMode`에 따라:
       - `AiPriority` → `CandidateReorder.Reorder(baseList, result.Selected, topN)`(4번)
       - `AiBoost` → `CandidateReorder.Boost(baseList, result.Selected, topN, boostValue)`(4번)
     - 실패/null/타임아웃 → **baseList 원본** 방출(fallback).
   - async 처리: UI 스레드에서 블로킹하지 않도록 `async void` 핸들러 + 본문 try/catch(fallback) + `Dispatcher.Invoke`(기존 `SuggestionBarViewModel` 패턴 참고).
   - **stale 결과 폐기 (latest-wins)**: 요청마다 sequence/generation 토큰을 부여하고, 응답이 도착할 때 "가장 최근 요청"이 아니면 결과를 버린다. 빠른 타이핑에서 이전 prefix의 늦은 결과가 새 후보를 덮쓰는 것을 방지(캐시/타임아웃만으로는 불가).
3. **모델 로드/언로드 타이밍**:
   - **다운로드**는 06번(설정 UI)에서 `AiAutoCompleteEnabled` on 시점에 트리거한다. 05번은 다운로드를 하지 않는다.
   - **메모리 로드**(InferenceSession 생성)는 제안 바 활성화(`AutoCompleteEnabled` → true) 시점에 수행. `SuggestionBarViewModel.SetVisibleFromConfig()`이 `AutoCompleteEnabled` 변경을 감지할 때, AI on이면 `LayaReranker.EnsureLoaded()`를 호출한다. **모델 파일이 아직 없으면(다운로드 중/실패) 로드를 보류**해 `IsLoaded=false` 유지 → 기존 후보 fallback. `ModelStore`의 다운로드 완료 신호와 연계해 완료 후 로드.
   - **메모리 언로드**는 제안 바 비활성화(`AutoCompleteEnabled` → false) 시점에 **무조건** 수행. `LayaReranker.Unload()` 호출 → InferenceSession 해제, 메모리 회수.
   - 앱 시작 시 즉시 로드는 안 한다(AI off 사용자에게 영향 주지 않기 위함).

## 하지 말 것
- `KoreanDictionary`/`EnglishDictionary`에 AI 코드를 넣지 않는다(기획 11번: 별도 서비스로 분리).
- AI off 상태에서 어떤 경로도 기존 후보를 바꾸게 하지 않는다.
- UI 스레드에서 ONNX를 동기 실행하지 않는다.

## 검증
- `AltKey.Tests`(fake LayaReranker 주입):
  - AI on + 성공 → 재배치된 목록 방출.
  - AI on + 실패(null) → baseList 원본 방출.
  - AI off → baseList 원본, LayaReranker **호출 안 됨**(spy로 확인).
  - 제안 바 활성화 시 `LayaReranker.EnsureLoaded()` 호출(spy). 비활성화 시 추가 호출 없음.

## 산출물
- 수정된 `AutoCompleteService.cs`, `App.xaml.cs` DI 등록, 테스트.
