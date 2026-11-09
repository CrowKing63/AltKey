# 01 — 상단바 버튼 한도 검증 통합

> **소요**: 1~2시간
> **선행**: 없음
> **후행**: 02, 03, 04

---

## 1. 목표 한 줄

- 커스텀 개수·좌우 표시 개수 한도 검증을 `HeaderButtonConfig`의 정적 메서드 하나로 통합하고, 메인 앱과 Tools가 같은 규칙을 쓰게 한다.

---

## 2. 배경 (레포 기준)

- `AltKey\ViewModels\SettingsViewModel.cs:929-972` — `TryValidateHeaderButtonLayout()`이 좌우 표시 개수만 검사(커스텀 총개수는 `EnsureCanAddCustomHeaderButton()`:978에서 별도).
- `AltKey.Tools\HeaderShortcutEditorWindow.xaml.cs:187-220` — `ValidateHeaderButtonLimits()`이 커스텀 총개수 + 좌우 표시 개수를 검사. MessageBox를 직접 호출.
- 두 구현은 같은 상수(`HeaderButtonConfig.MaxCustomButtonCount`, `MaxVisibleButtonsLeft/Right`)를 쓰지만 **메시지 문구와 검사 순서, 반환 형태가 다름**.
- `AltKey.Tests`에 `HeaderButtonConfig` 관련 테스트가 없음(검색 확인).

---

## 3. 결정

- 검증 로직은 `HeaderButtonConfig`의 정적 메서드로 이동:
  ```csharp
  public static (bool ok, string? message) TryValidateSave(
      IReadOnlyCollection<HeaderButtonConfig> nextButtons,
      HeaderButtonConfig savedButton,
      bool isEditingExisting)
  ```
  - 커스텀 총개수 한도 → `message`에 "커스텀 상단바 단축키는 최대 N개까지…"
  - 좌우 표시 개수 한도 → `message`에 "상단바 {좌측|우측}에는 최대 N개까지… 위치를 바꾸거나 표시를 끄고 다시 저장해 주세요."
  - `savedButton.Visible == false`이면 표시 개수는 검사하지 않음(기존 동작 유지).
- **UI 계층(MessageBox/WpfMsgBox)은 각 앱이 그대로 담당**. 메서드는 메시지 문구만 반환한다.
- 기존 두 메서드(`TryValidateHeaderButtonLayout`, `ValidateHeaderButtonLimits`)는 이 정적 메서드를 호출하는 얇은 래퍼로 줄인다. 동작(메시지·복구 로직)은 바르지 않는다.

---

## 4. 작업

1. `AltKey\Models\HeaderButtonConfig.cs`에 `TryValidateSave` 추가. 한국어 주석으로 검사 순서와 이유 기록.
2. `SettingsViewModel.TryValidateHeaderButtonLayout()`을 새 메서드 호출로 교체:
   - 좌측/우측 각각 검사하던 기존 구조를 유지하되, 메시지 문구는 `TryValidateSave` 반환값 사용.
   - 한도 초과 시 `RestoreHeaderButtonsFromConfig()` 복구 동작은 그대로 둔다.
3. `HeaderShortcutEditorWindow.ValidateHeaderButtonLimits()`을 새 메서드 호출로 교체. MessageBox 표시는 그대로.
4. 테스트 신규 작성: `AltKey.Tests\HeaderButton\HeaderButtonValidationTests.cs`
   - 커스텀 10개 + 1개 추가 → 거부 / 9개 + 1개 → 허용
   - 좌측 8개 표시 중 9번째 Visible 추가 → 거부 / 8개 → 허용
   - `isEditingExisting=true`일 때 동일 ID 중복 계산 안 됨(`CountVisibleButtons(excludingId:)` 경로)
   - `Visible=false` 버튼은 표시 개수에 안 잡힘
   - 한 파일 400줄 이하 유지.

---

## 5. 하지 말 것

- 메시지 문구를 바꾸지 않는다(사용자가 보는 문구는 그대로).
- 한도 상수(`MaxCustomButtonCount` 등) 값을 바꾸지 않는다.
- `HeaderButtonConfig`에 DI 서비스나 파일 I/O 의존성을 넣지 않는다(순수 정적 메서드 유지).
- `Core-Logic-Protection.md` §2 영역(자동완성/한글 조합)은 건드리지 않는다.

---

## 6. 검증

1. `dotnet build AltKey.Tools\AltKey.Tools.csproj`(솔루션 전체 빌드) 그린.
2. `dotnet test AltKey.Tests\AltKey.Tests.csproj` 그린 — 새 테스트 포함.
3. 기존 동작 회귀: 설정 탭에서 좌측 8개 채우고 9번째 표시 켜면 기존과 같은 메시지 + 복구.

---

## 7. 산출물

- `HeaderButtonConfig.TryValidateSave` 정적 메서드
- 두 앱의 얇은 래퍼
- `AltKey.Tests\HeaderButton\HeaderButtonValidationTests.cs`
