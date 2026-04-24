# Phase 2: 입력 엔진

> 목표: 화상 키보드 키 클릭이 SendInput으로 타겟 앱에 전달되고, 고정 키(Sticky Keys)와 조합키가 동작한다.

**의존성**: Phase 0 (P/Invoke 선언), Phase 1 (WS_EX_NOACTIVATE 적용)

---

## T-2.1: INPUT 구조체 및 관련 상수 정의

**설명**: `SendInput`에 필요한 구조체와 상수를 `Platform/Win32.cs`에 추가한다.

**파일**: `Platform/Win32.cs`

**구현 내용**:
```csharp
public const int INPUT_KEYBOARD = 1;
public const uint KEYEVENTF_KEYUP      = 0x0002;
public const uint KEYEVENTF_UNICODE    = 0x0004;
public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

[StructLayout(LayoutKind.Sequential)]
public struct INPUT
{
    public int Type;
    public InputUnion Data;
}

[StructLayout(LayoutKind.Explicit)]
public struct InputUnion
{
    [FieldOffset(0)] public KEYBDINPUT Keyboard;
}

[StructLayout(LayoutKind.Sequential)]
public struct KEYBDINPUT
{
    public ushort Vk;
    public ushort Scan;
    public uint Flags;
    public uint Time;
    public IntPtr ExtraInfo;
}
```

**검증**: `dotnet build` 성공 (구조체 레이아웃 에러 없음).

---

## T-2.2: VirtualKeyCode 열거형 정의

**설명**: Windows VK 코드를 C# 열거형으로 정의한다. JSON 레이아웃에서 문자열 이름으로 참조할 수 있어야 한다.

**파일**: `Models/VirtualKeyCode.cs`

**구현 내용**:
```csharp
public enum VirtualKeyCode : ushort
{
    // 알파벳 (0x41~0x5A)
    VK_A = 0x41, VK_B, VK_C, VK_D, VK_E, VK_F, VK_G,
    VK_H, VK_I, VK_J, VK_K, VK_L, VK_M, VK_N,
    VK_O, VK_P, VK_Q, VK_R, VK_S, VK_T, VK_U,
    VK_V, VK_W, VK_X, VK_Y, VK_Z,
    // 숫자 (0x30~0x39)
    VK_0 = 0x30, VK_1, VK_2, VK_3, VK_4,
    VK_5, VK_6, VK_7, VK_8, VK_9,
    // 제어 키
    VK_BACK = 0x08, VK_TAB = 0x09, VK_RETURN = 0x0D,
    VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, // Alt
    VK_PAUSE = 0x13, VK_CAPITAL = 0x14,
    VK_ESCAPE = 0x1B, VK_SPACE = 0x20,
    VK_PRIOR = 0x21, VK_NEXT = 0x22,  // Page Up / Down
    VK_END = 0x23, VK_HOME = 0x24,
    VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28,
    VK_INSERT = 0x2D, VK_DELETE = 0x2E,
    VK_LWIN = 0x5B, VK_RWIN = 0x5C,
    VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1,
    VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3,
    VK_LMENU = 0xA4, VK_RMENU = 0xA5,
    // 기능 키
    VK_F1 = 0x70, VK_F2, VK_F3, VK_F4, VK_F5, VK_F6,
    VK_F7, VK_F8, VK_F9, VK_F10, VK_F11, VK_F12,
    // 한국어
    VK_HANGUL = 0x15, VK_HANJA = 0x19,
    // OEM 기호
    VK_OEM_1 = 0xBA, VK_OEM_PLUS = 0xBB, VK_OEM_COMMA = 0xBC,
    VK_OEM_MINUS = 0xBD, VK_OEM_PERIOD = 0xBE, VK_OEM_2 = 0xBF,
    VK_OEM_3 = 0xC0, VK_OEM_4 = 0xDB, VK_OEM_5 = 0xDC,
    VK_OEM_6 = 0xDD, VK_OEM_7 = 0xDE,
    VK_SNAPSHOT = 0x2C, VK_NUMLOCK = 0x90, VK_SCROLL = 0x91,
}

public static class VirtualKeyCodeExtensions
{
    public static bool IsModifier(this VirtualKeyCode vk) =>
        vk is VirtualKeyCode.VK_SHIFT or VirtualKeyCode.VK_LSHIFT or VirtualKeyCode.VK_RSHIFT
           or VirtualKeyCode.VK_CONTROL or VirtualKeyCode.VK_LCONTROL or VirtualKeyCode.VK_RCONTROL
           or VirtualKeyCode.VK_MENU or VirtualKeyCode.VK_LMENU or VirtualKeyCode.VK_RMENU
           or VirtualKeyCode.VK_LWIN or VirtualKeyCode.VK_RWIN;
}
```

