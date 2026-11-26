# AI 텍스트 캡처 경로 전환 — UIA 우선 + 클립보드 폴백 (설계안)

> **프로젝트**: AltKey — AI 버튼(번역) 선택 텍스트 캡처
> **작성일**: 2026-11-26
> **상태**: Phase 1 구현 완료 (2026-11-26) — 수동 검증 대기 (샌드박스 번들 전달)
> **대상 코드**: `AltKey/ViewModels/MainViewModel.cs` (`ExecuteAiCoreAsync`), `AltKey/Services/ClipboardHelper.cs`

---

## 1. 배경 (현재 동작)

| 단계 | 현재 동작 (`MainViewModel.ExecuteAiCoreAsync`) | 문제 |
|---|---|---|
| 클립보드 백업 | ① `ClipboardHelper.GetTextWithRetry()` — **평문만** 백업 | 이미지/HTML 포맷은 사실상 손실됨. 클립보드 폴백 이후 복원 시 재사용 |
| 포커스 복원 | `TryFocusLastExternalTarget()` (AltKey 포커스를 대상 앱으로) | 포커스 왕복은 AltKey UI 경로이므로 불가피 |
| 포커스 대기 | `WaitForFocusInTargetAsync(targetPid)` — UIA로 포커스 요소의 프로세스만 확인 | 포커스 확인은 있으나, **텍스트 자체는 UIA로 읽지 않는다** |
| 캡처 | `SendCombo(Ctrl+C)` → 220ms 대기 → `GetTextWithRetry()` (최대 3회) | ① 브라우저·Electron에서 합성 Ctrl+C가 무시되어 캡처 실패 ② 성공해도 클립보드는 반드시 오염됨 → 마지막에 복원 부담 |
| 결과 전달 | 클립보드에 결과 기록 → 50ms 후 `Ctrl+V` → 사운드 → 100ms 후 원본 복원(텍스트만, finally, 200ms 대기) | 원본 복원은 평문만 — 이미지/HTML 등은 복원 불가. Chromium에서 합성 Ctrl+V도 무시되는 앱이 있음 |

**현행 설계 전체 흐름**

```
[AI 버튼] → 포커스 복원 → UIA 포커스 확인 → Ctrl+C → 220ms → 클립보드 읽기 ×3 → AI 처리 → 클립보드에 결과 → Ctrl+V → 사운드 → 원본 복원
```

**핵심 결함**: 텍스트 읽기에 UIA를 쓰지 않는 것. UIA TextPattern은 포커스 앱이 TextPattern을 구현하는 한 합성키 없이 선택 텍스트를 안정적으로 읽는 유일한 방법이고, 2026-11-25 조사(`docs/research-text-detection.md`)에서 3단계 폴백(UIA → MSAA → 클립보드)의 표준 구조로 확인되었습니다.

---

## 2. 목표

1. AI 캡처를 **UIA TextPattern 우선 + 클립보드 폴백** 계층형으로 전환한다.
2. 클립보드는 이제 **최후의 폴백**으로만 쓰인다 (성공 경로에 클립보드 불침투).
3. 결과 전달(Ctrl+V + 복원)은 현재 동작을 유지하되, 원본 복원은 전체 포맷으로 확장하는 옵션을 오픈한다.
4. UIA 쿼리는 UI 스레드(Dispatcher)에서 하지 않아 대상 앱(Chromium 포함)을 동결시키지 않는다.

---

## 3. 설계

### 3.1 캡처 경로 상태 전이

```
Click [AI 버튼]
   ↓
[1] IsAiProcessing = true, _aiCts = new CancellationTokenSource
   ↓
[2] 포커스 복원 (TryFocusLastExternalTarget) — 기존 유지
   ↓
[3] UIA 포커스 확인 WaitForFocusInTargetAsync — 기존 유지, 단 실패 시 사용자 안내
   ↓
[4] CaptureSelectedTextViaUIA (새 경로)
     ├── 성공 (텍스트 반환) → [8] AI 처리
     └── 실패/빈 결과 → [5]
[5] 클립보드 폴백
     ├── 백업 (IDataObject 전체) → 클리어 → Ctrl+C → 시퀀스 증가 폴링 → 읽기
     │   ├── 성공 → [8] AI 처리 (폴백 성공 여부 로그)
     │   └── 실패 → 안내 메시지, finally에서 원본 복원
   ↓
[8] _aiService.ProcessTextAsync(selectedText, prompt, ct) — 기존 유지
   ↓
[9] 클립보드에 결과 기록 → 50ms 대기 → Ctrl+V → 사운드 (기존 유지)
   ↓
[10] finally: 원본 클립보드 복원 (옵션) + IsAiProcessing = false
```

### 3.2 UIA 경로 상세 (새 서비스 `AiTextCaptureService`)

**새 클래스: `AltKey/Services/AiTextCaptureService.cs`** — 존재하는 UIA 사용(`MainViewModel`의 포커스 확인)을 이 서비스로 통합.

