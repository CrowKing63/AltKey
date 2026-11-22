# 재질(마감) 설정 스펙 — 플라스틱 / 메탈 / 우드

- 상태: 구현 완료 — 수동 검증 대기 (§6 체크리스트)
- 관련 문서: `materials-research.md` (메탈·우드 외 후보 재질 조사 — v2 확장용)
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
| 플라스틱 | `plastic` | 라이트 **유리/세라믹**(frost + hairline 테두리) / 다크 **탄소섬유**(중립 스텔스) — 합성 소재 계열 | **기본값** |
| 메탈 | `metal` | 라이트 **알루미늄**(anodized silver) / 다크 **티타늄 건메탈**, 가공 모서리 테두리 강화 (단단함) | |
| 우드 | `wood` | 라이트 **메이플**(pale cream) / 다크 **월넛**(진한 붉은-브라운), 따뜻한 톤 테두리 — 강도 베이스 대비 약 2배(라이트/다크 동일) | |

## 3. 설계

### 3.1 리소스 파일 추가

재질은 테마 사전을 **이어서** 로드하는 오버라이드 사전이다. 같은 키만 포함하면 되므로 파일당 12개 브러시(기본 7 + 액센트 키 5).

- `Themes/LightPlasticMaterial.xaml` — 유리/세라믹 (플라스틱 라이트)
- `DarkPlasticMaterial.xaml` — 탄소섬유 (플라스틱 다크)
- `LightMetalMaterial.xaml`
- `DarkMetalMaterial.xaml`
- `LightWoodMaterial.xaml`
- `DarkWoodMaterial.xaml`

(모든 카테고리가 테마 파일 + 재질 오버라이드 1개. 알 수 없는 재질 값만 테마 파일 단독.)

### 3.2 재질이 오버라이드하는 키 (실제 사용 12개)

키보드 창 표면이 대상이다. 클립보드·빠른 메모 패널은 별도 배경 키가 아니라 `KeyboardBg`(와 상단 바 hover/pressed 브러시)를 그대로 공유하므로, 아래 키만 오버라이드하면 자동으로 함께 적용된다. 설정 화면은 테마 전용으로 둔다 (v1 범위).

| 키 | 역할 |
|---|---|
| `KeyboardBg` | 키보드 컨테이너 (패널 배경과도 공유) |
| `KeyBg` / `KeyBgHover` / `KeyBgPressed` | 키캡 배경 |
| `KeyBorder` | 키 테두리 |
| `HeaderBarHoverBg` / `HeaderBarPressedBg` | 상단 바 버튼·클립보드 칩의 hover/pressed (패널도 공유) |
| `KeyBgAccent` / `KeyBgAccentHover` / `KeyBgAccentPressed` | 액센트 키(사용자 지정 특수 키, `HasSoftAccentStyle`) 배경 — v5 추가 |
| `KeyFgAccent` | 액센트 키 글자 (Fn 뱃지도 공유) |
| `KeyBorderAccent` | 액센트 키 테두리 + Fn one-shot/lock 점선 테두리 |

> 참고: 테마 파일에 있는 `KeyShadow`, `HeaderBarBg`, `HeaderBarBorder`는 현재 XAML에서 사용되지 않는 키라 오버라이드 대상에서 제외했다 — 키에는 그림자가 아니라 1px 테두리만 있고, 상단 바는 카드(KeyboardBg) 위에 투명한 구조다.
> 참고(v5): **상태 표시 색은 재질과 무관하게 유지**한다 — `KeyBgSticky`/`KeyFgSticky`(sticky·lock 고정 표시), `AccentBrush`(가/A 토글·포커스 테두리, 브랜드 라벤더-블루). 재질을 바꿔도 "고정됨/포커스" 인식이 깨지면 안 되므로(접근성) 모든 재질에서 동일하게 남는다.

### 3.3 재질 색값 (v11 — 다크 테두리 α 배율 일치(수치 일관성), 샌드박스 수동 검증 대기)

