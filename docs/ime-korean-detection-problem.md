# IME 한/영 자동 감지 문제 — 진단 및 해결 방안

> 작성일: 2026-04-16  
> 최종 갱신: 2026-04-16  
> 상태: **해결 (Unicode 우회 방식 채택)**

---

## 1. 문제 증상

한국어 가상 키보드(qwerty-ko 레이아웃)에서 **한/영 키(VK_HANGUL)로 영어 모드로 전환해도 자동 완성이 한글 모드로만 동작**한다. 영어 모드에서 타자를 쳐도 영어 단어 추적/제안이 전혀 되지 않는다.

---

## 2. 관련 파일 구조

```
AltKey/
├── Services/
│   ├── AutoCompleteService.cs    → OnHangulInput, OnKeyInput(vk), AcceptSuggestion
│   ├── InputService.cs           → InputMode (Unicode/VirtualKey), SendAtomicReplace,
│   │                               IsImeKorean() (관리자 모드 전용), TrackedOnScreenLength
│   ├── WordFrequencyStore.cs     → RecordWord, GetSuggestions, Save (JSON 직렬화)
│   ├── KoreanDictionary.cs       → 내장 빈도 사전 + 사용자 학습 결합 제안
│   └── HangulComposer.cs          → 초성/중성/종성 조합 추적, CompositionDepth 속성
├── ViewModels/
│   ├── KeyboardViewModel.cs      → KeyPressed, _isKoreanInput 내부 토글,
│   │                               HandleKoreanLayoutKey (모드별 분기)
│   ├── SuggestionBarViewModel.cs → AcceptSuggestion (모드별 BS+재전송)
│   └── MainViewModel.cs          → IsKoreanMode 설정
└── Platform/
    └── Win32.cs                  → IMM32 P/Invoke (관리자 모드 전용)
```

---

## 3. 핵심 데이터 흐름 (현재 — Unicode 우회 방식)

```
실행 시:
  InputService.Mode = 관리자 권한 ? VirtualKey : Unicode

키 입력 시 (Unicode 모드 — 일반 실행):
  KeyboardViewModel.KeyPressed()
    → _isKoreanInput 내부 상태로 한/영 판별 (IME 조회 없음)
    → 한국어 + 자모 키: HangulComposer.Feed() → SendAtomicReplace (BS + SendUnicode)
    → 한국어 + 영문 키: 조합 완료 후 VK 코드 전송
    → 영어 + 알파벳: SendUnicode('a') (IME 우회)
    → 영어 + 조합키(modifier 활성): VK 코드 전송
    → 한/영 키: _isKoreanInput 토글 + VK_HANGUL 전송 (IME 동기화)

키 입력 시 (VirtualKey 모드 — 관리자 실행):
  기존 방식 유지 (VK 코드 → IME 변환)
  IsImeKorean() 폴링으로 보정 (100ms 타이머)
```

### SendAtomicReplace 원자적 전송

```
조합 변경 시 (예: "ㅎ" → "하"):
  prevLen = TrackedOnScreenLength     // 1
  HangulComposer.Feed("ㅏ")
  newOutput = "하"                    // 길이 1
  SendInput([ BS_DOWN, BS_UP,        // 이전 "ㅎ" 삭제
              유니코드('하') DOWN/UP ]) // 새 조합 전송
  → 단일 SendInput 호출 = 깜빡임 없는 원자적 교체
```

---

## 4. 시도한 방법과 결과

| # | 방법 | 결과 |비고|
|---|------|------|-------|
| 1 | `IsImeKorean` 내부 토글 (VK_HANGUL 감지 시 반전) | AltKey 내부 상태와 OS IME 동기화 안 됨 | 하드웨어 키보드나 다른 앱에서 한/영 전환 시 어긋남 |
| 2 | `GetKeyState(VK_HANGUL)` + `AttachThreadInput` | 항상 한글로 감지 | `AttachThreadInput` 100ms마다 호출 시 **포커스 탈취 문제** 발생 |
| 3 | IMM32 API (`ImmGetDefaultIMEWnd` → `ImmGetContext`) + `GetForegroundWindow` | 항상 한글로 감지 | `IsImeKorean()`이 `true`만 반환 |
| 4 | `GetGUIThreadInfo`로 `hwndFocus` 획득 후 IMM32 API | 동일하게 항상 한글 감지 | `AttachThreadInput` 제거했지만 여전히 IME 영어 모드 감지 안 됨 |

