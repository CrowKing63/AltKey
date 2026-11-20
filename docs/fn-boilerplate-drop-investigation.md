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

## 가설 (순위순, 각각 반증 가능한 예측 포함)

### H1 (유력): 같은 창 안 필드 이동은 감지 못 함
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

## 확정 계획 (사용자 승인 필요 — 조사와 로그 추가는 별개 승인)
1. 임시 진단 로그 추가 (고유 마커 `[FN-DIAG]`, 검증 후 단일 grep으로 제거):
   - `HandleAction` Boilerplate 분기: 텍스트 길이, Fn 상태
   - `SendUnicode`: 요청 이벤트 수
   - `DispatchInput`: requested/sent, 포그라운드 HWND/제목/PID, 대기 발생 여부
   - `EnsureForegroundSettled`: last HWND vs 현재 HWND (H1 직접 검증)
   - 위치: `PathResolver.DataDir\fn-boilerplate-diag.log` (lock + 예외 삼킴 — 로그가 입력을 방해하면 안 됨)
2. 디버그 빌드 → 사용자가 로그인 흐름 1회 재현 (스토어판 먼저 종료 — 단일 인스턴스 뮤텍스)
   → 에이전트가 로그 직접 확인 (사용자에게 결과 보고 요구하지 않음).
3. 실패 시점의 전후 로그로 H1~H4 판별. 필요 시 UIA FocusedElement 프로브 추가.

## 수정 방향 (가설별, 아직 미적용)
- **H1/H2**: 포그라운드 변경 '시각' 기록(`ProfileService.OnWinEvent`가 이미 이벤트
  시각 파라미터를 받음 — 현재 버림) → 변경 후 N ms 이내 입력이면 대기.
  또는 상용구 경로에 한해 AI 캡처와 같은 UIA 포커스 확인 적용.
  `SendAtomicReplace` 공유 경로의 타이밍을 건드리지 않도록 주의
  (`docs/auto-complet/CORE-LOGIC-PROTECTION.md` §2 확인).
- **H3**: Fn 소모 정책 변경은 접근성 트레이드오프가 있어 사용자 결정 필요
  (예: 상용구 전송 확인 전까지 one-shot 유지 — 전송 확인 자체가 어려우므로 대안 논의).

## 미해결 질문 (사용자에게 전달 완료, 응답 대기)
1. 실패 시 비밀번호 칸에 무엇이 나타나나: 아무것도 / 일부만 / ㅏ / 모름
2. 주 실패 시점: 아이디 직후 / 다른 앱에서 넘어온 직후 / 둘 다
3. 진단 로그 + 디버그 빌드 승인 여부