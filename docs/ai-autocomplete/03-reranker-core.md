# 03 — LayaReranker 추론 코어 (ONNX Runtime + 인터페이스)

## 목표
`laya-ko` ONNX 모델을 **AltKey 프로세스 안에서 CPU로 추론**하는 코어(`LayaReranker`)를 만든다. Python/Ollama/별도 서버에 의존하지 않는다.

## 배경 (레포 기준)
- ONNX Runtime NuGet(`Microsoft.ML.OnnxRuntime`)은 아직 레포에 없다. 이 단계에서 `AltKey.csproj`에 추가한다.
- 기존 `AiService.cs`(OpenAI HTTP 클라이언트)와 **무관**. 이름 충돌 주의: 그 파일의 global type(`AiServiceException`, `ChatMessage` 등)과 겹치지 않게 `AltKey.Services.AiRerank` 서브네임스페이스에 둔다.
- 2번의 `ModelStore`가 모델을 디스크에 준비한다. 본 단계는 "파일이 이미 있는 상태"를 전제로 한다.

## ✅ 시퀀스 구성 알고리즘 (C# 포트 대상, docs/ONNX.md 확정)
`[CLS] <type> question: <instructions> [SEP] [MASK] opt0 [MASK] opt1 ... [SEP] state [SEP]`
1. 옵션을 라벨 순서로 렌더(choice → "key: description", 설명 없으면 "key")
2. instructions/option/state의 문자 그대로 `<mask>`를 공백으로 치환
3. 각 옵션을 `[MASK] + tokenize(" " + option_text)[:48]`로 토큰화(선행 공백 필수)
4. `opt_budget = head_max_len - Σ len(option)`. 16 미만이면 모든 옵션을 `max(4, (head_max_len-16)/n_options)`으로 잘라 재계산
5. instruction 토큰을 `max(8, opt_budget)`로 잘라
6. 조립하고 각 옵션 마커 위치 기록
7. 나머지 공간을 state 토큰으로 채우고 마지막 `[SEP]`
8. 어떤 마커가 max_len에 도달/초과하면 거부(안 맞음)

- budget: instructions+모든 옵션은 256 토큰 이내(옵션당 최대 48), state는 1024까지(오른쪽부터 잘림)
- `qtype` = 0(choice). AltKey용 instruction 예: "이 문장의 다음 단어로 가장 자연스러운 것을 고르세요"

## ✅ 입력/출력 시그니처 (확인됨)
- **ONNX 입력 텐서**: `input_ids`(int64), `attention_mask`(int64), `marker_pos`(int64), `marker_mask`(int64), `qtype`(int64). 출력: logits(옵션별 점수).
- **마커 메커니즘**: 각 후보(옵션)에 `[MASK]` 마커를 배치하고, 그 위치(`marker_pos`)와 마스크(`marker_mask`)를 텐서로 전달. 문맥 + 모든 후보를 **한 번의 forward pass**로 넣어 옵션별 확률을 얻는다(`qtype=choice`).
- **토크나이저**: ONNX 그래프에 내장 안 됨(입력이 `input_ids` 정수). `tokenizer.json`(34MB BPE, 256k vocab, 특수 토큰 `<bos>/<eos>/<mask>/<pad>`)이 repo에 공개 — C#으로 포트(Rust `rust/src/lib.rs`의 build_sequence가 참고 구현). **검증**: `golden.json`(13케이스)의 기대 토큰 id·마커 위치·확률로 C# 포트 정확성 확인.

## 작업
1. `AltKey/Services/AiRerank/LayaReranker.cs` 작성:
   - `InferenceSession`을 **한 번만** 생성해 멤버로 유지. `IDisposable`.
   - `void EnsureLoaded()` — 세션이 없으면 생성, 있으면 no-op. (5번이 제안 바 활성화 시점에 호출)
   - `void Unload()` — 세션 해제 + 메모리 회수. (5번이 제안 바 비활성화 시점에 **무조건** 호출)
   - `bool IsLoaded` — 현재 세션 존재 여부.
   - `Task<RerankResult> RerankAsync(string context, IReadOnlyList<string> candidates, int topN, CancellationToken ct)`:
     - `IsLoaded`가 false면 null 반환(호출자가 fallback).
     - 후보를 토크나이저로 인코딩 → ONNX 입력 텐서 구성 → 세션 실행 → logits/점수 추출.
     - **후보만 평가**: 모델이 생성하지 않고, 주어진 후보에 점수를 매기거나 순위를 내린다.
   - `RerankResult`: `IReadOnlyList<string> Selected`(topN), 필요 시 후보별 점수.
2. 실패 시 **예외를 던지지 말고** `null`/빈 결과 반환하는 계약(5번 fallback이 이걸로 잡는다). 또는 전용 예외(`AiRerankException`, 기존 `AiServiceException`과 다른 이름)를 만들어 5번이 catch한다.
3. 타임아웃: `CancellationTokenSource.CreateLinkedTokenSource(ct)` + 상한(예: 150ms, 8번에서 튜닝).

## 하지 말 것
- 모델을 매 호출마다 로드하지 않는다(한 번만).
- ONNX Runtime을 WPF UI 스레드에서 동기 호출하지 않는다. `async`로 둔다.
- 기존 `AiService`의 DTO/예외를 재사용하지 않는다.

## 검증
- `AltKey.Tests`:
  - 작은 **테스트용 ONNX**(합성 가중치)로 세션 로드→추론→점수 순서 검증.
  - 타임아웃 만료 시 빈 결과 반환.
  - 파일 없음/손상 시 예외(또는 null) 경로.
- 실제 `laya-ko` 모델이 도착하면(2번) integration smoke: 대표 문맥 3~5개로 추론이 정상 점수를 내는지 확인.

## 산출물
- `LayaReranker.cs`, `AltKey.csproj` NuGet 추가, 테스트. **모델 I/O 시그니처를 이 파일 상단에 기록.**
