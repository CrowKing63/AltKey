# [L3] 스위치 접근용 스캔 입력 모드

## 이 문서의 역할

소형 LLM이 단독으로 수행하는 **단일 구현 프롬프트**.

## 목표

- 마우스/터치가 어려운 사용자를 위해 자동 스캔 + 1~2스위치 선택 입력을 제공한다.
- 가상 키보드를 순차 탐색하면서 선택 키 하나로 입력할 수 있게 한다.

## 변경 범위

- `AltKey/Models/AppConfig.cs`
- `AltKey/ViewModels/SettingsViewModel.cs`
- `AltKey/Views/SettingsWindow.xaml`
- `AltKey/ViewModels/KeyboardViewModel.cs`
- `AltKey/Views/KeyboardView.xaml(.cs)`
- 필요 시 `AltKey/Controls/KeyButton.xaml`

## 절대 금지

- 입력 전달 API 계약 변경 금지 (`KeyPressedCommand` 경로 유지)
- 자동완성/한글 조합 로직 수정 금지

## 구현 지시

1. 설정 추가:
- `SwitchScanEnabled` (bool)
- `SwitchScanIntervalMs` (int, 기본 800, `NumericAdjuster`)
- `SwitchScanMode` (1스위치/2스위치)
2. 스캔 하이라이트 순서를 정의한다.
- 행 단위 -> 키 단위 2단계 스캔 또는 단일 순차 스캔 중 택1
3. 선택 입력 트리거를 만든다.
- 1스위치: Enter 또는 Space
- 2스위치: 다음/선택 키 분리
4. 현재 스캔 대상은 시각적으로 분명해야 하며, `LiveRegion`으로도 공지한다.
5. 스캔 모드 OFF 시 기존 동작 100% 유지.

## 수용 기준

- 마우스 없이 스캔만으로 키 입력이 가능하다.
- 한글/영문 서브모드 모두 입력 가능하다.
- 자동완성 ON/OFF에서 크래시나 입력 소실이 없다.

## 검증

- 1스위치/2스위치 각각 수동 테스트
- 긴 입력 시나리오(한글 단어, 띄어쓰기, 백스페이스) 점검
- 접근성 공지 스팸(과도 낭독) 없는지 확인

## 참고

- Keyboard/focus navigation: https://learn.microsoft.com/en-us/windows/apps/design/input/focus-navigation
- WCAG 2.2 target size 맥락: https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html

