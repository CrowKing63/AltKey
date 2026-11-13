<#
.SYNOPSIS
    브랜드 아이콘 파생 에셋 일괄 적용.
    512 마스터(docs\assets\icon-concepts\altkey-icon-final-512.png)와
    Tools 마스터(altkey-tools-v1-lavender-slider.png)에서 모든 배포 에셋을 생성한다.

.DESCRIPTION
    대상:
      - AltKey\Assets\icon.ico        (멀티사이즈 PNG 엔트리 16/32/48/256)
      - AltKey\Assets\icon.png        (512 마스터 — README에 임베드)
      - AltKey\Assets\tray-icon.ico   (DIB 16/32 — TrayService의 System.Drawing.Icon 로드 호환)
      - AltKey\Assets\logo.png        (256 — 설정 창 헤더 로고, csproj Resource)
      - AltKey.Tools\Assets\tools-icon.png (256)
      - AltKey.Tools\Assets\tools-icon.ico (DIB 16/32/48/256)
      - msix\Assets\Square150x150Logo.png / Square44x44Logo.png / StoreLogo.png
      - docs\assets\altkey-logo.png   (index.html 브랜트 마크)
      - Wiki\AltKey.wiki\altkey-logo.png (위키 홈 로고)

    .ico는 두 방식이 혼재한다:
      - icon.ico / tools-icon.ico: WPF ApplicationIcon은 PNG 엔트리를 지원하므로
        build-final-icon.ps1의 PNG 엔트리 파일을 그대로 사용한다.
      - tray-icon.ico: System.Drawing.Icon(.NET Framework)은 PNG 압축 ICO를
        안정적으로 못 로드하므로 DIB(BMP) 형식으로 인코딩한다 (기존 create-icon.ps1 방식).

.EXAMPLE
    .\scripts\apply-brand-assets.ps1
#>
Add-Type -AssemblyName System.Drawing

$root   = Split-Path -Parent $PSScriptRoot
$master = Join-Path $root "docs\assets\icon-concepts\altkey-icon-final-512.png"
$toolsMaster = Join-Path $root "docs\assets\icon-concepts\altkey-tools-v1-lavender-slider.png"

# --- 다운샘플 도우미 (GDI+ HighQualityBicubic) ---
function Downsample([string]$src, [int]$size) {
    $img = [System.Drawing.Image]::FromFile($src)
    $bmp = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.DrawImage($img, 0, 0, $size, $size)
    $g.Dispose(); $img.Dispose()
    return $bmp
}

# --- DIB 형식 ICO 저장 (TrayService의 System.Drawing.Icon 로드과 호환) ---
function Save-IcoDib([string]$icoPath, [System.Drawing.Bitmap[]]$bitmaps) {
    $sizes  = @($bitmaps | ForEach-Object { $_.Width })
    $ms     = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($ms)
    $dibStreams = [System.IO.MemoryStream[]]::new($bitmaps.Count)

    for ($i = 0; $i -lt $bitmaps.Count; $i++) {
        $bmp = $bitmaps[$i]; $sz = $sizes[$i]
        $ds  = [System.IO.MemoryStream]::new()
        $dw  = [System.IO.BinaryWriter]::new($ds)
        # BITMAPINFOHEADER (40 bytes) — height는 2배(AND mask 영역 포함)로 기록
        $dw.Write([uint32]40); $dw.Write([int32]$sz); $dw.Write([int32]($sz * 2))
        $dw.Write([uint16]1);  $dw.Write([uint16]32); $dw.Write([uint32]0)
        $dw.Write([uint32]0);  $dw.Write([int32]0);   $dw.Write([int32]0)
        $dw.Write([uint32]0);  $dw.Write([uint32]0)
        # XOR mask (bottom-up BGRA)
        for ($row = $sz - 1; $row -ge 0; $row--) {
            for ($col = 0; $col -lt $sz; $col++) {
                $px = $bmp.GetPixel($col, $row)
                $dw.Write([byte]$px.B); $dw.Write([byte]$px.G); $dw.Write([byte]$px.R); $dw.Write([byte]$px.A)
            }
        }
        # AND mask (전부 0 — 알파 채널 사용)
        $andRowBytes = [int][Math]::Ceiling($sz / 8.0) * 4
        $dw.Write([byte[]]::new($andRowBytes * $sz))
        $dw.Flush()
        $dibStreams[$i] = $ds
        $bmp.Dispose()
    }

    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $dataOffset = [uint32](6 + 16 * $sizes.Count)
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $sz = $sizes[$i]
        $szByte = if ($sz -eq 256) { [byte]0 } else { [byte]$sz }
        $writer.Write($szByte); $writer.Write($szByte); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$dibStreams[$i].Length); $writer.Write([uint32]$dataOffset)
        $dataOffset += [uint32]$dibStreams[$i].Length
    }
    foreach ($ds in $dibStreams) { $ds.Position = 0; $ds.CopyTo($ms); $ds.Dispose() }
    $writer.Flush()
    [System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
    $ms.Dispose(); $writer.Dispose()
}

