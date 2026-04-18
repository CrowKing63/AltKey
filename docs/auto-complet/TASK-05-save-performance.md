# TASK-05 — `WordFrequencyStore.Save()` 빈번 I/O + 에러 조용히 삼킴

> **심각도**: 중간 (성능 체감 + 장애 원인 파악 방해)
> **선행 독해**: [CORE-LOGIC-PROTECTION.md](CORE-LOGIC-PROTECTION.md) §2 (UnsafeRelaxedJsonEscaping 유지, MaxWords 상수 유지)
> **예상 소요**: 45~75분

---

## 1. 문제 두 가지

### 1.1 매 단어마다 디스크 쓰기

`AltKey/Services/WordFrequencyStore.cs:27~35`

```csharp
public void RecordWord(string word)
{
    if (string.IsNullOrWhiteSpace(word)) return;
    word = word.Trim();
    if (word.Length == 0) return;
    _freq[word] = (_freq.TryGetValue(word, out var c) ? c : 0) + 1;
    if (_freq.Count > MaxWords) PruneLowest();
    Save();   // ← 매번 전체 Dictionary 직렬화 + WriteAllText
}
```

- `Save()`는 `JsonSerializer.Serialize` + `File.WriteAllText`를 수행.
- 사용자 사전이 수천 건 쌓이면 한 번 저장에 수~수십 ms. 공백·엔터·제안 수락마다 GC 압박과 지연.
- 또한 `WriteAllText`는 원자적 쓰기가 아니라 중간에 프로세스 종료 시 **부분 쓰기로 파일 깨짐** 가능.

### 1.2 에러 조용히 삼킴

`AltKey/Services/WordFrequencyStore.cs:51~60`

```csharp
public void Save()
{
    try { ... File.WriteAllText(_filePath, json); }
    catch { /* 저장 실패 — 무시 */ }
}
```

- 디스크 풀, 파일 잠금, 권한 오류, 경로 문제 모두 **무음 실패**.
- 사용자는 "학습이 안 되네"라고 느끼지만 원인 추적 불가.
- 테스트에서도 실패 신호가 없어 회귀 감지 어려움.

---

## 2. 해결 방향

### 2.1 디바운스 저장 (권장)

- `RecordWord` 호출 시 즉시 저장하지 않고 **N ms 뒤 1회 저장** 예약.
- 연속 입력이 쏟아지면 마지막 입력 후 N ms 대기한 다음 한 번만 쓴다.
- N = 500~1500ms 권장. 사용자가 자동완성을 쓰는 리듬 기준.
- 앱 종료 시 반드시 `Flush()` — 이미 `Save()` public이므로 종료 훅에서 호출.

구현 스케치:

```csharp
private readonly System.Timers.Timer _debounceTimer;
private readonly object _saveLock = new();
private bool _pending;

public WordFrequencyStore(string languageCode)
{
    _filePath = ...;
    Load();
    _debounceTimer = new System.Timers.Timer(1000) { AutoReset = false };
    _debounceTimer.Elapsed += (_, _) => FlushIfPending();
}

public void RecordWord(string word)
{
    ... // 기존 빈도 갱신
    ScheduleSave();
}

private void ScheduleSave()
{
    lock (_saveLock) { _pending = true; }
    _debounceTimer.Stop();
    _debounceTimer.Start();
}

public void FlushIfPending()
{
    bool shouldSave;
    lock (_saveLock) { shouldSave = _pending; _pending = false; }
    if (shouldSave) Save();
}

/// 앱 종료 시 호출 (App.OnExit)
public void Flush()
{
    _debounceTimer.Stop();
    FlushIfPending();
}
```

**주의**: 타이머 콜백은 스레드풀에서 실행되므로 `Save()`가 `_freq`를 읽고 직렬화하는 동안 `RecordWord`가 `_freq`를 수정하면 `InvalidOperationException`이 터진다. 현재 `_freq`는 락 없이 접근되는데, 디바운스 도입 시엔 **직렬화 순간만 스냅샷 복사**가 필요:

```csharp
public void Save()
{
    Dictionary<string, int> snapshot;
    lock (_saveLock) { snapshot = new(_freq); }
    // snapshot을 직렬화
}
```

`RecordWord`도 `_saveLock` 안에서 `_freq` 수정. 또는 `ConcurrentDictionary<string,int>`로 교체도 가능하지만 JSON 포맷 호환성 유지를 위해 일반 Dictionary + lock이 덜 침습적.

### 2.2 원자적 쓰기

`File.WriteAllText`는 중단 시 파일을 깨뜨릴 수 있다. 다음 패턴으로 교체:

```csharp
var tmp = _filePath + ".tmp";
File.WriteAllText(tmp, json);
File.Move(tmp, _filePath, overwrite: true);  // Windows에서 원자적
```

### 2.3 에러 로깅

`catch`에서 완전히 삼키지 말고 최소한의 진단을 남긴다. 이 저장소에 범용 로거가 없다면:

