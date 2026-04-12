# AltKey

WPF 기반 키보드 레이아웃 커스터마이징 도구

## 사전 요구사항

- .NET 8 SDK
- Windows 10 22H2+ (Acrylic 효과 권장)

## 개발

```bash
cd AltKey/AltKey
dotnet run
```

## 빌드

```bash
dotnet build -c Release
```

## 배포

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## 알려진 제약

- SendInput API 사용 시 권한 제약으로 일부 앱/게임에서 키 입력 미작동
- 관리자 권한 앱에서는 SendInput 사용 불가

## 프로젝트 구조

```
AltKey/
├── Views/          # XAML 뷰
├── ViewModels/    # MVVM ViewModel
├── Models/        # 데이터 모델
├── Services/      # 핵심 서비스
├── Controls/      # 커스텀 컨트롤
├── Platform/      # Win32 P/Invoke
├── Themes/        # 테마 리소스
└── layouts/       # JSON 레이아웃 정의
```
