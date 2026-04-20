# 키보드 창 높이 관리 — 설계 분석 및 변경 이력

> 최종 갱신: 2026-04-20

---

## 0. 사용자 생각

처음에는 키보드 창 크기 조절 매커니즘에 대해 별 계획 없이 시작했고, 기능이 추가되면서 키보드 접기, 단어 제안 패널 온오프 등 여러 변수가 추가되면서 창 크기가 왜곡되는 문제가 발생했다.
사용자 커스텀 레이아웃도 가능하게 된 시점에서 창 크기 조절 매커니즘을 재검토, 리팩토링할 필요가 생겼다.
일단 내가 생각할 때 가장 골격이 되는 순서는 이렇다.

1. 기본 레이아웃을 포함해서 사용자가 만드는 커스텀 레이아웃이 가장 기준이 된다. 각 레이아웃의 최대 키 행렬과 그 길이를 토대로 보더를 그린다.
2. 보더의 최대 가로폭이 전체 메인 창의 최소 가로폭의 기준이 된다(테두리나 기타 필요한 공간은 추가될 수 있다).
3. 메인 창의 전체 최소 세로폭(높이)은 다음 요소들의 합이 된다.
    1. 보더의 최대 세로폭(높이)
    2. 헤더의 높이(현재 키보드를 접으면 헤더 높이만 남지만 그것은 고려하지 않는다. 어차피 키보드가 새로 켜지면 펼쳐진 채 실행되므로.)
    3. 자동완성 바의 높이(온오프, 오프일 때 0)
4. 2번과 3번이 전체 메인 창의 기본 크기가 된다.
5. 설정에서 창 크기를 조절할 수 있게 하는데 백분율 100(4번 값)퍼센트를 기본으로 10퍼센트 단위로 증감할 수 있다. 최종적으로 렌더링 되는 윈도우 사이즈가 이 결과값이다.

---

## 1. 문제 개요

키보드의 전체 창 높이가 **접기/펼치기**, **자동완성 바 토글** 등의 변수에 의해 수시로 변하며,
종료 시 해당 높이가 `config.json`에 저장된 후 재시작 시 복원되면서 **키 배열 크기가 왜곡되는** 문제.

증상이 랜덤하게 발생하여 재현 조건을 특정하기 매우 어려움.

---

## 2. 현재 창 높이 관리 구조

### 2-1. 관련 파일

| 파일 | 역할 |
|---|---|
| `Models/AppConfig.cs` | `WindowConfig` — Left, Top, Width, Height 기본값 정의 |
| `Services/ConfigService.cs` | config.json 읽기/쓰기 |
| `MainWindow.xaml.cs` | 창 위치/크기 복원(`RestoreWindowPosition`) 및 저장(`OnClosing`) |
| `Views/KeyboardView.xaml.cs` | 키보드 높이 관리의 핵심 — 접기, 바 토글, 리사이즈 |
| `ViewModels/SettingsViewModel.cs` | 창 레이아웃 초기화 명령 |
| `Views/SettingsView.xaml` | 설정 UI — 초기화 버튼 |

### 2-2. 창 높이에 영향을 주는 변수

```
창 총 높이 = 헤더(28px) + [자동완성 바(KeyRowHeight)] + 키보드 영역 + 여백
```

| 변수 | 높이 변화 | 관련 코드 |
|---|---|---|
| 접기/펼치기 | 28px ↔ 저장된 높이 | `CollapseButton_Click` |
| 자동완성 바 ON/OFF | ±KeyRowHeight | `ApplySuggestionBarHeight` |
| 사용자 리사이즈 | 자유 변화 | `ResizeGrip_DragDelta` |

### 2-3. 높이 저장·복원 흐름

**저장** (`MainWindow.OnClosing`, Line 109-126):
```
접힌 상태 → _expandedHeight 저장
펼쳐진 상태 → window.Height 그대로 저장
```

**복원** (`MainWindow.RestoreWindowPosition`, Line 132-162):
```
config에서 Width, Height 읽기 (최소 400×180)
Left, Top이 -1이거나 화면 밖이면 중앙 하단 배치
```

**초기화** (`KeyboardView.OnLoaded`, Line 42-82):
```
UpdateKeyUnit(window.Width) → KeyUnit 폐쇄형 계산
ApplySuggestionBarHeight() → 바 상태 초기화
```

### 2-4. KeyUnit 폐쇄형 계산 (`UpdateKeyUnit`, Line 179-203)

```csharp
// 바 표시 여부를 이미 반영하여 자동 계산
bool barVisible = AutoCompleteBar?.Visibility == Visibility.Visible;
double barH = barVisible ? AutoCompleteBar!.ActualHeight : 0;
double totalBudget = KeyboardBorder.ActualHeight + barH;
double availH = totalBudget - KbVerticalPad - (barVisible ? 4.0 : 0);

// KeyUnit = min(너비 기준 단위, 높이 기준 단위)
vm.Keyboard.KeyUnit = Math.Max(MinKeyUnit, Math.Min(MaxKeyUnit, Math.Min(kW, kH)));
```

