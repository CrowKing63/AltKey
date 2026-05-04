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

## 5.3 2단계 후보 여부 결정

2단계 후보로 결정.

- 1단계에서는 "검토 + 리스크 노출"까지 구현한다.
- 2단계에서 필요 시 독립 "프로필 매핑 편집기"로 승격한다.

## 이번 반영 코드 (검토 기능)

- `AltKey.Tools` 시작 화면에 `프로필 매핑 편집 검토 열기` 추가
- `ProfileMappingReviewWindow` 추가
  - 현재 매핑 목록/상태 표시
  - 미존재 레이아웃, 비어 있는 값 요약
  - 2단계 후보 판단 텍스트 제공
  - 접근성: 첫 포커스, 선형 탭 순서, `Esc` 닫기, AutomationProperties 이름 제공
