# 07 — "가/A" 토글 키 + 상단바 VK_HANGUL 비상 버튼

> **소요**: 2~3시간
> **선행**: 05 (`KeyboardViewModel`이 `ToggleKoreanSubmodeAction` 처리 가능)
> **후행**: 09, 10
> **관련 기획**: [refactor-unif-serialized-acorn.md D2, D3, §4-4, §4-5, §4-9, §5](../refactor-unif-serialized-acorn.md)

---

## 0. 이 태스크의 목표 한 줄

- 레이아웃의 **VK_HANGUL 슬롯을 "가/A" 토글 키로 교체**: 내부 Submode 전환. OS IME 건드리지 않음.
- **OS IME 한/영 키는 상단바의 비상 버튼**으로 이동: 거의 쓸 일 없는 VK_HANGUL 1회 전송.

---

## 1. 전제 조건

- 01에서 `ToggleKoreanSubmodeAction` 정의됨.
- 05에서 `KeyPressed`가 이 액션을 인식하고 `_autoComplete.ToggleKoreanSubmode()` 호출.
- `KeySlotVm.DisplayLabel`이 Submode 변경에 따라 "가"/"A" 재평가.

---

## 2. 현재 상태

### 2-1. `AltKey/layouts/qwerty-ko.json:81` (근방)

```json
{ "label": "한/영", "action": { "type": "SendKey", "vk": "VK_HANGUL" }, "width": 1.25 }
```

### 2-2. `AltKey/Views/MainWindow.xaml` 상단바

현재 헤더: 드래그 핸들 + 이동 + 설정/이모지/클립보드/접기/닫기 버튼. 06에서 자동완성 토글 추가.

---

## 3. 작업 내용

### 3-1. `qwerty-ko.json`의 VK_HANGUL 슬롯 교체

**파일**: `AltKey/layouts/qwerty-ko.json`

기존:
```json
{ "label": "한/영", "action": { "type": "SendKey", "vk": "VK_HANGUL" }, "width": 1.25 }
```

**변경**:
```json
{ "label": "가", "action": { "type": "ToggleKoreanSubmode" }, "width": 1.25 }
```

> `label`은 기본값이지만 실제 표시 라벨은 `DisplayLabel` 계산 프로퍼티(05-3-5)에서 `_autoComplete.ComposeStateLabel`로 오버라이드되므로 어떤 값이 있어도 무방. 명시성 위해 "가"로 둠.
> Space 너비가 맞지 않으면 전체 행의 너비를 미세 조정(예: Space 3.0 → 2.75). 렌더링 확인 후 조정.

### 3-2. "가/A" 토글 키의 DisplayLabel 바인딩

**파일**: `AltKey/ViewModels/KeyboardViewModel.cs` (또는 `KeySlotVm` 클래스 정의)

`KeySlotVm.DisplayLabel` 계산 시:

```csharp
public string DisplayLabel
{
    get
    {
        if (Slot.Action is ToggleKoreanSubmodeAction)
            return _autoComplete.ComposeStateLabel;   // "가" 또는 "A"

        return GetLabel(ShowUpperCase, _autoComplete.ActiveSubmode);
    }
}
```

> `_autoComplete`는 생성자 주입 또는 `ServiceProvider` 통해 접근.
> Submode 변경 시 `OnPropertyChanged(nameof(DisplayLabel))` 호출 필요. 05-3-6의 `SubmodeChanged` 구독에서 처리.

### 3-3. "가/A" 키의 시각적 강조

**파일**: `AltKey/Controls/KeyButton.xaml` 또는 스타일 리소스

너비 1.25(이미 JSON에서 지정). 추가로:
- `StyleKey: "SubmodeToggle"` 같은 스타일 키를 JSON에 추가하고 DarkTheme/LightTheme에서 해당 키에 대한 배경색·테두리 정의.
- 또는 `KeySlotVm.IsKoreanSubmodeToggle` 프로퍼티로 DataTrigger 바인딩.

스타일 예시(Themes/Generic.xaml 또는 KeyButton.xaml):
```xml
<DataTrigger Binding="{Binding IsKoreanSubmodeToggle}" Value="True">
    <Setter Property="Background" Value="{DynamicResource AccentBrush}" />
</DataTrigger>
```

> Dwell 오조작 방지를 위해 일반 키보다 살짝 넓고 색상으로 구분. 08의 AutomationProperties와 별개.

### 3-4. 상단바에 VK_HANGUL 비상 버튼 추가

**파일**: `AltKey/Views/MainWindow.xaml` (또는 KeyboardView.xaml 상단 헤더)

06의 자동완성 토글 옆에 버튼 추가:

```xml
<Button x:Name="OsImeHangulButton"
        Click="OnOsImeHangulClick"
        Width="32" Height="28"
        Margin="4,0,0,0"
        ToolTip="OS IME 한/영 전환 (거의 쓸 일이 없는 비상 버튼)"
        AutomationProperties.Name="OS IME 한영 전환"
        AutomationProperties.HelpText="거의 사용할 일이 없는 비상용 버튼입니다. 키보드 내부 한영 전환은 가/A 버튼을 사용하세요.">
    <TextBlock Text="한/영" FontSize="10" />
</Button>
```

Code-behind 또는 Command:

```csharp
// MainWindow.xaml.cs (또는 MainViewModel의 RelayCommand)
private void OnOsImeHangulClick(object sender, RoutedEventArgs e)
{
    _inputService.SendKeyPress(VirtualKeyCode.VK_HANGUL);
    _liveRegion?.Announce("OS IME 한영 전환 신호 전송됨");
    // InputSubmode는 건드리지 않음
}
```

