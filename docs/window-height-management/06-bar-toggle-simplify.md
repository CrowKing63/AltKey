# 작업 06: 바 토글 로직 단순화

> 의존성: 작업 03 (ApplyScale)

---

## 목표

`ApplySuggestionBarHeight()`를 단순화.
`AdjustWindowHeight` (±delta) 방식을 제거하고 기준 크기 재계산 + ApplyScale로 교체.

---

## 변경 파일

### `Views/KeyboardView.xaml.cs`

#### 삭제할 메서드

```csharp
private static void AdjustWindowHeight(Window window, double delta)
{
    if (Math.Abs(delta) < 0.01) return;
    CaptureAndClearHeightAnimation(window);
    window.Height = Math.Max(AbsMinWindowHeight, window.Height + delta);
}
```

#### 삭제할 필드

```csharp
private double _appliedBarHeight = 0;
private double _lastExpandedKeyRowHeight = 0;
```

#### 삭제할 공개 속성

```csharp
public double AppliedBarHeight => _appliedBarHeight;
```

(작업 05에서 OnClosing이 더 이상 이 속성을 참조하지 않으므로 삭제 가능)

#### 단순화할 필드

```csharp
// Before
private bool _autoCompleteBarAdded = false;
private bool _initialized = false;

// After — _initialized 제거, _autoCompleteBarAdded 유지
private bool _autoCompleteBarAdded = false;
```

#### 재작성: ApplySuggestionBarHeight

**Before** (~40행, AdjustWindowHeight 호출 + 접힌 상태 분기):

**After:**
```csharp
private void ApplySuggestionBarHeight()
{
    if (DataContext is not MainViewModel vm) return;

    var wantBar = _configService?.Current.AutoCompleteEnabled == true;
    _autoCompleteBarAdded = wantBar;

    // 창 크기는 ApplyScale()이 ComputeBaseSize()로 재계산하여 설정.
    // 여기서는 상태만 갱신.
}
```

- 초기 로드에서도 런타임 토글에서도 동일한 경로
- `_autoCompleteBarAdded`는 `ComputeBaseSize()`가 바 높이 포함 여부를 결정하는 데 사용
- `ApplySuggestionBarHeight()` 호출 후 반드시 `ApplyScale()`이 뒤따라야 함

#### 호출부 패턴

```csharp
// OnConfigChanged
Dispatcher.InvokeAsync(() =>
{
    ApplySuggestionBarHeight();
    ApplyScale();
});

// OnLoaded
ApplySuggestionBarHeight();
ApplyScale();
```

#### KeyUnit PropertyChanged 핸들러 정리

기존 핸들러에서 `_appliedBarHeight` 갱신 코드 제거:

```csharp
// 삭제할 블록
if (_autoCompleteBarAdded && !_isCollapsed)
    _appliedBarHeight = mainVm.Keyboard.KeyRowHeight;
```

`_lastExpandedKeyRowHeight` 관련 코드도 삭제 (작업 07에서 접기 정리와 함께).

---

## ComputeBaseSize와의 연동

`ComputeBaseSize()`는 `_autoCompleteBarAdded`를 읽어 바 높이를 포함할지 결정:

```csharp
double barH = _autoCompleteBarAdded ? (BaseKeyUnit + 4.0) : 0;
```

플로우:
```
바 ON → _autoCompleteBarAdded = true
       → ApplyScale() → ComputeBaseSize()가 바 높이 포함
       → 창 높이 증가
       → UpdateKeyUnit이 넓어진 공간에 맞춰 KeyUnit 재계산

바 OFF → _autoCompleteBarAdded = false
        → ApplyScale() → ComputeBaseSize()가 바 높이 제외
        → 창 높이 감소
        → UpdateKeyUnit이 좁아진 공간에 맞춰 KeyUnit 재계산
```

**핵심**: `AdjustWindowHeight(±delta)` 대신 전체를 다시 계산.
delta가 누적되지 않으므로 상태 불일치 불가.

---

## 주의사항

- `AdjustWindowHeight`를 삭제하기 전 다른 호출부가 없는지 grep 확인
  → 현재 `ApplySuggestionBarHeight` 내부에서만 호출됨
- `AutoCompleteBarApplied` 공개 속성도 참조가 없으면 함께 삭제
- `ApplySuggestionBarHeight`에서 `Window.GetWindow(this)` 확인이 필요 없어짐
  (창 조작을 직접 하지 않으므로)
- 접힌 상태에서 바 토글 시 특별 처리 불필요
  → `_autoCompleteBarAdded`만 바꾸고 ApplyScale에서 `_isCollapsed`면 높이 28로 강제

---

## 완료 조건

- [ ] AdjustWindowHeight 메서드 삭제
- [ ] _appliedBarHeight, _lastExpandedKeyRowHeight 필드 삭제
- [ ] AppliedBarHeight, AutoCompleteBarApplied 속성 삭제
- [ ] ApplySuggestionBarHeight가 상태만 갱신하도록 단순화
- [ ] _initialized 가드 제거
- [ ] 바 ON → OFF 전환 후 키 크기 유지
- [ ] 바 OFF → ON 전환 후 키 크기 유지
- [ ] 접힌 상태에서 바 토글 후 펼치기 시 올바른 크기
