# Phase 6: 배포 및 최적화

> 목표: .NET 8 Single-file 포터블 빌드, GitHub Actions CI/CD, 성능 최적화, 최종 통합 테스트를 완료한다.

**의존성**: Phase 0~5 완료

---

## T-6.1: Single-file 포터블 빌드 설정

**설명**: 런타임 포함 단일 exe 파일로 빌드되도록 `.csproj`와 `publish` 프로파일을 설정한다.

**파일**: `AltKey.csproj`, `Properties/PublishProfiles/portable.pubxml` (신규)

**portable.pubxml**:
```xml
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <Platform>AnyCPU</Platform>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishReadyToRun>true</PublishReadyToRun>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  </PropertyGroup>
</Project>
```

**빌드 명령어**:
```
dotnet publish -p:PublishProfile=portable
```

**예상 출력 크기**: 자체 포함 기준 50~80MB (런타임 포함), 시스템에 .NET 8 설치 시 10MB 이하

**검증**: `publish/` 폴더에 `AltKey.exe` 단일 파일 생성. .NET 없는 VM에서 정상 실행.

---

## T-6.2: 포터블 ZIP 패키지 스크립트

**설명**: 빌드 후 자동으로 포터블 배포용 ZIP을 생성하는 PowerShell 스크립트를 작성한다.

**파일**: `scripts/package-portable.ps1`

**스크립트 내용**:
```powershell
param([string]$Version = "0.1.0")

$out = "dist/altkey-portable-v$Version"
New-Item -ItemType Directory -Force $out | Out-Null

# 빌드
dotnet publish -p:PublishProfile=portable -o "$out"

# 기본 에셋 복사
Copy-Item -Recurse "layouts" "$out/layouts"
Copy-Item -Recurse "themes" "$out/themes" -ErrorAction SilentlyContinue

# 포터블 모드 트리거용 빈 config.json 생성 (exe 옆에 존재해야 포터블 모드)
$defaultConfig = @{version="1.0.0"; language="ko"; default_layout="qwerty-ko"} | ConvertTo-Json
$defaultConfig | Out-File "$out/config.json"

# ZIP 압축
Compress-Archive -Path "$out/*" -DestinationPath "dist/altkey-portable-v$Version.zip" -Force

Write-Host "✅ dist/altkey-portable-v$Version.zip 생성 완료"
```

**출력 구조**:
```
altkey-portable-vX.Y.Z.zip
├── AltKey.exe
├── config.json
└── layouts/
    ├── qwerty-ko.json
    └── qwerty-en.json
```

**검증**: `./scripts/package-portable.ps1 -Version 0.1.0` → ZIP 파일 생성, 압축 해제 후 실행 성공.

---

## T-6.3: 앱 아이콘 생성 및 적용

**설명**: AltKey 아이콘을 제작하고 빌드에 적용한다.

**파일**: `Assets/icon.ico`, `Assets/icon.png`, `Assets/tray-icon.ico`

**디자인 요구사항**:
- 배경: 둥근 사각형 (라운드 16dp)
- 전경: "Alt" 텍스트 또는 키보드 실루엣
- 색상: 다크/라이트 모두에서 식별 가능
- 사이즈: 16×16, 32×32, 48×48, 256×256 포함 ICO

**무료 도구**: Figma → ico 변환, 또는 ImageMagick:
```
magick icon.png -define icon:auto-resize=256,48,32,16 icon.ico
```

**적용**:
```xml
<!-- AltKey.csproj -->
<ApplicationIcon>Assets\icon.ico</ApplicationIcon>

<!-- 빌드 포함 -->
<ItemGroup>
  <Content Include="Assets\icon.ico" CopyToOutputDirectory="PreserveNewest"/>
  <Content Include="Assets\tray-icon.ico" CopyToOutputDirectory="PreserveNewest"/>
</ItemGroup>
```

**검증**: 빌드 후 .exe 파일 아이콘과 트레이 아이콘이 모두 표시됨.

---

## T-6.4: GitHub Release 자동 업데이트 체크

**설명**: 앱 시작 시 GitHub API를 조회하여 새 버전이 있으면 상단 배너로 알린다.

**파일**: `Services/UpdateService.cs` (신규)

**구현 내용**:
```csharp
public class UpdateService
{
    private const string ApiUrl =
        "https://api.github.com/repos/{owner}/altkey/releases/latest";

    public async Task<(bool hasUpdate, string version, string url)> CheckAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AltKey");
            var json = await client.GetStringAsync(ApiUrl);
            var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString()!;
            var htmlUrl = doc.RootElement.GetProperty("html_url").GetString()!;

            var current = Assembly.GetExecutingAssembly().GetName().Version!;
            var remote = Version.Parse(tag.TrimStart('v'));

            return (remote > current, tag, htmlUrl);
        }
        catch { return (false, "", ""); }
    }
}
```

