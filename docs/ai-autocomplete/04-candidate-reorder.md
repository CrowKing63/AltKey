# 04 — 후보 재배치 pure function (순서만 변경)

## 목표
정렬 방식 두 가지(AI 우선 배치, AI 보정)를 **순수 함수**로 분리한다. UI·추론에 독립적으로 테스트 가능하게 만든다.

## 배경 (레포 기준)
- 최종 표시 목록은 `SuggestionBarViewModel.Suggestions`(ObservableCollection<string>)으로 간다.
- 이 단계는 **정렬 로직만**. 모델/설정/UI를 알지 못한다 → pure function.
- 기존 BigramBoost 메커니즘 참고: `KoreanDictionary.GetSuggestions(prefix, prevWord, count)`에서 `BigramBoost=3`을 점수에 더해 재정렬한다. AI 보정도 같은 구조.

## 정렬 방식 정의

> **범위**: 1차는 `AiPriority`(방식 1)만 UI에 연결한다. `AiBoost`(방식 3)은 pure function + 테스트로 구현해 두되, UI 노출은 v2. (00번·plan §5 "가장 단순한 방식" 원칙과 일치.)

### 방식 1: AI 우선 배치 (`AiPriority`)
AI가 선택한 상위 N개를 목록 **앞으로 이동**, 나머지는 원본 순서 유지.

### 방식 3: AI 보정 (`AiBoost`)
원본 점수(빈도·Bigram)에 AI 선택 후보에게 **작은 boost 값**을 추가하고 전체를 점수순 재정렬. BigramBoost와 동일한 메커니즘. AI가 "전체를 지배"하지 않고 "살짝 밀어올림".

## 작업
1. `AltKey/Services/AiRerank/CandidateReorder.cs` 작성:
   - **방식 1**: `static IReadOnlyList<string> Reorder(IReadOnlyList<string> baseList, IReadOnlyList<string> aiSelected, int topN)`:
     1. `aiSelected`를 `topN`으로 절단.
     2. `baseList`에 **없는** 단어는 무시(모델이 새 단어를 만들어낸 경우 방어).
     3. 선택된 단어들을 `baseList`에서 제거하고 **앞에** 추가(선택 순서 유지).
     4. 나머지는 원본 순서 그대로 뒤에 배치.
     - 예: base=`[합니다, 하고, 하는, 하니까, 하지만]`, aiSelected=`[하지만, 합니다]`, topN=3 → `[하지만, 합니다, 하고, 하는, 하니까]`.

   - **방식 3**: `static IReadOnlyList<string> Boost(IReadOnlyList<string> baseList, IReadOnlyList<string> aiSelected, int topN, double boostValue)`:
     1. 각 후보에 원본 점수(위치 기반: `baseList.Count - index`)를 부여.
     2. `aiSelected`에 포함된 후보(전부, topN 제한 없음)에게 `boostValue` 추가.
     3. 점수순 내림차 정렬 → 최종 목록.
     - 예: base=`[합니다, 하고, 하는, 하니까, 하지만]`, aiSelected=`[하지만]`, boost=5
       → 점수: 합니다=4, 하고=3, 하는=2, 하니까=1, 하지만=0+5=5
       → 결과: `[하지만, 합니다, 하고, 하는, 하니까]`
     - **참고**: `boostValue`는 BigramBoost(3)와 같은 규모로 시작. 8번에서 튜닝.

   - **공통 엣지 케이스** (두 방식 모두):
     - `aiSelected`가 비어 있으면 baseList 원본 반환.
     - `aiSelected`에 baseList에 없는 단어가 섞여 있으면 그 단어는 생략.
     - `topN <= 0`(방식 1) 또는 `boostValue <= 0`(방식 3)이면 baseList 원본.

2. `AiSortMode` enum 정의 (`AltKey/Models/AiSortMode.cs`):
   - `AiPriority` (방식 1)
   - `AiBoost` (방식 3)
   - 기본값: `AiPriority`

## 하지 말 것
- 이 함수에서 점수 **생성**(모델 호출, 빈도 조회)을 하지 않는다. 위치 기반 점수만 사용.
- 입력 컬렉션을 변경하지 않는다(새 리스트 반환).
- `boostValue`를 0 이하로 받지 않는다(호출자가 클램핑).

## 검증
- `AltKey.Tests`:
  - **방식 1**: 위 예 + 엣지 케이스(빈 aiSelected, 전부 중복, 일부만 존재, topN 초과) table-driven.
  - **방식 3**: boost로 순서가 바뀌는 경우, boost가 작아도 원본 유지되는 경우, aiSelected에 없는 단어가 boost 안 받는 경우.
  - 두 방식 모두 `baseList` 원본이 변경되지 않는지(immutable) 확인.

## 산출물
- `CandidateReorder.cs`, `AiSortMode.cs`(enum), 테스트. **두 방식의 규칙을 이 파일에 명시.**
