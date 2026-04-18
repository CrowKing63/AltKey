# 09 — 한국어 전용 축소 정리

> **소요**: 1~2시간
> **선행**: 06, 07, 08
> **후행**: 10
> **관련 기획**: [refactor-unif-serialized-acorn.md §0, D1](../refactor-unif-serialized-acorn.md), [사용자 의견 refactor-unified-autocomplete.md §7](../refactor-unified-autocomplete.md)

---

## 0. 이 태스크의 목표 한 줄

**영어 전용 레이아웃과 다국어 확장 잔재를 제거**하여 "한국어 사용자 전용 소프트웨어"로 정체성을 확정. 코드와 설정에서 PrimaryLanguage·qwerty-en 흔적을 지운다.

---

## 1. 전제 조건

- 01~08 완료. 한국어 모듈 경로가 엔드투엔드로 동작.

---

## 2. 현재 상태

### 2-1. 남아 있을 영어 잔재

- `AltKey/layouts/qwerty-en.json` — 파일.
- `AltKey/Models/AppConfig.cs` — `Language` 필드, `DefaultLayoutForLocale`에서 OS 로케일 체크.
- `AltKey/ViewModels/MainViewModel.cs` — 레이아웃 목록에 `qwerty-en`이 있을 가능성.
- `AltKey/Services/LayoutService.cs` — 번들 레이아웃 enumeration.
- `AltKey/Services/InputService.cs` — `SetAutoComplete` (04에서 처리 중일 수 있음).
- 기존 `user-words.json` (02에서 무시하기로 했지만 파일은 남음).

---

## 3. 작업 내용

### 3-1. `qwerty-en.json` 삭제

**파일**: `AltKey/layouts/qwerty-en.json` (삭제)

`.csproj`의 `<Content Include="layouts\qwerty-en.json">` 항목이 있다면 삭제.

### 3-2. `AppConfig` 단순화

**파일**: `AltKey/Models/AppConfig.cs`

- `Language` 필드를 `"ko"` 상수 또는 **제거**.
- `DefaultLayout` 기본값을 `"qwerty-ko"` 하드코딩.
- `DefaultLayoutForLocale` 계산 함수 제거.

```csharp
public string DefaultLayout { get; set; } = "qwerty-ko";
// Language 필드 삭제 또는 public string Language => "ko";
```

### 3-3. `MainViewModel`·`LayoutService` 정리

**파일**: `AltKey/ViewModels/MainViewModel.cs`, `AltKey/Services/LayoutService.cs`

- `qwerty-en` 참조 삭제.
- 레이아웃 목록 UI에서 `qwerty-en` 숨김.
- 사용자 커스텀 레이아웃은 허용(단, 한국어 모듈이 처리하므로 English 라벨만 있는 레이아웃은 QuietEnglish 모드로 입력됨 — 이는 의도된 동작).

### 3-4. `InputService.SetAutoComplete` 제거 확인

04에서 이미 제거되었을 수 있음. 그렇지 않다면 여기서 제거.

**파일**: `AltKey/Services/InputService.cs`

```csharp
// 제거 대상
private AutoCompleteService? _autoComplete;
public void SetAutoComplete(AutoCompleteService svc) => _autoComplete = svc;
// HandleAction 내부의 _autoComplete 호출 라인도 제거
```

**파일**: `AltKey/App.xaml.cs`
```csharp
// 제거
// inputService.SetAutoComplete(autoComplete);
```

### 3-5. 기존 `user-words.json` 마이그레이션 공지

**결정**: 마이그레이션하지 않음(D7). 사용자 데이터 손실 가능성을 한 줄 릴리스 노트로 공지.

**파일**: `docs/release-notes-v0.3.md` (신규, 선택)

```markdown
# v0.3 — 한국어 전용 리팩토링

## 호환성 주의
- 기존 `user-words.json`은 더 이상 사용되지 않습니다. 새로 학습이 시작됩니다.
- 영문 전용 레이아웃(qwerty-en)은 제거되었습니다. 영어 입력은 한국어 레이아웃 내
  "가/A" 토글로 사용하세요.
- 상단바에 자동완성 토글과 OS IME 한/영 비상 버튼이 추가되었습니다.
```

### 3-6. 불필요 필드/메서드 정리

**파일**: `AltKey/ViewModels/KeyboardViewModel.cs`

05에서 제거되었어야 할 것 재확인:
- `_isKoreanInput`
- `_layoutSupportsKorean`
- `_lastImeKorean`
- `HandleKoreanLayoutKey`, `HandleEnglishLayoutKey`, `HandleEnglishSubMode`
- `FinalizeKoreanComposition`
- `GetHangulJamoFromSlot`
- `ShouldSkipHandleAction`
- `UpdateImeState` (폴링이 더 이상 필요하지 않으면)
- `ImeModeChanged` 이벤트

grep 재확인:
```
grep -n "HandleKoreanLayoutKey\|HandleEnglishLayoutKey\|HandleEnglishSubMode\|FinalizeKoreanComposition\|_isKoreanInput\|_layoutSupportsKorean\|_lastImeKorean\|ImeModeChanged" AltKey/
```
→ 남아 있다면 제거.

