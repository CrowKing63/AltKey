# [L2] 애니메이션 최소화 모드

## 이 문서의 역할

소형 LLM이 단독으로 수행하는 **단일 구현 프롬프트**.

## 목표

- 멀미/주의력 민감 사용자를 위해 움직임 효과를 줄인다.
- OS 설정과 앱 설정을 함께 고려해 애니메이션 강도를 조절한다.

## 변경 범위

- `AltKey/Models/AppConfig.cs`
- `AltKey/ViewModels/SettingsViewModel.cs`
- `AltKey/Views/SettingsWindow.xaml`
- `AltKey/Controls/KeyButton.xaml`
- `AltKey/Views/KeyboardView.xaml.cs`
- 필요 시 애니메이션이 있는 다른 XAML 스타일

## 절대 금지

- 기능 동작 자체(입력 결과)를 바꾸는 수정 금지
- 자동완성/한글 조합 코드 수정 금지

## 구현 지시

1. 설정 `ReducedMotionEnabled` 추가(기본 false).
2. OS 설정을 함께 반영한다.
- 예: `SystemParameters.ClientAreaAnimation` 또는 동등 신호를 읽어 최종 적용값 계산
3. 다음 애니메이션을 조건부 비활성화/단축한다.
- 키 Hover/Pressed 확대 축소
- 접기/펼치기 창 높이 애니메이션
- 과도한 전환 효과
4. 기능 OFF 시 기존 연출은 유지한다.

## 수용 기준

- 최소화 모드 ON에서 화면 움직임이 체감적으로 줄어든다.
- ON/OFF 전환 후 동작 안정성(클릭 누락, 타이밍 꼬임) 문제 없다.
- 앱 시작/종료/접기 동작이 여전히 자연스럽다.

## 검증

- ON/OFF 비교 동영상 또는 수동 확인 기록
- 주요 상호작용(키 입력, 접기/펼치기, 패널 토글) 회귀 점검

## 참고

- Motion guidance: https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/motion
- Keyboard accessibility(비포인터 입력 맥락): https://learn.microsoft.com/en-us/windows/apps/design/accessibility/keyboard-accessibility

