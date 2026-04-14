# Phase 7: 버그 수정

> **난이도**: ★☆☆ 쉬움  
> **목표**: 현재 동작하지 않거나 시각적으로 잘못된 기능을 수정한다.  
> **의존성**: Phase 0~5 완료 (현재 코드베이스 기준)

각 태스크는 독립적으로 수행 가능하다. 완료 후 반드시 `dotnet build` 성공을 확인할 것.

---

## T-7.1: 레이아웃 드롭다운 UI 개선

**문제**: `KeyboardView.xaml:62`의 ComboBox가 기본 WPF 스타일이라 앱 디자인과 어울리지 않는다.
닫혀 있을 때 현재 레이아웃 이름이 보이지 않는 경우가 있다.

**파일**: `AltKey/Views/KeyboardView.xaml`

**현재 코드** (`KeyboardView.xaml:60-68`):
```xml
<StackPanel Orientation="Horizontal" HorizontalAlignment="Right"
            Margin="0,0,68,0" VerticalAlignment="Center">
    <ComboBox x:Name="LayoutComboBox"
              ItemsSource="{Binding AvailableLayouts}"
              SelectedItem="{Binding CurrentLayoutName, Mode=OneWay}"
              SelectionChanged="OnLayoutSelectionChanged"
              Width="120" Height="20"
              FontSize="11"/>
</StackPanel>
```

**수정 방법**:

1. ComboBox에 스타일 속성을 추가한다:
```xml
<ComboBox x:Name="LayoutComboBox"
          ItemsSource="{Binding AvailableLayouts}"
          SelectedItem="{Binding CurrentLayoutName, Mode=OneWay}"
          SelectionChanged="OnLayoutSelectionChanged"
          Width="130" Height="20"
          FontSize="11"
          Background="Transparent"
          BorderBrush="#44FFFFFF"
          BorderThickness="1"
          Foreground="{StaticResource KeyFg}"
          Padding="6,2,4,2">
    <ComboBox.ItemContainerStyle>
        <Style TargetType="ComboBoxItem">
            <Setter Property="Padding" Value="6,3"/>
            <Setter Property="FontSize" Value="11"/>
        </Style>
    </ComboBox.ItemContainerStyle>
</ComboBox>
```

2. `MainViewModel.cs`에서 `CurrentLayoutName`이 초기값을 올바르게 노출하는지 확인한다.
   `SelectedItem="{Binding CurrentLayoutName, Mode=TwoWay}"` 로 변경하고
   `SelectionChanged` 이벤트 핸들러를 제거하거나 ViewModel 커맨드로 대체한다.

**검증**: 앱 실행 후 드롭다운에 현재 레이아웃 이름이 표시되고, 다른 항목 선택 시 레이아웃이 전환된다.

---

## T-7.2: 닫기 버튼 동작 수정 + 접기 버튼 추가

**문제**:
1. 현재 닫기(✕) 버튼이 `Window.Close()`를 호출해 앱이 완전히 종료된다(`KeyboardView.xaml.cs:77`). 트레이로 숨겨야 한다.
2. 최소화 버튼을 추가해도 `ShowInTaskbar=False`이므로 작업 표시줄에 아이콘이 없어 완전히 사라지는 것과 동일하다. 트레이로 복귀하는 UX는 기존과 다를 게 없다.
3. 화상 키보드 사용자는 키보드 단축키를 쓸 수 없으므로, 키보드를 완전히 숨기지 않고도 방해가 되지 않게 할 수단이 필요하다.

**올바른 해결책**: 두 가지 버튼을 분리한다.
- **접기 버튼(▲/▼)**: 창을 타이틀 바 높이(28px)로 줄인다. 창이 화면에 남아 있어 다시 펼치기 쉽다.
- **닫기(✕) 버튼**: 앱을 종료하지 않고 트레이로 숨긴다 (`Window.Hide()`).

### 1단계: 닫기 버튼 동작 수정

**파일**: `AltKey/Views/KeyboardView.xaml.cs`

`CloseButton_Click` 핸들러를 수정한다 (현재 77번 줄):
```csharp
// 기존
private void CloseButton_Click(object sender, RoutedEventArgs e)
{
    Window.GetWindow(this)?.Close();   // ← 앱 종료
}

// 변경 후
private void CloseButton_Click(object sender, RoutedEventArgs e)
{
    Window.GetWindow(this)?.Hide();    // ← 트레이로 숨기기만
}
```

> **트레이에서 완전 종료**: `TrayService`의 컨텍스트 메뉴 "종료" 항목을 통해서만 `Application.Current.Shutdown()`을 호출한다. 이미 T-5.5 구현에 포함되어 있다.

