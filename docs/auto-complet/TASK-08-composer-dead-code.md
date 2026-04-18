# TASK-08 — `HangulComposer`의 dead private 메서드 2개 제거

> **심각도**: 낮음 (동작 영향 없음, 유지비만 증가)
> **선행 독해**: [CORE-LOGIC-PROTECTION.md](CORE-LOGIC-PROTECTION.md) §2 — "HangulComposer 내부 알고리즘 수정 금지"는 **조합 상태 머신**에 대한 보호 조항. 호출자가 전혀 없는 private 유틸리티는 이 조항의 대상이 아님.
> **예상 소요**: 10~15분 (삭제 + 빌드 + 테스트)

---

## 1. 대상

`AltKey/Services/HangulComposer.cs:320~332`

```csharp
private static bool IsHangulSyllable(char ch)
{
    return ch >= 0xAC00 && ch <= 0xD7A3;
}

private static (int cho, int jung, int jong) Decompose(char ch)
{
    int code = ch - 0xAC00;
    int jong = code % 28;
    int jung = (code / 28) % 21;
    int cho = code / (28 * 21);
    return (cho, jung, jong);
}
```

둘 다 `private static`이고, **`HangulComposer` 내부에서도 호출하지 않는다**.

검증:

```bash
# 프로젝트 전체
grep -n "IsHangulSyllable\|\\.Decompose\\(" AltKey/
```

→ 선언부 2줄만 출력되고 호출 0건. 다른 파일에서도 참조 없음(`HangulComposer` 내부에서도 정의만 있고 본문에서 호출 안 함).

---

## 2. 배경 추정

`HangulComposer`의 초기 설계에서 "외부에서 들어온 완성 음절을 분해해 composer 상태로 되돌린다" 같은 기능이 구상되었을 수 있다. 현재는:

- composer는 자모를 입력받는 쪽만 담당 (`Feed(string jamo)`).
- 외부에서 조합된 음절을 가져오지 않음.
- 조합 종료는 `Reset()`으로 일괄 버리고 새로 시작.

따라서 두 메서드는 **사용될 계획 없이 남겨진 유틸리티**다.

---

## 3. 삭제 후 영향

- 컴파일 에러 가능성: 없음 (private + unused).
- 반영 범위: `HangulComposer.cs` 1파일, 13줄 삭제.
- 테스트: 전량 녹색 유지 (영향 없음).
- 문서: `refactoring_ToDo/00-overview.md` 또는 `CORE-LOGIC-PROTECTION.md`에서 **composer 상태 머신 자체**의 수정 금지는 유지. 이 dead code 제거는 상태 머신을 건드리지 않는다.

---

## 4. 작업 절차

1. `grep` 재확인 — 출력이 선언 2줄뿐인지 확인. 누군가 최근에 호출 추가했을 가능성을 1초라도 허용하지 말 것.
2. 메서드 2개 블록 삭제 (라인 320~332).
3. 빌드: `dotnet build`. Green이어야 함.
4. 테스트: `dotnet test --filter HangulComposer` + `KoreanInputModule` 전부 녹색.
5. 수동 재현 시나리오 4종 통과 확인:
   - `해` + `ㅆ` → "했" (해T 회귀 #2 없음)
   - `ㄷㅏㄹㄱ` 순차 입력 → "닭"
   - `화사` 입력 후 BS 3번 → "" (BS 회귀 없음)
   - 엔터 입력 후 줄바꿈 정상 (#1 회귀 없음)

---

## 5. 수정 금지 영역 (이 작업 중에도)

- `HangulComposer`의 다음 public API 절대 불변:
  - `Feed(string jamo)`
  - `Backspace()`
  - `Reset()`
  - `Current`, `HasComposition`, `CompletedLength`, `CompositionDepth`, `TotalBackspaceCount`
- private 필드(`_completed`, `_choseongIdx`, `_jungseongIdx`, `_jongseongIdx` 등) 시그니처·의미 불변.
- `Choseong`, `Jungseong`, `Jongseong`, `CompoundJongseongMap`, `CompoundJungseong`, `JongToCho`, `JongseongDecomposition` 상수 테이블 전부 불변.
- 조합 상태 전이 로직 **어떤 줄도** 건드리지 말 것. "조합 로직은 전설의 무덤"(CORE-LOGIC-PROTECTION §2.2).

---

## 6. 커밋 메시지 초안

```
chore(composer): drop unused private utilities

- HangulComposer.IsHangulSyllable(char) 및 Decompose(char) 는 호출자 없음.
- 과거 설계 잔재로 남아있었음. 조합 상태 머신과는 무관하여 안전 삭제.
- 빌드·테스트 변화 없음.
```

---

## 7. 롤백 안전장치

만약 삭제 후 테스트가 예기치 않게 실패하면(이론상 불가능하지만 Reflection을 통한 호출 등 극단 케이스) 커밋을 `git revert`하고 원인 조사. **절대로 다른 로직을 수정해서 문제를 우회하지 말 것**.

---

## 8. 함께 점검 가능 (선택)

이 PR에서 함께 끼워넣기 좋은 유사 청소 항목:

- `HangulComposer.cs`에 주석 중 "TODO"·"HACK"·"XXX" 태그가 있다면 수거해 별도 이슈화. (코드 변경은 아님.)
- `using` 지시문 정리 (`dotnet format`).

단, **그 외의 리팩토링은 하지 말 것**. 스코프 유혹 방지.
