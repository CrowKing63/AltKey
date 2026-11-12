<#
.SYNOPSIS
    AltKey Alt 키캡 아이콘 변형 렌더링.

.DESCRIPTION
    정사각형 시릴루엣 수정(기존 베이스 스트립이 아래로 튀어나와 높이가 너비보다
    컸던 문제)과 상단바 "Al"(=AI) 강조 효과를 본뜬 라벨 변형을 2x2 매트릭스로 만든다:
      - 잉크 / 틸 배경
      - 일반 "Alt" / "Al" 액센트 강조
    PNG는 docs/assets/icon-concepts에만 쓴다. 기존 아이콘 자산은 건드리지 않는다.

.EXAMPLE
    .\scripts\create-icon-keycap-variants.ps1
#>
Add-Type -AssemblyName System.Drawing

$rootPath = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $rootPath "docs\assets\icon-concepts"

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# Palette: ink (minimal), teal (brand continuity), white label, AI accent mint.
# NOTE: this line must end in ASCII - PS 5.1 reads BOM-less UTF-8 as cp949 and a
# Hangul byte right before the newline can swallow the newline itself.
$ink      = [System.Drawing.Color]::FromArgb(255, 28, 25, 23)     # #1C1917
$inkDeep  = [System.Drawing.Color]::FromArgb(255, 12, 10, 9)      # #0C0A09
$teal     = [System.Drawing.Color]::FromArgb(255, 15, 118, 110)   # #0F766E
$tealDeep = [System.Drawing.Color]::FromArgb(255, 12, 84, 78)     # #0C544E
$white    = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
$accent       = [System.Drawing.Color]::FromArgb(255, 94, 234, 212)  # #5EEAD4 (AI accent mint)
# Pastel accent series for the Al highlight (tailwind-300 level, all readable on ink).
$pastelSky    = [System.Drawing.Color]::FromArgb(255, 125, 211, 252) # #7DD3FC
$pastelLavender = [System.Drawing.Color]::FromArgb(255, 196, 181, 253) # #C4B5FD
$pastelPink   = [System.Drawing.Color]::FromArgb(255, 249, 168, 212) # #F9A8D4
$pastelButter = [System.Drawing.Color]::FromArgb(255, 253, 230, 138) # #FDE68A
$pastelSage   = [System.Drawing.Color]::FromArgb(255, 134, 239, 172) # #86EFAC

# 둥근 사각형(squircle) 경로를 만든다.
function New-RoundedRectPath {
    param(
        [System.Drawing.RectangleF]$Rect,
        [float]$Radius
    )
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $Radius * 2
    $path.AddArc($Rect.X, $Rect.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rect.X, $Rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

# 투명 배경 캔버스와 그래픽 컨텍스트를 준비한다.
function Initialize-Canvas {
    param([int]$Size)
    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.Clear([System.Drawing.Color]::Transparent)
    return @{ Bitmap = $bitmap; Graphics = $graphics }
}

# 정사각형 시릴루엣 키캡을 그린다.
# 베이스(진한 색, 전체 시릴루엣)를 먼저 깔고, 키 본체는 아래쪽 0.06 정도 짧게 덮어
# 하단 스트립만 보이게 한다. 이전 버전은 베이스가 시릴루엣 밖으로 튀어나와
# 높이가 너비보다 커 정사각형이 아니었다 — 이번엔 바운딩 박스가 정확히 정사각.
function Add-Keycap {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Size,
        [System.Drawing.Color]$KeyFill,
        [System.Drawing.Color]$BaseFill
    )
    $silhouette = [System.Drawing.RectangleF]::new($Size * 0.11, $Size * 0.11, $Size * 0.78, $Size * 0.78)
    $basePath = New-RoundedRectPath -Rect $silhouette -Radius ($Size * 0.15)
    $baseBrush = [System.Drawing.SolidBrush]::new($BaseFill)
    $Graphics.FillPath($baseBrush, $basePath)
    $baseBrush.Dispose()
    $basePath.Dispose()

    $keyRect = [System.Drawing.RectangleF]::new($Size * 0.11, $Size * 0.11, $Size * 0.78, $Size * 0.72)
    $keyPath = New-RoundedRectPath -Rect $keyRect -Radius ($Size * 0.15)
    $keyBrush = [System.Drawing.SolidBrush]::new($KeyFill)
    $Graphics.FillPath($keyBrush, $keyPath)
    $keyBrush.Dispose()
    $keyPath.Dispose()

    return $keyRect
}

# "Alt" 라벨을 "Al" + "t" 두 조각으로 나눠 그린다.
# 상단바 타이틀의 AI 강조(Al = AI)를 아이콘에 반영하려면 "Al"만 따로 색칠해야 해서
# 이렇게 분리한다. GenericTypographic로 여백 없는 실측 폭을 받아 중앙 정렬한다.
function Add-AltLabel {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Size,
        [System.Drawing.RectangleF]$KeyRect,
        [System.Drawing.Color]$ColorAl,
        [System.Drawing.Color]$ColorT,
        [string]$FontFamily = "Segoe UI",
        [float]$SizeRatio = 0.30,
        [System.Drawing.FontStyle]$FontStyle = [System.Drawing.FontStyle]::Bold
    )
    $fontSize = $Size * $SizeRatio
    $font = [System.Drawing.Font]::new($FontFamily, $fontSize, $FontStyle, [System.Drawing.GraphicsUnit]::Pixel)
    $format = [System.Drawing.StringFormat]::new([System.Drawing.StringFormatFlags]::GenericTypographic)

    # Draw the full "Alt" in ColorT first so native l-t kerning keeps "t" tight.
    # Then overdraw "Al" in ColorAl from the same start point: A and l occupy the
    # identical positions, so the accent overlays cleanly with no gap.
    $wFull = $Graphics.MeasureString("Alt", $font, $format).Width
    $startX = ($Size - $wFull) / 2
    $cellH = $font.GetHeight($Graphics)
    $y = $KeyRect.Y + ($KeyRect.Height - $cellH) / 2

    $brushAl = [System.Drawing.SolidBrush]::new($ColorAl)
    $brushT  = [System.Drawing.SolidBrush]::new($ColorT)
    $Graphics.DrawString("Alt", $font, $brushT, $startX, $y, $format)
    $Graphics.DrawString("Al", $font, $brushAl, $startX, $y, $format)

    $brushAl.Dispose()
    $brushT.Dispose()
    $format.Dispose()
    $font.Dispose()
}

