# Phase 9: 고급 기능

> **난이도**: ★★★ 어려움  
> **목표**: 복잡한 신규 기능을 추가한다. 각 태스크가 여러 파일에 걸쳐 있으며 설계 결정이 필요하다.  
> **의존성**: Phase 0~8 완료

---

## T-9.1: 커스텀 액션 키 — 새 KeyAction 타입 정의

**설명**: 기존 `SendKey`, `SendCombo`, `ToggleSticky`, `SwitchLayout` 외에 다음 액션 타입을 추가한다:
- `RunApp` — 애플리케이션 실행
- `Boilerplate` — 상용구 텍스트 입력
- `ShellCommand` — 터미널 명령 실행
- `VolumeControl` — 볼륨 조정
- `ClipboardPaste` — 지정 텍스트 클립보드에 복사 후 붙여넣기

### 1단계: KeyAction 모델 확장

**파일**: `AltKey/Models/KeyAction.cs`

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SendKeyAction),      "SendKey")]
[JsonDerivedType(typeof(SendComboAction),    "SendCombo")]
[JsonDerivedType(typeof(ToggleStickyAction), "ToggleSticky")]
[JsonDerivedType(typeof(SwitchLayoutAction), "SwitchLayout")]
// ── 추가 ──────────────────────────────────────────────────────
[JsonDerivedType(typeof(RunAppAction),       "RunApp")]
[JsonDerivedType(typeof(BoilerplateAction),  "Boilerplate")]
[JsonDerivedType(typeof(ShellCommandAction), "ShellCommand")]
[JsonDerivedType(typeof(VolumeControlAction),"VolumeControl")]
[JsonDerivedType(typeof(ClipboardPasteAction),"ClipboardPaste")]
public abstract record KeyAction;

// 기존
public record SendKeyAction(string Vk) : KeyAction;
public record SendComboAction(List<string> Keys) : KeyAction;
public record ToggleStickyAction(string Vk) : KeyAction;
public record SwitchLayoutAction(string Name) : KeyAction;

// 신규
/// 애플리케이션 실행
/// path: 실행 파일 경로 (예: "C:\\Windows\\notepad.exe")
/// args: 실행 인수 (선택, 기본 "")
public record RunAppAction(string Path, string Args = "") : KeyAction;

/// 상용구 텍스트를 유니코드로 직접 입력
/// text: 입력할 텍스트
public record BoilerplateAction(string Text) : KeyAction;

/// 셸 명령 실행 (cmd.exe /c 또는 powershell.exe -c)
/// command: 실행할 명령어 문자열
/// shell: "cmd" | "powershell" (기본 "cmd")
/// hidden: true면 콘솔 창 숨김 (기본 true)
public record ShellCommandAction(string Command, string Shell = "cmd", bool Hidden = true) : KeyAction;

/// 볼륨 조정
/// direction: "up" | "down" | "mute"
/// step: 조정 단계 (1~100, 기본 5)
public record VolumeControlAction(string Direction, int Step = 5) : KeyAction;

/// 지정 텍스트를 클립보드에 복사 후 Ctrl+V로 붙여넣기
/// text: 붙여넣을 텍스트
public record ClipboardPasteAction(string Text) : KeyAction;
```

### 2단계: InputService에 핸들러 추가

**파일**: `AltKey/Services/InputService.cs`

`HandleAction(KeyAction action)` 의 `switch` 구문에 추가:

```csharp
case RunAppAction { Path: var path, Args: var args }:
    try { Process.Start(new ProcessStartInfo(path, args) { UseShellExecute = true }); }
    catch (Exception ex) { Log.Warning(ex, "RunApp 실패: {path}", path); }
    break;

case BoilerplateAction { Text: var text }:
    SendUnicode(text);  // T-8.3에서 구현한 메서드
    break;