### 3-7. `README.md` · `AGENTS.md` · `BLUEPRINT.md` 갱신

**파일**: `README.md`
- 프로젝트 설명을 "한국어 사용자 전용 가상 키보드"로 수정.
- 기능 목록에 "가/A 토글", "상단바 OS IME 한/영 비상 버튼", "유니코드 기반 한국어 자동완성" 반영.

**파일**: `AGENTS.md`
- 자동완성 코드 주의사항 업데이트. `KoreanInputModule`을 언급. 기존 `HandleKoreanLayoutKey` 경고문 업데이트.

**파일**: `docs/BLUEPRINT.md`
- 한국어 전용 기조 반영. 다국어 확장 섹션이 있다면 "미래 작업" 또는 삭제.

### 3-8. `.csproj` 정리

**파일**: `AltKey/AltKey.csproj`
- `<Content Include="layouts\qwerty-en.json">` 제거.
- `en-words.txt`의 `<EmbeddedResource>`는 유지(QuietEnglish 서브모드에서 사용).

### 3-9. DI 조립 최종 확인

**파일**: `AltKey/App.xaml.cs`

최종 순서:
```csharp
services.AddSingleton<Func<string, WordFrequencyStore>>(_ => lang => new WordFrequencyStore(lang));
services.AddSingleton<KoreanDictionary>();
services.AddSingleton<EnglishDictionary>();
services.AddSingleton<KoreanInputModule>();
services.AddSingleton<IInputLanguageModule>(sp => sp.GetRequiredService<KoreanInputModule>());
services.AddSingleton<AutoCompleteService>();
services.AddSingleton<InputService>();
services.AddSingleton<LiveRegionService>();   // 08에서 신규
// ... 기타 ViewModel 등
```

관리자 모드 보정(06-3-7):
```csharp
if (_inputService.IsElevated && _config.Current.AutoCompleteEnabled)
{
    _config.Current.AutoCompleteEnabled = false;
    _config.Save();
}
```

---

## 4. 검증

1. 빌드 녹색.
2. 런타임:
   - 레이아웃 목록에 `qwerty-ko`만 표시. 커스텀 레이아웃이 있으면 추가 표시.
   - 모든 기능 정상: 한글 조합·자동완성·"가/A" 토글·상단바 두 버튼·Submode 전환.
3. grep:
   - `grep -rn "qwerty-en" AltKey/` → 빈 결과.
   - `grep -rn "PrimaryLanguage" AltKey/` → 빈 결과(이 용어가 남아 있다면 제거).
4. 포터블 빌드:
   ```
   dotnet publish AltKey/AltKey.csproj -r win-x64 --self-contained -c Release
   ```
   산출물이 단일 exe인지 확인(기존 설정 유지).

---

## 5. 함정 / 주의

- **사용자 config.json 호환성**: 기존 사용자의 `Language`·`DefaultLayout` 값이 JSON 역직렬화 시 `UnknownMemberHandler.Skip` 또는 기본값으로 떨어지는지 확인. `System.Text.Json`은 기본적으로 알 수 없는 프로퍼티 무시.
- **레이아웃 디렉토리에 `qwerty-en.json` 잔존 시**: 런타임이 로드 대상으로 착각할 수 있음. 파일·csproj 항목 모두 제거.
- **테스트 프로젝트 수정**: `AltKey.Tests`가 `qwerty-en`을 참조한다면 제거. `HangulComposerTests`는 영향 없음.
- **문서 업데이트 누락**: AGENTS.md가 과거 경고문을 담고 있으면 코드와 괴리.
- **코드 정리 과정에서 Submode 비활성화**: 실수로 `KoreanInputModule`의 QuietEnglish 경로를 함께 제거하면 "A" 모드가 동작하지 않음. **QuietEnglish는 유지**.

---

## 6. Critical Files

| 파일 | 수정 유형 |
|---|---|
| `AltKey/layouts/qwerty-en.json` | **삭제** |
| `AltKey/Models/AppConfig.cs` | 수정 (Language 필드 제거, DefaultLayout 하드코딩) |
| `AltKey/ViewModels/MainViewModel.cs` | 소폭 (레이아웃 목록 정리) |
| `AltKey/Services/LayoutService.cs` | 소폭 (qwerty-en enumeration 제거) |
| `AltKey/Services/InputService.cs` | 소폭 (SetAutoComplete 제거 확인) |
| `AltKey/App.xaml.cs` | 소폭 (DI·관리자 보정) |
| `AltKey/AltKey.csproj` | 수정 (Content 항목 제거) |
| `README.md`, `AGENTS.md`, `docs/BLUEPRINT.md` | 문서 갱신 |

---

## 7. 커밋 메시지 초안

```
chore(ko-only): drop qwerty-en, retire PrimaryLanguage concept

- Delete layouts/qwerty-en.json and csproj Content entry.
- AppConfig.Language removed; DefaultLayout hardcoded to qwerty-ko.
- LayoutService and MainViewModel no longer enumerate qwerty-en.
- InputService.SetAutoComplete removed (replaced by module delegation).
- Docs updated: README, AGENTS, BLUEPRINT reflect Korean-only scope.
- Note in release notes: old user-words.json is ignored.
```
