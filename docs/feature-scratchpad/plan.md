# 스크래치패드(빠른 메모) 기능 계획

> 상태: **계획 (개정 2)** — U2~U4 완료·검증됨, U1 구현은 사용자 확인 대기. (2026-11-15 브레인스토밍 3라운드 + 범위 재확인)

## 1. 배경

사용자는 초안을 빠르게 써서 다른 앱에 붙여넣거나, 반대로 붙여받아 다듬는 흐름이 잦다. 기존 퀵노트 앱 대신 AltKey 자체에 "스크래치패드"를 추가한다. 사용은 마우스/시선 등 포인팅 장치 중심이라 **전역 단축키 없이 상단 헤더 버튼으로 열고 닫는다**.

### 이번 계획에서 함께 처리할 별개 항목
| 항목 | 내용 | 상태 |
|---|---|---|
| U2 | 클립보드 패널 스크롤 위치 버그 수정 | ✅ 완료 (샌드박스 검증, `2b33edb`) |
| U3 | 드래그 바 더블클릭 동작 변경(수평 중앙 배치) | ✅ 완료 (샌드박스 검증, `2b33edb`) |
| U4 | 기존 전역 단축키(Ctrl+Alt+K) 제거 | ✅ 완료 (샌드박스 검증, `2b33edb`) |
| U5 | 사용자 매뉴얼(Wiki) 등 문서 업데이트 | ⏳ U1 구현 후 |

## 2. 합의된 결정 (브레인스토밍 기록 + 범위 재확인)

| # | 결정 | 근거 |
|---|---|---|
| D1 | 전용 헤더 버튼, **기본 OFF** (AI ✨와 동일 패턴) | 사용자가 명시 |
| D2 | 전역 단축키 미사용 — 기존 단축키 제거(U4 ✅) | 포인팅 장치 사용자라 Ctrl+Alt+K 도달 어려움 |
| D3 | 키보드 창과 같은 차원의 **외부 확장** — 격리된 별도 윈도우 (MainWindow 크기·레이아웃 무간섭) | 사용자가 명시 (클립보드 패널처럼 내부에 뜨는 방식 아님) |
| D4 | 배치: 키보드 상/하 반만 구분 → 패드가 그 반대편에 **수평 중앙**. 매번 열 때 새로 계산, 위치 영속화 없음 | "키보드 위치가 같으면 같은 위치에 떠야 함" — 예측 가능성(시선 입력) 우선 |
| D5 | 포커스: topmost + **클릭 포커스** 일반 방식 + **열기 시 자동 포커스**(1회성). 입력을 패드로 강제 라우팅 **안 함** | 열기 자체가 명시적 의식이라 안전. "스크래치패드가 열리면 키보드 입력이 무조건 그곳으로" 방식은 기존 포커스 기능을 망가뜨려서 **거절**. 다른 앱 작업 중 패드는 위에 남고 포커스는 클릭한 곳으로 |
| D6 | 키보드 이동(드래그/상하 이동 버튼 등)이 있으면 열린 패드는 **자동 재배치** | 사용자가 명시 — "패드가 열린 채 상하 이동 버튼을 누르는 경우" |
| D7 | 앱 간 복사·붙여넣기 자동화 없음. 편집 숏컷 **버튼**만: 모두 선택 / 잘라내기 / 복사 / 붙여넣기 | 사용자가 명시 |
| D8 | 드래그 핸들 더블클릭 = 접기/펼치기 제거 → **창 수평 중앙 배치**. 접기/펼치는 빈 바 더블클릭 + 접기 버튼으로만 (U3 ✅) | 사용자가 명시 |
| D9 | **겹침 금지 불변식**: 패드가 떠 있는 동안 키보드(AltKey) 창과 겹치지 않는다. 드래그 중에도 즉시 클램프 | 사용자가 명시 — "알트키 창과 겹치지 않게 떠야 하고 드래그해서 옮길 수 있어야" |

## 3. U1: 스크래치패드 패널 (핵심)