case ShellCommandAction { Command: var cmd, Shell: var shell, Hidden: var hidden }:
    var shellExe = shell == "powershell" ? "powershell.exe" : "cmd.exe";
    var shellArg = shell == "powershell" ? $"-Command \"{cmd}\"" : $"/c \"{cmd}\"";
    var psi = new ProcessStartInfo(shellExe, shellArg)
    {
        UseShellExecute = false,
        CreateNoWindow = hidden
    };
    try { Process.Start(psi); }
    catch (Exception ex) { Log.Warning(ex, "ShellCommand 실패: {cmd}", cmd); }
    break;

case VolumeControlAction { Direction: var dir, Step: var step }:
    HandleVolumeControl(dir, step);
    break;

case ClipboardPasteAction { Text: var text2 }:
    Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(text2));
    SendCombo([VirtualKeyCode.VK_CONTROL, VirtualKeyCode.VK_V]);
    break;
```

볼륨 제어는 Win32 `SendInput`에 `VK_VOLUME_UP`/`VK_VOLUME_DOWN`/`VK_VOLUME_MUTE`를 사용한다:

```csharp
private void HandleVolumeControl(string direction, int step)
{
    var vk = direction switch
    {
        "up"   => (ushort)0xAF, // VK_VOLUME_UP
        "down" => (ushort)0xAE, // VK_VOLUME_DOWN
        "mute" => (ushort)0xAD, // VK_VOLUME_MUTE
        _ => (ushort)0
    };
    if (vk == 0) return;

    // step 횟수만큼 반복 전송 (볼륨 키는 보통 2씩 변화)
    for (int i = 0; i < step / 2; i++)
    {
        SendKeyPress((VirtualKeyCode)vk);
    }
}
```

### 3단계: JSON 레이아웃 예시

커스텀 레이아웃 파일 `layouts/custom.json`에서 이 액션들을 사용:
```json
{
  "name": "커스텀 액션",
  "rows": [
    {
      "keys": [
        { "label": "메모장", "action": { "type": "RunApp", "Path": "notepad.exe" }, "width": 2.0 },
        { "label": "볼업",   "action": { "type": "VolumeControl", "Direction": "up", "Step": 10 }, "width": 1.5 },
        { "label": "볼다운", "action": { "type": "VolumeControl", "Direction": "down", "Step": 10 }, "width": 1.5 },
        { "label": "음소거", "action": { "type": "VolumeControl", "Direction": "mute" }, "width": 1.5 }
      ]
    },
    {
      "keys": [
        { "label": "서명",   "action": { "type": "Boilerplate", "Text": "홍길동 드림" }, "width": 2.0 },
        { "label": "종료예약","action": { "type": "ShellCommand", "Command": "shutdown /s /t 300", "Shell": "cmd" }, "width": 3.0 }
      ]
    }
  ]
}
```

**검증**:
- `RunApp` → 메모장 실행됨
- `Boilerplate` → 포커스된 앱에 텍스트 입력됨
- `VolumeControl` → 시스템 볼륨 변화 확인
- `ShellCommand` → 5분 후 종료 예약됨

---

## T-9.2: 커스텀 액션 키 — 설정 UI (액션 빌더)

**설명**: 설정 패널에서 커스텀 키를 시각적으로 추가/편집할 수 있는 UI를 제공한다.
레이아웃 JSON을 직접 편집하지 않아도 된다.

### 설계

- 설정 → "커스텀 키 편집" 진입
- 현재 레이아웃의 키 목록 표시 (행/열 구조)
- 각 키 클릭 시 사이드 패널에서 편집:
  - 라벨 텍스트
  - 액션 타입 선택 (드롭다운)
  - 액션별 파라미터 입력 폼
- "빈 키 추가" / "키 삭제" 버튼
- "저장" → 레이아웃 JSON 파일 덮어쓰기

### 구현 순서

1. **ActionBuilderViewModel**: 편집 중인 키 슬롯 상태 관리
   - `SelectedActionType`: enum (SendKey, RunApp, Boilerplate, ShellCommand, ...)
   - 각 타입별 파라미터 프로퍼티
   - `BuildAction()`: 현재 입력값으로 `KeyAction` 객체 생성

2. **ActionBuilderView.xaml**: 액션 타입별 조건부 폼 (Visibility 토글)
   ```xml
   <!-- 액션 타입 선택 -->
   <ComboBox ItemsSource="{Binding ActionTypes}" SelectedItem="{Binding SelectedActionType}"/>

   <!-- RunApp 폼 (선택 시 표시) -->
   <StackPanel Visibility="{Binding IsRunApp, Converter={StaticResource BoolToVis}}">
       <TextBox Text="{Binding AppPath}" PlaceholderText="실행 파일 경로"/>
       <Button Content="찾아보기" Command="{Binding BrowseAppCommand}"/>
       <TextBox Text="{Binding AppArgs}" PlaceholderText="실행 인수 (선택)"/>
   </StackPanel>

   <!-- Boilerplate 폼 -->
   <TextBox Visibility="{Binding IsBoilerplate, Converter={StaticResource BoolToVis}}"
            Text="{Binding BoilerplateText}" AcceptsReturn="True" Height="60"
            PlaceholderText="입력할 텍스트"/>

   <!-- ShellCommand 폼 -->
   <StackPanel Visibility="{Binding IsShellCommand, Converter={StaticResource BoolToVis}}">
       <TextBox Text="{Binding ShellCmd}" PlaceholderText="명령어 (예: shutdown /s /t 300)"/>
       <ComboBox ItemsSource="{Binding ShellTypes}" SelectedItem="{Binding SelectedShell}"/>
   </StackPanel>
   ```

3. **LayoutService에 저장 메서드 추가**:
   ```csharp
   public void Save(string name, LayoutConfig config)
   {
       var path = Path.Combine(LayoutsDir, name + ".json");
       var json = JsonSerializer.Serialize(config, JsonOptions.Default);
       File.WriteAllText(path, json);
       InvalidateCache();
   }
   ```

4. **레이아웃 편집 후 저장 흐름**:
   - ActionBuilderViewModel에서 키 수정 → LayoutConfig 업데이트
   - `LayoutService.Save(currentLayoutName, updatedConfig)` 호출
   - `LayoutService.Load(currentLayoutName)` 재호출 → 화면 갱신

**검증**:
1. 설정 → 커스텀 키 추가 → "내 앱" / RunApp / notepad.exe → 저장
2. 키보드에 "내 앱" 버튼 표시 → 클릭 시 메모장 실행

---

## T-9.3: 자동 완성 시스템 (로컬 학습)

**설명**: 키보드로 입력한 단어를 학습해 자주 쓰는 단어를 상단에 표시한다.
서버 없이 로컬 파일에 단어 빈도를 저장한다.

### 설계 결정

| 항목 | 결정 |
|------|------|
| 저장 형식 | JSON 파일 (`user-words.json`) |
| 최대 단어 수 | 5,000개 (파일 크기 ~150KB) |
| 제안 표시 수 | 최대 5개 |
| 제안 위치 | 키보드 상단 슬라이드인 바 |
| 제안 트리거 | 3자 이상 입력 시 |
| 언어 | 영문(공백/구두점 기준 분리), 한글(어절 기준) |

### 1단계: WordFrequencyStore

**파일 (신규)**: `AltKey/Services/WordFrequencyStore.cs`

```csharp
namespace AltKey.Services;