### 2단계: 접기 버튼 추가

**파일**: `AltKey/Views/KeyboardView.xaml`

상단 버튼 배치를 조정한다. 기어 버튼 Margin을 `"0,0,64,0"`으로 변경하고, 접기 버튼을 사이에 추가한다:

```xml
<!-- 기어(설정) 버튼: Margin을 "0,0,32,0" → "0,0,64,0" 으로 변경 -->
<Button x:Name="SettingsButton" Width="32" Height="28"
        HorizontalAlignment="Right" VerticalAlignment="Top"
        Margin="0,0,64,0"
        Background="Transparent" Foreground="#CCC"
        BorderThickness="0" Cursor="Hand"
        Command="{Binding ToggleSettingsCommand}"
        ToolTip="설정">
    <TextBlock Text="⚙" FontSize="14"/>
</Button>

<!-- 새로 추가: 접기/펼치기 버튼 -->
<Button x:Name="CollapseButton" Width="32" Height="28"
        HorizontalAlignment="Right" VerticalAlignment="Top"
        Margin="0,0,32,0"
        Background="Transparent" Foreground="#CCC"
        BorderThickness="0" Cursor="Hand"
        Click="CollapseButton_Click"
        ToolTip="접기 / 펼치기">
    <TextBlock x:Name="CollapseIcon" Text="▲" FontSize="10"/>
</Button>

<!-- 닫기 버튼: 위치 변화 없음 (HorizontalAlignment=Right, Margin=0) -->
```

**파일**: `AltKey/Views/KeyboardView.xaml.cs`

접기/펼치기 핸들러를 추가한다:
```csharp
private double _expandedHeight = 0;
private bool _isCollapsed = false;

private void CollapseButton_Click(object sender, RoutedEventArgs e)
{
    var window = Window.GetWindow(this);
    if (window is null) return;

    if (!_isCollapsed)
    {
        // 접기: 현재 높이 저장 후 타이틀 바 높이로 축소
        _expandedHeight = window.Height;
        var anim = new DoubleAnimation(28, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        window.BeginAnimation(Window.HeightProperty, anim);
        CollapseIcon.Text = "▼";
        _isCollapsed = true;
    }
    else
    {
        // 펼치기: 저장된 높이로 복원
        var anim = new DoubleAnimation(_expandedHeight, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        window.BeginAnimation(Window.HeightProperty, anim);
        CollapseIcon.Text = "▲";
        _isCollapsed = false;
    }
}
```

**검증**:
1. ✕ 버튼 클릭 → 창이 사라지고 트레이 아이콘 유지됨 (앱 미종료)
2. ▲ 버튼 클릭 → 키보드 영역이 접혀 타이틀 바만 화면에 남음
3. ▼ 버튼 클릭 → 원래 높이로 복원됨
4. 트레이 메뉴 → "종료" → 앱 완전 종료

---

## T-7.3: CapsLock 상태 시각적 인디케이터

**문제**: CapsLock이 켜져 있는지 키보드 UI에서 확인할 수 없다. `ShowUpperCase`는 이미 구현되어 있지만
CapsLock 키 버튼 자체에 활성 상태를 나타내는 별도 표시가 없다.

**파일**:
- `AltKey/ViewModels/KeyboardViewModel.cs`
- `AltKey/Views/KeyboardView.xaml`

**수정 방법**:

`KeyboardViewModel.cs`에 `IsCapsLockOn` 속성을 추가한다:

```csharp
// 기존 showUpperCase ObservableProperty 아래에 추가
[ObservableProperty]
private bool isCapsLockActive;
```

`UpdateModifierState()` 메서드에서 `IsCapsLockActive`를 갱신한다:
```csharp
private void UpdateModifierState()
{
    var capsLock = _inputService.IsCapsLockOn;
    IsCapsLockActive = capsLock;  // ← 추가

    ShowUpperCase =
        _inputService.StickyKeys.Contains(VirtualKeyCode.VK_SHIFT) || ...
```

`KeyboardView.xaml`의 KeyButton 바인딩에 CapsLock 상태를 전달하기 위해,
`KeySlotVm`에 CapsLock 키를 감지하는 로직을 추가한다.

`KeyboardViewModel.cs`의 `UpdateModifierState()`에서 CapsLock 키 슬롯을 찾아 `IsLocked` 속성을 설정한다:

