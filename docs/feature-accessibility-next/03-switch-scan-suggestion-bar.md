# 작업 03: 자동완성 제안 바 스캔 가능화

## 목표

스위치 제어 사용자가 자동완성 제안과 현재 조합 단어 슬롯을 스캔으로 선택할 수 있게 한다. 이 작업은 AltKey 접근성에서 가장 큰 사용자 가치를 낸다.

## 작업 난이도

높음. 자동완성 내부 로직은 건드리지 않고, 제안 바 명령을 스캔 대상에 안전하게 노출해야 한다.

## 변경 범위

- `AltKey/ViewModels/SuggestionBarViewModel.cs`
- `AltKey/Views/SuggestionBar.xaml`
- `AltKey/ViewModels/KeyboardViewModel.cs`
- 필요 시 새 모델: `AltKey/ViewModels/ScanTargetVm.cs`
- `AltKey/Views/KeyboardView.xaml`
- 테스트: 새 파일 `AltKey.Tests/SwitchScanSuggestionTests.cs` 권장

## 반드시 먼저 확인

`docs/auto-complet/CORE-LOGIC-PROTECTION.md` §2 "절대 건드리지 말 것"을 읽는다.

## 금지 범위

- `KoreanInputModule.AcceptSuggestion`의 BS 계산 수정 금지
- `SuggestionBarViewModel.AcceptSuggestion`의 `SendAtomicReplace` 후 `ResetTrackedLength()` 흐름 제거 금지
- `AutoCompleteService`에 스캔 전용 분기 추가 금지
- 스캔 선택 시 `InputService.Send*`를 직접 호출하지 말고 기존 제안 명령을 호출

## 설계 지시

### 1. 스캔 대상 추상화

키보드 키와 제안 바 항목을 같은 방식으로 다룰 수 있게 작은 스캔 대상 모델을 만든다.

권장 형태:

- 표시 이름: `AccessibleName`
- 실행 동작: `Action Activate`
- 하이라이트 상태: `IsScanFocused`
- 대상 종류: `KeyboardKey`, `CurrentWord`, `Suggestion`, `HeaderButton` 등

소형 모델이 어렵다면 1차 구현에서는 키보드 대상 목록과 제안 대상 목록을 따로 관리해도 된다. 단, 최종적으로는 하나의 순서 목록으로 스캔되어야 한다.

### 2. SuggestionBarViewModel에 스캔용 대상 노출

`SuggestionBarViewModel`이 현재 표시 중인 항목을 읽기 전용 목록으로 제공한다.

포함 대상:

- 현재 조합 중인 단어 슬롯: 실행 시 `CommitCurrentWordCommand`
- 각 제안 단어: 실행 시 `AcceptSuggestionCommand`

삭제 메뉴는 1차 스캔 대상에 넣지 않는다. 삭제는 실수 위험이 있으므로 별도 고급 작업으로 분리한다.

### 3. SuggestionBar 하이라이트 표시

스캔 포커스를 받은 제안 버튼이 시각적으로 분명해야 한다.

조건:

- 라이트/다크/고대비에서 보인다.
- 키보드 본체의 `IsA11yFocused`와 비슷한 강조를 사용한다.
- 텍스트가 잘리지 않도록 기존 `TextTrimming`은 유지한다.

### 4. 스캔 순서 옵션

`AppConfig`에 `SwitchScanIncludeSuggestions`를 추가한다. 기본값은 true를 권장한다.

가능하면 `SwitchScanSuggestionPriority`도 추가한다.

- `BeforeKeyboard`: 제안 바를 먼저 훑기
- `AfterKeyboard`: 키보드 뒤에 훑기

기본값은 `BeforeKeyboard`를 권장한다. 자동완성은 입력 횟수를 줄이는 핵심 기능이기 때문이다.

### 5. 현재 단어 저장/취소 정책

스캔 1차 범위에는 "현재 단어 저장"만 넣는다. "현재 단어 취소"는 실수로 조합을 잃을 위험이 있으므로 별도 취소 키나 고급 메뉴로 분리한다.

## 수용 기준

- 자동완성 제안이 표시되면 스캔 하이라이트가 제안 단어에도 이동한다.
- 제안 단어를 선택하면 기존 마우스 클릭과 같은 방식으로 수락된다.
- 현재 조합 단어 슬롯을 선택하면 기존 저장 동작과 같다.
- 자동완성 OFF이면 제안 바 스캔 대상이 생기지 않는다.
- 한글 조합 중 제안 수락 후 다음 자모 입력 시 기존 단어가 지워지는 회귀가 없어야 한다.

## 테스트 지시

- `SuggestionBarViewModel`이 제안 목록 변경 시 스캔 대상 수를 갱신하는지 테스트
- 현재 단어가 있을 때 current word 대상이 생기는지 테스트
- 제안 선택이 `AcceptSuggestionCommand` 경로를 타는지 테스트
- 자동완성 OFF 또는 제안 없음에서 대상 목록이 비는지 테스트

## 수동 검증

1. 자동완성 ON
2. 한글 자모를 입력해 제안을 표시
3. 스위치 스캔으로 제안 단어를 선택
4. 선택 직후 다음 자모를 입력해 이전 단어가 삭제되지 않는지 확인
5. `가/A` QuietEnglish 모드에서도 영어 제안 선택이 가능한지 확인

