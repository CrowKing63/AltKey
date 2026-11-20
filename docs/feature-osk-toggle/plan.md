# OSK(화면 키보드) 버튼 토글 복원

상태: 검증 완료(사용자 샌드박스 확인) · 작성일 2026-11-20 · 범위: 메인 앱 상단바 ⌨ 버튼

## 배경

상단바 ⌨(화면 키보드) 버튼에 대한 사용자 보고:
1. 어느 때는 버튼이 작동하지 않는다.
2. OSK를 열고 나서 버튼으로 다시 닫을 수 없다(토글로 알고 있었음).

### 조사 결과 (2026-11-20, 이 컴퓨터에서 실측 재현)

| 항목 | 확인 내용 |
|---|---|
| 현재 코드 | `MainViewModel.SendOsk()`가 `osk.exe` 직접 실행 우선, `Process.Start` 예외 시에만 Win+Ctrl+O 폴백 |
| 이력 | 원래 Win+Ctrl+O만 전송(= OS 토글). 커밋 `3652e4e`(v0.8.3)에서 직접 실행 방식으로 교체되며 닫기 수단 상실 |
| 재실행 거동 | **OSK가 열린 상태에서 `osk.exe` 재실행 = 무동작** (3회 확인: 새 프로세스 없음, 기존 창 복원·포커스 이동 없음). 두 번째 인스턴스는 즉시 종료 |
| 토글 단축키 | Win+Ctrl+O는 열린 OSK를 정상적으로 닫음(실측 확인) |
| 폴백 실효성 | exe가 존재하는 한 `Process.Start`는 예외를 던지지 않음 → 폴백은 사실상 죽은 코드 |
| 권한 | `osk.exe`는 매니페스트 `uiAccess=true`로 **Medium에서 실행해도 High 무결성으로 자동 상승**. Medium AltKey는 osk.exe를 taskkill 불가(액세스 거부 실측), WM_CLOSE도 UIPI로 차단(실측) |
| 창 속성 | 클래스 `OSKMainClass`, topmost로 열리지만 포커스는 가져가지 않음(입력 앱 포커스 유지 설계) |
| 자동 시작 | 레지스트리 Run/Accessibility에 OSK 자동 시작 없음 |

**증상 설명**: 두 증상 모두 같은 뿌리 — 버튼에 닫기 경로가 없고, OSK가 이미 실행 중이면(이전에 열어둔 경우 등) 재실행이 무동작이라 "작동 안 함"으로 체감된다.

## 목표

- ⌨ 버튼을 **토글**로 복원: 닫혀 있으면 열고, 열려 있으면 닫는다.
- v0.8.3의 접근성 개선 의도(포커스 조작이 제한된 환경에서도 직접 실행 우선)는 유지한다.
- 열기/닫기 동작을 음성 안내(LiveRegion)로 구분한다.

## 설계

### 동작 결정표

| OSK 창 상태 | 동작 | 결과 안내 |
|---|---|---|
| 열려 있음 (`OSKMainClass` 창 존재) | Win+Ctrl+O 전송 (OS가 내부에서 닫기 처리) | "화면 키보드 닫기" |
| 닫혀 있음 | `osk.exe` 직접 실행 (기존 후보 경로 유지) | "화면 키보드 열기" |
| 닫혀 있음 + 직접 실행 실패 | Win+Ctrl+O 폴백 (기존 동작 유지) | "화면 키보드 열기" |

### hook point

- `OskLauncherService`에 토글 결정 로직을 소유시킨다 (서비스가 결정, VM은 배선만 — `AnchoredPanelService.Toggle` 패턴):
  - `IsRunning()`: `Win32.FindWindow("OSKMainClass", null)`로 창 존재 확인. virtual이라 테스트에서 재정의 가능.
  - `Toggle(Action sendToggleHotkey)`: 위 결정표대로 수행하고 `OskToggleResult { Open, Close }` 반환.
- `MainViewModel.SendOsk()`: `Toggle` 호출 + 결과에 따른 안내만 담당. 단축키 전송은 기존 `InputService.SendCombo` 경로 그대로.
- 닫기 수단으로 Win+Ctrl+O를 쓰는 이유: 프로세스 kill/WM_CLOSE는 무결성 차이로 차단됨(실측). OS 단축키는 OS가 직접 처리해서 권한 문제를 우회한다.

### 버튼 표시

- 툴팁 "화면 키보드 열기" → "화면 키보드 열기/닫기", 접근성 도움말 "화면 키보드를 엽니다." → "화면 키보드를 열거나 닫습니다."
- `IsToggle` 시각 상태는 쓰지 않는다: OSK는 외부(X 버튼, Win+Ctrl+O)에서도 열고 닫히므로 버튼이 상태를 표시하려면 별도 폴링이 필요하다. 범위 밖 — 버튼은 동작 버튼으로 유지.

### 재진입 분석 (주입 경로)