---

## 3. 2026-04-20 시도 내역

### 3-1. 시도 A: 높이 정규화 (Phase 1) — **되돌림**

**목표**: config에 항상 "바 OFF 기준 높이"를 저장하고, 시작 시 바 ON이면 보정.

#### 변경 내용 (모두 되돌림)

1. **`KeyboardView.xaml.cs`**: `AppliedBarHeight`, `AutoCompleteBarApplied` 공개 속성 추가
2. **`KeyboardView.xaml.cs`** `ApplySuggestionBarHeight`: 첫 초기화 시 `AdjustWindowHeight(win, _appliedBarHeight)` 호출
3. **`MainWindow.xaml.cs`** `OnClosing`: 바 높이 차감 정규화 (`saveHeight -= AppliedBarHeight`)

#### 되돌린 이유: SizeChanged 피드백 루프

```
AdjustWindowHeight → window.Height 증가
  → SizeChanged 발생
    → UpdateKeyUnit 재계산
      → KeyRowHeight 변화
        → SuggestionBar.Height 바인딩 갱신
          → KeyboardBorder.SizeChanged
            → UpdateKeyUnit 재호출
              → … 무한 반복 가능
```

초기화 시점에는 레이아웃이 완전히 확정되지 않은 상태이므로
창 높이를 강제로 변경하면 WPF 레이아웃 시스템과 충돌하여 불안정해짐.

### 3-2. 시도 B: 창 레이아웃 초기화 버튼 (Phase 2) — **유지**

**목표**: 오염된 창 크기 값을 초기 상태로 되돌리는 탈출구 제공.

#### 변경 내용 (현재 적용됨)

| 파일 | 변경 |
|---|---|
| `Models/AppConfig.cs` | 변경 없음 — 기본값(900×320, Left=-1, Top=-1) 활용 |
| `MainWindow.xaml.cs:26` | `ResetPending` 플래그 추가 |
| `MainWindow.xaml.cs:111` | `OnClosing`에서 `ResetPending` 시 저장 스킵 |
| `ViewModels/SettingsViewModel.cs:361-386` | `ResetWindowLayoutCommand` 추가 |
| `Views/SettingsView.xaml:393-402` | 안내 텍스트 + "창 레이아웃 초기화" 버튼 |

#### 동작 흐름

```
버튼 클릭
  → config.Window = new WindowConfig() (기본값으로 초기화)
  → config.AutoCompleteEnabled = false
  → MainWindow.ResetPending = true
  → 새 프로세스 시작
  → ShutdownCurrentApp()
    → OnClosing: ResetPending == true → 창 설정 저장 스킵
    → 종료
  → 새 프로세스: 기본 창 크기(900×320) + 바 OFF로 시작
```

#### 보호 장치: ResetPending

`ResetPending`이 없으면 `OnClosing`에서 현재(오염된) 창 크기를 다시 저장하여
초기화가 무효화됨. 이 플래그는 종료 시 창 설정 저장만 건너뛰며
레이아웃, 사용자 단어, 테마 등 다른 설정에는 영향 없음.

---

## 4. 남아있는 근본 문제

현재 구조에서 높이 저장은 "종료 시 총 높이(바 포함)"를 그대로 저장.
`UpdateKeyUnit`의 폐쇄형 계산이 바 유무에 따라 KeyUnit을 자동 조정하므로
**대부분의 경우 정상 동작**하지만, 다음 시나리오에서 여전히 취약:

| 시나리오 | 위험 |
|---|---|
| 바 ON → 리사이즈 → 접기 → 종료 → 재시작 | `_expandedHeight`에 바 포함 높이가 저장되어 있을 수 있음 |
| 바 OFF → 리사이즈 → 바 ON → 종료 → 재시작 | 바 포함 높이 저장 후 바 ON으로 재시작 → 정상 |
| 바 ON → 리사이즈 → 바 OFF → 접기 → 종료 → 재시작 | 바 미포함 높이가 `_expandedHeight`에 반영될 수 있음 |

이 문제의 증상이 랜덤하게 나타나는 이유는 WPF 레이아웃 계산 타이밍과
`SizeChanged` 이벤트의 비결정적 발생 순서 때문으로 추정됨.

---

## 5. 개선 방향 분석 — 세 가지 접근법

> 2026-04-20 분석. 접근법 1 시도 후 **되돌림**.

문제의 본질은 WPF 선언적 UI에서 **물리적 창 크기(Window.Height)** 와
**논리적 상태(접기, 바 ON/OFF)** 를 동일한 라이프사이클에서 혼합 관리할 때
발생하는 전형적인 동기화 문제.

