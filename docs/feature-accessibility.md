# 기능 설계: 접근성 기능 추가

> **상태**: 사전 설계 완료 / 구현 대기  
> **대상 파일**: 신규 `Services/AccessibilityService.cs`, `Models/AppConfig.cs` 수정, `Themes/HighContrastTheme.xaml` 신규, `SettingsView.xaml` 수정

---

## 기능 개요

다양한 사용자의 접근성 요구를 지원한다. 시각/청각/운동 장애 사용자가 AltKey를 보다 쉽게 사용할 수 있도록 아래 기능들을 단계적으로 구현한다.

---

## 현재 코드 파악

### 관련 파일

| 파일 | 관련 내용 |
|------|-----------|
| `Services/ThemeService.cs` | 라이트/다크/시스템 테마 전환 — 고대비 테마 추가 지점 |
| `Services/SoundService.cs` | 키 클릭 사운드 (`SoundEnabled`, `SoundFilePath`) |
| `Models/AppConfig.cs` | 설정 저장 구조 |
| `Themes/DarkTheme.xaml` | 다크 테마 리소스 딕셔너리 — 고대비 테마의 참고 구조 |
| `Themes/LightTheme.xaml` | 라이트 테마 리소스 딕셔너리 |
| `Controls/KeyButton.xaml.cs` | 키 버튼 컨트롤 — AutomationProperties 추가 지점 |
| `Views/KeyboardView.xaml.cs` | `KeyUnit` 반응형 계산 — 텍스트 크기 조정 연계 가능 |
| `Platform/Win32.cs` | Win32 API 선언 — 추가 API 필요시 여기 |

### 기존 접근성 관련 요소

- `SoundEnabled`: 키 클릭 사운드 ON/OFF (이미 구현됨)
- `DwellEnabled`: 마우스 호버로 클릭 (이미 구현됨)
- `StickyKeysEnabled`: Sticky Keys (이미 구현됨)
- `OpacityIdle/Active`: 투명도 조절 (이미 구현됨)

---

## 구현할 접근성 기능 목록

### 우선순위 분류

| 기능 | 우선순위 | 난이도 | 설명 |
|------|---------|--------|------|
| 고대비 테마 (High Contrast) | 높음 | 낮음 | 흰 배경 + 굵은 검정 테두리 테마 |
| 키 라벨 읽기 (TTS) | 높음 | 중간 | 버튼 클릭/호버 시 텍스트 음성 출력 |
| 큰 텍스트 모드 | 중간 | 낮음 | KeyUnit 기반으로 폰트 크기 배율 조절 |
| Windows Narrator 지원 | 중간 | 낮음 | AutomationProperties 추가 |
| 색맹 친화 테마 | 낮음 | 중간 | 색상 구분 대신 패턴/아이콘 활용 |
| 커서 확대경 | 낮음 | 높음 | 마우스 커서 주변 확대 오버레이 |

---

## 기능 1: 고대비 테마 (High Contrast Theme)

### 목표
배경 흰색, 텍스트 검정, 테두리 굵고 진하게 — 저시력 사용자와 강한 시각적 대비가 필요한 환경용.

### 파일 추가: `Themes/HighContrastTheme.xaml`

기존 `DarkTheme.xaml` / `LightTheme.xaml`과 동일한 리소스 키 구조 사용.

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- 키보드 배경 -->
    <Color x:Key="BgColor">#FFFFFFFF</Color>
    <SolidColorBrush x:Key="KeyboardBg"  Color="#FFFFFFFF"/>
    <SolidColorBrush x:Key="KeyBg"       Color="#FFFFFFFF"/>
    <SolidColorBrush x:Key="KeyBgHover"  Color="#FFDDDDDD"/>
    <SolidColorBrush x:Key="KeyBgPress"  Color="#FFAAAAAA"/>
    <SolidColorBrush x:Key="KeyFg"       Color="#FF000000"/>
    <SolidColorBrush x:Key="KeyBorder"   Color="#FF000000"/>
    <Thickness       x:Key="KeyBorderThickness">2</Thickness>

    <!-- 특수 키 (Shift, Ctrl 등) -->
    <SolidColorBrush x:Key="ModifierBg"  Color="#FF000000"/>
    <SolidColorBrush x:Key="ModifierFg"  Color="#FFFFFFFF"/>
    <SolidColorBrush x:Key="StickyBg"    Color="#FF0000FF"/>
    <SolidColorBrush x:Key="LockedBg"    Color="#FF007700"/>

    <!-- 설정 창 -->
    <SolidColorBrush x:Key="SettingsBg"       Color="#FFFFFFFF"/>
    <SolidColorBrush x:Key="SettingsFg"       Color="#FF000000"/>
    <SolidColorBrush x:Key="SettingsFgSub"    Color="#FF333333"/>
    <SolidColorBrush x:Key="SettingsFgHint"   Color="#FF555555"/>
    <SolidColorBrush x:Key="SettingsHighlight" Color="#FF0000CC"/>
    <SolidColorBrush x:Key="SettingsBorder"   Color="#FF000000"/>
