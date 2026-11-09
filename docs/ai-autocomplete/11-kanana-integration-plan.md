# 11 — Kanana-2-1.3B 통합 계획 (접근법 판정 + 세부 설계)

> **이 문서의 역할**: 이전 PoC(09, 10)에서 시도했던 다양한 통합 접근법을 비교·판정하고, Kanana-2-1.3B를 AltKey에 통합하는 세부 계획을 세운다.
>
> **전제**: `poc-bigram-roles/`의 Kanana PoC 결과(2026-11-09)가 유효하다 — 1.3B Q4_K_M, top-1 76%, top-3 88%, MRR 0.837, avg 263ms.

## 1. 접근법 비교 판정

AltKey에서 AI 모델을 통합하려는 시도는 세 가지 프레임으로 나뉜다.

### 1.1 매 키스트로크 rerank (docs 03~05)

| 항목 | 내용 |
|---|---|
| 후크 지점 | `SuggestionsChanged` (매 키 입력마다) |
| 제약 | p50 < 30ms (원래), 완화해도 ~100ms |
| 모델 출력 역할 | **상위 포지션 소유** — AI가 top-N을 직접 결정 |
| 시도한 모델 | laya-ko (74~204ms FAIL), Qwen3.5-0.8B (1~2s FAIL) |

**Kanana 적용 판정: 부적합.**
- 263ms는 매 키스트로크에 못 씀. 사용자가 "아"를 치고 263ms 후에 바가 갱신되면 입력 흐름이 끊김.
- 상위 포지션 소유 구조는 doc 09의 "일관성 한계"를 재현: AI가 top-1을 잡으면 bigram이 밀려남. 한 번 어긋나도 신뢰 깨짐.
- **결론: 이 프레임에서 Kanana는 쓸 수 없다.**

### 1.2 단어 수락 직후 비동기 (doc 10 — bigram roles)

| 항목 | 내용 |
|---|---|
| 후크 지점 | `AcceptSuggestion`/`AcceptBigramSuggestion` **이후** |
| 제약 | 100~300ms 윈도우 (사용자가 다음 단어를 스캔하기 전) |
| 모델 출력 역할 | **bigram 히트보다 아래만 배치** — 승격 불가, 디모트 전용 |
| 시도한 모델 | laya-ko (게이트 0 실패로 미실행) |

**Kanana 적용 판정: 적합.**
- 263ms가 300ms 윈도우에 들어감.
- 구조적 보장("AI는 bigram 히트 위로 못 감")이 일관성 문제를 소멸시킴:
  - bigram 히트가 충분하면 → AI는 보이지 않음 (노이즈 없음)
  - bigram 히트가 희박하면 → AI가 꼬리를 채움 (filler)
  - 저count 노이즈 쌍이면 → AI가 디모트 (demoter, 옵션)
- **결론: 이 프레임이 Kanana와 가장 잘 맞는다.**

### 1.3 배치/캐시 의존 (doc 09 대안 3)

Qwen 계열(요청당 1~2초)을 실시간에 못 쓰고, 캐시+배치로만 쓰는 경로. Kanana는 263ms로 실시간이 가능하므로 이 프레임 불필요.

### 1.4 판정 요약

| 접근법 | Kanana 적합성 | 이유 |
|---|---|---|
| 매 키스트로크 rerank | ✗ | 263ms > 키 입력 반응 예산, 일관성 리스크 |
| **단어 수락 직후 비동기** | **○** | 263ms < 300ms 윈도우, 구조적 보장이 일관성 해결 |
| 배치/캐시 의존 | ✗ | 불필요 — Kanana는 실시간 가능 |

→ **선택: doc 10의 역할 프레임 (filler + demoter)에 Kanana-2-1.3B를 탑재한다.**

## 2. 모델 특성 → 설계 영향

Kanana는 laya-ko와 본질이 다른 **디코더**(다음 토큰 예측)다. 이 차이가 구현을 결정한다.

