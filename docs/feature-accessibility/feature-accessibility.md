# 접근성 기능 계획 (2026-04-22 갱신)

이 문서는 접근성 작업의 **허브 문서**다.  
소형 LLM에게 바로 맡길 수 있도록 기능별 단일 프롬프트 문서는 아래 폴더로 분리했다.

- 분해 문서 폴더: `docs/feature-accessibility/`

## 왜 갱신했는가

기존 문서(2026-04-15 작성)는 현재 코드와 충돌하는 지점이 있다.

- `SettingsView.xaml` 기준으로 쓰였지만 실제 파일은 `SettingsWindow.xaml`이다.
- 테마 UI는 콤보박스가 아니라 라디오 버튼(`system`, `Light`, `Dark`)이다.
- `KeyUnit` 계산은 `KeyboardViewModel`이 아니라 `Views/KeyboardView.xaml.cs`에 있다.
- `AutomationProperties`/`LiveRegion`/`JamoNameResolver`는 이미 구현되어 있다.
- 설정 UI는 슬라이더보다 `NumericAdjuster` 중심 패턴이 이미 자리잡았다.

## 접근성 우선 원칙

- 자동완성/한글 조합 핵심 로직은 수정 금지 범위를 지킨다.
- 접근성 개선은 **입력 안정성(회귀 방지)**보다 우선할 수 없다.
- UI 조정은 색상 의존을 줄이고, 키보드/스크린리더 사용성을 함께 개선한다.
- 새 설정 추가 시 `AppConfig` 기본값은 보수적으로(기능 OFF) 시작한다.

## 지금 상태 요약

- 이미 있음: `AutomationProperties.Name/HelpText/AutomationId`, `LiveRegion` 공지, `KeyRepeat`, `Dwell`, `Sound`.
- 미완료/개선 여지: 고대비 전용 테마(앱 자체)와 시스템 고대비 연동 정교화, 큰 텍스트 배율(키 라벨/보조 라벨) 사용자 설정, 색상 외 신호(Sticky/Locked 패턴/텍스트) 강화, TTS 라벨 읽기(속도/호버 정책 포함), 애니메이션 최소화 모드(시스템 설정 연동), 스위치 접근(스캔 입력) 같은 중증 운동장애 대응.

## 분해 문서 목록

- [README.md](/C:/Users/UITAEK/AltKey/docs/feature-accessibility/README.md)
- [L1-contrast-theme.md](/C:/Users/UITAEK/AltKey/docs/feature-accessibility/L1-contrast-theme.md)
- [L1-large-text.md](/C:/Users/UITAEK/AltKey/docs/feature-accessibility/L1-large-text.md)
- [L1-focus-visible-tab-navigation.md](/C:/Users/UITAEK/AltKey/docs/feature-accessibility/L1-focus-visible-tab-navigation.md)
- [L2-tts-key-readout.md](/C:/Users/UITAEK/AltKey/docs/feature-accessibility/L2-tts-key-readout.md)
- [L2-color-independent-state.md](/C:/Users/UITAEK/AltKey/docs/feature-accessibility/L2-color-independent-state.md)
- [L2-reduced-motion.md](/C:/Users/UITAEK/AltKey/docs/feature-accessibility/L2-reduced-motion.md)
- [L3-switch-scan-input.md](/C:/Users/UITAEK/AltKey/docs/feature-accessibility/L3-switch-scan-input.md)

## 외부 기준(조사 출처)

- WCAG 2.2 신규 기준 개요: https://www.w3.org/WAI/standards-guidelines/wcag/new-in-22/
- WCAG 2.5.8 Target Size (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- Windows contrast themes 가이드: https://learn.microsoft.com/en-us/windows/apps/design/accessibility/high-contrast-themes
- Windows keyboard accessibility 가이드: https://learn.microsoft.com/en-us/windows/apps/design/accessibility/keyboard-accessibility
- WPF `SystemParameters.HighContrast`: https://learn.microsoft.com/en-us/dotnet/api/system.windows.systemparameters.highcontrast?view=windowsdesktop-9.0
- WPF FocusVisualStyle: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/styling-for-focus-in-controls-and-focusvisualstyle
- WPF `AutomationProperties.Name`: https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.automationproperties.name?view=windowsdesktop-9.0
