# TASK-06 — `PruneLowest()` 동점 경계에서 대량 삭제 방지

> **심각도**: 낮음 (현재 MaxWords=5000이라 터질 확률은 낮으나, 트리거 시 피해가 큼)
> **선행 독해**: [CORE-LOGIC-PROTECTION.md](CORE-LOGIC-PROTECTION.md) §2 (MaxWords·JSON 포맷 유지)
> **예상 소요**: 20~30분

---

## 1. 문제

`AltKey/Services/WordFrequencyStore.cs:78~84`

```csharp
private void PruneLowest()
{
    // 빈도 하위 20% 제거
    var threshold = _freq.Values.OrderBy(v => v).ElementAt(_freq.Count / 5);
    foreach (var key in _freq.Keys.Where(k => _freq[k] <= threshold).ToList())
        _freq.Remove(key);
}
```

의도: 상한(5000) 초과 시 "빈도 하위 20%"를 제거. 실제 동작:

1. `ElementAt(count/5)`로 20% 지점 값을 `threshold`로 삼는다.
2. `freq <= threshold`인 **모든** 항목 제거.

문제는 threshold 지점에서 **동점(ties)**가 많을 때 발생한다.

예: 전체 5001개 중 4500개가 빈도 1, 500개가 빈도 2, 1개가 빈도 100.
- `count/5 = 1000` → `OrderBy(v)`의 1000번째 값은 **1**.
- `threshold = 1` → `freq <= 1`인 엔트리 **4500개 일괄 제거**.
- 의도한 "20% 제거"가 아니라 **90% 제거**가 된다.

이 시나리오는 "사전이 막 쌓이기 시작해서 대부분 단어 빈도가 1"일 때 특히 쉽게 발생한다.

---

## 2. 원인 본질

"하위 20% 경계값 이하"라는 조건은 빈도가 **연속 분포**일 때만 올바르게 작동한다. 자연어 단어 빈도는 Zipf 분포에 가까워 하위 빈도에 동점이 집중된다.

---

## 3. 해결 방향

### 안 A — 삭제 개수를 상한으로 고정 (권장)

"20%를 정확히 제거"하되 정렬 순서 기준으로 개수만 보장.

```csharp
private void PruneLowest()
{
    int targetRemoveCount = _freq.Count / 5;
    if (targetRemoveCount == 0) return;

    var toRemove = _freq
        .OrderBy(kv => kv.Value)          // 빈도 오름차순
        .ThenBy(kv => kv.Key)             // 동점이면 문자열 순 (결정론적)
        .Take(targetRemoveCount)
        .Select(kv => kv.Key)
        .ToList();

    foreach (var k in toRemove) _freq.Remove(k);
}
```

- 장점: 정확히 20% 제거. 결정론적.
- 단점: 동점 중 "일부만" 살아남음. 공정성 이슈는 크지 않다(어차피 빈도 1끼리는 우선순위 차이가 없다).

### 안 B — threshold에서 동점이면 "부족분만" 제거

```csharp
var targetRemoveCount = _freq.Count / 5;
var threshold = _freq.Values.OrderBy(v => v).ElementAt(targetRemoveCount);

// threshold 미만은 전부 제거
var strictlyLess = _freq.Where(kv => kv.Value < threshold).Select(kv => kv.Key).ToList();
foreach (var k in strictlyLess) _freq.Remove(k);

// 남은 부족분은 threshold와 같은 값 중 일부
int remaining = targetRemoveCount - strictlyLess.Count;
if (remaining > 0)
{
    var equal = _freq
        .Where(kv => kv.Value == threshold)
        .OrderBy(kv => kv.Key)
        .Take(remaining)
        .Select(kv => kv.Key)
        .ToList();
    foreach (var k in equal) _freq.Remove(k);
}
```

- 안 A와 결과는 같지만 알고리즘이 명시적.
- 더 읽기 쉬운 쪽을 고르면 됨.

**권장**: 안 A. 한 번의 정렬 + Take로 끝나고 의도가 명확.

### 안 C — 빈도 1 단어에 별도 수명 타이머

"최근 30일 내 재등장이 없는 빈도 1 단어는 제거" 같은 복잡한 정책은 이번 스코프를 벗어난다. **후속 과제**.

---

## 4. 추가로 손 볼 점

### 4.1 MaxWords 초과 직후 한 번만 prune

