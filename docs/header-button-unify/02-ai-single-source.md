# 02 — AI 버튼 표시는 상단바 탭이 관리 (D1)

> **소요**: 2~3시간
> **선행**: 01
> **후행**: 없음 (03·04와 독립)
> **수정(2026-11)**: 초기 안("AiEnabled 단일 스위치")에서 방향 변경. 표시는 상단바 탭 체크박스가 담당하고, AI 탭의 온/오프 체크박스를 제거한다.

---

## 1. 목표 한 줄

- ✨(AI) 버튼의 **표시** 켜기/끄기는 상단바 탭의 `Visible` 체크박스가 유일한 조절 소스다. 다른 기본 버튼과 완전히 같은 행 템플릿을 쓴다.
- AI 도구 탭은 엔드포인트·API 키·모델·기본 프롬프트·타임아웃 등 **동작 세팅만** 담당한다. 온/오프 체크박스는 없다.

---

## 2. 배경 (레포 기준)

- 초기 상태: `AppConfig.AiEnabled`(AI 도구 탭 체크박스) + `HeaderButtons[Ai].Visible`(상단바 탭 체크박스) **두 개의 진실 소스**.
- `MainViewModel.CreateHeaderButtonVm`은 `cfg.Visible`과 `AiEnabled`을 모두 확인해 ✨를 생성했다.
- 결과: AI 탭에서 끄면 ✨는 사라지지만 상단바 탭의 AI 행은 체크된 채 남아 UI가 서로 다른 상태를 표시했다.

---

## 3. 결정 (최종)

- **표시 = `HeaderButtons[Ai].Visible`** — 상단바 탭 체크박스가 유일한 조절 소스.
- **AI 탭 = 세팅만** — 온/오프 체크박스 제거, 설정 패널은 항상 활성화(게이트 없음).
- `AppConfig.AiEnabled` 필드를 코드에서 **전면 제거**. 기존 `altkey.config.json`에 남은 `AiEnabled` 키는 JSON 역직렬화에서 무시되어 호환된다.
- ✨ 버튼 클릭 동작(AI 처리)은 AI 탭 세팅(엔드포인트·키·모델)이 올바른지 여부에 따라 성패한다. 세팅이 없으면 기존처럼 `AiServiceException` 안내를 받는다 — 표시와 기능은 이제 독립적이다.

---

## 4. 작업 (완료됨)

1. `AppConfig.cs` — `AiEnabled` 프로퍼티 제거.
2. `MainViewModel.cs` — `AiEnabled` 속성·`OnPropertyChanged` 제거, `CreateHeaderButtonVm`의 `IdAi` 분기가 `cfg.Visible`만으로 생성.
3. `SettingsViewModel.cs` — `aiEnabled` 필드·`OnAiEnabledChanged`·로드/복원 시 AI `Visible` 동기화 로직 제거.
4. `SettingsWindow.xaml`:
   - 상단바 탭 공용 행 템플릿: AI 행 체크박스 숨김 트리거 제거 → 모든 버튼이 같은 체크박스를 사용.
   - "AI 도구가 비활성화되어 있습니다..." 안내 문구 제거.
   - AI 도구 탭: `"AI 도구 활성화 (상단바 ✨ 표시)"` 체크박스 제거, 설정 패널 `IsEnabled` 게이트 제거.

---

## 5. 하지 말 것

- `HeaderButtonConfig`의 `Visible` 프로퍼티를 삭제하지 않는다(모든 버튼 공용).
- AI 탭의 나머지 설정(엔드포인트/키/모델/프롬프트/타임아웃)은 건드리지 않는다.
- ✨ 표시와 AI 기능(세팅)을 다시 하나로 묶는 동기화 코드를 추가하지 않는다 — 둘은 독립적이다.

---

## 6. 검증

1. 빌드 그린 + `AltKey.Tests` 그린 (259개 통과).
2. 수동(사용자 확인 필요 — GUI 자동 입력 불가):
   - 상단바 탭에서 AI 행 체크 → ✨ 표시, 해제 → ✨ 사라짐. 위치·순서 편집은 그대로 가능.
   - AI 탭에서 세팅만 가능하고 온/오프 체크박스가 없음을 확인.
   - 세팅 없는 상태에서 ✨ 클릭 → 기존과 같은 AI 실패 안내.

---

## 7. 산출물

- `AiEnabled` 전면 제거(설정·ViewModel·XAML)
- 상단바 탭: AI 행이 다른 버튼과 동일한 체크박스 관리
- AI 도구 탭: 세팅 전용
