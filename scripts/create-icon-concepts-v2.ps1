<#
.SYNOPSIS
    AltKey 메인 앱 아이콘 리디자인 시안 v2 생성.

.DESCRIPTION
    기존 아이콘은 그대로 두고, docs/assets/icon-concepts 아래에
    5개 플랫 컨셉 PNG와 비교 보드(32/16px 밝은·어두운 배경 샘플 포함)만 만든다.

.EXAMPLE
    .\scripts\create-icon-concepts-v2.ps1
#>
Add-Type -AssemblyName System.Drawing

$rootPath = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $rootPath "docs\assets\icon-concepts"

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# 팔레트: 딥 틸(브랜드 연속) / 잉크(미니멀) / 흰 심볼
$teal  = [System.Drawing.Color]::FromArgb(255, 15, 118, 110)    # #0F766E
$ink   = [System.Drawing.Color]::FromArgb(255, 28, 25, 23)      # #1C1917
$inkDeep = [System.Drawing.Color]::FromArgb(255, 12, 10, 9)     # #0C0A09
$white = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)

# 한글 음절 '가'는 코드포인트로 구성한다.
# PS 5.1이 BOM 없는 UTF-8 스크립트를 ANSI(cp949)로 읽으면 리터럴 한자가 깨져
# 한자처럼 렌더링되므로, 문자 리터럴을 스크립트에 직접 쓰지 않는다.
$gaGlyph = [string][char]0xAC00

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

# 플랫(광택 없음) squircle 타일을 채운다.
function Add-FlatTile {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Size,
        [System.Drawing.Color]$Fill
    )
    $tileRect = [System.Drawing.RectangleF]::new($Size * 0.05, $Size * 0.05, $Size * 0.90, $Size * 0.90)
    $tilePath = New-RoundedRectPath -Rect $tileRect -Radius ($Size * 0.17)
    $brush = [System.Drawing.SolidBrush]::new($Fill)
    $Graphics.FillPath($brush, $tilePath)
    $brush.Dispose()
    $tilePath.Dispose()
}

# 지정 사각형 안에 글자를 중앙 정렬로 그린다(한글은 y 보정 옵션).
function Add-CenteredGlyph {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.RectangleF]$Rect,
        [string]$Glyph,
        [string]$FontFamily,
        [float]$FontSize,
        [System.Drawing.Color]$Color,
        [float]$YOffsetRatio = 0.0
    )
    $font = [System.Drawing.Font]::new($FontFamily, $FontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = [System.Drawing.SolidBrush]::new($Color)
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $drawRect = [System.Drawing.RectangleF]::new($Rect.X, $Rect.Y + ($Rect.Height * $YOffsetRatio), $Rect.Width, $Rect.Height)
    $Graphics.DrawString($Glyph, $font, $brush, $drawRect, $format)
    $format.Dispose()
    $brush.Dispose()
    $font.Dispose()
}