아래 값은 **슬롯(재질×테마)마다 각각 다른 소재**를 쓴 v3 + 플라스틱(기본)에 유리/세라믹·탄소섬유를 부여한 v4 + 액센트 키 5개 브러시를 모든 재질에 확장한 v5 + **메탈 액센트 테두리 보정(v6)**. 제4 카테고리는 v8에서 제거됐다(결정 근거는 아래 히스토리). 액센트 값은 각 테마의 "기본 키 → 액센트 키" 명도 오프셋을 그대로 재질 톤에 적용했다.
- 메탈: 라이트는 **알루미늄**(anodized silver), 다크는 **티타늄 건메탈** — 올리브/브라운 힌트의 무디한 프리미엄 그레이(스페이스 그레이의 블루 시프트도, 탄소의 순수 중립 블랙도 아님). 테두리는 가공 모서리처럼 강화.
- 우드: 라이트는 **메이플**(가장 밝은 목재, 페일 크림), 다크는 **월넛**(다크 오크보다 밝고 붉은 포화의 딥 브라운). 테두리는 따뜻한 톤(브라운/골드), 강도는 α 배율로 라이트/다크 동일(베이스 대비 약 2배 — v11 수치 확인: "완화"가 아니었음).
- 플라스틱(기본): 라이트는 **유리/세라믹**(frost 화이트 + hairline 테두리), 다크는 **탄소섬유**(중립 스텔스) — 합성 소재 계열.

**플라스틱 — 라이트 (유리/세라믹)**

| 키 | 현재(테마 기본) | 플라스틱 값 |
|---|---|---|
| KeyboardBg | `#CCE8E8EC` | `#CCEFF1F4` (frost 화이트) |
| KeyBg | `#FFFFFFFF` | `#FFFFFFFF` (순수 흰색 — 유지) |
| KeyBgHover | `#FFD4D4D8` | `#FFE8EBEF` |
| KeyBgPressed | `#FFBCBCC4` | `#FFD9DEE4` |
| KeyBorder | `#1E000000` | `#14000000` (hairline — v10: 6%→8%로 올려 정지 시 경계선 식별, ΔL* 3.5→7) |
| HeaderBarHoverBg | `#FFE2E2E7` | `#FFECEFF3` |
| HeaderBarPressedBg | `#FFD4D4DC` | `#FFDFE3E8` |
| KeyBgAccent | `#FFF3F3F6` | `#FFF5F7FA` (frost — 기본과 거의 같아 재질감이 약함 = 유리 특성) |
| KeyBgAccentHover | `#FFE8E8ED` | `#FFE9EDF2` |
| KeyBgAccentPressed | `#FFDCDCE3` | `#FFDCE2E9` |
| KeyFgAccent | `#FF22242A` | `#FF1C2028` (차가운 거의-검정) |
| KeyBorderAccent | `#4A787C86` | `#3A707884` (hairline, 약화) |

**플라스틱 — 다크 (탄소섬유)**

| 키 | 현재(테마 기본) | 플라스틱 값 |
|---|---|---|
| KeyboardBg | `#CC1E1E1E` | `#CC262628` (중립 탄소) |
| KeyBg | `#CC3C3C3C` | `#CC3A3A3C` (거의 중립) |
| KeyBgHover | `#CC505050` | `#CC4E4E50` |
| KeyBgPressed | `#FF2D2D2D` | `#FF313133` |
| KeyBorder | `#0FFFFFFF` | `#0AFFFFFF` (hairline — v11: α 배율 0.67×로 라이트와 일치, ΔL* 13.8→3.6) |
| HeaderBarHoverBg | `#CC646464` | `#CC5F5F61` |
| HeaderBarPressedBg | `#CC727272` | `#CC717173` |
| KeyBgAccent | `#CC4D4D4D` | `#CC4B4B4D` (중립) |
| KeyBgAccentHover | `#CC595959` | `#CC575759` |
| KeyBgAccentPressed | `#CC404040` | `#CC444446` |
| KeyFgAccent | `#FFF5F5F5` | `#FFF2F2F4` (중립 오프화이트) |
| KeyBorderAccent | `#80A2A2A2` | `#8A909094` (크리스프 중립, 강화) |

