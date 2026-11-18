# 클립보드 즐겨찾기 — 핀 칩 스트립 + 키보드 내비게이션 제거 계획

> 상태: 계획 (사용자 확정 2026-11-18) | 목업: `mockup.html` (본 폴더, 브라우저로 열기)
> 관련 문서: `docs/feature-panel-size-consistency/plan.md` (패널 높이 H 통일 — 이 문서와 함께 배포, 서로 크기를 건드리지 않고 독립)

## 1. 배경 · 문제

- 현재 즐겨찾기(핀)는 **같은 목록 상단의 풀 행**(44px/개)으로 표시된다. 핀이 조금만 쌓여도 히스토리 목록이 급격히 줄어든다 — 핀 6개면 264px 소모로 목록 2행 남음, 10개면 0행.
- 과거 "즐겨찾기 탭" 구분이 있었으나 별도 탭은 탐색 비용이라 폐기 방향. **별도 탭 없이 같은 화면에 고정 표시**하되 높이 손해를 최소화하는 방식이 필요했다.
- 대안 검토 결과 (목업 v1→v2 A/B 비교로 확정):

| 옵션 | 내용 | 평가 |
|---|---|---|
| 칩 v1: 고정 폭, 3개/줄, 상한 6 | 줄바꿈 없이 최대 2줄(64px) 고정 | 칩당 ≈110px → 미리보기 6~7자만 표시(식별성 ↓). 상한 6은 임의 — 핀 7~10개 사용자에게 기능 축소 + 마이그레이션 문제. **기각** |
| **칩 v2: 1줄 가로 스크롤 + MRU, 무제한** (확정) | 높이가 **상수(≈38px)** 로 핀 개수와 무관. 자주 쓰는 칩이 항상 보이는 영역에 | 높이 보장과 정보 손실 없는 최적점. Ditto 등 실서비스에도 "사용 시 맨앞 이동" 선례 있음(§5) |

## 2. 최종 설계

### 2.1 칩 스트립 (UI)

- **위치**: 헤더 바로 아래, **목록 스크롤 영역 밖 고정** (`_showFavoritesSection` 같은 메커니즘으로 빈 때 숨김 — 기존 바인딩 재사용).
- **구성**: 가로 `ScrollViewer` + 가로 `StackPanel`. 줄바꿈 없음. **높이 1줄 상수 ≈38px** (패딩 상 8 + 칩 28 + 하 2).
- **칩**: 가변 폭 min 74 / max 152 DIP, 텍스트 ellipsis. 탭 = 붙여넣기, 항상 표시되는 ✕ = 핀 해제(항목은 히스토리 원래 자리 복귀 — `RemoveFromFavorites` 기존 동작).
- **과잉 시**: 가로 스크롤 + 끝의 반 칩 peek("더 있다" 신호) + 얇은 라벤더 스크롤바.
- **하이라이트**: 영구 선택 링 제거 → **호버 하이라이트**만 (행·칩 모두 MouseOver).

### 2.2 정렬 — MRU (최근 사용 순)

- **칩**: 핀 추가 시 맨앞(기존 `AddFavorite` Insert(0)), **패널에서 붙여넣기 시 해당 칩을 0번지로 승격** (신규 `ClipboardService.PromoteFavorite(text)` — 히스토리 `PromoteItem`과 동일한 시맨틱).
- **히스토리: 변경 없음 — 이미 MRU다.** `PasteItem → PromoteItem`(서비스 :113, 사용 시 맨앞 + "방금"), 재복사 시 `AddToHistory`가 Remove+Insert(0). 칩이 이 패턴을 따라가면 앱 전체 정렬 시맨틱이 하나로 통일된다.
- **헤더 최신순/오래순 토글은 히스토리 행에만 적용**(기존 — VM :37 주석). 칩 스트립은 항상 MRU 순서로 고정(토글 영향 없음).

### 2.3 개수 제한 제거

- `MaxFavorites = 10` 상수와 "상한 도달 시 가장 오래된 핀 밀어내기"(`AddFavorite`의 `RemoveAt(Count-1)`, 서비스 :152-153) **삭제**.
- 표시는 가로 스크롤이라 개수 무관, 저장량은 텍스트 참조 몇 줄이라 제한 불필요. ✕로 언제든 정리 가능.

