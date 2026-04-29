# 작업 01: 스위치 입력 장치 커스텀

## 목표

스위치 제어 입력을 `Tab`, `Space`, `Enter`에 강제하지 않고, 사용자가 원하는 물리 키를 "다음", "선택", "이전", "취소/일시정지" 역할에 매핑할 수 있게 한다.

## 작업 난이도

중간. 자동완성 로직을 건드리지 않고 `AccessibilityNavigationService`, 설정 모델, 설정 UI 중심으로 처리한다.

## 변경 범위

- `AltKey/Models/AppConfig.cs`
- `AltKey/Services/AccessibilityNavigationService.cs`
- `AltKey/ViewModels/SettingsViewModel.cs`
- `AltKey/Views/SettingsWindow.xaml`
- 테스트: `AltKey.Tests/AccessibilitySafetyTests.cs` 또는 새 접근성 테스트 파일

## 금지 범위

- `KoreanInputModule`, `HangulComposer`, `AutoCompleteService` 내부 로직 수정 금지
- `InputService.SendAtomicReplace` 수정 금지
- 기존 `KeyPressedCommand` 입력 경로 우회 금지

## 설계 지시

### 1. 설정값 추가

`AppConfig`에 다음 값을 추가한다. 기본값은 현재 동작과 같게 둔다.

- `SwitchScanNextKey`: 기본 `"VK_TAB"`
- `SwitchScanSelectKey`: 기본 `"VK_RETURN"`
- `SwitchScanSecondarySelectKey`: 기본 `"VK_SPACE"`
- `SwitchScanPreviousKey`: 기본 빈 문자열
- `SwitchScanPauseKey`: 기본 빈 문자열

주석에는 "외부 스위치 장치가 실제로 보내는 키 이름"이라고 설명한다.

### 2. 키 매칭 유틸리티 분리

`AccessibilityNavigationService` 안에 문자열 설정값을 `VirtualKeyCode`로 해석하는 작은 도우미를 만든다.

수용 조건:

- 대소문자가 달라도 동작한다.
- 빈 문자열은 매칭되지 않는다.
- 잘못된 키 이름은 앱을 멈추지 않고 무시한다.

### 3. 설정 UI 추가

설정 창의 "스위치 스캔 입력" 영역에 키 매핑 입력을 추가한다.

첫 단계에서는 TextBox 방식도 허용한다. 단, 사용자가 코딩 전문가가 아니므로 설명 문구를 짧게 제공한다.

권장 UI:

- 다음 키
- 선택 키
- 보조 선택 키
- 이전 키
- 일시정지 키

2단계 작업으로 "키 누르기 캡처" 버튼을 만들 수 있게 TODO 주석을 남긴다.

### 4. 동작 연결

스위치 모드에서 훅이 키를 받으면 설정값에 따라 다음 동작을 호출한다.

- 다음: `Keyboard.AdvanceScan()`
- 이전: 새 메서드 `Keyboard.ReverseScan()` 추가 가능
- 선택: `Keyboard.SelectScanTarget()`
- 일시정지: 새 메서드 `Keyboard.ToggleScanPaused()` 추가 가능

이 작업에서 `ReverseScan`, `ToggleScanPaused`가 부담되면 이전/일시정지는 설정만 추가하고 "작업 02에서 구현" 주석을 남긴다.

## 수용 기준

- 기본 설정에서는 기존처럼 `Tab`, `Enter`, `Space`가 동작한다.
- 사용자가 선택 키를 다른 키로 바꾸면 그 키로 선택할 수 있다.
- 잘못된 키 이름을 넣어도 앱이 크래시하지 않는다.
- 스위치 모드가 꺼져 있으면 기존 물리 키 동작을 가로채지 않는다.

## 테스트 지시

- `AppConfig` 새 기본값 테스트
- JSON 직렬화/역직렬화 유지 테스트
- 키 이름 파싱 유틸리티가 정상/빈 값/잘못된 값에서 안전한지 테스트

## 수동 검증

1. 스위치 스캔 모드를 켠다.
2. 기본 `Enter`로 현재 스캔 키가 입력되는지 확인한다.
3. 선택 키를 다른 키로 바꾸고 앱 재시작 없이 반영되는지 확인한다.
4. 자동완성 ON/OFF 양쪽에서 한글 입력이 깨지지 않는지 확인한다.

