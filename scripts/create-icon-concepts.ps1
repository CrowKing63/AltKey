<#
.SYNOPSIS
    Generate three AltKey icon redesign concepts and a comparison board.

.DESCRIPTION
    Keeps the original app icon intact and writes preview PNG files only under
    docs/assets/icon-concepts.

.EXAMPLE
    .\scripts\create-icon-concepts.ps1
#>

Add-Type -AssemblyName System.Drawing

$rootPath = Split-Path -Parent $PSScriptRoot
$sourceIconPath = Join-Path $rootPath "AltKey\Assets\Icon.png"
$outputDir = Join-Path $rootPath "docs\assets\icon-concepts"

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

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

function Initialize-Canvas {
    param([int]$Size)

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.Clear([System.Drawing.Color]::Transparent)

    return @{
        Bitmap   = $bitmap
        Graphics = $graphics
    }
}

function Add-BaseShadow {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Size
    )

    $shadowRect = [System.Drawing.RectangleF]::new($Size * 0.06, $Size * 0.07, $Size * 0.88, $Size * 0.86)
    $shadowPath = New-RoundedRectPath -Rect $shadowRect -Radius ($Size * 0.17)
    $shadowBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(58, 9, 40, 74))
    $Graphics.FillPath($shadowBrush, $shadowPath)
    $shadowBrush.Dispose()
    $shadowPath.Dispose()
}

function Add-RoundedGradientTile {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Size,
        [System.Drawing.Color]$StartColor,
        [System.Drawing.Color]$EndColor,
        [System.Drawing.Color]$OutlineColor
    )

    $tileRect = [System.Drawing.RectangleF]::new($Size * 0.045, $Size * 0.03, $Size * 0.89, $Size * 0.89)
    $tilePath = New-RoundedRectPath -Rect $tileRect -Radius ($Size * 0.16)
    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new($tileRect.Left, $tileRect.Bottom),
        [System.Drawing.PointF]::new($tileRect.Right, $tileRect.Top),
        $StartColor,
        $EndColor
    )
    $Graphics.FillPath($brush, $tilePath)

    # Add a soft top highlight so the tile keeps some depth.
    $highlightRect = [System.Drawing.RectangleF]::new($tileRect.Left + 8, $tileRect.Top + 8, $tileRect.Width - 16, $tileRect.Height * 0.36)
    $highlightPath = New-RoundedRectPath -Rect $highlightRect -Radius ($Size * 0.13)
    $highlightBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new($highlightRect.Left, $highlightRect.Top),
        [System.Drawing.PointF]::new($highlightRect.Left, $highlightRect.Bottom),
        [System.Drawing.Color]::FromArgb(90, 255, 255, 255),
        [System.Drawing.Color]::FromArgb(5, 255, 255, 255)
    )
    $Graphics.FillPath($highlightBrush, $highlightPath)

    $outlinePen = [System.Drawing.Pen]::new($OutlineColor, [float]($Size * 0.01))
    $Graphics.DrawPath($outlinePen, $tilePath)

    $outlinePen.Dispose()
    $highlightBrush.Dispose()
    $highlightPath.Dispose()
    $brush.Dispose()
    $tilePath.Dispose()

    return $tileRect
}

function Add-CircularArrow {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Size,
        [System.Drawing.Color]$Color
    )

    $pen = [System.Drawing.Pen]::new($Color, [float]($Size * 0.075))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $leftArcRect = [System.Drawing.RectangleF]::new($Size * 0.12, $Size * 0.43, $Size * 0.34, $Size * 0.34)
    $rightArcRect = [System.Drawing.RectangleF]::new($Size * 0.53, $Size * 0.43, $Size * 0.24, $Size * 0.24)
    $Graphics.DrawArc($pen, $leftArcRect, 108, 204)
    $Graphics.DrawArc($pen, $rightArcRect, 282, 156)

    $arrowPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $arrowPoints = @(
        [System.Drawing.PointF]::new($Size * 0.53, $Size * 0.76),
        [System.Drawing.PointF]::new($Size * 0.70, $Size * 0.76),
        [System.Drawing.PointF]::new($Size * 0.57, $Size * 0.88)
    )
    $arrowPath.AddPolygon($arrowPoints)
    $arrowBrush = [System.Drawing.SolidBrush]::new($Color)
    $Graphics.FillPath($arrowBrush, $arrowPath)

    $arrowBrush.Dispose()
    $arrowPath.Dispose()
    $pen.Dispose()
}

