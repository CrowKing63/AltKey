# 레이아웃 편집기 분리 분석

## 목적

이 문서는 `LayoutEditorViewModel`과 `LayoutEditorWindow`를 `AltKey.Tools`로 옮길 때 무엇이 이미 분리되어 있고, 무엇이 아직 메인 앱에 묶여 있는지 정리한다.

## 현재 구조 요약

현재 레이아웃 편집기는 다음 구조를 가진다.

- 뷰모델: [LayoutEditorViewModel.cs](/C:/Users/UITAEK/AltKey/AltKey/ViewModels/LayoutEditorViewModel.cs)
- 창 코드비하인드: [LayoutEditorWindow.xaml.cs](/C:/Users/UITAEK/AltKey/AltKey/Views/LayoutEditorWindow.xaml.cs)
- 진입점: [SettingsViewModel.cs](/C:/Users/UITAEK/AltKey/AltKey/ViewModels/SettingsViewModel.cs)

현재 창은 `SettingsViewModel.OpenLayoutEditor()`에서 직접 생성된다.

즉, 편집기 자체는 별도 창이지만 여전히 메인 프로세스 안에서 열리고, 설정 화면 하위 기능으로 취급된다.

## 현재 결합 지점

### 1. `SettingsViewModel` 진입점 결합

현재 편집기 열기는 설정창 내부 명령으로 제공된다.

영향:

- 편집기를 독립 작업 공간으로 쓰기 어렵다.
- 메인 설정창을 거치지 않고 직접 실행하는 경로가 없다.

분리 방향:

- 메인 설정창은 `AltKey.Tools` 실행만 담당하도록 바꾼다.
- `LayoutEditorWindow` 직접 생성 책임은 제거한다.

### 2. `FocusTracker` 결합

현재 `LayoutEditorWindow`는 생성 시 `FocusTracker.Register(this)`를 호출한다.

영향:

- 편집기가 메인 앱 포커스 추적 생태계에 속한다.
- 별도 프로세스로 이동 시 이 호출은 재검토가 필요하다.

분리 방향:

- 1단계에서는 메인 앱용 `FocusTracker` 의존성을 제거하는 것이 바람직하다.
- 대신 도구 앱 내부에서 독립적인 포커스 초기화와 접근성 동작을 제공한다.

### 3. `LayoutService` 의존성

`LayoutEditorViewModel`은 `LayoutService`를 통해 레이아웃 파일을 읽고 쓴다.

이 의존성은 오히려 분리에 유리하다.

이유:

- 편집 대상이 파일 기반으로 명확하다.
- 저장 후 메인 앱이 다시 읽는 구조를 만들기 쉽다.

주의:

- `LayoutService`가 메인 앱 전용 상태나 UI 알림에 기대고 있지 않은지 확인이 필요하다.

### 4. `ConfigService` 의존성

현재 뷰모델은 `ConfigService`를 사용해 기본 레이아웃 삭제 제한 같은 정책을 판단한다.

영향:

- 편집기에도 최소한 현재 설정 읽기 능력이 필요하다.

분리 방향:

- `ConfigService` 전체를 그대로 끌고 가기보다, 필요한 최소 정보만 읽는 방식도 검토할 수 있다.
- 다만 1단계에서는 같은 설정 파일을 읽는 공용 서비스 재사용이 더 실용적일 가능성이 크다.

### 5. 메인 앱 즉시 반영 경계

레이아웃 편집 결과는 저장만으로 끝나지 않는다.
메인 앱이 새 레이아웃 목록 또는 수정된 레이아웃 내용을 다시 읽어야 한다.

분리 방향:

- 저장 성공 후 메인 앱에 `ReloadLayouts` 신호를 보낸다.
- 메인 앱은 이 신호를 받아 레이아웃 목록과 현재 적용 상태를 다시 계산한다.

## 분리 난이도 평가

`중간`

이유:

- 사용자 단어 편집기보다 구조가 단순하다.
- 데이터 경계가 파일 중심이라 명확하다.
- 하지만 진입점과 포커스 처리, 저장 후 메인 반영 경로를 정리해야 한다.

## 1단계 권장 작업 순서

1. `AltKey.Tools` 프로젝트 생성
2. 도구 호스트 창 추가
3. `LayoutEditorViewModel`과 관련 뷰 이관
4. `FocusTracker` 의존성 제거 또는 대체
5. 저장 후 `ReloadLayouts` 연동
6. 설정창의 기존 편집기 열기 경로를 도구 앱 실행으로 교체

## 접근성 관점 체크포인트

- 창이 열릴 때 첫 포커스가 예측 가능해야 한다.
- 키 리스트와 속성 편집 영역 사이 탭 순서가 자연스러워야 한다.
- 저장/삭제/불러오기 버튼 이름과 도움말이 충분히 읽혀야 한다.
- 편집 도중 메인 키보드와 포커스 충돌이 없어야 한다.

## 결론

레이아웃 편집기는 `AltKey.Tools` 1단계의 첫 분리 대상으로 적절하다.

이유는 다음과 같다.

- 파일 기반 데이터라 분리 경계가 명확하다.
- 설정 전체를 떼지 않고도 사용자 체감 개선을 만들 수 있다.
- 메인 입력 루프 핵심을 건드리지 않고 진행 가능하다.