**검증**: `Enum.Parse<VirtualKeyCode>("VK_A")` == `VirtualKeyCode.VK_A` (JSON 역직렬화에 활용).

---

## T-2.3: KeyAction 모델 정의

**설명**: 키를 클릭했을 때의 동작 타입을 JSON과 호환되는 방식으로 정의한다.

**파일**: `Models/KeyAction.cs`

**구현 내용**:
```csharp
using System.Text.Json.Serialization;

// 판별 유니온 패턴 (System.Text.Json JsonDerivedType)
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SendKeyAction),    "SendKey")]
[JsonDerivedType(typeof(SendComboAction),  "SendCombo")]
[JsonDerivedType(typeof(ToggleStickyAction), "ToggleSticky")]
[JsonDerivedType(typeof(SwitchLayoutAction), "SwitchLayout")]
public abstract record KeyAction;

public record SendKeyAction(string Vk) : KeyAction;
public record SendComboAction(List<string> Keys) : KeyAction;
public record ToggleStickyAction(string Vk) : KeyAction;
public record SwitchLayoutAction(string Name) : KeyAction;
```

**검증**: `{"type":"SendKey","Vk":"VK_A"}` JSON → `SendKeyAction("VK_A")` 역직렬화 성공.

---

## T-2.4: SendInput 래퍼 — 단일 키 전송

**설명**: `InputService`에 단일 키의 KeyDown + KeyUp을 전송하는 메서드를 작성한다.

**파일**: `Services/InputService.cs`

**구현 내용**:
```csharp
public class InputService
{
    public void SendKeyPress(VirtualKeyCode vk)
    {
        var inputs = new Win32.INPUT[]
        {
            MakeKeyDown((ushort)vk),
            MakeKeyUp((ushort)vk),
        };
        Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

    public void SendKeyDown(VirtualKeyCode vk)
    {
        var inputs = new[] { MakeKeyDown((ushort)vk) };
        Win32.SendInput(1, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

    public void SendKeyUp(VirtualKeyCode vk)
    {
        var inputs = new[] { MakeKeyUp((ushort)vk) };
        Win32.SendInput(1, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

    private static Win32.INPUT MakeKeyDown(ushort vk) => new()
    {
        Type = Win32.INPUT_KEYBOARD,
        Data = new() { Keyboard = new() { Vk = vk } }
    };

    private static Win32.INPUT MakeKeyUp(ushort vk) => new()
    {
        Type = Win32.INPUT_KEYBOARD,
        Data = new() { Keyboard = new() { Vk = vk, Flags = Win32.KEYEVENTF_KEYUP } }
    };
}
```

**검증**: 메모장에 포커스 → `SendKeyPress(VirtualKeyCode.VK_A)` 호출 → "a" 입력됨.

---

## T-2.5: 고정 키(Sticky Keys) 상태 관리

**설명**: 수식자 키의 고정/잠금 상태를 관리하는 로직을 `InputService`에 추가한다.

**파일**: `Services/InputService.cs`

