# 작업 02: 스위치 스캔 방식과 타이밍 옵션 보강

## 목표

단일 순차 스캔만 제공하는 현재 구조를 확장해 사용자가 스캔 방식과 속도를 더 세밀하게 조정할 수 있게 한다.

## 작업 난이도

중간~높음. `KeyboardViewModel`의 스캔 상태 관리를 정리하되 입력 전송 경로는 유지한다.

## 변경 범위

- `AltKey/Models/AppConfig.cs`
- `AltKey/ViewModels/KeyboardViewModel.cs`
- `AltKey/ViewModels/SettingsViewModel.cs`
- `AltKey/Views/SettingsWindow.xaml`
- 필요 시 새 enum/model 파일: `AltKey/Models/SwitchScanMode.cs`
- 테스트: 새 파일 `AltKey.Tests/SwitchScanNavigationTests.cs` 권장

## 금지 범위

- 자동완성 조합 처리 수정 금지
- 키 입력 실행은 기존 `KeyPressed(...)` 또는 명령 호출 경로를 유지
- 스캔 타이머에서 직접 `InputService.Send*` 호출 금지

## 설계 지시

### 1. 스캔 방식 설정 추가

문자열 또는 enum으로 `SwitchScanMode`를 추가한다.

권장 값:

- `Linear`: 현재처럼 모든 키를 순서대로 훑기
- `RowColumn`: 먼저 행을 훑고, 선택 후 해당 행의 키를 훑기
- `Manual`: 자동 타이머 없이 다음/이전 스위치로만 이동

기본값은 `Linear`로 둔다.

### 2. 타이밍 설정 추가

다음 설정을 추가한다.

- `SwitchScanInitialDelayMs`: 스캔 시작 후 첫 이동 전 대기 시간, 기본 800
- `SwitchScanSelectPauseMs`: 선택 후 다음 스캔 재개 전 대기 시간, 기본 500
- `SwitchScanCyclesBeforePause`: 전체를 몇 바퀴 돈 뒤 멈출지, 기본 0(무제한)
- `SwitchScanWrapEnabled`: 끝에서 처음으로 돌아갈지, 기본 true

모든 숫자 설정은 `NumericAdjuster`를 사용한다.

### 3. 스캔 상태를 명시적으로 관리

`KeyboardViewModel`에 현재 스캔 상태를 설명하는 필드를 정리한다.

예:

- 현재 단계: 행 선택 중 / 키 선택 중
- 현재 행 인덱스
- 현재 키 인덱스
- 일시정지 여부
- 현재 회전 횟수

주석에는 사용자가 속도나 방식을 조정할 때 어떤 값이 영향을 주는지 설명한다.

### 4. 행/열 스캔 구현

`RowColumn` 모드는 다음 흐름을 따른다.

1. 행 단위로 하이라이트한다.
2. 선택하면 해당 행 안의 키 단위 스캔으로 들어간다.
3. 키를 선택하면 입력 후 다시 행 단위로 돌아간다.
4. 취소/뒤로 동작이 있으면 키 단위에서 행 단위로 돌아간다.

첫 구현에서는 열 단위보다 "행 → 키"가 더 단순하고 한국어 키보드 구조에 맞다.

### 5. 수동 스캔 구현

`Manual` 모드는 타이머를 시작하지 않는다.

- 다음 키: 다음 대상
- 이전 키: 이전 대상
- 선택 키: 현재 대상 실행

외부 스위치 장치를 2개 이상 쓰는 사용자에게 중요하다.

## 수용 기준

- `Linear`는 기존 동작과 호환된다.
- `RowColumn`에서 키 입력까지 마우스 없이 가능하다.
- `Manual`에서 자동 이동이 발생하지 않는다.
- 선택 후 잠깐 멈춤이 적용되어 같은 키가 실수로 연속 선택되지 않는다.
- 설정 변경 시 앱 재시작 없이 반영된다.

## 테스트 지시

- 스캔 대상 목록이 비어 있을 때 크래시하지 않는지 테스트
- `Linear` 다음/이전 이동 테스트
- `RowColumn` 행 선택 후 키 선택 단계 전환 테스트
- `Manual`에서는 타이머가 시작되지 않는지 테스트 가능한 구조로 분리

## 수동 검증

1. 기본 키보드에서 `Linear`로 한글 단어 입력
2. `RowColumn`으로 같은 단어 입력, 대기 시간이 줄어드는지 확인
3. `Manual`에서 다음/선택 키만으로 입력
4. `SwitchScanIntervalMs`를 300/800/1500으로 바꿔 실제 속도 반영 확인

