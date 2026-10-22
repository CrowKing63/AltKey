# 영어 입력 수정(1.1.7) 재도입 + 한글 쌍받침 회귀 방지 계획

- 상태: 계획 (미구현)
- 관련 커밋: `3d5f39c` (1.1.7 원본), `7286618` (롤백), v1.1.10 릴리스
- 목표 버전: 1.1.11

## 1. 문제 정의

| ID | 증상 | 발생 조건 |
|----|------|-----------|
| Bug A | Shift+숫자가 특수문자 대신 숫자를 입력 (예: `1%`을 치면 `15`) | 영어 모드, 자동완성 ON |
| Bug B | 쌍받침 입력 시 한글 조합이 풀림 (`가` + Shift + ㅅ → `가ㅆ`, 기대 `갔`) | 한글 모드, 자동완성 ON (ON/OFF 무관하게 문자 깨짐) |

Bug B는 Bug A를 고치던 1.1.7 패치의 부수 피해다.

## 2. 원인 분석 (조사 완료, 실측 재현 확인)

### Bug A — 1.1.7 이전 코드

`KoreanInputModule.GetEnglishCharFromSlot`:

```csharp
string? label = showUpperCase && slot.EnglishShiftLabel is { Length: > 0 }
    ? slot.EnglishShiftLabel
    : slot.EnglishLabel ?? slot.Label;   // ← Shift 중인데 EnglishShiftLabel이 없으면 원본 라벨("5")로 떨어짐
```

"5" 키는 `EnglishShiftLabel`이 없고 `%`가 `ShiftLabel`에 있다 → Shift+5가 `'5'`를 전송한다.

또한 `HandleQuietEnglish`에서 숫자·기호가 `_englishPrefix`에 추적되어 제안 바를 오염시켰다.

### Bug B — 1.1.7 패치

`KoreanInputModule.HandleKey` (HangulJamo 경로):

```csharp
if (jamo is null)
{
    FinalizeComposition();   // ← 모든 자모-비 키에 무조건 적용
    return false;
}
```

레이아웃(Basic.json)에서 **Shift/Ctrl/Win/Alt 버튼은 `ToggleSticky` 액션이고 자모가 없다** → Shift를 탭하는 순간:

1. 진행 중 음절("가")이 완성 단어로 커밋됨 (자동완성 ON이면 `RecordWord`+`RecordBigram` → 반쪽단어 학습 오염)
2. `_composer.Reset()` — "가가 조합 중"이라는 상태 소실
3. `ResetTrackedLength()` — 화면 길이 추적 0

이후 Shift+ㅅ(→ㅆ)이 오면 리셋된 컴포저는 ㅆ을 **새 음절의 초성**으로만 해석 → Current="ㅆ"(맨 자모), 추적 길이 0이라 백스페이스 없이 `SendAtomicReplace(0, "ㅆ")` → "가" 뒤에 "ㅆ"이 붙음.

실측 재현 (동일 시나리오, 1.1.7 vs 이전):

| 버전 | 화면 | 전송 내역 |
|------|------|-----------|
| 1.1.7 이전 | `AC14` = 갔 ✓ | prevLen=1, "갔" |
| 1.1.7 | `AC00 3146` = 가ㅆ ✗ | prevLen=0, "ㅆ" |

### 왜 1.1.7이 FinalizeComposition을 넣었는가

한글 모드에서 자모-비 입력 키(숫자·기호)는 모듈이 nothing 하고 ViewModel이 `SendUnicode`(트랙킹 안 함)로만 전송한다 → 화면은 "간5"인데 추적 길이는 1, 컴포저에는 "간"이 그대로 남음 → 다음 자모 키의 `SendAtomicReplace`가 잘못된 기준 길이로 백스페이스를 계산해 문자를 깨뜨린다. 즉 **입력을 실제로 생성하는 자모-비 키에 대한 확정은 필요했고**, 문제는 Shift 버튼까지 함께 잡은 것이다.

## 3. 수정 설계

### 원칙: "실제로 화면에 텍스트를 보내는 키일 때만 조합을 확정한다"

레이아웃 액션 타입이干净的한 판별자다:

| 액션 | 예 | 텍스트 생성? |
|------|-----|--------------|
| `SendKey` | ㄱ, ㅅ, 1, 5, %, - | O (유니코드 모드에서 문자 전송) |
| `ToggleSticky` | Shift, Ctrl, Win, Alt | X |
| `ToggleKoreanSubmode` | 가/A 버튼 | X (ToggleButton 자체에서 FinalizeComposition 호출) |

### 변경 사항 (3건)

#### ① `GetEnglishCharFromSlot` — 1.1.7 원본 그대로 재적용

```csharp
string? label = showUpperCase
    ? (slot.EnglishShiftLabel is { Length: > 0 } ? slot.EnglishShiftLabel : slot.ShiftLabel)
    : (slot.EnglishLabel is { Length: > 0 } ? slot.EnglishLabel : slot.Label);
```

영문 서브모드에서만 호출되므로 한글 모드에 영향 없음. Shift+5 → `%` 복구.

#### ② `HandleQuietEnglish` — 1.1.7 원본 그대로 재적용

```csharp
if (ch != '\0')
{
    if (char.IsLetter(ch))
        TrackEnglishKey(ch);
    else
        FinalizeComposition();
    _input.SendUnicode(ch.ToString());
    return true;
}
```

