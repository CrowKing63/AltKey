# Phase 4: UI/UX 디자인

> 목표: WPF ResourceDictionary 기반 테마 시스템, Acrylic 배경, 키 애니메이션, 고정 키 인디케이터를 구현한다.

**의존성**: Phase 3 (키보드 렌더링 동작)

---

## T-4.1: WPF ResourceDictionary 테마 구조 설계

**설명**: 색상, 크기, 폰트 등을 ResourceDictionary로 정의하여 테마 전환을 리소스 교체만으로 가능하게 한다.

**파일**: `Themes/Generic.xaml`

**구현 내용**:
```xml
<ResourceDictionary xmlns="...">
    <!-- 크기 -->
    <sys:Double x:Key="KeyUnit">48</sys:Double>
    <sys:Double x:Key="KeyGap">4</sys:Double>
    <CornerRadius x:Key="KeyRadius">8</CornerRadius>
    <CornerRadius x:Key="KeyboardRadius">16</CornerRadius>
    <Thickness x:Key="KeyboardPadding">12</Thickness>

    <!-- 폰트 -->
    <FontFamily x:Key="KeyFont">Segoe UI Variable, Segoe UI, sans-serif</FontFamily>
    <sys:Double x:Key="KeyFontSize">13</sys:Double>
    <sys:Double x:Key="KeyFontSizeSmall">10</sys:Double>

    <!-- 애니메이션 시간 -->
    <Duration x:Key="TransitionFast">0:0:0.12</Duration>
    <Duration x:Key="TransitionNormal">0:0:0.20</Duration>
</ResourceDictionary>
```

모든 색상은 테마 파일(LightTheme.xaml / DarkTheme.xaml)에서 정의.

**검증**: `{StaticResource KeyUnit}` 이 48로 해석됨.

---

## T-4.2: 다크 테마 ResourceDictionary 작성

**파일**: `Themes/DarkTheme.xaml`

**구현 내용**:
```xml
<ResourceDictionary>
    <SolidColorBrush x:Key="KeyboardBg"   Color="#CC1E1E1E"/>
    <SolidColorBrush x:Key="KeyBg"        Color="#CC3C3C3C"/>
    <SolidColorBrush x:Key="KeyBgHover"   Color="#CC505050"/>
    <SolidColorBrush x:Key="KeyBgPressed" Color="#FF2D2D2D"/>
    <SolidColorBrush x:Key="KeyBgSticky"  Color="#663B82F6"/>
    <SolidColorBrush x:Key="KeyFg"        Color="#FFE4E4E4"/>
    <SolidColorBrush x:Key="KeyFgSticky"  Color="#FF93C5FD"/>
    <SolidColorBrush x:Key="KeyBorder"    Color="#0FFFFFFF"/>
    <SolidColorBrush x:Key="KeyShadow"    Color="#66000000"/>
</ResourceDictionary>
```

**검증**: 다크 테마 적용 시 키 배경이 어두운 색으로 표시됨.

---

## T-4.3: 라이트 테마 ResourceDictionary 작성

**파일**: `Themes/LightTheme.xaml`

**구현 내용**:
```xml
<ResourceDictionary>
    <SolidColorBrush x:Key="KeyboardBg"   Color="#CCFFFFFF"/>
    <SolidColorBrush x:Key="KeyBg"        Color="#E6FFFFFF"/>
    <SolidColorBrush x:Key="KeyBgHover"   Color="#F0E6E6E6"/>
    <SolidColorBrush x:Key="KeyBgPressed" Color="#FFC8C8C8"/>
    <SolidColorBrush x:Key="KeyBgSticky"  Color="#4D3B82F6"/>
    <SolidColorBrush x:Key="KeyFg"        Color="#FF1A1A1A"/>
    <SolidColorBrush x:Key="KeyFgSticky"  Color="#FF1D4ED8"/>
    <SolidColorBrush x:Key="KeyBorder"    Color="#14000000"/>
    <SolidColorBrush x:Key="KeyShadow"    Color="#1E000000"/>
</ResourceDictionary>
```

**검증**: 라이트 테마 적용 시 키 배경이 밝은 색으로 표시됨.

---

## T-4.4: 테마 전환 서비스

**설명**: 런타임에 테마 ResourceDictionary를 교체하는 서비스를 작성한다.

**파일**: `Themes/Converters.cs` → 분리: 별도 `Services/ThemeService.cs` 작성 권장