function Add-PencilBadge {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Size
    )

    $badgeRect = [System.Drawing.RectangleF]::new($Size * 0.63, $Size * 0.63, $Size * 0.22, $Size * 0.22)
    $badgeBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(236, 15, 23, 42))
    $Graphics.FillEllipse($badgeBrush, $badgeRect)

    $state = $Graphics.Save()
    $Graphics.TranslateTransform($badgeRect.X + $badgeRect.Width / 2, $badgeRect.Y + $badgeRect.Height / 2)
    $Graphics.RotateTransform(-38)

    $bodyRect = [System.Drawing.RectangleF]::new(-$Size * 0.06, -$Size * 0.012, $Size * 0.12, $Size * 0.024)
    $bodyBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 252, 210, 77))
    $Graphics.FillRectangle($bodyBrush, $bodyRect)

    $tipPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $tipPath.AddPolygon(@(
        [System.Drawing.PointF]::new($bodyRect.Right, $bodyRect.Top),
        [System.Drawing.PointF]::new($bodyRect.Right + $Size * 0.03, 0),
        [System.Drawing.PointF]::new($bodyRect.Right, $bodyRect.Bottom)
    ))
    $tipBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 245, 244, 240))
    $Graphics.FillPath($tipBrush, $tipPath)

    $leadPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $leadPath.AddPolygon(@(
        [System.Drawing.PointF]::new($bodyRect.Right + $Size * 0.03, 0),
        [System.Drawing.PointF]::new($bodyRect.Right + $Size * 0.014, -$Size * 0.008),
        [System.Drawing.PointF]::new($bodyRect.Right + $Size * 0.014, $Size * 0.008)
    ))
    $leadBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 30, 41, 59))
    $Graphics.FillPath($leadBrush, $leadPath)

    $eraserRect = [System.Drawing.RectangleF]::new($bodyRect.Left - $Size * 0.03, -$Size * 0.012, $Size * 0.03, $Size * 0.024)
    $eraserBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 251, 146, 179))
    $Graphics.FillRectangle($eraserBrush, $eraserRect)

    $bandRect = [System.Drawing.RectangleF]::new($bodyRect.Left - $Size * 0.014, -$Size * 0.012, $Size * 0.012, $Size * 0.024)
    $bandBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 226, 232, 240))
    $Graphics.FillRectangle($bandBrush, $bandRect)

    $Graphics.Restore($state)

    $bandBrush.Dispose()
    $eraserBrush.Dispose()
    $leadBrush.Dispose()
    $leadPath.Dispose()
    $tipBrush.Dispose()
    $tipPath.Dispose()
    $bodyBrush.Dispose()
    $badgeBrush.Dispose()
}

function DrawGrayscaleImage {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Image]$Image,
        [System.Drawing.Rectangle]$Destination
    )

    $attributes = [System.Drawing.Imaging.ImageAttributes]::new()
    $matrix = [System.Drawing.Imaging.ColorMatrix]::new(@(
        @(0.299, 0.299, 0.299, 0, 0),
        @(0.587, 0.587, 0.587, 0, 0),
        @(0.114, 0.114, 0.114, 0, 0),
        @(0, 0, 0, 1, 0),
        @(0.04, 0.04, 0.04, 0, 1)
    ))
    $attributes.SetColorMatrix($matrix)
    $Graphics.DrawImage(
        $Image,
        $Destination,
        0,
        0,
        $Image.Width,
        $Image.Height,
        [System.Drawing.GraphicsUnit]::Pixel,
        $attributes
    )
    $attributes.Dispose()
}

