# 클립보드 패널 리디자인 + 이모지 패널 제거 계획

> 상태: **구현(코드) 완료** (2026-11-17) — U1~U4 반영. 수동 검증 피드백 반영(포커스 복원 폴백, 전체 삭제 재추가 수정, 드래그 핸들·라벨 제거). 사용자 수동 검증 대기 (§9).

## 1. 배경 (현재 동작)

| 항목 | 현재 | 문제 |
|---|---|---|
| 클립보드 패널 | `KeyboardView` 내부 오버레이(UserControl), 키보드 **위를 전체 덮음** (T-8.4) | 키보드를 가리므로 ↑↓/Enter 키보드 내비게이션 불가. 탭 2개(최근/즐겨찾기) + 4열 그리드, ★는 호버 시에만 거의 보임(Opacity 0.3) — 터치(탭만) 사용자에게 애드훅스 약함 |
| 이모지 패널 | 같은 오버레이 (T-8.3), 카테고리 탭 + WrapPanel 그리드 | 사용자 미사용. 화면 키보드에서 **Win(스틱키, `Basic.json` VK_LWIN) → .** 으로 OS 이모지 피커를 열 수 있어 기능 중복 |
| 스크래치패드 | 독립 창(`ScratchPadWindow`) + `ScratchPadPlacement`(순수 배치 함수) + MainViewModel에 라이프사이클 코드 ~100줄 | "키보드에 앵커링되는 topmost 보조 패널"이라는 검증된 패턴이지만 창 1개 전용이라 새 패널(클립보드)을 만들면 로직 중복 |
| 붙여넣기 후 동작 | 항상 닫힘 (`ClipboardViewModel.PasteItem` → `IsVisible = false`) | 선택할 수 없음 |

두 패널이 서로 다른 틀(리스트 vs 그리드, 헤더 구조, 패딩·폰트)이라 UI적으로 동떨어져 보인다는 사용자 피드백에서 시작.

## 2. 합의된 결정 (브레인스토밍 기록)

| # | 결정 | 근거 |
|---|---|---|
| D1 | **이모지 패널 완전 제거** (버튼·창·ViewModel·문서까지) | 화면 키보드로 Win(스틱키)+.을 직접 칠 수 있어 OS 이모지 피커 도달 가능 — "화면 키보드 전용 사용자에게 Win+.이 안 된다"는 반대 논리 무력화 (사용자 결정) |
| D2 | 클립보드 패널 → **독립 top-level 창** (`ScratchPadWindow` 패턴). 키보드 오버레이 아님 | 키보드 내비게이션을 쓰려면 키가 계속 보여야·탭 가능해야 함. 스크래치패드가 이미 이 차원의 외부 확장(D3-scratchpad)으로 검증됨 |
| D3 | **액션 항상 표시** — ★(고정)·삭제 버튼을 호버 없이 항상 보임, 터치 타깃 ≥32px. 호버는 색 피드백만 | 사용자가 드래그 없이 탭만으로 제어 (사용자 명시). 기존 0.3 투명도 호버 리빌은 제거 |
| D4 | 리스트: **단일 컬럼 최신순**, 상단 "고정" 섹션(★ 라벤더 항상 표시, 비어 있으면 섹션 숨김), 검색 입력, 헤더 "전체 삭제"(히스토리만 — 즐겨찾기 보존, Win+V 시맨틱), 빈 상태 안내 | 클립보드 매니저 공통 패턴(Win+V·Maccy·CopyClip) + 첫 라운드 디자인 합의 |
| D5 | **키보드 내비게이션**: ↑↓ 선택(랩어라운드, 라벤더 하이라이트), Enter = 선택 항목 붙여넣기, Esc = 닫기. 열기 시 Activate()로 포커스 | Maccy/Win+V 패턴 + 접근성 우선 정체성. 물리 키보드가 없으면 탭 사용 — 충돌 없음 |
| D6 | 설정 "붙여넣기 후 패널 닫기" — **기본값: 닫기** (현재 동작 유지). OFF면 열려 있음(선택 유지), Esc/✕로 닫기 | 사용자가 선택 가능하게, 기본은 닫기로 지정 |
| D7 | 배치 엔진(순수 함수): 1개 창 = 현행 규칙(키보드 상/하반 → 반대편 + 수평 중앙). **2개 이상 = 한 줄 나란히, 총 너비 기준 키보드 중심선 수평 중앙**, top 정렬. 좌우 순서 고정: **클립보드 좌 / 스크래치패드 우**. 총 너비가 작업 영역에 안 들면 위/아래 1행씩 분리 폴백. **멀티모니터 수정**: `SystemParameters.WorkArea`(기본 모니터) 대신 키보드 창이 있는 모니터의 작업 영역 사용 | 두 창이 각자 현행 규칙을 적용하면 동일 좌표에 겹침 → 조정 필수 (사용자 제안: 옆으로 + 총 너비 중앙). 예측 가능성 원칙("같은 키보드 위치면 항상 같은 자리") |
| D8 | **호스트 서비스 추출** (`AnchoredPanelService`): 창 생성/재사용, Show/Hide(✕=숨김), Topmost(`WindowService.SetTopmost`), 트레이 숨김/복원 동기화, `OpacityStateApplied` 투명도 연동, 재배치 트리거(패널 열림/닫힘·키보드 이동/리사이즈)를 1곳에서 소유. 스크래치패드를 이 서비스로 **마이그레이션** (MainViewModel ~100줄 이동). 새 패널은 {id, 크기, 순서} 메타데이터 등록 | 사용자의 재사용 요구 — 향후 AI 패널류 팝업도 같은 체계. 설정 창·Tools는 키보드 앵커링 대상이 아니므로 이 체계에 **포함 안 함** (장수명 작업 창 — Topmost/앵커링/자동숨김 불필요) |
| D9 | 드래그 = v1에서는 현행 유지: 다음 재계산 시 계산 위치로 복귀(영속화 없음). "드래그 후 free-floating"은 향후 옵션으로 오픈 | 스크래치패드现行 동작과 일치 + 터치 중심 사용자에게 드래그 위치 의존성 낮음. 과한 설계 방지 (D9-scratchpad 제거 결정과 같은 취지) |