**메탈 — 라이트 (알루미늄)**

| 키 | 현재(플라스틱) | 메탈 값 |
|---|---|---|
| KeyboardBg | `#CCE8E8EC` | `#CCCBD1DA` (진하고 차가운 실버) |
| KeyBg | `#FFFFFFFF` | `#FFF8FAFC` (차가운 거의-흰색) |
| KeyBgHover | `#FFD4D4D8` | `#FFC7CFDB` |
| KeyBgPressed | `#FFBCBCC4` | `#FFAEB9C8` |
| KeyBorder | `#1E000000` | `#40000000` (가공 모서리, 2배 강화) |
| HeaderBarHoverBg | `#FFE2E2E7` | `#FFD5DCE6` |
| HeaderBarPressedBg | `#FFD4D4DC` | `#FFBFC9D7` |
| KeyBgAccent | `#FFF3F3F6` | `#FFEDEEF3` (v9 — 차가운 실버, 진하게. 일반키 대비 ΔL 0.058→0.097) |
| KeyBgAccentHover | `#FFE8E8ED` | `#FFD7DFEA` (v9) |
| KeyBgAccentPressed | `#FFDCDCE3` | `#FFC4CDDC` (v9) |
| KeyFgAccent | `#FF22242A` | `#FF1E242E` (차가운 거의-검정) |
| KeyBorderAccent | `#4A787C86` | `#8A525E6E` (v6 — 일반 키 테두리(25% 블랙)와 명확히 구분되는 차가운 스틸그레이. 측정 차이 10→32) |

**메탈 — 다크 (티타늄 건메탈)**

| 키 | 현재(플라스틱) | 메탈 값 |
|---|---|---|
| KeyboardBg | `#CC1E1E1E` | `#CC32342F` (무디한 웜-뉴트럴 건메탈) |
| KeyBg | `#CC3C3C3C` | `#CC464842` (무디한 다크 그레이) |
| KeyBgHover | `#CC505050` | `#CC5B5D55` |
| KeyBgPressed | `#FF2D2D2D` | `#FF3C3E38` |
| KeyBorder | `#0FFFFFFF` | `#20FFFFFF` (가공 모서리 — v11: α 배율 2.13×로 라이트와 일치, ΔL* 12.3→9.8) |
| HeaderBarHoverBg | `#CC646464` | `#CC6C6E64` |
| HeaderBarPressedBg | `#CC727272` | `#CC7E8074` |
| KeyBgAccent | `#CC4D4D4D` | `#CC575953` (무디한 티타늄) |
| KeyBgAccentHover | `#CC595959` | `#CC64665E` |
| KeyBgAccentPressed | `#CC404040` | `#CC4F514B` |
| KeyFgAccent | `#FFF5F5F5` | `#FFF2F3EF` (따뜻한 오프화이트) |
| KeyBorderAccent | `#80A2A2A2` | `#A0A0A398` (v6 — 일반 키 테두리와 명확히 구분되는 밝은 웜그레이. 측정 차이 10→32) |

**우드 — 라이트 (메이플)**