function Add-LargePencilOverlay {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Size
    )

    $state = $Graphics.Save()
    $Graphics.TranslateTransform($Size * 0.50, $Size * 0.54)
    $Graphics.RotateTransform(-36)

    # Keep the shape simple so it still reads as a pencil at 16px and 32px.
    $bodyRect = [System.Drawing.RectangleF]::new(-$Size * 0.27, -$Size * 0.035, $Size * 0.44, $Size * 0.07)
    $bodyBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(248, 251, 191, 36))
    $bodyPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(220, 15, 23, 42), [float]($Size * 0.01))
    $Graphics.FillRectangle($bodyBrush, $bodyRect)
    $Graphics.DrawRectangle($bodyPen, $bodyRect.X, $bodyRect.Y, $bodyRect.Width, $bodyRect.Height)

    $tipPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $tipPath.AddPolygon(@(
        [System.Drawing.PointF]::new($bodyRect.Right, $bodyRect.Top),
        [System.Drawing.PointF]::new($bodyRect.Right + $Size * 0.095, 0),
        [System.Drawing.PointF]::new($bodyRect.Right, $bodyRect.Bottom)
    ))
    $tipBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(248, 245, 245, 244))
    $tipPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(220, 15, 23, 42), [float]($Size * 0.01))
    $Graphics.FillPath($tipBrush, $tipPath)
    $Graphics.DrawPath($tipPen, $tipPath)

    $leadPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $leadPath.AddPolygon(@(
        [System.Drawing.PointF]::new($bodyRect.Right + $Size * 0.095, 0),
        [System.Drawing.PointF]::new($bodyRect.Right + $Size * 0.052, -$Size * 0.018),
        [System.Drawing.PointF]::new($bodyRect.Right + $Size * 0.052, $Size * 0.018)
    ))
    $leadBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(248, 30, 41, 59))
    $Graphics.FillPath($leadBrush, $leadPath)

    $eraserRect = [System.Drawing.RectangleF]::new($bodyRect.Left - $Size * 0.07, -$Size * 0.035, $Size * 0.07, $Size * 0.07)
    $eraserBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(248, 251, 146, 179))
    $Graphics.FillRectangle($eraserBrush, $eraserRect)
    $Graphics.DrawRectangle($bodyPen, $eraserRect.X, $eraserRect.Y, $eraserRect.Width, $eraserRect.Height)

    $bandRect = [System.Drawing.RectangleF]::new($bodyRect.Left - $Size * 0.024, -$Size * 0.035, $Size * 0.022, $Size * 0.07)
    $bandBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(248, 226, 232, 240))
    $Graphics.FillRectangle($bandBrush, $bandRect)

    $Graphics.Restore($state)

    $bandBrush.Dispose()
    $eraserBrush.Dispose()
    $leadBrush.Dispose()
    $leadPath.Dispose()
    $tipPen.Dispose()
    $tipBrush.Dispose()
    $tipPath.Dispose()
    $bodyPen.Dispose()
    $bodyBrush.Dispose()
}

function Add-KoreanGlyph {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Size,
        [string]$Glyph,
        [float]$FontScale,
        [System.Drawing.Color]$Color,
        [float]$YOffsetRatio = 0.0
    )

    $fontSize = [float]($Size * $FontScale)
    $font = [System.Drawing.Font]::new("Malgun Gothic", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = [System.Drawing.SolidBrush]::new($Color)
    $shadowBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(55, 0, 0, 0))

    $measured = $Graphics.MeasureString($Glyph, $font)
    $x = ($Size - $measured.Width) / 2
    $y = (($Size - $measured.Height) / 2) + ($Size * $YOffsetRatio)

    $Graphics.DrawString($Glyph, $font, $shadowBrush, $x + ($Size * 0.008), $y + ($Size * 0.012))
    $Graphics.DrawString($Glyph, $font, $brush, $x, $y)

    $shadowBrush.Dispose()
    $brush.Dispose()
    $font.Dispose()
}

function Add-LatinGlyph {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Size,
        [string]$Glyph,
        [float]$FontScale,
        [System.Drawing.Color]$Color,
        [float]$YOffsetRatio = 0.0
    )

    $fontSize = [float]($Size * $FontScale)
    $font = [System.Drawing.Font]::new("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = [System.Drawing.SolidBrush]::new($Color)
    $shadowBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(45, 0, 0, 0))

    $measured = $Graphics.MeasureString($Glyph, $font)
    $x = ($Size - $measured.Width) / 2
    $y = (($Size - $measured.Height) / 2) + ($Size * $YOffsetRatio)

    $Graphics.DrawString($Glyph, $font, $shadowBrush, $x + ($Size * 0.006), $y + ($Size * 0.01))
    $Graphics.DrawString($Glyph, $font, $brush, $x, $y)

    $shadowBrush.Dispose()
    $brush.Dispose()
    $font.Dispose()
}

