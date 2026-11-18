# 스크래치패드 가상키모드 한글 조합 실패 — 조사 기록

> 상태: **선택지 B로 결정 — 스크래치패드 유니코드 경로 복구 + 스크래치패드 헤더에 `가/A` 버튼 추가**.
> 아래 조사 내용은 근거 보존용으로 남긴다. 코드 수정은 사용자 승인 후 진행했다.

## 증상

- 스크래치패드를 열고 **가상 키보드(가상키모드 / VirtualKey)**로 `ㄱ` → `ㅏ`를 누르면 `가`가 아니라 `ㄱㅏ`로 갈라진다.
- 같은 스크래치패드에서 **물리 키보드**로 `ㄱ` → `ㅏ`를 누르면 `가`로 조합된다.
- **다른 앱(메모장 등)**에서는 가상 키보드로 `ㄱ` → `ㅏ`를 누르면 `가`로 조합된다.

로그: `dist/input-diagnostic.log` (각 시도 후 보관된 로그에서 확인).

## 확인된 사실

| 항목 | 값 | 출처 |
|---|---|---|
| 입력 모드 | `VirtualKey` | `IME_KEY mode=VirtualKey` |
| 키보드 레이아웃 | `hkl=0x4120412` (한국어) | 로그 공통 |
| IME 열림 | `imeOpen=True`, `imeConversion=0x1 native=True` | 로그 공통 |
| 텍스트 상자 포커스 | 키 입력 전·후 모두 `textBoxFocused=True`, `focusHwnd`도 메모 창 | `IME_KEY` / `KEY_FOCUS_READY` |
| 주입 키 | `vk=0x52` + `vk=0x4B` (VK 방식), 또는 `vk=0x0 scan=0x20` (스캔코드 방식) | `OWN_SENDINPUT keys=...` |
| 전송 결과 | `sent=2 error=0` (Windows가 주입은 수락) | `OWN_SENDINPUT` |
| 결과 텍스트 | 매 키마다 `SCRATCHPAD_TEXT_CHANGED length=N` — 조합 없이 자모가 확정됨 | 로그 공통 |

## 배제된 가설

1. **포커스 이동이 조합을 취소** — 포커스 복원을 PreviewMouseDown으로 앞당겨 `textBoxFocused=True`가 유지되도록 했지만 증상 동일.
2. **스캔코드 주입 방식** — `KEYEVENTF_SCANCODE` → 일반 VK(`wVk` 설정)로 바꿔도 증상 동일.
3. **KeyDown/KeyUp 동시 주입** — 20ms 간격으로 분리해도 증상 동일.
4. **스크래치패드 전용 IME 강제 정책** (`ScratchPadImePolicy`의 `On`/`Native`) — 완전히 제거해 기본값(`DoNotCare`)으로 두어도 증상 동일.
5. **IME 자체 / TextBox 설정** — 물리 키보드로는 같은 창에서 조합되므로 정상.

진단용으로 넣었던 `WM_IME_*` 훅은 `IME_MSG`를 한 번도 기록하지 못했다. 훅이 `OnSourceInitialized` 시점의 `PresentationSource.FromVisual`에 붙어 실제 HwndSource를 놓쳤을 가능성이 있어, 이 로그만으로 "IME이 조합을 시작하지 않았다"고 단정할 수는 없다.

## 남은 선택지

| 선택지 | 내용 | 장단점 |
|---|---|---|
| A | VirtualKey 모드에서도 한글 자모는 AltKey 내부 조합(`HangulComposer` + `SendAtomicReplace`)으로 처리 | 근본 해결. 단 `docs/auto-complet/CORE-LOGIC-PROTECTION.md` §2 보호 영역 — 자동완성 연동, `TrackedOnScreenLength`, 관리자 모드 동작까지 재검증 필요 |
| B | 스크래치패드가 열려 있으면 Unicode 모드로 자동 전환 (이전 동작 복구) | 작고 빠름. 단 창별 모드 불일치 — `altkey-input-implementation` 스킬의 "패널에 모드 오버라이드 금지"에 반함 |
| C | 문서로 정리만 하고 보류 | safest. 증상은 남음 |
| D | 현재 상태 유지, 물리 키보드/다른 앱으로 우회 | 조치 없음 |

## 결정 및 구현 (선택지 B)

- 사용자 지시로 스크래치패드는 다시 **항상 Unicode(내부 조합)** 경로를 쓴다. OS IME 주입 경로에 의존하지 않는다.
- OS IME 한/영 전환 수단이 필요하므로 **스크래치패드 헤더의 붙여넣기 버튼 오른쪽(닫기 왼쪽)**에 `가/A` 버튼을 추가했다.
- 버튼 라벨은 메인 키보드의 `가/A` 키와 같은 `KoreanInputModule.ActiveSubmode`를 따라 `가`/`A`로 바뀐다.
  툴팁·스크린리더 이름도 같은 시점에 같이 갱신된다.

## 다음 단계

- 수동 검증: 스크래치패드에서 가상키로 `ㄱ → ㅏ` 입력 시 `가`가 나오는지,
  스크래치패드 헤더의 `가/A` 버튼 라벨과 메인 키보드 `가/A` 키 라벨이 함께 바뀌는지 확인한다.
- 확인 전까지 코드 커밋은 보류한다.
