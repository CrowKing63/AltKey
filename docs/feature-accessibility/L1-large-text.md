# [L1] 큰 텍스트 모드 (KeyUnit 연동)

## 이 문서의 역할

소형 LLM이 단독으로 수행하는 **단일 구현 프롬프트**.

## 목표

- 키 라벨 크기를 사용자가 키울 수 있게 한다.
- 현재 `KeyUnit` 계산 구조(`KeyboardView.xaml.cs`)와 충돌 없이 적용한다.

## 변경 범위

- `AltKey/Models/AppConfig.cs`
- `AltKey/ViewModels/SettingsViewModel.cs`
- `AltKey/Views/SettingsWindow.xaml`
- `AltKey/Themes/Generic.xaml`
- `AltKey/Controls/KeyButton.xaml`
- 필요 시 `AltKey/Views/KeyboardView.xaml.cs` (읽기 전용 확인 중심)

## 절대 금지

- `UpdateKeyUnit()`의 계산식 변경 금지 (이번 작업 범위 아님)
- `KoreanInputModule`/`HangulComposer` 수정 금지

## 구현 지시

1. `AppConfig`에 `KeyFontScalePercent`(예: 기본 100, 범위 80~220)를 추가.
2. `SettingsViewModel`에 해당 속성 바인딩 및 `ConfigService.Update` 연결.
3. `SettingsWindow.xaml`에 `NumericAdjuster`로 폰트 배율 UI 추가(슬라이더 금지).
4. `KeyButton` 스타일에서 실제 `FontSize`를 `KeyUnit`과 배율을 같이 반영하도록 바꾼다.
5. 라벨 잘림 방지를 위해 필요하면 `TextTrimming`/`TextWrapping`/마진을 조정한다.

## 설계 힌트

- 고정 리소스 `KeyFontSize`를 완전히 제거하지 말고 fallback으로 남긴다.
- 기본 폰트가 너무 커져 키 높이를 침범하지 않게 상한을 둔다.
- `SubLabel`은 메인 라벨보다 작은 비율을 유지한다.

## 수용 기준

- 설정 값 변경 시 키 라벨 크기가 즉시 반영된다.
- 창 크기(`Window.Scale`)를 바꿔도 폰트 비율이 안정적으로 유지된다.
- 큰 배율에서도 주요 키 레이블이 읽을 수 있는 수준으로 남는다.

## 검증

- 80/100/150/200% 각각에서 한글/영문 라벨 확인
- 자동완성 ON/OFF 상태에서 레이아웃 깨짐 여부 확인
- 제안 바 높이(`KeyRowHeight`)와 충돌 없는지 확인

## 참고

- Target Size 개념: https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html

