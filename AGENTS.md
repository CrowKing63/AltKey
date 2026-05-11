# AltKey AGENTS Guide

이 문서는 `C:\Users\UITAEK\AltKey`에서 작업하는 에이전트용 실행 지침이다. 모든 수정은 아래 우선순위와 규칙을 따른다.

## 1. 최우선 원칙

### 1.1 접근성 우선
- 모든 기능 구현·수정에서 가장 먼저 묻는다: **"이 변경이 접근성 향상에 도움이 되는가?"**
- 접근성 개선 효과가 불명확하면, 동작 추가보다 사용성·가독성·조작 가능성 개선을 우선 검토한다.

### 1.2 응답 언어
- 사용자 응답은 항상 **한국어**로 작성한다.

### 1.3 사용자 친화적 주석
- 주석은 항상 **한국어**로 작성한다. 단 영어를 표기할 때는 영어 그대로 표기한다.
- 사용자는 코딩 전문가가 아니므로, 사소한 값 조정이나 단순 함수 수정이 쉽도록 **주석을 충분히 유지·보강**한다.
- 새 코드를 만들 때뿐 아니라 **기존 코드를 수정할 때도 주석을 함께 갱신**한다.
- 주석이 없거나, 현재 동작과 맞지 않거나, 사용자가 오해하기 쉬우면 바로 수정한다.
- 주석 작성 기준은 다음 문서를 따른다.
  - `C:\Users\UITAEK\AltKey\docs\AltKey 프로젝트 주석 작성 가이드.md`

## 2. 자동완성·한글 입력 보호 규칙

### 2.1 자동완성은 최고 위험 영역
- 이 프로젝트에서 자동완성은 가장 중요하고 가장 까다로운 기능이다.
- 자동완성·한글 조합·IME 관련 코드를 수정할 때는 일반 기능보다 더 보수적으로 접근한다.
- 참고 문서:
  - `C:\Users\UITAEK\AltKey\docs\ime-korean-detect.md`

### 2.2 수정 전 최소 확인 문서
- 자동완성/한글 조합 관련 코드를 수정하기 전에 아래 문서의 **§2 "절대 건드리지 말 것"** 목록만 먼저 확인한다.
  - `C:\Users\UITAEK\AltKey\docs\auto-complet\CORE-LOGIC-PROTECTION.md`

### 2.3 과거 분석 문서 취급
- 2026-04-18 분석에서 파생된 `TASK-01~08`은 모두 해결 완료 상태다.
- 따라서 아래 문서들은 **과거 맥락 참고용 기록**으로만 취급한다.
  - `docs/auto-complet/TASK-XX-*.md`
  - `findings-overview.md`
- 사용자가 **새 버그를 명시적으로 지시하지 않는 한** 위 문서들을 다시 순회할 필요는 없다.
- 새 이슈가 확인되면 기존 번호 뒤에 이어서 새 문서를 작성한다.

## 3. 현재 아키텍처 고정 사실

### 3.1 제품 범위
- 이 프로젝트는 **한국어 사용자 전용** 가상 키보드다.
- `qwerty-en.json`은 삭제되었다.
- `Language` 필드와 `PrimaryLanguage` 개념은 제거되었다.
- 영어 입력은 한국어 레이아웃 내부의 `"가/A"` 토글, 즉 **QuietEnglish 서브모드**로 처리한다.

### 3.2 제거된 구식 진입점
- 아래 항목들은 이미 제거되었으므로 되살리거나 의존하지 않는다.
  - `HandleKoreanLayoutKey`
  - `HandleEnglishLayoutKey`
  - `HandleEnglishSubMode`
  - `_isKoreanInput`
  - `_layoutSupportsKorean`
  - `_lastImeKorean`
- 한국어 입력 로직은 `KoreanInputModule`로 이전되었다.

## 4. 빌드·테스트·쉘 규칙

### 4.1 기준 경로
- 빌드와 테스트는 항상 아래 경로를 기준으로 사용한다.
  - 메인 빌드: `C:\Users\UITAEK\AltKey\AltKey.Tools\AltKey.Tools.csproj`
    - 이 프로젝트를 빌드하면 솔루션 전체가 함께 빌드된다.
  - 테스트: `C:\Users\UITAEK\AltKey\AltKey.Tests\AltKey.Tests.csproj`

### 4.2 PowerShell 주의
- PowerShell에서는 `&&`가 기대대로 동작하지 않을 수 있으므로, 여러 명령은 `;`로 구분하거나 별도 실행한다.