</ResourceDictionary>
```

### `ThemeService.cs` 수정

```csharp
// 기존: "light", "dark", "system"
// 추가: "highcontrast"
case "highcontrast":
    mergedDict.MergedDictionaries.Add(new ResourceDictionary
    {
        Source = new Uri("pack://application:,,,/Themes/HighContrastTheme.xaml")
    });
    break;
```

### `AppConfig.cs` 수정

```csharp
// Theme 속성의 허용값: "system" | "light" | "dark" | "highcontrast"
// 변경 없음 — 기존 string 타입으로 충분
```

### `SettingsView.xaml` 수정

```xml
<!-- 기존 테마 선택 ComboBox에 항목 추가 -->
<ComboBoxItem Content="고대비" Tag="highcontrast"/>
```

---

## 기능 2: 키 라벨 읽기 (TTS — Text-to-Speech)

### 목표
버튼을 클릭하거나 마우스를 올릴 때 키 라벨을 음성으로 읽어준다.

### 사용 API
`System.Speech.Synthesis.SpeechSynthesizer` — .NET 기본 포함 (Windows전용).  
NuGet 패키지 추가 불필요. (단, 프로젝트가 `net8.0-windows` 타겟이어야 함 — 이미 해당)

### 신규 서비스: `Services/AccessibilityService.cs`

```csharp
using System.Speech.Synthesis;

namespace AltKey.Services;

public class AccessibilityService : IDisposable
{
    private readonly SpeechSynthesizer _synth = new();
    private readonly ConfigService     _config;

    public AccessibilityService(ConfigService config)
    {
        _config = config;
        _synth.Rate   = 1;   // -10~10, 0=기본
        _synth.Volume = 80;  // 0~100
        // 한국어 음성 설치된 경우 자동 선택
        TrySetKoreanVoice();
    }

    /// 키 라벨 읽기 (TTS 활성화된 경우에만)
    public void SpeakLabel(string label)
    {
        if (!_config.Current.TtsEnabled) return;
        if (string.IsNullOrWhiteSpace(label)) return;
        _synth.SpeakAsyncCancelAll();  // 이전 발화 취소 후 새로 시작
        _synth.SpeakAsync(label);
    }

    private void TrySetKoreanVoice()
    {
        var koVoice = _synth.GetInstalledVoices()
            .FirstOrDefault(v => v.VoiceInfo.Culture.TwoLetterISOLanguageName == "ko");
        if (koVoice != null)
            _synth.SelectVoice(koVoice.VoiceInfo.Name);
    }

    public void Dispose() => _synth.Dispose();
}
```

### `KeyboardViewModel.cs` 수정

```csharp
// KeyPressed() 메서드에 추가
if (_configService.Current.TtsEnabled)
    _accessibilityService.SpeakLabel(slot.Label ?? slot.HangulLabel ?? "");
