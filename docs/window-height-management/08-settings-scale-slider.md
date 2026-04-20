# 작업 08: 설정 UI — 창 크기 조절 (NumericAdjuster)

> 의존성: 작업 01 (Scale 모델) — Phase D (최종)

---

## 목표

설정 패널에 창 크기 조절 [(-) 값 (+)] 컨트롤을 추가.
기존 `NumericAdjuster` 커스텀 컨트롤 재사용.
60%~200%, 10% 단위, 기본 100%.

---

## 변경 파일

### 1. `ViewModels/SettingsViewModel.cs`

#### 추가할 속성

```csharp
[ObservableProperty] private int windowScale = 100;
```

#### 추가할 partial method

```csharp
partial void OnWindowScaleChanged(int value)
{
    if (_isLoading) return;
    var clamped = Math.Clamp(value, 60, 200);
    if (clamped != value) { WindowScale = clamped; return; }
    _configService.Update(c => c.Window.Scale = clamped, "Window.Scale");
}
```

- 범위 밖 값이 들어오면 강제 클램프 후 재설정 (`return`으로 중복 저장 방지)
- `_configService.Update`의 두 번째 인자 `"Window.Scale"`은 `ConfigChanged` 이벤트에서
  `propertyName`으로 전달됨 → `KeyboardView.OnConfigChanged`가 감지하여 ApplyScale 호출

#### LoadFromConfig에 추가

```csharp
WindowScale = c.Window.Scale;
```

### 2. `Views/SettingsView.xaml`

"항상 위에 표시" 체크박스 아래에 배치. 기존 `NumericAdjuster` 패턴과 동일.

```xml
<!-- 창 크기 (%): NumericAdjuster [−] 100 [+] -->
<TextBlock Text="창 크기 (%)" Foreground="{DynamicResource SettingsFgSub}" FontSize="11" Margin="0,0,0,4"/>
<ctrl:NumericAdjuster Value="{Binding WindowScale}"
                      Minimum="60" Maximum="200" Step="10" DecimalPlaces="0"
                      ButtonBackground="{DynamicResource SettingsHighlight}"
                      ButtonForeground="{DynamicResource SettingsFg}"
                      TextBoxBackground="{DynamicResource SettingsHighlight}"
                      TextBoxForeground="{DynamicResource SettingsFg}"
                      TextBoxBorderBrush="{DynamicResource SettingsBorder}"
                      Margin="0,0,0,16"/>
```

---

## 실시간 적용 흐름

```
[−]/[+] 버튼 클릭 (또는 텍스트 직접 입력)
  → NumericAdjuster.Value 변경
    → SettingsViewModel.WindowScale setter
      → OnWindowScaleChanged
        → _configService.Update(c => c.Window.Scale, "Window.Scale")
          → ConfigChanged 이벤트 (propertyName = "Window.Scale")
            → KeyboardView.OnConfigChanged
              → ApplySuggestionBarHeight() + ApplyScale()
                → ComputeBaseSize() × Scale → window.Width/Height 설정
                  → SizeChanged → UpdateKeyUnit → KeyUnit 재계산
```

---

## 접근성

- `NumericAdjuster`는 WPF 기본 Button/TextBox 기반이므로 Narrator 및 키보드 탐색 지원
- 방향키 ↑/↓로 ±10 조절 (NumericAdjuster에 이미 구현됨)
- 텍스트 직접 입력 후 Enter 또는 포커스 이탈 시 적용

---

## 완료 조건

- [x] SettingsViewModel에 WindowScale 속성 추가
- [x] OnWindowScaleChanged에서 config 저장
- [x] SettingsView.xaml에 NumericAdjuster UI 추가
- [x] [−]/[+] 버튼 조작 시 실시간으로 창 크기 변경
- [x] 텍스트 직접 입력 후 Enter 시 창 크기 변경
- [x] 60~200 범위 외 값 입력 시 자동 클램프
- [x] 창 레이아웃 초기화 시 Scale=100으로 리셋
