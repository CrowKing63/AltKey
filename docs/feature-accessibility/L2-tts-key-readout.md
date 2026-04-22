# [L2] 키 라벨 TTS 읽기

## 이 문서의 역할

소형 LLM이 단독으로 수행하는 **단일 구현 프롬프트**.

## 목표

- 키 입력 시 키 라벨을 음성으로 읽어준다.
- 저시력 사용자가 현재 입력 대상과 상태를 빠르게 인지할 수 있게 한다.

## 변경 범위

- `AltKey/Services/AccessibilityService.cs` (신규)
- `AltKey/App.xaml.cs` (DI 등록)
- `AltKey/Models/AppConfig.cs`
- `AltKey/ViewModels/SettingsViewModel.cs`
- `AltKey/Views/SettingsWindow.xaml`
- `AltKey/ViewModels/KeyboardViewModel.cs`

## 절대 금지

- 키 입력 이벤트 순서 변경 금지 (`KeyPressed` 흐름 유지)
- `KoreanInputModule.HandleKey()` 분기 변경 금지

## 구현 지시

1. `AccessibilityService`를 만들고 TTS 엔진 래핑(`SpeakLabel`) 제공.
2. 설정값:
- `TtsEnabled` (bool, 기본 false)
- `TtsOnHover` (bool, 기본 false, 2단계 옵션)
- `TtsRate` (int, 기본 0, 범위 -5~5)
3. 설정 UI는 `NumericAdjuster`로 속도 값을 조정한다.
4. `KeyboardViewModel.KeyPressed()`에서 입력 처리 후(또는 확정 시점) 라벨을 읽도록 연결한다.
5. 너무 잦은 중복 발화를 막기 위해 간단한 디바운스/중복억제를 넣는다.
6. 한국어 음성이 없을 때 fallback 동작(기본 음성)으로 실패 없이 작동시킨다.

## 수용 기준

- 기능 OFF일 때 기존 동작과 완전히 동일하다.
- 기능 ON일 때 키 클릭 시 라벨이 읽힌다.
- 속도 설정이 즉시 반영된다.
- 성능 저하(버벅임, UI 멈춤)가 없어야 한다.

## 검증

- 한글/영문 서브모드 각각에서 읽기 결과 확인
- 토글 키(가/A), 기능 키(Shift/Enter) 읽기 품질 확인
- 자동완성 ON/OFF와 무관하게 회귀 없는지 확인

## 참고

- Windows speech interactions: https://learn.microsoft.com/en-us/windows/apps/develop/speech
- UIA Name 기본 원칙: https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.automationproperties.name?view=windowsdesktop-9.0

