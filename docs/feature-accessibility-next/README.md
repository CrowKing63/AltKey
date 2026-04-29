# 접근성 보강 작업 지시서 묶음

## 목적

`docs/feature-accessibility`의 1차 구현 이후 남은 접근성 격차를 정리하고, 무료로 사용할 수 있는 소형 AI 모델도 하나씩 맡아 구현할 수 있도록 작업을 잘게 나눈다.

이 묶음은 특히 다음 두 영역을 실사용 수준으로 끌어올리는 데 초점을 둔다.

- 탭 네비게이션: 단순 키 순회가 아니라, 범위·공지·탈출·설정 접근까지 가능한 조작 체계로 개선한다.
- 스위치 제어: 현재 `Tab`/`Space`/`Enter`에 묶인 베타 기능을 사용자 입력 장치 커스텀, 스캔 방식 선택, 자동완성 제안 스캔까지 가능한 구조로 확장한다.

## 공통 작업 규칙

1. 사용자 응답과 코드 주석은 한국어로 작성한다.
2. 접근성 향상에 실제로 도움이 되는지 먼저 판단한다.
3. 자동완성/한글 조합 관련 코드를 건드리기 전 `docs/auto-complet/CORE-LOGIC-PROTECTION.md` §2 "절대 건드리지 말 것"만 확인한다.
4. `HangulComposer`, `KoreanInputModule`의 조합 알고리즘, `InputService.SendAtomicReplace`의 원자적 전송 구조는 수정하지 않는다.
5. 설정 UI에서 숫자 조정은 슬라이더 대신 `NumericAdjuster`를 사용한다.
6. 새 기능 기본값은 보수적으로 꺼짐 또는 기존 동작 유지값으로 둔다.
7. 사용자가 직접 조정할 수 있는 숫자·문자열·키 이름에는 주석을 붙인다.

## 문서 순서

| 순서 | 문서 | 역할 |
|---|---|---|
| 0 | `00-accessibility-gap-report.md` | 현재 구현 진단과 전체 개선 방향 |
| 1 | `01-switch-input-device-customization.md` | 스위치 입력 장치 커스텀 |
| 2 | `02-switch-scan-engine-options.md` | 스캔 방식·타이밍·반복 옵션 보강 |
| 3 | `03-switch-scan-suggestion-bar.md` | 자동완성 제안 바 스캔 가능화 |
| 4 | `04-tab-navigation-production.md` | 탭 네비게이션 실사용 보강 |
| 5 | `05-accessibility-feedback-testing.md` | 공지/TTS/테스트/수동 검증 체계 |
| 부록 | `manual-test-checklist.md` | 구현 후 사람이 직접 확인할 접근성 시나리오 |

## 추천 실행 순서

1. `01-switch-input-device-customization.md`
2. `02-switch-scan-engine-options.md`
3. `03-switch-scan-suggestion-bar.md`
4. `04-tab-navigation-production.md`
5. `05-accessibility-feedback-testing.md`

`00-accessibility-gap-report.md`는 작업 전 전체 맥락 확인용이다.