```csharp
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"[WordFrequencyStore] Save failed ({_filePath}): {ex}");
    // 선택: 마지막 에러를 public 속성으로 노출해 상태표시줄/설정창에서 표시
    LastSaveError = ex;
}

public Exception? LastSaveError { get; private set; }
```

정책 질문: 실패가 누적되면 사용자에게 알릴 것인가? 첫 PR에서는 **Debug.WriteLine + LastSaveError 속성**만 두고, UI 노출은 별도 태스크로.

---

## 3. 수정 금지 영역

- `MaxWords = 5000` 상수 유지.
- `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` 유지 (한글 `\uXXXX` 회피 목적 — 제거 시 파일 가독성 저하).
- `Load()`의 빈 파일 자동 생성 동작 유지 (첫 실행 시 없는 파일을 생성).
- `GetSuggestions`의 `prefix.Length < kv.Key.Length` 필터 유지 (prefix 자기 자신은 제안에서 제외).
- **파일 포맷**(Dictionary<string,int> JSON) 변경 금지. 사용자 기존 데이터 호환성.

---

## 4. 변경 파일

1. `AltKey/Services/WordFrequencyStore.cs` — 디바운스 + 원자적 쓰기 + 로깅.
2. `AltKey/App.xaml.cs` (또는 종료 훅이 있는 곳) — `Flush()` 호출 추가.
3. `AltKey.Tests` — 디바운스 동작·Flush 동작·스냅샷 일관성 테스트.

---

## 5. 회귀 방지 테스트

```csharp
[Fact]
public void RecordWord_does_not_save_immediately_when_debounced()
{
    var store = new WordFrequencyStore("test-ko");  // 테스트용 임시 디렉터리
    store.RecordWord("해달");
    // 디바운스 초기화 직후 파일 내용은 빈 딕셔너리 or 직전 상태
    var jsonBefore = File.ReadAllText(store.FilePath);
    Assert.DoesNotContain("해달", jsonBefore);

    store.Flush();
    var jsonAfter = File.ReadAllText(store.FilePath);
    Assert.Contains("해달", jsonAfter);
}

[Fact]
public void RecordWord_burst_only_writes_once_after_flush()
{
    var store = new WordFrequencyStore("test-ko");
    int writeCount = 0;
    store.SaveCompleted += () => writeCount++;

    for (int i = 0; i < 100; i++) store.RecordWord($"단어{i}");
    store.Flush();

    Assert.Equal(1, writeCount);
}

[Fact]
public void Save_failure_exposes_LastSaveError()
{
    var store = new WordFrequencyStore("test-ko");
    // 파일을 잠그거나 읽기전용으로 바꿔 Save를 실패시킴
    store.RecordWord("단어1");
    store.Flush();
    Assert.NotNull(store.LastSaveError);
}
```

테스트에서 실제 파일 I/O를 쓰는 게 부담이면 `WordFrequencyStore` 생성자를 `IFileSystem` 추상화로 분리하는 방안이 있지만, 스코프를 크게 만든다. **임시 디렉터리(`Path.GetTempPath()`) 사용이 더 간단**.

---

## 6. 수동 검증

1. 자동완성 ON 상태로 한국어 문장 여러 개 타이핑.
2. 타이핑 도중 `user-words.ko.json` 수정 시각(Last Write Time)을 관찰 — 과거보다 적게 갱신되는지.
3. 앱 정상 종료(닫기 버튼) → 파일이 최신 상태로 저장되었는지 확인.
4. 앱 비정상 종료(작업 관리자 강제 종료) → 기존 파일이 깨지지 않고, 직전 Flush 시점 상태로 복원되는지.
5. 폴더 권한을 제거하여 Save 실패 유도 → `LastSaveError`에 예외가 기록되는지.

---

## 7. 성능 기대치

- 100 단어 입력 시 디스크 쓰기 100회 → **1회**.
- 각 쓰기는 여전히 O(N) (N = 사전 크기) 직렬화이지만, 디바운스로 빈도가 줄어든다.
- 추가 메모리: 타이머 1개, lock 오브젝트 1개. 무시할 수준.

---

## 8. 후속 과제 (이 태스크에서는 하지 말 것)

- **증분 저장**: 변경된 엔트리만 WAL 방식으로 append. 구현 복잡도 높음.
- **사전 압축**: JSON → MessagePack/Protobuf. 호환성 깨짐.
- **암호화**: 사용자 입력 이력은 개인 정보일 수 있음. 별도 보안 검토 후 태스크 분리.

---

## 9. 커밋 메시지 초안

```
perf(store): debounce WordFrequencyStore writes + surface Save errors

- RecordWord 호출 시 즉시 저장 대신 1s 디바운스 타이머로 예약.
- 앱 종료 훅에서 Flush() 호출 (App.OnExit).
- WriteAllText 대신 tmp + File.Move 원자적 쓰기.
- Save 실패 시 Debug.WriteLine + LastSaveError 속성 노출.
- Save 중 _freq 동시 수정 방지를 위해 _saveLock + 스냅샷 직렬화.
```