> Command 방식이 MVVM 친화적. 예:
> ```csharp
> [RelayCommand]
> private void SendOsImeHangul()
> {
>     _inputService.SendKeyPress(VirtualKeyCode.VK_HANGUL);
>     _liveRegion?.Announce("OS IME 한영 전환 신호 전송됨");
> }
> ```

### 3-5. 상단바 레이아웃 조정

현재 헤더에 버튼이 많다면 공간 확보:
- 자동완성 토글 (AC)
- VK_HANGUL 비상 버튼 (한/영)
- 기존 설정/이모지/클립보드/접기/닫기

작은 버튼 2개 추가 시 가로폭 증가 고려. 창 너비 기본값 `WindowConfig.Width`와 충돌 없는지 확인.

### 3-6. "가/A" 버튼 누를 때의 부수 효과

기획 §5-1:
1. `KoreanInputModule.ToggleSubmode()` 호출.
2. 이전 Submode 조합 상태 플러시(`FinalizeComposition`).
3. Submode 플래그 반전, `ComposeStateLabel` 갱신.
4. PropertyChanged로 `DisplayLabel` 재평가 → UI 전환.
5. LiveRegion 공지(08에서 구현): "영어 입력 상태" 또는 "한국어 입력 상태".
6. OS IME에 신호 보내지 않음.

위 1~4는 03·05에서 이미 구현됨. 5는 08에서. 6은 구현하지 않음(신호 없음이 기본).

### 3-7. 비상 버튼 누를 때의 부수 효과

기획 §5-2:
1. `InputService.SendKeyPress(VK_HANGUL)` 1회.
2. OS IME 토글.
3. AltKey 내부 Submode 건드리지 않음.
4. LiveRegion 공지: "OS IME 한영 전환 신호 전송됨"(08).

---

## 4. 검증

1. 빌드 녹색.
2. 런타임:
   - qwerty-ko 로딩 → 81 라인 자리에 "가"가 표시. 너비 1.25.
   - "가" 클릭 → 버튼 라벨이 "A"로. 주변 키의 라벨이 알파벳으로 전환(HangulJamo→QuietEnglish).
   - "A" 클릭 → "가"로 복귀. 한글 자모 라벨로 복귀.
   - 타자 테스트:
     - "가" 상태(HangulJamo) → ㅎ+ㅐ 타자 → "해" 조합 정상.
     - "A" 상태(QuietEnglish) → `abc` 타자 → 메모장에 `abc` 그대로 표시, 영어 자동완성 제안 출현.
3. 상단바 "한/영" 버튼:
   - 클릭 → 작업표시줄 IME 인디케이터(EN/KO) 토글됨.
   - AltKey 내부 Submode는 변화 없음(의도).
4. 접근성(08 이후):
   - "가" 버튼 포커스 → Narrator가 "한국어 입력 중" 낭독.
   - Submode 토글 후 "영어 입력 상태" 낭독.

---

## 5. 함정 / 주의

- **레이아웃 JSON 파싱**: 새 JSON 값 `"type": "ToggleKoreanSubmode"`이 `KeyAction` polymorphism에 등록되어 있어야 한다(01-3-3 참조). 실행 시 파싱 에러 나면 01 구현 확인.
- **"가/A" 키에 `english_label` 없음**: JSON에 `english_label`을 지정하지 않았으므로 `QuietEnglish` 상태에서 `DisplayLabel`이 기본 `Slot.Label`("가")로 떨어질 수 있다. `KeySlotVm.DisplayLabel`은 **`ToggleKoreanSubmodeAction` 우선 분기**로 항상 `ComposeStateLabel`을 반환하게.
- **Space 너비 재조정**: 가운데 행의 총 너비가 다른 행과 어긋나지 않는지 시각 확인.
- **상단바 버튼 오조작**: Dwell 사용자 기준으로 너비 32px는 작다. 접근성 기준 최소 `KeyUnit × 0.9` 이상으로(기획 §4-4). 첫 버전에서는 32~40px로 두고 UX 확인 후 조정.
- **`SendKeyPress(VK_HANGUL)` 실제 동작**: OS가 기대하는 이벤트는 KEYDOWN+KEYUP 페어. `SendKeyPress`가 이미 그 의미일 것이지만 기존 구현 확인.
- **기존 `_inputService.HasActiveModifiers`**: 상단바 버튼 클릭은 Sticky 수식자가 활성 상태일 때 영향을 주지 않아야 한다. `SendKeyPress` 직전에 sticky를 해제할지 여부 결정. 기획에는 명시 없음. **현상 유지**(아무것도 해제하지 않음) 권장.

---

## 6. Critical Files

| 파일 | 수정 유형 |
|---|---|
| `AltKey/layouts/qwerty-ko.json` | 수정 (VK_HANGUL 슬롯 → ToggleKoreanSubmode) |
| `AltKey/Views/MainWindow.xaml` | 수정 (상단바 VK_HANGUL 버튼) |
| `AltKey/ViewModels/MainViewModel.cs` | 소폭 (`SendOsImeHangulCommand`) |
| `AltKey/ViewModels/KeyboardViewModel.cs` | 소폭 (`KeySlotVm.DisplayLabel` 특수 분기) |
| `AltKey/Controls/KeyButton.xaml` | 소폭 (토글 키 스타일) |

---

## 7. 커밋 메시지 초안

```
feat(layout): 가/A submode toggle in layout, emergency hangul in topbar

- qwerty-ko.json: replace VK_HANGUL slot with ToggleKoreanSubmode "가".
- MainWindow header: add OS IME 한/영 button (VK_HANGUL one-shot).
- KeySlotVm.DisplayLabel binds ComposeStateLabel for the toggle key.
- Accessibility names applied; help text warns the emergency button is rarely needed.
```