# --- 1) 메인 앱 에셋 ---
Copy-Item (Join-Path $root "docs\assets\icon-concepts\altkey-icon-final.ico") (Join-Path $root "AltKey\Assets\icon.ico") -Force
Copy-Item $master (Join-Path $root "AltKey\Assets\icon.png") -Force
Write-Host "OK: AltKey\Assets\icon.ico, icon.png"

# --- 2) tray-icon.ico (16/32 DIB — TrayService가 로드하는 트레이 아이콘) ---
$t32 = Downsample $master 32
$t16 = Downsample $master 16
Save-IcoDib (Join-Path $root "AltKey\Assets\tray-icon.ico") @($t32, $t16)
Write-Host "OK: AltKey\Assets\tray-icon.ico (16/32)"

# --- 3) 설정 창 로고 (256 — csproj Resource) ---
$logo = Downsample $master 256
$logo.Save((Join-Path $root "AltKey\Assets\logo.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$logo.Dispose()
Write-Host "OK: AltKey\Assets\logo.png (256)"

# --- 4) Tools 앱 아이콘 (tools-icon.png 256 + tools-icon.ico DIB 16/32/48/256) ---
$tpng = Downsample $toolsMaster 256
$tpng.Save((Join-Path $root "AltKey.Tools\Assets\tools-icon.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$tpng.Dispose()
$b16 = Downsample $toolsMaster 16; $b32 = Downsample $toolsMaster 32
$b48 = Downsample $toolsMaster 48; $b256 = Downsample $toolsMaster 256
Save-IcoDib (Join-Path $root "AltKey.Tools\Assets\tools-icon.ico") @($b16, $b32, $b48, $b256)
Write-Host "OK: AltKey.Tools\Assets\tools-icon.png, tools-icon.ico"

# --- 5) MSIX 로고 (Square150 / Square44 / StoreLogo 50) ---
$s150 = Downsample $master 150
$s150.Save((Join-Path $root "msix\Assets\Square150x150Logo.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$s150.Dispose()
$s44 = Downsample $master 44
$s44.Save((Join-Path $root "msix\Assets\Square44x44Logo.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$s44.Dispose()
$s50 = Downsample $master 50
$s50.Save((Join-Path $root "msix\Assets\StoreLogo.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$s50.Dispose()
Write-Host "OK: msix\Assets (150/44/50)"

# --- 6) 웹(index.html)·위키용 로고 PNG (512 마스터 원본) ---
Copy-Item $master (Join-Path $root "docs\assets\altkey-logo.png") -Force
Copy-Item $master (Join-Path $root "Wiki\AltKey.wiki\altkey-logo.png") -Force
Write-Host "OK: docs\assets\altkey-logo.png, Wiki\AltKey.wiki\altkey-logo.png"

Write-Host "Done." -ForegroundColor Green
