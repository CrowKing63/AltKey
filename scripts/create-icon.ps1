<#
.SYNOPSIS
    AltKey 아이콘을 GDI+로 그려서 ICO / PNG 파일로 저장합니다.
    DIB 형식으로 ICO를 만들어 .NET 컴파일러와 호환성을 보장합니다.
.EXAMPLE
    .\scripts\create-icon.ps1
#>

Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot "..\AltKey\Assets"
New-Item -ItemType Directory -Force $outDir | Out-Null

function New-AltKeyBitmap([int]$size) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    # 둥근 사각형 배경 (파란색)
    $radius  = [int]($size * 0.20)
    $bgBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 37, 99, 235))
    $rect    = [System.Drawing.Rectangle]::new(0, 0, $size - 1, $size - 1)
    $path    = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($rect.X,                   $rect.Y,                   $radius*2, $radius*2, 180, 90)
    $path.AddArc($rect.Right - $radius*2,   $rect.Y,                   $radius*2, $radius*2, 270, 90)
    $path.AddArc($rect.Right - $radius*2,   $rect.Bottom - $radius*2,  $radius*2, $radius*2,   0, 90)
    $path.AddArc($rect.X,                   $rect.Bottom - $radius*2,  $radius*2, $radius*2,  90, 90)
    $path.CloseFigure()
    $g.FillPath($bgBrush, $path)

    # "A" 텍스트 (흰색) — 크기 32 이상일 때만
    if ($size -ge 32) {
        $fontSize  = [float]($size * 0.48)
        $font      = [System.Drawing.Font]::new("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $textBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
        $label     = "A"
        $measured  = $g.MeasureString($label, $font)
        $x = ($size - $measured.Width)  / 2.0
        $y = ($size - $measured.Height) / 2.0
        $g.DrawString($label, $font, $textBrush, $x, $y)
        $font.Dispose()
        $textBrush.Dispose()
    } else {
        # 16px: 흰 사각형 dot
        $dot = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
        $ds  = [int]($size * 0.5)
        $dx  = ($size - $ds) / 2
        $dy  = ($size - $ds) / 2
        $g.FillRectangle($dot, $dx, $dy, $ds, $ds)
        $dot.Dispose()
    }

    $g.Dispose()
    $bgBrush.Dispose()
    $path.Dispose()
    return $bmp
}

# PNG 저장 (256px)
$png256  = New-AltKeyBitmap 256
$pngPath = Join-Path $outDir "icon.png"
$png256.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$png256.Dispose()
Write-Host "Saved: $pngPath"

# ICO 저장 — DIB 형식 (BITMAPINFOHEADER + pixel data)
function Save-IcoDib([string]$icoPath, [int[]]$iconSizes) {
    $ms     = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($ms)

    $bitmaps  = $iconSizes | ForEach-Object { New-AltKeyBitmap $_ }
    $dibStreams = [System.IO.MemoryStream[]]::new($bitmaps.Count)

    for ($i = 0; $i -lt $bitmaps.Count; $i++) {
        $bmp = $bitmaps[$i]
        $sz  = $iconSizes[$i]
        $ds  = [System.IO.MemoryStream]::new()
        $dw  = [System.IO.BinaryWriter]::new($ds)

        # BITMAPINFOHEADER (40 bytes)
        $dw.Write([uint32]40)              # biSize
        $dw.Write([int32]$sz)              # biWidth
        $dw.Write([int32]($sz * 2))        # biHeight (doubled for ICO)
        $dw.Write([uint16]1)               # biPlanes
        $dw.Write([uint16]32)              # biBitCount
        $dw.Write([uint32]0)               # biCompression (BI_RGB)
        $dw.Write([uint32]0)               # biSizeImage
        $dw.Write([int32]0)                # biXPelsPerMeter
        $dw.Write([int32]0)                # biYPelsPerMeter
        $dw.Write([uint32]0)               # biClrUsed
        $dw.Write([uint32]0)               # biClrImportant

        # XOR mask (pixel data, bottom-up, 32-bit BGRA)
        for ($row = $sz - 1; $row -ge 0; $row--) {
            for ($col = 0; $col -lt $sz; $col++) {
                $px = $bmp.GetPixel($col, $row)
                $dw.Write([byte]$px.B)
                $dw.Write([byte]$px.G)
                $dw.Write([byte]$px.R)
                $dw.Write([byte]$px.A)
            }
        }

        # AND mask (all zeros — use alpha channel)
        $andRowBytes = [int][Math]::Ceiling($sz / 8.0) * 4   # 32-bit aligned
        $andMask     = [byte[]]::new($andRowBytes * $sz)
        $dw.Write($andMask)

        $dw.Flush()
        $dibStreams[$i] = $ds
        $bitmaps[$i].Dispose()
    }

    # ICONDIR header
    $writer.Write([uint16]0)                    # idReserved
    $writer.Write([uint16]1)                    # idType (1=ICO)
    $writer.Write([uint16]$iconSizes.Count)     # idCount

    # ICONDIRENTRY list
    $dataOffset = [uint32](6 + 16 * $iconSizes.Count)
    for ($i = 0; $i -lt $iconSizes.Count; $i++) {
        $sz     = $iconSizes[$i]
        $szByte = if ($sz -eq 256) { [byte]0 } else { [byte]$sz }
        $writer.Write($szByte)                              # bWidth
        $writer.Write($szByte)                              # bHeight
        $writer.Write([byte]0)                              # bColorCount
        $writer.Write([byte]0)                              # bReserved
        $writer.Write([uint16]1)                            # wPlanes
        $writer.Write([uint16]32)                           # wBitCount
        $writer.Write([uint32]$dibStreams[$i].Length)       # dwBytesInRes
        $writer.Write($dataOffset)                          # dwImageOffset
        $dataOffset += [uint32]$dibStreams[$i].Length
    }

    # image data
    foreach ($ds in $dibStreams) {
        $ds.Position = 0
        $ds.CopyTo($ms)
        $ds.Dispose()
    }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
    $ms.Dispose()
    $writer.Dispose()
}

$icoPath  = Join-Path $outDir "icon.ico"
$trayPath = Join-Path $outDir "tray-icon.ico"

Write-Host "Generating icon.ico..."
Save-IcoDib $icoPath  @(16, 32, 48, 256)
Write-Host "Saved: $icoPath"

Write-Host "Generating tray-icon.ico..."
Save-IcoDib $trayPath @(16, 32)
Write-Host "Saved: $trayPath"

Write-Host "Done." -ForegroundColor Green