# --- 컨셉 1: '가' 모노그램 (플랫 딥 틸 + 흰 한글 글자) ---
function Save-ConceptGa {
    param([string]$OutputPath)
    $size = 256
    $canvas = Initialize-Canvas -Size $size
    $graphics = $canvas.Graphics

    Add-FlatTile -Graphics $graphics -Size $size -Fill $teal
    # 한글 음절은 라틴보다 상하 여백이 크므로 살짝 아래로 보정한다.
    Add-CenteredGlyph -Graphics $graphics -Rect ([System.Drawing.RectangleF]::new($size * 0.20, $size * 0.18, $size * 0.60, $size * 0.64)) `
        -Glyph $gaGlyph -FontFamily "Malgun Gothic" -FontSize ($size * 0.52) -Color $white

    $canvas.Bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $canvas.Bitmap.Dispose()
}

# --- 컨셉 2: Alt 키캡 (잉크 키 + 흰 'Alt', 하단 베이스로 깊이감) ---
function Save-ConceptKeycap {
    param([string]$OutputPath)
    $size = 256
    $canvas = Initialize-Canvas -Size $size
    $graphics = $canvas.Graphics

    # 키 아래로 살짝 보이는 더 진한 베이스: 물리적 키처럼 읽히게 한다.
    $baseRect = [System.Drawing.RectangleF]::new($size * 0.12, $size * 0.16, $size * 0.76, $size * 0.76)
    $basePath = New-RoundedRectPath -Rect $baseRect -Radius ($size * 0.15)
    $baseBrush = [System.Drawing.SolidBrush]::new($inkDeep)
    $graphics.FillPath($baseBrush, $basePath)
    $baseBrush.Dispose()
    $basePath.Dispose()

    # 키캡 본체(잉크)와 흰 'Alt' 표기.
    $keyRect = [System.Drawing.RectangleF]::new($size * 0.12, $size * 0.11, $size * 0.76, $size * 0.76)
    $keyPath = New-RoundedRectPath -Rect $keyRect -Radius ($size * 0.15)
    $keyBrush = [System.Drawing.SolidBrush]::new($ink)
    $graphics.FillPath($keyBrush, $keyPath)
    $keyBrush.Dispose()
    $keyPath.Dispose()

    Add-CenteredGlyph -Graphics $graphics -Rect ([System.Drawing.RectangleF]::new($size * 0.12, $size * 0.13, $size * 0.76, $size * 0.72)) `
        -Glyph "Alt" -FontFamily "Segoe UI" -FontSize ($size * 0.30) -Color $white

    $canvas.Bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $canvas.Bitmap.Dispose()
}

# --- 컨셉 3: 가/A 스플릿 (대각선으로 나눈 2톤 타일) ---
function Save-ConceptSplit {
    param([string]$OutputPath)
    $size = 256
    $canvas = Initialize-Canvas -Size $size
    $graphics = $canvas.Graphics

    # 전체 squircle을 먼저 잉크로 채운다.
    Add-FlatTile -Graphics $graphics -Size $size -Fill $ink

    # 대각선(위쪽 오른쪽 → 아래쪽 왼쪽) 오른쪽 부분을 클립으로 제한해 틸로 다시 칠한다.
    # Region 보일러플레이트가 PS 5.1에서 불안정해 SetClip(GraphicsPath) 방식으로 바꿨다.
    $diagPolygon = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diagPolygon.AddPolygon(@([System.Drawing.PointF]::new($size * 0.62, 0),
                              [System.Drawing.PointF]::new($size, 0),
                              [System.Drawing.PointF]::new($size, $size),
                              [System.Drawing.PointF]::new($size * 0.38, $size)))

    $tileRect = [System.Drawing.RectangleF]::new($size * 0.05, $size * 0.05, $size * 0.90, $size * 0.90)
    $squircle = New-RoundedRectPath -Rect $tileRect -Radius ($size * 0.17)

    $state = $graphics.Save()
    $graphics.SetClip($diagPolygon)
    $tealBrush = [System.Drawing.SolidBrush]::new($teal)
    $graphics.FillPath($tealBrush, $squircle)
    $graphics.Restore($state)

    $tealBrush.Dispose()
    $diagPolygon.Dispose()
    $squircle.Dispose()

    # 좌측(잉크)에 '가', 우측(틸)에 'A'.
    Add-CenteredGlyph -Graphics $graphics -Rect ([System.Drawing.RectangleF]::new($size * 0.13, $size * 0.20, $size * 0.42, $size * 0.60)) `
        -Glyph $gaGlyph -FontFamily "Malgun Gothic" -FontSize ($size * 0.40) -Color $white -YOffsetRatio 0.05
    Add-CenteredGlyph -Graphics $graphics -Rect ([System.Drawing.RectangleF]::new($size * 0.52, $size * 0.18, $size * 0.38, $size * 0.64)) `
        -Glyph "A" -FontFamily "Segoe UI" -FontSize ($size * 0.40) -Color $white

    $canvas.Bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $canvas.Bitmap.Dispose()
}

