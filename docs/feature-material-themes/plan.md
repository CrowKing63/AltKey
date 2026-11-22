# 재질(마감) 설정 스펙 — 플라스틱 / 메탈 / 우드

- 상태: 계획 (구현 전)
- 관련 문서: 없음 (자립 문서)
- 범위: 시각 테마의 색 브러시 오버라이드만. 입력 로직·자동완성·한글 조합은 건드리지 않음.

## 1. 배경 (현재 동작)

테마는 `Themes/{이름}Theme.xaml`의 **플랫 브러시 사전**으로 구현되어 있다. `ThemeService.Apply(theme)`가 해당 사전을 `MergedDictionaries`에 추가하면 같은 키는 나중에 추가된 쪽이 이긴다.

| 항목 | 현재 값 |
|---|---|
| 테마 옵션 | 시스템 / 라이트 / 다크 / 고대비 (설정: `"system"` `"Light"` `"Dark"` `"HighContrast"`) |
| 저장 위치 | `AppConfig.Theme` (config.json) |
| 리소스 파일 | `Themes/LightTheme.xaml`, `DarkTheme.xaml`, `HighContrastTheme.xaml` (각 약 50개 브러시) |
| 적용 경로 | ① 메인 앱 시작 시 `App.xaml.cs:163` ② 설정 변경 시 `SettingsViewModel.OnThemeModeChanged` ③ 시스템 테마/고대비 실시간 변경 감지(`SystemParameters.StaticPropertyChanged`) ④ 도구 앱(AltKey.Tools) 시작 시 `ApplyInitialTheme` — Tools는 메인 프로젝트의 `ThemeService`를 그대로 참조 |
| 설정 UI | `Views/SettingsWindow.xaml` "테마" 섹션 라디오 버튼 (시스템/라이트/다크/고대비) |

## 2. 목표

- 테마(색 온도: 라이트/다크)와 별개로 **재질(마감 느낌)**을 고를 수 있게 한다.
- 재질은 실제 질감(텍스처·그라디언트)이 아니라 **톤과 테두리·그림자 세기**의 차이로 표현한다. 미니멀한 외관을 유지하기 위한 범위 결정이다.

### 재질 정의 (v1)

| 재질 | 설정 값 | 느낌 | 비고 |
|---|---|---|---|
| 플라스틱 | `plastic` | 현재 모습 그대로 | **기본값**. 별도 리소스 파일 없음 |
| 메탈 | `metal` | 차가운 회색, 테두리·그림자 강화 (단단함) | |
| 우드 | `wood` | 따뜻한 베이지 계열, 테두리 완화 (부드러움) | |

## 3. 설계

### 3.1 리소스 파일 추가

재질은 테마 사전을 **이어서** 로드하는 오버라이드 사전이다. 같은 키만 포함하면 되므로 파일당 10개 브러시.

- `Themes/LightMetalMaterial.xaml`
- `DarkMetalMaterial.xaml`
- `LightWoodMaterial.xaml`
- `DarkWoodMaterial.xaml`

(플라스틱은 파일 없음 = 현재 값 유지)

### 3.2 재질이 오버라이드하는 키 (테마 공통, 10개)

키보드 창 표면이 대상이다. 클립보드·빠른 메모 패널은 별도 배경 키가 아니라 `KeyboardBg`(와 상단 바 hover/pressed 브러시)를 그대로 공유하므로, 아래 키만 오버라이드하면 자동으로 함께 적용된다. 설정 화면은 테마 전용으로 둔다 (v1 범위).

| 키 | 역할 |
|---|---|
| `KeyboardBg` | 키보드 컨테이너 |
| `KeyBg` / `KeyBgHover` / `KeyBgPressed` | 키캡 배경 |
| `KeyBorder` | 키 테두리 |
| `KeyShadow` | 키 그림자 |
| `HeaderBarBg` / `HeaderBarBorder` | 상단 바 |
| `HeaderBarHoverBg` / `HeaderBarPressedBg` | 상단 바·클립보드 칩의 hover/pressed (패널도 공유) |

