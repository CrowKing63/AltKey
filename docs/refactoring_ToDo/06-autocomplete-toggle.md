# 06 — 자동완성 토글 ↔ Unicode/VirtualKey 모드 연동

> **소요**: 2~3시간
> **선행**: 05
> **후행**: 09, 10
> **관련 기획**: [refactor-unif-serialized-acorn.md §0 (사용자 피드백: "유니코드 방식은 자동 완성이 켜져 있을 때만")](../refactor-unif-serialized-acorn.md)

---

## 0. 이 태스크의 목표 한 줄

**자동완성 토글을 키 입력 방식과 연동**한다. 자동완성 ON → Unicode 모드 → IME 우회 + 자모 추적. 자동완성 OFF → VirtualKey 모드 → OS IME 의존(호환성 우선, 보안앱 동작). 사용자는 **상단바의 눈에 띄는 토글**로 언제든 전환 가능. **기본값은 OFF**(사용자 요구).

---

## 1. 전제 조건

- 05 완료: `KeyboardViewModel.KeyPressed`가 단일 `OnKey()` 호출로 축소됨.
- `InputService.Mode`가 관리자 권한 감지에 따라 자동 결정되는 기존 로직을 이해(`CheckElevated`).

---

## 2. 현재 상태

### 2-1. `AltKey/Services/InputService.cs`

- `public InputMode Mode { get; }` — `CheckElevated()`로 초기 결정, 이후 변경 불가(get-only).
- 관리자 권한 → `VirtualKey`, 일반 → `Unicode`.

### 2-2. `AltKey/Models/AppConfig.cs`

- `public bool AutoCompleteEnabled { get; set; } = true;` — 현재 **기본값 true**.

### 2-3. 사용자 피드백 (refactor-unif-serialized-acorn.md §0)

> "유니코드 방식은 자동 완성이 켜져 있을 때만. 자동 완성이 꺼져 있을 때는 기존 가상 키 방식 사용. 자동 완성(키 입력 방식)은 상단 바에서 손쉽게 토글. 이렇게 하면 유니코드 방식으로 입력이 안 되는 앱에서 빠른 조치 가능. 기본값은 오프."

---

## 3. 작업 내용

### 3-1. `InputService.Mode`를 setter 가능하게 변경

**파일**: `AltKey/Services/InputService.cs`

```csharp
public InputMode Mode { get; private set; }

public event Action<InputMode>? ModeChanged;

/// 관리자 권한 상태에서는 항상 VirtualKey. 일반 권한에서만 Unicode ↔ VirtualKey 전환 가능.
public bool TrySetMode(InputMode target)
{
    if (_isElevated && target == InputMode.Unicode)
        return false;   // 관리자 모드에서는 Unicode 불가

    if (Mode == target) return true;
    Mode = target;
    ModeChanged?.Invoke(Mode);
    return true;
}
```

> 기존 생성자의 `Mode = CheckElevated() ? VirtualKey : Unicode`는 **초기 모드만** 결정하도록 유지. 이후 `TrySetMode`로 사용자 토글 반영.

### 3-2. `AppConfig.AutoCompleteEnabled` 기본값 변경

**파일**: `AltKey/Models/AppConfig.cs`

```csharp
public bool AutoCompleteEnabled { get; set; } = false;   // ← false 로
```

> 기존 사용자의 config.json에 이미 `true`로 저장돼 있으면 영향 없음. 새 사용자만 기본 OFF.

### 3-3. 자동완성 토글 시 모드 동기화

**파일**: `AltKey/ViewModels/MainViewModel.cs` (또는 전용 `HeaderBarViewModel`이 있다면 그쪽)