### 3.1 윈도우 스펙
- `ScratchPadWindow`: 독립 top-level WPF Window, `Owner = MainWindow` (앱 종료 시 자동 정리, 작업표시줄에 안 보임: `ShowInTaskbar=False`).
- `Topmost=True` — `WindowService.SetTopmost` 패턴 재사용. **NOACTIVATE 아님** (TextBox 타이핑에 키보드 포커스가 필요).
- 고정 크기: 가로 600 × 세로 320 (키보드 900×350과 같은 "가로로 긴" 비율 계열, 리사이즈 없음 — 단순성·예측 가능성).
- 내용: 상단 드래그 핸들 행(이동용, U3의 키보드 창 패턴 재사용) + 숏컷 버튼 4개 + 전체 면적 `TextBox` (멀티라인, 자동 줄바꿈, 큰 글자 `ScaledSettingsBodyLargeFontSize` 계열 배율).
- 닫기: ✕ 버튼 → 숨김(종료 아님). MainWindow가 트레이로 숨겨지면 패드도 함께 숨김(동기화 명시 처리 — owned 창은 owner Hide에 자동 숨기지 않음).

### 3.2 포커스 모델
- 열기 시 `Activate()` + TextBox.Focus() (자동 포커스, D5).
- 그 외 일반 Windows 동작: 다른 앱 클릭 → 포커스 이동, 패드는 topmost로 가시 유지. **입력 무조건 라우팅 없음** (D5 — 강제 라우팅 방식은 기존 포커스 기능을 깨므로 거절).
- 키보드 창은 애초 `WS_EX_NOACTIVATE`라 포커스를 보유하지 않으므로 "기존 포커스 기능" 영향 없음(가정 — 구현 시 수동 검증으로 재확인).

### 3.3 배치 알고리즘 (순수 함수, 테스트 대상)
```
PlacePad(keyboardRect, workArea, padSize):   # 열기 시 / 키보드 이동 시(재배치)
  below = keyboardRect.Top < workArea.Top + workArea.Height / 2   # 키보드가 상반부면 패드는 아래
  left  = keyboardRect.Left + (keyboardRect.Width - padSize.Width) / 2  # 수평 중앙
  top   = below ? keyboardRect.Bottom + Gap : keyboardRect.Top - padSize.Height - Gap
  → WorkArea에 클램프 (Gap = 4px)

ConstrainPadPosition(x, y, keyboardRect, workArea, padSize):   # 드래그 중(D9 겹침 금지)
  1. WorkArea로 클램프 (화면 밖으로 나가지 않는 기본 불변식)
  2. keyboardRect와 교차하면: 이동 거리가 가장 작은 비교차 위치로 이동
     (후보: 키보드 창 위/아래/왼쪽/오른쪽 — WorkArea에 들어가는 후보 중 최소 변위 선택)
  3. 비교차 영역이 없으면(극소 작업 영역): WorkArea 클램프 결과 반환
```
- 호출 시점: ① 열기 시 ② 키보드 창 `LocationChanged`/크기 변경 시(열려 있을 때만). D6 — 드래그든 상하 이동 버튼이든 무엇이든 키보드 위치가 바뀌면 재계산. 패드 자체 드래그로 만든 임시 오프셋은 다음 재계산 시 사라짐(영속화 없음, D4).
- 드래그(`MouseMove`) 중: 매 이동마다 `ConstrainPadPosition` 적용 → **패드가 절대 키보드 창을 덮지 못함** (D9).