```csharp
// UpdateModifierState() 내부, foreach 루프 안에 추가
foreach (var row in Rows)
{
    foreach (var slotVm in row.Keys)
    {
        // 기존 Sticky/Locked 처리
        if (slotVm.StickyVk is { } vk)
        {
            slotVm.IsSticky = _inputService.StickyKeys.Contains(vk);
            slotVm.IsLocked = _inputService.LockedKeys.Contains(vk);
        }

        // ← 추가: CapsLock 키 슬롯 강조
        if (slotVm.Slot.Action is SendKeyAction { Vk: "VK_CAPITAL" })
        {
            slotVm.IsLocked = _inputService.IsCapsLockOn;
        }
    }
}
```

**검증**: CapsLock 켜기 → CapsLock 키 버튼이 Locked 스타일(강조색)로 표시됨. 끄면 원래 색으로 복귀.

---

## T-7.4: 레이아웃 전체 직사각형 정렬 (완료 — JSON 수정)

**문제**: 1~4행은 모두 너비 합계 **15.0**이지만, 5행(하단)만 **18.25**로 훨씬 넓다.
그 결과 키보드 전체 외곽선이 직사각형이 아닌 아래로 튀어나온 모양이 된다.

```
┌──────────────────────────────────────┐  ← 행 1~4: 15.0 단위
│  ` 1 2 3 4 5 6 7 8 9 0 - = [  BS  ] │
│  [Tab] Q W E R T Y U I O P [ ] \    │
│  [Caps] A S D F G H J K L ; ' [Ent] │
│  [  Shift  ] Z X C V B N M , . / [S]│
└──────────────────────────────────────┘
┌────────────────────────────────────────────┐  ← 행 5: 18.25 단위 (초과)
│ Ctrl Win Alt 한/영 [   Space   ] 한자 Alt Ctrl ← ↑ ↓ → │
└────────────────────────────────────────────┘
```

**해결 방법**: JSON에서 Space바 및 수식자 키 너비를 조정해 5행 합계도 15.0으로 맞춘다. 코드 변경 없음.

**이미 완료**: 이 수정은 JSON 파일에 직접 반영되었다.

### 최종 5행 구성

**qwerty-ko.json** (Ctrl+Win+Alt+한/영+Space+한자+Alt+Ctrl+←+↑+↓+→):

```
1.25 + 1.0 + 1.25 + 1.0 + 3.0 + 1.0 + 1.25 + 1.25 + 1.0 + 1.0 + 1.0 + 1.0 = 15.0 ✓
```

```json
{ "label": "Ctrl",  "action": { "type": "ToggleSticky", "vk": "VK_CONTROL" }, "width": 1.25 },
{ "label": "Win",   "action": { "type": "SendKey",      "vk": "VK_LWIN" },    "width": 1.0  },
{ "label": "Alt",   "action": { "type": "ToggleSticky", "vk": "VK_MENU" },    "width": 1.25 },
{ "label": "한/영", "action": { "type": "SendKey",      "vk": "VK_HANGUL" },  "width": 1.0  },
{ "label": "Space", "action": { "type": "SendKey",      "vk": "VK_SPACE" },   "width": 3.0  },
{ "label": "한자",  "action": { "type": "SendKey",      "vk": "VK_HANJA" },   "width": 1.0  },
{ "label": "Alt",   "action": { "type": "ToggleSticky", "vk": "VK_MENU" },    "width": 1.25 },
{ "label": "Ctrl",  "action": { "type": "ToggleSticky", "vk": "VK_CONTROL" }, "width": 1.25 },
{ "label": "←",    "action": { "type": "SendKey",      "vk": "VK_LEFT" },    "width": 1.0  },
{ "label": "↑",    "action": { "type": "SendKey",      "vk": "VK_UP" },      "width": 1.0  },
{ "label": "↓",    "action": { "type": "SendKey",      "vk": "VK_DOWN" },    "width": 1.0  },
{ "label": "→",    "action": { "type": "SendKey",      "vk": "VK_RIGHT" },   "width": 1.0  }
```

**qwerty-en.json** (Ctrl+Win+Alt+Space+Alt+Ctrl+←+↑+↓+→, 한/영·한자 제거):

```
1.5 + 1.0 + 1.5 + 4.0 + 1.5 + 1.5 + 1.0 + 1.0 + 1.0 + 1.0 = 15.0 ✓
```

```json
{ "label": "Ctrl",  "action": { "type": "ToggleSticky", "vk": "VK_CONTROL" }, "width": 1.5  },
{ "label": "Win",   "action": { "type": "SendKey",      "vk": "VK_LWIN" },    "width": 1.0  },
{ "label": "Alt",   "action": { "type": "ToggleSticky", "vk": "VK_MENU" },    "width": 1.5  },
{ "label": "Space", "action": { "type": "SendKey",      "vk": "VK_SPACE" },   "width": 4.0  },
{ "label": "Alt",   "action": { "type": "ToggleSticky", "vk": "VK_MENU" },    "width": 1.5  },
{ "label": "Ctrl",  "action": { "type": "ToggleSticky", "vk": "VK_CONTROL" }, "width": 1.5  },
{ "label": "←",    "action": { "type": "SendKey",      "vk": "VK_LEFT" },    "width": 1.0  },
{ "label": "↑",    "action": { "type": "SendKey",      "vk": "VK_UP" },      "width": 1.0  },
{ "label": "↓",    "action": { "type": "SendKey",      "vk": "VK_DOWN" },    "width": 1.0  },
{ "label": "→",    "action": { "type": "SendKey",      "vk": "VK_RIGHT" },   "width": 1.0  }
```

**결과**:
```
┌──────────────────────────────────────┐
│  ` 1 2 3 4 5 6 7 8 9 0 - = [  BS  ] │
│  [Tab] Q W E R T Y U I O P [ ] \    │
│  [Caps] A S D F G H J K L ; ' [Ent] │
│  [  Shift  ] Z X C V B N M , . / [S]│
│  [Ctrl][Win][Alt][Spc][Alt][Ctrl][←↑↓→]│
└──────────────────────────────────────┘
```

