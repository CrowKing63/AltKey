# [L1] 포커스 가시화 + 탭 탐색 모드

## 이 문서의 역할

소형 LLM이 단독으로 수행하는 **단일 구현 프롬프트**.

## 목표

- 키보드/보조기기 사용자가 현재 포커스 위치를 명확히 알 수 있게 한다.
- 기본 사용 흐름(마우스/터치)에는 영향이 없게 접근성 탐색 모드를 분리한다.

## 변경 범위

- `AltKey/Models/AppConfig.cs`
- `AltKey/ViewModels/SettingsViewModel.cs`
- `AltKey/Views/SettingsWindow.xaml`
- `AltKey/Controls/KeyButton.xaml`
- `AltKey/Controls/KeyButton.xaml.cs`
- 필요 시 `AltKey/Views/KeyboardView.xaml`

## 절대 금지

- 입력 전송 로직(`InputService.Send*`) 변경 금지
- 자동완성/한글 조합 로직 변경 금지

## 구현 지시

1. 설정 `KeyboardA11yNavigationEnabled`(기본 `false`)를 추가.
2. 설정 UI에 `NumericAdjuster` 없이 체크박스 토글 추가.
3. 탐색 모드 ON일 때만 키 버튼을 탭 탐색 가능(`Focusable/IsTabStop=true`)하게 한다.
4. 포커스 시 테두리/광선 등 시각 피드백을 명확히 추가한다.
5. 포커스 스타일은 라이트/다크/고대비에서 모두 보이게 만든다.
6. 탐색 모드 OFF일 때는 기존처럼 탭 정지로 인해 입력 흐름이 흔들리지 않게 유지한다.

## 수용 기준

- Narrator 또는 키보드 탭으로 이동할 때 현재 키가 시각적으로 분명히 보인다.
- 접근성 탐색 모드 OFF에서는 기존 동작(포커스 비활성)이 유지된다.
- `AutomationProperties.Name` 읽기 품질이 기존 대비 저하되지 않는다.

## 검증

- 탭 이동 순서 확인(헤더 버튼 → 키 영역)
- 포커스 표시가 배경과 충분히 구분되는지 확인
- 입력 클릭 동작 회귀 없는지 확인

## 참고

- Keyboard accessibility: https://learn.microsoft.com/en-us/windows/apps/design/accessibility/keyboard-accessibility
- WPF FocusVisualStyle: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/styling-for-focus-in-controls-and-focusvisualstyle

