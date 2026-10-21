# 06 — 설정 UI (SettingsWindow + SettingsViewModel)

## 목표
사용자가 AI 자동완성 on/off와 "AI 후보 수"를 설정 창에서 바꿀 수 있게 한다. 1차 구현은 **최소 구성**만(기획 6번).

## 배경 (레포 기준)
- `AltKey/Views/SettingsWindow.xaml`에 탭이 있다: 외형, 상단바, 동작, 보조기능, **AI 도구**, 고급 설정.
- 기존 "AI 도구" 탭(line 467~)은 **텍스트 처리(AiEnabled)**용이다. 이번 기능은 **별도 탭**으로 분리한다.
- `SettingsViewModel`이 `AppConfig` 속성을 bind한다(`AiEnabled`, `AutoCompleteEnabled` 등 이미 바인딩됨).

## 결정 (1차 구현)
- **새 탭 "AI 자동완성"** 을 추가한다. 기존 "AI 도구" 탭과 분리하여, 텍스트 처리(번역 등)와 AI 자동완성(reranking)이 서로 다른 기능임을 명확히 한다.
- 제공할 컨트롤:
  - `CheckBox` — "AI 자동완성 사용" → `AiAutoCompleteEnabled`
  - **NumericAdjuster** (기존 `Controls/NumericAdjuster.xaml.cs`) — "AI 후보 수" → `AiSuggestionCount` (1~5, +/- 버튼)
  - (v2) **RadioButtons** — "후보 정렬 방식" → `AiSortMode`. 1차는 `AiPriority`만 사용(라디오 미노출).
  - **모델 상태/진행률 표시** — 다운로드 중 진행률(0~100%), 완료 후 "모델 준비됨", 실패 시 오류 문구.
- **다운로드 트리거**: `AiAutoCompleteEnabled`가 false→true로 바뀐 순간 `ModelStore.EnsureModelAsync("laya-ko")`를 호출한다. 모델이 이미 있으면 no-op. 진행률은 `SettingsViewModel`의 Observable 속성으로 탭에 반영.
- **제공하지 않음**(향후): 통합 점수(`FinalScore`) 방식, 모델 선택.

## 작업
1. `SettingsViewModel.cs`:
   - `AppConfig.AiAutoCompleteEnabled`, `AiSuggestionCount`, `AiSortMode`을 바인딩 속성으로 노출(기존 패턴 그대로).
   - `AiSuggestionCount` 변경 시 1~5 클램핑(01번 validator와 일관).
   - **모델 상태 Observable 속성**: `ModelStatus`(Downloading/Ready/Error), `DownloadProgress`(0.0~1.0), `ModelErrorText`.
   - `AiAutoCompleteEnabled`가 false→true로 변경되면 `ModelStore.EnsureModelAsync("laya-ko")`를 async 호출. 진행률은 `IProgress<double>` → `DownloadProgress` 갱신. 완료/실패 시 `ModelStatus` 전환.
2. `SettingsWindow.xaml`:
   - **새 TabItem "AI 자동완성"** 추가. (기존 탭 사이에서 적절한 위치: "보조기능"과 "AI 도구" 사이에, 또는 "AI 도구" 바로 아래.)
   - 탭 내부에:
     - `CheckBox` — "AI 자동완성 사용" → `AiAutoCompleteEnabled`
     - `StackPanel IsEnabled="{Binding AiAutoCompleteEnabled}"` 안에:
       - **NumericAdjuster** — "AI 후보 수" (1~5, +/- 버튼) → `AiSuggestionCount`
       - (v2) **RadioButtons** — "후보 정렬 방식". 1차에는 라디오를 두지 않고 `AiPriority`로 고정.
     - **모델 상태 영역**:
       - `ProgressBar` (Visibility: Downloading) + `TextBlock` 진행률 %
       - `TextBlock` "모델 준비됨" (Visibility: Ready)
       - `TextBlock` 오류 문구 (Visibility: Error) + **재시도 버튼**(Error 시 `EnsureModelAsync` 재호출)
   - 접근성: `AutomationProperties.Name`/`HelpText` 부여(기존 스타일과 동일).
   - 기존 "AI 도구" 탭은 **변경하지 않는다**.

## 하지 말 것
- 기존 `AiEnabled`(텍스트 처리) 체크박스를 건드리지 않는다.
- 메모리 로드(InferenceSession 생성)는 06번에서 하지 않는다 — 05번(제안 바 활성화 시점)에서 한다.

## 검증
- 빌드 후 `AltKey.exe` 백그라운드 실행 → 설정 창 "AI 자동완성" 탭에서 on/off, 후보 수 변경이 `config.json`(runtime: `AppData/Roaming/AltKey/config.json`)에 반영되는지 확인.
- GUI 동작은 단위 테스트 불가(키보드 부재) → 백그라운드 실행 + config 파일 대조로만 검증.

## 산출물
- 수정된 `SettingsViewModel.cs`, `SettingsWindow.xaml` (새 TabItem 추가).