**검증**: 앱 실행 시 모든 행이 동일 너비로 렌더링되어 키보드 전체가 깔끔한 직사각형으로 보인다.

---

## T-7.5: 한/영 IME 연동 — 한글 레이블 표시

**문제**: `qwerty-ko.json`에서 알파벳 키의 `shift_label`에 한글 자모가 지정되어 있으나,
한/영 키를 눌러 IME를 전환해도 키보드 UI에는 영문 레이블이 그대로 표시된다.

**원인**: `KeyboardViewModel.ShowUpperCase`가 Shift/CapsLock만 감지하고, Hangul IME 상태를 감지하지 않는다.
`KeySlotVm.GetLabel(bool upperCase)`도 `upperCase` 하나의 파라미터만 받는다.

**해결 전략**: Hangul IME 상태를 추적하는 `ShowHangul` 속성을 추가하고, 레이블 선택 로직을 확장한다.

### 1단계: KeyboardViewModel에 Hangul 상태 추가

**파일**: `AltKey/ViewModels/KeyboardViewModel.cs`

```csharp
[ObservableProperty]
private bool showHangul;
```

`UpdateModifierState()` 내부에 추가:
```csharp
ShowHangul = _inputService.IsHangulOn;
```

> `InputService.IsHangulOn`은 이미 구현되어 있음:  
> `(GetKeyState(0x15) & 0x0001) != 0`

### 2단계: KeySlotVm.GetLabel 확장

**파일**: `AltKey/ViewModels/KeyboardViewModel.cs`

```csharp
// 기존
public string GetLabel(bool upperCase) =>
    upperCase && Slot.ShiftLabel is { } s ? s : Slot.Label;

// 변경 후
public string GetLabel(bool upperCase, bool hangul)
{
    // 한글 모드: shift_label을 기본값으로 표시, Shift+한글은 T-7.6에서 처리
    if (hangul && Slot.ShiftLabel is { } hangulLabel)
        return upperCase
            ? (Slot.HangulShiftLabel ?? hangulLabel)  // T-7.6 추가 예정
            : hangulLabel;

    // 영문 모드: 기존 동작
    return upperCase && Slot.ShiftLabel is { } s ? s : Slot.Label;
}
```

> **주의**: `Slot.HangulShiftLabel`은 T-7.6에서 추가된다. 지금은 `?? hangulLabel`로 폴백.

### 3단계: KeyButton에 ShowHangul 바인딩

**파일**: `AltKey/Controls/KeyButton.xaml.cs`

`KeyButton`에 `ShowHangul` DependencyProperty를 추가한다:
```csharp
public static readonly DependencyProperty ShowHangulProperty =
    DependencyProperty.Register(nameof(ShowHangul), typeof(bool), typeof(KeyButton),
        new PropertyMetadata(false, OnDisplayStateChanged));

public bool ShowHangul
{
    get => (bool)GetValue(ShowHangulProperty);
    set => SetValue(ShowHangulProperty, value);
}
```

기존 `OnDisplayStateChanged` 콜백에서 `ShowHangul`도 처리하도록 수정:
```csharp
private static void OnDisplayStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
{
    if (d is KeyButton kb)
        kb.UpdateLabel();
}

private void UpdateLabel()
{
    if (Slot is null) return;
    var vm = new KeySlotVm(Slot);
    LabelText = vm.GetLabel(ShowUpperCase, ShowHangul);
}
```

