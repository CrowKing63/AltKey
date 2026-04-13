param([string]$Version = "0.1.0")

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$out  = Join-Path $root "dist/altkey-portable-v$Version"

New-Item -ItemType Directory -Force $out | Out-Null

# 빌드
Write-Host "Building portable single-file..." -ForegroundColor Cyan
Push-Location (Join-Path $root "AltKey")
dotnet publish -p:PublishProfile=portable -o $out
Pop-Location

# 레이아웃 에셋 복사
$layoutsSrc = Join-Path $root "AltKey/layouts"
if (Test-Path $layoutsSrc) {
    Copy-Item -Recurse $layoutsSrc "$out/layouts" -Force
}

# 테마 에셋 복사 (있는 경우)
$themesSrc = Join-Path $root "AltKey/Themes"
if (Test-Path $themesSrc) {
    Copy-Item -Recurse $themesSrc "$out/themes" -Force -ErrorAction SilentlyContinue
}

# 포터블 모드 트리거용 config.json 생성 (exe 옆에 존재해야 포터블 모드)
$defaultConfig = @{
    version        = "1.0.0"
    language       = "ko"
    default_layout = "qwerty-ko"
} | ConvertTo-Json -Depth 2
$defaultConfig | Out-File -Encoding UTF8 "$out/config.json"

# ZIP 압축
$zipPath = Join-Path $root "dist/altkey-portable-v$Version.zip"
Write-Host "Compressing to $zipPath ..." -ForegroundColor Cyan
Compress-Archive -Path "$out/*" -DestinationPath $zipPath -Force

Write-Host "✅ dist/altkey-portable-v$Version.zip 생성 완료" -ForegroundColor Green
