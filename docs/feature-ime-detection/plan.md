# OS IME(한/영) 상태 판별

상태: 설계·검증 기록 — 코드 미반영(사용자 요청으로 디스카드, 2026-11-27) · 범위: OS IME 한/영 상태 **판별** 로직

> 이 문서는 "포그라운드의 OS IME가 한글인지 영문인지"를 읽어오는 판별 로직의 설계·검증 기록이다. 화면에 상태를 표시하는 인디케이터(칩) 부분은 다루지 않는다. 아래는 향후 구현 시 참고용이며, 현재 저장소에는 해당 코드가 반영되어 있지 않다.

## 배경

AltKey는 Windows 가상 키보드다. 초기 개발 때 **OS IME의 한/영 상태를 신뢰할 수 있게 읽을 수 없어서**, "지금 사용자가 한글 상태인지 영문 상태인지"를 AltKey가 스스로 확신할 수 있는 구조로 만들었다. 그 대안으로 **IME를 우회해 한글을 유니코드로 직접 조립**하는 방식(`HangulComposer` + `KoreanInputModule`)이 자리 잡았다(상세: `docs/auto-complet/CORE-LOGIC-PROTECTION.md`).

그 사이 "화상 키보드를 눈으로 보고 있는 동안 실제 OS IME가 어느 상태인지"를 알려주는 신호가 필요하다는 요구가 생겼다. 이번 작업은 그 요구에 답하기 위해 **OS IME의 한/영 상태를 실제로 판별하는 서비스**를 추가한 것이다(현재 코드는 디스카드 상태, 아래는 설계 기록). (참고: 판별 아이디어는 [IME-Caret](https://github.com/mattisonit/IME-Caret) 등 기존 IME 상태 조회 방식에서 출발했다.)

핵심 원칙:
- 화상 키보드는 IME 자체를 **제어하지 않는다**. 여기선 **판별만** 하고, 그 결과를 AltKey가 보여주는 것에 그친다.
- 판별이 실패하면 한글을 기본값으로 삼지 않고 **"알 수 없음(Unknown)"** 으로 반환한다. 어느 쪽도 적극 주장하지 않겠다는 설계.

## 목표

- 포그라운드(지금 텍스트 입력이 일어나는 곳)의 OS IME 상태를 `Unknown / Korean / English` 세 값으로 판별한다.
- 상태가 바뀔 때만 이벤트를 발화해, UI·접근성(LiveRegion)이 반응할 수 있게 한다.
- 멈추지 않는다: 모든 외부 조회에 상한(50ms)을 걸어 IME가 응답하지 않아도 AltKey가 걸리지 않도록 한다.

## 설계

### 상태 판별 순서 (`QueryForegroundImeState`)

1. 포그라운드 창(`GetForegroundWindow`)을 찾는다.
2. **AltKey 자기 창은 대상이 아니다** — 프로세스 ID로 제외한다(자기 키보드의 IME는 무의미).
3. 후보 창 리스트를 만들고, 각 후보에서 IME 상태를 읽어온다. **첫 번째로 판별 성공한 값을 반환**.
4. 아무 후보에서도 판별에 실패하면 `Unknown`.

### 후보 창 구성 (`BuildCandidates`)

"지금 텍스트 입력이 일어나는 창"을 여러 방식으로 노린다(신뢰도가 높은 순서):

| 순서 | 출처 | 설명 |
|---|---|---|
| 1 | UIA 포커스 요소의 native window | 파일/브라우저의 실제 입력 창을 대신. 접근 실패해도 다음 방법이 대체 |
| 2 | 스레드 `GUITHREADINFO` (포커스/캐럿/활성) | 64비트 핸들 체인은 여전히 신뢰도가 높음 |
| 3 | 포그라운드 창 자체 | 기본 후보 |
| 4 | 부모 체인 (상위 3단계 + 루트/소유자) | 입력 창이 자식 컨트롤일 때 상위 창까지 커버 |

중복은 `HashSet`으로 제거한다.

### 개별 창에서 상태 읽기 (`QueryCandidate`)

각 후보 창에서 두 단계로 묻는다:

1. **① 기본 IME 윈도우에 묻기** — `ImmGetDefaultIMEWnd(window)` → 그 윈도우에 `WM_IME_CONTROL`(`IMC_GETOPENSTATUS`, `IMC_GETCONVERSIONMODE`)을 `SendMessageTimeout`(50ms 상한)으로 보내 상태를 읽는다.
2. **② 컨텍스트에 묻기** — ①이 실패하면 `ImmGetContext(window)` → `ImmGetOpenStatus` + `ImmGetConversionStatus`로 상태와 변환 모드를 읽고, 끝나면 `ImmReleaseContext`.

### 단일 진리원 (`DeriveState`)

모든 판별은 **한 곳**에서 결론을 내린다:

| 열린 상태 | 변환 모드 정보 | 언어(하위 10비트) | 결과 |
|---|---|---|---|
| 닫힘 | — | — | **English** (입력 자체가 영문) |
| 열림 | 없음 (구형/현대 IME가 "열렸다"만 알려주는 경우) | 한글이면 | **Korean** (영어로 잘못 판단하지 않음) |
| 열림 | 없음 | 한글 아니면 | Unknown |
| 열림 | 있음 | `IME_CMODE_NATIVE` | **Korean** |
| 열림 | 있음 | `IME_CMODE_ALPHANUMERIC` | **English** |
| 열림 | 있음 | 그 외 | Unknown |

> "열렸지만 변환 모드를 못 읽는 경우"를 한글 IME면 한글로 두는 이유: 구형/현대 IME가 열린 상태만 알려주는 경우가 있는데, 그걸 영어로 판단하면 사용자가 한글을 쓰는데 영문으로 오해하게 된다. 그래서 **한글 IME이면 한글**, 아니라면 Unknown.

### 배선 (구현 시 설계)

- DI에 `ImeStateService`를 단일 인스턴스로 등록.
- 뷰모델(`KeyboardViewModel`)에서:
  - 400ms DispatcherTimer가 주기적으로 `Refresh()` → 결과를 뷰모델 프로퍼티에 반영.
  - `StateChanged` 이벤트 → LiveRegion으로 음성 안내(접근성).
  - 입력 모드 변경 시에도 즉시 재확인.
- UI는 이 상태를 **표시/안내**하는 데만 쓴다(화면 인디케이터·음성 안내 등 — 구현은 별도).

## 필요한 Win32 함수

| 함수 | 역할 |
|---|---|
| `GetGUIThreadInfo` + `GUITHREADINFO` | 스레드의 활성/포커스/캐럿 창 |
| `GetParent` / `GetAncestor(GA_ROOT, GA_ROOTOWNER)` | 부모 체인 구성 |
| `GetKeyboardLayout` | 스레드 언어 ID (한글 판별용) |
| `SendMessageTimeout` | IME 윈도우에 상태 조회(50ms 상한, 멈추지 않음) |
| `ImmGetDefaultIMEWnd` / `ImmGetContext` / `ImmReleaseContext` | 기본 IME 윈도우·컨텍스트 |
| `ImmGetOpenStatus` / `ImmGetConversionStatus` | 열림 상태·변환 모드 |

상수: `WM_IME_CONTROL`, `IMC_GETCONVERSIONMODE`, `IMC_GETOPENSTATUS`, `SMTO_BLOCK \| SMTO_ABORTIFHUNG`, `IME_CMODE_ALPHANUMERIC/NATIVE/LANGUAGE`.

## 제약

- IME를 **제어하지 않는다** — 판별만 한다. 한/영 전환은 기존 상단바 VK_HANGUL 버튼이 담당한다.
- 자동완성·한글 조합·IME 핵심 보호 영역(`CORE-LOGIC-PROTECTION.md` §2)을 건드리지 않음 — 입력·조합 상태와 무관한 판별 서비스 추가.
- `AltKey.Tools` 분리 아키텍처와 무관(메인 앱 전용 변경).
- 모든 외부 조회에 50ms 상한 → IME가 응답하지 않으면 Unknown, AltKey는 멈추지 않는다.

## 검증 케이스 (`DeriveState`)

판별 결론을 내리는 단일 진리원 `DeriveState`가 검증해야 할 케이스:

1. 열림 + NATIVE → Korean
2. 열림 + ALPHANUMERIC → English
3. 닫힘 → English
4. 열림 + 변환 정보 없음 + 한글 IME → Korean (영어로 잘못 판단하지 않음)
5. 언어 정보 없음 + 변환 정보 없음 → Unknown
6. 변환 모드 언어 비트가 알려진 값이 아니면 → Unknown

## 향후 과제 (리팩토링) — 중요

> **기존에 OS IME 상태를 못 읽어서** "IME를 우회하고 한글을 유니코드로 직접 조립"하는 방식으로 구현된 부분이 있다. 위 설계의 판별 로직이 신뢰할 수 있게 동작한다면, 이 우회 구조를 더 단순하게 리팩토링할 여지가 생긴다 — 그게 향후 과제다.

### 현재 왜 복잡한가
- AltKey는 IME에 VK 코드를 보내고 IME가 조합을 하도록 하는 대신, **IME를 우회해 한글 자모를 `HangulComposer`로 유니코드로 조립**해서 직접 송신한다(`KoreanInputModule`). 입력은 두 가지 모드로 나뉜다:
  - `InputMode.VirtualKey`: OS IME에 VK 코드를 보내 IME가 조합 (관리자 모드).
  - `InputMode.Unicode`: IME를 우회해 유니코드로 직접 조립·송신 (비관리자 모드).
- 이유: 초기에 IME 상태를 신뢰할 수 없어서 "지금 무엇을 조합 중인지"를 AltKey가 스스로 확신해야 했고, 그 조합 상태로 자동완성 제안을 만들었다. 이 선택이 `SendAtomicReplace`의 원자성(백스페이스+재전송) 같은 복잡한 메커니즘을 만들어냈다.
- 영어는 QuietEnglish 서브모드로 분리되어 있고("가/A" 토글), OS IME 한/영은 상단바 VK_HANGUL 버튼이 담당 — 세 층위가 독립적으로 얽혀 있다.

### 리팩토링 방향 (아직 결정 안 함)
- IME 상태를 신뢰할 수 있으니, **IME를 우회하지 않고 VK 코드를 보내 IME가 조합을 하도록** 하는 경로로 갈 수 있을지 검토.
- 그러면 `HangulComposer`의 직접 조립·`SendAtomicReplace` 원자성 같은 부분이 단순해질 여지가 있다.
- 단, 자동완성이 "조합 중 상태를 확신해야 한다"는 전제가 유지되는 한, 단순히 IME에 맡기면 안 되는 부분도 있다 — **자동완성의 조합 상태 추적과 어떤 식으로 공존할지**가 핵심 판단 사항이다.
- 이 리팩토링은 `CORE-LOGIC-PROTECTION.md` §2의 보호 영역을 건드리는 큰 작업이므로, **별도 기획(스펙)을 거쳐 단계적으로** 진행해야 한다. 이번 IME 판별 작업과 분리해서 다루자.

## 검증 기록 (2026-11-27)

- 당시 구현 후 단위 테스트 통과, Release 빌드 오류 0.
- 샌드박스 번들(`dist/AltKey-sandbox`)에서 사용자가 실제 동작 확인 완료 — 포그라운드의 IME 상태를 `한`/`영`/알 수 없음으로 올바르게 판별·표시.
- 이후 사용자 요청으로 코드 변경은 모두 디스카드됨(문서만 유지). 위 검증 결과는 해당 구현에 대한 기록이다.
