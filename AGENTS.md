- 모든 기능의 구현과 수정에 대해 '이것이 접근성 향상에 도움이 되는가'를 최우선으로 고려할 것. 
- 사용자 요청에 대한 응답은 한국어로 작성할 것.
- 사용자는 코딩 전문가가 아님. 접근성 측면에서, 사용자가 사소한 수정(하드코딩된 변수값처럼 의존성이 없어 수정에 제약이 없는 요소들 또는 함수들)을 하기 쉽게 주석 작업에 충실할 것. 코드를 새로 생성할 때뿐 아니라 수정하는 경우에도 주석을 업데이트할 것. 혹시 작업 중에 주석이 없거나 틀린 곳이 있다면 추가, 수정한다. C:\Users\UITAEK\AltKey\docs\AltKey 프로젝트 주석 작성 가이드.md
- 이 프로젝트에서 가장 중요한 기능인 자동 완성은 구현 자체가 너무나 까다로웠다. 관련 코드 수정시 각별한 주의가 필요하다. 다음의 문서를 참고할 것. C:\Users\UITAEK\AltKey\docs\ime-korean-detect.md
- **자동완성/한글 조합 관련 코드를 수정하기 전에 [`docs/auto-complet/CORE-LOGIC-PROTECTION.md`](docs/auto-complet/CORE-LOGIC-PROTECTION.md) §2 "절대 건드리지 말 것" 목록만 확인하면 된다.** 2026-04-18 분석에서 파생된 TASK-01~08은 2026-04-18에 모두 해결 완료되었으므로 `docs/auto-complet/TASK-XX-*.md`와 `findings-overview.md`는 과거 맥락 참고용 기록일 뿐이며, 사용자가 명시적으로 새 버그를 지시하지 않는 한 다시 순회할 필요 없다. 새 이슈가 발견되면 번호를 이어 붙여 작성한다.
- 이 프로젝트는 **한국어 사용자 전용** 가상 키보드이다. `qwerty-en.json`은 삭제되었으며, `Language` 필드와 `PrimaryLanguage` 개념은 제거되었다. 영어 입력은 한국어 레이아웃 내 "가/A" 토글(QuietEnglish 서브모드)로 처리된다.
- 기존 `HandleKoreanLayoutKey`, `HandleEnglishLayoutKey`, `HandleEnglishSubMode`, `_isKoreanInput`, `_layoutSupportsKorean`, `_lastImeKorean`은 모두 제거되었다. 한국어 입력 로직은 `KoreanInputModule`으로 이전되었다.
- **프로젝트 빌드 및 테스트 시 다음 경로를 기준으로 작업을 수행할 것.** (경로를 찾지 못하는 문제를 방지하기 위함)
  - 메인 프로젝트(빌드): `C:\Users\UITAEK\AltKey\AltKey\AltKey.csproj`
  - 테스트 프로젝트: `C:\Users\UITAEK\AltKey\AltKey.Tests\AltKey.Tests.csproj`
- **PowerShell 환경에서는 `&&` 연산자가 작동하지 않으므로, 여러 명령어를 실행할 때는 `;`를 사용하거나 각 명령어를 별도로 실행할 것.**
- 사용자 변수 조정 기능에는 슬라이더 대신 NumericAdjuster 기반으로 구현할 것.
- **테스트 파일 구조**: `KoreanInputModuleTests.cs`는 분할되어 더 이상 존재하지 않는다. 테스트는 다음 4개 파일로 구성되어 있다.
  - `KoreanInputModuleTestBase.cs` — 공통 slots, contexts, `CreateModule` 팩토리 (상속용)
  - `KoreanInputModuleHangulTests.cs` — 한글 조합·제안·bigram 테스트
  - `KoreanInputModuleQuietEnglishTests.cs` — 영어 입력·대문자·숫자/기호 테스트
  - `KoreanInputModuleBackspaceTests.cs` — 한글/영어 백스페이스 테스트
- **테스트 작성 시 주의사항**:
  - 새 테스트는 기능별로 알맞은 파일에 추가할 것. 한 파일도 400줄을 넘지 않게 유지한다.
  - 과거 버그 기반 주석(예: "HasActiveModifiers=true이면 SendUnicode는 호출되지 않음")은 버그 수정 후 잘못된 정보가 되므로, 테스트 주석에는 **무엇을 검증하는지**만 적고 구현 내부 동작 추측은 적지 않는다.
  - `FakeInputService`, `KoreanDictionaryTestable`, `EnglishDictionaryTestable`, `TestSlotFactory`는 `public`이며 `TestHelpers.cs`에 정의되어 있다. 새 테스트에서 자유롭게 사용할 수 있다.
  - 디버그를 위한 로그는 텍스트 파일로 출력되도록 작성한다.
- **버그 디버깅 교훈 (2026-04-21 CommitCurrentWord 버그)**:
  - 테스트에서 재현 안 되는 버그는 실제 앱에 파일 로깅을 추가해 어떤 코드 경로가 실행되는지 먼저 확인할 것. 이번엔 AcceptSuggestion 버그라 가정했으나 실제로는 CommitCurrentWord 경로였음.
  - `FakeInputService`는 `TrackedOnScreenLength`를 갱신하지 않아 실제 앱과 다른 동작을 보일 수 있음. `SendAtomicReplace` 오버라이드 시 `TrackedOnScreenLength = next.Length` 추가로 해결함.
  - 상태를 리셋하는 메서드(`AcceptSuggestion`, `CancelComposition`, `FinalizeComposition`)와 상태를 유지하는 메서드(`CommitCurrentWord`)의 차이를 명확히 할 것. 이번 버그는 "저장" 의미의 CommitCurrentWord가 composer를 리셋하지 않아 발생.
  
