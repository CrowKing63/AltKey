# TASK-02 — `IsImeKorean()` 및 IMM32 P/Invoke 관련 dead code 정리

> **심각도**: 낮음 (동작에 영향 없음, 유지비만 증가)
> **선행 독해**: [CORE-LOGIC-PROTECTION.md](CORE-LOGIC-PROTECTION.md) 완독, 특히 §2의 "`InputService.Mode` 결정 로직"은 건드리지 않음.
> **예상 소요**: 20~30분 (정리 + 빌드 + 테스트)

---

## 1. 배경

`ime-korean-detection-problem.md §6.5`에서 Unicode 우회 방식을 채택한 뒤, "OS IME가 지금 한글 모드인지 감지"하는 기능은 자동완성에서 사용하지 않는다. 내부 Submode(`HangulJamo`/`QuietEnglish`)가 유일한 진실의 근원이 되었고, 관리자 모드에서조차 `IsImeKorean()` 호출 경로가 제거되었다.

현재 상태를 전역 검색(`grep IsImeKorean AltKey/**`)으로 확인한 결과:

- 선언: `AltKey/Services/InputService.cs:103` — `public bool IsImeKorean()` 구현체 1개.
- 호출자: **없음**. (`docs/` 폴더의 히스토리 문서에만 등장.)

따라서 메서드 자체와, 이 메서드만이 쓰던 Win32 P/Invoke 선언도 dead code 후보다.

---

## 2. 정리 대상

### 2.1 `InputService.IsImeKorean()`

```csharp
// AltKey/Services/InputService.cs:103~149
public bool IsImeKorean() { /* GetGUIThreadInfo + ImmGetDefaultIMEWnd + ImmGetContext ... */ }
```

→ **제거 가능**. 구현 전체 삭제.

### 2.2 `Platform/Win32.cs`의 연관 선언

다음 P/Invoke / 상수 / 구조체가 **오직** `IsImeKorean` 경로에서만 쓰였는지 확인 후 제거:

- `GetGUIThreadInfo`, `GUITHREADINFO` 구조체
- `ImmGetDefaultIMEWnd`
- `ImmGetContext`, `ImmReleaseContext`, `ImmGetConversionStatus`
- `IME_CMODE_NATIVE` 상수
- `AttachThreadInput`, `GetCurrentThreadId` (주석에 "IsImeKorean에서만 폴백으로 사용"이라고 표기됨)

**검증 방법**: 각 심볼에 대해 전역 `grep`. 사용처가 `InputService.cs:103~149`에만 있다면 제거 대상 확정. 한 군데라도 다른 호출자가 있으면 남긴다.

### 2.3 `WindowThreadProcessId`·`GetForegroundWindow`는 남길 가능성

`InputService.IsImeKorean` 내부에서 `Win32.GetForegroundWindow()`, `Win32.GetWindowThreadProcessId()`도 호출하는데, 이 두 함수는 `ProfileService`나 `WindowService`에서 포그라운드 앱 감지용으로도 쓴다. 그래서 **이 두 개는 남긴다**. grep으로 `GetForegroundWindow` 호출자를 확인하고, `IsImeKorean` 제거로 사용처가 0이 되는지를 먼저 본 다음 결정.

---

## 3. 수정 금지 영역 (이 작업 중에도)

- `InputService.Mode` 초기화 로직(`CheckElevated()` + Unicode/VirtualKey 배정) 유지.
- `TrySetMode`, `NotifyElevatedApp`, `ElevatedAppDetected` 이벤트 유지.
- `HangulComposer`, `KoreanInputModule`의 어떤 것도 변경 금지.
- `Platform/Win32.cs`의 키보드 이벤트 P/Invoke(`SendInput`, `INPUT`, `KEYBDINPUT`, `KEYEVENTF_*`) 유지.
- `VK_HANGUL` 상수 유지 (상단바 비상 버튼이 사용).

---

## 4. 작업 절차

1. `grep -n IsImeKorean AltKey/` 로 호출자 재확인 (결과 0건이어야 함).
2. `InputService.cs`에서 `IsImeKorean` 메서드와 관련 주석 삭제.
3. `Platform/Win32.cs`에서 §2.2 목록의 각 심볼에 대해 한 번 더 `grep`. 다른 호출자 없으면 삭제.
4. 빌드 녹색 확인.
5. `dotnet test` — `HangulComposerTests` + `KoreanInputModuleTests` + `InputServiceTests` 전체 녹색.
6. 수동 재확인: 일반 실행에서 자동완성 ON/OFF 토글, 자모 입력, 제안 수락, Submode 토글 정상 동작.

---

## 5. 커밋 메시지 초안

```
chore: drop unused IsImeKorean + IMM32 P/Invokes

- Unicode 우회 방식 채택 이후 IME 상태 조회는 쓰이지 않음.
- InputService.IsImeKorean(), GUITHREADINFO, ImmGetDefault/GetContext/
  ReleaseContext/GetConversionStatus, AttachThreadInput 등 dead code 삭제.
- SendInput 관련 Win32 선언 및 Mode 결정 로직은 유지.
```

---

## 6. 롤백 안전장치

삭제 후 빌드가 깨지거나 알 수 없는 호출자가 드러나면, 해당 심볼만 되살리고 제거 대상에서 제외한다. **모드 결정·키 전송 관련 코드는 어떤 경우에도 건드리지 말 것**.
