# 내장 AI (로컬 GGUF) 기능 — 구현 계획 v2

## 1. 개요

기존 **외부 API 의존** AI 텍스트 처리(✨ 버튼, `AiService`)에 **내장 로컬 모델 백엔드**를 추가한다. **CPU 전용**으로, 기본 llama.cpp(`llama-server.exe`)을 **사이드카 프로세스로 on-demand 다운로드**해 로드→추론→언로드하는 방식이다.

- 인퍼런스 엔진: **llama.cpp `llama-server.exe`(CPU prebuilt)** — 사이드카(별도 프로세스, HTTP). in-process(LLamaSharp)는 **제거**(업데이트 중단 + 키보드 앱 크래시 격리 필요).
- 모델: **내부 PoC로 두 후보 테스트** 후 승자 1개 채택. 사용자 선택 UI 없음(단일 슬롯).
  - `LGAI-EXAONE/EXAONE-4.0-1.2B-GGUF` (bf16)
  - `google/gemma-4-E2B-it-qat-q4_0-gguf` (Q4_0, ~3.35GB)

### 왜 사이드카 + CPU 전용인가
- **크래시 격리**: in-process 네이티브 크래시는 키보드 앱 전체를 동반 → 치명적. 사이드카는 프로세스 죽어도 키보드는 살아남음(`docs/ai-autocomplete/11` §3.2에서 이미 이 이유로 사이드카 선택).
- **CPU 전용**: 다른 프로젝트에서 GPU 포함 로컬 모델 실험이 여러 번 실패해 CPU 버전만 채택한 전례. `--gpu-layers 0`은 자동완성 PoC에서 이미 검증된 경로.
- **LLamaSharp 제거**: 업데이트 중단 + in-process 크래시 리스크. 기본 llama.cpp 사이드카로 통일.
- **엔진 on-demand 다운로드**: "다운로드 버튼 두 번(엔진+모델) → 바로 사용" UX. API 키 불필요.

## 2. 목표 / 비목표

### 목표
- 오프라인에서도 ✨ AI 텍스트 처리 동작 (서버·API 키 불필요)
- **CPU 전용** (GPU 없음)
- 모델·엔진 자체 관리: on-demand 다운로드 → 무결성 검증(SHA-256) → 저장 → 삭제
- on-demand 로드, idle 언로드 (상주 RAM 0)
- 기존 외부 API 백엔드와의 동일 UX·동일 fail-safe

### 비목표 (v1 제외)
- **GPU 오프로드**: CPU 전용이라 전무. (이전 v1의 auto/cpu/gpu 선택 제거)
- **키 입력 직결 AI**: 레이아웃 키에 AiAction 할당, 자동완성 후보 재평가 — `docs/ai-autocomplete/`의 포기 결정과 충돌하지 않게 on-demand ✨만 지원
- **스트리밍 입력**(대상 앱에 토큰 단위 타이핑): `docs/feature-ai/FUTURE-streaming-response.md` 보류 유지
- **다중 모델 관리 / 사용자 모델 선택**: v1은 단일 모델 슬롯. 두 후보는 내부 PoC로만 비교, UI에는 선택 없음

## 3. 설계 원칙

1. **백엔드 추상화**: `ILlmBackend` 뒤에서 외부 API와 로컬 GGUF가 상호 교환 가능. UX(✨ 플로우)는 백엔드와 무관하게 동일.
2. **CPU 고정(pinning)**: 사이드카 spawn은 항상 `--gpu-layers 0`. GPU 감지·오토디텍트 전무.
3. **on-demand 로드 / idle 언로드**: 상주 키보드 앱에 ~3GB RAM 스파이크가 남지 않도록, 미사용 시 모델이 메모리에서 빠진다.
4. **양방향 fail-safe**: 로컬 로드·추론 실패 → 명확한 에러 + 원본 텍스트 보존. 빈 결과·크래시로 연결하지 않는다.
5. **기존 동작 무영향**: 기본 백엔드는 `api`. 내장 모델을 안 쓰면 기존 코드 경로가 그대로다.

## 4. 아키텍처

```text
MainViewModel.ExecuteAiCommand
        │
        ▼
   BackendSelector (AppConfig.AiBackend)
        │
        ├─ "api"   → RemoteApiBackend   ← 기존 AiService 로직 그대로
        │
        └─ "local" → LocalGgufBackend
                         │
                    ModelStore (엔진·모델 다운로드·검증)
                         │
                    SidecarProcessManager (llama-server.exe spawn/kill/health)
                         │
                    HTTP http://127.0.0.1:<port>/v1/chat/completions
```