function Add-CenteredGlyphInRect {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.RectangleF]$Rect,
        [string]$Glyph,
        [string]$FontFamily,
        [float]$FontSize,
        [System.Drawing.Color]$Color
    )

    $font = [System.Drawing.Font]::new($FontFamily, $FontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = [System.Drawing.SolidBrush]::new($Color)
    $shadowBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(35, 15, 23, 42))
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center

    $shadowRect = [System.Drawing.RectangleF]::new($Rect.X + 1.8, $Rect.Y + 2.2, $Rect.Width, $Rect.Height)
    $Graphics.DrawString($Glyph, $font, $shadowBrush, $shadowRect, $format)
    $Graphics.DrawString($Glyph, $font, $brush, $Rect, $format)

    $format.Dispose()
    $shadowBrush.Dispose()
    $brush.Dispose()
    $font.Dispose()
}

function Add-GearBadge {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Size,
        [float]$CenterX,
        [float]$CenterY,
        [float]$OuterRadius,
        [System.Drawing.Color]$Color
    )

    $state = $Graphics.Save()
    $Graphics.TranslateTransform($CenterX, $CenterY)
    $gearBrush = [System.Drawing.SolidBrush]::new($Color)

    for ($i = 0; $i -lt 8; $i++) {
        $Graphics.RotateTransform(45)
        $Graphics.FillRectangle($gearBrush, -$OuterRadius * 0.12, -$OuterRadius * 1.2, $OuterRadius * 0.24, $OuterRadius * 0.42)
    }

    $Graphics.ResetTransform()
    $Graphics.TranslateTransform($CenterX, $CenterY)
    $Graphics.FillEllipse($gearBrush, -$OuterRadius, -$OuterRadius, $OuterRadius * 2, $OuterRadius * 2)

    $holeBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 17, 24, 39))
    $Graphics.FillEllipse($holeBrush, -$OuterRadius * 0.42, -$OuterRadius * 0.42, $OuterRadius * 0.84, $OuterRadius * 0.84)

    $Graphics.Restore($state)

    $holeBrush.Dispose()
    $gearBrush.Dispose()
}

function Save-ConceptA {
    param([string]$OutputPath)

    $size = 256
    $canvas = Initialize-Canvas -Size $size
    $bitmap = $canvas.Bitmap
    $graphics = $canvas.Graphics

    Add-BaseShadow -Graphics $graphics -Size $size

    $source = [System.Drawing.Image]::FromFile($sourceIconPath)
    $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, $size, $size))

    # Add a pencil badge so this concept reads as an editor/tool app.
    Add-PencilBadge -Graphics $graphics -Size $size

    $source.Dispose()
    $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