현재 `RecordWord` 내부에서 `if (_freq.Count > MaxWords) PruneLowest();`. 한 번 정리 후에도 여전히 `>MaxWords`일 수 있다(정상적으로는 20% 제거로 충분하지만). 방어적으로:

```csharp
while (_freq.Count > MaxWords) PruneLowest();
// 단, PruneLowest가 0개 제거하면 무한 루프 — 가드 필요
```

안 A에서 `targetRemoveCount == 0`이면 return하므로 _freq.Count가 MaxWords 근처에서 제자리일 수 있다. MaxWords = 5000 → 5000/5 = 1000이므로 1회로 충분. **굳이 while 루프 도입하지 말 것** (무한 루프 리스크).

### 4.2 Prune 결과 로깅

장기 운영 시 프루닝 빈도와 삭제량을 디버그로 남기면 튜닝에 도움.

```csharp
System.Diagnostics.Debug.WriteLine(
    $"[WordFrequencyStore] Pruned {toRemove.Count} of {originalCount} words.");
```

---

## 5. 수정 금지 영역

- `MaxWords = 5000` 상수 유지.
- `_freq` 딕셔너리 타입·시그니처 유지(파일 포맷 불변).
- `RecordWord`의 Prune 트리거 시점 유지(`_freq.Count > MaxWords`).
- `Save()` 호출 흐름 유지.

---

## 6. 회귀 방지 테스트

```csharp
[Fact]
public void PruneLowest_removes_exactly_twenty_percent_when_ties_exist()
{
    var store = new WordFrequencyStore("test-ko-prune");
    // 5001개 단어: 4500개 빈도 1, 500개 빈도 2, 1개 빈도 100
    for (int i = 0; i < 4500; i++) store.RecordWord($"a{i}");
    for (int i = 0; i < 500; i++) { store.RecordWord($"b{i}"); store.RecordWord($"b{i}"); }
    store.RecordWord("big");  // 첫 기록
    for (int i = 0; i < 99; i++) store.RecordWord("big");

    store.Flush();
    // MaxWords=5000이므로 RecordWord 내부에서 PruneLowest가 한 번 호출됨
    int totalAfter = store.Count;
    Assert.InRange(totalAfter, 3990, 4010);  // 5001 - 1000 ± 오차
    Assert.True(store.Contains("big"));       // 빈도 100은 살아남아야
}

[Fact]
public void PruneLowest_is_deterministic_across_runs()
{
    // 동일 입력 시퀀스 → 동일 생존 단어 집합
    var a = BuildStore();
    var b = BuildStore();
    Assert.Equal(a.Keys.OrderBy(x => x), b.Keys.OrderBy(x => x));
}

[Fact]
public void PruneLowest_no_op_when_under_max()
{
    var store = new WordFrequencyStore("test-ko-small");
    for (int i = 0; i < 100; i++) store.RecordWord($"w{i}");
    store.Flush();
    Assert.Equal(100, store.Count);  // Prune 발동 안 함
}
```

테스트 가시성을 위해 `WordFrequencyStore`에 `public int Count => _freq.Count;`와 `public bool Contains(string w) => _freq.ContainsKey(w);` 정도를 **테스트 전용**으로 추가해도 되고, 기존 `GetSuggestions` 경유로도 간접 검증 가능.

---

## 7. 수동 검증

자연 사용 패턴으로는 5000개 돌파가 드물어 재현이 어렵다. **단위 테스트가 유일한 안전망**이므로 위 테스트를 반드시 추가할 것.

---

## 8. 후속 과제

- 빈도 1 단어의 수명 기반 정리(LRU + frequency).
- 사용자 설정으로 MaxWords 조정.
- Prune 결과를 사용자에게 알림 (선택 기능).

---

## 9. 커밋 메시지 초안

```
fix(store): prune exactly N words when tie-breaking on threshold

- OrderBy(value).ThenBy(key).Take(count/5) 로 정렬 순서 기준 N개 제거.
- 기존 `freq <= threshold` 조건이 빈도 동점에서 대량 삭제를 유발하던 문제 해결.
- 빈도 1 단어가 80% 넘게 쌓인 초기 상태에서도 20%만 정확히 제거.
- 회귀 테스트 추가 (동점 시나리오, 결정론, 상한 미달 시 no-op).
```
