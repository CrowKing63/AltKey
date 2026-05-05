# Fluent Design Rollout Phase 5 검증 기록

- 작업일: 2026-05-05
- 범위: 페이즈 5 (마감 정리/회귀 검증)

## 1. 중복 스타일 정리

- 공용 리소스 파일 [`AltKey/Themes/FluentControls.xaml`](/C:/Users/UITAEK/AltKey/AltKey/Themes/FluentControls.xaml)에 아래 스타일 키를 추가했다.
  - `FluentSecondaryButtonStyle`
  - `FluentPrimaryButtonStyle`
  - `FluentDangerButtonStyle`
  - `FluentEditorInputBoxStyle`
  - `FluentLabelTextStyle`
- 편집기 창의 로컬 중복 스타일를 공용 키 기반 별칭으로 축소했다.
  - [`AltKey/Views/LayoutEditorWindow.xaml`](/C:/Users/UITAEK/AltKey/AltKey/Views/LayoutEditorWindow.xaml)
  - [`AltKey/Views/UserDictionaryEditorWindow.xaml`](/C:/Users/UITAEK/AltKey/AltKey/Views/UserDictionaryEditorWindow.xaml)

## 2. 접근성 관점 정리

- 버튼/입력 컨트롤의 상태(배경/테두리/포커스 대비)를 창마다 따로 유지하지 않고 공용 키로 통일해,
  - 창 간 학습 비용을 줄이고
  - 포커스 이동 시 시각적 예측 가능성을 높였다.
- 기능 로직(ViewModel/IPC/저장 경계)은 수정하지 않았다.

## 3. 검증 체크

- 빌드
  - `dotnet build "C:\Users\UITAEK\AltKey\AltKey\AltKey.csproj" -v minimal`
  - `dotnet build "C:\Users\UITAEK\AltKey\AltKey.Tools\AltKey.Tools.csproj" -v minimal`
- 테스트
  - `dotnet test "C:\Users\UITAEK\AltKey\AltKey.Tests\AltKey.Tests.csproj"`
- 수동 확인 권장 항목
  - 설정 창에서 도구 실행
  - 각 창 `Esc`/닫기 버튼 동작
  - 도구 저장 후 메인 앱 반영
  - 고대비 테마 + Narrator로 라벨/버튼 이름 확인
