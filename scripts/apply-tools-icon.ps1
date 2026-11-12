<#
.SYNOPSIS
    AltKey.Tools 최종 아이콘 적용: V1(라벤더 슬라이더) 마스터에서 파생 에셋 생성.
      - AltKey.Tools\Assets\tools-icon.png  (256)
      - AltKey.Tools\Assets\tools-icon.ico  (PNG 엔트리 16/32/48/256)
    기존 build-final-icon.ps1의 ICO 조립 로직을 재사용한다.
#>
Add-Type -AssemblyName System.Drawing

$rootPath = Split-Path -Parent $PSScriptRoot
$masterPath = Join-Path $rootPath "docs\assets\icon-concepts\altkey-tools-v1-lavender-slider.png"
$toolsAssets = Join-Path $rootPath "AltKey.Tools\Assets"

# --- 1. 256 마스터를 tools-icon.png로 복사. ---
$pngOut = Join-Path $toolsAssets "tools-icon.png"
Copy-Item -Path $masterPath -Destination $pngOut -Force
Write-Host "tools-icon.png (256) -> $pngOut"

# --- 2. 48/32/16 다운스케일. ---
$master = [System.Drawing.Bitmap]::FromFile($masterPath)
function Save-DownscaledPng {
    param([System.Drawing.Bitmap]$Master, [int]$Size, [string]$OutputPath)
    $bmp = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($Master, 0, 0, $Size, $Size)
    $bmp.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()
}

$smallSizes = @(48, 32, 16)
$pngPaths = @{ 256 = $masterPath }
foreach ($s in $smallSizes) {
    $p = Join-Path $env:TEMP ("tools-icon-{0}.png" -f $s)
    Save-DownscaledPng -Master $master -Size $s -OutputPath $p
    $pngPaths[$s] = $p
}
$master.Dispose()

# --- 3. 멀티사이즈 .ico 조립 (PNG 엔트리). ---
$icoSizes = @(16, 32, 48, 256)
$entries = @()
foreach ($s in $icoSizes) {
    $entries += @{ Size = $s; Bytes = [System.IO.File]::ReadAllBytes($pngPaths[$s]) }
}

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)
$bw.Write([uint16]0)                # reserved
$bw.Write([uint16]1)                # type (icon)
$bw.Write([uint16]$entries.Count)   # image count

$dataStart = 6 + ($entries.Count * 16)
$offsets = @()
$running = $dataStart
foreach ($e in $entries) {
    $offsets += $running
    $running += $e.Bytes.Length
}

for ($i = 0; $i -lt $entries.Count; $i++) {
    $e = $entries[$i]
    $dim = if ($e.Size -ge 256) { 0 } else { [byte]$e.Size }   # 0 means 256
    $bw.Write([byte]$dim)
    $bw.Write([byte]$dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$e.Bytes.Length)
    $bw.Write([uint32]$offsets[$i])
}

foreach ($e in $entries) {
    $bw.Write($e.Bytes)
}
$bw.Flush()
$icoBytes = $ms.ToArray()
$bw.Dispose()
$ms.Dispose()

$icoOut = Join-Path $toolsAssets "tools-icon.ico"
[System.IO.File]::WriteAllBytes($icoOut, $icoBytes)
foreach ($s in $smallSizes) { Remove-Item (Join-Path $env:TEMP ("tools-icon-{0}.png" -f $s)) -Force }

Write-Host "tools-icon.ico -> $icoOut ($($icoBytes.Length) bytes, entries: $($icoSizes -join '/'))"
Write-Host "Done." -ForegroundColor Green