### 3.3 시작 색값 (수동 검증 시 조정 대상)

아래 값은 첫 제안이다. 사용자가 직접 보고 고치므로 확정값이 아니다.

**메탈 — 라이트**

| 키 | 현재 | 메탈 시작값 |
|---|---|---|
| KeyboardBg | `#CCE8E8EC` | `#CCE3E6EB` (차가운 회색) |
| KeyBg | `#FFFFFFFF` | `#FFF9FAFC` (약간의 차가움) |
| KeyBgHover | `#FFD4D4D8` | `#FFCFD5DC` |
| KeyBgPressed | `#FFBCBCC4` | `#FFB6BAC3` |
| KeyBorder | `#1E000000` | `#30000000` (강화) |
| KeyShadow | `#1E000000` | `#26000000` (강화) |
| HeaderBarBg | `#80FFFFFF` | `#80F2F4F8` |
| HeaderBarBorder | `#22000000` | `#2A000000` |
| HeaderBarHoverBg | `#FFE2E2E7` | `#FFDEE3EA` |
| HeaderBarPressedBg | `#FFD4D4DC` | `#FFCBD1DA` |

**메탈 — 다크**

| 키 | 현재 | 메탈 시작값 |
|---|---|---|
| KeyboardBg | `#CC1E1E1E` | `#CC1B1F25` (차가움) |
| KeyBg | `#CC3C3C3C` | `#CC3A3E45` |
| KeyBgHover | `#CC505050` | `#CC4C5158` |
| KeyBgPressed | `#FF2D2D2D` | `#FF2B2F36` |
| KeyBorder | `#0FFFFFFF` | `#1AFFFFFF` (강화) |
| KeyShadow | `#66000000` | `#73000000` |
| HeaderBarBg | `#33111111` | `#3314171C` |
| HeaderBarBorder | `#24FFFFFF` | `#2EFFFFFF` |
| HeaderBarHoverBg | `#CC646464` | `#CC5A6069` |
| HeaderBarPressedBg | `#CC727272` | `#CC686F79` |

**우드 — 라이트**

| 키 | 현재 | 우드 시작값 |
|---|---|---|
| KeyboardBg | `#CCE8E8EC` | `#CCF0EBE2` (따뜻한 밝음) |
| KeyBg | `#FFFFFFFF` | `#FFFDFBF6` (따뜻한 흰색) |
| KeyBgHover | `#FFD4D4D8` | `#FFE6DFD3` |
| KeyBgPressed | `#FFBCBCC4` | `#FFD8CFC0` |
| KeyBorder | `#1E000000` | `#194A3B28` (따뜻한 갈색, 약화) |
| KeyShadow | `#1E000000` | `#164A3B28` |
| HeaderBarBg | `#80FFFFFF` | `#80FBF7F0` |
| HeaderBarBorder | `#22000000` | `#1E3A2E1C` |
| HeaderBarHoverBg | `#FFE2E2E7` | `#FFEDE5D8` |
| HeaderBarPressedBg | `#FFD4D4DC` | `#FFDFD4C2` |

**우드 — 다크**

| 키 | 현재 | 우드 시작값 |
|---|---|---|
| KeyboardBg | `#CC1E1E1E` | `#CC211D18` (따뜻한 어두움) |
| KeyBg | `#CC3C3C3C` | `#CC3E3931` |
| KeyBgHover | `#CC505050` | `#CC4A443B` |
| KeyBgPressed | `#FF2D2D2D` | `#FF2F2A23` |
| KeyBorder | `#0FFFFFFF` | `#0BFFFFFF` (약화) |
| KeyShadow | `#66000000` | `#5E3A2E1C` |
| HeaderBarBg | `#33111111` | `#331A1712` |
| HeaderBarBorder | `#24FFFFFF` | `#1EFFFFFF` |
| HeaderBarHoverBg | `#CC646464` | `#CC5E574B` |
| HeaderBarPressedBg | `#CC727272` | `#CC6E6659` |

