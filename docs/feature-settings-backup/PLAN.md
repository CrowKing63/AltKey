# 사용자 설정 백업·복원·초기화 계획

상태: **구현 완료** (2026-11-16) — 포터블 모드 초기화 안전성 수정 포함 (§3.3 참고)
위치: 설정창 → 고급 탭, "사용자 설정 데이터 폴더 열기" 버튼 아래 새 섹션

## 1. 배경 (현재 상태)

모든 사용자 데이터는 `PathResolver.DataDir` 한 폴더에 모인다(포터블 = exe 폴더, 설치판 = `%AppData%\AltKey`).

| 항목 | 위치 | 내용 |
|---|---|---|
| `config.json` | DataDir | 모든 앱 설정 (창 레이아웃, 자동완성, 헤더 버튼, AI 등) |
| `layouts/` | DataDir/layouts | 레이아웃 전부. 내장(Basic/Basic Plus)은 시작 시 exe에서 자동 시드, 나머지는 사용자 커스텀 |
| `user-words.ko.json` / `.en.json` | DataDir | 사용자 단어 사전(빈도 포함) |
| `user-bigrams.ko.json` | DataDir | 바이그램 빈도 통계 |
| `scratchpad.json` | DataDir | 빠른 메모 내용 |
| `clipboard_history.json` | DataDir | 클립보드 히스토리 |
| `engine/`, `models/`, `manifest.json` | DataDir | 로컬 AI 엔진·모델 (수백 MB 다운로드물) |

참고:
- `config.json`이 없으면 시작 시 기본값으로 생성됨 (`ConfigService.cs:40`).
- 내장 레이아웃은 `SeedBuiltinLayouts()`가 없는 파일만 복사하므로(`LayoutService.cs:47`), 폴더를 통째로 지워도 다음 시작에 내장은 되돌아오고 커스텀만 사라진다.
- 확인 팝업의 Yes/No MessageBox 패턴이 이미 존재(`SettingsViewModel.cs:1609`, `UserDictionaryEditorViewModel`).
- 앱 재시작 메커니즘: 새 프로세스 시작 → `ShutdownCurrentApp()` (기존 `ResetWindowLayout` 방식).

## 2. 목표

고급 탭에 버튼 3개 추가:

1. **사용자 설정 백업** — 사용자 데이터를 단일 zip 파일로 저장
2. **사용자 설정 복원** — zip 파일을 선택하면 확인 후 덮어쓰기 복원
3. **사용자 설정 초기화** — "처음 설치한 상태"로 리셋 (경고 팝업 필수)

## 3. 설계

### 3.1 백업 형식 (질문: 어떤 식으로 뽑나?)

**단일 zip 파일.** 예: `AltKey-backup-20261116-1530.zip`

포함:
- `config.json`
- `layouts/` 전체(내장 수정본 + 커스텀)
- `user-words.*.json`, `user-bigrams.*.json`
- `scratchpad.json`, `clipboard_history.json` (사용자 결정: 포함)

제외:
- `engine/`, `models/`, `manifest.json` — 수백 MB라 zip 용량만 불리고, 설정 화면에서 다시 다운로드 가능. 제외가 맞다.

구현: `System.IO.Compression.ZipFile`(BCL 내장, 새 의존성 0). 저장 위치는 SaveFileDialog로 사용자가 정한다(기본명 자동 제안).

### 3.2 복원 흐름 (질문: 압축 파일을 넣으면 자동으로 되나?)

**자동 복원은 하지 않는다.** 덮어쓰기는 되돌릴 수 없으니 명시적 2단계:

1. "사용자 설정 복원" 클릭 → **파일 선택 대화상자**(.zip)
2. zip 검증(아래) → **확인 팝업**: 어떤 항목이 덮어써지는지 나열 + 앱 재시작 안내 → 예/아니오
3. 예 → DataDir에 내용 복사(존재하는 파일 덮어쓰기, layouts 폴더 병합) → **앱 재시작**(새 프로세스 시작 후 종료 — 실행 중 메모리에 올라온 설정·사전을 무효화하려면 재시작이 가장 안전)

검증 규칙:
- `.zip`가 아니면 거부 ("지원하지 않는 파일 형식입니다")
- zip 안에 `config.json` 또는 `layouts/`가 없으면 거부 ("AltKey 백업 파일이 아닙니다")
- 손상된 zip → 오류 메시지, 중단
- 검증 통과 전까지 어떤 파일도 건드리지 않는다 (검증 → 확인 → 쓰기 순서)

### 3.3 초기화 흐름

1. "사용자 설정 초기화" 클릭 → **경고 팝업** (Yes/No):
   - 삭제될 항목 목록(설정, 커스텀 레이아웃, 사용자 단어, 바이그램 통계, 빠른 메모, 클립보드 히스토리)
   - 내장 레이아웃은 자동 복원됨 / 로컬 AI 엔진·모델은 유지됨을 명시
   - "백업부터 하는 것을 권장합니다" 안내
2. 예 → 파일 삭제 → **앱 재시작** (삭제 전 자동 백업은 하지 않음 — 팝업에서 "백업부터 하는 것을 권장" 안내만)
3. 재시작 후: config 기본값, 내장 레이아웃 시드, 사용자 데이터는 없음 = 처음 설치 상태

**삭제 대상**: `layouts/`의 **커스텀 파일만**, `user-words.*.json`, `user-bigrams.*.json`, `scratchpad.json`, `clipboard_history.json`.
**유지**: `engine/`, `models/`, `manifest.json`.