```csharp
[ObservableProperty]
private bool autoCompleteEnabled;

partial void OnAutoCompleteEnabledChanged(bool value)
{
    _config.Current.AutoCompleteEnabled = value;
    _config.Save();

    // 모드 동기화
    var target = value ? InputMode.Unicode : InputMode.VirtualKey;
    bool ok = _inputService.TrySetMode(target);

    if (!ok && value)
    {
        // 관리자 모드라 Unicode로 전환 실패 — 사용자에게 안내
        _liveRegionService.Announce("관리자 모드에서는 자동완성이 제한됩니다.");
        AutoCompleteEnabled = false;   // 토글 되돌리기
    }

    // 현재 조합 상태 플러시
    _autoComplete.ResetState();
}
```

> `LiveRegionService`·`AnnounceService` 같은 공지 서비스가 없다면 08 태스크에서 추가. 지금은 `System.Media.SystemSounds.Beep.Play()` 등으로 임시 대체 가능.

### 3-4. 상단바에 자동완성 토글 추가

**파일**: `AltKey/Views/MainWindow.xaml` (또는 `KeyboardView.xaml` 상단 헤더 영역)

기존 헤더 바에 있는 설정/이모지/클립보드/접기/닫기 버튼 옆에 토글 버튼 추가:

```xml
<ToggleButton x:Name="AutoCompleteToggleButton"
              IsChecked="{Binding AutoCompleteEnabled, Mode=TwoWay}"
              ToolTip="자동완성 (유니코드 직접 입력) 켜기/끄기"
              AutomationProperties.Name="자동완성 토글"
              AutomationProperties.HelpText="켜면 유니코드 직접 입력, 끄면 가상 키 전송(보안 앱 호환)"
              Width="32" Height="28"
              Margin="4,0,0,0">
    <TextBlock Text="AC" FontWeight="Bold" />
</ToggleButton>
```

> 라벨 "AC"는 임시. 아이콘이 있다면 교체. "자동완성"이라는 풀 라벨은 너비 절약을 위해 축약.

### 3-5. 사용자가 자동완성을 OFF한 상태에서 키 입력

`KeyPressed`는 이미 05에서 `_autoComplete.OnKey(slot, ctx)`를 호출한다. `AutoCompleteEnabled == false`면:

- Unicode 모드가 아닌 VirtualKey 모드(위에서 `TrySetMode`로 전환됨).
- `KoreanInputModule.HandleKey`는 VirtualKey 분기를 타고 `return false`(HandleAction이 OS에 VK 전송, OS IME가 조합).
- 자동완성 제안은 모듈이 여전히 계산해서 이벤트 발생시킬 수 있음. **하지만 제안 바 UI를 숨겨야 함**.

따라서 `SuggestionBarViewModel.IsVisible`을 `AutoCompleteEnabled`에 바인딩:

```csharp
public SuggestionBarViewModel(AppConfig config, AutoCompleteService autoComplete)
{
    // ... 기존 ...
    IsVisible = config.AutoCompleteEnabled;
    config.PropertyChanged += (_, e) =>
    {
        if (e.PropertyName == nameof(AppConfig.AutoCompleteEnabled))
            IsVisible = config.AutoCompleteEnabled;
    };
}
```

또는 `MainViewModel`에서 `AutoCompleteEnabled` 변경 시 직접 `_suggestionBar.IsVisible`을 세팅.

### 3-6. 관리자 모드에서 자동완성 동작

관리자 모드에서는 `Mode == VirtualKey`가 강제된다. 자동완성을 ON으로 해도 모드 전환 실패 → `AutoCompleteEnabled`을 false로 되돌리거나, 또는 `AutoCompleteEnabled` 상태는 유지하되 **VirtualKey 모드에서의 자동완성**을 허용하는 선택지가 있다.

기획 §0에서는 관리자 모드 호환성이 핵심이었다. 선택:
- **옵션 A (권장)**: 관리자 모드에서는 자동완성 버튼을 비활성화(disabled) + tooltip으로 안내.
- **옵션 B**: 관리자 모드에서도 자동완성 ON을 허용하되 VirtualKey 모드로 동작(기존 IME 의존).

관리자 모드에서의 복잡성 감소를 위해 **옵션 A**를 선택. `AutoCompleteToggleButton.IsEnabled="{Binding CanToggleAutoComplete}"`로 게이트.