**파일**: `AltKey/Views/KeyboardView.xaml`

KeyButton 바인딩에 추가:
```xml
<controls:KeyButton
    ...
    ShowHangul="{Binding DataContext.Keyboard.ShowHangul,
        RelativeSource={RelativeSource AncestorType=UserControl}}"
    .../>
```

**검증**: 한/영 키 클릭 → `qwerty-ko.json`의 알파벳 키가 한글 자모로 표시됨 (ㅂ, ㅈ, ㄷ 등).

---

## T-7.6: 한글 Shift 쌍자음/복모음 표시

**문제**: 한글 모드에서 Shift를 눌러도 ㅃ, ㅉ, ㄸ, ㄲ, ㅆ, ㅒ, ㅖ가 표시되지 않는다.

**해결 전략**: `KeySlot` 모델에 `hangul_shift_label` 필드를 추가하고 JSON 레이아웃을 업데이트한다.

### 1단계: KeySlot 모델 수정

**파일**: `AltKey/Models/KeySlot.cs`

```csharp
public record KeySlot(
    string Label,
    [property: JsonPropertyName("shift_label")] string? ShiftLabel,
    KeyAction? Action,
    double Width = 1.0,
    double Height = 1.0,
    [property: JsonPropertyName("style_key")] string StyleKey = "",
    [property: JsonPropertyName("gap_before")] double GapBefore = 0.0,
    [property: JsonPropertyName("hangul_shift_label")] string? HangulShiftLabel = null  // ← 추가
);
```

### 2단계: qwerty-ko.json 업데이트

**파일**: `AltKey/layouts/qwerty-ko.json`

쌍자음·복모음이 있는 키에 `hangul_shift_label`을 추가한다:

| 키 | shift_label (한글 기본) | hangul_shift_label (쌍자음/복모음) |
|----|------------------------|-----------------------------------|
| Q  | ㅂ                     | ㅃ                                |
| W  | ㅈ                     | ㅉ                                |
| E  | ㄷ                     | ㄸ                                |
| R  | ㄱ                     | ㄲ                                |
| T  | ㅅ                     | ㅆ                                |
| O  | ㅐ                     | ㅒ                                |
| P  | ㅔ                     | ㅖ                                |

나머지 키(모음 및 단자음)는 `hangul_shift_label` 생략 (Shift 시 동일 동작).

예시:
```json
{ "label": "Q", "shift_label": "ㅂ", "hangul_shift_label": "ㅃ", "action": { "type": "SendKey", "vk": "VK_Q" }, "width": 1.0 }
```

### 3단계: T-7.5의 GetLabel 로직 확인

T-7.5의 `GetLabel(bool upperCase, bool hangul)` 구현에서
`Slot.HangulShiftLabel ?? hangulLabel` 부분이 이제 실제 값을 반환한다.
별도 코드 변경 없음.

**검증**: 한글 모드 + Shift 고정 상태에서 Q 키가 "ㅃ"으로 표시됨.

---

## T-7.7: 트레이 아이콘 경로 수정

**문제**: `TrayService.cs`에서 아이콘을 상대 경로 `"Assets/icon.ico"`로 로드하는데,
실행 환경(특히 single-file publish)에 따라 경로 해석이 달라져 아이콘이 보이지 않을 수 있다.

**파일**: `AltKey/Services/TrayService.cs`

**현재 코드** (추정):
```csharp
Icon = new System.Drawing.Icon("Assets/icon.ico"),
```

**수정 방법**:

실행 파일 위치 기준 절대 경로를 사용한다:
```csharp
var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
Icon = File.Exists(iconPath)
    ? new System.Drawing.Icon(iconPath)
    : SystemIcons.Application;  // 폴백: 시스템 기본 아이콘
```

또는 아이콘을 어셈블리 리소스로 내장한다:

1. `AltKey.csproj`에서 `icon.ico`를 EmbeddedResource로 설정:
```xml
<ItemGroup>
  <EmbeddedResource Include="Assets\icon.ico"/>
</ItemGroup>
```

2. 코드에서 스트림으로 로드:
```csharp
var asm = Assembly.GetExecutingAssembly();
using var stream = asm.GetManifestResourceStream("AltKey.Assets.icon.ico");
Icon = stream is not null ? new System.Drawing.Icon(stream) : SystemIcons.Application;
```

**검증**: 앱 실행 시 시스템 트레이에 AltKey 아이콘이 표시된다.