- Win+Ctrl+O는 `InputService.SendCombo` → 태그된 빌더(`DwExtraInfo = INPUT_EXTRAINFO_ALTKEY`)를 거친다. 전역 훅(`AccessibilityNavigationService.HookProc`)은 태그된 이벤트를 그대로 통과시키므로 재진입 없음. 기존 폴백과 동일한 경로라 새 주입 메커니즘 없음.
- 시스템 단축키이지 앱에 넣는 텍스트가 아니므로 대상-포커스 가드 불필요(기존 폴백과 동일).

## 제약

- 자동완성/한글 조합/IME 핵심 보호 영역(`CORE-LOGIC-PROTECTION.md` §2)은 건드리지 않음 — 입력 모듈·조합 상태와 무관한 상단바 버튼 동작 변경.
- `AltKey.Tools` 분리 아키텍처와 무관(메인 앱 전용 변경).
- 주석은 한국어, 기존 주석 함께 갱신(주석 작성 가이드 준수).

## 수정 파일

| 파일 | 변경 |
|---|---|
| `AltKey/Platform/Win32.cs` | `FindWindow` P/Invoke 추가 |
| `AltKey/Services/OskLauncherService.cs` | `IsRunning()`, `Toggle(...)`, `OskToggleResult` 추가 + 클래스 주석 갱신 |
| `AltKey/ViewModels/MainViewModel.cs` | `SendOsk()`를 Toggle 배선으로 교체, 도움말 문구 갱신 |
| `AltKey/Models/HeaderButtonConfig.cs` | IdOsk 툴팁 갱신 |
| `AltKey.Tests/OskLauncherServiceTests.cs` | 토글 결정표 테스트 추가 |

## 테스트 계획

단위 테스트(기존 Tracking 패턴 확장 — `IsRunning`/실행 결과를 재정의하고 단축키 호출을 기록):
1. 열려 있음 → 단축키 1회 전송, 실행 시도 없음, Close 반환
2. 닫혀 있음 + 실행 성공 → 단축키 미전송, Open 반환
3. 닫혀 있음 + 실행 실패 → 단축키 폴백 전송, Open 반환
4. 기존 `TryLaunch` 후보 경로 테스트 3종 유지(회귀 없음)

실측 검증(조사 시 확인 완료): `OSKMainClass` 창 클래스, Win+Ctrl+O의 닫기 동작, 재실행 무동작.

## 수동 검증 체크리스트

사용자가 이 컴퓨터 디버그 빌드(자동완성 켠 상태)로 직접 확인:
- [ ] OSK 닫힌 상태 → ⌨ 누르면 OSK 열림 + "화면 키보드 열기" 안내
- [ ] OSK 열린 상태 → ⌨ 누르면 OSK 닫힘 + "화면 키보드 닫기" 안내
- [ ] 연속 토글 3회 이상 정상 왕복
- [ ] OSK를 X 버튼으로 닫은 직후 ⌨ → 다시 열림
- [ ] Win+Ctrl+O로 OSK를 연 뒤 ⌨ → 닫힘
- [ ] 일반 앱(메모장 등)에서 타이핑 중 ⌨ 토글 후에도 입력 포커스가 앱에 유지되는지
- [ ] (가능하면) 관리자 권한 앱이 포그라운드인 상태에서 토글 — AltKey가 관리자 권한이면 동작, 일반 권한이면 닫기 단축키가 막힐 수 있음(아래 제약 참조)

## 가정 · 오픈 질문 · 알려진 제약

- **다른 가상 데스크톱에 OSK가 있는 경우**: `FindWindow`는 현재 데스크톱 창만 찾음 → 닫혀 있다고 판단해 직접 실행하지만 기존 인스턴스 때문에 무동작(현재와 동일한 거동). 드문 경우라 이번 범위에서 제외.
- **포그라운드가 관리자 권한 앱 + AltKey 일반 권한**: SendInput가 UIPI로 차단될 수 있음 — AltKey 일반 타이핑과 같은 제약. AltKey를 관리자 권한으로 실행하면 해소(기존 관리자 모드).
- **창 닫힘 직후 프로세스 잔존 레이스**: X로 닫은 직후 아주 짧게는 프로세스가 남아 있어 새 실행이 묻힐 수 있음. 창 기준 판정이라 레이스 창이 매우 짧음(수동 체크리스트에서 확인).
- 조사 결과 기준 자동 시작 설정은 없는 환경이라 "OSK가 항상 떠 있어서 버튼이 죽은 것처럼 보이는" 시나리오는 이 컴퓨터에서는 재현되지 않음 — 이미 실행 중인 경우의 무동작이 핵심 원인.

## 구현 결과 (2026-11-20)

- 단위 테스트 **373개 전부 통과**(신규 토글 결정표 테스트 3개 포함), `AltKey.Tools` 포함 Release 빌드 오류 0.
- 실측 검증: `FindWindow("OSKMainClass")` 감지가 닫힘(0) → 열림(hwnd) → 토글 닫기 후 다시 0으로 왕복하는 것 확인, Win+Ctrl+O 전송 후 osk 프로세스 종료 확인.
- 샌드박스 번들: `dist/AltKey-sandbox` (SandboxTestBuild=true, 입력 테스트 전용이라 Tools 미포함).
- 수동 검증: 사용자가 샌드박스 번들에서 체크리스트 확인 완료(2026-11-20).