---

## 5. 공통 문제점

`InputService.IsImeKorean()`의 모든 실패 경로가 `return true`다. 즉, IMM32 API가 제대로 동작하지 않으면 **기본값이 한글**이 되어 영어 모드를 감지할 수 없다.

```csharp
// InputService.cs — IsImeKorean() 축약
public bool IsImeKorean()
{
    try
    {
        // ... IMM32 API 호출들 ...
        return (conversion & IME_CMODE_NATIVE) != 0;  // ← 여기까지 도달 못 함
    }
    catch
    {
        return true;  // ← 기본값이 한글! 영어 모드 감지 불가
    }
}
```

가능성 높은 원인:
- `ImmGetDefaultIMEWnd(hwndFocus)`가 `IntPtr.Zero`를 반환 (AltKey가 `WS_EX_NOACTIVATE` 창이라 IME 창 핸들이 없음)
- 또는 `ImmGetContext(hIMEWnd)`가 `IntPtr.Zero`를 반환
- 또는 타겟 프로세스의 IME 컨텍스트에 접근할 권한이 없음 (다른 프로세스)

---

## 6. 근본적 의문: 이 접근방식을 계속 갈 것인가?

### 현재 방식의 문제

AltKey는 `WS_EX_NOACTIVATE` 속성을 가진 가상 키보드다. 자체 창이 활성화되지 않으므로, **다른 프로세스의 IME 상태를 읽어오는 것**이 핵심인데, 이것이 정상 작동하지 않는다.

### 대안

#### 대안 1: VK_HANGUL 직접 감지 + 내부 상태 관리 (권장)

- 가상 키보드의 한/영 키가 눌리면 AltKey 내부에서 `_isImeKorean = !_isImeKorean` 토글
- **장점**: 즉각적 반응, 외부 API 의존 없음, 포커스 문제 없음
- **단점**: 하드웨어 키보드나 OS 수준에서 한/영 전환 시 동기화 안 됨
- **보완**: IMM32 폴링은 동기화 보정 용도로만 사용. 실패 시 기본값 `true` 대신 **현재 내부 상태 유지**

#### 대안 2: 저수준 키보드 훅으로 VK_HANGUL 시스템 전역 감지

- 이미 `WH_KEYBOARD_LL` 훅이 `Win32.cs`에 P/Declare되어 있음
- 시스템 전역에서 VK_HANGUL 키 눌림을 감지하여 내부 상태 토글
- **장점**: 하드웨어 키보드의 한/영 키도 감지 가능
- **단점**: 훅 콜백에서 IME 상태 판단이 복잡할 수 있음, 훅 설치 시 성능 영향

#### 대안 3: 디버그 로깅 추가 후 원인 정밀 진단

- `IsImeKorean()` 내부에 `Debug.WriteLine`으로 각 단계 결과 로깅
- `hwndFocus`, `hIMEWnd`, `hIMC`, `conversion` 값을 실시간으로 확인
- IMM32 API가 **어디서** 실패하는지(반환값이 0인지, conversion 플래그가 맞는지) 정확히 파악
- 이후 실패 지점에 맞는 정밀 수정 가능

### 권장 전략

**대안 1 + 대안 3 조합**:

1. VK_HANGUL 기반 내부 토글을 **주 방식**으로 채택
2. IMM32 폴링으로 주기적 동기화 **보정** (실패 시 내부 상태 유지, 기본값으로 되돌리지 않음)
3. 진단 로깅으로 IMM32가 왜 실패하는지 파악 → 보정 로직 정밀화

---

## 6.5 최종 해결: Unicode 직접 입력 우회 방식 (2026-04-16)

> **채택 배경**: IME 상태를 읽는 모든 시도가 실패했으므로, IME를 아예 우회하는 방식으로 전환.  
> **벤치마킹**: Windows 8+ 터치 키보드(TabTip.exe)는 IME 상태와 무관하게 자체 텍스트를 생성하여 입력.

### 핵심 아이디어

한국어 모드에서 VK 코드를 Windows IME로 보내지 않고, `HangulComposer`에서 직접 조합한 유니코드 문자를 `SendUnicode()`로 전송하여 IME를 완전히 우회한다.

