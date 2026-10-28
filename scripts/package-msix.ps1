param(
    [string]$Version  = "0.1.0",
    # CI에서는 이미 publish된 폴더(dist/publish)를 넘겨줌. exe + Tools/ 를 기대한다.
    [string]$BuildDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

# 태그에서 앞의 'v' 제거 (v1.1.12 → 1.1.12)
$Version = $Version.TrimStart('v')
# MSIX Identity Version은 반드시 4부(X.Y.Z.W)여야 하므로 부족하면 .0을 채운다
while ($Version.Split('.').Count -lt 4) { $Version += ".0" }

$staging = Join-Path $root "dist/msix-staging"
if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
New-Item -ItemType Directory -Force $staging | Out-Null

# 1. 메인 exe — 네이티브 DLL은 publish 시 IncludeNativeLibrariesForSelfExtract로 이미 번들링됨
$exe = Join-Path $BuildDir "AltKey.exe"
if (-not (Test-Path $exe)) { throw "AltKey.exe not found in $BuildDir" }
Copy-Item $exe $staging -Force

# 2. Tools 폴더 — 레이아웃·단어 편집기(별도 프로세스) 전체 복사
$tools = Join-Path $BuildDir "Tools"
if (-not (Test-Path $tools)) { throw "Tools folder not found in $BuildDir" }
Copy-Item -Recurse $tools (Join-Path $staging "Tools") -Force

# 3. layouts — tracked 기본 레이아웃
$layoutsSrc = Join-Path $root "AltKey/layouts"
New-Item -ItemType Directory -Force (Join-Path $staging "layouts") | Out-Null
Copy-Item (Join-Path $layoutsSrc "*.json") (Join-Path $staging "layouts") -Force

# 4. config.json — tracked 기본 설정
Copy-Item (Join-Path $root "AltKey/config.json") (Join-Path $staging "config.json") -Force

# 5. Assets — MSIX 로고(msix/Assets) + 앱 공용 에셋(emoji.json, Sounds/click.wav)
$assetsOut = Join-Path $staging "Assets"
New-Item -ItemType Directory -Force $assetsOut | Out-Null
Copy-Item (Join-Path $root "msix/Assets/*.png") $assetsOut -Force
Copy-Item (Join-Path $root "AltKey/Assets/emoji.json") $assetsOut -Force
$soundsOut = Join-Path $assetsOut "Sounds"
New-Item -ItemType Directory -Force $soundsOut | Out-Null
Copy-Item (Join-Path $root "AltKey/Assets/Sounds/click.wav") $soundsOut -Force

# 6. AppxManifest.xml — 태그 버전으로 Identity Version 치환 (4부 X.Y.Z.W)
# Get-Content -Raw는 시스템 기본 코드페이지(CP949 등)로 한글을 깨뜨뜨므로,
# 명시적 UTF-8 읽기/쓰기로 매니페스트의 한글 Description을 보존한다.
# MSIX Identity Version은 항상 4부(X.Y.Z.W)여야 하므로, 태그 버전이 3부면 .0을 붙인다.
$msixVersion = $Version
if ((($msixVersion -split '\.').Count) -eq 3) { $msixVersion = "$msixVersion.0" }
$manifestSrc = Join-Path $root "msix/AppxManifest.xml"
$manifestText = [System.IO.File]::ReadAllText($manifestSrc, [System.Text.Encoding]::UTF8)
$manifestText = $manifestText -replace 'Version="\d+\.\d+\.\d+\.\d+"', "Version=`"$msixVersion`""
[System.IO.File]::WriteAllText((Join-Path $staging "AppxManifest.xml"), $manifestText, [System.Text.Encoding]::UTF8)

# 7. MakeAppx 위치 탐색 — Windows SDK App Certification Kit
$makeappx = "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\MakeAppx.exe"
if (-not (Test-Path $makeappx)) {
    $found = Get-ChildItem "C:\Program Files (x86)\Windows Kits\" -Recurse -Filter "MakeAppx.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { $makeappx = $found.FullName }
}
if (-not (Test-Path $makeappx)) { throw "MakeAppx.exe not found in Windows Kits" }

# 8. 팩킹 — 스토어 제출용은 서명 없이 만든다(마이크로소프트가 재서명)
# 기존 .msix가 있으면 MakeAppx가 "덮어쓸까요?" GUI 대화상자를 띄워 멈추므로,
# 먼저 삭제하고 /o(overwrite) 플래그도 붙여 비대화형으로 팩킹한다.
$msixOut = Join-Path $root "dist/AltKey-$Version.msix"
if (Test-Path $msixOut) { Remove-Item -Force $msixOut }
& $makeappx pack /d $staging /p $msixOut /o /nv
if ($LASTEXITCODE -ne 0) { throw "MakeAppx pack failed (exit $LASTEXITCODE)" }

Write-Host "OK $msixOut created" -ForegroundColor Green
