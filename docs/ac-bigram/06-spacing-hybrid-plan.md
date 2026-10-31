# 띄어쓰기 하이브리드 계획 (조사 붙여쓰기 자동 판단)

> **상태**: 계획만 작성됨 — 아직 구현하지 않음. 이 문서를 읽고 다음 세션에서 구현·심화한다.
>
> **관련 문서**: [00-overview](00-overview.md), [01-bigram-store](01-bigram-store.md), [03-module-wire](03-module-wire.md), [CORE-LOGIC-PROTECTION](../auto-complet/CORE-LOGIC-PROTECTION.md)

---

## 1. 배경

현재 바이그램 제안 수락(`AutoCompleteService.AcceptBigramSuggestion`)은 반환값에 **항시 선행 공백**을 붙인다:

```csharp
// AltKey/Services/AutoCompleteService.cs — AcceptBigramSuggestion
return " " + suggestion;
```

공백 키 이후 bigram 문맥이 유지되는 상황에서 다음 단어 제안을 수락하면, 제안어가 공백과 함께 삽입된다. 한국어에서는 조사(이·가·은·는·을·로·도·만·에·해 등)가 앞 명사에 **붙어서** 쓰여야 하는데("학생이", "집에"), 현재는 무조건 "학생 이"처럼 띄어지게 된다.

## 2. 목표 / 비목표

### 목표
- bigram 제안 수락 시, (prev, next) 쌍의 **붙여쓰기 이력**에 따라 공백 삽입 여부를 결정한다.
- 사용자의 실제 입력 패턴에서 학습 신호를 수집해 정적 규칙을 교정한다.
- 기존 `user-bigrams.{lang}.json` 스키마는 **변경하지 않는다** (마이그레이션 리스크 제로).

### 비목표
- 품사 분석(명사/동사 구분) 구현 — 품사 사전 없이 조사/어미 충돌을 완전 분리하는 것은 불가능하므로, 학습으로 완화한다.
- 영어 서브모드 적용 — 붙여쓰기는 한국어 전용이다.
- 일반 제안 모드(`AcceptSuggestion`)의 변경 — 조합 중 텍스트를 교체하는 경로라 공백 문제가 없다.

## 3. 설계

### 3.1 정적 조사 목록 (시드)

`AltKey/Services/SpacingRules.cs`(신규)에 정적 조사 목록을 둔다:

```
이, 가, 은, 는, 을, 로, 도, 만, 에, 해, 등, 부터, 까지, 만큼, 인, 한, 이(중복 제외)
```

- **초기 동작**: 학습 데이터가 없는 쌍은 **공백 삽입 유지**(현재 행동). 정적 목록만으로는 오탐 위험("감사해"는 동사)이 있어서, 목록은 *학습 우선순위 힌트*로만 쓰고 단독 결정에는 쓰지 않는다.
- **어미 충돌 주의**: 해·에·로·도·만은 동사 어미와 겹친다. 이 조사들은 학습 임계값을 더 높게(예: attached ≥ 5) 잡는다.

### 3.2 학습 스토어: `BigramAttachStore` (신규 클래스)

**파일**: `user-bigram-attach.{lang}.json` — 기존 bigram 파일과 **분리**.

```json
{
  "학생": { "이": { "a": 4, "s": 1 } },
  "집":   { "에": { "a": 2, "s": 0 } }
}
```

- `a` = attached(붙여쓰기) 증거 수, `s` = separated(띄어쓰기) 증거 수.
- **규율**: `BigramFrequencyStore`와 동일 — 1초 디바운스 + 원자적 쓰기(tmp+Move) + `UnsafeRelaxedJsonEscaping` + `LastSaveError` 노출 + 앱 종료 `Flush()`.
- 프루닝: pair당 카운터만 유지하므로 별도 프루닝 불필요. (prev, next) key는 bigram 스토어와 같은 소문자/trim 정규화 적용.

### 3.3 증거 수집 포인트

| 시점 | 코드 위치 | 신호 |
|---|---|---|
| 단어 commit (`FinalizeComposition` / `CommitCurrentWord`) | `KoreanInputModule` | W가 조사 P로 끝나고 `W[..^P.Length] == lastCommittedWord`이면 → **(prev, P) attached +1** |
| 단어 commit (공백 이후 독립 토큰) | 위 동일 | W가 정적 조사 목록에 있고 `W == lastCommittedWord 직후 토큰`이면 → **(prev, W) separated +1** |
| bigram 제안 수락 (`AcceptBigramSuggestion`) | `AutoCompleteService` | **최강 신호**: 공백 없이 삽입하면 attached +1, 공백과 함께면 separated +1 (사용자의 명시적 선택) |

