# 내장 AI (로컬 GGUF) 기능 — 구현 계획 v1

## 1. 개요

기존 **외부 API 의존** AI 텍스트 처리(✨ 버튼, `AiService`)에 **내장 로컬 모델 백엔드**를 추가한다. 별도 서버·사이드카 없이 AltKey 프로세스 안에서 GGUF 모델을 로드→추론→언로드하는 방식이다(전사 앱 패턴).

- 인퍼런스 엔진: **LLamaSharp**(llama.cpp .NET 바인딩, in-process)
- v1 모델: `unsloth/gemma-4-E2B-it-qat-GGUF:UD-Q4_K_XL` (2.62 GB, 텍스트 전용)
- 사용자 요청으로 **장치 선택(CPU/GPU/auto)을 설정화에서 고를 수 있으며 기본값은 auto**

### PoC 통과 근거 (2026-11-04, 본머신 RTX 5070 Ti)

| 항목 | GPU(auto) | CPU 전용 |
|---|---|---|
| 벽 시간(문장 번역) | 3초 | 4초 |
| 생성 속도 | 170~270 t/s | ~20 t/s |
| RAM | ~3 GB (Unsloth 공시 + 본머신 llama-server 실측 3.28GB 참조) |

번역 품질: 8개 도메인 샘플(일상/비즈니스/기술/관용/슬랭크/공문/의료/혼합)에서 7/8 클린, 관용 1건 소폭 의역. `reasoning off` + 출력 가드레일 조합에서 설명·마크다운 혼입 0건 → ✨ 붙여넣기 플로우에 그대로 사용 가능.

## 2. 목표 / 비목표

### 목표
- 오프라인에서도 ✨ AI 텍스트 처리 동작 (서버·API 키 불필요)
- 장치 선택: `auto`(기본) / `cpu` / `gpu`
- 모델 자체 관리: 다운로드 → 무결성 검증 → 저장 → 삭제
- on-demand 로드, idle 언로드 (상주 RAM 0)
- 기존 외부 API 백엔드와의 동일 UX·동일 fail-safe

### 비목표 (v1 제외)
- **키 입력 직결 AI**: 레이아웃 키에 AiAction 할당, 자동완성 후보 재평가 — `docs/ai-autocomplete/`의 포기 결정과 충돌하지 않게 on-demand ✨만 지원
- **스트리밍 입력**(대상 앱에 토큰 단위 타이핑): `docs/feature-ai/FUTURE-streaming-response.md` 보류 유지
- **다중 모델 관리**: v1은 단일 모델 슬롯. manifest 구조는 확장 가능하게만 두고 UI는 1개 모델용

## 3. 설계 원칙

1. **백엔드 추상화**: `ILlmBackend` 뒤에서 외부 API와 로컬 GGUF가 상호 교환 가능. UX(✨ 플로우)는 백엔드와 무관하게 동일.
2. **장치 고정(pinning)**: `GpuLayerCount`를 항상 명시적으로 지정한다. 런타임 오토디텍트에 맡기면 바쁜 GPU를 잡아오는 등 비결정적 동작이 생긴다(`dotnet-local-llm` 스킬 규칙).
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
                    ModelStore (파일·다운로드·검증)
                         │
                    LLamaSharp (in-process)
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
  - `NotLoaded → Loading`: ✨ 클릭 시 `LoadAsync()` (진행률 노출)
  - `Ready`: 추론 가능. 마지막 추론 완료 시 idle 타이머 시작
  - `Ready → Unloading`: idle 초과 또는 수동 언로드 → `weights.Dispose()` (네이티브 메모리 회수)
- `RemoteApiBackend`: 기존 `AiService`를 래핑(상태는 항상 `Ready`).

## 5. 장치 선택 정책

설정 `AiLocalDevice`: `"auto"`(기본) | `"cpu"` | `"gpu"`

| 선택 | LLamaSharp 동작 | 결과 |
|---|---|---|
| `auto` | 로드 전 CUDA 존재 감지 → 있으면 GPU 레이어 all, 없으면 0 | 결정적. 매 로드마다 재감지(경량) |
| `cpu` | `GpuLayerCount = 0`, CPU 백엔드만 사용 | GPU 무관 |
| `gpu` | `GpuLayerCount = all`, CUDA 백엔드 필요 | GPU 없으면 **명확한 에러**("GPU를 찾을 못했습니다. auto로 변경하세요") — 침묵 fallback 금지 |