### 3.4 고대비 제외 정책 (핵심 결정)

- **해결된 테마가 HighContrast면 재질은 무조건 무시한다** (플라스틱 동작으로).
- 적용은 `ThemeService`가 코드에서 강제한다 — 설정 UI의 비활성화 표시와 별개로 안전망이 된다.
- "시스템" 모드에서 OS가 고대비로 실시간 전환되는 경우에도 동일하게 무시된다 (`StaticPropertyChanged` 경로를 그대로 사용).

### 3.5 ThemeService 변경

```csharp
// 기존: Apply(string theme) — 서명은 유지한다.
public void Apply(string theme)
{
    var resolved = theme == "system" ? DetectSystemTheme() : theme;
    var material = _configService.Current.Material;          // config에서 읽음
    foreach (var file in ResolveResourceFiles(resolved, material))
        /* MergedDictionaries에 기존 제거 후 순서대로 추가 */;
}

// 순수 함수 — WPF 없이 단위 테스트 가능
public static IReadOnlyList<string> ResolveResourceFiles(string resolvedTheme, string material)
    => ...  // 예: (Light, metal) → ["LightTheme.xaml", "LightMetalMaterial.xaml"]
```

- 재질은 매번 `config.Current.Material`을 읽으므로 **호출 사이트 수정이 없다** (메인 시작·설정 변경·시스템 감지·Tools 시작 모두 그대로).
- 기존 테마 사전과 재질 사전을 모두 참조해서 다음 Apply 시 둘 다 제거해야 한다.
- 재질 파일 경로가 없으면(빌드 누락 등) except한 채로 기본값으로 동작 — 앱이 깨지지 않는다.

### 3.6 설정 값

`AppConfig`에 추가:

```csharp
// 앱의 재질(마감) 설정. "plastic" | "metal" | "wood".
public string Material { get; set; } = "plastic";
```

- 기본값 `plastic`이므로 **기존 config.json 마이그레이션 불필요** — 키가 없으면 현재 모습 그대로다.

### 3.7 설정 UI (SettingsWindow.xaml)

"테마" 섹션 바로 아래 **"재질"** 섹션 추가:

- 라디오 3개: 플라스틱 / 메탈 / 우드 (`AutomationProperties.Name` 필수, 탭 순서 이어받기).
- ViewModel에 `MaterialMode` 속성 + `MaterialIsPlastic/Metal/Wood` 라디오 바인딩 (테마와 동일한 패턴).
- `OnMaterialModeChanged`: **먼저 config 저장 → 그 다음** `Apply(현재 테마)` 호출 (Apply가 config에서 재질을 읽으므로 순서가 중요하다).
- `OnThemeModeChanged`: 기존처럼 Apply만 — 재질은 config에서 자동으로 따라온다.
- **비활성화 규칙**: 해결된 테마가 고대비면 라디오 그룹을 전체 비활성 + 힌트 문구 "고대비 모드에서는 재질 설정이 적용되지 않습니다".
  - 조건: `ThemeMode == "HighContrast"` 또는 (`ThemeMode == "system"` && `SystemParameters.HighContrast`).
  - 시스템 모드는 OS 고대비 전환에 따라 실시간으로 바뀔 수 있으므로 `SettingsViewModel`도 `StaticPropertyChanged`(HighContrast)를 구독해 해당 속성의 `OnPropertyChanged`을 발생시킨다.

## 4. 수정 파일