# --- 변형 1: 잉크 키캡, 일반 "Alt" (정사각형 수정 베이스라인) ---
function Save-KcInk {
    param([string]$OutputPath)
    $size = 256
    $canvas = Initialize-Canvas -Size $size
    $keyRect = Add-Keycap -Graphics $canvas.Graphics -Size $size -KeyFill $ink -BaseFill $inkDeep
    Add-AltLabel -Graphics $canvas.Graphics -Size $size -KeyRect $keyRect -ColorAl $white -ColorT $white
    $canvas.Bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Graphics.Dispose()
    $canvas.Bitmap.Dispose()
}

# --- 변형 2: 틸 키캡, 일반 "Alt" ---
function Save-KcTeal {
    param([string]$OutputPath)
    $size = 256
    $canvas = Initialize-Canvas -Size $size
    $keyRect = Add-Keycap -Graphics $canvas.Graphics -Size $size -KeyFill $teal -BaseFill $tealDeep
    Add-AltLabel -Graphics $canvas.Graphics -Size $size -KeyRect $keyRect -ColorAl $white -ColorT $white
    $canvas.Bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Graphics.Dispose()
    $canvas.Bitmap.Dispose()
}

# --- Ink keycap with Al accent (AI feature echo). The accent color is a parameter
# so one function serves the whole pastel series.
function Save-InkAccent {
    param(
        [string]$OutputPath,
        [System.Drawing.Color]$AccentColor,
        [string]$FontFamily = "Segoe UI",
        [float]$SizeRatio = 0.30,
        [System.Drawing.FontStyle]$FontStyle = [System.Drawing.FontStyle]::Bold
    )
    $size = 256
    $canvas = Initialize-Canvas -Size $size
    $keyRect = Add-Keycap -Graphics $canvas.Graphics -Size $size -KeyFill $ink -BaseFill $inkDeep
    Add-AltLabel -Graphics $canvas.Graphics -Size $size -KeyRect $keyRect -ColorAl $AccentColor -ColorT $white `
        -FontFamily $FontFamily -SizeRatio $SizeRatio -FontStyle $FontStyle
    $canvas.Bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Graphics.Dispose()
    $canvas.Bitmap.Dispose()
}

# --- 변형 4: 틸 키캡, "Al" 액센트 강조 (AI 기능 반영) ---
function Save-KcTealAi {
    param([string]$OutputPath)
    $size = 256
    $canvas = Initialize-Canvas -Size $size
    $keyRect = Add-Keycap -Graphics $canvas.Graphics -Size $size -KeyFill $teal -BaseFill $tealDeep
    Add-AltLabel -Graphics $canvas.Graphics -Size $size -KeyRect $keyRect -ColorAl $accent -ColorT $white
    $canvas.Bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Graphics.Dispose()
    $canvas.Bitmap.Dispose()
}

# 보드용 타일: 160px 시안 + 32/16px 밝은·어두운 배경 샘플 + 제목.
function Add-PreviewTile {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Image]$Image,
        [float]$X,
        [float]$Y,
        [float]$Width,
        [string]$Title,
        [string]$Subtitle
    )
    $cardRect = [System.Drawing.RectangleF]::new($X, $Y, $Width, 330)
    $cardPath = New-RoundedRectPath -Rect $cardRect -Radius 18
    $cardBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $borderPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(60, 15, 23, 42), 1.5)
    $Graphics.FillPath($cardBrush, $cardPath)
    $Graphics.DrawPath($borderPen, $cardPath)

    # 160px 메인 시안.
    $Graphics.DrawImage($Image, [System.Drawing.RectangleF]::new($X + ($Width - 160) / 2, $Y + 20, 160, 160))

    # 32/16px 샘플: 밝은 배경 / 어두운 배경 순서.
    $samples = @(
        @{ Size = 32; Back = [System.Drawing.Color]::FromArgb(255, 241, 245, 249); CX = $X + $Width * 0.125 },
        @{ Size = 32; Back = [System.Drawing.Color]::FromArgb(255, 31, 41, 55);  CX = $X + $Width * 0.375 },
        @{ Size = 16; Back = [System.Drawing.Color]::FromArgb(255, 241, 245, 249); CX = $X + $Width * 0.625 },
        @{ Size = 16; Back = [System.Drawing.Color]::FromArgb(255, 31, 41, 55);  CX = $X + $Width * 0.875 }
    )
    foreach ($sample in $samples) {
        $backRect = [System.Drawing.RectangleF]::new($sample.CX - ($sample.Size + 12) / 2, $Y + 200 - 6, $sample.Size + 12, $sample.Size + 12)
        $backBrush = [System.Drawing.SolidBrush]::new($sample.Back)
        $Graphics.FillRectangle($backBrush, $backRect)
        $backBrush.Dispose()
        $Graphics.DrawImage($Image, [System.Drawing.RectangleF]::new($sample.CX - $sample.Size / 2, $Y + 200, $sample.Size, $sample.Size))
    }

    $titleFont = [System.Drawing.Font]::new("Segoe UI", 15, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $subtitleFont = [System.Drawing.Font]::new("Segoe UI", 11, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $titleBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 17, 24, 39))
    $subBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 71, 85, 105))
    $Graphics.DrawString($Title, $titleFont, $titleBrush, $X + 18, $Y + 254)
    $Graphics.DrawString($Subtitle, $subtitleFont, $subBrush, [System.Drawing.RectangleF]::new($X + 18, $Y + 282, $Width - 36, 40))

    $subBrush.Dispose()
    $titleBrush.Dispose()
    $subtitleFont.Dispose()
    $titleFont.Dispose()
    $borderPen.Dispose()
    $cardBrush.Dispose()
    $cardPath.Dispose()
}

# 4개 변형을 한 화면에 나열한 비교 보드를 만든다.
function Save-ConceptBoard {
    param(
        [string]$OutputPath,
        [string[]]$SourcePaths,
        [string[]]$Titles,
        [string[]]$Subtitles,
        [int]$TileWidth = 232,
        [string]$BoardTitle = "AltKey Keycap Variants",
        [string]$BoardSubtitle = "Square silhouette fix + Al (AI) label emphasis, ink/teal x plain/accent"
    )
    $tileWidth = $TileWidth
    $gap = 12
    $margin = 34
    $boardWidth = $margin * 2 + $SourcePaths.Count * $tileWidth + ($SourcePaths.Count - 1) * $gap
    $bitmap = [System.Drawing.Bitmap]::new($boardWidth, 470, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $backgroundBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(0, 0),
        [System.Drawing.PointF]::new($boardWidth, 470),
        [System.Drawing.Color]::FromArgb(255, 241, 245, 249),
        [System.Drawing.Color]::FromArgb(255, 226, 232, 240)
    )
    $graphics.FillRectangle($backgroundBrush, 0, 0, $boardWidth, 470)

    $titleFont = [System.Drawing.Font]::new("Segoe UI", 24, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $subtitleFont = [System.Drawing.Font]::new("Segoe UI", 12, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $titleBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 17, 24, 39))
    $subtitleBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 71, 85, 105))
    $graphics.DrawString($BoardTitle, $titleFont, $titleBrush, 34, 24)
    $graphics.DrawString($BoardSubtitle, $subtitleFont, $subtitleBrush, 36, 62)

    for ($i = 0; $i -lt $SourcePaths.Count; $i++) {
        $image = [System.Drawing.Image]::FromFile($SourcePaths[$i])
        Add-PreviewTile -Graphics $graphics -Image $image -X ($margin + ($i * ($tileWidth + $gap))) -Y 96 -Width $tileWidth `
            -Title $Titles[$i] -Subtitle $Subtitles[$i]
        $image.Dispose()
    }

    $subtitleBrush.Dispose()
    $titleBrush.Dispose()
    $subtitleFont.Dispose()
    $titleFont.Dispose()
    $backgroundBrush.Dispose()

    $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

