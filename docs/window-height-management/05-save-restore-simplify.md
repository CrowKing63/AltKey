# 작업 05: 저장/복원 단순화

> 의존성: 작업 01 (Scale 모델) + 작업 03 (ApplyScale)

---

## 목표

`MainWindow`의 저장/복원 로직을 Scale 기반으로 단순화.
Width/Height 픽셀 저장을 완전히 제거하고 Left/Top + Scale만 저장/복원.

---

## 변경 파일

### 1. `MainWindow.xaml.cs` — RestoreWindowPosition

**Before:**
```csharp
Width  = Math.Max(400, cfg.Width);
Height = Math.Max(180, cfg.Height);
// + 화면 밖 판정 로직...
```

**After:**
```csharp
// 창 크기는 KeyboardView.ApplyScale()이 담당.
// 여기서는 위치만 복원. Scale은 ConfigService가 이미 로드함.
var cfg = _configService.Current.Window;

var screen = System.Windows.SystemParameters.WorkArea;
// ... Left/Top 복원 로직은 동일 ...
```

- `Width`/`Height` 설정 코드 제거
- Left/Top 복원은 그대로 유지 (화면 밖 판정 포함)
- 창의 초기 Width/Height는 MainWindow.xaml의 기본값(900×350)을 유지하되,
  `OnLoaded`에서 `ApplyScale()`이 즉시 올바른 크기로 덮어씀

### 2. `MainWindow.xaml.cs` — OnClosing

**Before:**
```csharp
if (!ResetPending)
{
    var saveHeight = Height;
    if (KeyboardViewControl?.IsCollapsed == true && KeyboardViewControl.ExpandedHeight > 0)
        saveHeight = KeyboardViewControl.ExpandedHeight;

    _configService.Update(c =>
    {
        c.Window.Left   = Left;
        c.Window.Top    = Top;
        c.Window.Width  = Width;
        c.Window.Height = saveHeight;
    });
}
```

**After:**
```csharp
if (!ResetPending)
{
    _configService.Update(c =>
    {
        c.Window.Left = Left;
        c.Window.Top  = Top;
        // Scale은 설정 슬라이더에서만 변경 → OnClosing에서 저장 불필요
    });
}
```

- `Width`/`Height` 저장 완전 제거
- `IsCollapsed`/`ExpandedHeight` 확인 로직 제거
- `ResetPending` 플래그는 유지 (창 레이아웃 초기화 기능에 여전히 필요)

### 3. `SettingsViewModel.cs` — ResetWindowLayout

**Before:**
```csharp
_configService.Update(c =>
{
    c.Window = new WindowConfig();   // Left=-1, Top=-1, Width=900, Height=320
    c.AutoCompleteEnabled = false;
});
```

**After:**
```csharp
_configService.Update(c =>
{
    c.Window = new WindowConfig();   // Left=-1, Top=-1, Scale=100
    c.AutoCompleteEnabled = false;
});
```

- `new WindowConfig()`가 이제 Scale=100을 포함하므로 자연스럽게 동작
- 별도 수정 불필요할 수 있으나 확인 필요

---

## 복원 흐름 (리팩토링 후)

```
앱 시작
  → MainWindow.OnSourceInitialized
    → RestoreWindowPosition: Left/Top만 복원 (Width/Height 설정 안 함)
  → MainWindow.Loaded
    → KeyboardView.OnLoaded
      → ApplySuggestionBarHeight(): 바 상태 초기화
      → ApplyScale(): ComputeBaseSize() × Scale → 창 크기 설정
  → UpdateKeyUnit: Scale에 비례한 KeyUnit 자동 계산
```

---

## 주의사항

- `RestoreWindowPosition`에서 Width/Height를 설정하지 않으므로
  `Loaded` 이벤트 전까지는 MainWindow.xaml의 기본 900×350 크기
  → ApplyScale이 즉시 올바른 크기로 덮어쓰므로 깜빡임 없음
  (이미 `PlayOpenAnimation()`에서 Opacity=0 → 1 페이드인이 있어 가려짐)
- `KeyboardView.ExpandedHeight` / `IsCollapsed` 프로퍼티는 작업 07에서 정리
- Left/Top 저장은 여전히 OnClosing에서 수행 (Scale과 무관)

---

## 완료 조건

- [ ] OnClosing에서 Width/Height 저장 제거, Left/Top만 저장
- [ ] RestoreWindowPosition에서 Width/Height 설정 제거
- [ ] 접힌 상태에서 종료 → 재시작 시 올바른 크기로 복원
- [ ] ResetWindowLayout이 Scale=100으로 초기화되는지 확인
- [ ] 빌드 에러 없음
