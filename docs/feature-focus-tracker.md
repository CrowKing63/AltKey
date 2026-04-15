# 기능 설계: 텍스트 포커스 위치에 따라 키보드 자동 이동

> **상태**: 사전 설계 완료 / 구현 대기  
> **대상 파일**: 신규 `Services/FocusTrackerService.cs`, `MainWindow.xaml.cs` 수정, `Models/AppConfig.cs` 수정, `Views/SettingsView.xaml` 수정

---

## 기능 개요

현재 포커스된 텍스트 필드의 화면 좌표를 감지하여, AltKey 키보드가 해당 필드를 가리지 않는 위치로 자동 이동한다.  
설정에서 ON/OFF 토글 가능.

---

## 현재 코드 파악

### 관련 파일 및 위치

| 파일 | 관련 내용 |
|------|-----------|
| `MainWindow.xaml.cs:121` | `RestoreWindowPosition()` — 창 위치 초기화 로직 |
| `MainWindow.xaml.cs:108` | `OnClosing` — `c.Window.Left/Top` 저장 |
| `Views/KeyboardView.xaml.cs` | `MoveToScreenEdge(string)` — 화면 끝 이동 (참고용) |
| `Models/AppConfig.cs:27` | `WindowConfig Window` — Left/Top/Width/Height 저장 |
| `Platform/Win32.cs:50` | `SetWinEventHook` / `WinEventDelegate` — 이미 선언됨 |
| `Platform/Win32.cs:67` | `GetForegroundWindow()` — 이미 선언됨 |
| `Services/ProfileService.cs` | `SetWinEventHook` 사용 예제 (포그라운드 앱 감지) |

### Win32.cs에 이미 선언된 API

```csharp
// 이미 있음 — 추가 불필요
SetWinEventHook(...)
UnhookWinEvent(...)
GetForegroundWindow()
WinEventDelegate
```

---

## 필요한 Win32 API 추가 목록

`Platform/Win32.cs`에 아래를 추가해야 한다.

```csharp
// 포커스된 요소의 바운딩 렉트 조회
[DllImport("user32.dll")]
public static extern bool GetCaretPos(out POINT lpPoint);

[DllImport("user32.dll")]
public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

[DllImport("user32.dll")]
public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

[DllImport("user32.dll")]
public static extern IntPtr GetFocus();  // 동일 스레드 내에서만 동작 (제한적)

// GUI 스레드 정보 (다른 프로세스의 캐럿/포커스 위치 조회)
[DllImport("user32.dll")]
public static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

// 스레드 ID 조회
[DllImport("user32.dll")]
public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
// ※ 이미 선언됨 — 중복 추가 불필요

// 구조체
[StructLayout(LayoutKind.Sequential)]
public struct POINT { public int X; public int Y; }

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left; public int Top; public int Right; public int Bottom;
    public int Width  => Right  - Left;
    public int Height => Bottom - Top;
}

[StructLayout(LayoutKind.Sequential)]
public struct GUITHREADINFO
{
    public int      cbSize;        // Marshal.SizeOf<GUITHREADINFO>() 로 초기화
    public uint     flags;
    public IntPtr   hwndActive;
    public IntPtr   hwndFocus;
    public IntPtr   hwndCapture;
    public IntPtr   hwndMenuOwner;
    public IntPtr   hwndMoveSize;
    public IntPtr   hwndCaret;
    public RECT     rcCaret;       // 캐럿의 클라이언트 좌표 바운딩 렉트
}

// WinEvent 상수 (이미 SetWinEventHook는 있지만 상수 누락)
public const uint EVENT_OBJECT_FOCUS       = 0x8005;  // 포커스 이동
public const uint EVENT_SYSTEM_FOREGROUND  = 0x0003;  // 포그라운드 앱 변경 (이미 ProfileService에서 사용)
public const uint WINEVENT_OUTOFCONTEXT    = 0x0000;
public const uint WINEVENT_SKIPOWNPROCESS  = 0x0002;
```

---

## 새로운 서비스: `Services/FocusTrackerService.cs`

### 책임

- `EVENT_OBJECT_FOCUS` WinEventHook으로 외부 창의 포커스 변경 감지
- 포커스된 요소의 화면 좌표 계산 (`GUITHREADINFO.rcCaret` 또는 UI Automation BoundingRectangle)
- `FocusRectChanged` 이벤트로 MainWindow에 전달

### 인터페이스 설계

```csharp
namespace AltKey.Services;

public class FocusTrackerService : IDisposable
{
    /// 포커스된 요소의 화면 좌표가 바뀔 때 발생 (null = 감지 불가)
    public event Action<System.Windows.Rect?>? FocusRectChanged;

    public void Start();   // WinEventHook 등록
    public void Stop();    // WinEventHook 해제
    public void Dispose();
}
```

### 내부 구현 흐름

```
SetWinEventHook(EVENT_OBJECT_FOCUS, ..., WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS)
  ↓ 이벤트 콜백 (OnWinEvent)
  ├─ GetForegroundWindow() → hwnd
  ├─ GetWindowThreadProcessId(hwnd, ...) → threadId
  ├─ GetGUIThreadInfo(threadId, ref gui) → gui.rcCaret (클라이언트 좌표)
  ├─ gui.hwndCaret != IntPtr.Zero 이면:
  │     ClientToScreen(gui.hwndCaret, ref pt) → 화면 좌표로 변환
  │     FocusRect = new Rect(pt.X + gui.rcCaret.Left,
  │                          pt.Y + gui.rcCaret.Top,
  │                          gui.rcCaret.Width,
  │                          gui.rcCaret.Height)
  ├─ 캐럿 없는 경우 (비텍스트 컨트롤):
  │     UIA 폴백: Automation.FromHandle(hwnd)?.GetFocusedElement()?.Current.BoundingRectangle
  └─ FocusRectChanged?.Invoke(focusRect)
```

