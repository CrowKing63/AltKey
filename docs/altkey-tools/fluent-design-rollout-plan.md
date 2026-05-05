# Fluent Design 적용 계획: 설정/편집기/도구 창

## 1. 목적

메인 화상키보드 창을 제외한 보조 창에 **Fluent Design System 요소를 단계적으로 적용**한다.

- 대상: `SettingsWindow`, 보조 설정창, `AltKey.Tools` 호스트 창, 각종 편집기/검토 창
- 비대상: 메인 화상키보드 창, 자동완성 핵심 로직, 한글 조합 로직
- 최우선 기준: **접근성 향상에 실질적으로 도움이 되는가**

이번 계획은 한 번에 큰 리디자인을 하는 문서가 아니라, **빌드/테스트/커밋까지 끝낼 수 있는 페이즈 단위 실행 문서**다. 중간에 다른 이슈를 처리하더라도 다음 페이즈부터 바로 이어서 작업할 수 있게 설계한다.

## 2. 대상 창 범위

### 2.1 메인 앱(`AltKey`)

- `AltKey/Views/SettingsWindow.xaml`
- `AltKey/Views/SwitchScanSettingsWindow.xaml`
- `AltKey/Views/FocusA11ySettingsWindow.xaml`
- `AltKey/Views/LayoutEditorWindow.xaml`
- `AltKey/Views/UserDictionaryEditorWindow.xaml`

### 2.2 도구 앱(`AltKey.Tools`)

- `AltKey.Tools/MainWindow.xaml`
- `AltKey.Tools/ProfileMappingEditorWindow.xaml`
- `AltKey.Tools/ProfileMappingReviewWindow.xaml`
- `AltKey.Tools/AiPromptEditorWindow.xaml`

## 3. 현재 상태 요약

- 메인 앱은 이미 `WPF-UI` 패키지를 참조하고 있다. 따라서 Fluent 계열 컨트롤/리소스를 도입할 기반은 있다.
- 그러나 각 창은 아직 `SettingsBg`, `SettingsFg`, `SettingsBorder` 같은 기존 리소스와 개별 XAML 스타일에 크게 의존한다.
- 설정 계열 창은 키보드 접근성과 `AutomationProperties`가 비교적 잘 들어가 있지만, 시각 체계는 창마다 일관성이 약하다.
- 일부 보조 창/XAML에는 한글 문자열 인코딩이 깨진 흔적이 보인다.
  - `AltKey/Views/SwitchScanSettingsWindow.xaml`
  - `AltKey/Views/FocusA11ySettingsWindow.xaml`
  - `AltKey.Tools/ProfileMappingEditorWindow.xaml`
  - `AltKey.Tools/AiPromptEditorWindow.xaml`

위 인코딩 문제는 단순 미관 문제가 아니라, **스크린리더 라벨/창 제목/유지보수 주석 신뢰성**을 떨어뜨리므로 Fluent 적용 전에 선행 정리하는 편이 안전하다.

## 4. 설계 원칙

1. Fluent 요소는 “예쁘게”보다 “더 읽기 쉽고, 더 찾기 쉽고, 더 예측 가능하게” 적용한다.
2. 새 시각 체계는 **기존 접근성 동작**을 깨지 않아야 한다.
3. 컨트롤 교체보다 먼저 **공용 리소스/토큰화**를 한다.
4. 편집기와 도구 창은 메인 앱과 분리된 프로세스 구조를 유지한다.
5. 한 페이즈의 수정 범위는 가급적 한 창군 또는 한 리소스 계층으로 제한한다.
6. 각 페이즈는 끝날 때마다 `빌드`, 필요한 `테스트`, 최소 `수동 검증`, `커밋`까지 마친다.
7. 애니메이션은 기본적으로 절제하고, 추후 적용 시에도 `L2-reduced-motion` 방향과 충돌하지 않게 한다.

## 5. 적용할 Fluent 요소

이번 계획에서 말하는 Fluent 요소는 아래 범위로 제한한다.

- 윈도우 표면 계층감: 카드형 섹션, 균일한 코너 반경, 경계선/배경 단계
- 정보 구조: 제목, 설명문, 섹션 헤더, 서브텍스트 위계 정리
- 컨트롤 스타일: 버튼, 텍스트 입력, 콤보박스, 데이터그리드, 탭
- 내비게이션: 명확한 탭/피벗/카테고리 구조
- 상태 표현: hover, focus, disabled, validation 상태를 색상 하나에만 의존하지 않도록 보강
- 아이콘: 필요 시 텍스트를 대체하지 않는 보조 용도만 허용
- 모션: 있어도 짧고 의미 있는 수준만 허용, 필수 의존성으로 만들지 않음