### 4.3 UI 입력값 조정 규칙
- 사용자 변수 조정 기능은 **슬라이더 대신 `NumericAdjuster` 기반**으로 구현한다.

## 5. 테스트 작성 규칙

### 5.1 테스트 파일 구조
- `KoreanInputModuleTests.cs`는 더 이상 존재하지 않는다.
- 관련 테스트는 아래 4개 파일 구조를 유지한다.
  - `KoreanInputModuleTestBase.cs`
    - 공통 slots, contexts, `CreateModule` 팩토리 제공
  - `KoreanInputModuleHangulTests.cs`
    - 한글 조합, 제안, bigram 테스트
  - `KoreanInputModuleQuietEnglishTests.cs`
    - 영어 입력, 대문자, 숫자/기호 테스트
  - `KoreanInputModuleBackspaceTests.cs`
    - 한글/영어 백스페이스 테스트

### 5.2 테스트 추가 원칙
- 새 테스트는 **기능별로 맞는 파일에만** 추가한다.
- 테스트 파일 하나가 **400줄을 넘지 않게** 유지한다.
- 테스트 주석에는 **무엇을 검증하는지**만 쓴다.
- 버그 수정 전 내부 구현을 추측한 설명은 남기지 않는다.
  - 예: `"HasActiveModifiers=true이면 SendUnicode는 호출되지 않음"` 같은 과거 버그 기반 서술 금지

### 5.3 재사용 가능한 테스트 도우미
- 아래 타입은 `TestHelpers.cs`에 `public`으로 정의되어 있으므로 자유롭게 사용한다.
  - `FakeInputService`
  - `KoreanDictionaryTestable`
  - `EnglishDictionaryTestable`
  - `TestSlotFactory`

### 5.4 디버그 로그
- 디버그용 로그는 **텍스트 파일 출력 기준**으로 작성한다.

## 6. 디버깅 교훈 고정 규칙

2026-04-21 `CommitCurrentWord` 버그 대응에서 얻은 교훈을 재발 방지 규칙으로 유지한다.

- 테스트에서 재현되지 않는 버그는 가정부터 하지 말고, **실제 앱에 파일 로깅을 추가해 실행 경로를 먼저 확인**한다.
  - 당시 `AcceptSuggestion` 문제처럼 보였지만 실제 원인은 `CommitCurrentWord` 경로였다.
- `FakeInputService`는 실제 앱과 동일하지 않을 수 있다.
  - 특히 `TrackedOnScreenLength`가 자동 갱신되지 않을 수 있으므로, `SendAtomicReplace` 오버라이드 시 필요하면 `TrackedOnScreenLength = next.Length`를 반영한다.
- 상태 초기화 메서드와 상태 유지 메서드를 혼동하지 않는다.
  - 상태 초기화: `AcceptSuggestion`, `CancelComposition`, `FinalizeComposition`
  - 상태 유지: `CommitCurrentWord`
- "`Commit`은 저장이지 초기화가 아니다"라는 의미 차이를 항상 점검한다.

## 7. AltKey.Tools 분리 이후 유지보수 규칙

### 7.1 도구 창 호출 방식
- 레이아웃 편집기와 사용자 단어 편집기는 메인 앱 내부 창을 직접 여는 구조가 아니다.
- 반드시 **별도 프로세스 `AltKey.Tools` 실행 구조**를 유지한다.
- `SettingsViewModel`에서 `LayoutEditorWindow`, `UserDictionaryEditorWindow`를 직접 다시 띄우는 방식으로 되돌리지 않는다.

### 7.2 수정 시 함께 볼 경계
- 편집 기능을 수정할 때는 메인 앱만 보지 말고 **`AltKey.Tools`와 공용 ViewModel/Service 경계**를 함께 본다.
- 특히 아래 경계는 메인 앱 전용 상태 의존성을 다시 주입하지 않도록 주의한다.
  - `ILayoutRepository`
  - `IUserDictionaryRepository`
  - `LayoutRepository`
  - `UserDictionaryRepository`

### 7.3 메인 앱 반영 방식
- `AltKey.Tools` 저장 후 메인 앱 반영은 **데이터 본문 IPC가 아니라 최소 재로드 신호**로 처리한다.
- 관련 변경 시 먼저 아래 경로를 확인한다.
  - `ToolsReloadSignalService`
  - `NotifyReloadLayouts`
  - `NotifyReloadUserDictionary`
  - `NotifyReloadBigramData`
- 같은 성격의 새 도구를 추가해도 이 원칙을 우선 유지한다.