### 2.4 키보드 내비게이션 — 완전 삭제

**범위: 클립보드 패널에만 존재** (스크래치패드는 텍스트 편집기라 해당 없음). **설정 스위치가 존재하지도, 추가되지도 않음** — 기능 자체가 사라지는 것이지 토글이 아니다. (`AccessibilityOptions.KeyboardNavigation`은 IME 포커스 소유자 enum이라 무관.)

| 위치 | 삭제 내용 |
|---|---|
| `ClipboardPanelWindow.xaml.cs:276-302` | `PreviewKeyDown`의 **Up/Down/Enter** case 제거. **Escape 닫기는 유지** (§8-1) |
| `ClipboardPanelWindow.xaml.cs:60, 270` | 열림 시 자동 선택(`SelectFirst`)과 `FocusList()` 제거 |
| `ClipboardViewModel.cs:41-42, 131-188, 240` | 키보드 내비게이션 섹션 전체 — `_selectedIndex`, `SelectFirst`, `MoveSelection`, `TryPasteSelected`, 텍스트 기준 선택 복원 로직 |
| `ClipboardViewModel.cs:22` + XAML 트리거 | `IsSelected` 속성 제거, DataTrigger → MouseOver 호버 트리거로 교체 |

**유지**: 붙여넣기 대상 복원 훅(`RestoreInputTarget`, VM :54)·스크래치패드 포커스 훅(`PrepareInputTarget`)은 내비게이션이 아니라 **붙여넣기 경로**라 그대로. `RequestClose`는 ✕ 버튼과 Esc가 함께 수렴하는 기존 통로라 유지.

### 2.5 붙여넣기 경로 단일화

키보드 Enter 제거 후 **마우스/터치 탭이 유일한 붙여넣기 경로** — 칩·행 모두 같은 `PasteItem(text)`를 타므로 MRU 승격이 자동 일관됨(별도 분기 없음).

## 3. 수정 파일

| 파일 | 변경 |
|---|---|
| `AltKey/Views/ClipboardPanelWindow.xaml` | 칩 스트립 UI(가로 ScrollViewer+StackPanel, 칩 템플릿 ✕ 포함), IsSelected 트리거 → MouseOver 호버, 주석 갱신 |
| `AltKey/Views/ClipboardPanelWindow.xaml.cs` | 키보드 내비게이션 삭제(§2.4), 파일 헤더 주석(:16) 갱신 |
| `AltKey/ViewModels/ClipboardViewModel.cs` | 내비게이션 섹션·`IsSelected` 제거, 칩 탭 → `PasteItem`(MRU 승격 포함) 연결, 주석 갱신 |
| `AltKey/Services/ClipboardService.cs` | `MaxFavorites`+밀어내기 삭제, `PromoteFavorite(text)` 추가(`PasteItem`에서 호출 — :113 옆) |
| `AltKey.Tests/Clipboard/ClipboardFavoritesChipsTests.cs` (신규) | §4 테스트. 기존 `ClipboardPanelWindowTargetTests.cs`는 내비게이션 참조가 없어 영향 없음(grep 확인) |

## 4. 테스트 계획 (신규 파일, 기능별 분리 규칙 준수)

1. **칩 MRU**: 즐겨찾기 항목을 붙여넣기 → `Favorites[0]`으로 이동. 이미 맨앞인 경우 순서 유지.
2. **★ 토글 시맨틱 유지**: `ToggleFavorite`(이미 핀된 항목) → 해제(기존 동작). UI상에서 재핀 경로가 없음(핀 항목은 칩, ✕만 표시)이라 서비스 API의 토글 의미를 그대로 보존 — 변경 없음. 회귀 가드 테스트로만 남김.
3. **무제한**: 11개 이상 `AddFavorite` → 전부 유지, 밀어내기 없음.
4. **핀 해제 복귀**: `RemoveFromFavorites` 후 항목이 히스토리 원래 위치(시각 기준)에 그대로 표시.
5. **내비게이션 제거**: Up/Down/Enter 전송 시 아무 동작 없음(선택 생성 안 함, 붙여넣기 안 됨). Esc는 닫기 요청(`RequestClose`) 발화 유지.