### `ILlmBackend`

```csharp
public interface ILlmBackend : IDisposable
{
    Task<string> ProcessTextAsync(string input, string prompt, CancellationToken ct);
    BackendState State { get; }          // NotLoaded / Loading / Ready / Unloading
    IAsyncProgress LoadProgress { get; } // 로드 시 진행률 바인딩용
}

public enum BackendState { NotLoaded, Loading, Ready, Unloading }
```

- `LocalGgufBackend` 내부 상태 머신:
  - `NotLoaded → Loading`: ✨ 클릭 시 사이드카 spawn + 모델 로드 (진행률 노출)
  - `Ready`: 추론 가능. 마지막 추론 완료 시 idle 타이머 시작
  - `Ready → Unloading`: idle 초과 또는 수동 언로드 → 사이드카 process kill (네이티브 메모리 회수)
- `RemoteApiBackend`: 기존 `AiService`를 래핑(상태는 항상 `Ready`).

## 5. 엔진 배포 (llama-server.exe, CPU prebuilt)

- **on-demand 다운로드**: 설정 창 **단일 "로컬 AI 다운로드" 버튼** 한 번으로 엔진(`llama-server.exe`) + 모델(`EXAONE-4.0-1.2B-Q8_0.gguf`)을 순서로 수신 → 각 SHA-256 검증 → `DataDir/engine/`·`DataDir/models/` 저장
- spawn: `llama-server.exe -m <gguf> -c 8192 --port <port> --gpu-layers 0 --threads <n>`
- 포트: 8081 기본, 충돌 시 8082~8090 스캔 (자동완성 `KananaProcessManager` 패턴 재사용)
- 헬스체크: `/health` poll, **CPU 로드 1~3분+ 커버하도록 타임아웃 ≥120s** (자동완성 스킬 확인 — 30s는 로드 중 프로세스 죽임)
- `Process.Start` stream redirect 금지 (llama-server 로딩 중 대량 출력 → unread buffer deadlock). `CreateNoWindow=true` + no redirect.
- **라이선스**: llama.cpp MIT — 다운로드·재배포 허용.

## 6. 모델 관리 (ModelStore)

```text
%LOCALAPPDATA%\AltKey\
├─ engine\llama-server.exe          (엔진, ~50~150MB)
└─ models\<modelId>.gguf            (모델)
   └─ manifest.json
```

- `manifest.json`: `{ modelId, fileName, sizeBytes, sha256, sourceUrl, version }`
- 다운로드: HF resolve URL → 진행률 + 재개(resume) → 완료 후 **SHA-256 검증** (불일치 시 파일 삭제 + 에러)
- 저장 위치는 사용자 데이터 디렉터리 — MSIX 설치 폴더는 읽기 전용이라 불가

### 6.1 모델 후보 (내부 PoC, 사용자 선택 없음)

| 후보 | 출처 | 크기目安 | 비고 |
|---|---|---|---|
| EXAONE-4.0-1.2B (bf16) | `LGAI-EXAONE/EXAONE-4.0-1.2B-GGUF` | ~2.5~3GB | 1.07B, 한국어 특화, chat_template.jinja repo에 있음, llama.cpp ≥b5932 필요 |
| gemma-4-E2B-it-qat (Q4_0) | `google/gemma-4-E2B-it-qat-q4_0-gguf` | ~3.35GB | QAT(≈bf16 품질), 멀티모달이라 임베딩 커서 파일 큼 |

- **PoC 실측 완료** (본머신 CPU, `--gpu-layers 0`, llama-server b12262):

| 지표 | EXAONE bf16 | **EXAONE Q8_0** | gemma Q4_0 |
|---|---|---|---|
| 파일 크기 | 2.4 GB | **1.27 GB** | 3.1 GB |
| 로드 시간(다운로드→Ready) | 1.2 s | **1.2 s** | 3.4 s |
| RAM(로드 후, ctx 8K) | ~3.0 GB | **~1.8 GB** | ~4–5 GB (추정) |
| 짧은 문장 번역 벽시간 | 1.58 s | **0.85 s** | 9–16 s (thinking) |
| 출력 | 약간 뻑뻑 | 자연스러움 | reasoning 소진 → 본문 공백 |

