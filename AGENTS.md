- 모든 기능의 구현과 수정에 대해 '이것이 접근성 향상에 도움이 되는가'를 최우선으로 고려할 것. 
- 사용자 요청에 대한 응답은 한국어로 작성할 것.
- 사용자는 코딩 전문가가 아님.
- 이 프로젝트에서 가장 중요한 기능인 자동 완성은 구현 자체가 너무나 까다로웠다. 관련 코드 수정시 각별한 주의가 필요하다. 다음의 문서를 참고할 것. C:\Users\UITAEK\AltKey\docs\ime-korean-detect.md
- **자동완성/한글 조합 관련 코드를 수정하기 전에 [`docs/auto-complet/CORE-LOGIC-PROTECTION.md`](docs/auto-complet/CORE-LOGIC-PROTECTION.md) §2 "절대 건드리지 말 것" 목록만 확인하면 된다.** 2026-04-18 분석에서 파생된 TASK-01~08은 2026-04-18에 모두 해결 완료되었으므로 `docs/auto-complet/TASK-XX-*.md`와 `findings-overview.md`는 과거 맥락 참고용 기록일 뿐이며, 사용자가 명시적으로 새 버그를 지시하지 않는 한 다시 순회할 필요 없다. 새 이슈가 발견되면 번호를 이어 붙여 작성한다.
- 이 프로젝트는 **한국어 사용자 전용** 가상 키보드이다. `qwerty-en.json`은 삭제되었으며, `Language` 필드와 `PrimaryLanguage` 개념은 제거되었다. 영어 입력은 한국어 레이아웃 내 "가/A" 토글(QuietEnglish 서브모드)로 처리된다.
- 기존 `HandleKoreanLayoutKey`, `HandleEnglishLayoutKey`, `HandleEnglishSubMode`, `_isKoreanInput`, `_layoutSupportsKorean`, `_lastImeKorean`은 모두 제거되었다. 한국어 입력 로직은 `KoreanInputModule`으로 이전되었다.
- **프로젝트 빌드 및 테스트 시 다음 경로를 기준으로 작업을 수행할 것.** (경로를 찾지 못하는 문제를 방지하기 위함)
  - 메인 프로젝트(빌드): `C:\Users\UITAEK\AltKey\AltKey\AltKey.csproj`
  - 테스트 프로젝트: `C:\Users\UITAEK\AltKey\AltKey.Tests\AltKey.Tests.csproj`
- **PowerShell 환경에서는 `&&` 연산자가 작동하지 않으므로, 여러 명령어를 실행할 때는 `;`를 사용하거나 각 명령어를 별도로 실행할 것.**

## 프로젝트 구조

```
AltKey/
├── App.xaml / App.xaml.cs          # 앱 진입점, DI 컨테이너
├── MainWindow.xaml / .cs           # 메인 창 (투명, NoActivate, Acrylic)
│
├── Views/                          # XAML 뷰
│   ├── KeyboardView.xaml           # 키보드 UI
│   ├── SettingsView.xaml           # 설정 패널
│   ├── LayoutEditorWindow.xaml    # 레이아웃 에디터
│   ├── ActionBuilderView.xaml      # 액션 빌더
│   ├── EmojiPanel.xaml             # 이모지 패널
│   ├── ClipboardPanel.xaml        # 클립보드 히스토리 패널
│   └── SuggestionBar.xaml        # 자동완성 추천 바
│
├── ViewModels/                    # MVVM 뷰모델 (CommunityToolkit.Mvvm)
├── Models/                        # 데이터 모델 (레이아웃 & 설정 구조체)
├── Services/                     # 핵심 서비스
│   ├── InputService.cs           # SendInput 래퍼, Sticky Keys, 액션 디스패처
│   ├── LayoutService.cs           # JSON 레이아웃 로딩, 세이브, 캐싱
│   ├── ConfigService.cs          # 설정 JSON 읽기/쓰기
│   ├── ProfileService.cs          # WinEventHook 포그라운드 앱 감지
│   ├── ThemeService.cs           # 다크/라이트/시스템 테마 적용
│   ├── HotkeyService.cs          # 전역 단축키 등록
│   ├── StartupService.cs          # 시작 프로그램 레지스트리 관리
│   ├── SoundService.cs           # 키 클릭 음향 재생
│   ├── AutoCompleteService.cs    # 단어 자동완성
│   └── UpdateService.cs         # GitHub Releases 업데이트 확인
├── Controls/                      # 커스텀 컨트롤 (KeyButton 등)
├── Platform/                     # Win32 P/Invoke 선언
├── Themes/                       # WPF 리소스 딕셔너리 (색상, 스타일)
└── layouts/                       # 기본 레이아웃 JSON 파일
```