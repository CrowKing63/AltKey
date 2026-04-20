# 작업 04: ResizeGrip 제거

> 의존성: 없음 (Phase A, 병렬 가능)

---

## 목표

드래그 리사이즈 그립을 완전히 제거하여 사용자가 실수로 창 크기를 변경하지 못하게 한다.
크기 조절은 작업 08의 설정 슬라이더로만 가능.

---

## 변경 파일

### 1. `Views/KeyboardView.xaml`

**삭제**: ResizeGrip Thumb 전체 (약 272~288행)

```xml
<!-- 삭제할 블록 -->
<Thumb x:Name="ResizeGrip"
       Width="20" Height="20"
       HorizontalAlignment="Right" VerticalAlignment="Bottom"
       Cursor="SizeNWSE"
       Panel.ZIndex="5"
       DragStarted="ResizeGrip_DragStarted"
       DragDelta="ResizeGrip_DragDelta">
    <Thumb.Template>
        <ControlTemplate>
            <Grid Background="Transparent">
                <Path Data="M 10 2 L 18 2 L 18 10 M 10 10 L 18 10 L 18 18"
                      Stroke="{DynamicResource TitleFg}" StrokeThickness="1.5" Fill="Transparent"
                      HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Grid>
        </ControlTemplate>
    </Thumb.Template>
</Thumb>
```

### 2. `Views/KeyboardView.xaml.cs`

**삭제할 멤버**:

```csharp
private double _resizeAspectRatio = 900.0 / 350.0;
```

**삭제할 메서드**:

```csharp
private void ResizeGrip_DragStarted(object sender, DragStartedEventArgs e)
private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
```

**검토 후 삭제 가능한 메서드**:

```csharp
private (double W, double H) ComputeMinWindowSize()
```

- `ComputeMinWindowSize()`는 `ResizeGrip_DragDelta`에서만 호출됨
- ResizeGrip 제거 후 더 이상 사용되지 않으므로 삭제
- 단, 다른 곳에서 참조하는지 grep 확인 후 삭제

### 3. `MainWindow.xaml`

**확인**: `ResizeMode="NoResize"` 유지 (이미 설정되어 있음, 변경 불필요)

---

## 접근성 영향

- 창 크기를 실수로 변경하던 경로가 원천 차단됨 — **긍정적**
- 대신 설정 패널에서 의도적으로만 크기 조절 가능 (작업 08)
- Narrator 등 스크린 리더 사용자는 설정 슬라이더로 크기 조절 (접근성 향상)

---

## 주의사항

- XAML에서 Thumb를 삭제할 때 그 주변의 주석(`<!-- 우하단 리사이즈 그립 -->`)도 함께 삭제
- Thumb의 `Panel.ZIndex="5"`가 다른 오버레이(이모지, 클립보드, 설정)에 영향 없는지 확인
  — 영향 없음: 이 오버레이들은 Panel.ZIndex="6" 또는 "10"
- `using System.Windows.Controls.Primitives;` — DragStartedEventArgs/DragDeltaEventArgs용
  다른 곳에서 사용하지 않으면 using 제거

---

## 완료 조건

- [ ] KeyboardView.xaml에서 ResizeGrip Thumb 전체 삭제
- [ ] KeyboardView.xaml.cs에서 관련 필드/메서드 삭제
- [ ] ComputeMinWindowSize() 미사용 확인 후 삭제
- [ ] 빌드 에러 없음
- [ ] 창에서 리사이즈 핸들이 보이지 않음
