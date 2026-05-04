# 파일 이동/참조 변경 지도: AltKey.Tools 1단계

## 목적

이 문서는 실제 작업 시작 시 어떤 파일을 먼저 복사, 이동, 참조 변경 대상으로 봐야 하는지 정리한다.

이 단계에서는 "정확한 수정 순서"보다 "영향 범위를 놓치지 않는 것"이 더 중요하다.

## 1. 새 프로젝트에 직접 들어갈 1차 후보

### 도구 호스트

- 신규 `AltKey.Tools.csproj`
- 신규 `App.xaml`
- 신규 `App.xaml.cs`
- 신규 `MainWindow.xaml`
- 신규 `MainWindow.xaml.cs`

### 레이아웃 편집기

- [LayoutEditorWindow.xaml](/C:/Users/UITAEK/AltKey/AltKey/Views/LayoutEditorWindow.xaml)
- [LayoutEditorWindow.xaml.cs](/C:/Users/UITAEK/AltKey/AltKey/Views/LayoutEditorWindow.xaml.cs)
- [LayoutEditorViewModel.cs](/C:/Users/UITAEK/AltKey/AltKey/ViewModels/LayoutEditorViewModel.cs)

### 사용자 단어 편집기

- [UserDictionaryEditorWindow.xaml](/C:/Users/UITAEK/AltKey/AltKey/Views/UserDictionaryEditorWindow.xaml)
- [UserDictionaryEditorWindow.xaml.cs](/C:/Users/UITAEK/AltKey/AltKey/Views/UserDictionaryEditorWindow.xaml.cs)
- [UserDictionaryEditorViewModel.cs](/C:/Users/UITAEK/AltKey/AltKey/ViewModels/UserDictionaryEditorViewModel.cs)

## 2. 새 프로젝트가 참조할 가능성이 높은 기존 서비스

### 그대로 참조 또는 공용화 재사용 후보

- [LayoutService.cs](/C:/Users/UITAEK/AltKey/AltKey/Services/LayoutService.cs)
- [ConfigService.cs](/C:/Users/UITAEK/AltKey/AltKey/Services/ConfigService.cs)
- [PathResolver.cs](/C:/Users/UITAEK/AltKey/AltKey/Services/PathResolver.cs)
- [SecureStorage.cs](/C:/Users/UITAEK/AltKey/AltKey/Services/SecureStorage.cs) 필요 시

### 사용자 단어 편집기 관련

- [KoreanDictionary.cs](/C:/Users/UITAEK/AltKey/AltKey/Services/KoreanDictionary.cs)
- [EnglishDictionary.cs](/C:/Users/UITAEK/AltKey/AltKey/Services/EnglishDictionary.cs)
- [WordFrequencyStore.cs](/C:/Users/UITAEK/AltKey/AltKey/Services/WordFrequencyStore.cs)
- [BigramFrequencyStore.cs](/C:/Users/UITAEK/AltKey/AltKey/Services/BigramFrequencyStore.cs)

## 3. 참조 변경이 필요한 메인 앱 파일

### 편집기 진입점 제거 또는 대체

- [SettingsViewModel.cs](/C:/Users/UITAEK/AltKey/AltKey/ViewModels/SettingsViewModel.cs)
  - `OpenLayoutEditor()`
  - `OpenUserDictionaryEditor()`

### DI 등록 변경

- [App.xaml.cs](/C:/Users/UITAEK/AltKey/AltKey/App.xaml.cs)
  - `LayoutEditorViewModel`
  - `UserDictionaryEditorViewModel`

### 필요 시 도구 앱 실행 경로 추가

- `PathResolver` 또는 신규 실행 경로 도우미

## 4. 1단계에서 끊어야 하는 결합

### `FocusTracker`

다음 창들은 현재 메인 프로세스의 포커스 추적과 결합돼 있다.

- [LayoutEditorWindow.xaml.cs](/C:/Users/UITAEK/AltKey/AltKey/Views/LayoutEditorWindow.xaml.cs)
- [UserDictionaryEditorWindow.xaml.cs](/C:/Users/UITAEK/AltKey/AltKey/Views/UserDictionaryEditorWindow.xaml.cs)

도구 앱에서는 이 결합을 그대로 유지하지 않는 것이 원칙이다.

### 설정창 종속 진입

현재 편집기들은 설정창 하위 기능으로 열리므로, 다음 결합을 끊어야 한다.

- 설정창 명령 -> 편집기 창 직접 생성

대체 방향:

- 설정창 명령 -> `AltKey.Tools` 실행
- 필요 시 도구 이름 전달

## 5. 1단계에서 건드리지 말아야 할 파일군

이번 단계에서는 직접 수정하지 않는 것을 기본으로 한다.

- `KoreanInputModule` 관련 핵심 입력 파일
- 자동완성 핵심 제안 처리 파일
- 메인 키 입력 전송 경로
- 스위치 스캔 핵심 제어 로직

이유:

- 현재 목적은 입력 코어 재설계가 아니라 편집 도구 분리이기 때문이다.

## 6. 추천 작업 순서

1. `AltKey.Tools` 프로젝트 추가
2. 도구 호스트 창 추가
3. 레이아웃 편집기 이관
4. 메인 앱에서 레이아웃 편집기 직접 생성 경로 제거
5. `ReloadLayouts` 연결
6. 사용자 단어 편집기 이관
7. `ReloadUserDictionary`, `ReloadBigramData` 연결

## 요약

실제 작업 시작 시 가장 먼저 손댈 핵심 파일은 다음 네 곳이다.

- `AltKey.Tools.csproj` 신규
- `LayoutEditorViewModel.cs`
- `UserDictionaryEditorViewModel.cs`
- `SettingsViewModel.cs`