- 백엔드 NuGet: `LLamaSharp.Backend.Cpu` + `LLamaSharp.Backend.Cuda12`(구현 시 이 머신 CUDA 버전과 패키지 호환 확인)
- 감지 실패·에러는 LiveRegion 공지 + 설정 창 유도.

## 6. 모델 관리 (ModelStore)

```text
%LOCALAPPDATA%\AltKey\models\
└─ gemma-4-E2B-it-qat-UD-Q4_K_XL.gguf   (2.62 GB)
└─ manifest.json
```

- `manifest.json`: `{ modelId, fileName, sizeBytes, sha256, sourceUrl, version }`
- 다운로드: HF resolve URL → 진행률 노출 + 재개(resume) 지원 → 완료 후 **SHA-256 검증** (불일치 시 파일 삭제 + 에러)
- 저장 위치는 사용자 데이터 디렉터리 — MSIX 설치 폴더는 읽기 전용이라 불가
- **라이선스**: Gemma ToU — 런타임 다운로드만 허용, 인스톨러 번들링 금지. 따라서 다운로드 플로우가 필수 경로(설계와 일치)
- v1은 단일 모델 슬롯(`AiLocalModel` = modelId). manifest 스키마는 다중 모델 확장 여지만 두자

## 7. 로드/언로드 정책

```text
✨ 클릭
  → State == NotLoaded ? LoadAsync(진행률 UI) : 바로 추론
  → 추론 완료 → idle 타이머 시작 (AiLocalIdleUnloadMinutes, 기본 10분)
  → idle 초과 → Unloading → weights.Dispose()
설정 창: [모델 언로드] 수동 버튼 + 현재 상태 표시
```

- 앱 종료 시 항상 `Dispose()`
- 로드 중 ✨ 재클릭은 무시(`IsAiProcessing` 가드 기존 재사용)

## 8. 추론 상세

- **chat template 직접 조립** — LLamaSharp은 GGUF 내장 Jinja 템플릿을 실행하지 못함(`ApplyTemplate` 금지, 스킬 확인). Gemma 4 E2B 마커(본모델에서 검증됨):
  ```text
  <|turn>system\n{system}\n<turn|>\n<|turn>user\n{user}\n<turn|>\n<|turn>model\n
  ```
- **시스템 프롬프트**: 사용자 프롬프트 + `OutputOnlyGuardrail`(기존 `AiService` 상수 재사용) — "결과물 본문만" 강제
- **파라미터**(PoC 검증값): `temp 0.7, top-p 0.95, top-k 64`, `MaxTokens` 상한(설정 가능, 기본 300), `ContextSize 8192`
- **reasoning off**: Gemma 4는 thinking 토큰을 내뱉음(lama.cpp `--reasoning off` 대응). LLamaSharp의 해당 API는 구현 시 설치된 패키지 문서로 확정 — 없으면 템플릿/토큰 필터로 대안
- 실행: `StatelessExecutor` + `InferAsync`(토큰 스트림) → 전체 수신 후 반환(v1은 non-streaming 유지, FUTURE 문서와 동일 결론)

## 9. 설정 UX (설정 창 "AI 도구" 탭 확장)

```text
┌──────────────────────────────────────────────┐
│ AI 텍스트 처리                                │
├──────────────────────────────────────────────┤
│ 백엔드                                        │
│   (•) 외부 API      ( ) 내장 모델             │
│                                              │
│ ── 외부 API 선택 시: 기존 항목 그대로          │
│ ── 내장 모델 선택 시:                          │
│   장치        [auto ▾]  (auto / CPU / GPU)    │
│   모델        gemma-4-E2B (2.62 GB)           │
│             [다운로드] [삭제]                  │
│             상태: 미설치 / 다운로드 중 45% / 준비됨 │
│   idle 언로드  [10] 분                        │
└──────────────────────────────────────────────┘
```

- 내장 모델 선택 + 모델 미설치 상태에서 ✨ 클릭 → LiveRegion "AI 모델을 먼저 설치하세요" + 설정 창 유도
- 프롬프트·타임아웃 입력은 백엔드 공통(기존 유지)

## 10. 성능 기대 (PoC 실측 기반)

