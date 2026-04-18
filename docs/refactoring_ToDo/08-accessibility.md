# 08 — 접근성: `AutomationProperties` + `LiveRegion`

> **소요**: 2~4시간
> **선행**: 05 (KeySlotVm 확장), 07 (상단바 버튼 존재)
> **후행**: 10
> **관련 기획**: [refactor-unif-serialized-acorn.md §7, D5](../refactor-unif-serialized-acorn.md)

---

## 0. 이 태스크의 목표 한 줄

Narrator로 AltKey를 **처음부터 끝까지 쓸 수 있도록** 키·버튼 라벨과 모드 변경 공지를 구현. 이번 릴리스 범위는 **AutomationProperties 바인딩 + LiveRegion 공지**만. TTS·고대비·큰 텍스트는 후속.

---

## 1. 전제 조건

- 05에서 `KeySlotVm`이 확장됨.
- 06의 자동완성 토글 버튼, 07의 "가/A" 키·상단바 한/영 버튼 존재.

---

## 2. 현재 상태

### 2-1. `AltKey/Controls/KeyButton.xaml`

ContentPresenter(line 42 근방)에 `AutomationProperties.Name` 바인딩 **없음**. 서브라벨 TextBlock(line 33 근방) 이중 낭독 가능성.

### 2-2. `AltKey/Views/KeyboardView.xaml`

`LiveRegion` 공지용 TextBlock 없음.

---

## 3. 작업 내용

### 3-1. `KeySlotVm`에 접근성 프로퍼티 추가

**파일**: `AltKey/ViewModels/KeyboardViewModel.cs`

```csharp
public sealed class KeySlotVm : ObservableObject
{
    // ...기존...

    public string AccessibleName => ComputeAccessibleName();
    public string AccessibleHelp => ComputeAccessibleHelp();
    public string AutomationId   => Slot.Action?.GetType().Name ?? "UnknownAction";

    private string ComputeAccessibleName()
    {
        // "가/A" 토글 키
        if (Slot.Action is ToggleKoreanSubmodeAction)
        {
            return _autoComplete.ActiveSubmode == InputSubmode.HangulJamo
                ? "한국어 입력 중, 누르면 영어로 전환"
                : "영어 입력 중, 누르면 한국어로 전환";
        }

        var submode = _autoComplete.ActiveSubmode;
        if (submode == InputSubmode.HangulJamo)
        {
            // 자모 키 → 한국어 명칭
            string label = ShowUpperCase && Slot.ShiftLabel is { } s ? s : Slot.Label;
            string? jamoName = JamoNameResolver.ResolveKorean(label);
            if (jamoName is not null) return $"{jamoName}";
        }
        else   // QuietEnglish
        {
            string? letter = ShowUpperCase
                ? (Slot.EnglishShiftLabel ?? Slot.EnglishLabel?.ToUpperInvariant())
                : Slot.EnglishLabel;
            if (letter is not null) return $"{letter} 키";
        }

        // 기능 키(Shift/Ctrl/Enter/Space/Tab 등) — 한국어 이름
        return ResolveFunctionKeyName(Slot);
    }

    private string ComputeAccessibleHelp()
    {
        // Sticky/Locked 상태
        if (IsSticky) return "일회성 고정 상태";
        if (IsLocked) return "영구 고정 상태";
        return "";
    }
}
```

### 3-2. `JamoNameResolver` 헬퍼 신규

**파일**: `AltKey/Services/InputLanguage/JamoNameResolver.cs` (신규)

```csharp
namespace AltKey.Services.InputLanguage;

public static class JamoNameResolver
{
    private static readonly Dictionary<string, string> _names = new()
    {
        ["ㄱ"] = "기역", ["ㄲ"] = "쌍기역", ["ㄴ"] = "니은", ["ㄷ"] = "디귿",
        ["ㄸ"] = "쌍디귿", ["ㄹ"] = "리을", ["ㅁ"] = "미음", ["ㅂ"] = "비읍",
        ["ㅃ"] = "쌍비읍", ["ㅅ"] = "시옷", ["ㅆ"] = "쌍시옷", ["ㅇ"] = "이응",
        ["ㅈ"] = "지읒", ["ㅉ"] = "쌍지읒", ["ㅊ"] = "치읓", ["ㅋ"] = "키읔",
        ["ㅌ"] = "티읕", ["ㅍ"] = "피읖", ["ㅎ"] = "히읗",
        ["ㅏ"] = "아", ["ㅐ"] = "애", ["ㅑ"] = "야", ["ㅒ"] = "얘",
        ["ㅓ"] = "어", ["ㅔ"] = "에", ["ㅕ"] = "여", ["ㅖ"] = "예",
        ["ㅗ"] = "오", ["ㅛ"] = "요", ["ㅜ"] = "우", ["ㅠ"] = "유",
        ["ㅡ"] = "으", ["ㅣ"] = "이",
    };

    public static string? ResolveKorean(string? jamo)
        => jamo is not null && _names.TryGetValue(jamo, out var name) ? name : null;
}
```