## 6. 페이즈 구조

## Phase 0. 인코딩/문자열 무결성 복구

### 목표

Fluent 작업 전에 창 제목, 안내 문구, `AutomationProperties` 문자열이 신뢰 가능한 상태인지 복구한다.

### 범위

- 깨진 한글 문자열 복구
- 잘못된/누락된 접근성 라벨 정리
- 필요 시 주석 보강

### 수정 대상 후보

- `AltKey/Views/SwitchScanSettingsWindow.xaml`
- `AltKey/Views/FocusA11ySettingsWindow.xaml`
- `AltKey.Tools/ProfileMappingEditorWindow.xaml`
- `AltKey.Tools/AiPromptEditorWindow.xaml`

### 완료 기준

- 창 제목/본문/버튼/도움말 텍스트가 정상 한글로 보인다.
- `AutomationProperties.Name`이 실제 기능을 설명한다.
- 이 단계에서는 레이아웃 재설계나 컨트롤 교체를 하지 않는다.

### 검증

```powershell
dotnet build "C:\Users\UITAEK\AltKey\AltKey\AltKey.csproj" -v minimal
dotnet build "C:\Users\UITAEK\AltKey\AltKey.Tools\AltKey.Tools.csproj" -v minimal
dotnet test "C:\Users\UITAEK\AltKey\AltKey.Tests\AltKey.Tests.csproj" --no-build
```

### 커밋 단위 예시

- `fix: restore broken korean strings in auxiliary windows`

### 중단 후 재개 기준

- 이 커밋이 들어가면 이후 페이즈는 문자열 손상 걱정 없이 XAML 구조 변경에만 집중할 수 있다.

## Phase 1. Fluent 공용 리소스 계층 도입

### 목표

창별 하드코딩 스타일을 바로 바꾸지 않고, 먼저 공용 Fluent 리소스 계층을 만든다.

### 핵심 작업

- 기존 `Settings*` 색/브러시를 감싸는 Fluent용 alias/resource 추가
- 공용 `Button`, `TextBox`, `ComboBox`, `TabControl`, `DataGrid`, `Card` 계열 스타일 정의
- 포커스 시각 표시를 눈에 잘 띄게 통일
- 라이트/다크/고대비와 충돌하지 않는지 확인

### 수정 대상 후보

- `AltKey/App.xaml`
- `AltKey/Themes/*` 또는 신규 리소스 딕셔너리
- 필요 시 `AltKey.Tools/App.xaml`에서 병합 리소스 연결

### 완료 기준

- 어떤 창도 아직 크게 바뀌지 않아도 된다.
- 대신 이후 페이즈에서 재사용할 수 있는 공용 스타일 키가 준비되어 있어야 한다.
- 새 스타일은 기존 UI를 깨지 않는 opt-in 방식이어야 한다.

### 검증

```powershell
dotnet build "C:\Users\UITAEK\AltKey\AltKey\AltKey.csproj" -v minimal
dotnet build "C:\Users\UITAEK\AltKey\AltKey.Tools\AltKey.Tools.csproj" -v minimal
dotnet test "C:\Users\UITAEK\AltKey\AltKey.Tests\AltKey.Tests.csproj" --no-build
```

### 커밋 단위 예시

- `feat: add shared fluent resources for auxiliary windows`

### 중단 후 재개 기준

- 다음 페이즈부터는 창별로 `Style`/`DynamicResource`만 갈아끼우면 된다.

## Phase 2. 설정 창군 적용

### 목표

설정 관련 창부터 Fluent 체계를 적용해, 가장 자주 열리는 보조 UI의 탐색성과 가독성을 먼저 높인다.

### 범위

- `SettingsWindow`
- `SwitchScanSettingsWindow`
- `FocusA11ySettingsWindow`

### 핵심 작업

- 상단 제목/설명/본문 섹션을 카드형 위계로 재정리
- 탭, 그룹 제목, 숫자 조정기 주변 여백과 시선 흐름 정리
- 닫기/보조 동작 버튼의 일관된 크기와 초점 표시 적용
- 긴 설명문은 가독성 높은 길이로 재배치
- 키보드만으로 탭 이동 시 현재 위치가 명확하게 보이도록 보강

### 주의점

- `NumericAdjuster` 사용 원칙 유지
- 기존 바인딩/커맨드 이름은 가능한 한 유지
- 접근성 설정은 시각적 갱신 때문에 의미가 흐려지지 않도록 설명문을 더 명확히 다듬는다