### 3.4 헤더 버튼 연결
- `HeaderButtonConfig`: `IdScratchPad = "ScratchPad"` 추가 — `CreateDefaults()`에 `visible: false` (D1), `IsBuiltInId`/이름·아이콘(✏️)·툴팁·접근성 이름 switch 보강.
- **마이그레이션 필수**: `MainViewModel.RebuildHeaderButtons()`는 목록이 비어 있을 때만 기본값을 넣으므로, 기존 사용자에게 새 버튼이 안 보인다 → `HeaderButtonConfig.EnsureAllBuiltIns(List<HeaderButtonConfig>)` 순수 함수 추가(누락된 BuiltIn ID를 목록 끝에 보정, 커스텀 버튼·순서 불변)을 **메인 앱과 AltKey.Tools 상단바 편집기가 공유** (§6.2 공용 ViewModel/Service 경계 규칙 — Models에 둠).
- `MainViewModel`에 `ToggleScratchPadCommand` (ClipboardPanel 토글과 동일 구조: 창 생성은 지연, 이후 Show/Hide만).

### 3.5 영속화 + 숏컷 버튼
- `ScratchPadService`: `DataDir\scratchpad.json` (`{ text, updatedAt }`). TextChanged 500ms 디바운스 자동 저장 + 닫기·앱 종료 시 저장. 손상 JSON → 빈으로 시작 (ClipboardService와 동일 관용).
- 숏컷 버튼(TextBox 참조로 실행): 모두 선택 / 잘라내기 / 복사 / 붙여넣기(`ClipboardHelper.GetTextWithRetry` 재사용). 앱 간 자동화 없음(D7).

## 4. 완료 항목 (U2~U4) — `2b33edb` 반영
- **U2**: 클립보드 패널 재개방 시 스크롤 맨 위(최근 항목)로 초기화 — `ClipboardPanel` 코드비하인드에서 `IsVisibleChanged` + `ScrollToVerticalOffset(0)` (레이아웃 완료 후 1회 더 반영).
- **U3**: 드래그 핸들 더블클릭 → 창 좌우 수평 중앙 배치 (`CenterWindowHorizontally`, MainWindow 초기 중앙화 동일 공식). 접기/펼치기는 빈 바 더블클릭 + 접기 버튼으로만.
- **U4**: 전역 단축키(Ctrl+Alt+K) 전체 제거 — `HotkeyService.cs` 파일 삭제, 설정 행·config 필드·DI 등록·테스트 DI 인자까지. STJ는 잔여 필드를 무시하므로 기존 설정 마이그레이션 안전.

## 5. 수정 파일 목록 (U1)

**신규**
- `AltKey/Views/ScratchPadWindow.xaml` / `.xaml.cs`
- `AltKey/ViewModels/ScratchPadViewModel.cs`
- `AltKey/Services/ScratchPadService.cs`
- `AltKey/Views/ScratchPadPlacement.cs` (순수 배치 함수 + D9 겹침 금지 클램프 — `KeyboardWindowPlacement` 옆)
- `AltKey.Tests/ScratchPad/...` (기능별 폴더, 파일당 400줄 이하)

**수정**
- `AltKey/Models/HeaderButtonConfig.cs` (IdScratchPad + EnsureAllBuiltIns 마이그레이션 함수 — 메인 앱·Tools 공유)
- `AltKey/ViewModels/MainViewModel.cs` (토글 명령, 패드 창 관리, LocationChanged 팔로우)
- `AltKey/MainWindow.xaml.cs` (LocationChanged 연결, 트레이 숨김 동기화)
- `AltKey/App.xaml.cs` (DI: ScratchPadService + ScratchPadViewModel)

## 6. 제약·보호 영역 점검

| 항목 | 판정 |
|---|---|
| CORE-LOGIC-PROTECTION §2 (자동완성/한글 조합) | **무관** — 별도 창+서비스. SendInput 자동화 없음(D7) |
| AltKey.Tools 경계 (§6) | `EnsureAllBuiltIns`만 Models 공용 모델에 추가 — 메인 전용 상태 주입 아님, 편집기도 동일 함수 사용 |
| 전역 훅/재진입 (SendInput re-entry) | 해당 없음 — 패드 타이핑은 일반 WPF TextBox 입력 |
| 테스트 규칙 | 기능별 폴더(`ScratchPad/`), 파일 400줄 이하, 주석은 "무엇을 검증하는지"만 |

## 7. 테스트 계획

