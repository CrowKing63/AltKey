# AI 텍스트 처리 + 상단바 동적 버튼 시스템 — 구현 계획 v2

## 개요

사용자가 **외부 앱에서 텍스트를 선택** → AltKey 상단바 `✨` 버튼 클릭 → **AI가 결과를 생성** → **선택 영역을 결과로 대체**하는 기능입니다.

동시에, 상단바의 버튼 배치를 **동적 시스템으로 리팩터링**하여 사용자가 표시/숨김·좌우 배치·순서를 조정할 수 있게 합니다.

---

## Part A: AI 텍스트 처리 (AiAction)

### A-1. 동작 흐름 (replace 모드 전용)

```
1. 사용자가 외부 앱에서 텍스트를 드래그로 선택
2. AltKey 상단바의 ✨ 버튼 클릭
3. AltKey 내부:
   a. 현재 클립보드 내용을 백업
   b. Ctrl+C 전송 → 짧은 딜레이(100ms)
   c. 클립보드에서 선택 텍스트 획득
   d. AI API에 (시스템 프롬프트 + 선택 텍스트) 전송
   e. 응답 수신
   f. 클립보드에 AI 응답 설정 → Ctrl+V 전송
   g. 원래 클립보드 내용 복원 (짧은 딜레이 후)
4. 처리 중: 버튼 아이콘을 ⏳로 변경, 클릭 비활성화
5. 완료: LiveRegion "AI 처리 완료" 공지
6. 실패: LiveRegion "AI 처리 실패: {에러}" 공지
```

> [!NOTE]
> 클립보드 백업/복원을 통해 사용자의 기존 클립보드 내용을 보존합니다.

### A-2. AI 백엔드: OpenAI-Compatible Chat API (단일 클라이언트)

| 백엔드 | 엔드포인트 예시 |
|--------|---------------|
| OpenAI | `https://api.openai.com/v1/chat/completions` |
| Ollama | `http://localhost:11434/v1/chat/completions` |
| LM Studio | `http://localhost:1234/v1/chat/completions` |
| llama.cpp | `http://localhost:8080/v1/chat/completions` |

### A-3. API 키 보안: Windows DPAPI

```csharp
// NuGet: System.Security.Cryptography.ProtectedData
byte[] encrypted = ProtectedData.Protect(
    Encoding.UTF8.GetBytes(apiKey),
    entropy: Encoding.UTF8.GetBytes("AltKey.AiApiKey"),
    scope: DataProtectionScope.CurrentUser);

// config.json에는 Base64로 인코딩하여 저장
string stored = Convert.ToBase64String(encrypted);
```

- `DataProtectionScope.CurrentUser`: 현재 윈도우 사용자만 복호화 가능
- config.json에는 암호화된 Base64 문자열이 저장됨
- **설정 창에서 API 키 입력 시**: 평문 → DPAPI 암호화 → Base64 → 저장
- **API 호출 시**: 저장된 Base64 → 바이트 → DPAPI 복호화 → 평문 사용

### A-4. 설정 (`AppConfig` 추가 프로퍼티)

```
AiEnabled            : bool   = false
AiEndpoint           : string = ""               // 엔드포인트 URL
AiApiKeyEncrypted    : string = ""               // DPAPI 암호화 → Base64
AiModel              : string = ""               // 모델 이름
AiDefaultPrompt      : string = "다음 텍스트를 한국어로 간단히 요약해줘"
AiTimeoutSeconds     : int    = 30
```

> `AiDefaultResultMode`는 제거 (replace만 지원).

### A-5. 설정 창 — AI 탭 (인라인 프롬프트 편집)

설정 창에 **"AI" 탭**을 추가합니다:

```
┌──────────────────────────────────────────────┐
│ AI 텍스트 처리                                │
├──────────────────────────────────────────────┤
│ [✓] AI 기능 사용                              │
│                                              │
│ 엔드포인트 URL                                │
│ [http://localhost:11434/v1/chat/completions ] │
│                                              │
│ API 키 (로컬 서버는 비워두세요)                  │
│ [••••••••••••]  [보기/숨기기]                  │
│                                              │
│ 모델 이름                                     │
│ [llama3                                     ] │
│                                              │
│ 기본 프롬프트                                  │
│ ┌──────────────────────────────────────────┐ │
│ │다음 텍스트를 한국어로 간단히 요약해줘       │ │
│ │                                          │ │
│ │(여러 줄 작성 가능. 가상 키보드로 직접       │ │
│ │ 타이핑하거나 붙여넣기 가능)                 │ │
│ └──────────────────────────────────────────┘ │
│                                              │
│ 타임아웃 (초)                                  │
│ [◄ 30 ►]                                     │
│                                              │
│ [연결 테스트]                                  │
└──────────────────────────────────────────────┘
```