public class WordFrequencyStore
{
    private const int MaxWords = 5000;
    private readonly string _filePath;
    private Dictionary<string, int> _freq = [];

    public WordFrequencyStore()
    {
        _filePath = Path.Combine(AppContext.BaseDirectory, "user-words.json");
        Load();
    }

    public void RecordWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word) || word.Length < 2) return;
        word = word.Trim().ToLower();
        _freq[word] = (_freq.TryGetValue(word, out var c) ? c : 0) + 1;
        if (_freq.Count > MaxWords) PruneLowest();
    }

    public IReadOnlyList<string> GetSuggestions(string prefix, int count = 5)
    {
        if (prefix.Length < 2) return [];
        return _freq
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                         && kv.Key.Length > prefix.Length)
            .OrderByDescending(kv => kv.Value)
            .Take(count)
            .Select(kv => kv.Key)
            .ToList();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(_freq);
        File.WriteAllText(_filePath, json);
    }

    private void Load()
    {
        if (!File.Exists(_filePath)) return;
        var json = File.ReadAllText(_filePath);
        _freq = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? [];
    }

    private void PruneLowest()
    {
        // 빈도 하위 20% 제거
        var threshold = _freq.Values.OrderBy(v => v).ElementAt(_freq.Count / 5);
        foreach (var key in _freq.Keys.Where(k => _freq[k] <= threshold).ToList())
            _freq.Remove(key);
    }
}
```

### 2단계: AutoCompleteService

**파일 (신규)**: `AltKey/Services/AutoCompleteService.cs`

```csharp
public class AutoCompleteService
{
    private readonly WordFrequencyStore _store;
    private string _currentWord = "";