```

### `AppConfig.cs` 수정

```csharp
// 접근성 설정 그룹
public bool TtsEnabled          { get; set; } = false;  // 키 라벨 TTS
public bool TtsOnHover          { get; set; } = false;  // 마우스 호버 시 읽기 (클릭 대신)
public int  TtsRate             { get; set; } = 0;      // 말하기 속도 (-10~10)
```

### 한국어 TTS 설치 안내

Windows 10/11에서 한국어 TTS를 사용하려면:
1. 설정 → 시간 및 언어 → 언어 → 한국어 추가
2. 언어 팩의 "텍스트 음성 변환" 옵션 체크
3. 설치 후 AltKey 재시작

> 한국어 음성이 없으면 기본 영어 음성으로 발화됨 (fallback 자동 처리)

---

## 기능 3: 큰 텍스트 모드 (Large Text Mode)

### 목표
시력이 낮은 사용자를 위해 키 라벨 텍스트를 더 크게 표시한다.

### 구현 방법

`KeyUnit`에 배율을 적용하거나, 별도 `FontScaleFactor` 설정을 추가하여 `KeyButton`의 `FontSize`에 바인딩.

#### `AppConfig.cs` 수정

```csharp
public double FontScaleFactor { get; set; } = 1.0;  // 1.0=기본, 1.5=큰 텍스트, 2.0=매우 큰 텍스트
```

#### `KeyboardViewModel.cs` 수정

```csharp
// 기존 KeyUnit 계산 후 폰트 크기 노출
public double KeyFontSize => Math.Max(10, KeyUnit * 0.55 * _configService.Current.FontScaleFactor);
```

#### `Controls/KeyButton.xaml` 수정

```xml
<!-- FontSize를 KeyUnit 기반 계산에서 FontScaleFactor 적용으로 변경 -->
<TextBlock FontSize="{Binding KeyFontSize, RelativeSource=...}"/>
```

#### `SettingsView.xaml` 수정

```xml
<Slider Minimum="1.0" Maximum="2.0" Value="{Binding FontScaleFactor}" 
        TickFrequency="0.25" IsSnapToTickEnabled="True"/>
<TextBlock Text="{Binding FontScaleLabel}"/>  <!-- "보통 / 크게 / 매우 크게" -->
```

---

## 기능 4: Windows Narrator 지원 (AutomationProperties)

### 목표
Windows 스크린 리더(내레이터)가 AltKey 키 버튼을 인식하고 읽을 수 있도록 XAML AutomationProperties를 추가.

### `Controls/KeyButton.xaml` 수정

```xml
<Button AutomationProperties.Name="{Binding Label}"
        AutomationProperties.HelpText="{Binding HelpText}"
        AutomationProperties.AutomationId="{Binding VkCode}">
```

### `KeySlot.cs` 또는 ViewModel 수정

```csharp
// 키 버튼에 노출할 접근성 텍스트
public string AccessibilityLabel => HangulLabel ?? Label ?? "";
public string HelpText => Action switch
{
    SendKeyAction { Vk: var vk } => $"키: {vk}",
    BoilerplateAction { Text: var t } => $"입력: {t}",
    RunAppAction { Path: var p } => $"실행: {System.IO.Path.GetFileName(p)}",
    _ => ""
};
```

---

## 기능 5: 색맹 친화 테마 (Color Blind Friendly)

### 목표
색상으로만 구분되는 상태(Sticky/Locked 키 등)에 텍스트 표시 또는 패턴을 추가.

### 구현 방법

- Sticky 키: 배경색 변경 대신 테두리 굵기 + "S" 뱃지 오버레이
- Locked 키: 자물쇠 아이콘 오버레이
- 테마: `ColorBlindTheme.xaml` 추가 (파란색/빨간색 대신 기하학적 패턴 활용)

### `AppConfig.cs` 수정

```csharp
public bool ColorBlindMode { get; set; } = false;
```

---

## AppConfig.cs 최종 수정 내용 (접근성 전체)

```csharp
// ── 접근성 ──────────────────────────────────────────────
public bool   TtsEnabled        { get; set; } = false;
public bool   TtsOnHover        { get; set; } = false;
public int    TtsRate           { get; set; } = 0;
public double FontScaleFactor   { get; set; } = 1.0;
public bool   ColorBlindMode    { get; set; } = false;
// (고대비 테마는 기존 Theme = "highcontrast"로 처리)
```

---

## `SettingsViewModel.cs` 수정

```csharp
[ObservableProperty] private bool ttsEnabled;
[ObservableProperty] private bool ttsOnHover;
[ObservableProperty] private int  ttsRate;
[ObservableProperty] private double fontScaleFactor;
[ObservableProperty] private bool colorBlindMode;

partial void OnTtsEnabledChanged(bool v)      => _configService.Update(c => c.TtsEnabled = v);
partial void OnTtsOnHoverChanged(bool v)      => _configService.Update(c => c.TtsOnHover = v);
partial void OnTtsRateChanged(int v)          => _configService.Update(c => c.TtsRate = v);
partial void OnFontScaleFactorChanged(double v) => _configService.Update(c => c.FontScaleFactor = v);
partial void OnColorBlindModeChanged(bool v)  => _configService.Update(c => c.ColorBlindMode = v);
```

---

## `SettingsView.xaml` 신규 섹션

```xml
<!-- 접근성 섹션 (기존 설정 섹션들 하단에 추가) -->
<TextBlock Text="접근성" Style="{StaticResource SectionHeader}"/>