**UI 배너**:
```xml
<!-- KeyboardView.xaml 상단 -->
<Border x:Name="UpdateBanner" Visibility="Collapsed"
        Background="#FF2563EB" Padding="8,4">
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="🆙 새 버전이 있습니다: " Foreground="White"/>
        <TextBlock x:Name="UpdateVersion" Foreground="White" FontWeight="Bold"/>
        <Button Content="다운로드" Click="OpenReleasePage" Margin="8,0,0,0"/>
        <Button Content="✕" Click="DismissUpdate" Margin="4,0,0,0"/>
    </StackPanel>
</Border>
```

**검증**: 버전 번호를 낮춰 테스트 시 배너 표시됨.

---

## T-6.5: GitHub Actions — 자동 빌드 및 릴리즈

**설명**: PR 빌드 체크와 태그 푸시 시 릴리즈 배포를 자동화한다.

**파일**: `.github/workflows/ci.yml`, `.github/workflows/release.yml`

**ci.yml**:
```yaml
name: CI
on: [push, pull_request]
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet build -c Release
      - run: dotnet test
```

**release.yml**:
```yaml
name: Release
on:
  push:
    tags: ['v*']
jobs:
  release:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet publish -p:PublishProfile=portable
      - run: powershell scripts/package-portable.ps1 -Version ${{ github.ref_name }}
      - uses: softprops/action-gh-release@v2
        with:
          files: dist/altkey-portable-*.zip
```

**검증**: `git tag v0.1.0 && git push --tags` → GitHub Release에 ZIP 파일 자동 업로드.

---

## T-6.6: 메모리 및 성능 점검

**설명**: 릴리즈 빌드에서 메모리 사용량과 키 입력 지연을 측정하고 목표치를 확인한다.

**목표치**:
- 유휴 메모리: < 50MB (작업 관리자 기준)
- 키 입력 지연 (클릭 → SendInput 호출): < 10ms
- 앱 시작 시간 (첫 창 표시까지): < 2초

**점검 방법**:
1. 릴리즈 빌드로 실행 후 작업 관리자에서 메모리 확인
2. 키 클릭 핸들러 시작/끝에 `Stopwatch` 삽입, Debug 로그 출력
3. 앱 시작 시 `Program.cs`의 `Environment.TickCount64` 로 측정

**검증**: 세 가지 목표치 모두 달성 확인.

---

## T-6.7: 에러 처리 및 로깅

**설명**: 앱 전체에 구조화된 예외 처리와 파일 로깅을 적용한다.

**파일**: `App.xaml.cs`

**구현 내용**:
```csharp
// 미처리 예외 전역 핸들러
Application.Current.DispatcherUnhandledException += (s, e) =>
{
    File.AppendAllText("altkey-error.log",
        $"[{DateTime.Now:u}] {e.Exception}\n");
    MessageBox.Show($"예기치 않은 오류: {e.Exception.Message}\n로그: altkey-error.log");
    e.Handled = true;
};
```

- 레이아웃 파싱 실패: UI 알림 + 기본 레이아웃으로 폴백
- WinEventHook 실패: 조용히 무시, 트레이 툴팁에 "앱 감지 비활성" 표시
- SendInput 실패: 권한 안내 배너 표시 (T-2.10 참조)

**검증**: 잘못된 레이아웃 JSON 파일 배치 → 에러 로그 기록 + 기본 레이아웃 동작.

---

## T-6.8: 최종 통합 테스트 체크리스트 실행

**설명**: 배포 전 포터블 빌드에서 전 기능을 수동으로 검증한다.

**파일**: `TESTING.md` (문서화)

**체크리스트**:
- [ ] 포터블 exe 실행 → Acrylic 배경 키보드 표시
- [ ] 메모장 열고 키 클릭 → 입력 전달 (포커스 유지 확인)
- [ ] Shift 고정 → 대문자 입력 → 자동 해제
- [ ] Shift 더블클릭 → 영구 잠금 → 재클릭으로 해제
- [ ] 한/영 키 → IME 전환
- [ ] 드래그 핸들로 창 이동 → 종료 → 재시작 → 위치 유지
- [ ] 리사이즈 핸들로 창 크기 변경
- [ ] 마우스 이탈 5초 → 반투명 → 재진입 → 불투명
- [ ] Ctrl+Alt+K → 창 토글 (창이 다른 앱 뒤에 있어도 동작)
- [ ] 트레이 아이콘 표시 → 우클릭 메뉴 동작
- [ ] 트레이 더블클릭 → 창 복귀
- [ ] 다크/라이트 테마 전환
- [ ] 레이아웃 드롭다운으로 qwerty-en 전환
- [ ] 체류 클릭 on → 키 위 800ms → 입력 발생, 프로그레스 링 표시
- [ ] .NET 미설치 환경(VM) 에서 포터블 exe 실행 성공
- [ ] 관리자 권한 앱 대상 시 경고 배너 표시
- [ ] 앱 최소화 → 트레이 이동 (태스크바 미표시)

**검증**: 모든 항목 수동 통과.