| 키 | 현재(플라스틱) | 우드 값 |
|---|---|---|
| KeyboardBg | `#CCE8E8EC` | `#CCF0E8D6` (페일 크림 메이플 — 오크보다 더 밝고 연함) |
| KeyBg | `#FFFFFFFF` | `#FFFEFBF4` (크림 화이트) |
| KeyBgHover | `#FFD4D4D8` | `#FFEFE7D5` |
| KeyBgPressed | `#FFBCBCC4` | `#FFE3D9C2` (페일 크림) |
| KeyBorder | `#1E000000` | `#3A342A12` (따뜻한 탄 — α 23% = 베이스 대비 약 1.9× / v11 수치 확인) |
| HeaderBarHoverBg | `#FFE2E2E7` | `#FFF2EBDD` |
| HeaderBarPressedBg | `#FFD4D4DC` | `#FFE5DAC4` |
| KeyBgAccent | `#FFF3F3F6` | `#FFF4EDDD` (페일 크림) |
| KeyBgAccentHover | `#FFE8E8ED` | `#FFF3EBD8` |
| KeyBgAccentPressed | `#FFDCDCE3` | `#FFEBDFC6` |
| KeyFgAccent | `#FF22242A` | `#FF2A2418` (따뜻한 거의-검정) |
| KeyBorderAccent | `#4A787C86` | `#4A54442E` (따뜻한 탄 — α 29%, 베이스와 동일) |

**우드 — 다크 (월넛)**

| 키 | 현재(플라스틱) | 우드 값 |
|---|---|---|
| KeyboardBg | `#CC1E1E1E` | `#CC4C3A28` (딥 월넛 — 다크 오크보다 밝고 붉음) |
| KeyBg | `#CC3C3C3C` | `#CC615038` (미디엄 월넛) |
| KeyBgHover | `#CC505050` | `#CC75624A` |
| KeyBgPressed | `#FF2D2D2D` | `#FF54432E` |
| KeyBorder | `#0FFFFFFF` | `#1FFFE8C8` (월넛 골드 — α 12% = 베이스 대비 약 2.1× / v11 수치 확인) |
| HeaderBarHoverBg | `#CC646464` | `#CC7A6750` |
| HeaderBarPressedBg | `#CC727272` | `#CC8B7960` |
| KeyBgAccent | `#CC4D4D4D` | `#CC726149` (월넛) |
| KeyBgAccentHover | `#CC595959` | `#CC7E6B53` |
| KeyBgAccentPressed | `#CC404040` | `#CC675641` |
| KeyFgAccent | `#FFF5F5F5` | `#FFF5EFE4` (크림 화이트) |
| KeyBorderAccent | `#80A2A2A2` | `#8A9E8258` (월넛 골드, 강화) |

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
| `AltKey/Themes/LightMetalMaterial.xaml` | 신규 — 메탈(라이트) 12개 브러시 (기본 7 + 액센트 5) |
| `AltKey/Themes/DarkMetalMaterial.xaml` | 신규 — 메탈(다크) 12개 브러시 |
| `AltKey/Themes/LightWoodMaterial.xaml` | 신규 — 우드(라이트) 12개 브러시 |
| `AltKey/Themes/DarkWoodMaterial.xaml` | 신규 — 우드(다크) 12개 브러시 |
| `AltKey/Themes/LightPlasticMaterial.xaml` | 신규 — 플라스틱(라이트, 유리/세라믹) 12개 브러시 |
| `AltKey/Themes/DarkPlasticMaterial.xaml` | 신규 — 플라스틱(다크, 탄소섬유) 12개 브러시 |
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
2. 라이트 + 메탈: 키보드 컨테이너·키캡이 차가운 회색, 테두리가 더 또렷해 보이는지.
3. 다크 + 우드: 따뜻한 어두운 계열인지, 키 테두리가 베이스보다 약 2배 강한 밝은 골드 톤인지 (v11 수치: "현재보다 약함"이 아니라 강화).
4. 재질 적용 상태에서 클립보드·빠른 메모 패널 열기: 키보드와 같은 재질(색)로 보이는지.
5. 고대비 + 메탈(또는 우드) 설정 상태: 화면은 고대비 그대로 유지되는지. 설정 창에서 재질 라디오가 비활성 + 힌트 문구 표시되는지.
6. 시스템 테마 모드에서 OS를 고대비로 전환: 재질이 자동 무시되는지, 설정 창이 비활성화 전환되는지.
7. 재질 변경 후 레이아웃 편집기(AltKey.Tools) 열기: 도구 창에도 같은 재질이 적용되는지.
8. 재질 변경 후 앱 재시작: 값이 유지되는지 (config.json에 `Material` 저장).
9. 색값 조정 반복: 2~4번 항목에서 색이 의도대로 안 나오면 §3.3 시작값을 이 문서에 갱신.