> [!IMPORTANT]
> 프롬프트 입력란은 **멀티라인 TextBox**로 구현합니다. 가상 키보드 자체로 프롬프트를 작성할 수 있도록 `AcceptsReturn=True`로 설정합니다. 설정 창이 열려 있는 상태에서 AltKey 키보드가 포커스를 잡을 수 있으므로, 이 TextBox에 직접 타이핑·붙여넣기가 자연스럽게 동작합니다.

---

## Part B: 상단바 동적 버튼 시스템

### B-1. 현재 문제점

현재 상단바 버튼들은 `HorizontalAlignment="Right"` + `Margin="0,0,264,0"` 같은 **절대 마진**으로 배치되어 있습니다. 이 방식은:
- 버튼 추가/제거/순서 변경이 불가능
- 상단바 너비가 바뀌면 버튼이 겹치거나 사라질 수 있음

### B-2. 새로운 구조

```
┌─────────────────────────────────────────────────────────────┐
│ [AltKey ●]  [사용자 좌측 버튼들...]   ┃   [사용자 우측 버튼들...]  [▲][▬][✕] │
└─────────────────────────────────────────────────────────────┘
     좌측 고정      좌측 구성 가능   드래그  우측 구성 가능    우측 고정
```

- **좌측 고정**: 앱 타이틀 + 업데이트 인디케이터 (현재 그대로)
- **좌측 구성 가능**: 사용자가 좌측에 배치하도록 선택한 버튼들
- **중앙**: 드래그 핸들 + 방향 이동 버튼 (현재 그대로)
- **우측 구성 가능**: 사용자가 우측에 배치하도록 선택한 버튼들
- **우측 고정**: 접기, 최소화, 닫기 (순서 고정, 항상 표시)

### B-3. 구성 가능한 버튼 목록 (기본 순서)

| ID | 아이콘 | 기능 | 기본 표시 | 기본 위치 |
|----|--------|------|----------|----------|
| `Clipboard` | 📋 | 클립보드 패널 | ✓ | 우측 |
| `Emoji` | 😊 | 이모지 패널 | ✓ | 우측 |
| `AutoComplete` | AC | 자동완성 토글 | ✓ | 우측 |
| `OsIme` | 한/영 | OS IME 전환 | ✓ | 우측 |
| `Osk` | ⌨ | 화면 키보드 | ✓ | 우측 |
| `Settings` | ⚙ | 설정 열기 | ✓ | 우측 |
| `Ai` | ✨ | AI 텍스트 처리 | ✗ | 우측 |

### B-4. 설정 데이터 모델

```csharp
// AppConfig에 추가
public List<HeaderButtonConfig> HeaderButtons { get; set; } = [];
```

```csharp
// Models/HeaderButtonConfig.cs (신규)
public class HeaderButtonConfig
{
    public string Id       { get; set; } = "";       // 버튼 식별자 (Clipboard, Emoji 등)
    public bool   Visible  { get; set; } = true;     // 표시 여부
    public string Position { get; set; } = "Right";  // "Left" 또는 "Right"
}
```

- `HeaderButtons` 리스트가 비어 있으면 기본값 적용 (마이그레이션)
- **리스트의 순서가 곧 버튼의 표시 순서**

### B-5. 설정 창 — 상단바 버튼 설정 섹션

"외형" 탭에 추가:

