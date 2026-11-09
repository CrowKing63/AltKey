# 03 — 좌우 배치 모델 일관성 (D2)

> **소요**: 3~4시간
> **선행**: 01
> **후행**: 없음 (02·04와 독립)

---

## 1. 목표 한 줄

- "버튼이 어느 쪽에, 그쪽에서 몇 번째"가 설정 탭 화면과 실제 상단바 표시와 완전히 일치하게 한다. ▲▼ 이동이 표시 순서와 어긋나는 현상을 없앤다.

---

## 2. 배경 (레포 기준)

- `AltKey\ViewModels\MainViewModel.cs:654-674` — 표시 순서 = `HeaderButtons` 리스트 순서를 유지하면서 `Position`으로 좌/우 컬렉션 분리. (즉, **쪽별 순서**가 이미 실제 표시 로직.)
- `AltKey\ViewModels\SettingsViewModel.cs:992-1013` — ▲▼는 **합쳐진 리스트** 인덱스 기준 이동. 좌측 버튼이 우측 버튼 사이를 지날 때 목록만 바뀌고 표시는 그대로 → "눌렀는데 아무것도 안 바뀜".
- `AltKey\Views\SettingsWindow.xaml:184-334` — 상단바 탭은 "기본 상단바 버튼" 섹션 → "사용자 커스텀 단축키" 섹션의 2분할. 표시 순서는 기본/커스텀이 섞여도 화면은 두 섹션에 나뉘어 있어 실제 배치와 안 맞음.
- `AltKey\Tools\HeaderShortcutEditorWindow.xaml.cs:32` — 편집기 위치控件은 ComboBox(`["Left","Right"]`), 설정 탭(:225-241)은 토글 버튼(`◄ 우측 ►`)으로 **같은 개념 두 가지 UI**.
- 기본값: `HeaderButtonConfig.CreateDefaults()`(Models/HeaderButtonConfig.cs:136-145) 전부 `Right`.

---

## 3. 결정

- **D2 유지**: 표시 순서 = 쪽별 리스트 순서(표시 로직 변경 없음).
- ▲▼는 **같은 쪽 안**에서만 이동. 그쪽 버튼이 하나면 no-op.
- 위치 토글 시 버튼은 **반대쪽 끝순**으로 붙음.
- 설정 탭 구조: "기본/커스텀" 2분할을 **"좌측 상단바" / "우측 상단바" 2그룹**으로 바꾼다(권안 A). 각 그룹에 현재 그쪽에 배치된 버튼(기본+커스텀 섞임)을 실제 표시 순서로 나열. 행에는 `기본`/`커스텀` 배지만 남긴다.
  - (대안 B: 2분할 유지 + ▲▼만 쪽별화 + 안내 문구 추가. 변경량 작지만 섹션 구조와 표시 순서의 괴리는 남음.)
- 편집기 위치控件을 설정 탭과 같은 토글 버튼 스타일로 통일(ComboBox 제거).
- 기본 배치값(전부 Right)은 **변경하지 않음** — 기존 설치 사용자 레이아웃 보존. 좌측 기본값 도입은 향후.

---

## 4. 작업

1. `HeaderButtonConfig`에 순수 정적 메서드 추가:
   - `MoveWithinSide(List<HeaderButtonConfig> buttons, string id, bool up)` → 같은 쪽 안에서 한 칸 이동한 새 리스트 반환(합쳐진 리스트 인덱스 기준 swaps).
   - `MoveToOtherSideEnd(List<HeaderButtonConfig> buttons, string id)` → 반대쪽 끝순으로 옮긴 새 리스트 반환.
   - 한국어 주석: "표시 순서는 쪽별로 필터링되므로 이동도 쪽별 기준으로만 의미 있음" 설명.
2. `SettingsViewModel`:
   - `MoveHeaderButtonUp/Down`을 위 메서드로 교체.
   - `ToggleHeaderButtonPosition`을 `MoveToOtherSideEnd`로 교체.
   - 바인딩용 새 속성: `LeftHeaderButtons` / `RightHeaderButtons`(각 그룹 표시용, `RaiseHeaderButtonCollectionChanged`에 추가).
3. `SettingsWindow.xaml` 상단바 탭 재구성(권안 A):
   - "좌측 상단바" / "우측 상단바" 섹션 제목 + 각 그룹 ItemsControl.
   - 행 템플릿: 기존 체크박스·아이콘·이름·위치 토글·▲▼ 유지, 커스텀 행의 편집/복제/삭제 버튼 유지, `기본`/`커스텀` 배지 TextBlock 추가.
   - 안내 문구 갱신: "버튼은 좌측/우측에 독립적으로 배치되며, 표시 순서는 각 쪽 안에서만 정렬됩니다."
4. `HeaderShortcutEditorWindow.xaml(.cs)`: ComboBox → 토글 버튼(설정 탭과 동일한 스타일·문구).
5. 접근성: 그룹 제목 `AutomationProperties.Name`, 배지 낭독 포함(`"기본 버튼"`/`"커스텀 단축키"`).

---

## 5. 하지 말 것

- `MainViewModel.RebuildHeaderButtons`의 표시 로직(쪽별 필터)은 바꾸지 않는다.
- 기본 배치값(`CreateDefaults` 전부 Right)을 바꾸지 않는다.
- 드래그 앤 드롭 재배열은 추가하지 않는다(향후).
- 커스텀 버튼 개수 한도·좌우 표시 한도 규칙은 01의 통합 메서드에 맡기고 여기에서 다시 구현하지 않는다.

---

## 6. 검증

1. `HeaderButtonConfig.MoveWithinSide` / `MoveToOtherSideEnd` 단위 테스트:
   - 좌측 2개 + 우측 3개 혼합 리스트에서 좌측 버튼 ▲▼ → 표시 순서만 바뀌고 우측 불변.
   - 그쪽 버튼 하나면 no-op.
   - 위치 토글 → 반대쪽 끝순 배치, 원위치 토글 시 복귀.
2. 빌드 그린 + 전체 테스트 그린.
3. 수동(사용자 확인):
   - 상단바 탭 좌측 그룹에 1개, 우측 그룹에 6개 배치 → 실제 상단바와 순서 일치.
   - ▲▼ 눌렀을 때 "아무것도 안 바뀜" 현상 없음(같은 쪽 안에서만 반응).
   - 위치 토글 시 버튼이 반대쪽 끝으로 이동.

---

## 7. 산출물

- `HeaderButtonConfig` 순수 이동 메서드 2개 + 테스트
- 설정 탭 좌/우 그룹 UI
- 편집기 위치控件 통일
