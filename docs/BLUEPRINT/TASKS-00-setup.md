# Phase 0: 프로젝트 초기화

> 목표: C# .NET 8 WPF 프로젝트를 생성하고, 핵심 의존성을 설치하며, 빌드 가능한 빈 창을 띄운다.

---

## T-0.1: WPF 프로젝트 생성

**설명**: .NET 8 WPF 프로젝트를 생성하고 기본 구조를 확인한다.

**명령어**:
```
dotnet new wpf -n AltKey -f net8.0-windows
cd AltKey
dotnet run
```

**검증**: 기본 WPF 창(흰 배경)이 뜬다.

---

## T-0.2: NuGet 패키지 추가

**설명**: 핵심 의존성 패키지를 설치한다.

**명령어**:
```
dotnet add package WPF-UI --version 3.*
dotnet add package CommunityToolkit.Mvvm --version 8.*
dotnet add package Microsoft.Extensions.DependencyInjection --version 8.*
```

**각 패키지 역할**:
- `WPF-UI` — Acrylic/Mica 블러, 모던 컨트롤(FluentWindow, NavigationView 등)
- `CommunityToolkit.Mvvm` — `[ObservableProperty]`, `[RelayCommand]` source generator
- `Microsoft.Extensions.DependencyInjection` — DI 컨테이너 (Service locator 없이 생성자 주입)

**검증**: `dotnet build` 성공, `.csproj`에 세 패키지 기록됨.

---

## T-0.3: .csproj 설정 최적화

**설명**: 프로젝트 파일에 빌드 관련 설정을 추가한다.

**파일**: `AltKey.csproj`

**추가할 설정**:
```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net8.0-windows</TargetFramework>
  <UseWPF>true</UseWPF>
  <UseWindowsForms>true</UseWindowsForms>  <!-- NotifyIcon 사용 -->
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <ApplicationManifest>app.manifest</ApplicationManifest>
  <ApplicationIcon>Assets/icon.ico</ApplicationIcon>
  <!-- Single-file publish용 (Phase 6에서 활성화) -->
  <!-- <PublishSingleFile>true</PublishSingleFile> -->
  <!-- <SelfContained>true</SelfContained> -->
</PropertyGroup>
```

**검증**: `dotnet build -c Release` 성공.

---

## T-0.4: app.manifest 작성 (DPI + UAC)

**설명**: DPI 인식 설정과 UAC 수준을 manifest 파일로 선언한다.

**파일**: `app.manifest` (프로젝트 루트)

**내용**:
```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="AltKey"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <!-- asInvoker: 일반 권한 실행 (SendInput 제약 있음, 별도 문서화) -->
        <requestedExecutionLevel level="asInvoker" uiAccess="false"/>
      </requestedPrivileges>
    </security>
  </trustInfo>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">
        PerMonitorV2
      </dpiAwareness>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">
        True/PM
      </dpiAware>
    </windowsSettings>
  </application>
</assembly>
```

**핵심 포인트**:
- `PerMonitorV2` — 모니터별 DPI 스케일링 자동 처리
- `asInvoker` — 관리자 권한 없이 실행 (SendInput 권한 제약 문서화 필요)

**검증**: 빌드 후 고DPI 모니터에서 창이 흐릿하지 않고 선명하게 표시됨.

---

## T-0.5: 디렉토리 구조 생성 및 빈 파일 배치

**설명**: BLUEPRINT에 정의된 디렉토리 구조와 빈 파일들을 생성한다.

**생성할 경로**:
```
Views/KeyboardView.xaml + .cs
Views/SettingsView.xaml + .cs
ViewModels/MainViewModel.cs
ViewModels/KeyboardViewModel.cs
ViewModels/SettingsViewModel.cs
Models/LayoutConfig.cs
Models/KeySlot.cs
Models/KeyAction.cs
Models/VirtualKeyCode.cs
Models/AppConfig.cs
Services/InputService.cs
Services/WindowService.cs
Services/LayoutService.cs
Services/ConfigService.cs
Services/ProfileService.cs
Services/TrayService.cs
Controls/KeyButton.xaml + .cs
Platform/Win32.cs
Themes/Generic.xaml
Themes/LightTheme.xaml
Themes/DarkTheme.xaml
Themes/Converters.cs
Assets/                        (아이콘 등 추후 추가)
layouts/                       (JSON 레이아웃 추후 추가)
```

각 .cs 파일 최소 내용: `namespace AltKey.{폴더명}; // TODO`

**검증**: `dotnet build` 시 모든 파일이 컴파일 대상으로 인식됨 (에러 없음).

---

## T-0.6: DI 컨테이너 구성 (App.xaml.cs)

**설명**: `App.xaml.cs`에서 모든 Service와 ViewModel을 DI 컨테이너에 등록한다.

**파일**: `App.xaml.cs`

**구현 내용**:
```csharp
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();

        // Services
        services.AddSingleton<ConfigService>();
        services.AddSingleton<LayoutService>();
        services.AddSingleton<InputService>();
        services.AddSingleton<WindowService>();
        services.AddSingleton<ProfileService>();
        services.AddSingleton<TrayService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<KeyboardViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

        var window = Services.GetRequiredService<MainWindow>();
        window.Show();
    }
}
```

**검증**: `App.Services.GetRequiredService<MainWindow>()` 가 정상 반환됨.

---

## T-0.7: Git 저장소 초기화 및 .gitignore 설정

**설명**: Git 저장소를 초기화하고 .NET 프로젝트용 .gitignore를 설정한다.

**명령어**:
```
git init
dotnet new gitignore    # .NET 표준 .gitignore 자동 생성
git add -A
git commit -m "chore: initial WPF .NET 8 scaffold"
```

추가로 `.gitignore`에 수동으로 추가:
```
config.json     # 사용자 설정 (개인화 파일, 커밋 제외)
layouts/*.json  # 기본 레이아웃은 포함하되 사용자 커스텀은 제외
```

**검증**: `git log --oneline`에 첫 커밋 표시됨.

---

## T-0.8: README.md 작성

**설명**: 개발 환경 요구사항과 빌드/실행 방법을 README에 기록한다.

**파일**: `README.md`

**포함 내용**:
- 사전 요구사항: .NET 8 SDK, Windows 10 22H2+ (Acrylic 권장)
- 개발 실행: `dotnet run`
- 릴리즈 빌드: `dotnet build -c Release`
- 포터블 배포: `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`
- 알려진 제약: SendInput 권한 한계 (관리자 앱/게임 미지원)
- 프로젝트 구조 개요

**검증**: README 파일 존재, 명령어 정확성 확인.