```
┌──────────────────────────────────────────────┐
│ 상단바 버튼 설정                               │
├──────────────────────────────────────────────┤
│ 표시할 버튼과 위치를 선택하세요.                  │
│ 위에서 아래 순서대로 상단바에 배치됩니다.          │
│                                              │
│ [✓] 📋 클립보드 패널    [◄ 우측 ►]  [▲][▼]    │
│ [✓] 😊 이모지 패널      [◄ 우측 ►]  [▲][▼]    │
│ [✓] AC 자동완성 토글    [◄ 우측 ►]  [▲][▼]    │
│ [✓] 한/영 OS IME 전환   [◄ 우측 ►]  [▲][▼]    │
│ [✓] ⌨ 화면 키보드       [◄ 우측 ►]  [▲][▼]    │
│ [✓] ⚙ 설정             [◄ 우측 ►]  [▲][▼]    │
│ [ ] ✨ AI 텍스트 처리    [◄ 우측 ►]  [▲][▼]    │
│                                              │
│ ※ 접기, 최소화, 닫기 버튼은 항상 우측 끝에       │
│   고정됩니다.                                  │
└──────────────────────────────────────────────┘
```

> [!NOTE]
> `[▲][▼]`는 순서 이동 버튼입니다. 위/아래로 이동하면 상단바에서의 순서가 즉시 바뀝니다.

### B-6. KeyboardView.xaml 상단바 리팩터링

현재 하드코딩된 버튼 7개를 **ItemsControl 기반**으로 변경합니다:

```xml
<!-- 좌측 구성 버튼 -->
<ItemsControl ItemsSource="{Binding HeaderButtonsLeft}"
              HorizontalAlignment="Left" Margin="...">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate><StackPanel Orientation="Horizontal"/></ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
        <DataTemplate><!-- HeaderButtonVm에 따라 아이콘/커맨드 렌더링 --></DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>

<!-- 우측 구성 버튼 -->
<ItemsControl ItemsSource="{Binding HeaderButtonsRight}" .../>

<!-- 우측 고정: 접기 / 최소화 / 닫기 (그대로 유지) -->
```

---

## 파일별 변경 내역

### Model

#### [MODIFY] [KeyAction.cs](file:///c:/Users/UITAEK/AltKey/AltKey/Models/KeyAction.cs)
- `AiAction` 레코드 추가: `Prompt`(string), `JsonDerivedType` 등록
- 레이아웃 편집기 통합은 **별도 문서**(향후 작업)에서 다룸

#### [MODIFY] [AppConfig.cs](file:///c:/Users/UITAEK/AltKey/AltKey/Models/AppConfig.cs)
- AI 설정 프로퍼티 6개 추가
- `HeaderButtons` 리스트 추가

#### [NEW] [HeaderButtonConfig.cs](file:///c:/Users/UITAEK/AltKey/AltKey/Models/HeaderButtonConfig.cs)
- `Id`, `Visible`, `Position` 프로퍼티

### Service

#### [NEW] [AiService.cs](file:///c:/Users/UITAEK/AltKey/AltKey/Services/AiService.cs)
- `HttpClient` 기반 OpenAI-compatible Chat API 클라이언트
- `Task<string> ProcessTextAsync(string input, string prompt, CancellationToken ct)`
- 요청 JSON 구조: `{ model, messages: [{ role: "system", content: prompt }, { role: "user", content: input }] }`
- 응답 파싱: `choices[0].message.content`
- 에러 핸들링: 타임아웃, HTTP 오류, JSON 파싱 실패

#### [NEW] [SecureStorage.cs](file:///c:/Users/UITAEK/AltKey/AltKey/Services/SecureStorage.cs)
- DPAPI 래퍼: `Encrypt(string plainText)` → Base64 / `Decrypt(string base64)` → 평문
- `System.Security.Cryptography.ProtectedData` 사용
- `DataProtectionScope.CurrentUser`
- 엔트로피: `"AltKey.SecureStorage"` 고정 문자열

### ViewModel

#### [MODIFY] [MainViewModel.cs](file:///c:/Users/UITAEK/AltKey/AltKey/ViewModels/MainViewModel.cs)
- `AiService` DI 주입
- `ExecuteAiCommand` (async): 텍스트 복사 → AI 호출 → 결과 붙여넣기
- `IsAiProcessing`: 로딩 상태 바인딩
- `AiEnabled`: AI 버튼 표시 여부
- `HeaderButtonsLeft` / `HeaderButtonsRight`: 구성 가능한 버튼 리스트 (ObservableCollection)
- 버튼 구성 변경 시 리스트 재구성 메서드

