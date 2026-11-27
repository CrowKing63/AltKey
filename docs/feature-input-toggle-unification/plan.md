# 한/영 전환 통합

상태: 구현 완료 (2026-11-27) · 범위: OS IME 한/영 전환 방식 통일 — 가/A 토글 키와 상단바 한/영 버튼·레이아웃 "한/영" 키의 통합. Basic.json/Basic Plus.json의 레이아웃 키 교체는 사용자의 수동 작업

> 이 문서는 "가/A와 한/영을 가진 두 개의 토글로 갈라져 있던 구조"를 **한 개의 키**("한/영", `SendKey VK_HANGUL`)로 통합하는 설계다.
> 별도 액션 타입(`ToggleKoreanSubmodeAction`)은 두지 않는다 — 통합 동작은 **키에만 걸린다**.
> 기본 레이아웃(`Basic.json`/`Basic Plus.json`)은 프리셋이라 앱 안에서 편집 불가 — 소스 파일을 직접 교체한다. 사용자는 개인 레이아웃을 레이아웃 편집기에서 자유롭게 수정한다.

## 배경

현재 한/영 전환은 **두 층위**를 건드린다:

1. **내부 서브모드 토글** — 레이아웃 "가" 키의 `ToggleKoreanSubmodeAction` → `KoreanInputModule.ToggleSubmode()` → 한글 조합(가) ↔ 조용한 영어(A) 서브모드 전환. 자동완성·제안·라벨과 결합된 진실원. `Basic.json:1170`.
2. **OS IME 토글** — 레이아웃 "한/영" 키의 `SendKey`(`VK_HANGUL`) (`Basic.json:1265`) 과 헤더 빌트인 `IdOsIme` 버튼(`MainViewModel.cs:596` `SendOsImeHangul`) → `InputService.SendKeyPress(VK_HANGUL)` → OS IME 한/영 상태 전환.

이 둘은 **서로 독립적**이다. 레이아웃 가/A 키를 눌러도 OS IME는 한/영 상태가 바뀌지 않고, 헤더 한/영 버튼을 눌러도 내부 서브모드는 바뀌지 않는다.

### 빠른 메모(스크래치패드) 특수 경우

패드(`ScratchPadWindow`)는 **항상 유니코드(내부 조합) 경로**를 쓴다. `KeyboardViewModel.cs:702` 근처:

```csharp
// 스크래치패드는 항상 내부 조합(Unicode) 경로를 쓴다 — 자기 창 IME 상태에 의존하면 한글이 안 조합된다.
var inputMode = scratchPadBox is not null || _inputService.Mode == InputMode.Unicode
    ? InputMode.Unicode
    : InputMode.VirtualKey;
```

패드에서는 OS IME가 실제로 동작하지 않는다. 패드 가/A 버튼은 `_autoComplete.ToggleKoreanSubmode()` 를 직접 호출해 OS IME와 무관한 내부 서브모드 토글만 한다. 통합 설계는 이 특수 경우를 그대로 보존한다.

## 목표

- **통합 키는 모든 경로에서 하나로 통합**한다. 레이아웃에는 가/A와 한/영을 별도 키로 두지 않고 하나만 남긴다 — 남는 형식은 사용자 수동 작업(Basic.json/Basic Plus.json)에서 선택.
- **전환 동작은 키에만 걸린다** — 별도 액션 타입(`ToggleKoreanSubmodeAction`)은 두지 않는다.
- **통합은 효과적 모드에 따라 갈라진다**:
  - **유니코드 모드** (자동완성 ON, 스크래치패드 활성) → `KoreanInputModule.ToggleSubmode()` 로 내부 서브모드 토글
  - **가상키 모드** (자동완성 OFF, 외부 앱 포커스) → `VK_HANGUL` 전송 + OS IME 판별 → 내부 서브모드 동기화
- **문자 키 라벨은 OS IME 상태에 따라 동적 전환한다** — 가상키 모드에서도 내부 서브모드를 `SetInputLanguage`로 동기화하고(`OnSubmodeChanged` → `RefreshKeyLabels`), 문자 키의 한글/영문 라벨·서브라벨이 갱신된다. 유니코드 모드(자동완성/패드)도 같은 경로 유지.