| | laya-ko (인코더) | Kanana (디코더) |
|---|---|---|
| 입력 형태 | 후보를 옵션으로 나열, choice/score 질문 | 문맥만 입력, 다음 토큰 분포 출력 |
| 점수 산출 | 각 옵션에 확률 직접 부여 | top-K logprobs에서 후보 토큰 id 조회 |
| 시퀀스 길이 | 후보 수에 비례 (100~341토큰) | **문맥만** (~20~60토큰) — 후보 무관 |
| 단일 forward | O(후보수) | O(1) — 문맥 1회만 |

**핵심 설계 결과**: Kanana는 **후보 수와 무관하게 1회 forward**로 끝난다. 8개든 20개든 후보 스코어링 비용은 동일(문맥 토큰화 + top-K 추출). 이 때문에:
- 지연이 후보수에 비례하지 않음 (PoC 실측: 8개 후보 263ms)
- top-K를 넉넉히 잡아도(500) 추가 비용 거의 없음

## 3. 세부 계획

### 3.1 스코어링 메커니즘 (PoC 검증 완료)

```
입력: context_words (최근 3~5단어, prev 포함)
      candidates (bigram 히트 제외, 빈도 스토어 + 내장 사전 상위 N개)

프로세스:
1. prompt = " ".join(context_words)
2. llama-server /v1/completions 호출 (top_k=5000, max_tokens=1, temperature=0)
3. 응답의 choices[0].logprobs.content[0].top_logprobs → {id, token, logprob} 목록
4. 각 candidate:
   a. leading-space 토크나이징 (" " + word)
   b. top-K에서 해당 토큰 id 조회
   c. 발견 → score = logprob / 미발견 → score = -inf
5. score 내림차 정렬 → model_order
```

**PoC에서 확인한 주의사항**:
- 모델이 예측하는 토큰에 **선두 공백** 포함 (" 마신다" not "마신다") → 후보도 " word"로 토크나이즈해야 매칭됨
- top-K=500이면 커버리지 부족(대부분 사전 단어가 부재), **5000 권장** (118ms, 300ms 예산 내)
- multi-word candidate은 첫 토큰만 매칭 (v1 범위)

### 3.2 아키텍처 — 사이드카 프로세스

| 항목 | 결정 |
|---|---|
| 실행 방식 | `llama-server.exe` 별도 프로세스 (PoC 검증 완료) |
| 통신 | HTTP `http://127.0.0.1:8081/v1/completions` |
| 생명주기 | AltKey 앱 시작 시 spawn, 종료 시 kill |
| 크래시 격리 | 모델 프로세스 죽어도 키보드는 살아남음 |
| IPC 오버헤드 | ~5-10ms (263ms 중 2~4%) — 윈도우 내 허용 |

**이유**:
- in-process(llama.dll 링크)는 크래시가 AltKey 전체를 동반 → 키보드 앱에 치명적
- 사이드카는 이미 PoC에서 검증된 경로
- `AltKey.Tools` 분리 아키텍처(AGENTS.md §6)와 동일한 패턴 — 별도 프로세스 + HTTP

**프로세스 관리**:
- 시작: `llama-server.exe -m <gguf_path> -c 4096 --port 8081 --threads <n>`
- 종료: AltKey exit 시 process kill
- 헬스체크: `/health` endpoint, 3초 poll → 타임아웃 시 fallback
- 포트 충돌: 8081 사용 중이면 8082~8090 스캔

### 3.3 통합 지점 — Normal + Bigram 모두

AI는 **두 모드 모두**에 적용된다:

#### Normal 모드 (매 키스트로크)

`KoreanDictionary.GetSuggestions(prefix, prevWord)`가 **사전 단어 + bigram 히트(부스트)**를 조합해 바에 노출. prefix 매칭만으로는 문맥과 안 맞는 사전 단어를 걸러낼 수 없음 → AI demote 필요.

```
트리거: 매 자모 입력 (KoreanInputModule:110,117)
컨텍스트: prevWord (이전 확정 단어) — 조합 중 고정
후보: 사전 prefix 매칭 + bigram 히트 (GetSuggestions 내부 결합)
AI 적용: 캐시된 분포에서 극단값 디모트
```

#### Bigram 모드 (단어 수락 후)

`AutoCompleteService.AcceptSuggestion()` → `bigramStore.GetNexts()` 결과만 노출. bigram 쌍 only.