**구현 내용**:
```csharp
public class InputService
{
    // 일회성 고정 (다음 키 입력 후 자동 해제)
    private readonly HashSet<VirtualKeyCode> _stickyKeys = [];
    // 영구 잠금 (명시적 해제 전까지 유지)
    private readonly HashSet<VirtualKeyCode> _lockedKeys = [];

    public IReadOnlySet<VirtualKeyCode> StickyKeys => _stickyKeys;
    public IReadOnlySet<VirtualKeyCode> LockedKeys => _lockedKeys;

    /// 수식자 키 토글: 미고정 → 일회성 고정 → 영구 잠금 → 해제 순환
    public void ToggleModifier(VirtualKeyCode vk)
    {
        if (_lockedKeys.Contains(vk))
        {
            _lockedKeys.Remove(vk);
            _stickyKeys.Remove(vk);
            SendKeyUp(vk);
        }
        else if (_stickyKeys.Contains(vk))
        {
            _lockedKeys.Add(vk);
            // 영구 잠금 상태 진입 — KeyDown 유지
        }
        else
        {
            _stickyKeys.Add(vk);
            SendKeyDown(vk);
        }

        StickyStateChanged?.Invoke();
    }

    /// 일반 키 입력 시 호출. 일회성 고정 수식자를 해제한다.
    private void ReleaseTransientModifiers()
    {
        var transient = _stickyKeys.Except(_lockedKeys).ToList();
        foreach (var mod in transient)
        {
            SendKeyUp(mod);
            _stickyKeys.Remove(mod);
        }
        if (transient.Count > 0) StickyStateChanged?.Invoke();
    }

    public event Action? StickyStateChanged;
}
```

**검증**: Sticky Shift 활성 → "a" 클릭 → "A" 입력 + Shift 자동 해제.

---

## T-2.6: KeyAction 처리기 (디스패처)

**설명**: `KeyAction` 타입을 분기하여 적절한 `InputService` 메서드를 호출하는 처리기를 작성한다.

**파일**: `Services/InputService.cs` (메서드 추가) 또는 `ViewModels/KeyboardViewModel.cs`

**구현 내용**:
```csharp
public void HandleAction(KeyAction action)
{
    switch (action)
    {
        case SendKeyAction { Vk: var vkStr }:
            if (Enum.TryParse<VirtualKeyCode>(vkStr, out var vk))
            {
                SendKeyPress(vk);
                ReleaseTransientModifiers();
            }
            break;

        case SendComboAction { Keys: var keys }:
            var vkList = keys
                .Select(k => Enum.TryParse<VirtualKeyCode>(k, out var v) ? (VirtualKeyCode?)v : null)
                .Where(v => v.HasValue).Select(v => v!.Value).ToList();
            SendCombo(vkList);
            break;

        case ToggleStickyAction { Vk: var vkStr2 }:
            if (Enum.TryParse<VirtualKeyCode>(vkStr2, out var modVk))
                ToggleModifier(modVk);
            break;
    }
}

private void SendCombo(List<VirtualKeyCode> keys)
{
    foreach (var k in keys) SendKeyDown(k);
    foreach (var k in Enumerable.Reverse(keys)) SendKeyUp(k);
    ReleaseTransientModifiers();
}
```

**검증**: `HandleAction(new SendComboAction(["VK_CONTROL", "VK_C"]))` → Ctrl+C 실행됨.

---

## T-2.7: Caps Lock 상태 동기화

**설명**: 시스템의 Caps Lock 상태를 읽어와서 UI 라벨에 반영한다.

**파일**: `Services/InputService.cs`

**구현 내용**:
```csharp
[DllImport("user32.dll")]
private static extern short GetKeyState(int nVirtKey);

public bool IsCapsLockOn =>
    (GetKeyState((int)VirtualKeyCode.VK_CAPITAL) & 0x0001) != 0;
```

- `KeyboardViewModel`에서 `DispatcherTimer`로 100ms마다 폴링하거나, `StickyStateChanged` 이벤트와 연동해서 라벨 갱신

