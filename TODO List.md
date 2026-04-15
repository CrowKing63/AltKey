# TODO List

> 새로 추가할 기능 아이디어나 직접 테스트하면서 겪게 되는 문제들을 목록화

## 기능 아이디어 및 문제점

- 관리자 권한 앱(예: 작업 관리자) 포그라운드 상태에서 키 클릭 → 권한 경고 배너 표시 안 됨.
- 설치형 앱 자동 업데이트(현재 포터블처럼 릴리즈 페이지로 연결만).

---

## 처리 완료

- [x] **클립보드와 이모지 패널 닫기** — 패널 바깥의 키보드 여백/버튼 클릭 시 닫히게, 패널 상단에 닫기(✕) 버튼 추가.
  - `EmojiViewModel`, `ClipboardViewModel`에 `CloseCommand` 추가
  - `EmojiPanel.xaml`, `ClipboardPanel.xaml`에 닫기 버튼 추가
  - `KeyboardViewModel.KeyTapped` 이벤트 → `MainViewModel`에서 구독해 패널 자동 닫기
  - `KeyboardBorder.MouseLeftButtonDown`으로 여백 클릭 시 닫기

- [x] **설정창 배경과 글자 테마 적용** — 하드코딩된 색상을 DynamicResource로 교체.
  - `DarkTheme.xaml`, `LightTheme.xaml`에 `SettingsBg`, `SettingsFg`, `SettingsFgSub`, `SettingsFgHint`, `SettingsHighlight`, `SettingsBorder` 리소스 추가
  - `SettingsView.xaml` 전체 색상을 DynamicResource 바인딩으로 교체

- [x] **한국어 레이아웃의 키 라벨을 한글 중심으로** — `qwerty-ko.json`에서 `label`(알파벳) ↔ `hangul_label`(한글) 교환. 쌍자음/쌍모음은 `shift_label`로 이동.

- [x] **README.md 영문화** — 전체 내용을 영문으로 번역.

---

## 대형 기능 (별도 세션 필요)

> 각 기능은 `docs/` 폴더에 상세 설계 문서가 작성되어 있음. 새 세션에서 해당 문서를 읽고 바로 구현 시작 가능.

- [ ] **텍스트 포커스 위치에 따라 키보드 자동 이동** — 현재 포커스된 텍스트 필드의 화면 좌표를 감지해 키보드가 가리지 않는 쪽으로 자동 이동. 옵션으로 온오프 가능.  
  → 설계 문서: [`docs/feature-focus-tracker.md`](docs/feature-focus-tracker.md)

- [ ] **한국어 자동 완성 기능** — AltKey 내부에서 한글 자모 조합 상태를 직접 추적해 IME 없이도 한국어 단어 제안 제공. 내장 빈도 사전 + 사용자 학습 2레이어 구조.  
  → 설계 문서: [`docs/feature-korean-autocomplete.md`](docs/feature-korean-autocomplete.md)

- [ ] **다양한 접근성 기능 추가** — 고대비 테마, 키 라벨 TTS(음성 읽기), 큰 텍스트 모드, Windows Narrator 지원, 색맹 친화 모드.  
  → 설계 문서: [`docs/feature-accessibility.md`](docs/feature-accessibility.md)