```
트리거: AcceptSuggestion / AcceptBigramSuggestion
컨텍스트: 수락된 단어
후보: bigram hits only
AI 적용: filler (희박 바 채움) + demote (저count 노이즈)
```

#### 캐시 전략 (Normal 모드 핵심)

모델 입력 = **prevWord 컨텍스트** (현재 조합 중 자모 포함 안 함). 조합 중에는 컨텍스트가 바뀌지 않으므로:

```
단어 수락 → 컨텍스트 변경 → forward pass 1회 (263ms) → 분포 캐시 저장
     ↓
"마" 입력 → prefix 필터 + 캐시 분포 조회 → 극단값 디모트 (~0ms)
"마시" 입력 → prefix 필터 + 같은 캐시 → 디모트 (~0ms)
     ↓
"마신다" 수락 → 컨텍스트 변경 → forward pass 다시 (263ms)
```

- **forward pass: 컨텍스트당 1회** (단어 수락 시점)
- **매 키스트로크: 캐시 히트** — prefix만 바뀌고 점수는 동일
- 자모 조합 중 "결정이 크게 변하는" 상황 없음 — 변하는 건 후보집합(prefix)이지, 각 후보의 점수가 아님

### 3.4 캐시 전략

| 키 | 값 | TTL |
|---|---|---|
| `(context_hash, candidate_set_hash)` | `model_order` + scores | 세션 내 (앱 종료 시 해제) |

- 같은 문맥+후보집합이 반복되면(같은 단어 다시 타이핑) → 캐시 히트, 0ms
- LRU cap: 512 entries
- doc 07의 RerankCache와 동일한 구조 — 재사용 가능

### 3.5 설정 모델 (AppConfig 확장)

| 필드 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `AiAutoCompleteEnabled` | bool | false | AI 기능 on/off |
| `AiModelPath` | string | "" | GGUF 파일 경로 |
| `AiPort` | int | 8081 | 사이드카 포트 |
| `AiFillerMaxCount` | int | 3 | filler 배치 최대 개수 |
| `AiTimeoutMs` | int | 300 | 타임아웃 상한 |

- `AiAutoCompleteEnabled=false` → 모든 AI 경로 no-op, 프로세스 spawn 안 함
- 설정 UI: doc 06 패턴 재사용 (on/off + 다운로드 상태 + 모델 정보)

### 3.6 모델 배포

| 항목 | 값 |
|---|---|
| 파일 | `kanana-2-1.3b-instruct-Q4_K_M.gguf` (815MB) |
| 출처 | `ssolunar/Kanana-2-1.3b-instruct-Q4_K_M-GGUF` (HuggingFace) |
| 라이선스 | **확인 필요** — 원본 kakaocorp/kanana-2-v-decide-3b-2611의 라이선스가 GGUF 변환에 상속되는지 확인 |
| 저장 위치 | `DataDir/models/kanana-2-1.3b-instruct-Q4_K_M.gguf` |
| 다운로드 | doc 02 ModelStore 인프라 재사용 (SHA-256 검증) |

**라이선스 게이트**: 배포 전 원본 모델 라이선스 확인 필수. 커뮤니티 GGUF 변환의 라이선스가 원본과 동일한지, 상업적 사용이 허용되는지 문서화.

### 3.7 fail-safe 및 fallback

| 상황 | 동작 |
|---|---|
| 프로세스 미시작/죽음 | `IsLoaded=false` → 빈도 폴백 원본 |
| HTTP 타임아웃 (>300ms) | 빈도 폴백 원본 |
| 응답 파싱 오류 | 빈도 폴백 원본 |
| 모델 파일 부재 | 프로세스 spawn 안 함 → 빈도 폴백 |
| AI off 설정 | 모든 경로 no-op |

**절대 빈 바로 안 감**: fail-safe는 항상 "기존 후보 원본"으로 회귀.

### 3.8 테스트 계획

| 레벨 | 내용 | 도구 |
|---|---|---|
| 단위 | `CandidateReorder` pure function (기존 doc 04 재사용) | `AltKey.Tests` |
| 단위 | `KananaClient` HTTP 클라이언트 (mock server) | `AltKey.Tests` |
| 통합 | 사이드카 spawn → health → score → kill | `AltKey.Tests` (skip: CI에서 GGUF 부재 시) |
| E2E | 실제 모델 + 30케이스 contexts.json → top-1/top-3/MRR | 수동 (PoC 재사용) |
| 성능 | p50/p95 지연, 캐시 히트율 | `random_benchmark.py` 확장 |