### 7.4 레이아웃 외부 변경 반영
- 외부 변경 반영은 단순 캐시 무효화만으로 끝내지 않는다.
- **목록/UI 재계산까지 포함한 갱신**이 필요하다.
- `LayoutService.InvalidateCache()`만 단독 호출하고 끝내지 말고, 현재 외부 변경 반영 경로인 `NotifyExternalLayoutsChanged`와 같은 수준으로 처리한다.

### 7.5 Tools 실행 파일 탐색 규칙
- `AltKey.Tools.exe`는 아래 순서로 찾는다.
  1. 같은 폴더
  2. `Tools` 하위 폴더
  3. 개발용 `AltKey.Tools\bin\{Configuration}\{TFM}`
- 이 규칙을 바꾸면 아래 항목도 항상 함께 수정한다.
  - `PathResolver.ToolsExePath`
  - 릴리스 워크플로
  - 인스톨러 스크립트

### 7.6 배포 수정 시 필수 확인
- 메인 앱만 publish하고 끝내지 않는다.
- 아래 두 경로에서 `AltKey.Tools` 포함 규칙이 유지되는지 반드시 확인한다.
  - `.github/workflows/release.yml`의 `AltKey.Tools` publish 단계
  - `installer/AltKey.iss`의 `Tools` 폴더 포함 규칙

### 7.7 프로필 매핑 기능 상태
- 프로필 매핑 편집은 아직 **완료 기능이 아니라 2단계 후보 검토 상태**다.
- 현재 반영 범위:
  - `docs/altkey-tools/profile-mapping-review.md`
  - `AltKey.Tools`의 검토 창
- 사용자가 명시적으로 요구하지 않는 한, 기존 런타임 적용 책임인 `MainViewModel`/`ProfileService`를 도구 앱으로 옮기지 않는다.

### 7.8 Tools 관련 변경 후 권장 확인 4종
- 가능하면 아래 4가지를 함께 확인한다.
  - 메인 앱 빌드
  - `AltKey.Tools` 빌드
  - 설정 창에서 도구 실행 여부
  - 저장 후 메인 앱 반영 여부


## 8. 텍스트 수정·인코딩 안전 수칙

### 8.1 기본 원칙
- 문자열 리터럴의 인코딩과 공백을 엄격히 보존한다.
- 치환 오류를 줄이기 위해 가능하면 **라인 전체 교체** 방식으로 수정한다.

### 8.2 이번 인코딩 사고의 핵심 교훈
- 1차 실패:
  - PowerShell `Get-Content -Raw` + 정규식/문자열 치환 + `Set-Content`로 파일 전체를 다시 저장하면서 한글과 인코딩 상태가 손상되었다.
- 2차 성공:
  - Git 원복 후 `apply_patch`로 **필요한 최소 구간만 수정**하여 파일 전체 재직렬화를 피했고, 한글 손상을 막았다.

### 8.3 금지
- 한글이 포함된 `C#`, `XAML`, `MD` 파일을 `Get-Content -Raw` 후 `Set-Content`로 전체 재저장하는 방식
- 인코딩 상태가 확실하지 않은 파일에 정규식 일괄 치환을 적용해 전체를 다시 쓰는 방식

### 8.4 권장
- 텍스트 수정은 기본적으로 `apply_patch`를 사용한다.
- 인코딩 이상이 보이면 기능 수정 전에 먼저 원복을 검토한 뒤 다시 작업한다.
  - 예: `git checkout -- <파일>`
- 스크립트 저장이 꼭 필요하면, 저장 전후로 한글 주석·문자열 샘플 라인을 직접 확인하고 즉시 빌드로 검증한다.

### 8.5 필수 검증 체크리스트
- 변경 파일에서 한글 주석과 문자열이 정상 표시되는지 확인
- 해당 프로젝트 빌드 성공 확인
- 이상 징후가 보이면 추가 작업을 중단하고 즉시 원복 후 `apply_patch` 방식으로 다시 적용

## 9. 작업 판단용 빠른 체크리스트

작업 전 또는 수정 중 아래를 빠르게 점검한다.

1. 이 변경이 접근성 향상에 실제로 도움이 되는가?
2. 자동완성/한글 조합/IME 핵심 보호 영역을 건드리는가?
3. 주석을 함께 갱신했는가?
4. `AltKey.Tools` 분리 아키텍처를 거스르지 않는가?
5. 테스트 위치와 파일 길이 규칙을 지켰는가?
6. 한글 인코딩을 안전하게 보존하는 방식으로 수정했는가?
7. 필요한 빌드/테스트/도구 실행 검증을 했는가?
8. 위키 문서를 함께 갱신했는가? (`skills/wiki-update.md` 참조)