**구현 내용**:
```csharp
public class ThemeService
{
    private const string ThemeDictKey = "ActiveTheme";

    public void Apply(string theme) // "light" | "dark" | "system"
    {
        var resolved = theme == "system" ? DetectSystemTheme() : theme;
        var uri = new Uri($"pack://application:,,,/Themes/{resolved}Theme.xaml");
        var dict = new ResourceDictionary { Source = uri };

        var merged = Application.Current.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d => d.Source?.ToString().Contains("Theme") == true);
        if (existing is not null) merged.Remove(existing);
        merged.Add(dict);
    }

    private static string DetectSystemTheme()
    {
        // HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize
        // AppsUseLightTheme: 0 = 다크, 1 = 라이트
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var val = key?.GetValue("AppsUseLightTheme");
        return val is int i && i == 0 ? "dark" : "light";
    }
}
```

**검증**: `themeService.Apply("dark")` 호출 시 키 배경이 즉시 어두워짐.

---

## T-4.5: Windows 시스템 테마 변경 자동 감지

**설명**: Windows 테마 변경 이벤트를 구독하여 AltKey 테마를 실시간으로 전환한다.

**파일**: `Services/ThemeService.cs`

**구현 내용**:
```csharp
// SystemParameters.StaticPropertyChanged 이벤트 활용
SystemParameters.StaticPropertyChanged += (s, e) =>
{
    if (e.PropertyName == nameof(SystemParameters.HighContrast)
        || e.PropertyName == "WindowGlassColor")
    {
        if (_config.Theme == "system")
            Apply("system");
    }
};
```

또는 레지스트리 감시(`RegistryMonitor` 라이브러리 사용).

**검증**: Windows 설정에서 다크→라이트 전환 시 AltKey 테마도 자동 변경.

---

## T-4.6: KeyButton ControlTemplate (WPF 스타일)

**설명**: `KeyButton` 컨트롤의 WPF ControlTemplate을 작성한다. Hover/Pressed 상태 트리거와 Sticky 상태 시각화를 포함한다.

**파일**: `Controls/KeyButton.xaml`

**구현 내용**:
```xml
<Style TargetType="controls:KeyButton">
    <Setter Property="Background" Value="{StaticResource KeyBg}"/>
    <Setter Property="Foreground" Value="{StaticResource KeyFg}"/>
    <Setter Property="BorderBrush" Value="{StaticResource KeyBorder}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="FontFamily" Value="{StaticResource KeyFont}"/>
    <Setter Property="FontSize"   Value="{StaticResource KeyFontSize}"/>
    <Setter Property="Margin"     Value="2"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="controls:KeyButton">
                <Border x:Name="Root"
                        Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="{StaticResource KeyRadius}"
                        RenderTransformOrigin="0.5,0.5">
                    <Border.RenderTransform>
                        <ScaleTransform x:Name="Scale"/>
                    </Border.RenderTransform>
                    <ContentPresenter HorizontalAlignment="Center"
                                      VerticalAlignment="Center"/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="Root" Property="Background"
                                Value="{StaticResource KeyBgHover}"/>
                        <Trigger.EnterActions>
                            <BeginStoryboard>
                                <Storyboard>
                                    <DoubleAnimation Storyboard.TargetName="Scale"
                                        Storyboard.TargetProperty="ScaleX"
                                        To="1.04" Duration="0:0:0.1"/>
                                    <DoubleAnimation Storyboard.TargetName="Scale"
                                        Storyboard.TargetProperty="ScaleY"
                                        To="1.04" Duration="0:0:0.1"/>
                                </Storyboard>
                            </BeginStoryboard>
                        </Trigger.EnterActions>
                        <Trigger.ExitActions>
                            <BeginStoryboard>
                                <Storyboard>
                                    <DoubleAnimation Storyboard.TargetName="Scale"
                                        Storyboard.TargetProperty="ScaleX"
                                        To="1.0" Duration="0:0:0.1"/>
                                    <DoubleAnimation Storyboard.TargetName="Scale"
                                        Storyboard.TargetProperty="ScaleY"
                                        To="1.0" Duration="0:0:0.1"/>
                                </Storyboard>
                            </BeginStoryboard>
                        </Trigger.ExitActions>
                    </Trigger>
                    <Trigger Property="IsPressed" Value="True">
                        <Setter TargetName="Root" Property="Background"
                                Value="{StaticResource KeyBgPressed}"/>
                        <Trigger.EnterActions>
                            <BeginStoryboard>
                                <Storyboard>
                                    <DoubleAnimation Storyboard.TargetName="Scale"
                                        Storyboard.TargetProperty="ScaleX"
                                        To="0.93" Duration="0:0:0.05"/>
                                    <DoubleAnimation Storyboard.TargetName="Scale"
                                        Storyboard.TargetProperty="ScaleY"
                                        To="0.93" Duration="0:0:0.05"/>
                                </Storyboard>
                            </BeginStoryboard>
                        </Trigger.EnterActions>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

**검증**: 키 hover 시 살짝 확대, 클릭 시 눌리는 scale 애니메이션 육안 확인.

---

## T-4.7: 고정 키 시각적 상태 (Sticky 스타일)

**설명**: Sticky 상태인 키에 별도 배경색과 테두리를 적용하는 트리거를 추가한다.

**파일**: `Controls/KeyButton.xaml`

**구현 내용**:
- `KeyButton`에 `IsSticky` DependencyProperty 추가
- `IsSticky=True` 트리거에서 `Background="{StaticResource KeyBgSticky}"`, `Foreground="{StaticResource KeyFgSticky}"` 적용
- `LockedKeys`에 있는 키는 추가로 작은 자물쇠 아이콘(TextBlock 오버레이)

**검증**: Shift 고정 시 Shift 키 배경이 파란색으로 변경됨.

---

## T-4.8: 드래그 핸들 스타일

**설명**: 키보드 상단 드래그 핸들 영역을 미니멀하게 스타일링한다.

**파일**: `Views/KeyboardView.xaml`

**구현 내용**:
```xml
<Border x:Name="DragHandle"
        Height="22"
        Background="Transparent"
        Cursor="SizeAll"
        MouseLeftButtonDown="DragHandle_MouseLeftButtonDown">
    <Rectangle Width="36" Height="4"
               Fill="{StaticResource KeyFg}"
               Opacity="0.25"
               RadiusX="2" RadiusY="2"
               HorizontalAlignment="Center" VerticalAlignment="Center"/>