**테스트 위치**: `AltKey.Tests/Services/AiRerank/` 폴더 (AGENTS.md §5 — 기능별 분리)

### 3.9 UX 타이밍 — "즉시 노출 + demote-only"

#### 문제

현재 제안 바는 매 자모 입력마다 즉시 갱신됨. Kanana가 263ms를 필요로 하면:
- A: AI 대기 후 노출 → 263ms 빈 바 (체감 지연)
- B: 즉시 노출 + AI 도착 시 전체 재배치 → 후보 "점프" (어색)
- **C: 즉시 노출 + demote-only** → baseline 유지, 극단값만 조용히 뒤로

#### 결정: C (즉시 노출 + demote-only)

| 원칙 | 내용 |
|---|---|
| baseline 즉시 표시 | 자모 입력 시 사전+bigram 후보를 바로 노출 (기존 동작 그대로) |
| AI 결과 적용 시점 | **단어 수락 시점(컨텍스트 변경)에 1회** forward pass 263ms. 그 안의 매 키스트로크는 캐시 히트(~0ms) |
| stale 처리 | doc 05 latest-wins: 컨텍스트가 이미 바뀐 결과 → 버림 (무음) |
| 애니메이션 | 불필요 — 1~2자리 이동은 눈에 안 띄는 수준 |
| 사용자 체감 | "바가 항상 있고, 가끔 안 맞는 단어가 조용히 뒤로 감" |

#### 300ms 갭의 실제 빈도 (캐시 전략)

forward pass는 **컨텍스트 변경(단어 수락) 시에만 1회**. 그 안의 매 키스트로크는 캐시 분포에서 prefix 필터만:

```
"커피를" 수락 → forward pass 263ms (스캔 윈도우 — 체감 낮음)
     ↓
"마" 입력 → 캐시 히트 ~0ms → 디모트 적용
"마시" 입력 → 캐시 히트 ~0ms → 디모트 적용
"마신다" 수락 → forward pass 263ms (다음 스캔 윈도우)
```

- **forward pass 시점 = 스캔 윈도우**: 사용자가 후보를 보고 다음 단어를 고르는 순간. 타이핑 중이 아님
- **매 키스트로크: ~0ms** — 캐시 분포에서 prefix 필터만 → 지연 체감 없음
- **stale 폐기**: 263ms 안에 다음 단어 수락되면 이전 결과 버림

#### 왜 점프/애니메이션이 문제 없는지

- **demote-only**: 승격이 없으므로 "새 단어가 갑자기 앞에 등장" 상황이 없음
- **극단값만**: logprob < -15인 후보만 건드림 — 대부분 순서 불변
- **forward pass 시점 = 스캔 윈도우**: AI가 발현하는 지점은 단어 수락 직후 사용자가 후보를 **스캔하고 다음 단어를 고르는 순간**. 타이핑 중이 아님
- **매 키스트로크 ~0ms**: 캐시 히트라 지연 없음 — "300ms마다 점프" 상황 자체가 안 생김
- **stale 폐기**: 263ms 안에 다음 단어 수락되면 이전 결과 버림 → "단어 사라짐" 상황 없음

#### 구현 시 주의

- `SuggestionBarViewModel.Suggestions` 갱신은 **Dispatcher.Invoke** (UI 스레드)
- AI 결과 적용 시 `Suggestions` 컬렉션에서 target 인덱스의 항목을 제거 → 끝에 재삽입
- 변경 감지: 이전 순서와 비교해 실제로 이동한 항목이 없으면 UI 갱신 안 함 (불필요한 리플렌 방지)

#### 추가 고려: 미세 전환 애니메이션

v1: 애니메이션 없음. v2 확장 조건: 실제 사용에서 "단어가 뒤로 밀리는" 순간이 어색하게 느껴지면, 이동한 항목에 **100~200ms fade-in**만 적용. 대량 재배열 애니메이션은 과함.