    public event Action<IReadOnlyList<string>>? SuggestionsChanged;

    public AutoCompleteService(WordFrequencyStore store)
    {
        _store = store;
    }

    /// InputService에서 각 키 입력 시 호출
    public void OnKeyInput(VirtualKeyCode vk, bool isHangul)
    {
        if (IsWordSeparator(vk))
        {
            // 단어 완성: 학습 후 초기화
            if (_currentWord.Length >= 2)
                _store.RecordWord(_currentWord);
            _currentWord = "";
            SuggestionsChanged?.Invoke([]);
            return;
        }

        if (vk == VirtualKeyCode.VK_BACK && _currentWord.Length > 0)
        {
            _currentWord = _currentWord[..^1];
        }
        else
        {
            // VK 코드를 문자로 변환 (한글/영문 모두)
            var ch = VkToChar(vk, isHangul);
            if (ch != '\0') _currentWord += ch;
        }

        var suggestions = _store.GetSuggestions(_currentWord);
        SuggestionsChanged?.Invoke(suggestions);
    }

    private static bool IsWordSeparator(VirtualKeyCode vk) =>
        vk is VirtualKeyCode.VK_SPACE or VirtualKeyCode.VK_RETURN
           or VirtualKeyCode.VK_TAB or VirtualKeyCode.VK_OEM_PERIOD
           or VirtualKeyCode.VK_OEM_COMMA;

    private static char VkToChar(VirtualKeyCode vk, bool hangul)
    {
        // 알파벳 VK_A~VK_Z → 'a'~'z'
        if (vk >= VirtualKeyCode.VK_A && vk <= VirtualKeyCode.VK_Z)
            return (char)('a' + ((int)vk - (int)VirtualKeyCode.VK_A));
        return '\0';
        // 한글은 실제 입력 문자를 VK 코드로 알 수 없으므로
        // IME 훅 또는 별도 텍스트 훅이 필요함 (고급 구현)
    }
}
```

> **한계**: 한글 자동 완성은 VK 코드만으로 현재 조합 중인 글자를 알 수 없다.
> 완전한 한글 지원을 위해서는 `SetWindowsHookEx(WH_KEYBOARD_LL)` 또는 `ITextStoreACP` (TSF) 통합이 필요하다.
> 초기 버전은 영문 자동 완성만 지원한다.

### 3단계: 자동 완성 UI

**파일 (신규)**: `AltKey/Views/SuggestionBar.xaml`

```xml
<UserControl ...>
    <Border Background="#CC1A1A1A" Padding="4,2">
        <ItemsControl ItemsSource="{Binding Suggestions}">
            <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate>
                    <StackPanel Orientation="Horizontal"/>
                </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <Button Content="{Binding}"
                            Command="{Binding DataContext.AcceptSuggestionCommand,
                                RelativeSource={RelativeSource AncestorType=UserControl}}"
                            CommandParameter="{Binding}"
                            Padding="8,4" Margin="2,0"
                            Background="Transparent"
                            BorderBrush="#44FFFFFF" BorderThickness="1"
                            Cursor="Hand" FontSize="12"/>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </Border>