$v1 = Join-Path $outputDir "altkey-keycap-v1-ink.png"
$v2 = Join-Path $outputDir "altkey-keycap-v2-teal.png"
$v3 = Join-Path $outputDir "altkey-keycap-v3-ink-ai.png"
$v4 = Join-Path $outputDir "altkey-keycap-v4-teal-ai.png"
$v5 = Join-Path $outputDir "altkey-keycap-v5-ink-sky.png"
$v6 = Join-Path $outputDir "altkey-keycap-v6-ink-lavender.png"
$v7 = Join-Path $outputDir "altkey-keycap-v7-ink-pink.png"
$v8 = Join-Path $outputDir "altkey-keycap-v8-ink-butter.png"
$v9 = Join-Path $outputDir "altkey-keycap-v9-ink-sage.png"
$board = Join-Path $outputDir "altkey-keycap-board-pastels.png"

Save-KcInk -OutputPath $v1
Save-KcTeal -OutputPath $v2
Save-KcTealAi -OutputPath $v4
Save-InkAccent -OutputPath $v3 -AccentColor $accent
Save-InkAccent -OutputPath $v5 -AccentColor $pastelSky
Save-InkAccent -OutputPath $v6 -AccentColor $pastelLavender
Save-InkAccent -OutputPath $v7 -AccentColor $pastelPink
Save-InkAccent -OutputPath $v8 -AccentColor $pastelButter
Save-InkAccent -OutputPath $v9 -AccentColor $pastelSage
Save-ConceptBoard -OutputPath $board `
    -SourcePaths @($v1, $v3, $v5, $v6, $v7, $v8, $v9) `
    -Titles @("Plain", "Mint", "Sky", "Lavender", "Pink", "Butter", "Sage") `
    -Subtitles @(
        "Baseline: plain white Alt label",
        "Al in mint #5EEAD4 (AI echo)",
        "Al in sky #7DD3FC",
        "Al in lavender #C4B5FD",
        "Al in pink #F9A8D4",
        "Al in butter #FDE68A",
        "Al in sage #86EFAC"
    ) `
    -TileWidth 170 `
    -BoardTitle "AltKey Keycap - Al Accent Pastels" `
    -BoardSubtitle "Ink keycap with pastel Al highlight (AI echo); hex per tile"