</Border>
```

Hover 시 Opacity 상승 (`EventTrigger` 또는 코드비하인드로 처리).

**검증**: 드래그 핸들이 키보드 상단에 세련되게 표시됨.

---

## T-4.9: 키보드 진입 애니메이션

**설명**: 앱 시작 시 키보드가 아래에서 올라오며 나타나는 슬라이드+페이드 애니메이션을 적용한다.

**파일**: `MainWindow.cs`

**구현 내용**:
```csharp
private void PlayOpenAnimation()
{
    // 초기 상태
    Opacity = 0;
    RenderTransform = new TranslateTransform(0, 24);

    var sb = new Storyboard();

    var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(280)));
    Storyboard.SetTarget(fadeIn, this);
    Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));

    var slideUp = new DoubleAnimation(24, 0, new Duration(TimeSpan.FromMilliseconds(300)));
    slideUp.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
    Storyboard.SetTarget(slideUp, this);
    Storyboard.SetTargetProperty(slideUp,
        new PropertyPath("RenderTransform.(TranslateTransform.Y)"));

    sb.Children.Add(fadeIn);
    sb.Children.Add(slideUp);
    sb.Begin();
}
```

**검증**: 앱 시작 시 키보드가 부드럽게 올라오며 나타남.

---

## T-4.10: 반응형 키 크기 (창 리사이즈 대응)

**설명**: 창 크기 변경 시 키 크기가 비례적으로 조정되도록 한다.

**파일**: `ViewModels/KeyboardViewModel.cs`

**구현 내용**:
```csharp
// 창 너비 변경 시 KeyUnit 재계산
public void OnWindowSizeChanged(double newWidth)
{
    const double minUnit = 32.0;
    const double maxUnit = 64.0;
    const double baseWidth = 900.0;
    const double baseUnit = 48.0;

    var unit = baseUnit * (newWidth / baseWidth);
    KeyUnit = Math.Clamp(unit, minUnit, maxUnit);
}

[ObservableProperty] private double keyUnit = 48.0;
```

- `MainWindow.SizeChanged` 이벤트에서 `keyboardViewModel.OnWindowSizeChanged(ActualWidth)` 호출
- `KeyButton.Width`가 `{Binding KeyUnit, RelativeSource=...}` 와 `slot.Width` 곱으로 바인딩

**검증**: 창을 좁게 줄이면 키가 작아지고, 최소(32)·최대(64) 범위 내로 제한됨.