v1은 **C(없음)**로 시작하고, 실제 사용에서 어색함이 느껴지면 A/B로 확장. WPF `Storyboard` + `DoubleAnimation`으로 구현 가능. 제안 바 항목 수(5~8개)라 성능 부담 없음.

```
AltKey/Services/AiRerank/
├── KananaClient.cs          # HTTP 클라이언트 (llama-server 통신)
├── KananaProcessManager.cs  # 사이드카 spawn/kill/health
├── FillerScorer.cs          # filler 로직 (후보 스코어링 + 배치)
├── CandidateReorder.cs      # pure function (doc 04 재사용)
└── AiRerankCache.cs         # 문맥별 캐시 (doc 07 패턴)

AltKey.Tests/Services/AiRerank/
├── KananaClientTests.cs
├── FillerScorerTests.cs
└── CandidateReorderTests.cs   # doc 04 테스트 재사용
```

### 3.10 Demoter 역할 — 우선순위 재평가 (v1 승격 근거)

#### UX 변화가 만든 신기회

최근 수정으로 bigram 모드에서 **기본 사전 + 사용자 사전 단어가 추가로 노출**됨. 후보 풀이 bigram 히트만에서 "히트 + 사전 상위"로 확대되면서, 문맥과 극단적으로 안 맞는 단어도 바에 섞이는 문제가 생김.

→ "완벽한 랭킹"이 아니라 **극단적으로 안 맞는 것만 뒤로 밀기**만 해도 바로 UX 개선.

#### 이전 시도와 차이

| | laya-ko (폐기, `8db56fa`) | Kanana (현재) |
|---|---|---|
| 질문 형태 | "이 쌍이 자연스러운가?" (NLI 판단) | "이 문맥 뒤에 이 단어가 올 확률은?" (예측) |
| 점수 메커니즘 | p_true — good/bad 판별력 없음 | logprob — PoC에서 작동 확인 |
| 실패 원인 | 모델이 "판단" 과제를 못 함 | 해당 없음 — 디코더의 본업 |

#### v1 승격 논리

- **리스크 최소화**: 승격 없음, 극단값만 감점 → 기존 순서 깨지 않음
- **임계값**: logprob < -15 → 디모트 (PoC v4에서 확정)
- **효과 즉각성**: 사전 노출로 생긴 노이즈는 바로 제거 가능
- **fail-safe 자연스러움**: 안 밀려도 손해 없음 (기존 순서 유지)

#### Gate 2 PoC 결과 (2026-11-09, v4)

| 지표 | 값 |
|---|---|
| top-K | 5000 (118ms) |
| threshold | -15 |
| 테스트 케이스 | 6개 (각 8후보) |
| 디모트율 | **6.2%** (48개 중 3개) |
| 평균 latency | 103ms |

**판정: 통과 (보조 역할로 확정)**
- "확실히 안 맞는" 것만 1~2자리 밀기 — conservative
- 커버리지 제한: 희소/특정 단어("기입해", "재부팅되어")는 top-5000에 부재 → score 못 받음 (건드리지 않음)
- 영향력: 바의 ~6% 수준. **polish**이지 transform 아님
- worst case = neutral (안 밀려도 손해 없음)

#### 폐기된 문서에서 재사용할 것

- 보수적 정책: count ≤ 2만, top-3 바깥만, 최대 1자리 이동
- "후보군 내 상대 비교" 프레임 → Kanana의 "후보별 logprob 비교"로 자연 매핑

### 3.11 단계별 로드맵

| 단계 | 내용 | 산출물 | 예상 기간 |
|---|---|---|---|
| **P0** | 라이선스 확인 | 문서 (라이선스 판정) | 1일 |
| **P1** | `KananaClient` + `ProcessManager` | C# 파일 + 테스트 | 2~3일 |
| **P2** | `FillerScorer` + `DemoterScorer` + 통합 | C# 파일 + 테스트 | 3~4일 |
| **P3** | 설정 UI + 모델 다운로드 | SettingsWindow 확장 | 1~2일 |
| **P4** | 캐시 + fail-safe + 성능 튜닝 | 코드 + 벤치마크 | 1~2일 |
| **P5** | E2E 검증 (filler + demoter) | 결과 문서 | 1~2일 |

