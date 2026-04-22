# [L1] 고대비 테마 추가 + 시스템 고대비 연동 정리

## 이 문서의 역할

소형 LLM이 단독으로 수행하는 **단일 구현 프롬프트**.

## 목표

- 앱 테마에 `HighContrast`를 추가한다.
- Windows 고대비 모드가 켜진 경우(`SystemParameters.HighContrast`) 사용자 선택이 `system`일 때 고대비 테마를 우선 적용한다.
- 색상만으로 의미를 전달하지 않도록 기본 대비를 강화한다.

## 변경 범위

- `AltKey/Themes/HighContrastTheme.xaml` (신규)
- `AltKey/Services/ThemeService.cs`
- `AltKey/Views/SettingsWindow.xaml`
- `AltKey/ViewModels/SettingsViewModel.cs`
- 필요 시 `AltKey/Models/AppConfig.cs` 주석/허용값 설명

## 절대 금지

- `Services/InputLanguage/KoreanInputModule.cs` 수정 금지
- `Services/HangulComposer.cs` 수정 금지
- 자동완성 파이프라인(`AutoCompleteService`, `SuggestionBarViewModel`) 수정 금지

## 구현 지시

1. `HighContrastTheme.xaml`을 만들고 기존 테마 키(`KeyboardBg`, `KeyBg`, `KeyFg`, `KeyBorder`, `Settings*`, `AccentBrush`)를 빠짐없이 정의한다.
2. `ThemeService.Apply()`가 `"HighContrast"`를 직접 처리할 수 있게 분기 추가.
3. `ThemeService.DetectSystemTheme()`에서 `SystemParameters.HighContrast == true`면 `"HighContrast"`를 반환.
4. 설정 창 테마 선택 UI에 "고대비" 옵션 추가.
5. `SettingsViewModel`에 `ThemeIsHighContrast` 바인딩 추가.
6. 기존 `system/Light/Dark` 동작 회귀가 없는지 확인.

## 수용 기준

- 설정에서 고대비를 선택하면 즉시 고대비 리소스가 적용된다.
- 설정이 `system`이고 OS 고대비가 켜지면 앱도 고대비로 전환된다.
- 라이트/다크 전환 기능은 기존과 동일하게 동작한다.

## 검증

- 수동:
- 라이트/다크/고대비 각각 전환 확인
- OS 고대비 ON/OFF 후 `system` 모드에서 반영 확인
- 제안 바/설정창/헤더 버튼 가독성 확인
- 테스트:
- 가능하면 테마 해석 로직에 단위 테스트 추가

## 참고

- Windows contrast themes: https://learn.microsoft.com/en-us/windows/apps/design/accessibility/high-contrast-themes
- WPF HighContrast API: https://learn.microsoft.com/en-us/dotnet/api/system.windows.systemparameters.highcontrast?view=windowsdesktop-9.0