| 시나리오 | GPU(auto, 5070 Ti) | CPU 전용 |
|---|---|---|
| 짧은 문장 번역 | ~3초 | ~4초 |
| 긴 문단(출력 ~100토큰) | ~3초 | ~6~8초 예상 |

→ 진행 표시(`IsAiProcessing` + 로드 진행률)만 있으면 어느 장치에서도 수용 가능.

## 11. 리스크와 fail-safe

| 상황 | 동작 |
|---|---|
| 모델 파일 손상/미다운로드 | 에러 공지 + 설정 유도. 원본 텍스트 보존 |
| `gpu` 선택 + GPU 부재 | 명확한 에러, auto 변경 권고 |
| 로드 중 취소/앱 종료 | `CancellationToken` + `Dispose`, 반쪽 상태 남김 없음 |
| 추론 타임아웃 | 기존 `AiTimeoutSeconds` 재사용 → 원본 유지 + 에러 공지 |
| SHA-256 불일치 | 파일 삭제 + 재다운로드 권고 |

## 12. 구현 단위 (파일별)

### Model
- [MODIFY] `Models/AppConfig.cs`: `AiBackend`("api" 기본), `AiLocalDevice`("auto" 기본), `AiLocalIdleUnloadMinutes`(10), `AiLocalModel` 추가
- [NEW] `Models/ModelManifest.cs`: manifest 스키마

### Service
- [NEW] `Services/ILlmBackend.cs`: 인터페이스 + `BackendState`
- [MODIFY] `Services/AiService.cs`: `RemoteApiBackend`로 래핑(내부 로직 변경 없음)
- [NEW] `Services/LocalGgufBackend.cs`: LLamaSharp 로드/추론/언로드, chat template 조립, 장치 pinning
- [NEW] `Services/ModelStore.cs`: 다운로드·재개·SHA-256·경로 관리

### ViewModel
- [MODIFY] `ViewModels/MainViewModel.cs`: `BackendSelector` 경유 실행, 로드 진행률 바인딩, idle 타이머
- [MODIFY] `ViewModels/SettingsViewModel.cs`: 백엔드 라디오, 장치 선택, 모델 설치/삭제/상태, idle 분

### View / DI / 프로젝트
- [MODIFY] `Views/SettingsWindow.xaml`: "AI 도구" 탭 확장(§9 레이아웃)
- [MODIFY] `App.xaml.cs`: `ILlmBackend` 등록(백엔드 팩토리)
- [MODIFY] `AltKey.csproj`: `LLamaSharp`, `LLamaSharp.Backend.Cpu`, `LLamaSharp.Backend.Cuda12` + 네이티브 DLL(`llama.dll`, `ggml*.dll`) `<None Include>` + `CopyToOutputDirectory=PreserveNewest`(자동 복사 안 됨 — 스킬 확인)

## 13. 테스트 계획

### 단위 (`AltKey.Tests/Services/` 기능별 분리, 파일 400줄 이하)
- `ModelStore`: manifest 파싱·SHA-256 검증(합격/불일치 fixture), 경로 규칙
- `LocalGgufBackend`: chat template 조립(fixture: §8 마커), 장치 선택 로직(auto/cpu/gpu × GPU 유무)
- `RemoteApiBackend`: 기존 `AiService` 테스트 이관·유지
- fail-safe: 로드 실패 → 예외 타입 확인 (네이티브 로드는 fake weights로 분리 테스트)

### 수동
1. auto + GPU 머신: ✨ → 3초 내 결과, RAM 상승→idle 언로드 확인
2. cpu 고정: 동일 플로우, ~4초
3. gpu 선택 + GPU 부재 시나리오(시뮬레이션): 에러 메시지 확인
4. 모델 미설치 + ✨ 클릭: 유도 플로우
5. 다운로드 진행률·중단·재개·SHA-256 실패 시뮬레이션
6. 앱 종료 시 프로세스 메모리 회수 확인

## 14. AGENTS.md 체크리스트

- [ ] 접근성: 진행 표시 + LiveRegion 공지(로드 시작/완료/실패)
- [ ] 주석 한국어로, 값 조정 지점에 충분히
- [ ] 테스트 위치·길이 규칙 준수
- [ ] `dotnet build` (AltKey.Tests 포함) 통과
- [ ] 커밋: `feat(ai-local): ...` 형식
