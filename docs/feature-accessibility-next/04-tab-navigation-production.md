# 작업 04: 탭 네비게이션 실사용 보강

## 목표

현재의 탭 이동 기능을 베타 수준에서 벗어나, 사용자가 현재 위치와 조작 범위를 이해하고 필요할 때 빠져나올 수 있는 안정적인 네비게이션 기능으로 보강한다.

## 작업 난이도

중간. 스위치 스캔과 같은 하이라이트 상태를 공유하므로 충돌 정리가 중요하다.

## 변경 범위

- `AltKey/Models/AppConfig.cs`
- `AltKey/Services/AccessibilityNavigationService.cs`
- `AltKey/ViewModels/KeyboardViewModel.cs`
- `AltKey/ViewModels/MainViewModel.cs`
- `AltKey/Views/KeyboardView.xaml`
- `AltKey/Views/SettingsWindow.xaml`

## 금지 범위

- 가상 키보드의 `Tab` 버튼이 대상 앱에 `Tab` 입력을 보내는 기존 동작을 깨지 말 것
- 자동완성/한글 조합 로직 수정 금지
- 탭 네비게이션 ON이 아닐 때 물리 키보드 입력을 가로채지 말 것

## 설계 지시

### 1. 탐색 범위 옵션 추가

`KeyboardA11yNavigationScope` 설정을 추가한다.

권장 값:

- `KeysOnly`: 키보드 본체 키만
- `KeysAndSuggestions`: 키보드 + 자동완성 제안
- `AllControls`: 헤더 버튼, 설정, 접기, 닫기 등 포함

기본값은 현재 동작과 가까운 `KeysOnly`로 둔다.

### 2. 탈출/일시정지 동작 추가

탭 네비게이션이 켜져 있으면 `Tab`, `Enter`, `Space`를 가로채므로 사용자가 혼란스러울 수 있다.

다음 중 하나를 구현한다.

- `Esc`: 접근성 포커스 해제
- `Ctrl+Tab`: 대상 앱으로 실제 Tab 보내기
- 설정값 `KeyboardA11yExitKey`: 기본 `"VK_ESCAPE"`

첫 구현에서는 `Esc`로 포커스 해제가 가장 단순하다.

### 3. 현재 위치 공지

포커스가 이동할 때 LiveRegion으로 현재 키 이름을 공지하는 옵션을 추가한다.

설정:

- `KeyboardA11yAnnounceFocus`: 기본 false

스캔 모드와 달리 탭 네비게이션은 사용자가 직접 이동시키므로 공지 빈도 부담이 낮다. 그래도 기본은 꺼짐으로 둔다.

### 4. 스위치 스캔과 상태 충돌 방지

스위치 스캔이 켜져 있으면 스위치 스캔이 우선한다. 탭 네비게이션과 스위치 스캔이 같은 `IsA11yFocused`를 써도 되지만, 내부 상태는 명확히 분리해야 한다.

권장:

- 현재 접근성 포커스 소유자 상태: `None`, `KeyboardNavigation`, `SwitchScan`
- 한 모드가 시작되면 다른 모드의 인덱스/타이머를 정리

### 5. 헤더 버튼 접근

`AllControls`는 한 번에 완성하기 어렵다. 1차 구현에서는 다음만 포함해도 된다.

- 자동완성 토글
- 설정
- 접기/펼치기
- 닫기

각 버튼은 기존 Command나 Click 핸들러를 재사용해야 한다.

## 수용 기준

- 탭 네비게이션 ON에서 `Tab`/`Shift+Tab` 이동, `Enter`/`Space` 실행이 유지된다.
- `Esc`로 접근성 포커스를 해제할 수 있다.
- 자동완성 제안 포함 범위가 켜져 있으면 제안에도 이동할 수 있다.
- 스위치 스캔 ON/OFF 전환 시 하이라이트가 꼬이지 않는다.
- 탭 네비게이션 OFF에서는 기존 입력 흐름을 가로채지 않는다.

## 테스트 지시

- 탐색 범위 기본값 테스트
- 포커스 해제 호출 시 모든 `IsA11yFocused`가 false가 되는지 테스트
- 스위치 스캔 시작 시 탭 네비게이션 포커스가 정리되는지 테스트

## 수동 검증

1. 탭 네비게이션 ON
2. `Tab`, `Shift+Tab`, `Enter`, `Space`, `Esc` 동작 확인
3. 자동완성 ON 상태에서 제안 포함 범위 확인
4. 스위치 스캔을 켰다가 끈 뒤 탭 네비게이션이 정상 복귀하는지 확인

