param(
    [string]$Version  = "0.1.0",
    # CI에서는 이미 publish된 폴더를 넘겨줌. 없으면 직접 빌드.
    [string]$BuildDir = ""
)

$ErrorActionPreference = "Stop"
$root    = Split-Path $PSScriptRoot -Parent
# 태그에서 앞의 'v'를 제거 (v0.1.0 → 0.1.0)
$Version = $Version.TrimStart('v')
$out     = Join-Path $root "dist/AltKey-Portable-v$Version"

New-Item -ItemType Directory -Force $out | Out-Null

if ($BuildDir -and (Test-Path $BuildDir)) {
    # CI 경로: 이미 publish된 결과물 복사
    Write-Host "Copying from pre-built: $BuildDir" -ForegroundColor Cyan
    Copy-Item -Recurse "$BuildDir/*" "$out/" -Force
} else {
    # 로컬 경로: pubxml 프로파일로 직접 빌드
    Write-Host "Building portable single-file..." -ForegroundColor Cyan
    Push-Location (Join-Path $root "AltKey")
    dotnet publish `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishReadyToRun=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o $out
    Pop-Location
}

# 레이아웃 에셋 복사
$layoutsSrc = Join-Path $root "AltKey/layouts"
if (Test-Path $layoutsSrc) {
    Copy-Item -Recurse $layoutsSrc "$out/layouts" -Force
}

# 포터블 모드 트리거용 config.json 생성 (exe 옆에 존재해야 포터블 모드)
$defaultConfig = @{
    version        = "1.0.0"
    language       = "ko"
    default_layout = "qwerty-ko"
} | ConvertTo-Json -Depth 2
$defaultConfig | Out-File -Encoding UTF8 "$out/config.json"

# ZIP 압축
$zipPath = Join-Path $root "dist/AltKey-Portable-v$Version.zip"
Write-Host "Compressing to $zipPath ..." -ForegroundColor Cyan
Compress-Archive -Path "$out/*" -DestinationPath $zipPath -Force

Write-Host "✅ dist/AltKey-Portable-v$Version.zip 생성 완료" -ForegroundColor Green