| 파일 | 변경 |
|---|---|
| `AltKey/Themes/LightMetalMaterial.xaml` | 신규 — 메탈(라이트) 8개 브러시 |
| `AltKey/Themes/DarkMetalMaterial.xaml` | 신규 — 메탈(다크) 8개 브러시 |
| `AltKey/Themes/LightWoodMaterial.xaml` | 신규 — 우드(라이트) 8개 브러시 |
| `AltKey/Themes/DarkWoodMaterial.xaml` | 신규 — 우드(다크) 8개 브러시 |
| `AltKey/Services/ThemeService.cs` | 재질 사전 로드 + `ResolveResourceFiles` 순수 함수 + 안전망 |
| `AltKey/Models/AppConfig.cs` | `Material` 속성 추가 (기본 "plastic") |
| `AltKey/ViewModels/SettingsViewModel.cs` | `MaterialMode`·라디오 바인딩·변경 처리·고대비 비활성화 로직 |
| `AltKey/Views/SettingsWindow.xaml` | "재질" 섹션 라디오 3개 + 힌트 |
| `AltKey.Tests/Services/ThemeServiceTests.cs` | 신규 — 테스트 (기능별 폴더 분리 규칙) |

**수정 불필요**: `App.xaml.cs`(메인), `AltKey.Tools/App.xaml.cs` — Apply 서명 유지라 호출 그대로. Tools는 메인 프로젝트의 ThemeService를 참조하므로 자동 반영.

## 5. 테스트 계획

`ThemeServiceTests.cs` (400줄 이하):

1. `(Light, plastic)` → `["LightTheme.xaml"]`
2. `(Dark, metal)` → `["DarkTheme.xaml", "DarkMetalMaterial.xaml"]`
3. `(Light, wood)` → `["LightTheme.xaml", "LightWoodMaterial.xaml"]`
4. `(HighContrast, metal|wood)` → `["HighContrastTheme.xaml"]` — **재질 무시**
5. 알 수 없는 재질 값 (예: `"marble"`) → 테마 파일만 반환 (안전망)
6. 기존 테스트 회귀: `dotnet test` 전체 통과 (테마 관련 기존 테스트는 없음 — `WindowOpacityProfileTests` 등은 config의 Theme 문자열만 사용)

## 6. 수동 검증 체크리스트

사용자 직접 확인 (디버그 빌드, 자동완성 켜진 상태에서):

1. 재질 기본값: 새로 설치(설정 없이) 시 현재 모습과 동일 — 플라스틱이 기본임을 확인.
2. 라이트 + 메탈: 키보드 컨테이너·키캡이 차가운 회색, 테두리·그림자가 더 또렷해 보이는지.
3. 다크 + 우드: 따뜻한 어두운 계열인지, 키 테두리가 현재보다 약한지.
4. 재질 적용 상태에서 클립보드·빠른 메모 패널 열기: 키보드와 같은 재질(색)로 보이는지.
5. 고대비 + 메탈(또는 우드) 설정 상태: 화면은 고대비 그대로 유지되는지. 설정 창에서 재질 라디오가 비활성 + 힌트 문구 표시되는지.
6. 시스템 테마 모드에서 OS를 고대비로 전환: 재질이 자동 무시되는지, 설정 창이 비활성화 전환되는지.
7. 재질 변경 후 레이아웃 편집기(AltKey.Tools) 열기: 도구 창에도 같은 재질이 적용되는지.
8. 재질 변경 후 앱 재시작: 값이 유지되는지 (config.json에 `Material` 저장).
9. 색값 조정 반복: 2~4번 항목에서 색이 의도대로 안 나오면 §3.3 시작값을 이 문서에 갱신.

## 7. 가정·오픈 질문

- **가정(범위)**: v1 적용 대상 = 키보드 창 표면 + 클립보드·빠른 메모 패널(`KeyboardBg` 등 공유 브러시로 자동 반영). 설정 화면은 테마 전용으로 둔다.
- **가정**: "플라스틱 = 현재 모습"이므로 기본값과 플라스틱이 동일하다 — 별도 "기본" 옵션을 두지 않는다 (중복 제거).
- **오픈 질문**: 메탈·우드 시작색은 §3.3의 제안값이다. 수동 검증 2~4번 항목에서 조정 후 이 문서에 확정값으로 갱신할 예정이다.
