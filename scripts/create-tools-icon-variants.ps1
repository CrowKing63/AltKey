<#
.SYNOPSIS
    AltKey.Tools 아이콘 후보 렌더링.
    V1: 잉크 키캡 + 라벤더 슬라이더 글리프 (추천 — family 유지, 기능 구분)
    V2: 잉크 키캡 + 흰 슬라이더 글리프
    V3: 잉크 키캡 + 민트 "Alt" (액센트만 교체 옵션)
    각 후보 256 마스터 + 비교 보드(256/48/32/16 다운스케일) 생성.
#>
Add-Type -AssemblyName System.Drawing

$rootPath = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $rootPath "docs\assets\icon-concepts"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# Palette (ASCII-safe for PS 5.1 cp949).
$ink            = [System.Drawing.Color]::FromArgb(255, 28, 25, 23)    # #1C1917
$inkDeep        = [System.Drawing.Color]::FromArgb(255, 12, 10, 9)     # #0C0A09
$white          = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
$pastelLavender = [System.Drawing.Color]::FromArgb(255, 196, 181, 253) # #C4B5FD
$pastelMint     = [System.Drawing.Color]::FromArgb(255, 167, 243, 208) # #A7F3D0

function New-RoundedRectPath {
    param([System.Drawing.RectangleF]$Rect, [float]$Radius)
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $Radius * 2
    $path.AddArc($Rect.X, $Rect.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rect.X, $Rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Initialize-Canvas {
    param([int]$Size)
    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)
    return @{ Bitmap = $bitmap; Graphics = $graphics }
}

function Add-Keycap {
    param([System.Drawing.Graphics]$Graphics, [int]$Size, [System.Drawing.Color]$KeyFill, [System.Drawing.Color]$BaseFill)
    $silhouette = [System.Drawing.RectangleF]::new($Size * 0.03, $Size * 0.03, $Size * 0.94, $Size * 0.94)
    $basePath = New-RoundedRectPath -Rect $silhouette -Radius ($Size * 0.18)
    $baseBrush = [System.Drawing.SolidBrush]::new($BaseFill)
    $Graphics.FillPath($baseBrush, $basePath)
    $baseBrush.Dispose(); $basePath.Dispose()

    $keyRect = [System.Drawing.RectangleF]::new($Size * 0.03, $Size * 0.03, $Size * 0.94, $Size * 0.87)
    $keyPath = New-RoundedRectPath -Rect $keyRect -Radius ($Size * 0.18)
    $keyBrush = [System.Drawing.SolidBrush]::new($KeyFill)
    $Graphics.FillPath($keyBrush, $keyPath)
    $keyBrush.Dispose(); $keyPath.Dispose()
    return $keyRect
}

# Slider glyph: three horizontal rounded lines with knobs at varied x positions.
# The standard "settings/adjustment" symbol — legible even at 16px.
function Add-SliderGlyph {
    param([System.Drawing.Graphics]$Graphics, [int]$Size, [System.Drawing.RectangleF]$KeyRect, [System.Drawing.Color]$Color)

    $boxW = $Size * 0.506
    $boxH = $Size * 0.36
    $x0 = $KeyRect.X + ($KeyRect.Width - $boxW) / 2
    $y0 = $KeyRect.Y + ($KeyRect.Height - $boxH) / 2

    $lineThick = $Size * 0.054
    $knobR     = $Size * 0.075
    # Knob x offsets (fraction of boxW) per line — varied so it reads as "sliders", not "list".
    $knobFx = @(0.30, 0.68, 0.45)

    $pen   = [System.Drawing.Pen]::new($Color, $lineThick)
    $pen.StartCap  = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap    = [System.Drawing.Drawing2D.LineCap]::Round
    $brush = [System.Drawing.SolidBrush]::new($Color)

    for ($i = 0; $i -lt 3; $i++) {
        $y = $y0 + $boxH * (0.18 + 0.32 * $i)
        $Graphics.DrawLine($pen, [float]$x0, [float]$y, [float]($x0 + $boxW), [float]$y)
        $kx = $x0 + $boxW * $knobFx[$i]
        $Graphics.FillEllipse($brush, [float]($kx - $knobR), [float]($y - $knobR), $knobR * 2, $knobR * 2)
    }

    $pen.Dispose()
    $brush.Dispose()
}

# "Alt" label (same technique as the main icon): full word in ColorT, then "Al" overdrawn in ColorAl.
function Add-AltLabel {
    param([System.Drawing.Graphics]$Graphics, [int]$Size, [System.Drawing.RectangleF]$KeyRect,
          [System.Drawing.Color]$ColorAl, [System.Drawing.Color]$ColorT)
    $fontSize = $Size * 0.34
    $font = [System.Drawing.Font]::new("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $format = [System.Drawing.StringFormat]::new()
    $wFull = $Graphics.MeasureString([string]"Alt", [System.Drawing.Font]$font, [System.Drawing.SizeF]::Empty, [System.Drawing.StringFormat]$format).Width
    $startX = ($Size - $wFull) / 2
    $cellH = $font.GetHeight($Graphics)
    $y = $KeyRect.Y + ($KeyRect.Height - $cellH) / 2

    $brushAl = [System.Drawing.SolidBrush]::new($ColorAl)
    $brushT  = [System.Drawing.SolidBrush]::new($ColorT)
    $Graphics.DrawString([string]"Alt", [System.Drawing.Font]$font, [System.Drawing.Brush]$brushT, [float]$startX, [float]$y, [System.Drawing.StringFormat]$format)
    $Graphics.DrawString([string]"Al",  [System.Drawing.Font]$font, [System.Drawing.Brush]$brushAl, [float]$startX, [float]$y, [System.Drawing.StringFormat]$format)
    $brushAl.Dispose(); $brushT.Dispose(); $format.Dispose(); $font.Dispose()
}

function Save-MasterPng {
    param([int]$Size, [string]$OutputPath, [scriptblock]$DrawLabel)
    $canvas = Initialize-Canvas -Size $Size
    $keyRect = Add-Keycap -Graphics $canvas.Graphics -Size $Size -KeyFill $ink -BaseFill $inkDeep
    & $DrawLabel $canvas.Graphics $Size $keyRect
    $canvas.Bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Graphics.Dispose()
    $canvas.Bitmap.Dispose()
}

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

# --- Render the three 256 masters. ---
$v1 = { param($g,$s,$k) Add-SliderGlyph -Graphics $g -Size $s -KeyRect $k -Color $pastelLavender }
$v2 = { param($g,$s,$k) Add-SliderGlyph -Graphics $g -Size $s -KeyRect $k -Color $white }
$v3 = { param($g,$s,$k) Add-AltLabel    -Graphics $g -Size $s -KeyRect $k -ColorAl $pastelMint -ColorT $white }

$m1 = Join-Path $outputDir "altkey-tools-v1-lavender-slider.png"
$m2 = Join-Path $outputDir "altkey-tools-v2-white-slider.png"
$m3 = Join-Path $outputDir "altkey-tools-v3-mint-alt.png"
Save-MasterPng -Size 256 -OutputPath $m1 -DrawLabel $v1
Save-MasterPng -Size 256 -OutputPath $m2 -DrawLabel $v2
Save-MasterPng -Size 256 -OutputPath $m3 -DrawLabel $v3
Write-Host "Masters: v1/v2/v3 (256)"

# --- Comparison board: 3 columns x rows of 256/48/32/16. ---
$margin = 24
$sizes  = @(256, 48, 32, 16)
$boardW = 3 * 256 + 4 * $margin
$boardH = ($sizes | Measure-Object -Sum).Sum + 5 * $margin
$board = [System.Drawing.Bitmap]::new($boardW, $boardH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$bg = [System.Drawing.Graphics]::FromImage($board)
$bg.Clear([System.Drawing.Color]::FromArgb(255, 245, 243, 250))

$masters = @([System.Drawing.Image]::FromFile($m1), [System.Drawing.Image]::FromFile($m2), [System.Drawing.Image]::FromFile($m3))
$y = $margin
foreach ($s in $sizes) {
    for ($c = 0; $c -lt 3; $c++) {
        $x = $margin + $c * (256 + $margin)
        # Each column shows its own master downscaled to the row's size, centered in a 256 cell.
        $tmp = [System.Drawing.Bitmap]::new($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $tg = [System.Drawing.Graphics]::FromImage($tmp)
        $tg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $tg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $tg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $tg.DrawImage($masters[$c], 0, 0, $s, $s)
        $bg.DrawImage($tmp, [float]($x + (256 - $s) / 2), [float]($y + (256 - $s) / 2))
        $tg.Dispose(); $tmp.Dispose()
    }
    $y += 256 + $margin
}
foreach ($m in $masters) { $m.Dispose() }

$boardPath = Join-Path $outputDir "altkey-tools-variants-board.png"
$board.Save($boardPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bg.Dispose(); $board.Dispose()
Write-Host "Board: $boardPath ($boardW x $boardH)"
Write-Host "Done." -ForegroundColor Green