```csharp
public sealed class AiTextCaptureService
{
    /// <summary>
    /// 포커스 앱(targetPid)에서 현재 선택 텍스트를 읽는다.
    /// UIA TextPattern 우선, 실패 시 null 반환 (클립보드 폴백은 호출자 책임).
    /// </summary>
    public string? CaptureSelectedText(uint targetPid);
}
```

**동작 순서**:
1. `AutomationElement.FocusedElement` — 포커스 요소 확보. 없으면 foreground window에서 `FromWindow`.
2. 포커스 요소부터 ancestor 방향으로 최대 **10단계** 탐색 (selection-hook이 Chromium 기준 검증으로 채택한 상한, Q2 결정).
3. 각 요소에서 `TryGetCurrentPattern(TextPattern.Pattern)` → `SupportedTextSelection != None` 확인 → `GetSelection()` ranges 전부 순회하며 `GetText(-1)` 연결.
4. 비어 있으면 상위 요소로 계속 탐색. 전부 비어 있으면 **null** 반환.
5. **절대 하지 말 것**: `DocumentRange` 전체 읽기/`ExpandToEnclosingUnit(Document)` 폴백 — Chromium에서 렌더러 동결 보고됨 ([docs/research-text-detection.md §2.6](docs/research-text-detection.md)).
6. 쿼리는 **Dispatcher 워커(Task.Run)에서** 실행 — UIA COM 호출은 블로킹이고, Dispatcher에서 호출하면 Chromium/Edge 렌더러가 멈출 수 있다는 selection-hook 릴리스 노트 참조.

**UIA 없음/예외 처리**:
- `FocusedElement` null 또는 예외 → 즉시 null 반환 (클립보드 폴백으로 위임)
- `TextPattern` 미지원 요소 → 상위 탐색으로 넘김
- 전체 탐색 실패 → null 반환 (호출자에서 폴백)

**호출 주체 변경**: `MainViewModel.ExecuteAiCoreAsync`에서
- 기존: `WaitForFocusInTargetAsync` (포커스 확인) → 같은 메서드에 `CaptureSelectedTextViaUIA` 호출 추가 → 성공 시 폴백 스킵
- 실패 시 기존 클립보드 폴백(현재 코드의 ×3 재시도 루프) 형식 유지

### 3.3 클립보드 폴백 상세 (기존 코드의 변형)

- **백업**: 현재 `GetTextWithRetry()` 평문 백업 → **IDataObject 전체 백업**(Orbital/Selection-hook 패턴). 단, WPF 클립보드 API는 전체 포맷 복원을 직접 지원하지 않으므로 현행 평문 복원을 유지하는 것이 안전하다고 판단 → **Q1 확정(평문 복원 유지, 풀 백업은 phase 2)**.
- **캡처**: 기존 `SendCombo(Ctrl+C)` → 220ms → `GetTextWithRetry()` 유지.
- **시퀀스 번호 감지**: Forge식 `GetSequenceNumberWithRetry()` 증가 감지 후 읽기(`ClipboardHelper`에 이미 존재)로 "같은 텍스트 재복사" 검출 오차를 줄인다.
- **재시도**: 최대 3회 유지. 3회 실패 시 안내 메시지.

### 3.4 결과 전달 (현행 유지, 단 복원 옵션)

- 현재: 클립보드 기록 → Ctrl+V → 사운드 → 복원(평문)
- 제안: **결과 전달은 현행 유지**. 복원 포맷은 **Q1 확정(평문 복원 유지)**.
- Chromium에서 합성 Ctrl+V가 무시되는 앱이 있다는 사실(조사됨)은 수동 검증 체크리스트에 명시.

### 3.5 스레딩

| 작업 | 스레드 |
|---|---|
| `CaptureSelectedText` (UIA 쿼리) | 워커 (`Task.Run`) |
| `ClipboardHelper` (WpfClipboard) | Dispatcher (소유자 스레드 제약) |
| `SendCombo` (SendInput) | 기존 디스패처 유지 + tagged builder |
| `ProcessTextAsync` | 기존 유지 |
| finally 복원 | Dispatcher (기존 유지) |

---

## 4. 제약 (compliance)

이 변경은 자동완성·한글 조합과 무관하므로 auto-complet §2 freeze 룰은 해당하지 않습니다. 대신 아래 시스템 제약을 지킨다.

| 규칙 | 이 설계의 처리 |
|---|---|
| 입력 주입은 tagged builder (`INPUT_EXTRAINFO_ALTKEY`) | 캡처/전달 시 `SendInput`은 기존 `SendCombo`/`InputService`를 통과 — 새 SendInput 직접 호출 금지 |
| 전역 WH_KEYBOARD_LL 훅은 tagged 이벤트를 필터 | 이미 `InputService`에서 처리됨 — 변경 없음 |
| 클립보드 복원 시 평문만 복원하면 이미지/HTML 손실 | Q1 확정으로 평문 복원 유지 (풀 백업은 phase 2) |
| 성능: Chromium 렌더러 동결 | UIA 쿼리는 Dispatcher 워커에서만 실행 (3.3 참조). DocumentRange 전체 읽기 절대 금지 |

---

## 5. 수정 파일 (예상)