## 설계

### 핵심: 전환 핸들러 하나

`ToggleKoreanSubmodeAction`은 **제거**한다. 모든 사용자 버튼은 **통합 핸들러(`ToggleInputLanguage`)를 통과한다**. 그 핸들러는 두 모드(유니코드/가상키)로 분기하고, 패드 버튼만 강제 유니코드 분기로만 진입한다(`forceUnicode: true`).

```csharp
// AutoCompleteService.ToggleInputLanguage() — 예시 시그니처
void ToggleInputLanguage(bool forceUnicode = false)
{
    InputMode effectiveMode = ResolveEffectiveMode(forceUnicode);
    if (effectiveMode == InputMode.Unicode)
    {
        _module.ToggleSubmode();
        return;
    }

    // VirtualKey 모드: OS IME를 토글하고, OS 상태가 바뀐 후 내부 서브모드를 동기화
    _inputService.SendKeyPress(VirtualKeyCode.VK_HANGUL);

    var ime = _imeState.Query(50ms);
    switch (ime)
    {
        case ImeState.Korean: _module.SetInputLanguage(InputSubmode.HangulJamo); break;
        case ImeState.English: _module.SetInputLanguage(InputSubmode.QuietEnglish); break;
        default:
            break;
    }
}
```

### 효과적 모드 판단 규칙

`KeyboardViewModel.KeyPressed()`의 기존 패턴과 **동일하게**:

```csharp
InputMode effectiveMode =
    (scratchPadBox is not null || _inputService.Mode == InputMode.Unicode)
        ? InputMode.Unicode
        : InputMode.VirtualKey;
```

- `forceUnicode: true` → 패드 버튼이 외부 모드 추측과 무관하게 항상 유니코드 분기로 들어가려면 이 플래그를 전달. 패드 버튼은 이 플래그로만 진입한다.
- `scratchPadBox`는 `KeyboardViewModel`의 `ScratchPadForegroundBox` 경로를 통해 얻는다(메인 뷰모델은 패드를 알지 못하는 경로 존재). 커스텀 헤더 버튼은 패드를 항상 "비활성"으로 보고 외부 앱 경로로 처리한다.
- `KeyboardViewModel.KeyPressed()`에서는 `scratchPadBox` 계산(현재 라인 ~655) **뒤에** 통합 핸들러를 호출해야 한다 — 현재는 `effectiveAction`/`ToggleKoreanSubmodeAction` 분기(~635행)가 패드 확인보다 앞에 있어야 하고 순서를 옮겨야 한다.

### 내부 서브모드 동기화(모듈 추가 API)

`KoreanInputModule.ToggleSubmode()`는 **토글**이다. 외부 판별 결과로 **특정 상태로 설정**해야 하는데, 새 메서드가 필요하다:

```csharp
public void SetInputLanguage(InputSubmode submode)
{
    // ToggleSubmode()와 같은 선행 처리 (FinalizeComposition, _suggestionContext = null, _pendingTrailingSpace = false)
    // 두 값이 이미 같으면 no-op
}
```

구현은 §2 freeze 알고리즘을 건드리지 않는다 — `_submode` 설정과 같은 `SubmodeChanged` 이벤트를 발화하는 상태 변경 경로만 추가한다.

### IME 판별 서비스(`ImeStateService`)

`docs/feature-ime-detection/plan.md`의 설계·검증 기록을 참조한다.

- `QueryForegroundImeState(window)` → `Unknown / Korean / English` 세 값. `DeriveState`를 단일 진리원으로.
- **상한 50ms**(`SendMessageTimeout` + `SMTO_BLOCK | SMTO_ABORTIFHUNG`) — IME가 응답하지 않아도 AltKey가 걸리지 않도록.
- **Unknown 방어** — 판별 실패(대체 경로 모두 실패)는 내부 상태를 바꾸지 않는다. 한글/영문을 적극 주장하지 않는다.

### 전환 후 안내