function Save-ConceptGa {
    param([string]$OutputPath)

    $size = 256
    $canvas = Initialize-Canvas -Size $size
    $bitmap = $canvas.Bitmap
    $graphics = $canvas.Graphics

    Add-BaseShadow -Graphics $graphics -Size $size
    Add-RoundedGradientTile -Graphics $graphics -Size $size `
        -StartColor ([System.Drawing.Color]::FromArgb(255, 29, 78, 216)) `
        -EndColor ([System.Drawing.Color]::FromArgb(255, 45, 212, 191)) `
        -OutlineColor ([System.Drawing.Color]::FromArgb(130, 255, 255, 255)) | Out-Null

    Add-CircularArrow -Graphics $graphics -Size $size -Color ([System.Drawing.Color]::FromArgb(220, 235, 249, 255))
    Add-KoreanGlyph -Graphics $graphics -Size $size -Glyph $gaGlyph -FontScale 0.40 -Color ([System.Drawing.Color]::FromArgb(255, 255, 255, 255)) -YOffsetRatio -0.08

    $accentBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(185, 226, 232, 240))
    $accentRect = [System.Drawing.RectangleF]::new($size * 0.35, $size * 0.59, $size * 0.30, $size * 0.06)
    $accentPath = New-RoundedRectPath -Rect $accentRect -Radius ($size * 0.02)
    $graphics.FillPath($accentBrush, $accentPath)
    $accentPath.Dispose()
    $accentBrush.Dispose()

    $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

function Save-ConceptFluent {
    param([string]$OutputPath)

    $size = 256
    $canvas = Initialize-Canvas -Size $size
    $bitmap = $canvas.Bitmap
    $graphics = $canvas.Graphics

    Add-BaseShadow -Graphics $graphics -Size $size
    Add-RoundedGradientTile -Graphics $graphics -Size $size `
        -StartColor ([System.Drawing.Color]::FromArgb(255, 17, 24, 39)) `
        -EndColor ([System.Drawing.Color]::FromArgb(255, 14, 165, 233)) `
        -OutlineColor ([System.Drawing.Color]::FromArgb(125, 196, 244, 255)) | Out-Null

    # Use simple cards to suggest tool windows and panel layout.
    $panelBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(210, 255, 255, 255))
    $panelShadow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(40, 15, 23, 42))
    foreach ($panel in @(
        [System.Drawing.RectangleF]::new($size * 0.19, $size * 0.21, $size * 0.28, $size * 0.22),
        [System.Drawing.RectangleF]::new($size * 0.53, $size * 0.21, $size * 0.19, $size * 0.22),
        [System.Drawing.RectangleF]::new($size * 0.19, $size * 0.48, $size * 0.53, $size * 0.14)
    )) {
        $shadowRect = [System.Drawing.RectangleF]::new($panel.X + 4, $panel.Y + 5, $panel.Width, $panel.Height)
        $shadowPath = New-RoundedRectPath -Rect $shadowRect -Radius ($size * 0.035)
        $panelPath = New-RoundedRectPath -Rect $panel -Radius ($size * 0.035)
        $graphics.FillPath($panelShadow, $shadowPath)
        $graphics.FillPath($panelBrush, $panelPath)
        $shadowPath.Dispose()
        $panelPath.Dispose()
    }

    Add-CenteredGlyphInRect -Graphics $graphics `
        -Rect ([System.Drawing.RectangleF]::new($size * 0.19, $size * 0.21, $size * 0.28, $size * 0.22)) `
        -Glyph "A" `
        -FontFamily "Segoe UI" `
        -FontSize 36 `
        -Color ([System.Drawing.Color]::FromArgb(255, 8, 47, 73))
    Add-CenteredGlyphInRect -Graphics $graphics `
        -Rect ([System.Drawing.RectangleF]::new($size * 0.53, $size * 0.21, $size * 0.19, $size * 0.22)) `
        -Glyph $gaGlyph `
        -FontFamily "Malgun Gothic" `
        -FontSize 28 `
        -Color ([System.Drawing.Color]::FromArgb(255, 8, 47, 73))
    Add-GearBadge -Graphics $graphics -Size $size -CenterX ($size * 0.69) -CenterY ($size * 0.69) -OuterRadius ($size * 0.10) -Color ([System.Drawing.Color]::FromArgb(255, 255, 255, 255))

    $linePen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(180, 255, 255, 255), [float]($size * 0.018))
    $linePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $linePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($linePen, $size * 0.24, $size * 0.56, $size * 0.67, $size * 0.56)
    $graphics.DrawLine($linePen, $size * 0.24, $size * 0.64, $size * 0.53, $size * 0.64)

    $linePen.Dispose()
    $panelShadow.Dispose()
    $panelBrush.Dispose()

    $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

function Save-ConceptMono {
    param([string]$OutputPath)

    $size = 256
    $canvas = Initialize-Canvas -Size $size
    $bitmap = $canvas.Bitmap
    $graphics = $canvas.Graphics

    Add-BaseShadow -Graphics $graphics -Size $size

    $source = [System.Drawing.Image]::FromFile($sourceIconPath)
    DrawGrayscaleImage -Graphics $graphics -Image $source -Destination ([System.Drawing.Rectangle]::new(0, 0, $size, $size))

    # Add a dark translucent wash so the icon reads as a separate tools variant.
    $washPath = New-RoundedRectPath -Rect ([System.Drawing.RectangleF]::new($size * 0.045, $size * 0.03, $size * 0.89, $size * 0.89)) -Radius ($size * 0.16)
    $washBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(26, 17, 24, 39))
    $graphics.FillPath($washBrush, $washPath)

    $washBrush.Dispose()
    $washPath.Dispose()
    $source.Dispose()
    $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

