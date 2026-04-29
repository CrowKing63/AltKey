# 작업 05: 접근성 피드백과 테스트 체계 보강

## 목표

접근성 기능이 늘어나도 회귀를 잡을 수 있도록 공지/TTS 정책과 테스트 기준을 정리한다. 특히 스위치 제어는 눈으로 보기 어렵거나 느린 상호작용이 많으므로 자동 테스트와 수동 검증 체크리스트가 필요하다.

## 작업 난이도

중간. 기능 추가보다 안정화와 검증 중심이다.

## 변경 범위

- `AltKey/Services/LiveRegionService.cs`
- `AltKey/Services/AccessibilityService.cs`
- `AltKey/ViewModels/KeyboardViewModel.cs`
- `AltKey.Tests/AccessibilitySafetyTests.cs`
- 필요 시 새 테스트 파일:
  - `AltKey.Tests/SwitchScanNavigationTests.cs`
  - `AltKey.Tests/SuggestionScanTests.cs`

## 금지 범위

- 입력 전송 순서 변경 금지
- 자동완성 조합 알고리즘 수정 금지
- 테스트 편의를 위해 실제 앱 동작과 다른 우회 경로 만들기 금지

## 설계 지시

### 1. LiveRegion 공지 빈도 제한

`LiveRegionService`에 간단한 중복/빈도 제한을 넣는다.

권장 설정:

- 같은 메시지는 500ms 안에 반복 공지하지 않음
- 스캔 모드 공지는 `SwitchScanAnnounceMode` 설정을 따른다

`SwitchScanAnnounceMode` 값:

- `Off`
- `SelectionOnly`
- `EveryMove`

기본값은 `SelectionOnly` 또는 `Off`를 권장한다. 빠른 스캔에서 모든 이동을 읽으면 실사용이 어려울 수 있다.

### 2. TTS와 LiveRegion 역할 분리

TTS는 소리로 직접 읽는 기능이고, LiveRegion은 스크린리더에게 알려주는 기능이다. 두 기능을 완전히 같은 설정으로 묶지 않는다.

권장 설정:

- `TtsEnabled`: 직접 음성 안내
- `KeyboardA11yAnnounceFocus`: 탭 이동 공지
- `SwitchScanAnnounceMode`: 스위치 스캔 공지

### 3. 접근성 상태 테스트 확대

다음 테스트를 추가한다.

- 접근성 관련 설정 기본값은 기존 사용자를 방해하지 않는 값인지
- JSON round trip 후 설정이 유지되는지
- 스캔 대상이 없을 때 `StartScan`, `AdvanceScan`, `SelectScanTarget`이 크래시하지 않는지
- 스캔 선택이 직접 입력 API가 아니라 기존 명령 경로를 쓰는지 확인 가능한 구조인지
- 자동완성 제안 스캔 대상이 제안 변경 시 갱신되는지

### 4. 수동 검증 체크리스트 문서화

`docs/feature-accessibility-next/manual-test-checklist.md`를 추가한다.

포함할 항목:

- 일반 마우스/터치 입력 회귀
- 탭 네비게이션
- 1스위치 자동 스캔
- 2스위치 수동/자동 스캔
- 자동완성 제안 선택
- 한글 조합 중 제안 선택 후 다음 자모 입력
- QuietEnglish 모드
- 고대비/큰 글자/Reduced Motion 조합

## 수용 기준

- 빠른 스캔에서도 공지 스팸을 줄일 수 있다.
- TTS를 꺼도 스크린리더 공지는 별도로 쓸 수 있다.
- 새 테스트가 접근성 설정과 스캔 기본 동작을 검증한다.
- 수동 테스트 체크리스트가 문서로 남는다.

## 수동 검증

1. 스캔 간격 300ms에서 공지가 과도하지 않은지 확인
2. TTS ON/OFF, LiveRegion 공지 ON/OFF 조합 확인
3. Narrator 사용 시 현재 위치 공지가 이해 가능한지 확인
4. 한글 입력 안정성 회귀가 없는지 확인

