# `docs/auto-complet/` — 한국어 자동완성 가드레일 + 개선 작업지시서

> 이 폴더는 **AltKey의 가장 취약한 서브시스템인 한국어 자동완성**을 AI 에이전트가 안전하게 유지·보수·개선할 수 있도록 만든 자료 모음이다.
>
> **배경**: 이 기능은 설계-구현-회귀를 4번 반복한 끝에 겨우 안정화되었고, "무조건 코드 접근 금지"로 막아 두는 것만으로는 추가 개선 여지가 막힌다. 그래서 (1) "절대 건드리면 안 되는 것"을 명시한 가드레일과 (2) 작은 단위로 쪼갠 개선 작업지시서를 분리했다.

---

## 목차

| 파일 | 성격 | 대상 독자 |
|---|---|---|
| [CORE-LOGIC-PROTECTION.md](CORE-LOGIC-PROTECTION.md) | **필수 참조**: 핵심 로직 + 수정 금지 영역 + 수정 시 체크리스트 | 모든 에이전트 |
| [findings-overview.md](findings-overview.md) | 2026-04-18 분석으로 찾은 버그·개선점 **요약 표** | 계획 수립 단계 |
| [TASK-01-accept-tracked-length-reset.md](TASK-01-accept-tracked-length-reset.md) | 제안 수락 직후 이어쓰기 시 제안 전체 삭제 버그 | 구현 에이전트 |
| [TASK-02-dead-code-imm-ime.md](TASK-02-dead-code-imm-ime.md) | `IsImeKorean()` 등 호출되지 않는 dead code 정리 | 구현 에이전트 |
| [TASK-03-dictionary-quality.md](TASK-03-dictionary-quality.md) | 단일 자모(`ㄱ`·`ㅏ`) 학습 오염 방지 | 구현 에이전트 |
| [TASK-04-composer-prefix-fallback.md](TASK-04-composer-prefix-fallback.md) | 초성 단독 시 제안이 거의 안 뜨는 UX 개선 | 구현 에이전트 |
| [TASK-05-save-performance.md](TASK-05-save-performance.md) | `WordFrequencyStore.Save()` I/O 빈도 완화 + 에러 로깅 | 구현 에이전트 |
| [TASK-06-prune-lowest-safety.md](TASK-06-prune-lowest-safety.md) | `PruneLowest()` 대량 삭제 경계 조건 | 구현 에이전트 |
| [TASK-07-test-coverage-gaps.md](TASK-07-test-coverage-gaps.md) | 회귀 방지용 추가 테스트 목록 | 구현 에이전트 |
| [TASK-08-composer-dead-code.md](TASK-08-composer-dead-code.md) | `HangulComposer`의 호출되지 않는 private 메서드 정리 | 구현 에이전트 |

---

## 사용 흐름 (에이전트 지시용)

1. 에이전트는 **가장 먼저 [CORE-LOGIC-PROTECTION.md](CORE-LOGIC-PROTECTION.md)를 전부 읽는다.**
2. 사용자가 구체적 태스크를 지시하지 않았다면, [findings-overview.md](findings-overview.md)로 현재 알려진 문제 목록을 확인한 뒤 사용자에게 우선순위를 묻는다.
3. 태스크가 정해지면 해당 `TASK-XX-*.md` 한 개만 읽고, 그 안의 "수정 금지 영역"을 절대 건드리지 않는다.
4. 변경 후 `AltKey.Tests`의 `HangulComposerTests`와 `KoreanInputModuleTests`를 **반드시 실행**하여 회귀를 확인한다.

---

## 작성·갱신 규칙

- 각 `TASK-XX-*.md`는 **단일 파일 32K 토큰 한도**를 넘기지 않는다 (대략 100KB 텍스트 이하).
- 새 버그·개선점이 발견되면 번호를 이어 붙여(TASK-09-*, TASK-10-*) 한 파일 한 관심사로 유지한다.
- `CORE-LOGIC-PROTECTION.md`의 "절대 건드리지 말 것" 목록은 **버그가 실제로 고쳐졌을 때**만 축소한다. 임의 삭제 금지.