- **유니코드**: 내부 서브모드 토글 — 현재 `OnSubmodeChanged`가 LiveRegion에 "한국어 입력 상태"/"영어 입력 상태"를 발화하므로 그대로 사용
- **가상키**: 판별된 상태로 내부 서브모드를 설정 → 같은 `SubmodeChanged` 경로로 라벨 스위치 + LiveRegion 발화
- **Unknown**: "OS IME 상태를 알 수 없음" 안내

## 제약

- `HangulComposer`·`SendAtomicReplace`·`InputService.Mode` 결정 로직 등 **§2 hard freeze 영역은 건드리지 않는다.** 같은 파일에 있는 새 메서드(`SetInputLanguage`)는 알고리즘이 아니라 상태 설정 경로다.
- **`InputService.Mode` 배정 로직은 변경하지 않는다** — 관리자 모드→VirtualKey 배정은 유지. 판별은 배정이 아니라 **검증용 읽기**.
- OS IME를 **제어하지 않는다** — 가상키 모드에서는 `VK_HANGUL` 전송이 한/영 전환을 실제 실행하므로, AltKey가 "OS IME를 제어한다"는 원칙과는 양립되지 않는다. 단, 이 전환은 기존 레이아웃/헤더 버튼에서 **이미 존재하는 동작**이지 새 제어가 아니다. 설계 원칙은 "제어는 VK_HANGUL 전송과 동치"라는 해석으로 유지한다.
- `AltKey.Tools` 분리 아키텍처 유지. 헤더 버튼은 메인 앱과 AltKey.Tools의 편집기에서 공유되므로 마이그레이션도 두 곳에서 동작해야 한다(동일 `HeaderButtonConfig` 로직).

## 수정 파일

### 코드

| 파일 | 변경 |
|---|---|
| `AltKey/Services/AutoCompleteService.cs` | `ToggleInputLanguage(bool forceUnicode = false)` 추가 — 모드 분기 + 전환 경로 |
| `AltKey/Services/InputLanguage/KoreanInputModule.cs` | `SetInputLanguage(InputSubmode)` 추가 — 토글 없이 특정 상태로 설정 |
| `AltKey/Services/InputLanguage/IInputLanguageModule.cs` | `SetInputLanguage(InputSubmode)` 선언 추가 |
| `AltKey/Services/ImeStateService.cs` (신규) | `QueryForegroundImeState()` + `DeriveState` — design doc 구현 |
| `AltKey/Platform/Win32.cs` | `GetGUIThreadInfo`/`GUITHREADINFO`/IMM32 함수 호출 선언 재추가 (TASK-02가 제거한 선언을 복구) |
| `AltKey/Services/LayoutService.cs` | 레이아웃 로드 시 구조적 JSON 마이그레이션 — 파싱 전 후보 `ToggleKoreanSubmode` discriminator를 `SendKey`(`VK_HANGUL`)로 자동 교체 (라벨/그리드/그 키의 그 외 속성은 유지) |
| `AltKey/Models/KeyAction.cs` | `ToggleKoreanSubmodeAction` 제거 (액션 타입·JsonDerivedType 등록 모두) |
| `AltKey/ViewModels/KeyboardViewModel.cs` | `ToggleKoreanSubmodeAction` 분기를 제거하고 `SendKey VK_HANGUL` → `ToggleInputLanguage()` 경로로 교체 (`scratchPadBox` 계산 후 호출). 기존 `IsKoreanSubmodeToggle`/`GetLabel`/`GetSubLabel`의 토글 전용 분기는 제거 — 문자 키의 한글/영문 라벨·서브라벨·AccessibleHelp는 `ActiveSubmode`(내부 서브모드)만으로 표시. `SetInputLanguage`가 `SubmodeChanged`를 발화하면 기존 `OnSubmodeChanged` → `RefreshKeyLabels` 경로로 라벨 동적 전환 + 안내가 발생. 통합 키의 정적/동적 표시와 강조 여부는 레이아웃 수동 작업에 맡긴다 |
| `AltKey/Controls/KeyButton.xaml` | `IsKoreanSubmodeToggle` 트리거 제거 — 액션이 사라지면 항상 false가 되므로 죽는 브랜치다 |
| `AltKey/ViewModels/MainViewModel.cs` | `SendOsImeHangul` 명령 제거; `CreateBuiltInHeaderButtonVm`의 `IdOsIme` 케이스 제거; `ExecuteHeaderButtonAction`의 `VK_HANGUL` 경로를 `ToggleInputLanguage()` 호출로 교체; `BuildHeaderActionHelp`의 OsIme 케이스 제거 |
| `AltKey/Views/ScratchPadWindow.xaml.cs` | `KoreanSubmodeButton_Click`을 `ToggleInputLanguage(forceUnicode: true)`로 교체 |
| `AltKey/Models/HeaderButtonConfig.cs` | `IdOsIme` 제거, `EnsureAllBuiltIns`에 `LegacyIdOsIme` RemoveAll 추가, `CreateDefaults`에서 항목 삭제, `IsBuiltInId`/`Get*` switch에서 제거 |
| `AltKey.Tools/HeaderShortcutEditorWindow.xaml.cs` | OsIme / ToggleKoreanSubmode 관련 분기 정리 |
| `AltKey/ViewModels/ActionBuilderViewModel.cs` | `ToggleKoreanSubmode` 액션 타입 제거 — 액션 타입 목록·`IsToggleKoreanSubmode`·분기 정리 |
| `AltKey/ViewModels/LayoutEditorViewModel.cs` | 동일 — 마이그레이션된 레이아웃을 편집할 때 "제거된 액션을 자동 교체한 키가 있습니다" 경고를 표시 (편집기 전용) |
| `AltKey/ViewModels/SettingsViewModel.cs` | 헤더 버튼 편집 관련 OsIme 항목·설명 정리 |