1. `ScratchPadPlacementTests` — 상반부→아래/하반부→위, 수평 중앙, WorkArea 클램프, 극소 작업 영역 + **`ConstrainPadPosition` 비교차 케이스** (D9: 드래그 중 겹침 금지, 최소 변위 방향, 비교차 영역 없을 때 폴백).
2. `HeaderButtonEnsureBuiltInsTests` — 누락 BuiltIn 추가, 기존 순서·커스텀 버튼 불변, 멱등(두 번 호출해도 중복 없음), ScratchPad 기본 OFF 확인.
3. `ScratchPadServiceTests` — 저장/로드, 빈 파일, 손상 JSON → 빈 상태.
4. 회귀: 전체 `dotnet test` 최초 실행을 계약 변경 검증으로 사용.

## 8. 수동 검증 체크리스트 (샌드박스 번들 — AGENTS.md §4 레시피)

- [ ] **신규 설치**: 상단바에 스크래치패드 버튼 기본 안 보임, 설정에 전역 단축키 행 없음
- [ ] **기존 설치**: 헤더 버튼 목록에 스크래치패드 버튼이 `EnsureAllBuiltIns`로 자동 추가되고 기본 숨김
- [ ] 헤더 버튼 ON → 열기: 키보드 상단이면 아래·하단이면 위에, 수평 중앙. 포커스 자동 이동되어 바로 타이핑 가능
- [ ] 패드에서 타이핑 후 다른 앱 클릭 → 그 앱에 입력, 패드는 가시 유지 (강제 라우팅 없음)
- [ ] 키보드 드래그/상하 이동 버튼 → 열린 패드가 재배치 (D6)
- [ ] 패드 드래그로 임시 이동 → 다음 키보드 이동·열기 시 계산 위치로 복귀 (D4/D6)
- [ ] **패드 드래그를 키보드 창 쪽으로 시도 → 겹치지 못하고 가장 가까운 비교차 위치에 클램프** (D9)
- [ ] 숏컷 버튼 4개 동작 (모두 선택/잘라내기/복사/붙여넣기)
- [ ] 재시작 후 내용 유지 (`scratchpad.json`)
- [ ] 트레이로 숨김 → 패드도 함께 숨김, 복원 시 정상
- [ ] 키보드 위치가 같으면 매번 같은 위치에 패드가 뜸 (D4)

## 9. 비용 요약

| 단위 | 범위 | 추정 |
|---|---|---|
| U1 | 스크래치패드 패널 (서비스·창·배치+겹침 금지 클램프·헤더 버튼·포커스·숏컷 + 테스트) | 11–12.5h |
| U5 | 문서: Wiki 매뉴얼(스크래치패드 페이지, 상단바/드래그 동작 갱신, 핫키 제거 반영) + wiki-meta-manual 체크리스트 | 1.5h |
| — | 샌드박스 수동 검증 번들 | 1h |
| **합계** | | **≈ 13.5–15h (약 2일)** |

## 10. 가정·오픈 질문

- 멀티모니터: 주 모니터 WorkArea 기준(기존 코드들과 동일 가정). 부 모니터 지원은 이번 범위 밖.
- 패드 고정 크기(600×320)는 첫 값 — 수동 검증 시 글자 가독성 기준으로 조정 가능.
- Z-order: D9가 겹침을 원천 차단하므로 "AltKey 창이 항상 위에" 특수 z-management는 불필요 (양쪽 모두 Topmost, 활성화 순서가 계층을 정하지만 겹치지 않아 시각적 충돌 없음).
- 패드 드래그 위치는 **임시 오프셋** — D4/D6에 따라 다음 키보드 이동·열기 시 계산 위치로 복귀 (영속화 없음). 커스텀 위치를 기억하는 기능이 필요해지면 그때 추가.
- U5 문서 대상은 `Wiki/AltKey.wiki` (docs/wiki-meta-manual.md 지시서 준수: 한국어, 비전문가 언어, 접근성 관점).