</UserControl>
```

### 4단계: InputService 연동

**파일**: `AltKey/Services/InputService.cs`

`HandleAction()` 내에서 `AutoCompleteService.OnKeyInput()` 호출:
```csharp
case SendKeyAction { Vk: var vkStr }:
    if (Enum.TryParse<VirtualKeyCode>(vkStr, out var vk))
    {
        _autoComplete?.OnKeyInput(vk, IsHangulOn);  // ← 추가
        SendKeyPress(vk);
        ReleaseTransientModifiers();
    }
    break;
```

### 5단계: AppConfig 및 설정

```csharp
public bool AutoCompleteEnabled { get; set; } = false;
```

앱 종료 시 `WordFrequencyStore.Save()` 호출 (App.xaml.cs의 종료 이벤트에서).

**검증**:
1. 영문 3자 입력 → 이전에 입력한 단어 중 일치하는 제안 표시
2. 제안 클릭 → 나머지 글자 자동 입력됨
3. `user-words.json` 파일에 빈도 데이터 저장 확인

---

## T-9.4: 레이아웃 시각적 편집기

**설명**: 레이아웃 JSON을 직접 편집하지 않고 키를 드래그하거나 클릭해 배치를 변경할 수 있는 GUI 편집기.

### 설계

- 별도 창(`LayoutEditorWindow`)으로 열림
- 현재 레이아웃을 그리드로 시각화 (실제 키보드와 동일한 렌더링)
- 키 클릭 → 우측 속성 패널에서 편집:
  - 라벨 / shift_label / hangul_shift_label
  - 너비(width) 슬라이더
  - 액션 타입 및 파라미터 (T-9.2의 ActionBuilderView 재사용)
- 행 추가 / 행 삭제 버튼
- 키 드래그로 순서 변경 (선택 구현)
- "다른 이름으로 저장" → 새 JSON 파일 생성
- "저장" → 현재 파일 덮어쓰기

### 핵심 구현 포인트

1. **EditableKeySlotVm**: `KeySlotVm`을 상속하여 편집 가능한 버전
   ```csharp
   public class EditableKeySlotVm : KeySlotVm
   {
       private string _label;
       public string EditLabel { get => _label; set => SetProperty(ref _label, value); }
       private double _width;
       public double EditWidth { get => _width; set => SetProperty(ref _width, value); }
       // ... 기타 편집 가능 프로퍼티
   }
   ```

2. **LayoutEditorViewModel**:
   - `ObservableCollection<EditableKeyRowVm> Rows`
   - `EditableKeySlotVm? SelectedKey`
   - `BuildLayoutConfig()` → 편집된 VM에서 `LayoutConfig` 생성
   - `SaveCommand` → `LayoutService.Save()` 호출

3. **드래그 앤 드롭** (선택 구현):
   - `KeyButton` 컨트롤에 `AllowDrop=True`, `DragOver`/`Drop` 이벤트 처리
   - 드롭 시 `EditableKeyRowVm`의 키 순서를 교환

**우선순위 구현**: 속성 편집 → 저장까지 먼저 구현하고, 드래그 앤 드롭은 이후 추가한다.

**검증**:
1. 편집기 열기 → 키 클릭 → 라벨 변경 → 저장
2. 메인 키보드에서 변경된 라벨 표시 확인
3. JSON 파일에 변경사항 반영 확인

---

## T-9.5: 설치형 배포 및 자동 업데이트

**설명**: 포터블 배포와 별개로 설치형 배포를 지원하고, 자동 업데이트 기능을 추가한다.

### 설계 결정

| 항목 | 결정 | 이유 |
|------|------|------|
| 인스톨러 | **Inno Setup** | 무료, 한국어 지원, 단일 파일 배포 가능 |
| 자동 업데이트 | **GitHub Releases + 수동 확인** | 서버 불필요, 구현 단순 |
| 업데이트 확인 주기 | 앱 시작 시 1회 | 부하 최소화 |
| 시작 시 자동 실행 | 설치형: 레지스트리 Run 키 (인스톨러에서 처리) | 포터블과 동일 |

### 1단계: UpdateCheckService

**파일 (신규)**: `AltKey/Services/UpdateCheckService.cs`

```csharp
using System.Net.Http;
using System.Text.Json;

namespace AltKey.Services;