```
[기존] 키 클릭 → SendInput(VK_Q) → IME 변환 → ㅂ 표시
[현재] 키 클릭 → HangulComposer.Feed("ㅂ") → SendUnicode("ㅂ") → ㅂ 표시
```

### 하이브리드 아키텍처: 실행 권한에 따른 모드 분기

관리자 모드에서는 보안 프로그램(은행, 게임 등) 호환성을 위해 기존 VK 방식을 유지해야 하므로, 실행 시 권한을 검사하여 모드를 결정한다.

```
┌─────────────────────────────────────────────────────────┐
│              실행 모드 판단 (시작 시 1회)                 │
│            CheckElevated() → InputMode                   │
├─────────────────────────┬───────────────────────────────┤
│   일반 모드 (Unicode)    │    관리자 모드 (VirtualKey)    │
│                         │                               │
│   한국어: SendAtomic    │   한국어: SendInput(VK)       │
│           Replace       │           + IsImeKorean()     │
│   영어: SendUnicode     │   영어: SendInput(VK)         │
│   조합키: VK 전송       │   조합키: VK 전송             │
│                         │                               │
│   IME 상태: 관심 없음   │   IME 상태: 폴링 감지 시도    │
│   자동완성: 100% 정확   │   자동완성: 내부 토글 기반    │
│   보안앱: ❌             │   보안앱: ✅                  │
└─────────────────────────┴───────────────────────────────┘
```

### 해결한 문제들

| 문제 | 해결 방법 |
|------|-----------|
| IME 한/영 상태 감지 불가 | Unicode 모드에서는 IME 조회 자체가 불필요. 내부 `_isKoreanInput` 토글만 사용 |
| 영어 모드에서 q → ㅂ (IME 변환) | Unicode 모드에서 영문 알파벳도 `SendUnicode('q')`로 직접 전송 |
| Ctrl+C 등 조합키 동작 안 함 | `_inputService.HasActiveModifiers` 확인 → modifier 활성 시 자모/유니코드 처리 스킵 |
| 조합 중 백스페이스 (자모/음절 단위) | `HangulComposer.Backspace()` + `SendAtomicReplace`로 원자적 교체 |
| 조합 변경 시 깜빡임 | BS + SendUnicode를 단일 `SendInput` 호출에 묶어서 원자적 전송 |
| 관리자 모드에서 보안앱 호환 | 관리자 권한 감지 시 자동으로 VirtualKey 모드로 전환 |

### 변경된 파일 (이번 세션)

| 파일 | 변경 내용 |
|------|-----------|
| `Services/InputService.cs` | `InputMode` enum, `Mode` 속성, `CheckElevated()`, `SendAtomicReplace()`, `TrackedOnScreenLength`, `HasActiveModifiers` 추가. `HandleAction`에서 자동완성 호출 제거 |
| `Services/AutoCompleteService.cs` | `OnKeyInput(vk)`로 시그니처 단순화, `imeKorean` 파라미터 제거 |
| `ViewModels/KeyboardViewModel.cs` | `_isKoreanInput` 내부 토글, `HandleKoreanLayoutKey()` (Unicode/VirtualKey 분기), `HandleEnglishSubMode()` (SendUnicode로 영문 전송), `ShouldSkipHandleAction()`, `UpdateImeState()` VirtualKey 모드로 제한 |
| `ViewModels/SuggestionBarViewModel.cs` | Unicode 모드에서 `SendAtomicReplace` 사용, VirtualKey 모드는 기존 BS+SendUnicode 유지 |

### 남은 과제

| 항목 | 상태 | 내용 |
|------|------|------|
| 하드웨어 한/영 키 동기화 | **예정** | `WH_KEYBOARD_LL` 훅으로 전역 VK_HANGUL 감지 → 내부 상태 동기화 |
| 관리자 모드 IME 감지 검증 | **예정** | 관리자 모드에서 `IsImeKorean()`이 실제 동작하는지 로깅으로 확인 |
| IMM32 API 제거 | **보류** | 하드웨어 키보드 동기화 완료 후 VirtualKey 모드에서만 사용하는 것으로 정리 |

---

## 7. 이번 세션에서 추가로 해결한 문제들

### 7-1. user-words.json 저장 문제 (해결)