### 레이아웃 (사용자 수동 편집)

| 파일 | 변경 |
|---|---|
| `AltKey/layouts/Basic.json` | 가/A와 한/영 키를 하나로 합침 — 남는 형식(한/영 또는 가/A)은 사용자 수동 작업으로 선택 |
| `AltKey/layouts/Basic Plus.json` | 동일 |

사용자는 레이아웃 파일을 직접 수정한다(눈으로 보면서). 사용자의 저장 레이아웃에도 `SendKey VK_HANGUL` 키가 남아 있으면 그대로 둔다 — 통합 동작으로 바꾸고 싶으면 사용자가 해당 키를 직접 수정한다.

### 테스트

| 파일 | 변경 |
|---|---|
| `AltKey.Tests/HeaderButton/HeaderButtonEnsureBuiltInsTests.cs` | `LegacyIdOsIme` 삭제 케이스 추가(기존 Emoji 삭제 테스트와 같은 구조) |
| `AltKey.Tests/InputLanguage/KoreanInputModuleSubmodeTests.cs` (신규) | `SetInputLanguage` — 외부 설정 상태 동기화 검증 |
| `AltKey.Tests/InputServiceDispatchTests.cs` | `VK_HANGUL` 전송 계약은 그대로 유지 (기존 테스트 유지) |
| (신규) 통합 핸들러 테스트 | 모드별 분기 검증 (Unicode→토글 / VirtualKey→전송+동기화 / 패드→유니코드 강제 / Unknown→유지) |

### 문서

| 파일 | 변경 |
|---|---|
| `docs/auto-complet/CORE-LOGIC-PROTECTION.md` | §1 #5의 "OS IME은 상단바 VK_HANGUL 버튼이 담당하고, AltKey 내부 서브모드와는 독립적이다" → 통합 설계 반영 |
| `docs/auto-complet/CORE-LOGIC-PROTECTION.md` §2 | "qwerty-ko.json의 '가' 버튼 슬롯 ToggleKoreanSubmode 액션" 행 — 의미 갱신 |
| `docs/feature-ime-detection/plan.md` | 향후 과제(리팩토링) 섹션을 통합 스펙 문서로 연결 |

## 테스트 계획