### 5-1. 접근법 1: 상태 변경과 사용자 리사이즈의 완전한 분리 — **시도 후 되돌림**

**핵심**: `OnClosing`에서 창 크기를 저장하지 않고,
**리사이즈 그립 조작 완료 직후(DragCompleted)에만** 저장.

**원리**:
- 접기/바 토글은 창 높이를 일시적으로만 변경 — config에 영향 없음
- 사용자가 의도적으로 크기를 조절한 직후만 안전하게 저장
- DragCompleted는 레이아웃이 완전히 안정화된 후 발생하므로 피드백 루프 없음

**구현**:
1. `KeyboardView.xaml`: ResizeGrip에 `DragCompleted` 이벤트 추가
2. `KeyboardView.xaml.cs`: `ResizeGrip_DragCompleted`에서 바 높이 차감 정규화 후 저장
3. `MainWindow.xaml.cs`: `OnClosing`에서 Width/Height 저장 제거, Left/Top만 저장

**장점**:
- 코드 변경량 최소 (~3곳)
- SizeChanged 피드백 루프와 완전 무관
- 기존 UpdateKeyUnit, AdjustWindowHeight 모두 유지

**단점**:
- 사용자가 리사이즈를 한 적 없으면 config 기본값(900×320) 계속 사용 → 문제없음
- 접힌 상태에서는 리사이즈 불가 → DragCompleted는 항상 펼쳐진 상태 → 정규화 유효

### 5-2. 접근법 2: KeyUnit을 단일 진실 공급원(SSOT)으로 사용

**핵심**: Window.Height 대신 KeyUnit(키 하나의 크기)을 저장하고,
시작 시 KeyUnit → Height 상향식 계산.

**장점**:
- 이론적으로 가장 깔끔 — 물리적 픽셀이 아닌 논리적 척도를 기준
- 바 ON/OFF와 완전 독립

**단점/위험**:
- 정확한 상향식 공식 필요: `Height = 28 + (바? KeyRowHeight:0) + rows×KeyUnit + rows×KeyMargin + KbVerticalPad + ?`
  - `?` 부분(패딩, 기타 오버헤드)이 시각 트리에 분산 → 정확한 계산 어려움
- 현재 `UpdateKeyUnit`의 하향식(창→키) 계산과 충돌
  - 폐쇄형 식은 WPF의 이미 확정된 레이아웃 결과(`ActualHeight`)를 읽는 방식이라 안정적
  - 상향식으로 뒤집으면 WPF 레이아웃과 경쟁
- 레이아웃 행 수 변경 시 같은 KeyUnit이라도 창 높이가 달라짐 → 처리 복잡

**평가**: 이론적으론 완벽하나 상향식 공식의 정확성 보장이 어렵고
현재 안정적인 하향식 계산과 충돌 위험이 있어 보류.

### 5-3. 접근법 3: WPF SizeToContent에 위임

**핵심**: `SizeToContent="Height"` 설정 후 WPF 레이아웃 엔진이 창 높이를 자동 결정.

**구조**:
```
Grid
├── Row 0 (Auto): 헤더
├── Row 1 (Auto): 자동완성 바 (Visibility.Collapsed → 자동 높이 0)
└── Row 2 (*):    키보드
```

**장점**:
- 가장 WPF다운 접근
- 바 Visibility만 바꾸면 창 높이 자동 조정

**단점/위험**:
- `SizeToContent="Height"`에서는 사용자 수동 높이 조절 불가
  - 리사이즈 시 `Manual` 전환 → 다시 `SizeToContent` 복귀… 복잡한 상태 관리
- 현재 비율 고정 리사이즈(`ResizeGrip_DragDelta`)가 `window.Height` 직접 조작 → 전면 재작성
- 접기 애니메이션(`AnimateWindowHeight` 28px)이 SizeToContent와 충돌
- 헤더 28px 고정, 이모지/클립보드 패널 오버레이 등 기존 레이아웃 전체 영향

**평가**: 새 프로젝트라면 최선이나, 이미 완성된 시스템 위에 얹으려면
재작업 범위가 너무 크고 새 버그 유발 위험이 높아 보류.

### 5-4. 비교 요약

| 기준 | 접근법 1 | 접근법 2 | 접근법 3 |
|---|---|---|---|
| 코드 변경량 | 소 (~3곳) | 중 (~5-6곳) | 대 (사실상 재작성) |
| 기존 로직 영향 | 거의 없음 | UpdateKeyUnit 충돌 위험 | 전면 재작성 |
| 피드백 루프 위험 | 정규화 시 높음 | 낮음 | 낮음 |
| 버그 유발 위험 | 높음 (상태 불일치) | 중간 | 높음 |
| 실제 테스트 결과 | **실패** | 미시도 | 미시도 |

