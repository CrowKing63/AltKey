# 01 — 모델 · 인터페이스 · 스키마 변경

> **소요**: 1~2시간
> **선행**: 없음
> **후행**: 02, 03, 04, 05, 07
> **관련 기획**: [refactor-unif-serialized-acorn.md §2, §4-8](../refactor-unif-serialized-acorn.md)

---

## 0. 이 태스크의 목표 한 줄

`KoreanInputModule`(03)이 만들어질 수 있도록 **타입 기반(enum · interface · record)** 을 먼저 깔아 둔다. 실제 로직 이동은 하지 않는다.

---

## 1. 전제 조건

- [00-overview.md](00-overview.md)를 읽었다.
- 브랜치 `feature/unified-autocomplete` 위에서 작업 중.
- 빌드가 녹색 상태.

---

## 2. 현재 상태 (수정 전)

### 2-1. `AltKey/Models/KeyAction.cs`

현재 `JsonDerivedType` 9개:
```csharp
[JsonDerivedType(typeof(SendKeyAction),        "SendKey")]
[JsonDerivedType(typeof(SendComboAction),      "SendCombo")]
[JsonDerivedType(typeof(ToggleStickyAction),   "ToggleSticky")]
[JsonDerivedType(typeof(SwitchLayoutAction),   "SwitchLayout")]
[JsonDerivedType(typeof(RunAppAction),         "RunApp")]
[JsonDerivedType(typeof(BoilerplateAction),    "Boilerplate")]
[JsonDerivedType(typeof(ShellCommandAction),   "ShellCommand")]
[JsonDerivedType(typeof(VolumeControlAction),  "VolumeControl")]
[JsonDerivedType(typeof(ClipboardPasteAction), "ClipboardPaste")]
```

### 2-2. `AltKey/Models/KeySlot.cs`

```csharp
public record KeySlot(
    string Label,
    string? ShiftLabel,
    KeyAction? Action,
    double Width = 1.0,
    double Height = 1.0,
    string StyleKey = "",
    double GapBefore = 0.0,
    string? HangulLabel = null,       // ← 리네이밍 대상
    string? HangulShiftLabel = null   // ← 리네이밍 대상
);
```

`HangulLabel`은 **이름과 달리** 현재 "영어 입력 상태에서 표시할 알파벳 라벨"로 쓰인다(예: `qwerty-ko.json`의 각 키에 `"hangul_label": "q"` 형태). 이번 리네이밍으로 의미와 이름을 맞춘다.

### 2-3. JSON 필드 이름

`qwerty-ko.json`의 모든 키 슬롯에서:
- `"hangul_label"` — 알파벳 라벨 (리네이밍 대상)
- `"hangul_shift_label"` — Shift 상태 알파벳 라벨 (리네이밍 대상)

---

## 3. 작업 내용

### 3-1. `InputSubmode` enum 신규 생성

**파일**: `AltKey/Services/InputLanguage/InputSubmode.cs` (신규)

```csharp
namespace AltKey.Services.InputLanguage;

/// 한국어 모듈 내부 입력 상태. "가/A" 토글 버튼이 이 값을 스위치한다.
public enum InputSubmode
{
    /// 한글 자모 조합 모드 (기본). 키 라벨은 자모, 입력 경로는 HangulComposer.
    HangulJamo,

    /// 조용한 영어 모드. OS IME는 건드리지 않고 유니코드로 영문만 입력.
    /// 키 라벨은 알파벳, 사전은 EnglishDictionary.
    QuietEnglish,
}
```

### 3-2. `IInputLanguageModule` 인터페이스 신규

**파일**: `AltKey/Services/InputLanguage/IInputLanguageModule.cs` (신규)

