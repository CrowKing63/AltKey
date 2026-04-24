# Phase 1: 핵심 윈도우 관리

> 목표: AltKey 창이 포커스를 뺏지 않고, 항상 위에 떠 있으며, Acrylic 블러 배경과 드래그 이동이 가능한 상태를 만든다.

**의존성**: Phase 0 완료

---

## T-1.1: P/Invoke 선언부 작성

**설명**: 이후 모든 Phase에서 사용할 Win32 API P/Invoke 선언을 한 파일에 모아 작성한다.

**파일**: `Platform/Win32.cs`

**선언할 함수들**:
```csharp
using System.Runtime.InteropServices;

internal static class Win32
{
    // 윈도우 스타일 상수
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_NOACTIVATE  = 0x08000000;
    public const int WS_EX_TOOLWINDOW  = 0x00000080;
    public const int WS_EX_LAYERED     = 0x00080000;
    public const int WS_EX_TOPMOST_VAL = 0x00000008;

    // SetWindowPos 플래그
    public static readonly IntPtr HWND_TOPMOST    = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST  = new(-2);
    public const uint SWP_NOMOVE  = 0x0002;
    public const uint SWP_NOSIZE  = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool SetLayeredWindowAttributes(
        IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    // SendInput (Phase 2에서 사용)
    [DllImport("user32.dll")]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // RegisterHotKey (Phase 5에서 사용)
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // WinEventHook (Phase 5에서 사용)
    public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
        uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    // 프로세스 이름 조회 (Phase 5에서 사용)
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // Acrylic 효과 (T-1.3에서 사용)
    [DllImport("user32.dll")]
    public static extern int SetWindowCompositionAttribute(
        IntPtr hwnd, ref WindowCompositionAttribData data);

    // INPUT 구조체 (Phase 2에서 사용)
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { /* ... */ }
}
```

**검증**: `dotnet build` — P/Invoke 선언 컴파일 성공 (미사용 경고는 무시).

---

## T-1.2: MainWindow XAML 기본 구성

**설명**: MainWindow를 투명 배경, 타이틀바 없음, 포커스 비침해로 초기 설정한다.

**파일**: `MainWindow.xaml`

**구현 내용**:
```xml
<ui:FluentWindow
    x:Class="AltKey.MainWindow"
    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
    WindowStyle="None"
    AllowsTransparency="True"
    Background="Transparent"
    ShowActivated="False"
    Topmost="True"
    ShowInTaskbar="False"
    ResizeMode="NoResize"
    Width="900" Height="320"
    ui:WindowBackdropType="Acrylic">

    <Grid>
        <views:KeyboardView/>
    </Grid>
</ui:FluentWindow>
```

**핵심 포인트**:
- `ShowActivated="False"` — 창 표시 시 포커스 자동 획득 방지 (WPF 빌트인)
- `AllowsTransparency="True"` — WPF 투명 창 활성화
- `ui:WindowBackdropType="Acrylic"` — WPF-UI Acrylic 효과
- `ShowInTaskbar="False"` — 태스크바 미표시

**검증**: 앱 실행 시 타이틀바 없는 창이 뜨고, Acrylic 블러가 배경에 적용됨.

---

## T-1.3: WS_EX_NOACTIVATE 적용 (WindowService)

**설명**: 창이 렌더링된 직후 P/Invoke로 `WS_EX_NOACTIVATE`를 추가한다. `ShowActivated=False`만으로는 클릭 시 포커스 이동을 완전히 막을 수 없으므로, HWND 스타일 직접 설정이 필수다.

**파일**: `Services/WindowService.cs`

**구현 내용**:
```csharp
public class WindowService
{
    public void ApplyNoActivate(IntPtr hwnd)
    {
        int exStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE,
            exStyle | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW);
    }
}
```

**호출 위치**: `MainWindow.cs`의 `SourceInitialized` 이벤트에서 호출 (HWND가 생성된 직후).

```csharp
protected override void OnSourceInitialized(EventArgs e)
{
    base.OnSourceInitialized(e);
    var hwnd = new WindowInteropHelper(this).Handle;
    _windowService.ApplyNoActivate(hwnd);
}
```

**검증**: 메모장에 커서 위치 후 AltKey 버튼 클릭 → 메모장 포커스(커서 깜빡임) 유지됨.

---

## T-1.4: Acrylic 효과 폴백 처리

**설명**: Windows 10 구버전 등 Acrylic 미지원 환경에서 반투명 단색으로 폴백한다.

**파일**: `Services/WindowService.cs`

**구현 내용**:
```csharp
public void ApplyBackdropOrFallback(Window window)
{
    // WPF-UI가 Acrylic 적용 실패 시 이벤트를 발생시킴
    // 실패하면 Window.Background를 반투명 단색으로 교체
    try
    {
        // WPF-UI가 자동 처리하므로 실패 감지만 구현
        window.Background = Brushes.Transparent;
    }
    catch
    {
        window.Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30));
    }
}
```

**OS 버전 감지**:
```csharp
bool isAcrylicSupported =
    Environment.OSVersion.Version >= new Version(10, 0, 19041); // Win10 2004+
```

**검증**: 가상 머신(구형 Win10) 에서도 앱이 정상 실행되고 불투명 배경 표시됨.

---

## T-1.5: 창 드래그 이동