## 3. U1: 이모지 패널 제거

- `EmojiPanel.xaml(.cs)`, `EmojiViewModel` 삭제. `KeyboardView.xaml`의 T-8.3 블록 제거.
- `MainViewModel`: `ToggleEmojiPanel`, `Emoji` 속성, DI 인자·할당(44/218/239), `Emoji.IsVisible = false`(303), 헤더 버튼 분기(879)·툴팁 분기(954) 제거.
- `App.xaml.cs`: `services.AddSingleton<EmojiViewModel>()`(148) 제거.
- **마이그레이션**: 기존 설치 config에 이모지 버튼이 저장되어 있음 → `HeaderButtonConfig.EnsureAllBuiltIns`에서 `Id == "Emoji"`인 BuiltIn 항목을 **삭제**하는 스텝 추가(멱등). 커스텀 버튼은 절대 건드리지 않음(기존 규칙 유지).
  - 버전 스터크: 새 메인 앱보다 오래된 AltKey.Tools 편집기를 열면 구버전 `EnsureAllBuiltIns`가 이모지 버튼을 재추가할 수 있음 → 다음 메인 앱 시작 시 다시 제거됨. 허용 (주석으로 기록).
- `HeaderButtonConfig`: `IdEmoji` 상수·`IsBuiltInId`·이름/아이콘(😊)/툴팁/접근성 이름 switch·`CreateDefaults()` 항목 제거.
- 문서: Wiki `이모지.md` 삭제 + `Home.md`·`소개.md`·`화상키보드-기본.md` 링크 제거, 웹사이트 `docs/index.html` 이모지 언급 제거 (altkey-wiki 스킬 절차대로).

## 4. U2: 앵커링 패널 배치 엔진 + 호스트 서비스 추출

### 4.1 `AltKey/Services/AnchoredPanelsLayout.cs` (순수 함수, WPF 무의존)

