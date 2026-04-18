# TASK-04 — 초성만 쳤을 때 제안이 거의 안 뜨는 UX 개선

> **심각도**: 중간 (품질/체감 문제)
> **선행 독해**: [CORE-LOGIC-PROTECTION.md](CORE-LOGIC-PROTECTION.md) 완독. 특히 §2 "HangulComposer 내부 알고리즘 수정 금지"는 이번 작업에도 유효.
> **예상 소요**: 2~3시간 (실험·튜닝 포함)
> **상태**: **실험적 — 품질 개선안이므로 반드시 프로토타입 후 사용자 검증 필요**

---

## 1. 문제

- `HangulComposer.Current`는 조합 상태를 문자열로 그대로 돌려준다.
- 초성만 입력된 상태(예: `ㄱ`, `ㅁ`, `ㅎ`)는 **호환 자모 영역(U+3131~U+3163)**의 문자 1개다.
- 내장 사전(`ko-words.txt`)과 사용자 사전에 저장된 단어는 **완성 음절 영역(U+AC00~U+D7A3)**의 문자로 시작한다.
- `"가족".StartsWith("ㄱ")` 은 `false`다. 따라서 초성만 쳤을 때 prefix 매칭이 거의 실패해서 제안이 비어 있다.

사용자 관점: "한 글자만 쳤는데 자동완성이 안 뜨네" → 자동완성 기능의 첫인상이 나쁨.

---

## 2. 설계안 3가지

### 안 A — 초성 분해 매칭 (권장 프로토타입)

사전 단어의 첫 음절의 **초성 인덱스**를 미리 계산해 두고, 사용자가 호환 자모 초성을 치면 해당 초성 인덱스로 필터링. 중성·종성 인덱스는 신경 쓰지 않는다.

- 장점: 사용자 의도에 부합. "ㄱ"을 치면 "가족", "공부", "건물" 같은 ㄱ-시작 단어가 뜬다.
- 단점: 사전 로드 시 전처리 필요, 메모리 살짝 증가.

구현 스케치:
```csharp
// KoreanDictionary 생성 시
static readonly string Choseong19 = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";

private readonly Dictionary<char, List<string>> _builtInByChoseong = new();

void IndexBuiltIn() {
    foreach (var w in _builtIn) {
        if (w.Length == 0) continue;
        var first = w[0];
        if (first < 0xAC00 || first > 0xD7A3) continue;
        int choIdx = (first - 0xAC00) / (21 * 28);
        char choChar = Choseong19[choIdx];
        if (!_builtInByChoseong.TryGetValue(choChar, out var list))
            _builtInByChoseong[choChar] = list = new();
        list.Add(w);
    }
}

// GetSuggestions:
// prefix가 길이 1이고 U+3131~U+3163 범위의 초성이면 → _builtInByChoseong 조회
```

사용자 학습 스토어도 유사하게 "초성만 매칭" 헬퍼 필요.

### 안 B — 조합 중 prefix를 "일시적 완성 음절"로 변환

초성만 있을 때는 기본 중성 `ㅏ`(index 0)·종성 없음으로 가정해 "가시적 완성 음절"을 만들고, 그걸 prefix로 사용. `ㄱ` → `가` 로 변환해 검색. 그러면 "가족", "간단"... 이 뜬다. "거울", "고양이"는 안 뜸.

- 장점: 코드 변화 적음. `HangulComposer`에 `PrefixCandidate` 같은 속성 하나 추가.
- 단점: 대부분 초성에 대해 `ㅏ`로만 매핑하므로 `거*`·`고*`·`구*` 단어가 싹 빠진다. **부정확**.

### 안 C — 프리픽스 없이, 초성 Frequency Top-K

사용자 단독 호환 자모 초성을 감지했을 때는 prefix 매칭을 포기하고 "최근 자주 쓴 단어 중 해당 초성으로 시작하는 것" Top-K를 반환. 간단하지만 결정론적이지 않아 혼란 가능.

**권장: 안 A**. 정확한 초성 매칭이 UX에 가장 자연스럽다.

---

## 3. 실험·롤아웃 절차