실행: `dotnet test AltKey.Tests/AltKey.Tests.csproj` 전체 통과.

## 5. 참고 조사 요약 (클립보드 매니저 8종)

| 앱 | 치수·비율 | 핀 표시 방식 |
|---|---|---|
| Win+V (Windows 내장) | 넓은·짧은 가로형(~2:1) | 같은 목록 **상단 고정 행** + 핀 아이콘, 탭 없음 |
| Maccy (macOS) | 450×800 세로형, 자유 리사이즈 | 같은 목록 **상단 이동** + 점 표식, 탭 없음 |
| CopyQ | 설정 자유 | 즐겨찾기 섹션(목록 상단), 탭 옵션 있음 |
| Xneko / 1Clipboard (Windows) | 그리드/리스트 전환 | 핀 항목 **상단 고정**, 탭 없음 |
| Ditto | 세로 패널 | **붙여넣기한 클립이 맨 위로 이동** — MRU 선례 |
| SuperV (Win+V 클론) | 가로형 | 목록 순서 유지 + 마지막 붙여넣기 ✓ 표시 |

결론: ① 즐겨찾기를 별도 탭으로 분리한 주류 앱이 없음 — 전부 같은 화면 상단 고정. 칩 스트립은 이 "상단 고정"의 **높이 상수화** 변형. ② MRU는 Ditto(사용 시 맨앞 이동)와 AltKey 히스토리 현재 동작과 일치. ③ 탭 구조는 탐색 비용만 추가하는 레거시 패턴.

## 6. 수동 검증 체크리스트 (디버그 빌드, 자동완성 ON — 사용자 직접 확인 후 커밋)

1. 핀 0개: 칩 스트립 없음, 목록 전체 높이.
2. 핀 3개: 칩 1줄(스크롤 없음), 목록 높이가 정확히 한 번만 감소(≈38px).
3. 핀 6~11개: 가로 스크롤 + 반 칩 peek, **목록 높이는 #2와 동일**. (상한 없음 — 11개도 정상)
4. 칩 탭 → 붙여넣기되고 해당 칩이 맨앞으로 이동(MRU).
5. 칩 ✕ → 핀 해제, 항목이 히스토리 원래 자리에 복귀.
6. 행 ★ → 맨앞에 칩으로 등장.
7. ↑↓/Enter: 아무 반응 없음. Esc: 패널 닫힘. 호버 시 라벤더 하이라이트.
8. 최신순/오래순 토글: 히스토리만 역전, 칩은 MRU 그대로.
9. 자동완성 켜진 상태에서 칩 탭 → Ctrl+V가 외부 앱으로 가는지(붙여넣기 대상 복원 훅 회귀).

완료 시 AGENTS.md §4에 따라 **샌드박스 번들(`dist/AltKey-sandbox`)까지 만들어 전달** (폴더 단위, zip 아님).

## 7. 검증 순서 · 커밋

1. 구현 → `dotnet test` 전체 통과.
2. 사용자 수동 검증(§6) → 통과 확인 후 커밋: `feat(clipboard): 핀을 가로 칩 스트립으로(MRU·무제한), 키보드 내비게이션 제거`.
3. 높이 통일 문서(`feature-panel-size-consistency`)는 별도 커밋 — 함께 배포하되 독립.

## 8. 가정 · 오픈 질문

1. **Esc 유지**: "완전 삭제"를 ↑↓/Enter(내비게이션)로 해석했고, Esc 닫기 단축키는 UX 표준(Win+V 등)이라 유지했다. Esc도 없애려면 `PreviewKeyDown` 전체 제거 — 말해 주시면 §2.4 표에 반영.
2. **H 값**: 높이 통일 문서의 H=400 확정 대기 중(그 문서 §8-1). 이 문서의 칩 계산은 폭 360 기준이라 H와 무관 — H 확정과 무관하게 먼저 구현 가능.
