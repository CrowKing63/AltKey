# 작업 03: 스케일 적용 — 창 크기 설정

> 의존성: 작업 02 (ComputeBaseSize)

---

## 목표

`ComputeBaseSize()` 결과에 Scale을 곱해 창 크기를 설정하는 `ApplyScale()` 메서드를 구현하고,
적절한 호출 시점을 확보.

---

## 변경 파일

### `Views/KeyboardView.xaml.cs`

#### 추가할 메서드

```csharp
/// 현재 Scale 설정에 따라 창 크기를 적용한다.
/// _isCollapsed == true면 창 높이를 CollapsedWindowHeight(28)로 강제.
public void ApplyScale()
{
    if (Window.GetWindow(this) is not { } window) return;

    var scale = _configService?.Current.Window.Scale ?? 100;
    scale = Math.Clamp(scale, MinScale, MaxScale);

    var (baseW, baseH) = ComputeBaseSize();

    window.Width  = baseW * scale / 100.0;
    window.Height = _isCollapsed
        ? CollapsedWindowHeight
        : baseH * scale / 100.0;
}
```

#### 호출 시점

| 시점 | 위치 | 설명 |
|---|---|---|
| 초기 로드 | `OnLoaded` 끝 | `ApplySuggestionBarHeight()` 이후 |
| 레이아웃 교체 | `OnLoaded`의 `PropertyChanged` 핸들러 | MaxRowUnits/MaxRowCount/RowCount 변경 시 |
| Scale 변경 | `OnConfigChanged` | `Window.Scale` 변경 감지 시 |
| 바 토글 | `ApplySuggestionBarHeight` (작업 06에서 정리) | 기준 크기 재계산 후 ApplyScale |
| 펼치기 | `CollapseButton_Click` (작업 07에서 정리) | 접힌 상태에서 펼칠 때 |

#### OnLoaded 수정

```csharp
private void OnLoaded(object sender, RoutedEventArgs e)
{
    if (Window.GetWindow(this) is { } window)
    {
        _configService = App.Services.GetRequiredService<ConfigService>();
        _configService.ConfigChanged += OnConfigChanged;

        ApplySuggestionBarHeight();  // 바 상태 초기화 (_autoCompleteBarAdded 세팅)
        ApplyScale();                // ← 추가: 창 크기 최초 설정

        window.SizeChanged += OnWindowSizeChanged;

        if (DataContext is MainViewModel mainVm)
        {
            // ... PropertyChanged 구독 (기존과 동일)
            mainVm.Keyboard.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(KeyboardViewModel.MaxRowUnits)
                                   or nameof(KeyboardViewModel.MaxRowCount)
                                   or nameof(KeyboardViewModel.RowCount))
                {
                    Dispatcher.InvokeAsync(() => ApplyScale());
                }
            };
        }
    }
}
```

- **주의**: `window.SizeChanged` 구독을 `ApplyScale()` **이후**에 해제해야 함.
  그렇지 않으면 ApplyScale이 트리거한 SizeChanged가 OnWindowSizeChanged를 호출함.
  (현재 OnWindowSizeChanged는 `UpdateKeyUnit`만 호출하므로 무해하지만,
  불필요한 재계산 방지 차원에서 순서 정리)

#### OnConfigChanged 수정

```csharp
private void OnConfigChanged(string? propertyName)
{
    if (propertyName is null
        or nameof(AppConfig.AutoCompleteEnabled)
        or "Window.Scale")
    {
        Dispatcher.InvokeAsync(() =>
        {
            ApplySuggestionBarHeight();
            ApplyScale();
        });
    }
}
```

---

## 피드백 루프 안전성

```
ApplyScale() → window.Width/Height 설정
  → SizeChanged 발생
    → OnWindowSizeChanged → UpdateKeyUnit(window.Width)
      → KeyUnit 계산 (폐쇄형 식, 안정적)
        → 종료 (KeyUnit이 정확히 맞아떨어지므로 추가 SizeChanged 없음)
```

- `ApplyScale`이 설정한 창 크기는 레이아웃 메트릭과 정확히 비례하므로
  `UpdateKeyUnit`이 산출하는 KeyUnit도 Scale과 정확히 비례
- `KeyRowHeight = KeyUnit + 4` → 바 높이도 비례 → 재귀 없이 한 번에 수렴

---

## 주의사항

- `ApplyScale()`은 `window.Width`와 `window.Height`를 **둘 다** 설정
- 부분 설정(너비만, 높이만)은 하지 않음 — 비율 항상 유지
- 접힌 상태에서는 너비만 Scale 적용, 높이는 CollapsedWindowHeight 고정
- 작업 01이 아직 적용되지 않았으면 `ConfigService.Current.Window.Scale`이 없으므로
  작업 01 완료 후 이 작업 진행

---

## 완료 조건

- [ ] `ApplyScale()` 메서드 구현
- [ ] OnLoaded에서 ApplyScale() 호출
- [ ] OnConfigChanged에서 Scale 변경 감지 후 ApplyScale() 호출
- [ ] 레이아웃 메트릭 변경 시 ApplyScale() 호출
- [ ] Scale 100%에서 키 크기가 BaseKeyUnit(50)에 근사하는지 확인
- [ ] Scale 150%에서 키 크기가 약 75px인지 확인