1. **§2 freeze 영역 미변경 확인** — `HangulComposer`·`KeyboardViewModel.OnSubmodeChanged`·`SendAtomicReplace` 등에 인접한 diff가 없는지 커밋 전에 확인.
2. **단위 테스트** — `KoreanInputModuleTests`, `AutoCompleteServiceTests`, `HeaderButtonTests`, `ImeStateServiceTests` 통과(전 스위트).
3. **통합 핸들러 시나리오** — 유니코드: 토글 후 라벨 스위치 + 안내 발화 / 가상키: VK_HANGUL 전송 + 판별 후 내부 서브모드 동기화 + 라벨 스위치 / 패드: 강제 유니코드 / 판별 실패: 상태 유지.
4. **마이그레이션 테스트** — (a) 헤더 버튼: 기존 `OsIme` 기본 버튼 설정 파일 → `EnsureAllBuiltIns` 실행 시 `OsIme` 항목 정리(제거·멱등). (b) 레이아웃: `ToggleKoreanSubmode` discriminator가 남은 파일은 `LayoutService.Load`에서 자동 `SendKey`(`VK_HANGUL`)로 교체(라벨/그리드 유지)되고, 최종 표시된 레이아웃에 `SendKey VK_HANGUL` 키가 남는다. 레이아웃 편집기에서는 경고를 표시한다.
5. **수동 검증(§4)** — 샌드박스 번들(`dist/AltKey-sandbox`)로 GUI 확인.

## 수동 검증 체크리스트

- [ ] 자동완성 ON + 외부 앱 → 한/영 키 → 내부 서브모드 토글, OS IME 상태 변화 없음, 문자 키 라벨(가↔A) 스위치
- [ ] 자동완성 OFF + 외부 앱 → 한/영 키 → OS IME 한/영 상태 전환, 내부 서브모드 동기화, 라벨(가↔A) 스위치
- [ ] 빠른 메모 패드(포커스) → 한/영 키 → 항상 유니코드 분기(내부 토글만, OS IME 무관)
- [ ] 헤더 상단바 — "한/영" 버튼이 없다 (마이그레이션)
- [ ] 패드 한/영 버튼 — 항상 internal 서브모드 토글
- [ ] 자동완성 ON/OFF 전환 시 OS IME가 어긋나지 않는지 확인
- [ ] AltKey.Tools — 헤더 버튼 편집기에서 OsIme 항목이 없는지 확인

> **후속(계획 범위 외, 2026-11-27 구현)**: OS IME를 직접 토글하거나 다른 창으로 포커스를 옮길 때도 문자 키 라벨이 따라가도록 `ImeStateService.Refresh()` + `StateChanged`와 400ms 백업 폴링, `EVENT_INPUTLANGCHANGE`/`EVENT_SYSTEM_FOREGROUND` WinEvent hook을 `KeyboardViewModel`에 연결했다. 위 체크리스트의 "자동완성 ON/OFF 전환 시 어긋남" 항목과 함께 확인한다.

## 오픈 질문

~~1~ ~~레이아웃 파일~~ → 결정됨: 가/A와 한/영 키를 하나로 합친다(코드 상 사용하지 않는 토글 액션은 제거). 남는 형식은 사용자 수동 작업으로 선택 — Basic.json/Basic Plus.json을 눈으로 보고 편집.

~~2~ ~~Unknown 분기 처리~~ → 결정됨: 내부 상태 유지 + LiveRegion 안내(프로브에서 잘 동작 확인).

~~3~ ~~기존 저장 레이아웃의 `ToggleKoreanSubmodeAction` 마이그레이션~~ → 결정됨: **자동 교체**(구조적 JSON 레벨에서 `ToggleKoreanSubmode` → `SendKey VK_HANGUL`, 라벨은 유지) + 경고는 **레이아웃 편집기에서만** 표시.

~~4~ ~~통합 키(표시 형식)~~ → 결정됨: 레이아웃 파일에는 가/A와 한/영을 둘 다 남발지 않고 하나만 남긴다. 남는 형식은 사용자 수동 작업으로 선택 — 문자 키 라벨은 둘 다 동적 전환되므로 형식 선택은 표시/편집 편의에 그친다.
