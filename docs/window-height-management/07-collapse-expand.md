# 작업 07: 접기/펼치기 정리

> 의존성: 작업 03 (ApplyScale) + 작업 06 (바 토글 단순화)

---

## 목표

접기/펼치기에서 `_expandedHeight` 추적을 제거하고,
펼치기 시 `ApplyScale()`로 복원하도록 단순화.

---

## 변경 파일

### `Views/KeyboardView.xaml.cs`

#### 삭제할 필드

```csharp
private double _expandedHeight = 0;
```

#### 삭제할 공개 속성

```csharp
public double ExpandedHeight => _expandedHeight;
```

(작업 05에서 OnClosing이 더 이상 `ExpandedHeight`를 참조하지 않으므로 삭제 가능)

#### 재작성: CollapseButton_Click

**Before:**
```csharp
private void CollapseButton_Click(object sender, RoutedEventArgs e)
{
    var window = Window.GetWindow(this);
    if (window is null) return;

    if (!_isCollapsed)
    {
        CaptureAndClearHeightAnimation(window);
        if (DataContext is MainViewModel vm)
            _lastExpandedKeyRowHeight = vm.Keyboard.KeyRowHeight;
        _expandedHeight = window.Height;          // ← 제거
        AnimateWindowHeight(window, CollapsedWindowHeight);
        if (FindName("CollapseIcon") is TextBlock collapseIcon)
            collapseIcon.Text = "▼";
        _isCollapsed = true;
    }
    else
    {
        var targetHeight = _expandedHeight > 0 ? _expandedHeight : Math.Max(window.Height, AbsMinWindowHeight);
        AnimateWindowHeight(window, targetHeight);
        if (FindName("CollapseIcon") is TextBlock collapseIcon)
            collapseIcon.Text = "▲";
        _isCollapsed = false;
    }
}
```

**After:**
```csharp
private void CollapseButton_Click(object sender, RoutedEventArgs e)
{
    var window = Window.GetWindow(this);
    if (window is null) return;

    if (!_isCollapsed)
    {
        AnimateWindowHeight(window, CollapsedWindowHeight);
        if (FindName("CollapseIcon") is TextBlock collapseIcon)
            collapseIcon.Text = "▼";
        _isCollapsed = true;
    }
    else
    {
        _isCollapsed = false;
        ApplyScale();
        if (FindName("CollapseIcon") is TextBlock collapseIcon)
            collapseIcon.Text = "▲";
    }
}
```

#### 변경 설명

| 동작 | Before | After |
|---|---|---|
| 접기 | `_expandedHeight = window.Height` 저장 후 28px 애니메이션 | 바로 28px 애니메이션 |
| 펼치기 | `_expandedHeight`에서 복원 | `ApplyScale()`로 기준 크기 × Scale 복원 |

- 펼치기 시 애니메이션 생략 → 즉시 ApplyScale()로 크기 설정
  (애니메이션 유지를 원하면 ApplyScale 후 애니메이션 추가 가능하나,
  접기→펼치기는 즉각적인 조작이므로 애니메이션 없이도 자연스러움)

- 만약 애니메이션을 유지하고 싶다면:
  ```csharp
  _isCollapsed = false;
  var (baseW, baseH) = ComputeBaseSize();
  var scale = _configService?.Current.Window.Scale ?? 100;
  var targetH = baseH * Math.Clamp(scale, MinScale, MaxScale) / 100.0;
  AnimateWindowHeight(window, targetH);
  ```

#### CaptureAndClearHeightAnimation 정리

접기 시 더 이상 `CaptureAndClearHeightAnimation`이 필요한지 검토:
- 여전히 애니메이션 중복 방지를 위해 접기 경로에서는 필요
- 유지하되 `_expandedHeight` 관련 코드만 제거

---

## MainWindow.OnClosing 정리 확인

작업 05에서 이미 아래 코드를 삭제했는지 확인:

```csharp
// 작업 05에서 삭제됨 — 혹시 남아있으면 여기서도 삭제
if (KeyboardViewControl?.IsCollapsed == true && KeyboardViewControl.ExpandedHeight > 0)
    saveHeight = KeyboardViewControl.ExpandedHeight;
```

---

## 주의사항

- 펼치기 시 `ApplyScale()`을 호출하면 `window.Height`가 Scale 기반으로 설정됨
- 접힌 상태에서 바 토글이 발생해도 `ApplyScale`이 알아서 처리
  (`_isCollapsed`면 높이를 CollapsedWindowHeight로 설정)
- 접힌 상태 → 종료 → 재시작 → 항상 펼쳐진 상태로 시작 (사용자 요구사항)
  → `OnLoaded`에서 `_isCollapsed`는 항상 `false`이므로 ApplyScale이 정상 동작

---

## 완료 조건

- [ ] _expandedHeight 필드 및 ExpandedHeight 속성 삭제
- [ ] CollapseButton_Click에서 _expandedHeight 참조 제거
- [ ] 펼치기 시 ApplyScale()로 크기 복원
- [ ] _lastExpandedKeyRowHeight 참조 완전 제거 (작업 06과 연계)
- [ ] 접기 → 펼치기 → 종료 → 재시작 후 올바른 크기
- [ ] 접힌 상태에서 바 토글 후 펼치기 시 올바른 크기
