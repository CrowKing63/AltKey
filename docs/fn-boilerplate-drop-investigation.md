# Fn 상용구 간헐 분실 재조사 (2026-11-20)

## 증상
- Fn + 🗝️(ㅏ 키, `My` 레이아웃) 상용구 입력이 간헐적으로 실패.
- 매번 실패도, 매번 성공도 아님.
- 1차 패치 `0ef97c1`(11/12, "포그라운드 변경 직후 첫 입력 분실 방지") 이후에도 개선 없음.

## 확인된 사실 (증거)
1. **실행 프로세스**: 스토어 설치판 v1.7.0 (PID 32940, 11/20 12:17 시작,
   `C:\Program Files\WindowsApps\CrowKing63.AltKey_1.7.0.0_x64__bz4khnyqz3t80\AltKey.exe`).
   `0ef97c1`은 v1.5.1부터 포함 → **패치가 실행 빌드에 들어있음. 오래된 바이너리 가설 기각.**
2. **런타임 데이터 폴더**: `%AppData%\AltKey` (config.json 수정 시각 11/20 12:49 — 스토어
   프로세스 시작 이후. 이 머신의 스토어 설치판은 실제 Roaming을 사용하며, Packages
   LocalCache/Roaming 아래에 AltKey 폴더 없음).
   - `default_layout: "My"`, `auto_complete_enabled: false`,
     `switch_scan_enabled: false`, `keyboard_a11y_navigation_enabled: false`
   - 입력을 가로챌 훅/기능 전부 비활성.
3. **상용구 정의**: `My.json`의 ㅏ 키 — `fn_action = {"type":"Boilerplate","text":"147*965632^"}`,
   `fn_label = "🗝️"`. 비밀번호로 보이는 텍스트 → 브라우저 로그인 폼이 주 사용처로 추정.
4. **에러 로그**: 입력 관련 예외 없음 (구버전 IAsyncDisposable 종료 오류만, 11/13~14).
5. **진단 로그 부재**: 현재 빌드에 입력 트레이싱 없음 (`4c6339e`에서 제거).
   기존 `input-diagnostic.log`는 클립보드 Ctrl+V만 기록 — 이 문제의 직접 증거 아님.
   참고: 해당 로그의 붙여넣기 대상은 대부분 Edge/Chrome(`Chrome_WidgetWin_1`).
6. **반복/홀드 간섭 없음**: BoilerplateAction은 VK가 없어 `RepeatPolicy.Disabled`
   (`KeyButton.xaml.cs` ClassifyRepeatPolicy) → 탭 1회 = 실행 1회.

## 현재 완화 장치의 동작 (0ef97c1)
- `DispatchInput` 직전 `EnsureForegroundSettled` (`InputService.cs:646-667`):
  **포그라운드 최상위 창 핸들(HWND)이 마지막 전송 시점과 다르면 50ms 대기.** 같으면 0ms.
- Fn 토글은 입력을 보내지 않음(상태만 변경). 상용구는 `SendUnicode` 단일 배치
  (`InputService.cs:489-500`) — 포커스 확인·재시도 없음.

## 사용자 답변 (2026-11-20, 2차)
- 실패 시: 비밀번호 칸에 **아무것도 안 나타남 + Fn 상태도 안 풀림**. 🗝️를 다시 눌러야 입력됨.
- 실패 시점: 아이디 직후 / 다른 앱에서 넘어온 직후 **둘 다**.
- 진단 로그 + 디버그 빌드 승인함.

**핵심 추론**: Fn이 켜진 상태에서 🗝️ 탭이 KeyPressed까지 도달하면 전송 성공 여부와 무관하게
Fn one-shot은 반드시 소모된다(`KeyboardViewModel.cs` Fn 분기 → FinalizeFunctionLayerKeypress).
"Fn이 안 풀린다" = **첫 🗝️ 탭이 KeyPressed에 도달조차 못 했다** = 입력 전송 문제가 아니라
버튼/제스처 계층에서 클릭이 삼켜진 것. H1/H2(포그라운드 settle)로는 설명 불가 → 순위 하향.

## 가설 (순위순, 각각 반증 가능한 예측 포함)

### H5 (확정 — 로그 증거 아래): stale `_suppressNextClick`가 첫 Fn 탭을 삼킴
전제(전부 코드/설정에서 확인):
- 자동완성 OFF → VirtualKey 모드(`App.xaml.cs:199`), `key_repeat_enabled: true`(런타임 설정).
- Fn 꺼짐 상태의 ㅏ 키: SendKey VK_K → VirtualKey 모드에서 **LegacyRepeat**
  (`KeyButton.ClassifyRepeatPolicy`) → `CanStartRepeat()=true`.
- Fn 켜짐 상태의 🗝️: Boilerplate → VK 없음 → **Disabled** → 일반 Click 경로.