Write-Host "Saved: $v1"
Write-Host "Saved: $v2"
Write-Host "Saved: $v3"
Write-Host "Saved: $v4"
Write-Host "Saved: $v5"
Write-Host "Saved: $v6"
Write-Host "Saved: $v7"
Write-Host "Saved: $v8"
Write-Host "Saved: $v9"
Write-Host "Saved: $board"

# Lavender label font A/B: bigger + bolder for small-size legibility.
$v6a = Join-Path $outputDir "altkey-keycap-v6a-lavender-bold34.png"
$v6b = Join-Path $outputDir "altkey-keycap-v6b-lavender-black32.png"
$boardFont = Join-Path $outputDir "altkey-keycap-board-lavender-font.png"

Save-InkAccent -OutputPath $v6a -AccentColor $pastelLavender -SizeRatio 0.34
Save-InkAccent -OutputPath $v6b -AccentColor $pastelLavender -FontFamily "Segoe UI Black" -SizeRatio 0.32 -FontStyle [System.Drawing.FontStyle]::Regular
Save-ConceptBoard -OutputPath $boardFont `
    -SourcePaths @($v6, $v6a, $v6b) `
    -Titles @("Original", "A: Bold x1.13", "B: Segoe UI Black") `
    -Subtitles @(
        "Bold 0.30 (baseline)",
        "Bold 0.34 - bigger, same weight",
        "Black 0.32 - heavier + slightly bigger"
    ) `
    -TileWidth 232 `
    -BoardTitle "Lavender Label Font A/B" `
    -BoardSubtitle "Bigger + bolder Alt label for small-size legibility (lavender accent)"

Write-Host "Saved: $v6a"
Write-Host "Saved: $v6b"
Write-Host "Saved: $boardFont"