```csharp
using AltKey.Models;

namespace AltKey.Services.InputLanguage;

/// 언어 입력 모듈의 공통 계약.
/// 이번 릴리스에서는 KoreanInputModule 단 하나만 존재.
public interface IInputLanguageModule
{
    /// ISO 언어 코드. "ko".
    string LanguageCode { get; }

    /// 현재 서브모드. "가/A" 토글 버튼이 라벨로 참조.
    InputSubmode ActiveSubmode { get; }

    /// 토글 버튼에 표시할 라벨. HangulJamo → "가", QuietEnglish → "A".
    string ComposeStateLabel { get; }

    /// 자동완성에 노출할 현재 조합 문자열.
    /// HangulJamo: HangulComposer.Current
    /// QuietEnglish: 누적 중인 영문 prefix
    string CurrentWord { get; }

    /// 자동완성 제안 변경 이벤트.
    event Action<IReadOnlyList<string>>? SuggestionsChanged;

    /// 키 입력 처리. true면 호출자가 HandleAction을 스킵해야 함(모듈이 유니코드/SendInput으로 이미 처리).
    bool HandleKey(KeySlot slot, KeyContext ctx);

    /// 자동완성 제안 수락.
    /// 반환값: (BackspaceCount, FullWord) — 호출자가 이 값으로 SendAtomicReplace 또는 BS+Unicode 전송.
    (int backspaceCount, string fullWord) AcceptSuggestion(string suggestion);

    /// "가/A" 토글 버튼이 호출. 이전 조합 상태는 플러시하고 Submode 반전.
    void ToggleSubmode();

    /// 단어 구분자(공백/엔터/탭) 도달 시 호출.
    void OnSeparator();

    /// 레이아웃 전환 등 상태 초기화.
    void Reset();
}

/// HandleKey / AcceptSuggestion 호출 시 필요한 런타임 문맥.
/// 모듈이 InputService에 직접 의존하지 않도록 분리.
public sealed record KeyContext(
    bool ShowUpperCase,
    bool HasActiveModifiers,
    bool HasActiveModifiersExcludingShift,
    InputMode InputMode,
    int TrackedOnScreenLength
);
```

> **참고**: `InputMode`는 `AltKey/Services/InputService.cs`에 이미 정의되어 있다. using 추가 필요. 순환 참조가 문제되면 `InputMode`를 별도 파일로 추출하는 것도 허용.

### 3-3. `ToggleKoreanSubmodeAction` 신규 KeyAction

**파일**: `AltKey/Models/KeyAction.cs` (수정)

- 기존 `JsonDerivedType` 9개 뒤에 한 줄 추가:
  ```csharp
  [JsonDerivedType(typeof(ToggleKoreanSubmodeAction), "ToggleKoreanSubmode")]
  ```
- 파일 하단에 레코드 선언 추가:
  ```csharp
  /// "가/A" 토글 버튼이 트리거하는 액션.
  /// KeyboardViewModel.KeyPressed가 이 액션을 감지하면 KoreanInputModule.ToggleSubmode()를 호출한다.
  public sealed record ToggleKoreanSubmodeAction() : KeyAction;
  ```

> `KeyAction`의 기존 패턴(record·JsonPolymorphism)을 그대로 따른다. 추가 필드 없음.

### 3-4. `KeySlot.HangulLabel` → `EnglishLabel` 리네이밍

**파일**: `AltKey/Models/KeySlot.cs` (수정)

```csharp
public record KeySlot(
    string Label,
    string? ShiftLabel,
    KeyAction? Action,
    double Width = 1.0,
    double Height = 1.0,
    string StyleKey = "",
    double GapBefore = 0.0,
    [property: JsonPropertyName("english_label")]
    string? EnglishLabel = null,
    [property: JsonPropertyName("english_shift_label")]
    string? EnglishShiftLabel = null
);
```

using 추가:
```csharp
using System.Text.Json.Serialization;
```

### 3-5. JSON 레이아웃 파일 필드명 교체

**파일**: `AltKey/layouts/qwerty-ko.json` (수정)

**전역 일괄 치환**:
- `"hangul_label"` → `"english_label"`
- `"hangul_shift_label"` → `"english_shift_label"`