### 3-3. `KeyButton.xaml`에 AutomationProperties 바인딩

**파일**: `AltKey/Controls/KeyButton.xaml`

기존 Button/ContentPresenter에 속성 추가:

```xml
<Button x:Name="RootButton"
        AutomationProperties.Name="{Binding AccessibleName}"
        AutomationProperties.HelpText="{Binding AccessibleHelp}"
        AutomationProperties.AutomationId="{Binding AutomationId}"
        ... >
    <Grid>
        <ContentPresenter x:Name="MainLabel" Content="{Binding DisplayLabel}" />
        <!-- 서브라벨: 자모와 알파벳 병기 서브 -->
        <TextBlock x:Name="SubLabel"
                   Text="{Binding SubLabelText}"
                   AutomationProperties.AccessibilityView="Raw"
                   FontSize="9" Opacity="0.6"
                   HorizontalAlignment="Right" VerticalAlignment="Top" />
    </Grid>
</Button>
```

- `AccessibilityView="Raw"`: 서브라벨을 UIA 트리에서 숨겨 이중 낭독 방지.
- `DisplayLabel` 은 05-3-5에서 정의. `SubLabelText` 는 Submode 반대편 라벨(참고용).

### 3-4. LiveRegion 공지 TextBlock

**파일**: `AltKey/Views/KeyboardView.xaml`

상단부 어딘가(헤더 아래):

```xml
<TextBlock x:Name="ModeAnnouncer"
           Text="{Binding ModeAnnouncement}"
           Opacity="0"
           IsHitTestVisible="False"
           AutomationProperties.LiveSetting="Polite"
           Width="0" Height="0" />
```

- 시각적으로 보이지 않지만 UIA LiveRegionChanged 이벤트로 Narrator가 낭독.

### 3-5. `LiveRegionService` 또는 헬퍼 추가

**파일**: `AltKey/Services/LiveRegionService.cs` (신규, 선택)

```csharp
namespace AltKey.Services;

public sealed class LiveRegionService
{
    public event Action<string>? Announced;

    public void Announce(string message) => Announced?.Invoke(message);
}
```

**파일**: `AltKey/ViewModels/KeyboardViewModel.cs`

```csharp
[ObservableProperty]
private string modeAnnouncement = "";

public KeyboardViewModel(..., LiveRegionService liveRegion)
{
    liveRegion.Announced += msg =>
    {
        ModeAnnouncement = msg;
        RaiseLiveRegionChanged();
    };
}

private void RaiseLiveRegionChanged()
{
    // Dispatcher로 UIA 이벤트 발행
    // 구체 구현: View의 Loaded 시 peer 캐시 후 RaiseAutomationEvent 호출
}
```

> XAML의 `x:Name="ModeAnnouncer"`를 code-behind에서 참조하여 `AutomationPeer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged)` 직접 호출하는 방법이 간단.

`KeyboardView.xaml.cs`:
```csharp
private void AnnounceLiveRegion()
{
    var peer = FrameworkElementAutomationPeer.FromElement(ModeAnnouncer);
    peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
}
```

### 3-6. 공지 트리거 지점

- **"가/A" 토글**: `KoreanInputModule.SubmodeChanged` 이벤트 → `LiveRegionService.Announce("영어 입력 상태" | "한국어 입력 상태")`.
- **상단바 한/영 버튼**: 07에서 구현. `_liveRegion.Announce("OS IME 한영 전환 신호 전송됨")`.
- **자동완성 토글**: 06에서 구현. 추가 공지: `"자동완성 켜짐" | "자동완성 꺼짐"`.
- **레이아웃 전환**(기존 기능): 현상 유지, 필요시 추가.