메커니즘:
1. 평소 ㅏ 탭(LegacyRepeat): MouseDown에서 즉시 실행 + `_suppressNextClick=true`
   (`KeyButton.xaml.cs:398`). 정상적으로는 MouseUp 뒤 WPF Click이 와서 OnClick이
   플래그를 소모하고 끝남(중복 실행 방지 — 코드 주석 394-397이 이 동작을 전제).
2. 반복 경로는 누름/뗌을 '처리 완료'(e.Handled=true)로 표시해 WPF 기본 Click 생성을
   구조적으로 막는다(마우스/터치 무관 — ButtonBase는 처리되지 않은 누름/뗌에서만
   Click을 만듦). Click이 안 오니 **플래그는 반복 탭 후 항상 잔류**.
   (플래그를 지우는 곳은 OnClick과 모드 변경 리셋뿐 — 캡처 손실/반복 취소는 안 지움.)
3. 이후 Fn 무장 → 🗝️ 탭: 정책이 Disabled로 바뀌어 일반 Click 경로로 가는데,
   **OnClick이 잔류 플래그에 걸려 첫 클릭을 무시** → KeyPressed 미발화 → 무전송 + Fn 유지.
4. 두 번째 🗝️ 탭: 플래그 소모됨 → 정상 실행 → 비밀번호 입력 + Fn 소모.

예측(로그로 검증):
- 실패 탭에서 `CLICK suppress=True` 로그 후 KEYPRESSED/BOILERPLATE 라인 없음.
- 직전 ㅏ 탭에서 DOWN(repeat=True) 후 CLICK 라인이 없거나 LOSTCAPTURE가 관측됨.
- 자동완성 ON(Unicode 모드)에서는 ㅏ 키도 Disabled라 플래그가 안 세팅 → 재현 안 될 것.
  (과거 디버그 검증이 자동완성 ON으로 이뤄졌다면 이 버그를 놓친 이유도 설명됨.)
- 수정 방향(확정 후): 반복 불가 정책의 새 MouseDown에서 잔류 플래그 해제
  (제스처 경계에서 리셋). 회귀 테스트: LegacyRepeat 탭 후 Click 미도달 상황 시뮬레이션
  → Fn 무장 → 첫 탭이 반드시 실행되어야 함.

### H1 (순위 하향 — H5가 증상 전체를 설명): 같은 창 안 필드 이동은 감지 못 함
아이디를 치는 동안 매 키가 `SendUnicode`/`SendAtomicReplace`로 전송되며
`_lastDispatchForegroundHwnd`를 브라우저 HWND로 갱신한다(`KoreanInputModule.cs:178,221`).
이후 비밀번호 칸을 클릭해도 최상위 HWND는 그대로 → **대기 0ms**.
비밀번호 칸의 내부 포커스 전환·자동완성(autofill) 팝업 타이밍과 겹치면 입력 분실 가능.
- 예측: 실패는 "아이디 입력 후 바로 비밀번호 칸으로 넘어가는" 흐름에 집중.
  다른 앱에서 넘어와서 누르는 경우는 대부분 성공.

### H2: 대기가 걸려도 50ms 고정 + 확인 없음
AI 캡처 경로는 이미 "최상위 포그라운드만으로는 부족, Edge 내부 포커스가 자리잡을 때까지
UIA `FocusedElement` 폴링(최대 ~1초) 필요"라는 교훈을 얻었다(스킬
`altkey-wpf-build-pitfalls`의 clipboard-races 절). 상용구 경로에는 미적용.
- 예측: 다른 앱에서 넘어오는 경우에도 간헐 실패. 무거운 페이지에서 더 잦음.

### H3: one-shot Fn 소모 함정 (실패를 증폭/오인시키는 구조)
전송이 실패해도 Fn one-shot은 소모된다(`KeyboardViewModel.cs:650`
`FinalizeFunctionLayerKeypress`). 이때 **Fn을 다시 누르지 않고 🗝️만 누르면
상용구 대신 기본 키 `ㅏ`(VK_K)가 비밀번호 칸에 입력**된다.
또한 Moonlight 터치 환경에서 Fn 탭 자체가 빗나가면 🗝️가 ㅏ를 입력한다.
- 예측: 실패 시 칸에 점 1개(ㅏ)가 보이거나, 재시도 성공 여부가 Fn 재누름에 좌우됨.
- 확인 방법: 진단 로그에 Boilerplate 분기 발화 여부 기록.

### H4 (낮음): OS 수준 분실
`SendInput` 반환값 sent < requested (예: UIPI). 대상이 비elevated 브라우저라 가능성 낮음.
- 예측: 로그에서 sent < requested 관측. 앱 수준 분실(sent == requested)과 구분됨.