- **AltKey.Tools 분리 이후 유지보수 규칙 (2026-05-04 반영)**:
  - 레이아웃 편집기와 사용자 단어 편집기는 이제 메인 앱 내부 창을 직접 여는 구조가 아니라 **별도 프로세스 `AltKey.Tools`를 실행하는 구조**다. 관련 진입점 수정 시 `SettingsViewModel`에서 `LayoutEditorWindow`, `UserDictionaryEditorWindow`를 직접 다시 띄우는 방식으로 되돌리지 말 것.
  - 편집 기능을 수정할 때는 메인 앱 코드만 보지 말고 **`AltKey.Tools` 프로젝트와 공용 ViewModel/Service 경계**를 함께 확인할 것. 특히 `ILayoutRepository`, `IUserDictionaryRepository`, `LayoutRepository`, `UserDictionaryRepository`는 도구 앱 분리 전제를 가진 경계이므로, 메인 앱 전용 상태 의존성을 다시 주입하지 않도록 주의할 것.
  - `AltKey.Tools`에서 데이터 저장 후 메인 앱 반영은 **데이터 본문 IPC가 아니라 최소 재로드 신호**로 동작한다. 레이아웃/사용자 단어/bigram 반영을 건드릴 때는 `ToolsReloadSignalService`, `NotifyReloadLayouts`, `NotifyReloadUserDictionary`, `NotifyReloadBigramData` 경로를 먼저 확인하고, 같은 성격의 새 도구를 추가해도 이 원칙을 우선 유지할 것.
  - 레이아웃 외부 변경 반영은 단순 캐시 무효화만으로 끝나지 않는다. **목록/UI 재계산까지 포함한 갱신**이 필요하므로 `LayoutService.InvalidateCache()`만 직접 호출해 끝내지 말고 현재 외부 변경 반영 경로(`NotifyExternalLayoutsChanged`)와 같은 수준으로 처리할 것.
  - `AltKey.Tools.exe` 경로는 **같은 폴더 -> `Tools` 하위 폴더 -> 개발용 `AltKey.Tools\\bin\\{Configuration}\\{TFM}`** 순서로 찾는다. 실행 파일 탐색 규칙을 바꾸면 `PathResolver.ToolsExePath`, 릴리스 워크플로, 인스톨러 스크립트를 항상 함께 수정할 것.
  - 배포/릴리스 수정 시에는 메인 앱만 publish하면 끝나지 않는다. **`.github/workflows/release.yml`의 `AltKey.Tools` publish 단계와 `installer/AltKey.iss`의 `Tools` 폴더 포함 규칙**이 유지되는지 반드시 확인할 것.
  - 프로필 매핑 편집은 아직 **구현 완료 기능이 아니라 2단계 후보 검토 상태**다. 현재는 `docs/altkey-tools/profile-mapping-review.md`와 `AltKey.Tools`의 검토 창만 반영되어 있으므로, 별도 편집기로 승격하라는 명시 요청이 없으면 기존 런타임 적용 책임(`MainViewModel`/`ProfileService`)을 도구 앱으로 옮기지 말 것.
  - `AltKey.Tools` 관련 변경 후에는 가능하면 다음 네 가지를 함께 확인할 것: **메인 앱 빌드, `AltKey.Tools` 빌드, 설정 창에서 도구 실행 여부, 저장 후 메인 앱 반영 여부**.

- 수정 시 문자열 리터럴의 인코딩과 공백을 엄격히 준수하고, 치환 오류 방지를 위해 가급적 라인 전체를 교체

## 인코딩 복구/수정 작업 규칙 (재발 방지)

- **이번 이슈의 차이점(반드시 기억):**
  - 1차 작업(문자 깨짐 발생): PowerShell `Get-Content -Raw` + 정규식/문자열 치환 + `Set-Content` 저장으로 파일 전체를 다시 직렬화하면서, 기존 파일의 한글/인코딩 상태를 보존하지 못해 주석과 문자열이 대량 손상됨.
  - 2차 작업(정상 반영): Git 원복 후 `apply_patch`로 **필요한 최소 구간만** 수정하여, 파일 전체 재인코딩을 피하고 한글 손상을 방지함.

- **금지:**
  - 한글이 포함된 C# / XAML / MD 파일에 대해 `Get-Content -Raw` 후 `Set-Content`로 전체 재저장하는 방식.
  - 정규식 일괄 치환으로 파일 전체를 다시 쓰는 방식(특히 인코딩 미확정 상태).

- **권장:**
  - 텍스트 수정은 기본적으로 `apply_patch` 사용(최소 라인 단위).
  - 인코딩 이상이 보이면 기능 수정 전에 먼저 `git checkout -- <파일>`로 원복 후 재작업.
  - 불가피하게 스크립트 저장이 필요하면, 사전/사후에 한글 주석/문자열 샘플 라인을 직접 확인하고 즉시 빌드로 검증.

- **검증 체크리스트(필수):**
  - 변경 파일에서 한글 주석/문자열이 정상 표시되는지 확인.
  - 해당 프로젝트 빌드(`AltKey.csproj`) 성공 확인.
  - 이상 징후 발견 시 추가 작업 중단 후 즉시 원복하고 `apply_patch` 방식으로 다시 적용.