**[포터블 모드 안전성] (2026-11-16 수정)**: 포터블에서는 DataDir이 exe 폴더라,
초기화 대상에 내장 리소스가 섞여 있었습니다 — 그대로 삭제하면 다음 시작 시 데이터가 깨졌습니다.

| 항목 | 문제 | 해결 |
|---|---|---|
| `config.json` | 파일 자체가 **포터블 판별 마커**(`PathResolver.IsPortablePath`) — 지우면 재시작 시 설치판(%AppData%)으로 오인, 데이터 폴더가 바뀌어 "레이아웃 파일 없음" 오류 | 파일을 지우지 않고 **내용만 기본값으로 덮어씀** (`ConfigService.ResetToDefaults()`) |
| `layouts/Basic.json`, `Basic Plus.json` | 포터블에서 이 파일들이 **시드 소스 그 자체** — 폴더를 통째로 지우면 다음 시작에 내장 레이아웃이 영구히 사라짐 | 초기화·복원 모두 **커스텀만 삭제**, 내장은 보존 (내장은 앱 리소스라 "사용자 데이터"가 아님) |

### 3.4 UI (고급 탭)

"사용자 설정 데이터 폴더 열기" 버튼 아래 새 섹션:

```
[설정 백업과 복원]  ← SectionTitleStyle
─────────────────────
[사용자 설정 백업]  [사용자 설정 복원]
[사용자 설정 초기화]  (위험 동작 스타일)
```

- TabIndex는 기존 80 이어서 81~83, 모든 버튼에 `AutomationProperties.Name` + `ToolTip`
- "초기화" 버튼은 경고 색(AdminWarning 계열 브러시 재활용)으로 시각적 구분

### 3.5 코드 구조

- **`SettingsBackupService`(신규)**: zip 생성/복원/검증만 담당하는 순수 파일 서비스 → ViewModel에서 분리해 단위 테스트 가능
  - `CreateBackup(string destZipPath)`
  - `RestoreFromZip(string zipPath)` (검증 포함)
  - `ResetUserSettings()`
  - `static bool LooksLikeAltKeyBackup(string zipPath)` (검증용)
- **`SettingsViewModel`**: `BackupUserSettingsCommand`, `RestoreUserSettingsCommand`, `ResetUserSettingsCommand` — 확인 팝업과 재시작만 담당
- XAML: 버튼 3개 + 섹션 타이틀

## 4. 수정 파일

| 파일 | 변경 |
|---|---|
| `AltKey/Services/SettingsBackupService.cs` | 신규 — zip 백업/복원/초기화 로직 |
| `AltKey/ViewModels/SettingsViewModel.cs` | 3개 커맨드 + 확인 팝업 + 재시작 호출 |
| `AltKey/Views/SettingsWindow.xaml` | 고급 탭 버튼 3개 (TabIndex 81~83) |
| `AltKey.Tests/Services/SettingsBackupServiceTests.cs` | 신규 — 기능별 폴더 규칙 준수, 400줄 이하 |

DI 등록: `App.xaml.cs`에 `SettingsBackupService` 싱글턴 등록 (WordFrequencyStore 팩토리 옆).

## 5. 테스트 계획 (자동)

1. **round-trip**: 임시 DataDir에 파일 생성 → CreateBackup → 원본 삭제 → RestoreFromZip → 내용 일치
2. **검증 실패 케이스**: 비-zip 파일 / config·layouts 없는 zip / 손상된 zip → 예외 또는 거부, 기존 파일 무변경
3. **초기화**: 파일 목록 삭제 대상이 정확히 설정된 목록뿐인지 (engine/models 유지)
4. **layouts 병합 복원**: 백업에 없는 커스텀 레이아웃은 그대로 남아야 하는지 vs 통째로 교체 — 기본안은 **백업 내용으로 layouts 폴더를 교체**(복원은 "그 시점의 상태"를 그대로 되돌리는 것). 테스트로 고정

## 6. 수동 검증 체크리스트 (사용자 직접)

1. 설정 몇 개 변경 + 사용자 단어 몇 개 추가 → [백업] → zip 열어보기: config.json·layouts·user-words가 들어있나
2. 다시 설정 변경 → [복원] → zip 선택 → 확인 팝업에서 항목 나열 확인 → 예 → 재시작 후 이전 상태로 복귀
3. [초기화] → 경고 팝업(삭제 목록, 백업 권장 안내) → 예 → 재시작 후: 자동완성 꺼짐? 설정 기본값, 내장 레이아웃(Basic/Basic Plus) 존재, 사용자 단어 없음, 로컬 AI 엔진·모델은 그대로
4. 복원에서 텍스트 파일이나 빈 zip 선택 → 오류 메시지, 아무것도 안 지워져야 함

## 7. 접근성 점검 (§1 원칙)

- 버튼 전부 `AutomationProperties.Name` + ToolTip 제공
- 경고 팝업 메시지에 삭제 대상 목록을 **텍스트로** 명시 (색상만으로 구분하지 않음)
- TabIndex 연속 유지 (키보드 순환 이동 깨지지 않음)

## 8. 확정 결정 (2026-11-16 사용자 확인)

1. **클립보드 히스토리·빠른 메모**: 백업/초기화 둘 다 **포함** — "처음 설치 상태"와 일치.
2. **초기화 전 자동 백업**: **생성 안 함** — 경고 팝업에서 "백업부터 하는 것을 권장합니다" 안내만.
3. **드래그 앤 드롭 복원**: **미지원** — 버튼 → 파일 선택 대화상자 방식만.

구현 범위: 위 4개 파일 + `App.xaml.cs` DI 등록. 수동 검증은 §6 체크리스트대로 사용자 디버그 빌드로 수행 (커밋은 검증 전까지 보류).