```csharp
public bool CanToggleAutoComplete => !_inputService.IsElevated;
```

> `IsElevated`는 `InputService`에 프로퍼티로 노출. `Mode == VirtualKey && _isElevated`이면 관리자.

### 3-7. 현재 config.json의 `AutoCompleteEnabled`가 true인 기존 사용자

기본값 변경은 새 사용자에게만 영향. 기존 사용자의 `config.json`을 강제로 덮어쓰지 않는다.

다만, **첫 실행 시** 관리자 모드가 감지되면 `AutoCompleteEnabled`을 자동으로 false로 보정하는 것은 권장:

```csharp
// App.xaml.cs 초기화 시점
if (_inputService.IsElevated && _config.Current.AutoCompleteEnabled)
{
    _config.Current.AutoCompleteEnabled = false;
    _config.Save();
}
```

---

## 4. 검증

1. 빌드 녹색.
2. 신규 사용자(설정 파일 없음)로 실행 → 자동완성 OFF 기본값. 상단바 토글 버튼이 OFF 상태로 표시.
3. 토글 ON → 자동완성 제안 바 표시, 한글 자모 타자 시 유니코드 직접 입력.
4. 토글 OFF → 제안 바 숨김, 한글 자모 타자 시 VK 전송(OS IME가 조합).
5. 관리자 권한으로 실행 → 토글 버튼 비활성화, tooltip 표시.
6. 같은 실행 중 여러 번 토글 → 모드 전환 매번 성공, 이전 조합 상태는 플러시.
7. 호환성 문제가 있는 앱(예: 일부 Electron 앱)에서 자동완성 OFF → 입력 정상 복구.

---

## 5. 함정 / 주의

- **모드 전환 시 조합 상태**: `TrySetMode` 호출 전에 반드시 `_autoComplete.ResetState()` 또는 `OnSeparator()`. 그렇지 않으면 한 모드에서 조합 중인 음절이 다른 모드에서 유령처럼 남는다.
- **`SuggestionBarViewModel.IsVisible` 바인딩**: 기존 XAML 바인딩 경로를 깨지 않도록. `SuggestionBar.xaml`의 `Visibility` 바인딩이 이미 이 프로퍼티를 가리킬 가능성이 높다.
- **관리자 모드 감지 타이밍**: `InputService` 생성자에서 한 번만 판단. 런타임 중 권한이 바뀌지 않는다는 가정.
- **사용자 피드백 UX**: 관리자 모드에서 토글을 눌렀을 때 조용히 실패하면 혼란. 반드시 비활성화 + tooltip 또는 클릭 시 공지.
- **Config 저장**: `config.json` 저장이 매 토글마다 일어나는 것은 OK(빈도 낮음).

---

## 6. Critical Files

| 파일 | 수정 유형 |
|---|---|
| `AltKey/Services/InputService.cs` | 수정 (`Mode` setter, `TrySetMode`, `IsElevated`, `ModeChanged` 이벤트) |
| `AltKey/Models/AppConfig.cs` | 수정 (`AutoCompleteEnabled` 기본값 false) |
| `AltKey/ViewModels/MainViewModel.cs` | 수정 (토글 프로퍼티, 모드 동기화) |
| `AltKey/ViewModels/SuggestionBarViewModel.cs` | 수정 (`IsVisible` 바인딩) |
| `AltKey/Views/MainWindow.xaml` | 수정 (상단바 토글 버튼) |
| `AltKey/App.xaml.cs` | 소폭 (관리자 모드 초기 보정) |

---

## 7. 커밋 메시지 초안

```
feat(toggle): autocomplete toggle drives InputMode

- InputService.TrySetMode(Unicode|VirtualKey) for user control.
- AppConfig.AutoCompleteEnabled default = false.
- Header toolbar gets an AC toggle (disabled when elevated).
- SuggestionBar hides when autocomplete is off.
- Module state flushes on every mode switch.
```