- **결론: EXAONE-4.0-1.2B 채택** — ✨는 "누르면 바로 결과"라 **속도가 UX 결정 요인**. EXAONE이 ~6–10배 빠르고 긴 토큰 예산 없이 즉시 본문 출력. gemma E2B는 thinking 모델이라 짧은 번역에 reasoning 400+토큰(16s+)을 쓰고 본문이 비어 옴 — 이 용도엔 과잉.
- **shipping 기본값: EXAONE-4.0-1.2B-Q8_0** — Q8_0 실측으로 bf16 대비 파일 절반(1.27GB), RAM 40% 감소(~1.8GB), 추론 더 빠름(0.85s)이고 품질은 거의 손실 없음(번역 자연스러움). Q4 계열(Q4_K_M/IQ4_XS)은 한국어 품질이 부족해 제외, **Q8_0 채택**.
- **라이선스**: EXAONE(LG) — 배포 조건 확인 후 manifest에 기록. 런타임 다운로드만 허용, 인스톨러 번들링 금지(v1과 동일).

## 7. 로드/언로드 정책

```text
✨ 클릭
  → State == NotLoaded ? LoadAsync(사이드카 spawn + 모델 로드, 진행률 UI) : 바로 추론
  → 추론 완료 → idle 타이머 시작 (AiLocalIdleUnloadMinutes, 기본 10분)
  → idle 초과 → Unloading → 사이드카 process kill
설정 창: [모델 언로드] 수동 버튼 + 현재 상태 표시
```

- 앱 종료 시 항상 사이드카 kill + `Dispose()`
- 로드 중 ✨ 재클릭은 무시(`IsAiProcessing` 가드 기존 재사용)

## 8. 추론 상세

- **chat template**: 사이드카 `/v1/chat/completions`가 GGUF 내장 템플릿을 **자동 적용** → in-process(LLamaSharp) 때의 "템플릿 직접 조립" 필요 없음. `messages: [{role, content}]`만 전송.
- **시스템 프롬프트**: 사용자 프롬프트 + `OutputOnlyGuardrail`(기존 `AiService` 상수 재사용) — "결과물 본문만" 강제
- **파라미터**(PoC 검증값): `temp 0.7, top-p 0.95, top-k 64`, `MaxTokens` 상한(설정 가능, 기본 300), `ContextSize 8192`
- **reasoning off**: EXAONE/Gemma thinking 토큰 억제 — llama-server `--reasoning off`(해당 모델 지원 시) 또는 시스템 프롬프트로 대안
- 실행: `/v1/chat/completions`(non-streaming, v1 유지 — FUTURE 문서와 동일 결론)

## 9. 설정 UX

→ **탭 레이아웃은 별도 문서 `settings-tab-layout.md` 참고** (라디오 + 조건부 섹션, 접기 없음).

- 내장 모델 선택 + 엔진/모델 미설치 상태에서 ✨ 클릭 → LiveRegion "AI 모델을 먼저 설치하세요" + 설정 창 유도
- 프롬프트·타임아웃 입력은 백엔드 공통(기존 유지)
- **idle 언로드 NumericAdjuster v1에 포함** (공통 타임아웃과 구분되는 별도 라벨 "로컬 모델 idle 언로드(분)")

## 10. 성능 (PoC 실측 — CPU 전용, EXAONE-4.0-1.2B bf16)

| 시나리오 | 실측 (CPU, `--gpu-layers 0`) |
|---|---|
| 첫 로드(다운로드 후 → Ready) | **~1.2 s** (bf16 2.4GB, ctx 8K) |
| 짧은 문장 번역(✨ 대표 작업) | **~1.6 s** |
| 긴 문단(출력 ~100토큰) | ~5–8 s 예상 (토큰 수 비례) |

→ 로드·추론 모두 2초 내라 **진행 UI는 첫 사용만 짧게** 노출하면 충분. idle 언로드로 상주 RAM 0 유지.

## 11. 리스크와 fail-safe

| 상황 | 동작 |
|---|---|
| 모델/엔진 파일 손상·미다운로드 | 에러 공지 + 설정 유도. 원본 텍스트 보존 |
| 사이드카 spawn 실패/죽음 | `IsReady=false` → 에러 공지 + 원본 유지 |
| 로드 중 취소/앱 종료 | `CancellationToken` + kill, 반쪽 상태 남김 없음 |
| 추론 타임아웃 | 기존 `AiTimeoutSeconds` 재사용 → 원본 유지 + 에러 공지 |
| SHA-256 불일치 | 파일 삭제 + 재다운로드 권고 |
| CPU 로드 1~3분+ | 진행률 UI + "로딩 중" LiveRegion. 첫 사용만 |