### 완료 기준

- 설정 메인 창과 세부 창이 같은 제품군처럼 보인다.
- 각 섹션의 제목, 설명, 입력 영역의 간격 체계가 통일된다.
- Tab/Shift+Tab만으로 이동해도 현재 포커스가 쉽게 식별된다.

### 검증

```powershell
dotnet build "C:\Users\UITAEK\AltKey\AltKey\AltKey.csproj" -v minimal
dotnet test "C:\Users\UITAEK\AltKey\AltKey.Tests\AltKey.Tests.csproj" --no-build
```

수동 확인:

- 설정창 열기
- 탭 전환
- `NumericAdjuster` 조작
- 고대비 테마 전환
- Narrator 또는 UIA 검사로 제목/탭/버튼 이름 확인

### 커밋 단위 예시

- `feat: apply fluent styling to settings windows`

### 중단 후 재개 기준

- 이 단계가 끝나면 메인 앱 설정 UX는 안정화되고, 이후 도구/편집기 작업과 충돌이 적다.

## Phase 3. 도구 허브 창군 적용

### 목표

`AltKey.Tools`의 진입 창과 검토성 창을 Fluent 계열로 먼저 정리해, “편집 작업을 고르는 화면”의 정보 구조를 명확히 한다.

### 범위

- `AltKey.Tools/MainWindow.xaml`
- `AltKey.Tools/ProfileMappingReviewWindow.xaml`

### 핵심 작업

- 도구 선택 버튼을 단순 버튼 나열에서 카드형 액션 선택 구조로 승격
- 각 도구의 목적을 보조 텍스트로 일관되게 표현
- 검토 창의 요약, 목록, 판단 결과를 시각적으로 분리
- 창 닫기/기본 액션의 우선순위 표현 정리

### 완료 기준

- 처음 보는 사용자도 “무엇을 어디서 여는지” 빠르게 이해할 수 있다.
- 검토 창의 정보 밀도가 높아도 섹션 경계가 명확하다.

### 검증

```powershell
dotnet build "C:\Users\UITAEK\AltKey\AltKey.Tools\AltKey.Tools.csproj" -v minimal
dotnet build "C:\Users\UITAEK\AltKey\AltKey\AltKey.csproj" -v minimal
dotnet test "C:\Users\UITAEK\AltKey\AltKey.Tests\AltKey.Tests.csproj" --no-build
```

수동 확인:

- 설정창에서 `AltKey.Tools` 실행
- 각 버튼 포커스 이동
- `Esc` 닫기 동작

### 커밋 단위 예시

- `feat: restyle AltKey.Tools hub windows with fluent layout`

### 중단 후 재개 기준

- 이후 편집기 창 작업은 독립적으로 진행 가능하다.

## Phase 4. 편집기 창군 적용

### 목표

실제 입력/편집이 일어나는 창에 Fluent 요소를 적용하되, 기능 회귀 없이 편집 밀도와 포커스 추적성을 높인다.

### 범위

- `AltKey/Views/LayoutEditorWindow.xaml`
- `AltKey/Views/UserDictionaryEditorWindow.xaml`
- `AltKey.Tools/ProfileMappingEditorWindow.xaml`
- `AltKey.Tools/AiPromptEditorWindow.xaml`

### 핵심 작업

- 툴바, 본문, 보조 패널, 하단 액션 영역을 일관된 카드/패널 구조로 재배치
- DataGrid 헤더/행 선택/편집 상태 가시성 강화
- 저장/삭제/닫기 같은 주요 액션의 우선순위 표현 통일
- 텍스트 입력창과 검색창의 focus/validation 상태를 분명히 표시
- 긴 한글 입력이 많은 편집기에는 줄 길이, 패딩, 스크롤 감각을 조정

### 주의점

- `LayoutEditorWindow`는 레이아웃 편집 기능이 크므로, 시각 변경과 구조 변경을 분리할 필요가 있다.
- `UserDictionaryEditorWindow`는 word/bigram 탭 구조와 DataGrid 편집 흐름을 깨뜨리면 안 된다.
- 도구 앱 편집기는 메인 앱으로의 재로드 신호 경로를 건드리지 않는다.

### 완료 기준

- 각 편집기에서 저장/추가/삭제/검색/탭 전환이 기존과 동일하게 동작한다.
- 조작이 많은 창일수록 포커스 위치와 선택 상태가 더 잘 드러난다.

### 검증