---

## 6. 접근법 1 시도 및 실패 분석 (2026-04-20)

### 6-1. 변경 내용 (모두 되돌림)

| 파일 | 변경 |
|---|---|
| `Views/KeyboardView.xaml:278` | ResizeGrip에 `DragCompleted` 이벤트 연결 |
| `Views/KeyboardView.xaml.cs:265-278` | `ResizeGrip_DragCompleted` 핸들러 — 바 높이 차감 정규화 후 Width/Height 저장 |
| `MainWindow.xaml.cs:111-121` | `OnClosing`에서 Width/Height 저장 제거, Left/Top만 유지 |

### 6-2. 실패 원인: 정규화-보정의 순환 의존

접근법 1은 "바 OFF 기준 높이"를 정규화하여 저장하는 방식이었다.
그러나 **정규화된 높이를 저장하려면 반드시 시작 시 보정이 필요**하고,
그 보정이 다시 피드백 루프를 유발하는 순환 구조가 핵심 문제.

#### 실패 시나리오

```
1. 사용자가 리사이즈 (바 ON 상태) → DragCompleted → 정규화 높이 320 저장 (바 높이 48 차감)
2. 사용자가 바를 끔 → 창 높이가 368→320으로 줄어듦 (AdjustWindowHeight(-48))
3. 종료 → OnClosing은 Left/Top만 저장
4. 재시작 → config.Height = 320 복원
5. 그런데 바가 꺼진 상태로 시작 → 320은 "바 OFF 기준 높이"와 일치 → 정상

하지만 역방향:
1. 사용자가 리사이즈 (바 OFF 상태) → DragCompleted → 높이 320 저장
2. 사용자가 바를 켬 → 창 높이가 320→368로 늘어남 (AdjustWindowHeight(+48))
3. 종료 → OnClosing은 Left/Top만 저장
4. 재시작 → config.Height = 320 복원
5. 바가 켜진 상태로 시작 → 창 높이 320에 바가 KeyRowHeight(48) 차지
   → 키보드 영역이 272로 줄어듦 → 키 크기 왜곡 ★
```

이 시나리오에서 시작 시 바 높이를 다시 더해주면 해결되지만,
그것이 바로 시도 A에서 피드백 루프를 유발했던 `AdjustWindowHeight` 호출이다.

#### 순환 의존 다이어그램

```
정규화 저장 → 시작 시 보정 필요 → AdjustWindowHeight → SizeChanged
     ↑                                              ↓
     └── 피드백 루프 방지 = 보정 생략 ← UpdateKeyUnit ← KeyRowHeight 변화
                         ↓
              보정 없이 시작 → 바 상태 불일치 → 키 크기 왜곡
```

#### 근본 원인 요약

| 문제 | 설명 |
|---|---|
| **저장 시점과 사용 시점의 상태 불일치** | DragCompleted 시점의 바 상태와 재시작 시점의 바 상태가 다를 수 있음 |
| **정규화와 보정은 한 쌍** | 정규화된 값을 저장하려면 반드시 시작 시 보정이 필요한데, 보정은 피드백 루프를 유발 |
| **OnClosing 저장 제거의 부작용** | 리사이즈 후 바 토글/접기 등의 상태 변화가 config에 반영되지 않아 저장값이 낡음(stale) |

### 6-3. 결론

접근법 1은 "저장 시점을 DragCompleted로 분리"한다는 점에서 합리적이었으나,
**정규화(저장 시 바 높이 차감)와 보정(시작 시 바 높이 복원)이 분리될 수 없는 순환 의존** 때문에 실패.
보정을 하면 피드백 루프, 보정을 안 하면 상태 불일치로 키 크기 왜곡이 발생함.

이 문제를 해결하려면 **정규화 없이** DragCompleted에서 "현재 총 높이"를 그대로 저장하는 방식도 가능하지만,
이 경우 OnClosing에서 이미 동일한 역할을 하므로 개선 효과가 없음.

---

## 7. 현재 코드에서 주의할 점

### 절대 건드리지 말 것

- `UpdateKeyUnit`의 폐쇄형 계산식 — 바 표시 여부를 이미 반영하고 있음
- `ApplySuggestionBarHeight`의 `!_initialized` 경로 — 창 높이를 조작하지 않는 것이 안정적
- `AdjustWindowHeight` 호출 — `SizeChanged` 체인을 유발할 수 있음

### 변경 시 반드시 확인할 것

- 접기 → 펼치기 → 종료 → 재시작 후 키 크기가 유지되는지
- 바 ON 상태에서 리사이즈 → 종료 → 재시작 후 키 크기가 유지되는지
- 바 ON → 접기 → 종료 → 재시작 → 펼치기 후 키 크기가 유지되는지
- 위 세 가지를 바 OFF 상태에서도 반복 확인