```
PlacePanels(keyboardRect, workArea, panels: [{id, width, height}, ...]) → {id: (left, top)}
  // panels 순서 = 고정 등록 순서(클립보드, 스크래치패드). 열려 있고 자동 레이아웃 대상인 패널만.
  if panels.Count == 1:
    → 현행 PlacePad 규칙 그대로 (상/하반 판단 + 수평 중앙 + Gap=4 + 클램프)
  else:
    rowSide = keyboardRect.Top < workArea.Top + workArea.Height / 2 ? below : above   // 첫 패널 기준, 현행 규칙과 동일
    totalWidth = Σ width + Gap*(n-1)
    if totalWidth <= workArea.Width:
      left0 = clamp(keyboardRect.Left + (keyboardRect.Width - totalWidth)/2, ...)
      → 나란히 배치, top 정렬(rowSide 기준), 각 창 좌표 누적
    else:
      → 폴백: 1번 패널은 rowSide에, 나머지 패널은 반대편에 각각 단독 중앙(현행 규칙) + 클램프
```

- `ScratchPadPlacement` 삭제 — 1개 패널 케이스가 엔진의 특수케이스. 기존 `ScratchPadPlacementTests`는 엔진 테스트로 이관(1개 창 결과는 **완전 동일**해야 함).
- workArea 인자는 호출부가 "키보드 창이 있는 모니터" 기준으로 넘김 (멀티모니터 수정 — 현행 `SystemParameters.WorkArea` 한계 해소).

### 4.2 `AltKey/Services/AnchoredPanelService.cs` (호스트)

- 레지스트리: `{ id → (Window, width, height, order) }`. 패널은 메타데이터만 등록.
- 호스트 연결(기존 `AttachScratchPadHost` 일반화): MainWindow의 `LocationChanged`/`SizeChanged`/`IsVisibleChanged` + `OpacityStateApplied` 구독. **모든** 레이아웃 이벤트(패널 Show/Hide, 키보드 이동/리사이즈) 시 엔진으로 전체 재계산 → 각 창에 적용.
- 라이프사이클: 지연 생성, `Owner=host`(작업표시줄 미표시·종료 시 자동정리), Topmost(`SetTopmost` — NOACTIVATE 아님), Show/Hide(✕=숨김), 트레이 숨김 시 전부 숨김·복원 시 되돌림.
- 투명도: `OpacityStateApplied(target, durationMs)` 이벤트를 구독해 열린 패널 전부 동기 페이드 + 열기 시 호스트 현재 Opacity로 시작 (기존 스크래치패드 코드 그대로 일반화).
- 포커스 정책은 패널 창 자체에서 처리(스크래치패드는 TextBox, 클립보드는 리스트) — 서비스는 `Activate()`까지만 담당.
- **마이그레이션**: `MainViewModel`의 `_scratchPadWindow` 필드·`AttachScratchPadHost`·`Show/HideScratchPad`·`OnHostOpacityStateApplied`·`ApplyComputedPadPosition`(538~662줄)을 서비스로 이동. MainViewModel은 `ToggleScratchPadCommand`만 남김. `ScratchPadWindow.xaml.cs`는 드래그 핸들·숏컷 버튼·FocusTracker 등록(창 자체 책임)만 유지.

## 5. U3: 클립보드 패널 → 독립 창 + 새 디자인

### 5.1 윈도우 스펙 — `AltKey/Views/ClipboardPanelWindow.xaml(.cs)`

