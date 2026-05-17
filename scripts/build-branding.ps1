#Requires -Version 5.1
[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
try { Add-Type -AssemblyName System.Drawing.Common -ErrorAction Stop } catch {}

$repoRoot = Split-Path -Parent $PSScriptRoot
$out = "$repoRoot\installer\Branding"
if (-not (Test-Path $out)) { New-Item -ItemType Directory -Path $out -Force | Out-Null }

function Render-Sidebar {
    param([int]$W, [int]$H, [string]$Path, [int]$Scale = 2)

    $sw = $W * $Scale
    $sh = $H * $Scale
    $bmp = New-Object System.Drawing.Bitmap $sw, $sh, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.TextRenderingHint  = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

    # Right two-thirds: clean light surface (default Windows dialog background)
    $bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#F0F0F0'))
    $g.FillRectangle($bgBrush, 0, 0, $sw, $sh)
    $bgBrush.Dispose()

    # Left strip: crimson, 100 DLU equivalent at 493 wide = ~100px (x scale)
    $stripW = [int](100 * $Scale)
    $stripRect = New-Object System.Drawing.Rectangle 0, 0, $stripW, $sh
    $deepCrimson  = [System.Drawing.ColorTranslator]::FromHtml('#5C0D18')
    $midCrimson   = [System.Drawing.ColorTranslator]::FromHtml('#8B1424')
    $brightAccent = [System.Drawing.ColorTranslator]::FromHtml('#A01828')

    $linear = New-Object System.Drawing.Drawing2D.LinearGradientBrush $stripRect, $deepCrimson, $deepCrimson, 135
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend 4
    $blend.Colors    = @($deepCrimson, $brightAccent, $midCrimson, $deepCrimson)
    $blend.Positions = @(0.0, 0.30, 0.65, 1.0)
    $linear.InterpolationColors = $blend
    $g.FillRectangle($linear, $stripRect)
    $linear.Dispose()

    # Top-left radial highlight on the strip (warm glow)
    $hl = New-Object System.Drawing.Rectangle (-$stripW * 0.5), (-$sh * 0.4), ($stripW * 2), ($sh * 1.4)
    $hlPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $hlPath.AddEllipse($hl)
    $radial = New-Object System.Drawing.Drawing2D.PathGradientBrush $hlPath
    $radial.CenterColor = [System.Drawing.Color]::FromArgb(60, 255, 220, 200)
    $radial.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 255, 255, 255))
    $g.FillPath($radial, $hlPath)
    $radial.Dispose()
    $hlPath.Dispose()

    # Strip edge: 1px crimson-shadow column at the right edge of the strip for separation
    $edgePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(40, 0, 0, 0)), ($Scale * 1.0)
    $g.DrawLine($edgePen, $stripW, 0, $stripW, $sh)
    $edgePen.Dispose()

    # GK mark on the strip -- centered horizontally on the strip, near the top
    $markSize = [int](56 * $Scale)
    $markX = [int](($stripW - $markSize) / 2)
    $markY = [int](38 * $Scale)
    $cornerRadius = [int](6 * $Scale)
    $arc = $cornerRadius * 2

    # Drop shadow
    $shadowPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $sox = [int](2 * $Scale); $soy = [int](4 * $Scale)
    $shadowPath.AddArc(($markX + $sox), ($markY + $soy), $arc, $arc, 180, 90)
    $shadowPath.AddArc(($markX + $markSize - $arc + $sox), ($markY + $soy), $arc, $arc, 270, 90)
    $shadowPath.AddArc(($markX + $markSize - $arc + $sox), ($markY + $markSize - $arc + $soy), $arc, $arc, 0, 90)
    $shadowPath.AddArc(($markX + $sox), ($markY + $markSize - $arc + $soy), $arc, $arc, 90, 90)
    $shadowPath.CloseFigure()
    $shadowBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(80, 0, 0, 0))
    $g.FillPath($shadowBrush, $shadowPath)
    $shadowBrush.Dispose()
    $shadowPath.Dispose()

    # White rounded square
    $markPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $markPath.AddArc($markX, $markY, $arc, $arc, 180, 90)
    $markPath.AddArc(($markX + $markSize - $arc), $markY, $arc, $arc, 270, 90)
    $markPath.AddArc(($markX + $markSize - $arc), ($markY + $markSize - $arc), $arc, $arc, 0, 90)
    $markPath.AddArc($markX, ($markY + $markSize - $arc), $arc, $arc, 90, 90)
    $markPath.CloseFigure()
    $g.FillPath([System.Drawing.Brushes]::White, $markPath)
    $markPath.Dispose()

    # GK text in mark
    $markFontSize = [Math]::Round($markSize * 0.46)
    $markFont = [System.Drawing.Font]::new('Segoe UI', $markFontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $markFmt = New-Object System.Drawing.StringFormat
    $markFmt.Alignment = 'Center'
    $markFmt.LineAlignment = 'Center'
    $markBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#8B1424'))
    $markRect = New-Object System.Drawing.RectangleF $markX, $markY, $markSize, $markSize
    $g.DrawString('GK', $markFont, $markBrush, $markRect, $markFmt)
    $markBrush.Dispose()
    $markFont.Dispose()

    # Below the mark on the strip: "GRACEKEEPER" rotated 90deg CCW, reads bottom-to-top
    $textState = $g.Save()
    $textX = [int]($stripW / 2)
    $textY = [int]($markY + $markSize + 24 * $Scale)
    $g.TranslateTransform($textX, $textY)
    $g.RotateTransform(-90)
    $vfont = [System.Drawing.Font]::new('Segoe UI', ([int](14 * $Scale)), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $vfmt = New-Object System.Drawing.StringFormat
    $vfmt.Alignment = 'Near'
    $vfmt.LineAlignment = 'Center'
    $g.DrawString('GRACEKEEPER', $vfont, [System.Drawing.Brushes]::White, 0, 0, $vfmt)
    $vfont.Dispose()
    $g.Restore($textState)

    $g.Dispose()

    # Downsample
    $final = New-Object System.Drawing.Bitmap $W, $H, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g2 = [System.Drawing.Graphics]::FromImage($final)
    $g2.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g2.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g2.DrawImage($bmp, 0, 0, $W, $H)
    $g2.Dispose()
    $bmp.Dispose()
    $final.Save($Path, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $final.Dispose()
    Write-Host "Wrote $Path  ($((Get-Item $Path).Length) bytes)"
}

function Render-Banner {
    param([int]$W, [int]$H, [string]$Path, [int]$Scale = 2)

    $sw = $W * $Scale
    $sh = $H * $Scale
    $bmp = New-Object System.Drawing.Bitmap $sw, $sh, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.TextRenderingHint  = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

    # Light bg matching dialog
    $bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#F0F0F0'))
    $g.FillRectangle($bgBrush, 0, 0, $sw, $sh)
    $bgBrush.Dispose()

    # Crimson strip on the LEFT only -- same width as sidebar
    $stripW = [int](100 * $Scale)
    $stripRect = New-Object System.Drawing.Rectangle 0, 0, $stripW, $sh
    $deepCrimson  = [System.Drawing.ColorTranslator]::FromHtml('#5C0D18')
    $midCrimson   = [System.Drawing.ColorTranslator]::FromHtml('#8B1424')
    $brightAccent = [System.Drawing.ColorTranslator]::FromHtml('#A01828')
    $linear = New-Object System.Drawing.Drawing2D.LinearGradientBrush $stripRect, $brightAccent, $deepCrimson, 135
    $g.FillRectangle($linear, $stripRect)
    $linear.Dispose()

    # Mini GK mark on the strip, centered vertically
    $markSize = [int](28 * $Scale)
    $markX = [int](($stripW - $markSize) / 2)
    $markY = [int](($sh - $markSize) / 2)
    $cornerRadius = [int](4 * $Scale)
    $arc = $cornerRadius * 2

    $markPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $markPath.AddArc($markX, $markY, $arc, $arc, 180, 90)
    $markPath.AddArc(($markX + $markSize - $arc), $markY, $arc, $arc, 270, 90)
    $markPath.AddArc(($markX + $markSize - $arc), ($markY + $markSize - $arc), $arc, $arc, 0, 90)
    $markPath.AddArc($markX, ($markY + $markSize - $arc), $arc, $arc, 90, 90)
    $markPath.CloseFigure()
    $g.FillPath([System.Drawing.Brushes]::White, $markPath)
    $markPath.Dispose()

    $markFontSize = [Math]::Round($markSize * 0.50)
    $markFont = [System.Drawing.Font]::new('Segoe UI', $markFontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $markFmt = New-Object System.Drawing.StringFormat
    $markFmt.Alignment = 'Center'; $markFmt.LineAlignment = 'Center'
    $markBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#8B1424'))
    $g.DrawString('GK', $markFont, $markBrush, (New-Object System.Drawing.RectangleF $markX, $markY, $markSize, $markSize), $markFmt)
    $markBrush.Dispose()
    $markFont.Dispose()

    # Edge separator
    $edgePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(60, 0, 0, 0)), ($Scale * 1.0)
    $g.DrawLine($edgePen, $stripW, 0, $stripW, $sh)
    $edgePen.Dispose()

    $g.Dispose()

    $final = New-Object System.Drawing.Bitmap $W, $H, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g2 = [System.Drawing.Graphics]::FromImage($final)
    $g2.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g2.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g2.DrawImage($bmp, 0, 0, $W, $H)
    $g2.Dispose()
    $bmp.Dispose()
    $final.Save($Path, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $final.Dispose()
    Write-Host "Wrote $Path  ($((Get-Item $Path).Length) bytes)"
}

Render-Sidebar -W 493 -H 312 -Path "$out\sidebar.bmp" -Scale 2
Render-Banner -W 493 -H 58  -Path "$out\banner.bmp"  -Scale 2

Write-Host "`nBranding regenerated. Re-run scripts\build-local.ps1 to package into MSI."