### UI Automation 폴백 (캐럿 없는 경우)

```csharp
// NuGet: 별도 패키지 불필요 — UIAutomationClient.dll은 .NET 기본 포함
using System.Windows.Automation;

var focused = AutomationElement.FocusedElement;
var rect    = focused?.Current.BoundingRectangle;  // screen coords
```

> **주의**: `AutomationElement.FocusedElement`는 UI 스레드가 아닌 백그라운드 스레드에서 호출해야 한다.  
> WinEventHook 콜백은 기본적으로 메시지 루프 스레드에서 실행되므로 Task.Run으로 오프로드 필요.

---

## AppConfig.cs 수정

```csharp
// 추가할 속성
public bool AutoMoveEnabled { get; set; } = false;   // 텍스트 포커스 자동 이동
```

---

## MainWindow.xaml.cs 수정

```csharp
// 생성자에 주입 추가
private readonly FocusTrackerService _focusTracker;

// OnSourceInitialized()에 추가
if (_configService.Current.AutoMoveEnabled)
    _focusTracker.Start();
_focusTracker.FocusRectChanged += OnFocusRectChanged;

// 새 메서드 추가
private void OnFocusRectChanged(System.Windows.Rect? focusRect)
{
    if (focusRect == null || !_configService.Current.AutoMoveEnabled) return;

    Dispatcher.Invoke(() =>
    {
        var screen     = SystemParameters.WorkArea;
        var kbRect     = new Rect(Left, Top, Width, Height);
        var targetRect = focusRect.Value;

        // 키보드가 포커스 영역을 가리는지 확인
        if (!kbRect.IntersectsWith(targetRect)) return;

        // 포커스 영역 위로 이동 가능한지 확인
        double upY = targetRect.Top - Height - 8;
        double dnY = targetRect.Bottom + 8;

        if (upY >= screen.Top)
            AnimateTo(Left, upY);
        else if (dnY + Height <= screen.Bottom)
            AnimateTo(Left, dnY);
        // 둘 다 불가 → 이동 안 함
    });
}

private void AnimateTo(double targetLeft, double targetTop)
{
    // DoubleAnimation으로 부드럽게 이동 (150ms)
    // Left/Top은 DependencyProperty가 아니므로 DispatcherTimer로 보간
    var fromLeft = Left;
    var fromTop  = Top;
    var steps    = 10;
    var step     = 0;
    var timer    = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
    timer.Tick += (_, _) =>
    {
        step++;
        double t = (double)step / steps;
        Left = fromLeft + (targetLeft - fromLeft) * t;
        Top  = fromTop  + (targetTop  - fromTop)  * t;
        if (step >= steps) timer.Stop();
    };
    timer.Start();
}
```

---

## SettingsViewModel.cs 수정

```csharp
// 추가할 바인딩 프로퍼티
[ObservableProperty]
private bool autoMoveEnabled;

partial void OnAutoMoveEnabledChanged(bool value)
{
    _configService.Update(c => c.AutoMoveEnabled = value);
    if (value) _focusTracker.Start();
    else       _focusTracker.Stop();
}
```

---

## SettingsView.xaml 수정

```xml
<!-- 기존 DwellEnabled 토글 근처에 추가 -->
<ToggleButton Content="포커스 위치 자동 이동"
              IsChecked="{Binding AutoMoveEnabled}"
              Style="{StaticResource ToggleStyle}"/>
```

---

## App.xaml.cs 수정

```csharp
// DI 등록 추가
services.AddSingleton<FocusTrackerService>();
```

---

## 구현 단계 체크리스트

- [ ] **Step 1** — `Platform/Win32.cs`에 `POINT`, `RECT`, `GUITHREADINFO`, `GetGUIThreadInfo`, `ClientToScreen`, `GetWindowRect`, WinEvent 상수 추가
- [ ] **Step 2** — `Services/FocusTrackerService.cs` 신규 작성 (GUITHREADINFO 기반 캐럿 감지)
- [ ] **Step 3** — UIA 폴백 추가 (`AutomationElement.FocusedElement`)
- [ ] **Step 4** — `AppConfig.cs`에 `AutoMoveEnabled` 추가
- [ ] **Step 5** — `MainWindow.xaml.cs`에 `FocusTrackerService` 주입 및 `OnFocusRectChanged` 구현
- [ ] **Step 6** — `SettingsViewModel.cs`에 `AutoMoveEnabled` 바인딩 추가
- [ ] **Step 7** — `SettingsView.xaml`에 토글 UI 추가
- [ ] **Step 8** — `App.xaml.cs`에 DI 등록

---

## 주의사항 및 엣지 케이스

| 상황 | 처리 방법 |
|------|----------|
| 포커스 창이 UAC 상승 앱 | `GetGUIThreadInfo` 실패 → `FocusRectChanged(null)` → 이동 안 함 |
| 멀티모니터 DPI 차이 | `SystemParameters.WorkArea`는 주 모니터 기준 — `Screen.AllScreens`로 확장 필요 |
| 빠른 포커스 이동 (타이핑 중) | 디바운스 300ms 적용 (마지막 이벤트만 처리) |
| 키보드가 이미 이상적 위치 | `IntersectsWith` 체크 후 이동 생략 |
| 옵션 OFF 상태에서 이벤트 수신 | `AutoMoveEnabled` 체크로 조기 반환 |
