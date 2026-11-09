# 상단바 버튼 설정 종합 정비 — 작업 지시 인덱스

> **범위**: 상단바(헤더) 버튼과 관련된 세 가지 일관성 문제를 한 번에 맞춘다.
> 1. AI 버튼이 "AI 도구 탭(활성화)"과 "상단바 탭(위치/표시)" 두 곳에 나뉘어 있는 문제
> 2. 좌측/우측 배치 모델이 직관적·일관적이지 않은 문제(기본값 전부 우측, 이동 버튼 동작이 표시 순서와 어긋남)
> 3. AltKey.Tools 상단바 단축키 편집기의 "추가"와 "편집" 모드가 분리되어 있지 않아, 새 버튼을 추가하려고 열면 기존 버튼이 덮어써지는 버그

---

## 0. 현재 상태 요약 (레포 기준)

| 영역 | 현재 구현 | 문제 |
|---|---|---|
| AI 버튼 표시 | `AppConfig.AiEnabled`(AI 도구 탭 체크박스) + `HeaderButtons[Ai].Visible`(상단바 탭 체크박스) **두 개의 진실 소스** | `MainViewModel.cs:691`은 `AiEnabled`만 보고 ✨ 생성, `:667`은 `cfg.Visible`을 또 확인. 두 값이 동기화되지 않아 UI가 서로 다른 상태를 표시 |
| 좌우 배치 | 모든 버튼(기본+커스텀) `Position` 기본값 `"Right"`. 설정 탭 ▲▼는 **합치된 리스트** 안에서 이동 (`SettingsViewModel.cs:992-1013`) | 표시 순서는 쪽별로 필터링(`MainViewModel.cs:654-674`)하는데, 이동은 합쳐진 리스트 기준이라 반대쪽 버튼 사이를 지날 때 "아무것도 안 바뀜" 현상 발생. 기본값이 전부 우측이라 좌측/우측 배치가 무원칙 |
| 편집기 모드 | `HeaderShortcutEditorWindow.LoadFromConfig()`(`AltKey.Tools\HeaderShortcutEditorWindow.xaml.cs:120-142`): ID 없이 열면 **첫 번째 커스텀 버튼**을 불러옴. 허브 버튼은 `createNew:false, headerButtonId:null`으로 오픈(`AltKey.Tools\MainWindow.xaml.cs:109-118`) | "새로 추가" 진입점이 실제로는 첫 커스텀 버튼 편집 모드로 열림 → 저장 시 기존 버튼이 덮어써짐. `ToolWindowRegistry.GetOrCreate`은 이미 열린 창을 인자 무시하고 재사용(`AltKey.Tools\ToolWindowRegistry.cs:25-30`)이라 창이 열려 있었는지 여부에 따라 동작이 다름 |
| 한도 검증 | `SettingsViewModel.TryValidateHeaderButtonLayout()`(:929)과 `HeaderShortcutEditorWindow.ValidateHeaderButtonLimits()`(:187)이 **각자 구현** | 같은 규칙을 두 프로세스에서 따로 유지 → 기준 변경 시 한쪽만 고치는 드리프트 위험 |

---

## 1. 공통 결정 (모든 유닛에 적용)

### D1 — AI 버튼 표시는 상단바 탭이 관리 (수정: 2026-11)
- ✨(AI) 버튼의 **표시** 여부는 상단바 탭의 `HeaderButtons[Ai].Visible` 체크박스가 유일한 조절 소스입니다. 다른 기본 버튼과 똑같이 켜고 끌 수 있습니다.
- AI 도구 탭의 온/오프 체크박스(`AiEnabled`)를 제거합니다. AI 탭은 엔드포인트·API 키·모델·프롬프트·타임아웃 등 **동작 세팅만** 담당합니다.
- `AppConfig.AiEnabled` 필드를 코드에서 제거합니다(기존 설정 파일의 잔여 필드는 무시 — JSON 역직렬화 호환).

### D2 — 표시 순서는 "쪽별"로 정의
- 상단바 표시 순서 = 각 쪽(Left/Right) 안에서 리스트 순서 유지. (현재 `MainViewModel.RebuildHeaderButtons`의 동작과 동일 — 표시 로직은 안 바뀜)
- 설정 탭 ▲▼는 **같은 쪽 안**에서만 이동한다.
- 위치 토글(좌↔우) 시 버튼은 반대쪽 끝순으로 붙는다.

### D3 — 편집기는 "추가/편집" 명시 모드만 존재
- `--header-button-mode create` → 항상 새 버튼(기존 항목 절대 불러오지 않음).
- `--header-button-id {id}` → 그 ID만 편집. ID가 없으면(삭제됨) 저장 불가 + 안내.
- ID 없이 열리는 경로(허브 버튼)는 **추가 모드**로 바꾼다. "첫 커스텀 버튼 폴백" 로직을 제거한다.
- 이미 열린 편집기 창이 새 요청(다른 ID/모드)과 다르면 기존 창을 닫고 새로 만든다.

### D4 — 한도 검증은 `HeaderButtonConfig`의 정적 메서드 하나로 통합
- 커스텀 개수·좌우 표시 개수 검증을 `HeaderButtonConfig`에 올리고, 메인 앱(`SettingsViewModel`)과 Tools(`HeaderShortcutEditorWindow`)가 같은 메서드를 쓴다.

---

## 2. 유닛 목록과 의존 순서

| 유닛 | 파일 | 내용 | 의존 |
|---|---|---|---|
| 01 | `01-shared-validation.md` | 한도 검증 로직을 `HeaderButtonConfig`으로 통합 + 테스트 | 없음 |
| 02 | `02-ai-single-source.md` | AI 버튼 단일 스위치화 (D1) | 01 (검증 통합 후 UI 제거) |
| 03 | `03-position-consistency.md` | 쪽별 배치 모델 + 설정 탭 재구성 (D2) | 01 |
| 04 | `04-editor-create-edit.md` | 편집기 추가/편집 분리 버그 수정 (D3) | 01 (검증 통합) |

- 02·03·04는 서로 독립적으로 구현·검증 가능. 01이 먼저 끝나야 각 유닛이 공통 검증 메서드를 참조할 수 있다.
- **1차 범위**: 위 4유닛 전부. 좌측 기본값 도입(예: 자주 쓰는 버튼을 좌측으로)은 **향후** — 기존 설치 사용자의 배치를 바꾸지 않는다.

---

## 3. 향후 (이번 작업에 포함하지 않음)

- 좌측/우측 기본 배치 규칙 변경(기본값 일부 좌측 이동 등)
- 상단바 버튼 드래그 앤 드롭 재배열(현재 ▲▼ + 위치 토글 유지)
- 커스텀 버튼 "AI 액션" 전용 빠른 입력(기존 액션 빌더가 이미 `AiAction` 지원 — 새 UI 불필요)

---

## 4. 공통 검증 기준 (AGENTS.md §6.8)

모든 유닛 완료 후 한 번에:
1. 메인 앱 빌드(`AltKey.Tools\AltKey.Tools.csproj`로 전체 빌드)
2. `AltKey.Tests` 테스트 그린
3. 설정 창에서 도구(상단바 단축키 편집기) 실행 여부
4. Tools 저장 → 메인 앱·설정 창 즉시 반영(IPC 재로드 신호) 여부
