# AI 자동완성 — 작업 지시서 인덱스

기획서(`ai-autocomplete-plan.md`)를 **단일 세션에서 소화 가능한 단위**로 쪼갠 세부 지시서 모음이다. 각 파일은 독립적으로 작업·검증할 수 있게 작성했으며, 의존 관계만 순서로 묶었다.

## 원칙
- **AI가 단어를 생성하지 않는다.** 기존 `KoreanDictionary`/`EnglishDictionary`가 만든 후보를 AI가 재평가(reranking)해 상위 N개를 앞으로 이동시킬 뿐이다.
- **AI 꺼짐 = 기존 동작 그대로.** AI 기능이 off면 어떤 코드 경로도 기존 자동완성에 영향을 주지 않아야 한다.
- **실패 시 즉시 fallback.** 모델 로드/추론의 어떤 오류든 기존 후보만 표시한다.

## 레포 기준 사실 (작성 시점)
| 항목 | 실제 위치 |
|---|---|
| 후보 생성 | `AltKey/Services/KoreanDictionary.cs` → `GetSuggestions(prefix, prevWord, count)` |
| 후보 중개·UI 갱신 | `AltKey/ViewModels/SuggestionBarViewModel.cs` → `OnSuggestionsChanged(...)` → `Suggestions` (ObservableCollection) |
| 자동완성 총괄 | `AltKey/Services/AutoCompleteService.cs` (module의 `SuggestionsChanged`를 재방출) |
| 표시 모드 enum | `AltKey/Models/SuggestionMode.cs` (`Normal`, `Bigram`) |
| 설정 모델 | `AltKey/Models/AppConfig.cs` (`AutoCompleteEnabled`, `AiEnabled` 등) |
| DI 등록 | `AltKey/App.xaml.cs` 104~150행 (singleton) |
| 데이터 저장 경로 | `AltKey/Services/PathResolver.cs` → `DataDir` |
| 다운로드 인프라 | `AltKey/Services/DownloadService.cs` (진행률 보고 포함) |
| 기존 AI(텍스트 처리) | `AltKey/Services/AiService.cs` — **별개 기능. 이름 충돌 주의** |

## 단위 목록과 순서
순서는 **의존도가 낮은 것부터**. 같은 행에 있는 단위는 병렬로 작업 가능.

| # | 파일 | 제목 | 의존 |
|---|---|---|---|
| 1 | `01-config-model.md` | 설정 모델 확장 (AppConfig + ConfigService) | — |
| 2 | `02-model-store-download.md` | 모델 저장/다운로드 인프라 (manifest, SHA-256) | — |
| 3 | `03-reranker-core.md` | LayaReranker 추론 코어 (ONNX Runtime + 인터페이스) | — |
| 4 | `04-candidate-reorder.md` | 후보 재배치 pure function (순서만 변경) | — |
| 5 | `05-integration-autocomplete-service.md` | AutoCompleteService 통합 + fallback | 1,3,4 |
| 6 | `06-settings-ui.md` | 설정 UI (SettingsWindow + SettingsViewModel + 다운로드 트리거) | 1,2 |
| 7 | `07-cache.md` | 문맥별 AI 결과 메모리 캐시 | 5 |
| 8 | `08-perf-fallback-testing.md` | 성능·타임아웃·에러 처리 + 통합 테스트 | 3,4,5,7 |

## 전제 및 범위 밖(향후) 항목
기획서에 있었지만 **1차 구현에서 제외**한 것:
- "AI 점수순으로 전체 재정렬" 옵션 → 1차는 "AI 후보를 앞에 배치"(`AiPriority`)만.
- `AiBoost`(AI 보정) 정렬 방식 + 설정 라디오 → v2에서 노출.
- `FinalScore = 빈도 + Bigram + AI` 통합 점수 → 향후.
- AltKey 전용 데이터셋 fine-tuning → 향후.

## 공통 주의 (모든 단위에 적용)
1. **이름 충돌**: 기존 `AiService.cs`에 global namespace의 `AiServiceException`, `ChatCompletionRequest`, `ChatMessage`, `ChatCompletionResponse`, `ChatChoice`, `AiJsonOptions`가 있다. 새 코드는 `AltKey.Services.AiRerank` 서브네임스페이스(또는 파일명)로 분리하고, DTO/예외 이름도 재사용하지 않는다.
2. **NuGet**: ONNX Runtime(`Microsoft.ML.OnnxRuntime`)은 아직 레포에 없다. 3번에서 추가한다.
3. **모델 출처 (확인됨)**: `laya-ko`는 `2nugu/laya-ko-decision-onnx`에서 ONNX(`model.onnx` + `model.onnx.data`)로 공개되어 있다(Apache-2.0, CPU ~30ms). 남은 확인 항목은 **토크나이저 처리**(ONNX에 내장 안 됨 → C#에서 토크나이징, 03번)뿐이다.
4. **테스트**: GUI 동작은 단위 테스트 불가(사용자 키보드 부재). 서비스 로직·pure function만 테스트하고, UI는 빌드 후 백그라운드 실행으로 확인한다.
