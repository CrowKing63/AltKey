# 작업 02: 기준 크기 계산 엔진

> 의존성: 없음 (Phase A, 병렬 가능)

---

## 목표

현재 레이아웃의 메트릭(MaxRowUnits, MaxRowCount, RowCount)을 바탕으로
"Scale 100%일 때의 창 크기"를 계산하는 메서드를 `KeyboardView`에 추가.

---

## 배경: 레이아웃 단위 체계

- 레이아웃 JSON의 `"width": 1.0` = 1단위 = **BaseKeyUnit(50px)** @ 100%
- `MaxRowUnits` = Σ(각 열의 가장 긴 행의 width 총합) + Σ(열 간격 Gap)
- `MaxRowCount` = Σ(각 열의 가장 긴 행의 키 개수) — 마진 계산용
- `RowCount` = max(열별 행 개수)

한국어 기본 레이아웃 예시:
- MaxRowUnits ≈ 15.0, MaxRowCount ≈ 14.0, RowCount = 5

---

## 변경 파일

### `Views/KeyboardView.xaml.cs`

#### 추가할 상수

```csharp
private const double BaseKeyUnit = 50.0;
private const double HeaderHeight = 28.0;
private const int MinScale = 60;
private const int MaxScale = 200;
```

#### 추가할 메서드

```csharp
/// 현재 레이아웃 기준 Scale 100% 창 크기 계산
private (double Width, double Height) ComputeBaseSize()
{
    if (DataContext is not MainViewModel vm)
        return (900.0, 320.0);

    double units  = Math.Max(1, vm.Keyboard.MaxRowUnits);
    double wKeys  = Math.Max(1, vm.Keyboard.MaxRowCount);
    double rows   = Math.Max(1, vm.Keyboard.RowCount);

    double baseW = units * BaseKeyUnit
                 + wKeys * KeyMargin
                 + KbHorizontalPad;

    double keyboardH = rows * BaseKeyUnit
                     + rows * KeyMargin
                     + KbVerticalPad;

    double barH = _autoCompleteBarAdded ? (BaseKeyUnit + 4.0) : 0;

    double baseH = HeaderHeight + barH + keyboardH;

    return (
        Math.Max(baseW, AbsMinWindowWidth),
        Math.Max(baseH, AbsMinWindowHeight)
    );
}
```

#### 계산 설명

```
baseW = MaxRowUnits × 50 + MaxRowCount × 4 + 12
      = 키 총폭            + 키 마진 총합       + 보더 좌우 패딩

baseH = 28               + (바: 54 | 0) + (rows×50 + rows×4 + 8)
        헤더                바 높이         키보드 영역
```

- `바 높이 = BaseKeyUnit + 4.0 = 54px` (KeyRowHeight 공식과 동일: `KeyUnit + 4`)
- 바 OFF 시 barH = 0

#### 한국어 기본 레이아웃 (100%):
```
baseW = 15×50 + 14×4 + 12 = 750 + 56 + 12 = 818 → max(818, 400) = 818
baseH(바 OFF) = 28 + 0 + (5×50 + 5×4 + 8) = 28 + 258 = 286 → max(286, 180) = 286
baseH(바 ON)  = 28 + 54 + 258 = 340
```

---

## 주의사항

- 이 메서드는 값을 계산만 하고 창 크기를 설정하지 않음
- 실제 적용은 작업 03에서 `ComputeBaseSize() × Scale / 100` 방식
- `ComputeMinWindowSize()` (기존 메서드)는 작업 04에서 ResizeGrip 제거 후 삭제

---

## 완료 조건

- [ ] `ComputeBaseSize()` 메서드 구현
- [ ] BaseKeyUnit, HeaderHeight, MinScale, MaxScale 상수 정의
- [ ] 한국어 기본 레이아웃에서 계산값이 합리적인지 수동 검증