| 파일 | 변경 내용 |
|---|---|
| **`AltKey/Services/AiTextCaptureService.cs`** (신규) | UIA TextPattern 캡처 로직 (3.2). `AutomationElement` 접근은 인터페이스 주입으로 분리(테스트 용이성) |
| **`AltKey/ViewModels/MainViewModel.cs`** | `ExecuteAiCoreAsync` 내 캡처 블록을 서비스 호출로 교체. UIA 실패 시 기존 클립보드 폴백 유지. UIA가 성공하면 클립보드 불침투 |
| **`AltKey/Services/ClipboardHelper.cs`** | 변경 없음 (시퀀스 번호 조회는 이미 존재) |
| **`AltKey.Tests/AiLocal/AiTextCaptureServiceTests.cs`** (신규) | iTextPatternWalker 가짜 체인으로 탐색 로직 검증 — 첫 레벨 성공, 빈/공백 레벨 스킵, 깊이 한도, 포커스 요소 없음(null 반환) |

(통합 시나리오 — UIA 성공 경로에서 클립보드 불침투 — 은 MainViewModel의 전체 흐름을 가려야 하므로 phase 2에 별건으로 분리. 단위 테스트가 아니라 수동 검증 체크리스트(§7)에서 확인한다.)

변경 없는 파일: `ClipboardHelper.cs` (기존 유지), `ProfileService.cs` (기존 유지), `ClipboardPanelWindow` (독립), `AccessibilityNavigationService` (WH_KEYBOARD_LL 경로 유지)

---

## 6. 테스트 계획

1. **UIA 성공 경로**: mock FocusedElement(TextPattern, selection ranges) → 텍스트 반환. 클립보드 불침투 확인 (ClipboardHelper 호출 없음)
2. **ancestor 탐색**: focused 요소 실패 → ancestor chain(8단계 이내)에서 TextPattern 발견 → 텍스트 반환
3. **UIA 완전 실패**: FocusedElement null → null 반환 → 호출자가 클립보드 폴백으로 전환
4. **빈 선택**: TextPattern 있으나 GetSelection() 빈 ranges → null 반환 (상위 탐색)
5. **클립보드 폴백**: UIA 실패 → SendCombo(Ctrl+C) → 시퀀스 증가 폴링 → 읽기 → 텍스트 반환
6. **재시도 상한**: 3회 다 실패 → 안내 메시지 발화 (기존와 동일)
7. **Cancel**: user 취소 시 합성 Ctrl+V/복원 안 함 (기존와 동일)

---

## 7. 수동 검증 체크리스트

| # | 시나리오 | 기대 동작 |
|---|---|---|
| 1 | Notepad에서 텍스트 선택 → AI 버튼 | UIA로 캡처, 클립보드 변경 없음, 번역 후 붙여넣기 |
| 2 | Chrome/Edge에서 텍스트 선택 → AI 버튼 | UIA ancestor 탐색으로 캡처 (tabindex 컨테이너 대응). **Chromium 동결 없음** |
| 3 | Chrome에서 선택 미완성(caret만) → AI 버튼 | 안내 메시지 후 종료. UIA/클립보드 모두 무성 알림 없이 조용히 실패하지 않음 |
| 4 | Word/문서 앱에서 선택 → AI 버튼 | UIA 성공 (TextPattern 구현 앱) |
| 5 | Sidebar > 설정에서 번역 실행 → 붙여넣기 후 원본 클립보드 복원 | 원본 클립보드 상태 확인 (텍스트 복원 여부는 Q1 확정(평문 복원) 반영) |
| 6 | AI 취소 버튼 → 처리 중 취소 | 취소 메시지 발화, 클립보드 복원 안 함 |

수동 검증은 Windows Sandbox 번들(AGENTS.md §4)로 진행.

---

## 8. 가정·오픈 질문

**가정**
- 현재 `ExecuteAiCoreAsync`는 Dispatcher 위에서 전체 실행 — 이 시점에 `Task.Run`으로 UIA 경로만 분리한다.
- 기존 `WaitForFocusInTargetAsync`는 포커스 확인 전용으로 남기고, 캡처는 별도 경로로 분리.
- 클립보드 폴백은 기존 타이밍(220ms + 3회 + 3×10/20/40ms)을 유지.

**오픈 질문 — 모두 확정 (2026-11-26)**
- **Q1**: 평문 복원 유지. full IDataObject 백업은 phase 2 별건으로 분리.
- **Q2**: ancestor 탐색 depth **10단계**. 성공 조건: 각 레벨에서 `SupportedTextSelection != None` + 비어있지 않은 selection 텍스트.
- **Q3**: Phase 1은 현행(클립보드 + Ctrl+V) 유지. Forge식 UIA 붙여넣기는 phase 2 별건으로 분리 (수동 검증 결과를 보고 도입 결정).

**이 문서가 바꾸지 않을 것**
- AI 버튼 배치/노출 정책 (settings 에서 켠 경우에만 표시)
- `AiActionPrompts` 사용자 입력 흐름
- 원본 클립보드 복원 메커니즘 (phase 1 유지)