function Save-ConceptCrossPencil {
    param([string]$OutputPath)

    $size = 256
    $canvas = Initialize-Canvas -Size $size
    $bitmap = $canvas.Bitmap
    $graphics = $canvas.Graphics

    Add-BaseShadow -Graphics $graphics -Size $size

    $source = [System.Drawing.Image]::FromFile($sourceIconPath)
    $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, $size, $size))

    $strokePen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(110, 15, 23, 42), [float]($size * 0.02))
    $graphics.DrawLine($strokePen, $size * 0.22, $size * 0.70, $size * 0.76, $size * 0.31)
    Add-LargePencilOverlay -Graphics $graphics -Size $size

    $strokePen.Dispose()
    $source.Dispose()
    $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

function Add-PreviewTile {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Image]$Image,
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [string]$Title,
        [string]$Subtitle
    )

    $cardRect = [System.Drawing.RectangleF]::new($X, $Y, $Width, $Height)
    $cardPath = New-RoundedRectPath -Rect $cardRect -Radius 18
    $cardBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(232, 255, 255, 255))
    $borderPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(24, 15, 23, 42), 1.5)
    $Graphics.FillPath($cardBrush, $cardPath)
    $Graphics.DrawPath($borderPen, $cardPath)

    $Graphics.DrawImage($Image, [System.Drawing.RectangleF]::new($X + 18, $Y + 18, 192, 192))

    foreach ($sample in @(
        @{ Size = 32; Back = [System.Drawing.Color]::FromArgb(255, 255, 255, 255); X = $X + 235; Y = $Y + 42 },
        @{ Size = 32; Back = [System.Drawing.Color]::FromArgb(255, 31, 41, 55); X = $X + 287; Y = $Y + 42 },
        @{ Size = 16; Back = [System.Drawing.Color]::FromArgb(255, 255, 255, 255); X = $X + 243; Y = $Y + 103 },
        @{ Size = 16; Back = [System.Drawing.Color]::FromArgb(255, 31, 41, 55); X = $X + 287; Y = $Y + 103 }
    )) {
        $sampleBrush = [System.Drawing.SolidBrush]::new($sample.Back)
        $Graphics.FillRectangle($sampleBrush, $sample.X - 6, $sample.Y - 6, $sample.Size + 12, $sample.Size + 12)
        $Graphics.DrawImage($Image, [System.Drawing.RectangleF]::new($sample.X, $sample.Y, $sample.Size, $sample.Size))
        $sampleBrush.Dispose()
    }

    $titleFont = [System.Drawing.Font]::new("Segoe UI", 15, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $subtitleFont = [System.Drawing.Font]::new("Segoe UI", 11, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $textBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 17, 24, 39))
    $subBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 71, 85, 105))
    $Graphics.DrawString($Title, $titleFont, $textBrush, $X + 18, $Y + 224)
    $Graphics.DrawString($Subtitle, $subtitleFont, $subBrush, [System.Drawing.RectangleF]::new($X + 18, $Y + 252, $Width - 36, 52))

    $subBrush.Dispose()
    $textBrush.Dispose()
    $subtitleFont.Dispose()
    $titleFont.Dispose()
    $borderPen.Dispose()
    $cardBrush.Dispose()
    $cardPath.Dispose()
}