## 12. 구현 단위 (파일별)

### Model
- [MODIFY] `Models/AppConfig.cs`: `AiBackend`("api" 기본), `AiLocalIdleUnloadMinutes`(10), `AiLocalModel`, `AiEnginePath` 추가. (`AiLocalDevice` 제거 — CPU 전용)
- [NEW] `Models/ModelManifest.cs`: manifest 스키마 (엔진+모델 공용)

### Service
- [NEW] `Services/ILlmBackend.cs`: 인터페이스 + `BackendState`
- [MODIFY] `Services/AiService.cs`: `RemoteApiBackend`로 래핑(내부 로직 변경 없음)
- [NEW] `Services/LocalGgufBackend.cs`: 사이드카 HTTP 추론, chat messages 조립, 상태 머신
- [NEW] `Services/SidecarProcessManager.cs`: `llama-server.exe` spawn/kill/health (자동완성 `KananaProcessManager` 패턴 재사용 — §5 주의사항 포함)
- [NEW] `Services/ModelStore.cs`: 엔진·모델 다운로드·재개·SHA-256·경로 관리

### ViewModel
- [MODIFY] `ViewModels/MainViewModel.cs`: `BackendSelector` 경유 실행, 로드 진행률 바인딩, idle 타이머
- [MODIFY] `ViewModels/SettingsViewModel.cs`: 백엔드 라디오, **단일 다운로드(엔진+모델)·합산 상태**, idle 분 (레이아웃은 `settings-tab-layout.md`)

### View / DI / 프로젝트
- [MODIFY] `Views/SettingsWindow.xaml`: "AI 도구" 탭 재구성 (`settings-tab-layout.md` §3)
- [MODIFY] `App.xaml.cs`: `ILlmBackend` 등록(백엔드 팩토리)
- [MODIFY] `AltKey.csproj`: **LLamaSharp 패키지 제거** (in-process 미사용). 사이드카는 외부 exe라 NuGet 불필요.

## 13. 테스트 계획

### 단위 (`AltKey.Tests/Services/` 기능별 분리, 파일 400줄 이하)
- `ModelStore`: manifest 파싱·SHA-256 검증(합격/불일치 fixture), 경로 규칙 (엔진+모델)
- `SidecarProcessManager`: spawn arg(`--gpu-layers 0` 확인), 포트 스캔, health poll 타임아웃 (fake process)
- `LocalGgufBackend`: chat messages 조립(fixture), 상태 머신(NotLoaded→Loading→Ready→Unloading)
- `RemoteApiBackend`: 기존 `AiService` 테스트 이관·유지
- fail-safe: spawn 실패 → 예외 타입 확인

### 수동
1. CPU 머신: ✨ → 첫 로드 1~3분+ 진행률 확인 → 번역 결과, RAM 상승→idle 언로드 확인
2. 모델 미설치 + ✨ 클릭: 유도 플로우
3. 엔진/모델 다운로드 진행률·중단·재개·SHA-256 실패 시뮬레이션
4. 앱 종료 시 사이드카 프로세스 메모리 회수 확인 (tasklist)

## 14. AGENTS.md 체크리스트

- [ ] 접근성: 진행 표시 + LiveRegion 공지(로드 시작/완료/실패, 다운로드 상태)
- [ ] 주석 한국어로, 값 조정 지점에 충분히
- [ ] 테스트 위치·길이 규칙 준수
- [ ] `dotnet build` (AltKey.Tests 포함) 통과
- [ ] 커밋: `feat(ai-local): ...` 형식

## 15. v1 → v2 변경 요약

| 항목 | v1 | v2 |
|---|---|---|
| 엔진 | LLamaSharp in-process (네이티브 DLL 번들) | llama-server.exe 사이드카 (on-demand 다운로드) |
| 장치 | auto/cpu/gpu 선택 | **CPU 전용** (`--gpu-layers 0`) |
| 모델 | unsloth gemma-4-E2B UD-Q4_K_XL 단일 | EXAONE-4.0-1.2B(bf16) + gemma-4-E2B-it-qat-q4_0 **내부 PoC** 후 채택, 사용자 선택 없음 |
| chat template | 직접 조립(LLamaSharp 한계) | 서버 자동 적용 |
| 설정 탭 | §9 인라인 레이아웃 | 별도 문서 `settings-tab-layout.md` (라디오+조건부) |