## 7. 가정·오픈 질문

- **가정(범위)**: v1 적용 대상 = 키보드 창 표면 + 클립보드·빠른 메모 패널(`KeyboardBg` 등 공유 브러시로 자동 반영). 설정 화면은 테마 전용으로 둔다.
- **가정(v4 변경, v5 확정)**: 플라스틱(기본값)에 유리/세라믹·탄소섬유 마감이 들어감에 따라 **"플라스틱 = 현재 모습" 성립하지 않음** — 수동 검증 결과 기본과 흡사하게 느껴져서 **무마감("기본") 카테고리를 두지 않기로 결정**.
- **오픈 질문**: 메탈·우드 시작색은 §3.3의 제안값이다. 수동 검증 2~4번 항목에서 조정 후 이 문서에 확정값으로 갱신할 예정이다.

## 8. 구현 기록 (2026-11-22)

- 스펙대로 구현 완료: 재질 XAML 4개 + `AppConfig.Material` + `ThemeService`(순수 함수 `ResolveResourceFiles`, 고대비 안전망, 파일 누락 시 건너뛰기) + 설정 UI(재질 섹션·고대비 비활성화+힌트) + 테스트.
- 세부 결정: 재질 라디오 3개는 외형 탭 TabIndex 14~16을 쓰고, 기존 컨트롤들(항상 위에 표시~사용자 단어 편집기)은 +3 이동(17~27).
- `ThemeServiceTests.cs` 6개 통과, 전체 스위트 380개 통과.
- 수동 검증 결과(§6)에 따라 §3.3 시작값을 확정값으로 갱신할 것.
- **색값 v2 (2026-11-22, 첫 피드백 반영)**: "플라스틱과 메탈 차이가 거의 없다"는 확인 → 재질 컨셉을 명확히 재정의. 메탈=알루미늄 하우징(라이트 anodized silver / 다크 스페이스 그레이), 우드=오크(라이트 honey cream / 다크 walnut-oak). §3.3 표를 v2 값으로 갱신, 4개 XAML 파일 반영. 샌드박스 번들(`dist/AltKey-sandbox`)로 수동 검증 대기.
- **색값 v3 (2026-11-22, 슬롯별 소재 선정)**: 알루미늄·오크를 라이트/다크로 나누는 대신 4개 슬롯에 각각 적절한 소재를 고름 — 메탈-라이트=**알루미늄**(v2 유지), 메탈-다크=**티타늄 건메탈**(스페이스 그레이 대체, 무디한 프리미엄 그레이), 우드-라이트=**메이플**(꿀-오크 대체, 페일 크림), 우드-다크=**월넛**(다크 오크 대체, 밝고 붉은 딥 브라운). §3.3 표 갱신 + 4개 XAML 파일 반영(주석도 소재명 포함) + 샌드박스 번들 재빌드. 후보 조사·제외 근거는 `materials-research.md`.
- **카테고리 v4 (2026-11-22, 플라스틱 = 합성 소재 계열, 테스트용)**: "유리/세라믹·탄소도 결국 플라스틱 일종"이라는 결정으로 기본 카테고리(플라스틱)에 라이트=**유리/세라믹**(frost + hairline 테두리), 다크=**탄소섬유**(중립 스텔스) 마감을 부여. `LightPlasticMaterial.xaml`·`DarkPlasticMaterial.xaml` 신규 + `ThemeService.ResolveResourceFiles`를 3 카테고리 균일 매핑으로 단순화(알 수 없는 값만 테마 단독) + 테스트 7개로 갱신(382 통과). **부작용: 기본값(플라스틱)의 첫 화면이 기존 무마감에서 유리/세라믹·탄소섬유로 바뀜** — 샌드박스 검증 후 유지 여부 결정. 제4 카테고리 후보는 `materials-research.md` §4 참고.
- **액센트 키 v5 (2026-11-22)**: "키 액센트도 재질에 적용되는지" 확인 결과 미적용 상태(테마 기본값 노출) → 6개 재질 파일 전부 **액센트 키 브러시 5개**(`KeyBgAccent`/`KeyBgAccentHover`/`KeyBgAccentPressed`/`KeyFgAccent`/`KeyBorderAccent`) 추가 — 파일당 7→12개. 값은 각 테마의 기본→액센트 명도 오프셋을 재질 톤에 적용. 상태 표시 색(`KeyBgSticky`/`KeyFgSticky`, `AccentBrush`)은 **재질과 무관하게 유지**(접근성: 고정·포커스 인식이 모든 재질에서 동일해야 함). §3.2·§3.3 갱신(플라스틱 표도 첫 추가). 무마감 카테고리 제외 확정(사용자 결정 — 기본 디자인과 흡사해서 별도 옵션 불필요).
- **패브릭 카테고리 + 메탈 액센트 테두리 보정 v6 (2026-11-22)**: 제4 카테고리로 **패브릭** 추가 — 라이트=**리넨 에크루**, 다크=**찰콜 직조**(조사 문서 §2.4). `LightFabricMaterial.xaml`·`DarkFabricMaterial.xaml` 신규(각 12개 브러시) + `ThemeService.ResolveResourceFiles`/ViewModel(`MaterialIsFabric`)/설정 UI 라디오("패브릭") 배선 + 테스트 3건 추가. **메탈 액센트 테두리 보정**: 전수 체크(테두리를 배경 위에 합성해 명도 차이 측정) 결과 메탈 라이트·다크의 액센트 테두리가 일반 키 테두리와 차이 10으로 거의 안 보임 — 라이트 `#5A6E7684`→`#8A525E6E`(차가운 스틸그레이), 다크 `#8A7D8072`→`#A0A0A398`(밝은 웜그레이)로 보정(차이 10→약 32). 나머지 재질(차이 19~43, 액센트 글자 대비 5.2~15.2)은 기준 충족이라 유지. **TabIndex**: 패브릭 라디오=17을 넣고 기존 컨트롤(17 이상)을 +1 이동해 중복 해소.
- **구리로의 패브릭 교체 v7 (2026-11-22)**: 샌드박스 수동 검증에서 "패브릭이 우드(라이트)와 거의 비슷해서 의미 없는 수준" 확인 → 제4 카테고리를 **구리**로 교체. 라이트=**밝은 코퍼**(따뜻한 샴페인 톤), 다크=**에스프레소 브론즈**(어둡고 붉은 코퍼). `LightCopperMaterial.xaml`·`DarkCopperMaterial.xaml` 신규(각 12개 브러시) + 패브릭 XAML 2개 삭제 + `ThemeService.ResolveResourceFiles`/ViewModel(`MaterialIsCopper`)/설정 UI 라디오("구리")/테스트 배선 교체. 실측(테두리 합성 차이): 라이트 35·다크 76 — 전수 최고로 우드(23/22)와 명확히 구분, 글자 대비 14.2/10.4(WCAG AA 충족). 패브릭 값은 이 문서의 v6 기록으로만 남김.
- **제4 카테고리 제거 v8 (2026-11-22)**: "소재가 아닌 완전히 다른 유형의 카테고리" 브레인스토밍(액센트 색·키 밀도/크기·모서리 원형화·투명도·목적 프리셋) 결과 이 기능 범위(플랫 브러시 오버라이드 + 설정 화면)에 맞는 후보 없음 → **구리 카테고리 제거**. `LightCopperMaterial.xaml`·`DarkCopperMaterial.xaml` 삭제 + ThemeService/ViewModel(`MaterialIsCopper`)/설정 UI 라디오("구리")/테스트 3건 배선 제거, TabIndex 복원(17→기존 컨트롤로). 구리 값(밝은 코퍼/에스프레소 브론즈)은 `materials-research.md` §2.5에 후보로 보존. 키 밀도·모서리 원형화는 독립 기능으로 검토 대상.
- **메탈 라이트 액센트 채우기 보정 v9 (2026-11-22)**: 수동 검증에서 "메탈 라이트 액센트가 약해 보인다" → 수치 체크(테두리 제외) 결과 정지 상태 일반키 대비 ΔL 0.058로 전 재질 중 가장 약함(원본 테마 0.102 / 플라스틱 0.072 / 우드 0.116). 글자 대비(14.0)·hover/pressed 단계는 무리 없음 — 정지 채우기가 일반키에 너무 가깝다는 문제. 원본 테마 수준(ΔL≈0.097)으로 `KeyBgAccent`/`Hover`/`Pressed`를 진하게 조정(`#FFF1F3F8→#FFEDEEF3`, `#FFDCE4EF→#FFD7DFEA`, `#FFC9D3E2→#FFC4CDDC`), 기존 hover/pressed 단계 유지. 글자 대비 14.0→13.5(여전 AAA). §3.3 메탈 라이트 표 갱신.
- **플라스틱 라이트 테두리 보정 v10 (2026-11-22)**: 전 재질 테두리 합성 명도 측정에서 플라스틱 라이트 일반키 테두리(ΔL*=3.5)가 원본 테마(10.5)의 약 1/3, 메탈(22.3)·우드(16.8)의 절반 이하로 정지 상태에서 경계선이 거의 사라짐 → `KeyBorder` `#0A000000`(6%)→`#14000000`(8%)로 보정, 합성 ΔL*=7. 여전히 전 재질 중 가장 약한 hairline(메탈 22.3 / 우드 16.8)이라 "유리/세라믹 무접선" 의도는 유지. 액센트 테두리(ΔL*=10.2, 일반키와 L*차 9.6)는 무리 없음 — 그대로. §3.3 플라스틱 라이트 표 갱신.
- **다크 테두리 α 배율 일치 v11 (2026-11-22)**: "수치 기준으로 정리" 요청 → 6개 슬롯 전수 수치화(테두리 α, 베이스 대비 배율, 배경 합성 ΔL*). 규칙 = **재질 정체성은 테두리 α 배율(베이스 테마 대비)로 정의하고 라이트/다크 동일하게 유지**. 측정 결과: 우드는 이미 일관(1.93×/2.06×), 비일관은 **플라스틱 다크**(라이트 0.67× vs 다크 2.73× — hairline 의도와 정반대)와 **메탈 다크**(2.13× vs 2.73×). 보정: 플라스틱 다크 `#29FFFFFF`→`#0AFFFFFF`(0.66×, ΔL* 13.8→3.6), 메탈 다크 `#29FFFFFF`→`#20FFFFFF`(2.13× 정확 일치, ΔL* 12.3→9.8). 우드·라이트 값은 그대로(이미 일관). 변경 후 다크 순위 = 베이스(4.8) > 플라스틱(3.6) < 우드(7.5) < 메탈(9.8)으로, 라이트(베이스 10.5 > P 7.0 < W 16.8 < M 22.3)와 동일한 형태 — v10 이전 "다크에서 플라스틱이 가장 강한 테두리"던 역전 해소. 부수: 우드 테두리는 실측상 베이스 대비 약 2배 **강화**였는데 문서/XAML에 "완화"로 잘못 표기돼 있었음 → 수치 확인 결과로 수정(값은 변경 없음). 플라스틱 다크 액센트 테두리 식별성(일반/액센트 합성 L*차)은 9.1→4.9로 낮아지나, hairline 정체성과 정지 채우기·글자 대비가 주 단서라 허용. §3.3 표·§2 표·§6 체크리스트 갱신.