```powershell
dotnet build "C:\Users\UITAEK\AltKey\AltKey\AltKey.csproj" -v minimal
dotnet build "C:\Users\UITAEK\AltKey\AltKey.Tools\AltKey.Tools.csproj" -v minimal
dotnet test "C:\Users\UITAEK\AltKey\AltKey.Tests\AltKey.Tests.csproj" --no-build
```

수동 확인:

- 레이아웃 편집기 열기/저장
- 사용자 단어 편집기 검색/추가/삭제
- 프로필 매핑 편집기 행 추가/저장
- AI 프롬프트 편집기 긴 문장 입력/저장
- 저장 후 메인 앱 반영 확인

### 커밋 단위 예시

- `feat: apply fluent styling to editor windows`

### 중단 후 재개 기준

- 이 단계는 창 수가 많으므로, 필요하면 `4A 메인 앱 편집기`, `4B AltKey.Tools 편집기`로 다시 나눠도 된다.

## Phase 5. 마감 정리와 회귀 검증

### 목표

시각 일관성, 접근성, 문서, 릴리스 영향 범위를 최종 정리한다.

### 핵심 작업

- 중복 스타일 제거
- 주석/문서 업데이트
- 릴리스에 필요한 리소스 누락 여부 확인
- 수동 접근성 점검 결과 기록

### 완료 기준

- 창군 전체가 같은 디자인 언어를 공유한다.
- 빌드/테스트/도구 실행/저장 반영까지 모두 통과한다.
- 이후 다른 이슈가 들어와도 Fluent 관련 수정 지점을 찾기 쉽다.

### 검증

```powershell
dotnet build "C:\Users\UITAEK\AltKey\AltKey\AltKey.csproj" -v minimal
dotnet build "C:\Users\UITAEK\AltKey\AltKey.Tools\AltKey.Tools.csproj" -v minimal
dotnet test "C:\Users\UITAEK\AltKey\AltKey.Tests\AltKey.Tests.csproj"
```

수동 확인:

- 설정창에서 각 도구 진입
- 각 창 `Esc`/닫기 버튼 동작
- 저장 후 메인 앱 재로드 반영
- 고대비/키보드 전용/Narrator 점검

### 커밋 단위 예시

- `chore: finalize fluent rollout for auxiliary windows`

## 7. 페이즈별 작업 순서 권장안

1. Phase 0
2. Phase 1
3. Phase 2
4. Phase 3
5. Phase 4
6. Phase 5

이 순서를 권장하는 이유는 다음과 같다.

- 문자열 무결성이 먼저 확보돼야 접근성 라벨을 안전하게 손볼 수 있다.
- 공용 리소스가 먼저 있어야 창별 수정이 얇아진다.
- 설정 창은 사용 빈도가 높고 피드백 루프가 빨라 초기에 검증하기 좋다.
- 편집기 창은 기능 밀도가 높으므로 가장 나중에 다루는 편이 안전하다.

## 8. 작업 분할 규칙

중간에 다른 이슈가 끼어들어도 꼬이지 않게 아래 규칙을 유지한다.

1. 한 커밋에는 가능하면 한 페이즈만 담는다.
2. 기능 변경과 시각 변경을 섞지 않는다.
3. 데이터/저장/IPC 경계 코드는 Fluent 작업에서 건드리지 않는다.
4. 창 제목, 안내 문구, `AutomationProperties` 수정은 가능하지만 ViewModel 의미 변경은 최소화한다.
5. 편집기 XAML 구조를 크게 바꿀 때는 code-behind/VM 변경을 같은 커밋에 무리하게 섞지 않는다.

## 9. 리스크와 대응

### 리스크 1. 기존 접근성 속성 누락

- 대응: 페이즈별 완료 기준에 `AutomationProperties` 재검토 포함

### 리스크 2. 고대비 테마 회귀

- 대응: Phase 1에서 공용 리소스 계층 설계 시 고대비 확인 필수

### 리스크 3. 편집기 기능 회귀

- 대응: 저장/추가/삭제/검색/재로드를 시각 변경과 별도로 수동 검증

### 리스크 4. 인코딩 재손상

- 대응: `apply_patch`만 사용, 파일 전체 재저장 금지, 한글 문자열 직접 확인

## 10. 바로 착수할 첫 작업

가장 안전한 시작점은 **Phase 0 + Phase 1**이다.

- Phase 0으로 깨진 문자열을 바로잡아 접근성 기반을 복구한다.
- 이어서 Phase 1에서 공용 Fluent 리소스를 도입한다.

이 두 단계가 끝나면 이후 작업은 창별 스타일 적용으로 쪼개기 쉬워지고, 중간에 다른 버그 수정이 끼어들어도 병합 충돌이 줄어든다.