영문 서브모드 전용. 숫자·기호는 단어 추적으로부터 제외되고, 이전 단어를 확정한다.

#### ③ `HandleKey` (HangulJamo) — 1.1.7을 **범위 한정**으로 재적용

```csharp
if (jamo is null)
{
    // 실제로 화면에 텍스트를 보내는 키(숫자·기호 등 SendKey)에만 조합을 확정한다.
    // ToggleSticky(Shift/Ctrl/Win/Alt), ToggleKoreanSubmode 등은 텍스트를 만들지 않으므로
    // 조합 상태를 건드리지 않는다. (쌍받침 회귀 방지)
    if (slot.Action is SendKeyAction && ctx.InputMode == InputMode.Unicode)
        FinalizeComposition();
    return false;
}
```

- `SendKeyAction`: 숫자·기호 → 확정 유지 (트랙킹 길이 일관성, Bug A의 한글 모드 대응)
- `ToggleSticky`: Shift 버튼 → 아무것도 안 함 (Bug B 해결)
- `InputMode.Unicode` 한정: VirtualKey 모드에서는 모듈이 텍스트를 보내지 않으므로(SendAtomicReplace 미사용) 트랙킹 일관성이 이슈가 되지 않는다. 1.1.7보다 좁아지는 지점이지만 해당 모드에서 확정은 불필요하다.

### 경계 사례 (허용)

- **CapsLock**(SendKey, 자모 없음): 조합 중 탭 시 확정된다. 1.1.7 이전(안 함)과 다른 지점이나, 입력 컨텍스트 전환이므로 확정하는 쪽이 상태 일관성에 유리하다. 동작 변경 허용.
- **가/A 버튼**: `ToggleSubmode()` 내부에서 이미 `FinalizeComposition()`을 호출하므로 별도 처리 불필요. ③의 조건에서도 제외됨(SendKey 아님).

### 동작 매트릭스

| 시나리오 | 1.1.7 이전 | 1.1.7 | 수정 후 |
|----------|-----------|-------|---------|
| 영문: `1` + Shift+5 | `15` ✗ | `1%` ✓ | `1%` ✓ |
| 영문: `abc` + `1` | prefix "abc1" ✗ | "abc" 확정, "1" 전송 ✓ | 동일 ✓ |
| 한글: `가` + Shift 탭 + ㅅ(→ㅆ) | `갔` ✓ | `가ㅆ` ✗ | `갔` ✓ |
| 한글: `간` + `5` | 트랙킹 불일치 ✗ | 확정 ✓ | 확정 ✓ |
| 자동완성 ON, 조합 중 Shift 탭 | 학습 없음 ✓ | 반쪽단어 "가" 학습 ✗ | 학습 없음 ✓ |

## 4. 테스트 계획 (TDD)

### 신규 테스트

1. **`HangulMode_ShiftButton_Tap_DoesNotFinalizeComposition`** (Bug B 회귀 방지)
   - ㄱ+ㅏ 입력 → Shift(ToggleSticky) 탭 → `_composer.HasComposition` 유지, 추적 길이 불변 assert
   - Shift+ㅅ(→ㅆ) → `SendAtomicReplace(prevLen=1, "갔")` assert
2. **`HangulMode_ShiftTap_DoesNotRecordWord_WhenAutoCompleteOn`** (사전 오염 방지)
   - 자동완성 ON, 조합 중("가") Shift 탭 → `RecordWord` 호출 없음 assert
3. **`QuietEnglish_ShiftDigit_SendsShiftLabel`** (Bug A)
   - 영문 모드: "1" 전송 후 Shift+5 → `SendUnicode("%")`, `_englishPrefix`에 숫자 없음 assert

### 유지되는 기존 테스트 (1.1.7에서 수정된 2개 — 새 동작과 양립 가능)

- `KoreanInputModuleHangulTests.Feed_number_one_without_shift_finalizes_composition`
  (숫자 키는 여전히 확정 → ③의 SendKeyAction 조건으로 통과)
- `KoreanInputModuleQuietEnglishTests.QuietEnglish_number_and_symbol_keys_are_sent_but_not_tracked`
  (영문 모드 숫자·기호 미추적 → ②로 유지)

### 검증

- 전체 테스트 스위트 실행 (241개 기준, 신규 포함)
- 시나리오 실측: scratch worktree 비교 방식은 이번엔 불필요 (regression 테스트가 코드 내에서 커버)

## 5. 릴리스

1. `AltKey.csproj` + `installer/AltKey.iss` 버전 1.1.10 → 1.1.11
2. 커밋 → `v1.1.11` 태그 push → GitHub Release 워크플로 자동 트리거 (v1.1.10과 동일 절차)
3. 릴리스 노트: "영문 모드 Shift+숫자 특수문자 입력 복구, 한글 쌍받침 회귀 방지"

## 6. 리스크

- **낮음**: ①②는 1.1.7에서 이미 실사용 검증된 코드 그대로 재적용. ③만 조건이 달라지며, 그 차이는 "ToggleSticky/ToggleKoreanSubmode 키가 FinalizeComposition을 호출하지 않는다" 한 가지다.
- 회귀 테스트 2건이 이 차이를 직접 커버한다.