1. `KoreanDictionary`에 초성 인덱스를 추가하는 **시험 구현** 작성. 내장 사전만 대상으로 시작.
2. `GetSuggestions(prefix, count)` 내부에서 `prefix` 길이 1 + 호환 자모 범위이면 `_builtInByChoseong` 경로로 분기. 아닌 경우는 기존 경로 유지.
3. 사용자 사전도 동일 인덱싱 적용할지 결정 — 사용자 단어가 많지 않으므로(<5000) 매 호출 시 스캔으로 충분할 수도 있음. 일단 내장 사전만 인덱싱.
4. 프로토타입 빌드 후 수동 테스트: `ㄱ`, `ㅁ`, `ㅎ`을 쳐서 제안 품질·속도 체감.
5. 단위 테스트에서 "초성 매칭 반환 수"가 0이 아닌 것, 기존 완성 음절 prefix 케이스의 결과가 변하지 않는 것 확인.

---

## 4. 수정 금지 영역

- `HangulComposer` 내부 알고리즘 **절대 불변**.
- `KoreanDictionary.GetSuggestions`의 **기존 prefix 매칭 경로(완성 음절로 시작하는 prefix)** 의 결과는 동일해야 한다. 새 경로는 **길이 1 + 호환 자모** 라는 조건에 한정.
- `WordFrequencyStore.GetSuggestions`는 건드리지 말 것. 사용자 사전 확장은 Dictionary 계층에서 덮는다.
- 병합 순서(사용자 학습 우선 → 내장 사전 보충) 유지.
- 신규 인덱스는 **싱글톤 초기화 시 한 번만** 만들고, 이후 변경 없음(스레드 안전 보장).

---

## 5. 회귀 방지 테스트

`AltKey.Tests/`에 테스트 추가 (`KoreanDictionaryTests.cs` 신설 권장):

```csharp
[Fact]
public void GetSuggestions_with_choseong_jamo_returns_words_starting_with_that_choseong()
{
    var dict = new KoreanDictionaryTestable();    // 내장 사전이 로드된 상태
    var sugg = dict.GetSuggestions("ㄱ", 5);
    Assert.All(sugg, w => {
        Assert.InRange(w[0], '\uAC00', '\uD7A3');
        int choIdx = (w[0] - 0xAC00) / (21 * 28);
        Assert.Equal(0, choIdx); // ㄱ = index 0
    });
}

[Fact]
public void GetSuggestions_with_complete_syllable_prefix_unchanged()
{
    var dict = new KoreanDictionaryTestable();
    var suggBefore = dict.GetSuggestions("가", 5);
    // 새 경로 도입 후에도 이 결과는 이전과 동일해야 함.
    // 스냅샷 비교로 회귀 방지.
}
```

**주의**: 내장 사전 내용이 바뀌면 테스트가 함께 바뀌어야 한다. 가능하면 별도 fixture(임베디드 리소스)를 두고 테스트를 그 위에서 실행하는 쪽이 안정적.

---

## 6. 수동 검증

1. `ㄱ` → 제안 패널에 `가*`·`고*`·`공*`·`간*` 등 여러 초성이 다양하게 등장 (안 A 채택 시).
2. `가` → 기존과 동일한 제안.
3. `간` → 기존과 동일한 제안 (길이 1 호환 자모가 아니므로 새 경로 미진입).
4. 사용자 학습에 "해달"이 있을 때 `ㅎ` 입력 → "해달"이 제안에 포함.

---

## 7. 성능 주의

- 초성 인덱스는 내장 사전 로드 시 1회 계산. 메모리 ~50~100KB 증가(한국어 단어 10K 기준).
- `GetSuggestions` 호출은 키 입력마다 발생하므로 O(1) 조회 + 상위 K개 추출을 벗어나지 않아야 한다. 각 초성 리스트가 빈도순으로 **미리 정렬**되어 있다면 앞쪽 K개만 자르면 된다.

---

## 8. 후속으로 남길 과제 (이 태스크에서는 하지 말 것)

- **Bigram/문맥 제안**: 이전 단어를 고려한 확률 모델. 별도 태스크.
- **자모 타이핑 중 중성만 있는 경우**(`ㅏ` 단독): 매우 드물고 사용자 의도가 모호하므로 지금은 빈 제안 유지.
- **초성 연속** (`ㄱㄱ`): 2자 호환 자모 prefix → 첫 음절 초성이 ㄱ이고 두 번째 음절 초성도 ㄱ인 단어. 복잡도 대비 가치 낮음.

---

## 9. 커밋 메시지 초안

```
feat(dict): prefix suggestions for standalone choseong (experimental)

- HangulComposer가 호환 자모 영역(U+3131~U+3163) 1글자 상태일 때
  사전의 첫 음절 초성 인덱스로 검색.
- 내장 사전은 로딩 시 초성별 리스트로 미리 인덱싱.
- 완성 음절 prefix의 기존 동작은 변경 없음 (스냅샷 테스트로 고정).
```