**검증**: 물리 키보드 Caps Lock 토글 시 AltKey 라벨도 변경됨.

---

## T-2.8: 한/영 전환 키 처리

**설명**: `VK_HANGUL`(0x15) 키를 통한 한/영 IME 전환을 처리한다.

**파일**: `Services/InputService.cs`

**구현 내용**:
- `VK_HANGUL` 전송 시 `KEYEVENTF_EXTENDEDKEY` 플래그 불필요 (일반 SendInput)
- WPF 창이 `WS_EX_NOACTIVATE`이므로 IME 컨텍스트는 타겟 앱에 유지됨
- 한/영 상태 감지: `GetKeyState(VK_HANGUL) & 0x0001`

**알려진 주의사항**: WPF IME 처리와 충돌 가능성이 있음. 테스트 필수. 문제 발생 시 `keybd_event` (구형 API)로 대체.

**검증**: 메모장 포커스 상태에서 AltKey 한/영 키 클릭 시 IME 전환됨.

---

## T-2.9: InputService 단위 테스트 프로젝트 생성

**설명**: Sticky Keys 로직에 대한 단위 테스트를 작성한다. SendInput 호출 자체는 모킹이 필요하므로, 상태 관리 로직만 우선 테스트한다.

**파일**: `AltKey.Tests/InputServiceTests.cs` (신규 테스트 프로젝트)

```
dotnet new xunit -n AltKey.Tests
dotnet add AltKey.Tests reference AltKey
```

**테스트 케이스**:
```csharp
[Fact]
public void ToggleModifier_FirstClick_AddsToStickyKeys()

[Fact]
public void ToggleModifier_SecondClick_AddsToLockedKeys()

[Fact]
public void ToggleModifier_ThirdClick_ClearsAll()

[Fact]
public void ReleaseTransientModifiers_DoesNotClearLockedKeys()
```

**검증**: `dotnet test` — 전체 통과.

---

## T-2.10: SendInput 권한 제약 사용자 안내

**설명**: 관리자 권한 앱(UAC 상승 앱)에 입력이 전달되지 않을 때 사용자에게 안내를 표시한다.

**파일**: `Services/InputService.cs` + `Views/KeyboardView.xaml`

**구현 내용**:
- `SendInput` 반환값이 0이고 `Marshal.GetLastWin32Error()`가 `ERROR_ACCESS_DENIED`(0x5)이면 경고 이벤트 발생
- UI에 노란색 배너: "현재 앱은 관리자 권한으로 실행 중이어서 입력이 전달되지 않습니다. AltKey를 관리자 권한으로 실행하세요."
- 배너는 5초 후 자동 사라짐

**검증**: 관리자 권한 앱 포커스 상태에서 키 클릭 시 경고 배너 표시됨.

---

## T-2.11: KeyboardViewModel — InputService 연결

**설명**: `KeyboardViewModel`에서 Key 클릭 이벤트를 받아 `InputService.HandleAction()`으로 전달한다.

**파일**: `ViewModels/KeyboardViewModel.cs`

**구현 내용**:
```csharp
public partial class KeyboardViewModel : ObservableObject
{
    private readonly InputService _inputService;

    [ObservableProperty]
    private IReadOnlyList<KeyRowViewModel> rows = [];

    [ObservableProperty]
    private bool showUpperCase;

    [RelayCommand]
    private void KeyPressed(KeySlot slot)
    {
        _inputService.HandleAction(slot.Action);
        // UI 상태 갱신 (Sticky, CapsLock)
        UpdateModifierState();
    }

    private void UpdateModifierState()
    {
        ShowUpperCase =
            _inputService.StickyKeys.Contains(VirtualKeyCode.VK_SHIFT) ||
            _inputService.LockedKeys.Contains(VirtualKeyCode.VK_SHIFT) ||
            _inputService.IsCapsLockOn;
    }
}
```

**검증**: Shift 고정 → `ShowUpperCase == true` → 키 라벨이 대문자로 변경됨.
