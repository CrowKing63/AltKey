# 레이아웃 프리셋 보호 (Preset Protection)

## 배경

기본 제공되는 두 레이아웃(`Basic`, `Basic Plus`)은 **프리셋**이다. 설치기가 업그레이드마다 이들을 갱신할 수 있어야
앱에 포함된 레이아웃 수정(예: Tab 키가 `VK_ESCAPE`로 잘못 매핑되어 있던 회귀 버그, 1.1.8에서 수정)이
모든 사용자에게 전달된다.

현재 설치기는 `%AppData%\AltKey\layouts\`의 기존 파일을 `onlyifdoesntexist`로 덮어쓰지 않는다.
이는 사용자가 레이아웃을 편집한 것을 보호하려는 의도였지만, 대가로 **프리셋에 대한 수정이 기존 사용자에게
영원히 반영되지 않는** 문제가 생겼다.

해결 방향: 프리셋은 더 이상 사용자가 편집/삭제할 수 없게 만든다. 그러면 설치기가 덮어써도
사용자 데이터 손실이 없으므로 `overwrite`로 바꾸는 것이 안전해진다.

## 설계 결정

| 항목 | 결정 | 이유 |
|---|---|---|
| 프리셋 식별 | 이름 고정 집합 `{"Basic", "Basic Plus"}` (대소문자 무시) | JSON에 마커 필드를 추가할 필요 없음. 기존 Bagic→Basic 이름 마이그레이션(`ConfigService`)과 같은 맥락 |
| 편집기 저장 | 프리셋 이름을 그대로 둔 채 `저장` → 거부 | 덮어쓰기를 원천 차단 |
| 편집기 다른이름저장 | 새 이름 필수, 프리셋 이름 사용 불가 | "편집하면 무조건 다른 이름으로" 유도. 기존 레이아웃과 이름 충돌도 방지 |
| 편집기 삭제 | 프리셋은 삭제 불가 | 지워도 다음 설치에 다시 생기면 혼란만 커짐. 기존 `DefaultLayoutName` 삭제 차단과 같은 패턴 확장 |
| 설치기 | `onlyifdoesntexist` → `overwrite` (프리셋 두 파일에만 적용) | portable 패키징(`package-portable.ps1`)이 이미 `-Force`를 쓰는 것과 일관성 확보 |
| 사용자 레이아웃 | 영향 없음 | `My.json` 등 사용자 파일은 소스 디렉터리에 없어 설치 대상이 아님 |

## 변경 사항

### 1. `LayoutService` (AltKey/Services/LayoutService.cs)

- `BuiltinNames`: 대소문자 무시 `HashSet<string>` — `"Basic"`, `"Basic Plus"`
- `IsBuiltin(string name)`: static, 이름이 프리셋 집합에 포함되는지 판정
- `Delete(name)`: `IsBuiltin`이면 `false` 반환 (파일은 그대로 유지)

### 2. `ILayoutRepository` / `LayoutRepository` (AltKey/Services/)

- 인터페이스에 `bool IsBuiltin(string name)` 추가
- 어댑터는 `LayoutService.IsBuiltin` 위임

### 3. `LayoutEditorViewModel` (AltKey/ViewModels/LayoutEditorViewModel.cs)

- `IsEditingBuiltin`: 현재 파일명이 프리셋인지 (`CurrentFileName` 변경·목록 새로고침 시 notify)
- `Save()`: `IsEditingBuiltin`이면 저장 거부 + 상태메시지
  - "프리셋 레이아웃은 덮어쓸 수 없습니다. 새 이름을 입력한 뒤 '다른 이름으로 저장'을 눌러 주세요."
- `SaveAs()`: 이름이 비어있거나 프리셋 이름이면 거부
  - "'Basic', 'Basic Plus'는 프리셋 이름이라 사용할 수 없습니다. 다른 이름을 입력하세요."
- `CanDeleteLayout`: 기존 `DefaultLayoutName` 차단 조건에 `IsBuiltin` 조건 추가

### 4. `LayoutEditorWindow.xaml` (AltKey/Views/)

- 툴바 힌트 영역에 프리셋 안내 TextBlock 추가 (`IsEditingBuiltin` 시 표시)
- 프리셋 안내가 표시되면 기존 "현재 레이아웃 편집 중" 힌트는 숨김 (중복 방지)
  - "프리셋 레이아웃입니다. 덮어쓸 수 없고 삭제할 수 없습니다. 수정하려면 새 이름을 입력해 '다른 이름으로 저장'하세요."

### 5. `installer/AltKey.iss`

- 레이아웃 `[Files]` 라인: `onlyifdoesntexist` → `overwrite`
- 주석 갱신 (프리셋은 덮어쓰기, 사용자 레이아웃은 소스에 없어 영향 없음)

## 테스트

`AltKey.Tests/LayoutEditorViewModelTests.cs`:

- `Save_on_builtin_layout_is_rejected` — "Basic" 로드 후 Save → 저장 안 됨
- `SaveAs_with_builtin_name_is_rejected` — 이름 변경 없이 SaveAs → 저장 안 됨
- `SaveAs_with_new_name_saves_copy` — 새 이름으로 SaveAs → 새 파일 저장, 원본 유지
- `CanDeleteLayout_is_false_for_builtin` — "Basic" 로드 시 삭제 불가

`AltKey.Tests/LayoutServiceTests.cs` (신규):

- `Delete_rejects_builtin_layout` — 실제 디렉토리에 "Basic.json" 생성 후 Delete → false, 파일 잔존
- `IsBuiltin_case_insensitive` — "basic", "BASIC PLUS" 등 변형 판정

## 검증 체크리스트

- [ ] `dotnet build` 성공
- [ ] `dotnet test` — 신규 테스트 포함 전체 통과
- [ ] 수동: 레이아웃 편집기에서 Basic 열기 → 저장 버튼 눌림 → 거부 메시지, 삭제 버튼 비활성
- [ ] 수동: Basic 수정 후 "My" 이름으로 다른이름저장 → 성공, 원본 Basic은 그대로
- [ ] 다음 설치(1.1.9) 시 Basic/Basic Plus가 갱신되고 사용자 레이아웃은 유지

## 롤백

코드 변경만 되돌리면 됨. 설치기 플래그는 다음 빌드까지 영향 없음 (설치기는 릴리스 시에만 실행).
이미 배포된 1.1.8 설치기의 `onlyifdoesntexist` 동작은 그대로라, 이 기능의 효과는 **플래그가 포함된 첫 릴리스**부터 발생한다.
