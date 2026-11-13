<#
.SYNOPSIS
    Renders the AltKey portrait poster art at a requested size.

.DESCRIPTION
    Minimal, modern portrait poster in the finalized brand system:
      - deep-ink background (#0C0A09), flat
      - subtle lavender radial glow behind the mark (very low alpha)
      - the finalized keycap mark, over-rendered at 2x and downsampled
        (same boundary-smoothing technique as the icon masters)
      - "AltKey" wordmark: full word in white, "Al" overlaid in lavender #C4B5FD
        (Segoe UI Bold) - native kerning, no gap between "l" and "t"
      - Hangul tagline in two lines, muted gray (Malgun Gothic)
      - a small lavender pill accent near the bottom

    Width scales all horizontal dimensions (and element sizes) from the 1080-wide
    reference layout. Height is explicit: pass -Height for non-9:16 targets such
    as store requirements (720x1080 / 1440x2160 are 2:3, NOT 9:16).

    Vertical layout is a stack computed from rendered sizes. If the gap between
    line 2's glyph body and the pill falls below the target (90*S px), the whole
    icon+text stack shifts up by just enough to restore it - so tight canvases
    (store 2:3) breathe, while the approved 9:16 layout stays pixel-identical.

.EXAMPLE
    .\scripts\create-poster.ps1                     # 1080x1920 (9:16)
    .\scripts\create-poster.ps1 -Width 720 -Height 1080   # store 2:3
    .\scripts\create-poster.ps1 -Width 1440 -Height 2160  # store 2:3 @2x
#>
param(
    [int]$Width = 1080,
    [int]$Height = 0
)

Add-Type -AssemblyName System.Drawing

$rootPath = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $rootPath "docs\assets\posters"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# Palette (ASCII-safe; must not end lines in non-ASCII for PS 5.1 cp949).
$inkDeep = [System.Drawing.Color]::FromArgb(255, 12, 10, 9)      # #0C0A09 background
$ink     = [System.Drawing.Color]::FromArgb(255, 28, 25, 23)     # #1C1917 key fill
$white   = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
$lavender = [System.Drawing.Color]::FromArgb(255, 196, 181, 253) # #C4B5FD accent
$muted   = [System.Drawing.Color]::FromArgb(255, 143, 138, 132)  # tagline gray

# Canvas: explicit height wins; otherwise default to 9:16 for the width.
$W = $Width
if ($Height -gt 0) { $H = $Height } else { $H = [int][math]::Round($W * 16 / 9) }
$S = [float]$W / 1080.0   # linear scale from the 1080-wide reference layout

# Rounded-rect (squircle) path.
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

# Square-silhouette keycap: dark base fills the whole silhouette, key body is
# drawn slightly shorter so only a bottom strip of the base peeks out.
function Add-Keycap {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Size,
        [System.Drawing.Color]$KeyFill,
        [System.Drawing.Color]$BaseFill
    )
    $silhouette = [System.Drawing.RectangleF]::new(0, 0, $Size, $Size)
    $basePath = New-RoundedRectPath -Rect $silhouette -Radius ($Size * 0.19)
    $baseBrush = [System.Drawing.SolidBrush]::new($BaseFill)
    $Graphics.FillPath($baseBrush, $basePath)
    $baseBrush.Dispose()
    $basePath.Dispose()

    $keyRect = [System.Drawing.RectangleF]::new(0, 0, $Size, $Size * 0.926)
    $keyPath = New-RoundedRectPath -Rect $keyRect -Radius ($Size * 0.19)
    $keyBrush = [System.Drawing.SolidBrush]::new($KeyFill)
    $Graphics.FillPath($keyBrush, $keyPath)
    $keyBrush.Dispose()
    $keyPath.Dispose()

    return $keyRect
}