<ToggleButton Content="키 라벨 소리로 읽기 (TTS)"
              IsChecked="{Binding TtsEnabled}"/>
<ToggleButton Content="마우스 올릴 때 읽기"
              IsChecked="{Binding TtsOnHover}"
              IsEnabled="{Binding TtsEnabled}"/>
<StackPanel Orientation="Horizontal">
    <TextBlock Text="말하기 속도" VerticalAlignment="Center" Width="100"/>
    <Slider Minimum="-5" Maximum="5" Value="{Binding TtsRate}" Width="150"/>
</StackPanel>

<Separator/>

<StackPanel Orientation="Horizontal">
    <TextBlock Text="텍스트 크기" VerticalAlignment="Center" Width="100"/>
    <Slider Minimum="1.0" Maximum="2.0" Value="{Binding FontScaleFactor}"
            TickFrequency="0.25" IsSnapToTickEnabled="True" Width="150"/>
    <TextBlock Text="{Binding FontScaleLabel}" Margin="8,0,0,0"/>
</StackPanel>

<ToggleButton Content="색맹 친화 모드"
              IsChecked="{Binding ColorBlindMode}"/>
```

---

## `App.xaml.cs` 수정

```csharp
services.AddSingleton<AccessibilityService>();
```

---

## 구현 단계 체크리스트

### 고대비 테마
- [ ] `Themes/HighContrastTheme.xaml` 신규 작성 (DarkTheme 구조 복사 후 색상 수정)
- [ ] `ThemeService.cs`에 `"highcontrast"` 케이스 추가
- [ ] `SettingsView.xaml` ComboBox에 항목 추가

### TTS (키 라벨 읽기)
- [ ] `Services/AccessibilityService.cs` 신규 작성
- [ ] `App.xaml.cs` DI 등록
- [ ] `AppConfig.cs`에 `TtsEnabled`, `TtsOnHover`, `TtsRate` 추가
- [ ] `KeyboardViewModel.cs`에 `SpeakLabel` 호출 추가
- [ ] `SettingsViewModel.cs`에 TTS 바인딩 추가
- [ ] `SettingsView.xaml`에 TTS 토글/슬라이더 UI 추가

### 큰 텍스트 모드
- [ ] `AppConfig.cs`에 `FontScaleFactor` 추가
- [ ] `KeyboardViewModel.cs`에 `KeyFontSize` 계산 프로퍼티 추가
- [ ] `Controls/KeyButton.xaml`에 `FontSize` 바인딩 연결
- [ ] `SettingsView.xaml`에 폰트 배율 슬라이더 추가

### Windows Narrator 지원
- [ ] `Controls/KeyButton.xaml`에 `AutomationProperties.Name` 바인딩 추가
- [ ] 키 슬롯 ViewModel에 `AccessibilityLabel` 프로퍼티 추가

### 색맹 친화 모드 (선택적)
- [ ] `AppConfig.cs`에 `ColorBlindMode` 추가
- [ ] `Controls/KeyButton.xaml`에 Sticky/Locked 상태 아이콘/텍스트 오버레이 추가
- [ ] `SettingsView.xaml`에 색맹 모드 토글 추가

---

## 주의사항 및 기술 제약

| 항목 | 내용 |
|------|------|
| TTS 언어 팩 | 한국어 TTS는 Windows 언어 팩 설치 필요. 없으면 영어 fallback |
| `System.Speech` 스레드 | `SpeakAsync`는 별도 스레드에서 실행 — UI 스레드 블로킹 없음 |
| 고대비 테마와 Windows 고대비 | Windows 시스템 고대비 모드와 별개. 시스템 모드 감지 시 자동 적용 고려 가능 |
| `FontScaleFactor` 범위 | 너무 크면 키 라벨이 잘릴 수 있음 — `TextTrimming="CharacterEllipsis"` 적용 필요 |
| Narrator와 `WS_EX_NOACTIVATE` | 현재 키보드 창은 `WS_EX_NOACTIVATE` 적용됨 — Narrator가 포커스를 줄 수 없으므로 `AutomationProperties` 외에 `UIAutomation Provider` 구현이 필요할 수 있음 |