- `ScratchPadWindow`와 같은 차원: top-level, `Owner=MainWindow`, Topmost, `ShowInTaskbar=False`, 고정 크기 **360×420** (리사이즈 없음 — 단순성·예측 가능성).
- 내용(상→하):
  1. 헤더 행: **좌측 드래그 핸들** (SizeAll 커서 — 누르면 창 이동, 더블클릭 = 화면 수평 중앙; 빠른 메모와 동일) | 우측 [전체 삭제] [✕]. 제목 라벨 없음 (빠른 메모와 정합 — 사용자 요청)
  2. 검색 입력 (한 줄, 얇은 테두리, 비어 있으면 placeholder "검색". 텍스트가 있으면 ✕ 지우기 버튼 표시)
  3. "고정" 섹션: 즐겨찾기 항목들 — ★ **항상 라벤더(#C4B5FD) 채움** 표시. 목록 비어 있으면 섹션 자체 숨김
  4. "최근" 리스트: 단일 컬럼, 최신순. 행(높이 ~44px) = `[미리보기 13px, 1줄 ellipsis] [상대 시간 11px 뭉뜩] [★ 32px] [✕삭제 32px]` — **액션 항상 표시** (D3). 선택 행은 라벤더 틴 배경
- 빈 상태: 리스트 비어 있으면 "아직 복사한 항목이 없습니다" + "Ctrl+C로 복사를 시작하세요" (검색 결과 없으면 "맞는 항목이 없습니다")
- 포커스: 열기 시 `Activate()` — 키보드 내비게이션(↑↓·Enter·Esc, D5)을 위해 패널이 포커스를 갖는다. **마우스도 동일하게 동작**: 행 클릭 = 그 항목 붙여넣기, 목록 위 휠 = 스크롤(ScrollViewer 기본), ★/✕ 버튼 클릭 — 액션이 항상 표시되니 호버 불필요 (D3).
- **붙여넣기 대상 복원**: 패널이 포커스를 갖고 있으면 클릭/Enter로 보내는 Ctrl+V가 AltKey 자체로 가므로, 전송 전 포그라운드를 열기 직전 외부 앱으로 되돌린다. `ClipboardPanelWindow.CapturePreviousForeground()`(Show 전) → `RestoreInputForeground()`(PasteItem·숨김 시). 포커스가 이미 다른 창/앱에 있으면 스킵 — 스크래치패드가 대상인 경우(`PrepareInputTarget`)와 충돌 없음.
  - **자기 프로세스 폴백**: 화상 키보드 상단 버튼·Win+. 등으로 열면 그 순간 포그라운드가 AltKey 창 자체가 되어 복원 대상이 잘못 잡힌다 → `CapturePreviousForeground`가 자기 프로세스 창을 감지하면 `ProfileService.LastExternalForegroundHwnd`(마지막 외부 앱)로 대체. 대상 핸들이 이미 무효(앱 종료)면 복원을 건너뜀.
  - 행 ★/✕·붙여넣기 버튼은 ViewModel 커맨드(`PasteItemCommand` 등, `CommandParameter=FullText`)에 바인딩 — 코드비핸드 없이 MVVM 일관.
- ✕/닫기: 숨김(종료 아님) — 서비스 Show/Hide 패턴.

### 5.2 ViewModel/Service 변경

- `ClipboardViewModel`: 탭(`IsFavoritesTab`·SwitchTo*TabCommand) **제거** — 탭 대신 섹션 동시 표시. 추가: `SearchText`(필터, 대소문자 무시 부분합치), `SelectedIndex`(↑↓), `DeleteItemCommand`, 검색 결과 비어 감지. 기존 `PrepareInputTarget` 훅(스크래치패드 포그라운드 확보) **유지** — 창이 바뀌어도 Ctrl+V 전 대상 확보 로직은 동일하게 필요.
- `ClipboardService`:
  - `RemoveFromHistory(text)` 신규 (행 삭제). 주의: 삭제된 텍스트가 현재 윈도우 클립보드에 남아 있으면 재복사 시 다시 추가됨 — 허용, 주석 기록.
  - **타임스탬프**: 히스토리 항목에 `copiedAt`(ISO) 저장. 기존 JSON(없음) → null → 시간 표시 생략 (마이그레이션 안전). 포맷: "방금"(<1분) / "N분 전" / "N시간 전" / "M월 d일".
  - `ClearHistory()`는 그대로 = **히스토리만, 즐겨찾기 보존** (D4 — Win+V 시맨틱). 헤더 "전체 삭제"는 이것을 연결. **삭제 후 `_lastClipboard`를 현재 클립보드 텍스트로 설정** — null이면 500ms 폴링이 현재 내용을 새 항목으로 보고 재추가해 "삭제됐다가 다시 생김" 회귀가 나므로.
- `ClipboardItem` record: `(FullText, Preview, IsFavorite)` → `copiedAt` 추가 (nullable datetime).

### 5.3 MainViewModel 연결

- `ToggleClipboardPanel`: 오버레이 `IsVisible` 토글 → **서비스를 통한 창 토글** (`ToggleScratchPad`와 동일한 구조 — 지연 생성·Show/Hide).
- `KeyboardView.xaml` T-8.4 블록(클립보드 오버레이) 제거.

## 6. U4: 설정 "붙여넣기 후 패널 닫기" (D6)

- `AppConfig`: `ClipboardCloseAfterPaste` (bool, **기본 true** = 현재 동작). STJ는 누락 필드를 기본값으로 채므로 기존 config 마이그레이션 안전.
- UI: `SettingsWindow.xaml` → **동작 탭 → "닫기/숨김" 섹션**에 토글 1줄 ("붙여넣기 후 클립보드 패널 닫기"). `SettingsViewModel`에 바인딩 + 툴팁.
- 동작: true = Enter/탭 붙여넣기 직후 창 닫힘(현행). false = 열려 있음, 선택은 유지 — 다음 ↑↓/탭으로 추가 붙여넣기 가능, Esc/✕로 닫기.

## 7. 수정 파일 목록

**신규**
- `AltKey/Services/AnchoredPanelsLayout.cs` (순수 배치 엔진)
- `AltKey/Services/AnchoredPanelService.cs` (호스트 서비스)
- `AltKey/Views/ClipboardPanelWindow.xaml` / `.xaml.cs`
- `AltKey.Tests/AnchoredPanels/AnchoredPanelsLayoutTests.cs`
- `AltKey.Tests/Clipboard/ClipboardServiceTests.cs`

**삭제**
- `AltKey/Views/EmojiPanel.xaml` / `.xaml.cs`, `AltKey/ViewModels/EmojiViewModel.cs`
- `AltKey/Views/ClipboardPanel.xaml` / `.xaml.cs` (창으로 대체)
- `AltKey/Views/ScratchPadPlacement.cs` (엔진 흡수)

**수정**
- `AltKey/Views/KeyboardView.xaml` — T-8.3·T-8.4 오버레이 블록 제거
- `AltKey/ViewModels/MainViewModel.cs` — 이모지 참조 전부 제거, 클립보드 토글을 창 기반으로, 스크래치패드 라이프사이클(538~662)을 서비스로 이동
- `AltKey/App.xaml.cs` — -`EmojiViewModel`, +`AnchoredPanelService`
- `AltKey/Models/HeaderButtonConfig.cs` — IdEmoji 제거 + EnsureAllBuiltIns에서 Emoji 항목 삭제 마이그레이션
- `AltKey/Services/ClipboardService.cs` — RemoveFromHistory, copiedAt 타임스탬프 + JSON 마이그레이션
- `AltKey/ViewModels/ClipboardViewModel.cs` — 탭 제거, 검색·선택·삭제·타임스탬프 포맷, closeAfterPaste 동작
- `AltKey/Models/AppConfig.cs`, `AltKey/Views/SettingsWindow.xaml`, `AltKey/ViewModels/SettingsViewModel.cs` — ClipboardCloseAfterPaste
- `AltKey/Views/ScratchPadWindow.xaml.cs` — 위치 적용 코드 서비스로 이동(드래그 핸들·숏컷은 유지)

**테스트**
- `AltKey.Tests/HeaderButton/HeaderButtonEnsureBuiltInsTests.cs` — Emoji 제거 마이그레이션 케이스 추가 (기존 3가지 동작 — 누락 추가/설정 앞 재배치/커스텀 불변 — 회귀 포함)
- `AltKey.Tests/ScratchPad/ScratchPadPlacementTests.cs` — 엔진 테스트로 이관 (1개 창 결과 동일 검증)

**문서**
- Wiki: `이모지.md` 삭제, `Home.md`·`소개.md`·`화상키보드-기본.md` 링크 제거, `클립보드-히스토리.md` 전면 갱신 (독립 창·검색·키보드 내비게이션·설정)
- 웹사이트 `docs/index.html`: 이모지 제거 + 클립보드 설명 갱신

## 8. 테스트 계획

1. **배치 엔진**(순수 함수 — 단위 테스트):
   - 1개 패널: 현행 `PlacePad` 출력과 **완전 일치** (이관 회귀)
   - 2개 패널: 나란히 + 총 너비 기준 중앙 + 고정 순서(클립보드 좌) + top 정렬
   - 총 너비 > 작업 영역 → 위/아래 분리 폴백
   - 작업 영역 경계 클램프, 키보드 상/하반 경계값(Top == 중심선)
   - workArea 인자 독립성 (멀티모니터: 호출부 전달 값 사용)
2. **마이그레이션**: 기존 config(Emoji BuiltIn 항목 포함) → EnsureAllBuiltIns 후 Emoji 사라짐, 커스텀 버튼 순서 불변, 멱등(두 번 호출해도 동일)
3. **ClipboardService**: RemoveFromHistory, copiedAt 저장/로드, 구형 JSON(타임스탬프 없음) → null로 로드, ClearHistory는 즐겨찾기 보존
4. 기존 전체 스위트 회귀: `dotnet test` (스크래치패드·헤더버튼 테스트 포커스 확인)

## 9. 수동 검증 체크리스트 (사용자 — 디버그 빌드, 자동 완성 켜고)

1. **이모지 제거**: 상단바에 이모지(😊) 버튼 없음. 업그레이드 전 설정 파일이던 설치에서도 시작 후 버튼 사라짐
2. **클립보드 창 기본**: 📋 버튼 → 키보드 **위/아래** 독립 창, 키보드가 전부 보이고 탭 가능. 패널 열려 있는 채로 키보드 키를 타도 정상 입력
3. **붙여넣기**: 항목 탭/Enter → 대상 앱에 붙여넣기 + 기본값(닫기)대로 닫힘. 설정에서 OFF → 열림 유지, Esc/✕로 닫기
4. **키보드 내비게이션**: (물리 키보드) ↑↓ 선택 이동 + Enter 붙여넣기 + Esc 닫기
5. **검색**: 타이핑 시 고정+최근 동시 필터, 결과 없음 문구, ✕ 지우기
6. **고정/삭제**: ★ 항상 보임(호버 불필요), "전체 삭제" 후 고정 항목만 남음, 행 ✕ 삭제 동작
7. **두 창 나란히**: 빠른 메모 + 클립보드 동시 열림 → 옆으로 나란히·총 너비 기준 중앙. 하나 닫으면 남은 쪽 재중앙. 키보드 창 이동 시 둘 다 재배치
8. **투명도 연동**: 유휴 페이드가 두 창에 동시에 적용, 어느 창에 호버해도 타이머 리셋
9. **트레이 숨김/복원**: 키보드 트레이로 숨기면 패널도 함께 숨김, 복원 시 되돌아옴
10. (멀티모니터 환경이면) 키보드 창이 있는 모니터 기준으로 배치
11. **포커스 복원(키보드 버튼으로 열기)**: 화상 키보드 상단 📋 버튼·Win+.로 클립보드를 열면, 항목 선택 시 붙여넣기가 **외부 입력창**에 들어가야 함 (AltKey 검색 박스·키보드가 아님). 닫고(Esc/✕) 나간 뒤 포커스가 원래 입력창에 돌아감
12. **"전체 삭제" 재추가 없음**: "전체 삭제"를 누르면 고정 항목만 남고, 몇 초 안에 지운 항목이 다시 생기지 않음
13. **행 ★/✕ 탭 동작**: ☆→★ 탭 = 고정(고정 섹션으로 이동), ✕ 탭 = 행 삭제 — 호버 없이 항상 보임
14. **드래그 핸들**: 헤더 왼쪽을 누르면 창 이동, 더블클릭 = 화면 수평 중앙 (빠른 메모와 동일)

## 10. 가정·오픈 질문

| # | 항목 | 내용 |
|---|---|---|
| O1 | 타임스탬프(D4) 포함 | 기존 제안에 따라 포함. 원치 않으면 U3의 copiedAt 관련만 잘라도 자립적 (JSON 마이그레이션·행 우측 시간 라벨 제거) |
| O2 | 행 삭제 버튼 신규 | 현재 서비스/UX에 없음. Win+V(…메뉴)와 달리 항상 보이는 별도 버튼으로 — D3(항상 표시) 원칙의 직접 파생. 재복사 시 재추가되는 동작은 허용 |
| O3 | 창 크기 360×420 고정 | 추정치 — 구현 시 실제 가독성 확인 후 미세 조정 (스크래치패드처럼 리사이즈 없음) |
| O4 | free-floating 미구현(D9) | 드래그 후 위치가 다음 재계산 시 복귀(현행 유지). 사용자 피드백이 있으면 순수 함수에 "detached" 플래그만 추가하면 되는 확장 지점 |
| O5 | 설정 탭 배치 | **해결** — "동작 → 닫기/숨김" 섹션에 존재 ("클립보드 항목을 클릭/Enter로 붙여넣은 후 클립보드 패널을 자동으로 닫습니다") |