public class UpdateCheckService
{
    // GitHub Releases API
    private const string ApiUrl =
        "https://api.github.com/repos/{owner}/{repo}/releases/latest";

    private readonly HttpClient _http;

    public UpdateCheckService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AltKey/1.0");
    }

    public async Task<UpdateInfo?> CheckAsync(string currentVersion)
    {
        try
        {
            var json = await _http.GetStringAsync(ApiUrl);
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var url = doc.RootElement.GetProperty("html_url").GetString() ?? "";
            var body = doc.RootElement.GetProperty("body").GetString() ?? "";

            // "v1.2.3" 형식 파싱
            var latestVer = Version.TryParse(tag.TrimStart('v'), out var v) ? v : null;
            var currentVer = Version.TryParse(currentVersion.TrimStart('v'), out var cv) ? cv : null;

            if (latestVer is not null && currentVer is not null && latestVer > currentVer)
                return new UpdateInfo(tag, url, body);

            return null; // 최신 버전
        }
        catch
        {
            return null; // 네트워크 오류 시 조용히 무시
        }
    }
}

public record UpdateInfo(string Version, string DownloadUrl, string ReleaseNotes);
```

### 2단계: 앱 시작 시 업데이트 확인

**파일**: `AltKey/App.xaml.cs`

```csharp
protected override async void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    // ... 기존 초기화 코드 ...

    // 백그라운드에서 업데이트 확인
    _ = Task.Run(async () =>
    {
        var svc = Services.GetService<UpdateCheckService>();
        var info = await svc!.CheckAsync("1.0.0"); // 현재 버전 상수
        if (info is not null)
        {
            Dispatcher.Invoke(() =>
            {
                // KeyboardView의 UpdateBanner를 표시
                // MainViewModel 또는 EventAggregator를 통해 전달
            });
        }
    });
}
```

### 3단계: 업데이트 배너 연동

기존 `KeyboardView.xaml`의 `UpdateBanner`가 이미 구현되어 있다 (라인 17-33).
`MainViewModel`에 `UpdateInfo` 속성을 추가하고 배너를 바인딩한다:

```csharp
// MainViewModel.cs
[ObservableProperty] private string? updateVersion;
[ObservableProperty] private string? updateUrl;

// UpdateBanner Visibility: updateVersion != null
```

### 4단계: Inno Setup 스크립트

**파일 (신규)**: `installer/AltKey.iss`

```iss
[Setup]
AppName=AltKey
AppVersion=1.0.0
AppPublisher=YourName
DefaultDirName={autopf}\AltKey
DefaultGroupName=AltKey
OutputBaseFilename=AltKey-Setup-1.0.0
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\AltKey.exe

[Files]
Source: "..\AltKey\bin\Release\net8.0-windows\win-x64\publish\AltKey.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\AltKey\layouts\*"; DestDir: "{app}\layouts"; Flags: ignoreversion recursesubdirs
Source: "..\AltKey\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\AltKey"; Filename: "{app}\AltKey.exe"
Name: "{group}\AltKey 제거"; Filename: "{uninstallexe}"
Name: "{commondesktop}\AltKey"; Filename: "{app}\AltKey.exe"

[Run]
Filename: "{app}\AltKey.exe"; Description: "AltKey 실행"; Flags: postinstall nowait
```

### 5단계: GitHub Actions CI/CD (선택 구현)

**파일 (신규)**: `.github/workflows/release.yml`

```yaml
name: Release

on:
  push:
    tags: ['v*']

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - name: Publish
        run: dotnet publish AltKey/AltKey.csproj -c Release -r win-x64 --self-contained
      - name: Create Release
        uses: softprops/action-gh-release@v1
        with:
          files: |
            AltKey/bin/Release/net8.0-windows/win-x64/publish/AltKey.exe
```

**검증**:
1. 새 버전 GitHub Release 생성 → 앱 시작 시 업데이트 배너 표시
2. "다운로드" 클릭 → 브라우저에서 릴리즈 페이지 열림
3. 설치형: 인스톨러 실행 → 프로그램 설치/제거 확인
