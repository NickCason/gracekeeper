#Requires -Version 5.1
[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
try { Add-Type -AssemblyName System.Drawing.Common -ErrorAction Stop } catch {}

$repoRoot = Split-Path -Parent $PSScriptRoot
$out = "$repoRoot\installer\Branding"
if (-not (Test-Path $out)) { New-Item -ItemType Directory -Path $out -Force | Out-Null }

function Render-Branding {
    param([int]$W, [int]$H, [string]$Path, [int]$MarkSize, [bool]$WithText, [int]$Scale = 2)

    $sw = $W * $Scale
    $sh = $H * $Scale

    $bmp = New-Object System.Drawing.Bitmap $sw, $sh, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.TextRenderingHint  = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

    # Base diagonal gradient: darker upper-left -> brighter mid -> darker lower-right
    $rect = New-Object System.Drawing.Rectangle 0, 0, $sw, $sh
    $deepCrimson  = [System.Drawing.ColorTranslator]::FromHtml('#5C0D18')
    $midCrimson   = [System.Drawing.ColorTranslator]::FromHtml('#8B1424')
    $brightAccent = [System.Drawing.ColorTranslator]::FromHtml('#A01828')

    $linear = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $deepCrimson, $deepCrimson, 135
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend 4
    $blend.Colors    = @($deepCrimson, $brightAccent, $midCrimson, $deepCrimson)
    $blend.Positions = @(0.0, 0.30, 0.65, 1.0)
    $linear.InterpolationColors = $blend
    $g.FillRectangle($linear, $rect)
    $linear.Dispose()

    # Subtle diagonal pattern for texture (extremely low opacity)
    $patternPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(10, 255, 255, 255)), ($Scale * 1.0)
    for ($x = -$sh; $x -lt $sw; $x += ($Scale * 14)) {
        $g.DrawLine($patternPen, $x, 0, ($x + $sh), $sh)
    }
    $patternPen.Dispose()

    # Top-left radial highlight (warm glow)
    $hrX = [int](-$sw * 0.3)
    $hrY = [int](-$sh * 0.5)
    $hrW = [int]($sw * 0.9)
    $hrH = [int]($sh * 1.5)
    $highlightRect = New-Object System.Drawing.Rectangle $hrX, $hrY, $hrW, $hrH
    $hlPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $hlPath.AddEllipse($highlightRect)
    $radial = New-Object System.Drawing.Drawing2D.PathGradientBrush $hlPath
    $radial.CenterColor = [System.Drawing.Color]::FromArgb(70, 255, 220, 200)
    $radial.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 255, 255, 255))
    $g.FillPath($radial, $hlPath)
    $radial.Dispose()
    $hlPath.Dispose()

    # Bottom-right vignette
    $vignetteBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(0, 0, 0, 0)), ([System.Drawing.Color]::FromArgb(80, 0, 0, 0)), 135
    $g.FillRectangle($vignetteBrush, $rect)
    $vignetteBrush.Dispose()

    # GK mark: white rounded square with shadow
    $markX = [int](24 * $Scale)
    $markY = [int](24 * $Scale)
    $msScaled = $MarkSize * $Scale
    $cornerRadius = [int](6 * $Scale)

    # Drop shadow (offset down-right)
    $shadowOffsetX = [int](2 * $Scale)
    $shadowOffsetY = [int](4 * $Scale)
    $shadowPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $arc = $cornerRadius * 2
    $shadowPath.AddArc(($markX + $shadowOffsetX), ($markY + $shadowOffsetY), $arc, $arc, 180, 90)
    $shadowPath.AddArc(($markX + $msScaled - $arc + $shadowOffsetX), ($markY + $shadowOffsetY), $arc, $arc, 270, 90)
    $shadowPath.AddArc(($markX + $msScaled - $arc + $shadowOffsetX), ($markY + $msScaled - $arc + $shadowOffsetY), $arc, $arc, 0, 90)
    $shadowPath.AddArc(($markX + $shadowOffsetX), ($markY + $msScaled - $arc + $shadowOffsetY), $arc, $arc, 90, 90)
    $shadowPath.CloseFigure()
    $shadowBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(60, 0, 0, 0))
    $g.FillPath($shadowBrush, $shadowPath)
    $shadowBrush.Dispose()
    $shadowPath.Dispose()

    # White rounded square
    $markPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $markPath.AddArc($markX, $markY, $arc, $arc, 180, 90)
    $markPath.AddArc(($markX + $msScaled - $arc), $markY, $arc, $arc, 270, 90)
    $markPath.AddArc(($markX + $msScaled - $arc), ($markY + $msScaled - $arc), $arc, $arc, 0, 90)
    $markPath.AddArc($markX, ($markY + $msScaled - $arc), $arc, $arc, 90, 90)
    $markPath.CloseFigure()
    $g.FillPath([System.Drawing.Brushes]::White, $markPath)
    $markPath.Dispose()

    # GK text in the mark
    $markFontSize = [Math]::Round($msScaled * 0.46)
    $markFont = [System.Drawing.Font]::new('Segoe UI', $markFontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $markFmt = New-Object System.Drawing.StringFormat
    $markFmt.Alignment = 'Center'
    $markFmt.LineAlignment = 'Center'
    $markBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#8B1424'))
    $markRect = New-Object System.Drawing.RectangleF $markX, $markY, $msScaled, $msScaled
    $g.DrawString('GK', $markFont, $markBrush, $markRect, $markFmt)
    $markBrush.Dispose()
    $markFont.Dispose()

    if ($WithText) {
        $titleSize = [int](28 * $Scale)
        $verSize = [int](14 * $Scale)
        $titleFont = [System.Drawing.Font]::new('Segoe UI Semibold', $titleSize, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
        $verFont = [System.Drawing.Font]::new('Segoe UI', $verSize, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)

        # Text shadow for legibility on the gradient
        $textShadowBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(120, 0, 0, 0))
        $g.DrawString('GraceKeeper', $titleFont, $textShadowBrush, ($markX + 1 * $Scale), ($markY + $msScaled + 16 * $Scale + 1 * $Scale))
        $g.DrawString('FactoryTalk grace-period helper', $verFont, $textShadowBrush, ($markX + 1 * $Scale), ($markY + $msScaled + 16 * $Scale + 1 * $Scale + 38 * $Scale))
        $textShadowBrush.Dispose()

        $whiteBrush = [System.Drawing.Brushes]::White
        $mutedBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(200, 255, 235, 235))
        $g.DrawString('GraceKeeper', $titleFont, $whiteBrush, $markX, ($markY + $msScaled + 16 * $Scale))
        $g.DrawString('FactoryTalk grace-period helper', $verFont, $mutedBrush, $markX, ($markY + $msScaled + 16 * $Scale + 38 * $Scale))
        $mutedBrush.Dispose()
        $titleFont.Dispose()
        $verFont.Dispose()
    }

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

Render-Branding -W 493 -H 312 -Path "$out\sidebar.bmp" -MarkSize 64 -WithText $true -Scale 2
Render-Branding -W 493 -H 58 -Path "$out\banner.bmp" -MarkSize 36 -WithText $false -Scale 2

Write-Host "`nBranding regenerated. Re-run scripts\build-local.ps1 to package into MSI."
