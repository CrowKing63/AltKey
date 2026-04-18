# `docs/auto-complet/` — 한국어 자동완성 가드레일 + 완료된 개선 이력

> 이 폴더는 **AltKey의 가장 취약한 서브시스템인 한국어 자동완성**을 AI 에이전트가 안전하게 유지·보수·개선할 수 있도록 만든 자료 모음이다.
>
> **배경**: 이 기능은 설계-구현-회귀를 4번 반복한 끝에 겨우 안정화되었다. (1) "절대 건드리면 안 되는 것"을 명시한 가드레일과 (2) 이미 해결된 개선 작업의 이력을 분리해 보존한다.

---

## 현재 상태 (2026-04-18 기준)

- **[CORE-LOGIC-PROTECTION.md](CORE-LOGIC-PROTECTION.md) §2 "절대 건드리지 말 것"**은 현재도 유효한 하드 프리즈 규칙이다. 자동완성/한글 조합 코드를 수정하려면 먼저 읽어야 한다.
- **TASK-01 ~ TASK-08은 전부 해결 완료되었다.** 해당 `TASK-XX-*.md` 파일과 `findings-overview.md`는 완료 이력 보존을 위해 남겨둘 뿐이며, **AI 에이전트가 기본 흐름으로 다시 순회할 필요는 없다.**

---

## 목차

| 파일 | 성격 | 상태 |
|---|---|---|
| [CORE-LOGIC-PROTECTION.md](CORE-LOGIC-PROTECTION.md) | **필수 참조**: §2의 하드 프리즈 규칙 | 유효 |
| [findings-overview.md](findings-overview.md) | 2026-04-18 분석으로 찾은 버그·개선점 요약 표 | 전 항목 해결 완료 |
| [TASK-01-accept-tracked-length-reset.md](TASK-01-accept-tracked-length-reset.md) | 제안 수락 직후 이어쓰기 시 제안 전체 삭제 버그 | ✅ 해결 (cfc87a9) |
| [TASK-02-dead-code-imm-ime.md](TASK-02-dead-code-imm-ime.md) | `IsImeKorean()` 등 호출되지 않는 dead code 정리 | ✅ 해결 (145d621) |
| [TASK-03-dictionary-quality.md](TASK-03-dictionary-quality.md) | 단일 자모(`ㄱ`·`ㅏ`) 학습 오염 방지 | ✅ 해결 (b222690) |
| [TASK-04-composer-prefix-fallback.md](TASK-04-composer-prefix-fallback.md) | 초성 단독 시 제안이 거의 안 뜨는 UX 개선 | ✅ 해결 (7ba5009) |
| [TASK-05-save-performance.md](TASK-05-save-performance.md) | `WordFrequencyStore.Save()` I/O 빈도 완화 + 에러 로깅 | ✅ 해결 (0aba873) |
| [TASK-06-prune-lowest-safety.md](TASK-06-prune-lowest-safety.md) | `PruneLowest()` 대량 삭제 경계 조건 | ✅ 해결 (6594c99) |
| [TASK-07-test-coverage-gaps.md](TASK-07-test-coverage-gaps.md) | 회귀 방지용 추가 테스트 목록 | ✅ 해결 (602afa1) |
| [TASK-08-composer-dead-code.md](TASK-08-composer-dead-code.md) | `HangulComposer`의 호출되지 않는 private 메서드 정리 | ✅ 해결 (de40720) |

---

## 사용 흐름 (에이전트 지시용)

1. 자동완성/한글 조합 코드를 수정하기 전에 [CORE-LOGIC-PROTECTION.md](CORE-LOGIC-PROTECTION.md) §2를 확인한다.
2. 사용자가 **새로운** 자동완성 버그·개선점을 지시한 경우에만 이 폴더에 새 `TASK-09-*.md`를 추가하고, 기존 TASK 파일들은 참조만 한다.
3. 변경 후 `AltKey.Tests`의 `HangulComposerTests`, `KoreanInputModuleTests`, `KoreanDictionaryTests`, `WordFrequencyStoreTests`를 **반드시 실행**하여 회귀를 확인한다.

---

## 작성·갱신 규칙

- 각 `TASK-XX-*.md`는 **단일 파일 32K 토큰 한도**를 넘기지 않는다 (대략 100KB 텍스트 이하).
- 새 버그·개선점이 발견되면 번호를 이어 붙여(TASK-09-*, TASK-10-*) 한 파일 한 관심사로 유지한다.
- `CORE-LOGIC-PROTECTION.md`의 "절대 건드리지 말 것" 목록은 **버그가 실제로 고쳐졌을 때**만 축소한다. 임의 삭제 금지.