# "Alt" label: draw full "Alt" in ColorT first (keeps native l-t kerning tight),
# then overdraw "Al" in ColorAl from the same start point so the accent overlays
# cleanly with no gap.
function Add-AltLabel {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Size,
        [System.Drawing.RectangleF]$KeyRect,
        [System.Drawing.Color]$ColorAl,
        [System.Drawing.Color]$ColorT,
        [string]$FontFamily = "Segoe UI",
        [float]$SizeRatio = 0.436,
        [System.Drawing.FontStyle]$FontStyle = [System.Drawing.FontStyle]::Bold
    )
    $fontSize = $Size * $SizeRatio
    $font = [System.Drawing.Font]::new($FontFamily, $fontSize, $FontStyle, [System.Drawing.GraphicsUnit]::Pixel)
    # Default StringFormat for BOTH measure and draw: identical padding on each side
    # keeps "Alt" centered. (GenericTypographic is accepted by the ctor but rejected
    # by the FormatFlags property setter under PS 5.1 - keep it out entirely.)
    $format = [System.Drawing.StringFormat]::new()

    # .NET Framework has NO MeasureString(string, Font, StringFormat) overload -
    # only (string, Font, SizeF) and (string, Font, SizeF, StringFormat). Force the
    # 4-arg one with an empty layout area (no wrap constraint = full string width).
    $wFull = $Graphics.MeasureString([string]"Alt", [System.Drawing.Font]$font, [System.Drawing.SizeF]::Empty, [System.Drawing.StringFormat]$format).Width
    $startX = ($Size - $wFull) / 2
    $cellH = $font.GetHeight($Graphics)
    $y = $KeyRect.Y + ($KeyRect.Height - $cellH) / 2

    $brushT = [System.Drawing.SolidBrush]::new($ColorT)
    $Graphics.DrawString([string]"Alt", [System.Drawing.Font]$font, [System.Drawing.Brush]$brushT, [float]$startX, [float]$y, [System.Drawing.StringFormat]$format)
    $brushT.Dispose()

    # Overdraw the "Al" accent from the same start point.
    $wAl = $Graphics.MeasureString([string]"Al", [System.Drawing.Font]$font, [System.Drawing.SizeF]::Empty, [System.Drawing.StringFormat]$format).Width
    $brushAl = [System.Drawing.SolidBrush]::new($ColorAl)
    $Graphics.DrawString([string]"Al", [System.Drawing.Font]$font, [System.Drawing.Brush]$brushAl, [float]$startX, [float]$y, [System.Drawing.StringFormat]$format)
    $brushAl.Dispose()
    $font.Dispose()
}

