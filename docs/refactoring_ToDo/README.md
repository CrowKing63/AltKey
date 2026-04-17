# AltKey 한국어 전용 리팩토링 — 작업 지시서 모음

> **대상 브랜치**: `feature/unified-autocomplete`
> **작성일**: 2026-04-17
> **상위 기획 문서**:
> - [docs/refactor-unif-serialized-acorn.md](../refactor-unif-serialized-acorn.md) — v2 리팩토링 계획(영어 모듈 섹션 제거됨)
> - [docs/refactor-unified-autocomplete.md](../refactor-unified-autocomplete.md) — 1차 리팩토링 기록 + 사용자 생각(§7)
> - [docs/feature-korean-autocomplete.md](../feature-korean-autocomplete.md) — 한국어 자동완성 원본 설계
> - [docs/ime-korean-detection-problem.md](../ime-korean-detection-problem.md) — IME 우회 설계 배경

---

## 1. 이 리팩토링이 무엇인가

AltKey를 **한국어 사용자 전용 가상 키보드**로 재편한다. 다국어 확장 가능성을 염두에 두고 시작한 프로젝트지만, IME 상태 동기화와 한국어 자동완성 구현 과정에서 코드 복잡도가 감당할 수 없는 수준으로 누적되어 노선을 변경한다.

**핵심 설계 결정**:

1. **한국어 레이아웃 1장**만. 내부 Submode(`HangulJamo`/`QuietEnglish`)를 "가/A" 버튼으로 토글.
2. **OS IME 한/영 키(VK_HANGUL)는 상단바의 비상 버튼**으로만 존재. 레이아웃에서 제거.
3. **자동완성 ON ↔ Unicode 입력 모드 / 자동완성 OFF ↔ VirtualKey 입력 모드** 연동. 상단바 토글. 기본값 **OFF**(호환성 우선).
4. `KoreanInputModule` 하나가 `HangulComposer` + 두 사전 + Submode를 소유. 기존 `HandleKoreanLayoutKey`/`HandleEnglishSubMode` 로직을 그대로 이전.
5. "해+ㅆ → 해T" 버그 수정(`HasActiveModifiersExcludingShift`).
6. `user-words.{ko|en}.json` 파일 분리.
7. AutomationProperties + LiveRegion만 이번 릴리스 접근성 범위.

**폐기**: `qwerty-en.json`, `PrimaryLanguage` 개념, `EnglishInputModule` 구상, `_layoutSupportsKorean` 게이트, 영어 전용 사용자 경로 전반.

---

## 2. 태스크 순서와 의존성

각 파일은 **전후 맥락 없이 단독으로 착수 가능**하도록 작성했다. 선행 태스크의 산출물은 "전제 조건" 섹션에 명시한다.

```
[00] overview (참조용, 모든 태스크가 읽고 시작)
      │
      ▼
[01] models-interfaces  ─────┐
      │                      │
      ▼                      │
[02] wordstore-split ────────┤
      │                      │
      ▼                      ▼
[03] korean-input-module (+"해+ㅆ" 버그 수정)
      │
      ▼
[04] autocomplete-service
      │
      ▼
[05] keyboard-viewmodel
      │
      ├──────────┬──────────┐
      ▼          ▼          ▼
   [06]        [07]       [08]
  autocomplete 토글      접근성
  토글       + 상단바
      │          │          │
      └──────────┼──────────┘
                 ▼
              [09] 한국어 전용 정리
                 │
                 ▼
              [10] 테스트 & 검증
```

- **01 → 02 → 03 → 04 → 05**: 순차(모델 → 저장소 → 모듈 → 서비스 → 뷰모델)
- **06, 07, 08**: 05 이후 서로 독립. 병렬 가능.
- **09**: 06·07·08 이후.
- **10**: 전체 통합 후 마지막.

---

## 3. 공통 규칙

