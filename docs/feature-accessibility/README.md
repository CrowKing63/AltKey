# 접근성 작업 분해 가이드 (소형 LLM용)

## 목적

이 폴더의 각 문서는 **한 번에 한 기능**만 다루는 단일 프롬프트다.  
작업자는 문서 1개만 읽고 구현/테스트/검증까지 끝낼 수 있어야 한다.

## 공통 규칙

1. 자동완성/한글 조합 코드를 건드리기 전 `docs/auto-complet/CORE-LOGIC-PROTECTION.md` §2만 먼저 확인한다.
2. 접근성 작업이라도 입력 안정성을 깨면 안 된다.
3. 설정 UI는 슬라이더 대신 `NumericAdjuster`를 우선 사용한다.
4. 신규 설정은 `AppConfig` 기본값 `false`(또는 보수적 값)로 시작한다.
5. 테스트는 변경 기능과 가장 가까운 파일에 추가한다.
6. 로그가 필요하면 텍스트 파일로 남길 수 있게 한다.

## 난이도/우선순위 매트릭스

| 문서 | 난이도 | 우선순위 | 사용자 가치 |
|---|---|---|---|
| `L1-contrast-theme.md` | 낮음 | 높음 | 저시력 사용자 가독성 즉시 개선 |
| `L1-large-text.md` | 낮음 | 높음 | 키 라벨 식별성 개선 |
| `L1-focus-visible-tab-navigation.md` | 낮음 | 중간 | 키보드/보조기기 탐색성 개선 |
| `L2-tts-key-readout.md` | 중간 | 높음 | 저시력/난독 사용자 보조 |
| `L2-color-independent-state.md` | 중간 | 중간 | 색각 이상 사용자 상태 인지 개선 |
| `L2-reduced-motion.md` | 중간 | 중간 | 멀미/주의력 민감 사용자 배려 |
| `L3-switch-scan-input.md` | 높음 | 중간 | 중증 운동장애 사용자 접근성 확보 |

## 추천 실행 순서

1. `L1-contrast-theme.md`
2. `L1-large-text.md`
3. `L1-focus-visible-tab-navigation.md`
4. `L2-color-independent-state.md`
5. `L2-tts-key-readout.md`
6. `L2-reduced-motion.md`
7. `L3-switch-scan-input.md`

## 현재 코드 기준 주의 포인트

- 설정 창 파일은 `Views/SettingsWindow.xaml` / `ViewModels/SettingsViewModel.cs`다.
- 키 크기 계산 핵심은 `Views/KeyboardView.xaml.cs`의 `UpdateKeyUnit()`이다.
- 키 스타일은 `Controls/KeyButton.xaml` + `Themes/Generic.xaml` 리소스 조합이다.
- 접근성 기본 토대(`AutomationProperties`, `LiveRegion`)는 이미 있다.