# --- 1. Render the keycap mark: over-render at 2x, downsample to final size. ---
$markOut = [int][math]::Round(640 * $S)
$markOver = $markOut * 2
$overBmp = [System.Drawing.Bitmap]::new($markOver, $markOver, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$overG = [System.Drawing.Graphics]::FromImage($overBmp)
$overG.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$overG.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$overG.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$overG.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$overG.Clear([System.Drawing.Color]::Transparent)
$keyRect = Add-Keycap -Graphics $overG -Size $markOver -KeyFill $ink -BaseFill $inkDeep
Add-AltLabel -Graphics $overG -Size $markOver -KeyRect $keyRect -ColorAl $lavender -ColorT $white
$overG.Dispose()

$markBmp = [System.Drawing.Bitmap]::new($markOut, $markOut, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$markG = [System.Drawing.Graphics]::FromImage($markBmp)
$markG.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$markG.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$markG.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$markG.DrawImage($overBmp, 0, 0, $markOut, $markOut)
$markG.Dispose()
$overBmp.Dispose()

# --- 2. Poster canvas. ---
$bmp = [System.Drawing.Bitmap]::new($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.Clear($inkDeep)

# --- 3. Layout: compute the full vertical stack BEFORE drawing, so a tight
# canvas can shift the icon+text block up as a unit if needed. ---
$markY = [int][math]::Round($H * 0.2083)

$wordFont = [System.Drawing.Font]::new("Segoe UI", [float](185 * $S), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$format = [System.Drawing.StringFormat]::new()
$wFull = $g.MeasureString([string]"AltKey", [System.Drawing.Font]$wordFont, [System.Drawing.SizeF]::Empty, [System.Drawing.StringFormat]$format).Width
$wordX = [float](($W - $wFull) / 2)
$wordH = $wordFont.GetHeight($g)
$wordY = [float]($markY + $markOut + [math]::Round(90 * $S))

# Code points verified with tools/hangul/hangul_calc.py (never hand-computed).
# Line 1: "한국어 사용자 전용"
$tagLine1 = ([char]0xD55C) + ([char]0xAD6D) + ([char]0xC5B4) + " " + ([char]0xC0AC) + ([char]0xC6A9) + ([char]0xC790) + " " + ([char]0xC804) + ([char]0xC6A9)
# Line 2: "커스터마이징 화상 키보드"
$tagLine2 = ([char]0xCEE4) + ([char]0xC2A4) + ([char]0xD130) + ([char]0xB9C8) + ([char]0xC774) + ([char]0xC9D5) + " " + ([char]0xD654) + ([char]0xC0C1) + " " + ([char]0xD0A4) + ([char]0xBCF4) + ([char]0xB4DC)

$tagFont = [System.Drawing.Font]::new("Malgun Gothic", [float](58 * $S), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$w1 = $g.MeasureString([string]$tagLine1, [System.Drawing.Font]$tagFont, [System.Drawing.SizeF]::Empty, [System.Drawing.StringFormat]$format).Width
$tagLine1Y = [float]($wordY + $wordH + [math]::Round(24 * $S))
$w2 = $g.MeasureString([string]$tagLine2, [System.Drawing.Font]$tagFont, [System.Drawing.SizeF]::Empty, [System.Drawing.StringFormat]$format).Width
$tagLine2Y = [float]($tagLine1Y + [math]::Round(90 * $S))

# Pill at its usual bottom margin; if line 2 would sit too close to it, shift the
# whole icon+text stack up by just enough to restore the target gap.
$pillByMargin = [float]($H - [math]::Round(170 * $S))
$line2Bottom = [float]($tagLine2Y + (58 * $S))
$gapTarget = [float](90 * $S)
if ($line2Bottom -gt ($pillByMargin - $gapTarget)) {
    $shift = [int][math]::Ceiling($line2Bottom - ($pillByMargin - $gapTarget))
    $markY -= $shift
    $wordY -= $shift
    $tagLine1Y -= $shift
    $tagLine2Y -= $shift
}
$pillY = $pillByMargin

# --- 4. Draw: glow, mark, wordmark, tagline, pill. ---
$glowCx = [float]($W / 2)
$glowCy = [float]$markY + $markOut / 2
$glowR  = [float](640 * $S)
$glowPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
$glowPath.AddEllipse($glowCx - $glowR, $glowCy - $glowR, $glowR * 2, $glowR * 2)
$glowBrush = [System.Drawing.Drawing2D.PathGradientBrush]::new($glowPath)
$glowBrush.CenterColor = [System.Drawing.Color]::FromArgb(30, 196, 181, 253)
$glowBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 196, 181, 253))
$g.FillEllipse($glowBrush, $glowCx - $glowR, $glowCy - $glowR, $glowR * 2, $glowR * 2)
$glowBrush.Dispose()
$glowPath.Dispose()

$markX = [int](($W - $markOut) / 2)
$g.DrawImage($markBmp, $markX, $markY, $markOut, $markOut)
$markBmp.Dispose()

# Wordmark: full word in white, then "Al" overlaid in lavender from the same
# start point (native kerning, no gap - same technique as the keycap).
$brushWhite = [System.Drawing.SolidBrush]::new($white)
$g.DrawString([string]"AltKey", [System.Drawing.Font]$wordFont, [System.Drawing.Brush]$brushWhite, $wordX, $wordY, [System.Drawing.StringFormat]$format)
$brushWhite.Dispose()
$brushLav = [System.Drawing.SolidBrush]::new($lavender)
$g.DrawString([string]"Al", [System.Drawing.Font]$wordFont, [System.Drawing.Brush]$brushLav, $wordX, $wordY, [System.Drawing.StringFormat]$format)
$brushLav.Dispose()
$wordFont.Dispose()

# Tagline, two lines, muted gray.
$brushMuted = [System.Drawing.SolidBrush]::new($muted)
$g.DrawString([string]$tagLine1, [System.Drawing.Font]$tagFont, [System.Drawing.Brush]$brushMuted, [float](($W - $w1) / 2), $tagLine1Y, [System.Drawing.StringFormat]$format)
$g.DrawString([string]$tagLine2, [System.Drawing.Font]$tagFont, [System.Drawing.Brush]$brushMuted, [float](($W - $w2) / 2), $tagLine2Y, [System.Drawing.StringFormat]$format)
$brushMuted.Dispose()
$tagFont.Dispose()

# Small lavender pill accent near the bottom.
$pillW = 84.0 * $S
$pillH = 12.0 * $S
$pillX = [float](($W - $pillW) / 2)
$pillPath = New-RoundedRectPath -Rect ([System.Drawing.RectangleF]::new($pillX, $pillY, $pillW, $pillH)) -Radius ($pillH / 2)
$brushLav2 = [System.Drawing.SolidBrush]::new($lavender)
$g.FillPath($brushLav2, $pillPath)
$brushLav2.Dispose()
$pillPath.Dispose()

# --- 5. Save. ---
$format.Dispose()
$g.Dispose()
$outPath = Join-Path $outputDir ("altkey-poster-{0}x{1}.png" -f $W, $H)
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Poster: $outPath ($W x $H)"
