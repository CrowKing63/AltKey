# 작업 요약: AI 텍스트 처리 설정 UI 통합 및 퓨처 플랜 문서화

사용자의 요청에 따라, AI 텍스트 처리 기능을 설정할 수 있는 UI와 상단바 버튼 표시 순서를 관리할 수 있는 인터페이스를 구현하고 관련 향후 계획을 문서화했습니다.

## 1. 구현 내용

### 1) 설정창 (SettingsWindow.xaml) UI 업데이트
- **"AI 도구" 탭 추가**: `고급` 탭 전에 "AI 도구" 탭을 신설했습니다.
  - **기능 활성화 스위치**: `AiEnabled` 속성과 바인딩된 체크박스를 제공하여 "✨" 버튼 노출 여부를 조절합니다.
  - **인라인 설정**: API 엔드포인트, 암호화 저장되는 API 키, 모델명, 기본 프롬프트(다중 라인 텍스트박스), 그리고 타임아웃 세부 설정 항목을 직관적으로 제공합니다.
- **"상단바 버튼 표시 및 순서" 섹션 추가**: `외형` 탭 끝부분에 동적으로 표시할 상단바 버튼들을 체크박스와 ▲/▼(위/아래) 버튼으로 관리할 수 있는 `ItemsControl`을 구축했습니다.
  - 리스트 조작이 실시간으로 `SettingsViewModel`에 반영되며, "저장" 버튼을 통해 AppConfig에 반영되도록 했습니다.

### 2) 설정 뷰모델 (SettingsViewModel.cs) 확장
- AI 관련 속성들(`AiEnabled`, `AiEndpoint`, `AiApiKey`, `AiModel`, `AiDefaultPrompt`, `AiTimeoutSeconds`)을 `AppConfig`에 바로 연동하도록 Observable 프로퍼티를 추가했습니다. 
- API 키 저장은 사용자가 입력한 평문을 즉시 `SecureStorage.Encrypt()`로 Windows DPAPI 암호화하여 저장하며, 불러올 때는 `SecureStorage.Decrypt()`로 읽어들입니다.
- `HeaderButtons`를 `ObservableCollection`으로 매핑하여 UI의 리스트 변동(`MoveHeaderButtonUp/Down`)을 관리합니다.

### 3) 퓨처 플랜 (향후 개발 방향) 문서화 완료
- **`docs/feature-ai/FUTURE-layout-editor-action.md`**: 상단바뿐만 아니라 레이아웃 편집기에서 특정 키를 "AI 작동 키"로 만들고 버튼별 고유 프롬프트를 지정하는 방식에 대한 설계 방향성을 정리했습니다.
- **`docs/feature-ai/FUTURE-streaming-response.md`**: 응답을 스트리밍(Token-by-Token)으로 실시간으로 보여줄 때 발생할 클립보드 병목 문제를 설명하고, 이를 해결하기 위한 독립적인 '미리보기 패널 UI' 필요성을 정리했습니다.

## 2. 보안(DPAPI) 안내
API Key가 저장될 때는 Windows DPAPI(`DataProtectionScope.CurrentUser`)를 통해 암호화됩니다. 해당 키는 현재 윈도우 계정 외에는 읽을 수 없으므로(config.json에 평문으로 남지 않음), 설정 파일을 다른 기기로 복사해도 유출되지 않습니다.

## 3. 검증
- 모든 수정 코드가 경고나 에러 없이 빌드됨(`dotnet build` 확인)을 확인했습니다.
- 이제 앱을 실행해 "설정" 창의 "외형"과 "AI 도구" 탭을 직접 확인하실 수 있습니다.

## 4. 문제 해결 (2026-05 보완)

- **API가 호출되지 않는 것처럼 보일 때**: 상단바 ✨ 클릭 직후 포커스가 AltKey에 남으면 `Ctrl+C`가 대상 앱이 아니라 AltKey로 가서 선택 텍스트가 비어 있게 됩니다. 마지막으로 포커스가 있던 **AltKey가 아닌 창**으로 포커스를 되돌린 뒤 복사하도록 처리했습니다. 또한 Ollama 등에서는 `http://localhost:11434`처럼 호스트만 넣어도 `/v1/chat/completions`가 자동으로 붙습니다. **모델 이름**이 비어 있으면 요청이 실패하므로 설정에서 반드시 입력하세요. **「연결 테스트」** 버튼으로 엔드포인트·모델·키를 바로 확인할 수 있습니다.
- **설정 → 외형의 상단바 목록이 첫 실행에서 비어 있음**: `config.json`에 `header_buttons`가 비어 있으면 앱 로드 시점에 기본 목록을 채워 저장하도록 했습니다 (이전에는 `MainViewModel` 초기화 이후에만 기본값이 생겨 설정 창이 먼저 열리면 빈 목록으로 보일 수 있었습니다).
- **자동완성(AC) 토글이 실제와 다르게 보일 때**: `ToggleButton`에 `IsThreeState="False"`, `FallbackValue`/`TargetNullValue`를 지정하고, 초기화 직후 `AutoCompleteEnabled` 알림을 한 번 보내 시각 상태를 맞췄습니다. 좌측 상단바에 AC를 옮긴 경우에도 우측과 동일하게 토글로 동작합니다.