> **실수 주의**: 파일 내 **데이터 값**(예: `"label": "ㅂ"`)은 건드리지 않는다. 필드 **키**만 바뀐다.

**파일**: `AltKey/layouts/qwerty-en.json` — 이 파일은 09에서 삭제 예정이므로 지금은 건드리지 않는다.

### 3-6. 코드 내 `HangulLabel` 참조 일괄 업데이트

grep으로 찾아서 교체. 예상 위치:

- `AltKey/ViewModels/KeyboardViewModel.cs` — `KeySlotVm.HangulLabel`, `slot.HangulLabel` 접근자 → `EnglishLabel`
- `AltKey/ViewModels/KeyboardViewModel.cs:ShowUpperCase` 관련 라벨 렌더링 로직 — `slot.HangulShiftLabel` → `slot.EnglishShiftLabel`
- 그 외 어디든 `HangulLabel`이 나오면 전부 `EnglishLabel`로 교체.

> 단 **AutoCompleteService의 `OnHangulInput` 메서드명**은 지금 단계에서는 건드리지 않는다(04에서 통째로 제거됨).

### 3-7. 폴더 생성

```
AltKey/Services/InputLanguage/
├── IInputLanguageModule.cs
└── InputSubmode.cs
```

`AltKey/AltKey.csproj`의 `<ItemGroup>` 설정상 소스 파일은 자동 포함되므로 수동 등록 불필요.

---

## 4. 검증

1. `dotnet build` 녹색. 컴파일 에러 없음.
2. `dotnet test` 녹색. 기존 `HangulComposerTests` 통과.
3. 런타임 실행 → `qwerty-ko` 로딩 성공, 영어 서브라벨("q" 등)이 정상 표시.
4. grep 확인:
   - `grep -r "HangulLabel" AltKey/ --include="*.cs"` → 0건.
   - `grep -r "hangul_label" AltKey/layouts/ --include="*.json"` → 0건.

---

## 5. 함정 / 주의

- `KeyContext`의 `HasActiveModifiersExcludingShift`는 **이 태스크에서는 `InputService`에 실제로 구현되어 있지 않다**. 03 태스크에서 `InputService`에 프로퍼티를 추가한다. 지금은 **인터페이스만 선언**하는 것.
- `KeySlot`은 `record`이므로 `[property: JsonPropertyName(...)]` 문법을 쓴다. 생성자 파라미터 바로 앞에 `[property: ...]`.
- `qwerty-ko.json`의 `"hangul_label"` 일괄 치환 후 **필드 키만 바뀌고 값은 그대로**인지 꼭 확인. 예:
  - OK: `"english_label": "q"`
  - NG: `"english_label": "english_label"` (실수로 값까지 치환)
- `KeyAction.cs`에 `JsonDerivedType` 추가할 때 기존 항목의 콤마·순서와 어긋나지 않도록.

---

## 6. Critical Files

| 파일 | 수정 유형 |
|---|---|
| `AltKey/Services/InputLanguage/InputSubmode.cs` | **신규** |
| `AltKey/Services/InputLanguage/IInputLanguageModule.cs` | **신규** |
| `AltKey/Models/KeyAction.cs` | 수정 (`ToggleKoreanSubmodeAction` + `JsonDerivedType` 1줄) |
| `AltKey/Models/KeySlot.cs` | 수정 (필드 리네이밍 + `JsonPropertyName`) |
| `AltKey/layouts/qwerty-ko.json` | 수정 (필드 키 치환) |
| `AltKey/ViewModels/KeyboardViewModel.cs` | 소폭 (참조 리네이밍만) |

---

## 7. 커밋 메시지 초안

```
refactor(ko-only): introduce IInputLanguageModule + InputSubmode

- Add InputSubmode enum (HangulJamo / QuietEnglish).
- Add IInputLanguageModule interface with KeyContext record.
- Add ToggleKoreanSubmodeAction to KeyAction polymorphism.
- Rename KeySlot.HangulLabel → EnglishLabel (JSON field too).
```