**설명**: 타이틀바가 없으므로 키보드 상단 드래그 핸들 영역으로 창을 이동할 수 있게 한다.

**파일**: `Views/KeyboardView.xaml` + `MainWindow.cs`

**구현 내용**:
```xml
<!-- 드래그 핸들 영역 -->
<Border x:Name="DragHandle" Height="20" Background="Transparent" Cursor="SizeAll">
    <TextBlock Text="⋯" HorizontalAlignment="Center" Opacity="0.3"/>
</Border>
```

```csharp
// DragHandle.MouseLeftButtonDown 이벤트
private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    DragMove(); // WPF 빌트인 창 드래그
}
```

**검증**: 드래그 핸들을 잡고 끌면 창이 이동함. 키 영역은 드래그되지 않음.

---

## T-1.6: 창 위치/크기 저장 및 복원

**설명**: 앱 종료 시 창 위치와 크기를 `config.json`에 저장하고, 다음 실행 시 복원한다.

**파일**: `MainWindow.cs`, `Services/ConfigService.cs`

**구현 내용**:
```csharp
// 종료 시 저장
protected override void OnClosing(CancelEventArgs e)
{
    _configService.Update(c =>
    {
        c.Window.Left = (int)Left;
        c.Window.Top = (int)Top;
        c.Window.Width = (int)Width;
        c.Window.Height = (int)Height;
    });
}

// 시작 시 복원
protected override void OnSourceInitialized(EventArgs e)
{
    var cfg = _configService.Current.Window;
    Left = cfg.Left; Top = cfg.Top;
    Width = cfg.Width; Height = cfg.Height;
}
```

**검증**: 창 이동 후 종료 → 재실행 시 같은 위치에 창이 뜸.

---

## T-1.7: 자동 페이딩 (DispatcherTimer)

**설명**: 마우스가 창 밖으로 나간 뒤 설정된 시간이 지나면 창을 반투명으로 전환하고, 재진입 시 복귀한다.

**파일**: `MainWindow.cs`

**구현 내용**:
```csharp
private DispatcherTimer _fadeTimer = new() { Interval = TimeSpan.FromSeconds(5) };

private void OnMouseLeave(object s, MouseEventArgs e)
{
    _fadeTimer.Start();
}

private void OnMouseEnter(object s, MouseEventArgs e)
{
    _fadeTimer.Stop();
    // 즉시 복귀 (애니메이션)
    BeginAnimation(OpacityProperty,
        new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150)));
}

private void FadeTimer_Tick(object? s, EventArgs e)
{
    _fadeTimer.Stop();
    BeginAnimation(OpacityProperty,
        new DoubleAnimation(_config.OpacityIdle, TimeSpan.FromMilliseconds(400)));
}
```

**검증**: 마우스를 창 밖으로 5초 이상 이동 시 반투명 → 다시 올리면 불투명 복귀.

---

## T-1.8: 항상 위 토글

**설명**: `Topmost` 속성을 런타임에 토글하는 메서드를 `WindowService`에 추가한다.

**파일**: `Services/WindowService.cs`

**구현 내용**:
```csharp
public void SetTopmost(Window window, bool topmost)
{
    window.Topmost = topmost; // WPF 빌트인으로 충분
    // WPF Topmost가 일부 환경에서 불안정하면 SetWindowPos로 보강:
    var hwnd = new WindowInteropHelper(window).Handle;
    Win32.SetWindowPos(hwnd,
        topmost ? Win32.HWND_TOPMOST : Win32.HWND_NOTOPMOST,
        0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
}
```

**검증**: 설정에서 항상 위 해제 시 다른 창이 AltKey 위로 올라올 수 있음.

---

## T-1.9: 창 리사이즈 핸들

**설명**: 우하단에 리사이즈 핸들을 배치하여 창 크기를 마우스로 조절할 수 있게 한다.

**파일**: `Views/KeyboardView.xaml`

**구현 내용**:
```xml
<Grid>
    <!-- ... 키보드 콘텐츠 ... -->
    <Thumb x:Name="ResizeGrip"
           Width="16" Height="16"
           HorizontalAlignment="Right" VerticalAlignment="Bottom"
           Cursor="SizeNWSE"
           DragDelta="ResizeGrip_DragDelta"/>
</Grid>
```

```csharp
private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
{
    Width  = Math.Max(400, Width  + e.HorizontalChange);
    Height = Math.Max(200, Height + e.VerticalChange);
}
```

**검증**: 우하단 핸들 드래그로 창 크기 변경됨. 최소 크기(400×200) 이하로 줄어들지 않음.

---

## T-1.10: WindowService 통합 테스트

**설명**: 위 기능들이 앱 시작 시 올바른 순서로 초기화되는지 확인하는 통합 점검을 수행한다.

**점검 시나리오**:
1. 앱 시작 → `ShowActivated=False` + `WS_EX_NOACTIVATE` 적용 확인
2. 메모장 열고 커서 위치 → AltKey 클릭 → 메모장 포커스 유지 확인
3. 드래그 핸들로 창 이동 → 종료 → 재시작 → 위치 유지 확인
4. 마우스 이탈 5초 → 반투명 → 재진입 → 불투명 확인
5. Acrylic 효과 적용 여부 육안 확인

**검증**: 5가지 시나리오 모두 수동 통과.