### 3-7. 상단바 버튼 AutomationProperties

07에서 이미 적용:
- `AutomationProperties.Name="OS IME 한영 전환"`
- `AutomationProperties.HelpText="거의 사용할 일이 없는 비상용 버튼입니다..."`
- 자동완성 토글에도 `Name`/`HelpText` 설정.

### 3-8. `KeySlotVm`의 Submode 변경 알림

05-3-6에서 Submode 변경 시 모든 키의 `DisplayLabel` 재평가를 구현했다. 여기에 `AccessibleName`도 포함:

```csharp
private void RefreshDisplay()
{
    OnPropertyChanged(nameof(DisplayLabel));
    OnPropertyChanged(nameof(AccessibleName));
    OnPropertyChanged(nameof(IsDimmed));
}
```

---

## 4. 검증 (Narrator 필요)

Windows의 Narrator를 `Ctrl+Win+Enter`로 켜고:

1. AltKey 창에 포커스 이동. 창 제목 낭독 확인.
2. Tab으로 상단바 버튼 순회:
   - "자동완성 토글" (Help: "켜면 유니코드 직접 입력...")
   - "OS IME 한영 전환" (Help: "거의 사용할 일이 없는...")
3. 키 포커스 이동 → 자모 키는 "비읍", "이응" 등 낭독. 알파벳 키는 "Q 키" 형태.
4. "가/A" 토글 포커스 → "한국어 입력 중, 누르면 영어로 전환" 낭독.
5. "가/A" 토글 클릭 → LiveRegion에서 "영어 입력 상태" 낭독.
6. 상단바 "한/영" 클릭 → "OS IME 한영 전환 신호 전송됨" 낭독.
7. Shift sticky 활성 상태 → Help에 "일회성 고정 상태" 낭독.

---

## 5. 함정 / 주의

- **UIA 이벤트 발행 타이밍**: `LiveRegionChanged`는 `Text`가 실제로 변경된 뒤 호출해야 함. `PropertyChanged → Dispatcher → 이벤트 발행` 순.
- **서브라벨 이중 낭독**: `AutomationProperties.AccessibilityView="Raw"` 없으면 메인 라벨 + 서브라벨 둘 다 낭독됨. 반드시 적용.
- **Narrator 미설치 환경**: Windows 11에는 기본 내장. 별도 설치 불필요.
- **수식자 키 공지**: Shift/Ctrl 등의 상태 변경(고정/해제)은 이번 릴리스 범위 외. 단, `AccessibleHelp`에 상태 표현은 해야 함.
- **한글 단어 조합 중 공지**: `HangulComposer.Current` 변경마다 공지하면 너무 빈번. 공지는 Submode 전환·토글 버튼 등 **상태 변화만**.
- **JamoName 매핑 누락**: `ㅘ`, `ㅙ` 같은 복합 모음이 레이아웃에 있다면 추가. 현재 qwerty-ko에는 단일 자모만 있을 가능성 높음.

---

## 6. Critical Files

| 파일 | 수정 유형 |
|---|---|
| `AltKey/Controls/KeyButton.xaml` | 수정 (AutomationProperties 바인딩) |
| `AltKey/ViewModels/KeyboardViewModel.cs` | 수정 (`AccessibleName`/`Help`, `ModeAnnouncement`) |
| `AltKey/Services/InputLanguage/JamoNameResolver.cs` | **신규** |
| `AltKey/Services/LiveRegionService.cs` | **신규** (선택) |
| `AltKey/Views/KeyboardView.xaml` | 수정 (LiveRegion TextBlock) |
| `AltKey/Views/KeyboardView.xaml.cs` | 수정 (RaiseAutomationEvent) |
| `AltKey/App.xaml.cs` | 소폭 (LiveRegionService DI) |

---

## 7. 커밋 메시지 초안

```
feat(a11y): AutomationProperties + LiveRegion announcements

- KeySlotVm exposes AccessibleName / AccessibleHelp / AutomationId.
- JamoNameResolver maps ㅎ→"히읗" etc.
- KeyButton.xaml binds UIA props; sub-label marked Raw.
- LiveRegionService + ModeAnnouncer TextBlock drive polite announcements.
- 가/A toggle, OS IME hangul, autocomplete toggle all announce state.
```
