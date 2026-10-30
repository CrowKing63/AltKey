# AltKey AGENTS Guide

이 문서는 `C:\Users\UITAEK\AltKey`에서 작업하는 에이전트용 실행 지침이다. 모든 수정은 아래 규칙을 따른다.

## 1. 최우선 원칙

### 1.1 접근성 우선
- 모든 기능 구현·수정에서 가장 먼저 묻는다: **"이 변경이 접근성 향상에 도움이 되는가?"**
- 접근성 개선 효과가 불명확하면, 동작 추가보다 사용성·가독성·조작 가능성 개선을 우선 검토한다.

### 1.2 사용자 친화적 주석
- 주석은 항상 **한국어**로 작성한다(영어 표기는 영어 그대로).
- 사용자는 코딩 전문가가 아니므로, 사소한 값 조정이나 단순 함수 수정이 쉽도록 **주석을 충분히 유지·보강**한다.
- 새 코드뿐 아니라 **기존 코드 수정 시에도 주석을 함께 갱신**한다.
- 주석 작성 기준: `docs/AltKey 프로젝트 주석 작성 가이드.md`

## 2. 자동완성·한글 입력 보호
- 자동완성/한글 조합 관련 코드를 수정하기 전에 `docs/auto-complet/CORE-LOGIC-PROTECTION.md`의 **§2 "절대 건드리지 말 것"** 목록만 먼저 확인한다. 이 문서가 해당 영역의 권위 있는 지침이다.
- 한글 음절의 유니코드 코드포인트를 **수동 계산하지 않는다.** 항상 `tools/hangul/hangul_calc.py`를 실행하여 ground truth를 확보한다 (스킬: `hangul-composition-calc`).

## 3. 제품 범위
- 이 프로젝트는 **한국어 사용자 전용** 가상 키보드다.
- 영어 입력은 한국어 레이아웃 내부의 `"가/A"` 토글, 즉 **QuietEnglish 서브모드**로 처리한다.

## 4. 빌드·테스트 규칙
- 메인 빌드: `AltKey.Tools\AltKey.Tools.csproj` (이 프로젝트를 빌드하면 솔루션 전체가 함께 빌드된다)
- 테스트: `AltKey.Tests\AltKey.Tests.csproj`
- 사용자 변수 조정 기능은 **슬라이더 대신 `NumericAdjuster` 기반**으로 구현한다.

## 5. 테스트 작성 규칙
- 테스트는 **기능별로 폴더/파일 분리**한다(예: `InputLanguage/`, `Services/`). 한 파일은 **400줄 이하**.
- 새 테스트는 **기능별로 맞는 파일에만** 추가한다.
- 테스트 주석에는 **무엇을 검증하는지**만 쓴다. 내부 구현을 추측한 서술이나 과거 버그 기반 설명은 남기지 않는다.
- 공통 slots·contexts·팩토리는 `KoreanInputModuleTestBase.cs`에, 재사용 도우미(`FakeInputService`, `KoreanDictionaryTestable`, `EnglishDictionaryTestable`, `TestSlotFactory`)는 `TestHelpers.cs`의 `public` 타입으로 제공된다.

## 6. AltKey.Tools 분리 이후 유지보수 규칙

### 6.1 도구 창 호출 방식
- 레이아웃 편집기·사용자 단어 편집기는 메인 앱 내부 창이 아니라 **별도 프로세스 `AltKey.Tools`**로 실행한다.
- `SettingsViewModel`에서 `LayoutEditorWindow`, `UserDictionaryEditorWindow`를 직접 띄우는 방식으로 되돌리지 않는다.

### 6.2 수정 시 함께 볼 경계
- 편집 기능 수정 시 메인 앱뿐 아니라 **`AltKey.Tools`와 공용 ViewModel/Service 경계**를 함께 본다.
- `ILayoutRepository`, `IUserDictionaryRepository`, `LayoutRepository`, `UserDictionaryRepository`에 메인 앱 전용 상태 의존성을 다시 주입하지 않는다.

### 6.3 메인 앱 반영 방식
- `AltKey.Tools` 저장 후 메인 앱 반영은 **데이터 본문 IPC가 아니라 최소 재로드 신호**로 처리한다.
- 관련 경로: `ToolsReloadSignalService`, `NotifyReloadLayouts`, `NotifyReloadUserDictionary`, `NotifyReloadBigramData`.

### 6.4 레이아웃 외부 변경 반영
- 외부 변경 반영은 단순 캐시 무효화만으론 부족하다. **목록/UI 재계산까지 포함한 갱신**이 필요하다.
- `LayoutService.InvalidateCache()`만 단독 호출하지 말고, 현재 경로인 `NotifyExternalLayoutsChanged` 수준으로 처리한다.

### 6.5 Tools 실행 파일 탐색 규칙
- `AltKey.Tools.exe`는 순서대로 찾는다: ① 같은 폴더 ② `Tools` 하위 폴더 ③ 개발용 `AltKey.Tools\bin\{Configuration}\{TFM}`.
- 이 규칙을 바꾸면 `PathResolver.ToolsExePath`, 릴리스 워크플로, 인스톨러 스크립트를 함께 수정한다.

### 6.6 배포 수정 시 필수 확인
- 메인 앱만 publish하고 끝내지 않는다. `.github/workflows/release.yml`의 `AltKey.Tools` publish 단계와 `installer/AltKey.iss`의 `Tools` 폴더 포함 규칙이 유지되는지 확인한다.

### 6.7 프로필 매핑 기능 상태
- 프로필 매핑 편집은 **완료 기능이 아니라 2단계 후보 검토 상태**다(`docs/altkey-tools/profile-mapping-review.md`, `AltKey.Tools` 검토 창).
- 사용자가 명시적으로 요구하지 않는 한, 런타임 적용 책임인 `MainViewModel`/`ProfileService`를 도구 앱으로 옮기지 않는다.

### 6.8 Tools 변경 후 권장 확인
- 가능하면 함께 확인: 메인 앱 빌드 / `AltKey.Tools` 빌드 / 설정 창에서 도구 실행 여부 / 저장 후 메인 앱 반영 여부.

## 7. 작업 판단용 빠른 체크리스트
작업 전·중 아래를 빠르게 점검한다.

1. 접근성 향상에 실제로 도움이 되는가?
2. 자동완성/한글 조합/IME 핵심 보호 영역을 건드리는가? (건드리면 §2 문서 확인)
3. 주석을 함께 갱신했는가?
4. `AltKey.Tools` 분리 아키텍처를 거스르지 않는가?
5. 테스트 위치·파일 길이 규칙을 지켰는가?
6. 필요한 빌드/테스트/도구 실행 검증을 했는가?
