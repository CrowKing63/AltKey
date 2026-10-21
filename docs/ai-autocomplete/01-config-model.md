# 01 — 설정 모델 확장 (AppConfig + ConfigService)

## 목표
AI 자동완성 전용 설정을 `AppConfig`에 추가하고 `ConfigService`가 정상 직렬화/복원하는지 보장한다.

## 배경 (레포 기준)
- `AltKey/Models/AppConfig.cs`에 기존 AI(텍스트 처리)용 필드가 이미 있다: `AiEnabled`, `AiEndpoint`, `AiModel`, `AiDefaultPrompt`, `AiTimeoutSeconds`. **이것과 혼동하지 말 것** — 이번 기능은 로컬 ONNX 모델 기반이라 별도 필드다.
- `AutoCompleteEnabled`(line 95)가 기존 자동완성 on/off다.
- `ConfigService`는 `AppConfig`를 JSON으로 저장/읽고 `ConfigChanged(propertyName)` 이벤트를 방출한다(`SuggestionBarViewModel.OnConfigChanged`가 이 이벤트에 걸려 있음).

## 작업
1. `AppConfig.cs`에 **AI 자동완성 전용 섹션**을 추가 (기존 AI 텍스트 처리 블록과 분리해 주석으로 구분):
   - `bool AiAutoCompleteEnabled { get; set; } = false;` — AI 후보 사용 on/off
   - `int AiSuggestionCount { get; set; } = 3;` — AI가 앞으로 올리는 후보 수 (1~5 권장, 클램핑)
   - `AiSortMode AiSortMode { get; set; } = AiSortMode.AiPriority;` — 정렬 방식 (04번 enum)
   - (선택) `string AiModelName { get; set; } = "laya-ko";` — 모델 식별자. manifest와 매칭용.
2. `AiSuggestionCount`는 1 미만이면 1로, 과도하게 크면 상한(예: 5)으로 클램핑하는 getter/validator를 둔다.
3. `AiSortMode` enum은 04번에서 정의(`AltKey/Models/AiSortMode.cs`). `AppConfig`가 참조한다.

## 하지 말 것
- 기존 `AiEnabled`(텍스트 처리)를 재사용하지 않는다. 둘은 서로 다른 기능이다.
- `AppConfig`에 ONNX 모델 경로 같은 가변 경로를 저장하지 않는다. 모델 위치는 2번에서 `PathResolver.DataDir` 기반으로 계산한다.

## 검증
- `AltKey.Tests`에 `AppConfig` 직렬화/복원 테스트를 추가: 기본값, `AiAutoCompleteEnabled=true` + `AiSuggestionCount=4` 저장 후 다시 읽어 값이 유지되는지.
- `ConfigService.ConfigChanged`가 `AiAutoCompleteEnabled` 변경 시 방출되는지(기존 `AutoCompleteEnabled`와 동일한 경로인지) 확인.

## 산출물
- 수정된 `AppConfig.cs`, 신규 테스트 파일 1개.
