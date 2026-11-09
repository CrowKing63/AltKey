# 04 — 상단바 단축키 편집기 추가/편집 분리 (D3)

> **소요**: 2~3시간
> **선행**: 01
> **후행**: 없음

---

## 1. 목표 한 줄

- "새 단축키 추가"와 "기존 단축키 편집"이 항상 명시적으로 분리되게 한다. 창이 이미 열려 있었는지 여부에 따라 저장 결과가 달라지는 버그를 없앤다.

---

## 2. 배경 (레포 기준)

- **버그 핵심**: `AltKey.Tools\HeaderShortcutEditorWindow.xaml.cs:120-142` — `LoadFromConfig()`에서 ID 없이 열리면(`_requestedHeaderButtonId == null && !_isCreateMode`) **첫 번째 커스텀 버튼**을 불러옴. 저장 시 `OnSave`(:147-181)가 `_editingId` 기준으로 리스트에서 찾으면 교체 → 기존 버튼이 덮어써짐.
- 허브 진입점: `AltKey.Tools\MainWindow.xaml.cs:109-118` — `createNew:false, headerButtonId:null`로 편집기 오픈. 즉 "상단바 단축키 편집기 열기" 버튼이 실제로는 첫 커스텀 버튼 편집 모드.
- 창 재사용: `AltKey.Tools\ToolWindowRegistry.cs:25-30` — 같은 도구 이름의 열린 창이 있으면 **인자를 무시하고** 기존 창만 활성화. 추가 모드 창이 열려 있는데 편집 요청이 와도(또는 그 반대) 상태가 리셋되지 않음.
- 메인 앱 진입점은 이미 명시적: `SettingsViewModel.cs:1042`(`--header-button-mode create`), `:1068`(`--header-button-id {id}`).
- 저장 후 창이 꺼지지 않고 편집 모드로 전환되는 현재 동작(:172-174)은 유지할 가치 있음(사용자가 이어 편집 가능).

---

## 3. 결정

- **편집 대상 해석을 순수 함수로 추출**:
  ```csharp
  public static (HeaderButtonConfig? target, bool isCreate, string? error) ResolveEditTarget(
      IEnumerable<HeaderButtonConfig> buttons,
      string? requestedId,
      bool createNew)
  ```
  - `createNew=true` → 항상 `CreateCustomDefault()`, `isCreate=true`. 기존 항목 절대 참조 안 함.
  - `createNew=false` + `requestedId` 있음 → 그 ID의 커스텀 버튼. 없으면 `error`(삭제됨/없음).
  - `createNew=false` + `requestedId` 없음 → **추가로 처리**(방어적 기본값). "첫 커스텀 버튼 폴백" 제거.
- 허브 버튼은 **"새 상단바 단축키 추가"**(`createNew=true`)로 바꾼다. 기존 항목 편집은 설정 탭 행의 "편집" 버튼이 담당(이미 존재).
- `ToolWindowRegistry`: `header-shortcut`만, 이미 열린 창과 새 요청의 인자가 다르면 **기존 창을 닫고 새로 생성**. (다른 편집기는 아이템 상태가 없어 재사용 유지.)
- 편집기 UI에 **모드 표시** 추가: 제목 아래 상태 행 `"추가 모드"` / `"편집 중: {이름}"`. 저장 후 추가→편집 전환 시 갱신.
- 편집 모드에서 ID를 못 찾을 때: 저장 버튼 비활성 + 안내 문구, Esc/닫기로 종료. (자동으로 추가 모드로 전환하지 않음 — 혼동 방지)

---

## 4. 작업

1. `HeaderButtonConfig.ResolveEditTarget` 정적 메서드 추가(한국어 주석: 각 분기의 이유).
2. `HeaderShortcutEditorWindow`:
   - `LoadFromConfig()`를 `ResolveEditTarget` 사용으로 교체. 폴백 로직(:125-129) 제거.
   - 에러 상태 UI: 저장 버튼 `IsEnabled=false`, 안내 TextBlock 표시.
   - 모드 상태 행 추가(바인딩 속성 `ModeStatusText`).
3. `AltKey.Tools\MainWindow.xaml(.cs)` — 허브 버튼 텍스트·HelpText 갱신, `createNew:true` 전달.
4. `ToolWindowRegistry.GetOrCreate`에 인자 비교 옵션 추가:
   - `header-shortcut` 창만 대상. 저장된 `extraArgs`와 새 `extraArgs`가 다르면 기존 창 `Close()` 후 신규 생성.
   - (기존 Closed 이벤트 정리 로직과 충돌 없게 — 레지스트리에서 먼저 제거 후 Close.)
5. 테스트: `AltKey.Tests\HeaderButton\HeaderButtonResolveTargetTests.cs`
   - createNew=true + 기존 커스텀 1개 있음 → 새 ID 반환, 기존 불변.
   - requestedId 존재 → 해당 버튼 / 부재 → error.
   - requestedId 없음 + createNew=false → 추가 모드(기존 버튼 안 불러옴).

---

## 5. 하지 말 것

- 저장 후 창 유지 동작(`OnSave`의 `_isCreateMode=false` 전환)은 바르지 않는다.
- IPC 메시지 형식(`open header-shortcut --header-button-mode create`)은 바꾸지 않는다(메인 앱 `LaunchTools`가 이미 이 형식 사용).
- 다른 편집기(레이아웃/사전/프로필/AI 프롬프트)의 창 재사용 동작은 건드리지 않는다.
- `HeaderShortcutEditorWindow`의 액션 빌더(`ActionBuilderViewModel`)는 건드리지 않는다.

---

## 6. 검증

1. 단위 테스트 그린 + 전체 빌드·테스트 그린.
2. 수동 버그 재현 절차(사용자 확인):
   - 설정 탭에서 커스텀 버튼 A 추가 → 저장.
   - 편집기 닫고, 허브 "새 상단바 단축키 추가" → 다른 이름으로 저장 → **B가 추가되고 A는 그대로** (버그 수정 확인).
   - A 편집 모드 창이 열려 있는 상태에서 설정 탭 "새 커스텀 단축키 추가" 클릭 → 기존 창이 닫히고 새 추가 모드 창이 오픈.
   - B 편집 모드로 열고 저장 → B만 바뀌고 A 불변.
3. AGENTS.md §6.8: Tools 저장 후 메인 앱·설정 창 즉시 반영(IPC 재로드) 확인.

---

## 7. 산출물

- `HeaderButtonConfig.ResolveEditTarget` + 테스트
- 편집기 모드 표시 UI, 폴백 제거
- 허브 진입점 텍스트·인자 수정
- `ToolWindowRegistry` 인자 비교 재생성 로직