# --- 컨셉 4: 플랫 A + 단일 아크 (기존 심볼의 미니멀 재해석) ---
function Save-ConceptArc {
    param([string]$OutputPath)
    $size = 256
    $canvas = Initialize-Canvas -Size $size
    $graphics = $canvas.Graphics

    Add-FlatTile -Graphics $graphics -Size $size -Fill $teal

    # 순환 화살표 두 개 대신, 오른쪽이 열린 단일 아크 하나로만 전환을 암시한다.
    $arcRect = [System.Drawing.RectangleF]::new($size * 0.19, $size * 0.19, $size * 0.62, $size * 0.62)
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(230, 255, 255, 255), [float]($size * 0.085))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawArc($pen, $arcRect, 70, 250)
    $pen.Dispose()

    Add-CenteredGlyph -Graphics $graphics -Rect ([System.Drawing.RectangleF]::new($size * 0.25, $size * 0.18, $size * 0.50, $size * 0.64)) `
        -Glyph "A" -FontFamily "Segoe UI" -FontSize ($size * 0.44) -Color $white

    $canvas.Bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $canvas.Bitmap.Dispose()
}

# --- 컨셉 5: 타이핑 리듬 (커서 + 점, 추상 심볼) ---
function Save-ConceptTyping {
    param([string]$OutputPath)
    $size = 256
    $canvas = Initialize-Canvas -Size $size
    $graphics = $canvas.Graphics

    Add-FlatTile -Graphics $graphics -Size $size -Fill $ink

    # 굵은 텍스트 커서(캡슐 모양 막대) + 오른쪽 점 3개: "타는 행위"를 추상적으로 표현한다.
    $caretRect = [System.Drawing.RectangleF]::new($size * 0.26, $size * 0.27, $size * 0.10, $size * 0.46)
    $caretPath = New-RoundedRectPath -Rect $caretRect -Radius ($size * 0.05)
    $whiteBrush = [System.Drawing.SolidBrush]::new($white)
    $graphics.FillPath($whiteBrush, $caretPath)
    $caretPath.Dispose()

    foreach ($dotX in @(0.48, 0.62, 0.76)) {
        $dotRect = [System.Drawing.RectangleF]::new($size * $dotX - $size * 0.05, $size * 0.45, $size * 0.10, $size * 0.10)
        $graphics.FillEllipse($whiteBrush, $dotRect)
    }
    $whiteBrush.Dispose()

    $canvas.Bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
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

# 5개 시안을 한 화면에 나열한 비교 보드를 만든다.
function Save-ConceptBoard {
    param(
        [string]$OutputPath,
        [string[]]$SourcePaths,
        [string[]]$Titles,
        [string[]]$Subtitles
    )
    $tileWidth = 232
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
    $graphics.DrawString("AltKey Icon Concepts v2", $titleFont, $titleBrush, 34, 24)
    $graphics.DrawString("Flat directions + 32px / 16px readability check on light and dark surfaces", $subtitleFont, $subtitleBrush, 36, 62)

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

$c1 = Join-Path $outputDir "altkey-icon-v2-1-ga-monogram.png"
$c2 = Join-Path $outputDir "altkey-icon-v2-2-alt-keycap.png"
$c3 = Join-Path $outputDir "altkey-icon-v2-3-ga-a-split.png"
$c4 = Join-Path $outputDir "altkey-icon-v2-4-flat-a-arc.png"
$c5 = Join-Path $outputDir "altkey-icon-v2-5-typing-rhythm.png"
$board = Join-Path $outputDir "altkey-icon-v2-board.png"

Save-ConceptGa -OutputPath $c1
Save-ConceptKeycap -OutputPath $c2
Save-ConceptSplit -OutputPath $c3
Save-ConceptArc -OutputPath $c4
Save-ConceptTyping -OutputPath $c5
Save-ConceptBoard -OutputPath $board `
    -SourcePaths @($c1, $c2, $c3, $c4, $c5) `
    -Titles @("Ga Monogram", "Alt Keycap", "Ga/A Split", "Flat A + Arc", "Typing Rhythm") `
    -Subtitles @(
        "Korean-first identity: a single Hangul syllable on flat deep teal",
        "The icon is a keyboard key labeled 'Alt'",
        "Diagonal split: ga on ink, A on teal — the toggle made visible",
        "Keeps the A mark, replaces double arrows with one open arc",
        "Abstract caret + dots: the act of typing, no letter"
    )

Write-Host "Saved: $c1"
Write-Host "Saved: $c2"
Write-Host "Saved: $c3"
Write-Host "Saved: $c4"
Write-Host "Saved: $c5"
Write-Host "Saved: $board"