- **원인**: `AutoCompleteService.OnKeyInput()`에서 `IsKoreanMode` 시 `return`하여 영어 단어 추적 안 됨, 한글 모드에서 구분자 입력 시 `ResetState()`만 호출하고 단어 저장 안 함
- **해결**: `CompleteCurrentWord()` 메서드 추가 (현재 조합 중인 단어 저장 후 초기화), `WordFrequencyStore.RecordWord()`에서 즉시 `Save()` 호출

### 7-2. 한글 자동 완성 제안 패널 미표시 문제 (해결)

- **원인**: `IsKoreanMode` 조건을 제거하면서 한글 제안 로직이 깨짐
- **해결**: IME 상태에 따른 분기 로직 재구성 (`IsKoreanMode && imeKorean` 시에만 한글 경로)

### 7-3. 한글 자동 완성 교체 버그 — "문문제" (해결)

- **원인**: `AcceptSuggestion`의 백스페이스 계산이 `CompletedLength`만 사용하여 조합 중인 음절의 분해 백스페이스를 누락. 예: "문" 조합 중 → 1회 백스페이스로 처리해야 하는데 0회로 계산
- **해결**: `HangulComposer.CompositionDepth` 속성 추가 (초성=1, 초성+중성=2, 초성+중성+종성=3). `AcceptSuggestion`에서 `CompletedLength + CompositionDepth`로 정확한 삭제 횟수 계산. ESC 키 전송 제거

### 7-4. JSON 한글 저장 형식 (해결)

- **원인**: `JsonSerializer.Serialize` 기본 설정이 비 ASCII 문자를 `\uXXXX` 이스케이프로 저장
- **해결**: `JsonSerializerOptions`에 `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` 적용

---

## 8. 변경된 파일 목록 (전체 이력)

### 세션 1 (초기 구현)

| 파일 | 변경 내용 |
|------|-----------|
| `Services/AutoCompleteService.cs` | `OnKeyInput(vk, imeKorean)` 시그니처 변경, `IsImeKorean` 속성 제거, `CompleteCurrentWord()` 추가, `AcceptSuggestion` 백스페이스 수정 |
| `Services/InputService.cs` | `IsImeKorean()` IMM32 API 메서드 추가 (GetGUIThreadInfo + ImmGetDefaultIMEWnd) |
| `Services/WordFrequencyStore.cs` | `RecordWord()`에서 즉시 `Save()` 호출, `JsonSerializerOptions` (한글 직렬화) |
| `Services/HangulComposer.cs` | `CompositionDepth` 속성 추가 |
| `ViewModels/KeyboardViewModel.cs` | IME 상태 폴링 타이머(`UpdateImeState`), `_lastImeKorean` 필드, `ImeModeChanged` 이벤트, `IsImeKorean()` 호출로 실시간 분기 |
| `ViewModels/SuggestionBarViewModel.cs` | ESC 키 전송 제거, 백스페이스만으로 교체, `AcceptSuggestion` 튜플 변경 반영 |
| `ViewModels/MainViewModel.cs` | `IsImeKorean` 초기화 제거 |
| `Platform/Win32.cs` | `GetGUIThreadInfo`, `GUITHREADINFO`, `ImmGetDefaultIMEWnd`, `AttachThreadInput`, `GetCurrentThreadId` P/Invoke 추가 |

### 세션 2 (Unicode 우회 방식 채택)

| 파일 | 변경 내용 |
|------|-----------|
| `Services/InputService.cs` | `InputMode` enum, `Mode` (관리자→VirtualKey, 일반→Unicode), `CheckElevated()`, `SendAtomicReplace()`, `TrackedOnScreenLength`, `HasActiveModifiers` 추가 |
| `Services/AutoCompleteService.cs` | `OnKeyInput(vk)` 시그니처 단순화 (`imeKorean` 파라미터 제거) |
| `ViewModels/KeyboardViewModel.cs` | `_isKoreanInput` 내부 토글, `HandleKoreanLayoutKey()` (Unicode/VirtualKey 분기), `HandleEnglishSubMode()` (SendUnicode로 영문 전송), `HandleEnglishLayoutKey()` (SendUnicode로 영문 전송), `ShouldSkipHandleAction()`, `FinalizeKoreanComposition()`, `UpdateImeState()` VirtualKey 모드로 제한 |
| `ViewModels/SuggestionBarViewModel.cs` | Unicode 모드에서 `SendAtomicReplace` 사용, VirtualKey 모드는 기존 BS+SendUnicode 유지 |