- 모든 태스크는 `feature/unified-autocomplete` 브랜치에서 작업.
- 각 태스크 완료마다 커밋 분리. 커밋 메시지 prefix: `refactor(ko-only):`.
- **알고리즘 변경 금지**: `HangulComposer` 내부, `SendAtomicReplace`, `CompositionDepth` 로직은 보존. 위치만 이동.
- **JSON 스키마**: 호환성 파괴 변경은 `KeySlot.HangulLabel` → `KeySlot.EnglishLabel` 리네이밍 하나뿐(01에서 처리). 레이아웃 파일도 함께 업데이트.
- **언어**: 모든 코드 주석과 문서는 한국어. 식별자는 영어.

---

## 4. 파일 목록

| # | 파일 | 요지 |
|---|---|---|
| [00](00-overview.md) | `00-overview.md` | 공통 컨텍스트(아키텍처, 파일 구조, 용어집) — 모든 태스크가 참조 |
| [01](01-models-interfaces.md) | `01-models-interfaces.md` | `InputSubmode` enum, `IInputLanguageModule`, `ToggleKoreanSubmodeAction`, `KeySlot.HangulLabel`→`EnglishLabel` |
| [02](02-wordstore-split.md) | `02-wordstore-split.md` | `WordFrequencyStore` 언어별 인스턴스 분리, `user-words.{ko|en}.json` |
| [03](03-korean-input-module.md) | `03-korean-input-module.md` | `KoreanInputModule` 신규 + "해+ㅆ" 버그 수정(`HasActiveModifiersExcludingShift`) |
| [04](04-autocomplete-service.md) | `04-autocomplete-service.md` | `AutoCompleteService` 단일 `OnKey()`로 축약, 모듈 위임 |
| [05](05-keyboard-viewmodel.md) | `05-keyboard-viewmodel.md` | 3상태 필드 제거, `KeySlotVm.GetLabel` 확장, 모듈 이전 정리 |
| [06](06-autocomplete-toggle.md) | `06-autocomplete-toggle.md` | 상단바 자동완성 토글 + Unicode/VirtualKey 연동, 기본 OFF |
| [07](07-toggle-and-topbar.md) | `07-toggle-and-topbar.md` | "가/A" 토글 키 + 상단바 VK_HANGUL 비상 버튼 |
| [08](08-accessibility.md) | `08-accessibility.md` | `AutomationProperties` 바인딩 + `LiveRegion` 공지 |
| [09](09-korean-only-cleanup.md) | `09-korean-only-cleanup.md` | `qwerty-en.json` 제거, `_layoutSupportsKorean` 제거, DI 정리 |
| [10](10-tests-and-verification.md) | `10-tests-and-verification.md` | `KoreanInputModuleTests` 신규 + 수동 검증 체크리스트 |

---

## 5. 릴리스 기준 (Definition of Done)

- [ ] `dotnet build AltKey.sln` 녹색.
- [ ] `dotnet test` 녹색. 신규 `KoreanInputModuleTests` 포함.
- [ ] [docs/refactor-unif-serialized-acorn.md §8 검증 방법](../refactor-unif-serialized-acorn.md)의 수동 체크리스트 통과:
  - "해+ㅎ" "닭" 기본 조합 회귀 없음.
  - **"해+ㅆ → 해ㅆ"** (T 아님) 확인.
  - 쌍자음/쌍모음 7종(ㅃ/ㅉ/ㄸ/ㄲ/ㅆ/ㅒ/ㅖ) 전부 정상.
  - "가/A" 토글: 라벨 전환 + 입력 경로 전환 + 자동완성 사전 전환.
  - 상단바 VK_HANGUL: 작업표시줄 IME 인디케이터 토글됨, 내부 Submode 건드리지 않음.
  - 자동완성 OFF → VirtualKey 모드 → 보안앱 호환 가능.
  - Narrator로 키 포커스·Submode 전환이 올바르게 낭독됨.
- [ ] 포터블 단일 EXE 유지(`dotnet publish -r win-x64 --self-contained`).
