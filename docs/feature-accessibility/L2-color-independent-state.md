# [L2] 색상 비의존 상태 표시 (Sticky/Locked)

## 이 문서의 역할

소형 LLM이 단독으로 수행하는 **단일 구현 프롬프트**.

## 목표

- Sticky/Locked 상태를 색상만으로 표현하지 않게 개선한다.
- 색각 이상 사용자도 상태를 즉시 구분할 수 있게 한다.

## 변경 범위

- `AltKey/Controls/KeyButton.xaml`
- `AltKey/Themes/DarkTheme.xaml`
- `AltKey/Themes/LightTheme.xaml`
- 필요 시 `AltKey/Themes/HighContrastTheme.xaml`
- 필요 시 `AltKey/ViewModels/KeyboardViewModel.cs` (접근성 텍스트 보강)

## 절대 금지

- 입력/조합 처리 로직 수정 금지
- 키 액션 타입 구조 변경 금지

## 구현 지시

1. Sticky 상태에 색상 외 표시를 추가한다.
- 예: 점선 테두리, "S" 뱃지, 패턴 오버레이 중 하나
2. Locked 상태에 명확한 비색상 신호를 추가한다.
- 예: 자물쇠 아이콘은 이미 있으므로 대비/크기/위치를 개선
3. 접근성 텍스트(`AccessibleHelp`)가 시각 상태와 일치하는지 확인한다.
4. 라이트/다크/고대비에서 모두 충분히 식별 가능하도록 리소스를 조정한다.

## 수용 기준

- 색을 구분하지 못해도 Sticky/Locked 상태를 식별할 수 있다.
- 기존 사용자에게 시각적 혼란이 과하지 않다.
- Narrator의 상태 안내와 화면 상태가 일치한다.

## 검증

- Sticky만 켠 상태, Locked 상태 각각 캡처/확인
- 라이트/다크/고대비에서 시인성 점검
- 접근성 텍스트(도움말) 낭독 점검

## 참고

- 고대비/색상 의존 최소화 원칙: https://learn.microsoft.com/en-us/windows/apps/design/accessibility/high-contrast-themes
- WCAG 2.2 개요: https://www.w3.org/WAI/standards-guidelines/wcag/new-in-22/