function Save-ConceptBoard {
    param(
        [string]$OutputPath,
        [string[]]$SourcePaths
    )

    $bitmap = [System.Drawing.Bitmap]::new(1180, 420, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $backgroundBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(0, 0),
        [System.Drawing.PointF]::new(1180, 420),
        [System.Drawing.Color]::FromArgb(255, 241, 245, 249),
        [System.Drawing.Color]::FromArgb(255, 226, 232, 240)
    )
    $graphics.FillRectangle($backgroundBrush, 0, 0, 1180, 420)

    $titleFont = [System.Drawing.Font]::new("Segoe UI", 24, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $subtitleFont = [System.Drawing.Font]::new("Segoe UI", 12, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $titleBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 15, 23, 42))
    $subtitleBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 71, 85, 105))
    $graphics.DrawString("AltKey Icon Concepts", $titleFont, $titleBrush, 34, 24)
    $graphics.DrawString("256px main preview + 32px / 16px readability check on light and dark surfaces", $subtitleFont, $subtitleBrush, 36, 62)

    $titles = @("A + Pencil", "Korean 'ga'", "Fluent Hybrid")
    $subtitles = @(
        "Keeps the current brand silhouette and adds an editor badge",
        "Puts Korean-first identity at the center of the icon",
        "Rebuilds the mark around tool windows and Fluent-style panels"
    )

    for ($i = 0; $i -lt $SourcePaths.Count; $i++) {
        $image = [System.Drawing.Image]::FromFile($SourcePaths[$i])
        Add-PreviewTile -Graphics $graphics -Image $image -X (34 + ($i * 372)) -Y 96 -Width 338 -Height 290 -Title $titles[$i] -Subtitle $subtitles[$i]
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

function Save-ToolsVariantBoard {
    param(
        [string]$OutputPath,
        [string[]]$SourcePaths
    )

    $bitmap = [System.Drawing.Bitmap]::new(808, 420, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $backgroundBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(0, 0),
        [System.Drawing.PointF]::new(808, 420),
        [System.Drawing.Color]::FromArgb(255, 241, 245, 249),
        [System.Drawing.Color]::FromArgb(255, 226, 232, 240)
    )
    $graphics.FillRectangle($backgroundBrush, 0, 0, 808, 420)

    $titleFont = [System.Drawing.Font]::new("Segoe UI", 24, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $subtitleFont = [System.Drawing.Font]::new("Segoe UI", 12, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $titleBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 15, 23, 42))
    $subtitleBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 71, 85, 105))
    $graphics.DrawString("AltKey Tools Variants", $titleFont, $titleBrush, 34, 24)
    $graphics.DrawString("Two higher-contrast directions for the tools app icon", $subtitleFont, $subtitleBrush, 36, 62)

    $titles = @("Monochrome", "Large Cross Pencil")
    $subtitles = @(
        "Uses the current icon silhouette with a grayscale tools-only tone",
        "Keeps the current icon and makes the pencil part of the main shape"
    )

    for ($i = 0; $i -lt $SourcePaths.Count; $i++) {
        $image = [System.Drawing.Image]::FromFile($SourcePaths[$i])
        Add-PreviewTile -Graphics $graphics -Image $image -X (34 + ($i * 372)) -Y 96 -Width 338 -Height 290 -Title $titles[$i] -Subtitle $subtitles[$i]
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

$conceptAPath = Join-Path $outputDir "altkey-icon-concept-a-edit.png"
$conceptGaPath = Join-Path $outputDir "altkey-icon-concept-ga-korean.png"
$conceptFluentPath = Join-Path $outputDir "altkey-icon-concept-fluent-hybrid.png"
$boardPath = Join-Path $outputDir "altkey-icon-concepts-board.png"
$conceptMonoPath = Join-Path $outputDir "altkey-tools-icon-concept-monochrome.png"
$conceptCrossPencilPath = Join-Path $outputDir "altkey-tools-icon-concept-cross-pencil.png"
$toolsBoardPath = Join-Path $outputDir "altkey-tools-icon-variants-board.png"
$gaGlyph = [string][char]0xAC00

Save-ConceptA -OutputPath $conceptAPath
Save-ConceptGa -OutputPath $conceptGaPath
Save-ConceptFluent -OutputPath $conceptFluentPath
Save-ConceptBoard -OutputPath $boardPath -SourcePaths @($conceptAPath, $conceptGaPath, $conceptFluentPath)
Save-ConceptMono -OutputPath $conceptMonoPath
Save-ConceptCrossPencil -OutputPath $conceptCrossPencilPath
Save-ToolsVariantBoard -OutputPath $toolsBoardPath -SourcePaths @($conceptMonoPath, $conceptCrossPencilPath)

Write-Host "Saved: $conceptAPath"
Write-Host "Saved: $conceptGaPath"
Write-Host "Saved: $conceptFluentPath"
Write-Host "Saved: $boardPath"
Write-Host "Saved: $conceptMonoPath"
Write-Host "Saved: $conceptCrossPencilPath"
Write-Host "Saved: $toolsBoardPath"