## 확정 계획 — 실행됨 (2026-11-20, 사용자 승인 완료)
- [FN-DIAG] 마커 진단 로그 11곳 삽입 + `AltKey/Services/FnDiagLog.cs` (lock+예외 삼킴):
  KeyButton DOWN/UP/CLICK/LOSTCAPTURE(플래그·정책·Fn 상태), KeyPressed 발화,
  FN-TOGGLE/FN-CONSUMED, BOILERPLATE, SENDUNICODE, DISPATCH(req/sent/err/fg), SETTLE-WAIT.
- 로그 위치: `PathResolver.DataDir\fn-boilerplate-diag.log`
  (디버그 exe는 포터블 → `AltKey\bin\Debug\net8.0-windows\fn-boilerplate-diag.log`).
- 디버그 데이터 폴더를 실제 환경과 동기화: config.json(백업 config.json.bak-fndiag) +
  layouts/My.json 복사 — My 레이아웃, 자동완성 OFF, key_repeat ON.
- 빌드 0오류, 관련 테스트 21개 통과(KeyButton/InputServiceDispatch/FunctionLayer).
- 재현 절차: 스토어판 종료(단일 인스턴스 뮤텍스) → 디버그 exe 실행 → 평소 한글 입력
  (ㅏ 포함) 몇 번 → 비밀번호 칸에서 Fn+🗝️ (실패/성공 모두 몇 회) → 에이전트가 로그 판독.
- 검증 후 [FN-DIAG] 단일 grep으로 전체 제거.

## 원인 확정 (2026-11-20, 진단 로그 증거)
재현 10+회 중 실패 1회 포착. 실패 시퀀스(로그 그대로):
1. 18:03:57.256 평소 ㅏ 탭: DOWN repeat=True(LegacyRepeat) → 즉시 실행,
   UP suppress=True → LOSTCAPTURE 후 **CLICK 라인 없음** → 플래그 잔류.
2. 18:04:00.387 FN-TOGGLE → OneShot.
3. 18:04:01.137 첫 🗝️ 탭: DOWN policy=Disabled **suppress=True(잔류)** →
   CLICK suppress=True → 삼킴. KEYPRESSED/BOILERPLATE 없음 = 무전송 + Fn 유지.
4. 18:04:02.539 두 번째 🗝️ 탭: suppress=False → 정상 실행 → FN-CONSUMED.
성공한 나머지 시도 전부 suppress=False로 시작. H1/H2/H4 기각(전송 경로 무관).
추가 관찰(정정): LegacyRepeat 탭 후 CLICK이 안 오는 것은 터치 드릴프트 같은 우연이
아니라 구조다 — 반복 경로가 누름/뗌을 e.Handled=true로 표시해 ButtonBase의 기본
Click 생성을 막는다(2라운드 로그에서 반복 탭 100% CLICK 없음으로 확인. 초기의
'터치 드리프트' 가설은 100% 비율로 기각). 디바이스 무관 → 평소 ㅏ 입력 직후의
Fn+🗝️는 항상 실패했음. '간헐성'은 직전에 ㅏ를 보통 입력으로 쳤느냐에만 달렸음.

## 수정 (적용됨, 수동 검증 대기)
- `KeyButton.OnPreviewMouseLeftButtonDown` 시작(진단 로그 직후)에서
  `_suppressNextClick = false` — 새 제스처 시작 시 이전 제스처의 잔류 억제 해제.
  같은 제스처의 중복 억제는 반복 분기에서 재세팅되므로 안전.
- 회귀 테스트: `KeyButtonRepeatPolicyTests.Stale_suppress_flag_does_not_swallow_first_click_of_new_gesture`
  (RED 확인: Expected 1 / Actual 0 → 수정 후 GREEN). KeyButton 테스트 9개 통과.
- 전체 스위트: **374개 전부 통과** (실패 0).

## 수정 방향 (가설별, 아직 미적용)
- **H1/H2**: 포그라운드 변경 '시각' 기록(`ProfileService.OnWinEvent`가 이미 이벤트
  시각 파라미터를 받음 — 현재 버림) → 변경 후 N ms 이내 입력이면 대기.
  또는 상용구 경로에 한해 AI 캡처와 같은 UIA 포커스 확인 적용.
  `SendAtomicReplace` 공유 경로의 타이밍을 건드리지 않도록 주의
  (`docs/auto-complet/CORE-LOGIC-PROTECTION.md` §2 확인).
- **H3**: Fn 소모 정책 변경은 접근성 트레이드오프가 있어 사용자 결정 필요
  (예: 상용구 전송 확인 전까지 one-shot 유지 — 전송 확인 자체가 어려우므로 대안 논의).

## 질문 응답 (완료)
1. 실패 시: 아무것도 안 나타남 + Fn 상태 유지 → 2차 답변으로 H5 수립.
2. 둘 다 (포커스 타이밍 무관) → H1/H2 순위 하향 근거.
3. 승인 완료 → 진단 빌드 제작 완료.