**총: ~10~14일** (P0 라이선스 제외)

## 4. 이전 문서와의 관계

| 문서 | 상태 |
|---|---|
| `09-model-viability.md` | laya-ko/Qwen 포기 결정은 **유지**. Kanana는 새 모델이므로 그 결정을 무효화하지 않음 — "laya-ko 안 됨, Qwen 안 됨"은 그대로. |
| `10-bigram-mode-model-roles.md` | 역할 프레임(filler/demoter)과 게이트 정의는 **재사용**. PoC 게이트(0: 지연, 1: filler, 2: demoter)에서 Kanana가 **전부 통과** (Gate 2: top-K=5000, threshold -15, 디모트율 6.2%). |
| `03~05` (ONNX reranker) | **무효** — Kanana는 ONNX가 아닌 GGUF/llama.cpp. 파일 구조·인터페이스만 참고. |
| `07-cache.md` | 캐시 구조 **재사용**. |
| `08-perf-fallback-testing.md` | fail-safe 패턴 **재사용**. |

## 5. 미해결 사항

1. ~~**라이선스**: 원본 kakaocorp 모델의 라이선스가 무엇인지, GGUF 변환이 배포 가능한지 확인 (P0)~~ → **확정: Kanana Open License — AltKey 사용 가능** (§5.1)
2. **multi-word candidate**: "설정에서" 같은 2단어 후보는 첫 토큰만 매칭 — v1 한계, v2에서 multi-token scoring
3. **SWA/YaRN 제거 영향**: GGUF 변환 과정에서 SWA(window 1024)·YaYN rope 제거됨. 1024토큰 이내에서는 동일, 그 이상은 미검증 — AltKey 문맥(~20~60토큰)에는 영향 없음
4. **3B vs 1.3B**: 3B가 품질 +7pp(83% vs 76%)이나 지연 2.1배(556ms vs 263ms). v1은 1.3B, 3B는 옵션(고성능 모드)으로 향후
5. ~~**demoter 임계값**: "극단적으로 안 맞는"의 logprob 임계값(-20?)은 P2 PoC에서 실데이터로 확정~~ → **확정: -15** (Gate 2 PoC v4, top-K=5000)

### 5.1 P0 라이선스 판정 (2026-11-09)

**라이선스**: Kanana Open License Agreement (kakaocorp/kanana-2-v-decide-3b-2611)

**판정: AltKey 배포에 사용 가능**

| 항목 | 판정 | 근거 |
|---|---|---|
| 상업 라이선스 필요? | **불필요** | §4.2 — "development and operation of your own services"에 해당 (로컬 데스크톱 앱, API/클라우드 제공 아님) |
| GGUF 변환 (Derivative Work) | **허용** | §2.1 — Derivative Works 생성·분포 허용 |
| Q4_K_M 양자화 | **허용** | Model Weights의 표현 형식 변경 — §9.4 "except as expressly permitted" 범위 |

**분포 시 준수 의무 (AltKey가 이행해야 할 것)**:

| # | 의무 | 구현 위치 |
|---|---|---|
| 1 | LICENSE 파일 포함 | `installer/` + `DataDir/models/LICENSE` |
| 2 | Notice 텍스트 | `DataDir/models/NOTICE.txt`: "Kanana is licensed in accordance with the Kanana Open License Agreement. Copyright © KAKAO Corp. All Rights Reserved." |
| 3 | 수정 파일 명시 | README: "GGUF 변환은 ssolunar/Kanana-2-1.3b-instruct-Q4_K_M-GGUF에서 가져옴" |
| 4 | **"Powered by Kanana" 표시** | 설정 UI AI 섹션 + README + 웹사이트 |

**Risky Zone (피할 것)**:
- AltKey를 SaaS/클라우드 서비스로 제공 → §4.1(i) 상업 라이선스 필요
- AltKey를 SI/온프레미즈 솔루션에 번들링해 판매 → §4.1(ii)
- AltKey를 온디바이스 도메인에 임베드해 재판매 → §4.1(iii)

→ 현재 AltKey의 사용形态(오픈소스 로컬 데스크톱 앱, 사용자가 직접 설치)는 모두 해당 안 됨.