#### [MODIFY] [SettingsViewModel.cs](file:///c:/Users/UITAEK/AltKey/AltKey/ViewModels/SettingsViewModel.cs)
- AI 설정 바인딩 프로퍼티 (Endpoint, ApiKey 표시/입력, Model, DefaultPrompt, Timeout)
- `TestAiConnectionCommand`: 연결 테스트
- 상단바 버튼 설정 바인딩 (리스트, 순서 이동, 표시 토글, 위치 변경)

### View

#### [MODIFY] [KeyboardView.xaml](file:///c:/Users/UITAEK/AltKey/AltKey/Views/KeyboardView.xaml)
- 헤더 바를 3구역으로 분할: 좌측 고정+구성 | 중앙 드래그 | 우측 구성+고정
- 구성 버튼 영역: ItemsControl + DataTemplate (HeaderButtonVm 기반)
- 고정 버튼(접기/최소화/닫기)은 현재 위치 유지

#### [MODIFY] [SettingsWindow.xaml](file:///c:/Users/UITAEK/AltKey/AltKey/Views/SettingsWindow.xaml)
- "AI" 탭 추가 (엔드포인트, API 키, 모델, 멀티라인 프롬프트, 타임아웃, 연결 테스트)
- "외형" 탭에 상단바 버튼 설정 섹션 추가

### DI / 프로젝트

#### [MODIFY] [App.xaml.cs](file:///c:/Users/UITAEK/AltKey/AltKey/App.xaml.cs)
- `services.AddSingleton<AiService>()` 등록

#### [MODIFY] [AltKey.csproj](file:///c:/Users/UITAEK/AltKey/AltKey/AltKey.csproj)
- `<PackageReference Include="System.Security.Cryptography.ProtectedData" Version="8.*" />` 추가

---

## 향후 작업 (별도 문서)

### 1. 레이아웃 편집기 AiAction 할당
- `ActionBuilderViewModel`에 `AiAction` 타입 추가
- `ActionBuilderView.xaml`에 프롬프트 입력 UI
- 문서: `docs/feature-ai/FUTURE-layout-editor-action.md`

### 2. 스트리밍 응답 지원

**스트리밍 응답이란?**
- 일반(non-streaming) 방식: AI가 **전체 응답을 한 번에** 생성한 뒤 보내줍니다. 응답이 길면 기다리는 시간이 깁니다.
- 스트리밍(streaming) 방식: AI가 응답을 **한 글자씩(토큰 단위로)** 실시간 전송합니다. ChatGPT 채팅처럼 글자가 하나씩 나타나는 것이 이 방식입니다.

**AltKey에서의 의미:**
- 현재 구현(non-streaming): 전체 응답을 받은 뒤 한 번에 붙여넣기 → **간단하고 안정적**
- 스트리밍 구현 시: 글자가 도착할 때마다 실시간으로 입력 → **빠른 피드백, 하지만 구현이 복잡**
  - SSE(Server-Sent Events) 파싱, 부분 JSON 처리, 실시간 입력 상태 관리 필요
  - replace 모드에서는 원본을 먼저 지운 뒤 글자를 하나씩 입력하는 방식이 되어야 함

**결론:** 첫 구현은 non-streaming으로 진행합니다. 스트리밍은 사용감 확인 후 필요 시 추가합니다.
- 문서: `docs/feature-ai/FUTURE-streaming-response.md`

---

## 검증 계획

### 빌드
```
dotnet build C:\Users\UITAEK\AltKey\AltKey\AltKey.csproj
```

### 수동 테스트
1. Ollama 로컬 서버 연결 → 메모장에서 텍스트 선택 → ✨ 클릭 → 결과 대체 확인
2. 키보드 접힌 상태에서 동일 테스트
3. API 키 저장/로드 후 복호화 정상 확인
4. 잘못된 엔드포인트 → 에러 메시지 확인
5. 타임아웃 테스트
6. 상단바 버튼 순서 변경 → 즉시 반영 확인
7. 버튼 좌/우 위치 변경 확인
8. 앱 재시작 후 버튼 설정 유지 확인

### 단위 테스트
- `AiService` JSON 요청/응답 파싱
- `SecureStorage` 암호화/복호화 라운드트립
- `HeaderButtonConfig` 직렬화/역직렬화
- 기본값 마이그레이션 (빈 HeaderButtons → 기본 설정 생성)
