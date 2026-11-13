<#
.SYNOPSIS
    AltKey final icon build: multi-size .ico + hi-res 512 master PNG.

.DESCRIPTION
    Renders the finalized "A" variant (ink keycap, lavender #C4B5FD "Al" accent,
    white "t", Segoe UI Bold at SizeRatio 0.41) at every size, then:
      - assembles a multi-size .ico (PNG-compressed entries: 16/32/48/256)
      - writes a standalone 512px master PNG (512 cannot fit in a standard .ico:
        the per-entry dimension byte maxes at 255, with 0 meaning 256).

    All small sizes are produced by high-quality downscaling from the 512 master
    (the same technique the approved comparison board used for its small samples),
    which gives smoother edges than native small-size rasterization.

    Output goes only to docs/assets/icon-concepts. Existing icon assets are NOT touched.

.EXAMPLE
    .\scripts\build-final-icon.ps1
#>
Add-Type -AssemblyName System.Drawing

$rootPath = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $rootPath "docs\assets\icon-concepts"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# Palette (ASCII-safe; must not end lines in non-ASCII for PS 5.1 cp949).
$ink            = [System.Drawing.Color]::FromArgb(255, 28, 25, 23)    # #1C1917
$inkDeep        = [System.Drawing.Color]::FromArgb(255, 12, 10, 9)     # #0C0A09
$white          = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
$pastelLavender = [System.Drawing.Color]::FromArgb(255, 196, 181, 253) # #C4B5FD

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

# Transparent canvas + graphics context.
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

# Square-silhouette keycap: dark base fills the whole silhouette, key body is
# drawn slightly shorter so only a bottom strip of the base peeks out.
function Add-Keycap {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Size,
        [System.Drawing.Color]$KeyFill,
        [System.Drawing.Color]$BaseFill
    )
    $silhouette = [System.Drawing.RectangleF]::new($Size * 0.03, $Size * 0.03, $Size * 0.94, $Size * 0.94)
    $basePath = New-RoundedRectPath -Rect $silhouette -Radius ($Size * 0.18)
    $baseBrush = [System.Drawing.SolidBrush]::new($BaseFill)
    $Graphics.FillPath($baseBrush, $basePath)
    $baseBrush.Dispose()
    $basePath.Dispose()

    $keyRect = [System.Drawing.RectangleF]::new($Size * 0.03, $Size * 0.03, $Size * 0.94, $Size * 0.87)
    $keyPath = New-RoundedRectPath -Rect $keyRect -Radius ($Size * 0.18)
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
        [float]$SizeRatio = 0.41,
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

    $brushAl = [System.Drawing.SolidBrush]::new($ColorAl)
    $brushT  = [System.Drawing.SolidBrush]::new($ColorT)
    $Graphics.DrawString([string]"Alt", [System.Drawing.Font]$font, [System.Drawing.Brush]$brushT, [float]$startX, [float]$y, [System.Drawing.StringFormat]$format)
    $Graphics.DrawString([string]"Al", [System.Drawing.Font]$font, [System.Drawing.Brush]$brushAl, [float]$startX, [float]$y, [System.Drawing.StringFormat]$format)

    $brushAl.Dispose()
    $brushT.Dispose()
    $format.Dispose()
    $font.Dispose()
}

# Render the master at a given size (native rasterization).
function Save-MasterPng {
    param([int]$Size, [string]$OutputPath)
    $canvas = Initialize-Canvas -Size $Size
    $keyRect = Add-Keycap -Graphics $canvas.Graphics -Size $Size -KeyFill $ink -BaseFill $inkDeep
    Add-AltLabel -Graphics $canvas.Graphics -Size $Size -KeyRect $keyRect -ColorAl $pastelLavender -ColorT $white
    $canvas.Bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Graphics.Dispose()
    $canvas.Bitmap.Dispose()
}

# Downscale the master to a smaller size with high-quality bicubic.
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

# --- 1. Render the 512 master (native). ---
$masterPath = Join-Path $outputDir "altkey-icon-final-512.png"
Save-MasterPng -Size 512 -OutputPath $masterPath
$master = [System.Drawing.Bitmap]::FromFile($masterPath)

# --- 2. Produce 256/48/32/16 by downscaling the master. ---
$smallSizes = @(256, 48, 32, 16)
$pngPaths = @{ 512 = $masterPath }
foreach ($s in $smallSizes) {
    $p = Join-Path $outputDir ("altkey-icon-final-{0}.png" -f $s)
    Save-DownscaledPng -Master $master -Size $s -OutputPath $p
    $pngPaths[$s] = $p
}
$master.Dispose()

# --- 3. Assemble the multi-size .ico (PNG entries: 16/32/48/256). ---
# 512 is intentionally excluded: a standard .ico entry dimension is one byte
# (max 255, 0 = 256), so 512 cannot be encoded. It ships as the standalone PNG.
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

# First pass: compute each image's byte offset (header + directory size).
$dataStart = 6 + ($entries.Count * 16)
$offsets = @()
$running = $dataStart
foreach ($e in $entries) {
    $offsets += $running
    $running += $e.Bytes.Length
}

# Directory entries (one 16-byte ICONDIRENTRY per image).
for ($i = 0; $i -lt $entries.Count; $i++) {
    $e = $entries[$i]
    $dim = if ($e.Size -ge 256) { 0 } else { [byte]$e.Size }   # 0 means 256
    $bw.Write([byte]$dim)                # width
    $bw.Write([byte]$dim)                # height
    $bw.Write([byte]0)                    # color count (0 = true color)
    $bw.Write([byte]0)                    # reserved
    $bw.Write([uint16]1)                  # planes
    $bw.Write([uint16]32)                 # bits per pixel
    $bw.Write([uint32]$e.Bytes.Length)    # resource byte size
    $bw.Write([uint32]$offsets[$i])       # offset from file start
}

# Image data (PNG byte streams, in directory order).
foreach ($e in $entries) {
    $bw.Write($e.Bytes)
}
$bw.Flush()
$icoBytes = $ms.ToArray()
$bw.Dispose()
$ms.Dispose()

$icoPath = Join-Path $outputDir "altkey-icon-final.ico"
[System.IO.File]::WriteAllBytes($icoPath, $icoBytes)

# --- 4. Report. ---
Write-Host ("Master 512 PNG : {0} ({1} bytes)" -f $masterPath, (Get-Item $masterPath).Length)
foreach ($s in $smallSizes) {
    Write-Host ("Downscaled {0,-3} : {1} ({2} bytes)" -f $s, $pngPaths[$s], (Get-Item $pngPaths[$s]).Length)
}
Write-Host ("ICO file       : {0} ({1} bytes, entries: {2})" -f $icoPath, $icoBytes.Length, ($icoSizes -join "/"))
