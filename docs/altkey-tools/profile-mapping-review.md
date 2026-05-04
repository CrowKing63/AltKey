# 태스크 5 구현 결과: 프로필 매핑 편집 검토

## 목적

이 문서는 `docs/altkey-tools/tasks.md`의 5번 항목(프로필 매핑 편집 검토)을 실제 코드 기준으로 완료 처리하기 위한 검토 결과다.

## 5.1 현재 구조 분석

- 설정 원본: `AppConfig.Profiles` (`Dictionary<string, string>`)
  - `Key`: 프로세스 이름(소문자, 예: `notepad.exe`)
  - `Value`: 레이아웃 이름
- 편집 위치: `SettingsViewModel`의 `Profiles`/`AddProfile`/`RemoveProfile`/`SaveProfiles`
- 런타임 적용 위치: `MainViewModel.OnForegroundAppChanged`
  - `ProfileService`가 활성 프로세스 이름을 전달하면, `config.Profiles` 조회 후 `SwitchLayout(layoutName)` 호출

## 5.2 별도 도구 적합성 판단

적합함.

- 데이터 구조가 단순 문자열 매핑이라 편집 UI를 별도 프로세스로 분리하기 쉽다.
- 저장 후 메인 앱이 재로드 또는 기존 config 변경 감지로 반영하면 충분하다.
- 매핑 데이터 본문을 IPC로 주고받을 필요가 없어 1단계 최소 연동 원칙과 충돌하지 않는다.

주의점:

- 실제 전환 실행(`SwitchLayout`)은 메인 런타임 책임이므로 도구 앱으로 옮기면 안 된다.
- 매핑 값 검증(존재하는 레이아웃인지 확인)과 접근성(첫 포커스/탭 순서/Esc 닫기)을 도구 쪽에서 보강해야 한다.

## 5.3 독립 "프로필 매핑 편집기"로 승격 (2026-05-04 반영)

승격 완료.

- `AltKey.Tools`에 `ProfileMappingEditorWindow`를 추가해 프로필 매핑을 독립 창에서 편집/저장할 수 있게 했다.
- 검증 규칙(빈 프로세스, 중복 프로세스, 빈 레이아웃, 미존재 레이아웃)을 행 상태와 요약으로 즉시 노출한다.
- 저장 시 유효한 행만 `AppConfig.Profiles`에 반영하고, `ToolsReloadSignalService.NotifyReloadProfiles()`로 메인 앱 설정을 재로드한다.
- 메인 앱 `SettingsWindow`의 인라인 프로필 매핑 편집 UI는 제거하고, `OpenProfileMappingEditorCommand`로 도구 앱을 열도록 변경했다.
- `AltKey.Tools` 시작 화면과 `--tool` 인자에 `profile`을 추가해 직접 진입을 지원한다.

## 이번 반영 코드 (독립 편집기)

- `AltKey.Tools` 시작 화면에 `프로필 매핑 편집기 열기` 추가
- `ProfileMappingEditorWindow` 추가
  - 현재 매핑 편집(추가/삭제/저장)
  - 즉시 상태 검증(중복/공백/미존재 레이아웃)
  - 접근성: 첫 포커스, 선형 탭 순서, `Esc` 닫기, AutomationProperties 이름 제공