commit 시 매칭은 규칙 보조(rule-assisted)다: "학생이"라는 토큰에서 "학생"+"이"를 분리하는 것은 정적 목록 없이는 불가능하므로,列表가 학습의 *트리거* 역할을 한다.

### 3.4 수락 시 결정 규칙

`AcceptBigramSuggestion` 반환값 계산:

```
if (attached >= minSamples && attached > separated)
    return suggestion;          // 붙여쓰기 — 공백 없음
else
    return " " + suggestion;    // 띄어쓰기 — 현재 행동
```

- `minSamples`: 일반 조사 3, 어미 충돌 조사(해·에·로·도·만) 5.
- 학습 데이터가 없으면 → 현재 행동(공백) 유지. **보수적 기본값**.

### 3.5 입력 경로 호환

`SuggestionBarViewModel.AcceptSuggestion`(bigram 분기)은 `SendUnicode(insertion)`으로 커서 위치(이전 단어 뒤)에 삽입하므로, 공백 없는 반환값도 그대로 동작한다. `ResetTrackedLength()` 호출도 영향 없음.

## 4. 엣지 케이스·리스크

| 리스크 | 완화 |
|---|---|
| "감사해" 같은 동사 어미 오탐 → "감사 해"가 붙여져 "감사해"로 저장 | 어미 충돌 조사는 높은 임계값 + separated 증거만으로도 공백 복귀 (attached > separated 조건) |
| 고유명사+조사 ("박서준이") | prev가 학습 단어인지 여부와 무관하게 매칭 — 명사성 판정 없음. 오탐 시 separated 신호로 자纠正 |
| 기존 bigram 파일에 자모 혼입 데이터 (과거 OnSeparator 우회 버그 유래) | attach 스토어는 신규 파일이라 영향 없음. bigram 스토어 청소는 별도 작업 |
| JSON 파싱 실패 (손상 파일) | `BigramFrequencyStore.Load()` 패턴 재사용 — catch 시 빈 dict |

## 5. 테스트 계획

1. **Unit — `BigramAttachStore`**: 기록/로드/디바운스/원자적 쓰기 (`AltKey.Tests/Services/`)
2. **Unit — 매칭 로직**: "학생이" commit → (학생, 이) attached +1; "학생"+"이" 분리 입력 → separated +1
3. **Unit — 결정 규칙**: 임계값 하/상, 어미 충돌 조사의 높은 임계값
4. **Integration — `BigramIntegrationTests` 확장**:
   - "학생" commit → "이" 독립 토큰 commit → separated 증거 확인
   - "학생이" 단일 토큰 commit ×3 → attached 증거 확인
   - `AcceptBigramSuggestion("이")` → attached 임계값 도달 시 반환값에 공백 없음 assert

## 6. 미결 사항 (다음 세션에서 결정)

- [ ] 정적 조사 목록의 최종 구성 — 빈도 코퍼스 기반 선별 vs 수동 큐레이션
- [ ] `AltKey.Tools` 편집기에 attach 데이터 뷰/편집 노출 여부
- [ ] 사용자 개별 override UI (특정 pair 강제 붙여쓰기/띄어쓰기)
- [ ] 오래된 증거 감쇠(decay) — 시간 가중 vs 단순 카운트
- [ ] minSamples 값의 실사용 튜닝

## 7. 관련 파일 목록

| 파일 | 역할 |
|---|---|
| `AltKey/Services/AutoCompleteService.cs` | `AcceptBigramSuggestion` 반환값 분기, 증거 기록 |
| `AltKey/Services/InputLanguage/KoreanInputModule.cs` | commit 시 매칭·증거 수집 |
| `AltKey/Services/BigramAttachStore.cs` (신규) | attach 카운터 저장소 |
| `AltKey/Services/SpacingRules.cs` (신규) | 정적 조사 목록 + 임계값 |
| `AltKey.Tests/Services/BigramAttachStoreTests.cs` (신규) | 스토어 단위 테스트 |
| `AltKey.Tests/InputLanguage/BigramIntegrationTests.cs` | 통합 시나리오 확장 |
