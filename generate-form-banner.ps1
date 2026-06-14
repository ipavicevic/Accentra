Add-Type -AssemblyName System.Drawing

$Blue        = [System.Drawing.Color]::FromArgb(30, 90, 200)    # #1E5AC8
$BlueDark    = [System.Drawing.Color]::FromArgb(26, 79, 176)    # #1A4FB0
$White       = [System.Drawing.Color]::White
$LightBlue   = [System.Drawing.Color]::FromArgb(168, 196, 240)  # #A8C4F0
$Glyph       = [char]0x0101  # ā

$W = 1600; $H = 400

$bmp = New-Object System.Drawing.Bitmap($W, $H)
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

# Background
$g.Clear($Blue)

# Icon background — rounded square
function Get-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($x,          $y,          $r*2, $r*2, 180, 90)
    $path.AddArc($x+$w-$r*2, $y,          $r*2, $r*2, 270, 90)
    $path.AddArc($x+$w-$r*2, $y+$h-$r*2, $r*2, $r*2,   0, 90)
    $path.AddArc($x,          $y+$h-$r*2, $r*2, $r*2,  90, 90)
    $path.CloseFigure()
    return $path
}

$iconX = 40; $iconY = 40; $iconSize = 320; $iconRadius = 70
$iconPath = Get-RoundedRectPath $iconX $iconY $iconSize $iconSize $iconRadius
$iconFill = New-Object System.Drawing.SolidBrush($BlueDark)
$g.FillPath($iconFill, $iconPath)
$iconFill.Dispose(); $iconPath.Dispose()

# Glyph inside icon
$glyphFont  = New-Object System.Drawing.Font("Segoe UI", 220, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$glyphBrush = New-Object System.Drawing.SolidBrush($White)
$glyphSF    = New-Object System.Drawing.StringFormat
$glyphSF.Alignment     = [System.Drawing.StringAlignment]::Center
$glyphSF.LineAlignment = [System.Drawing.StringAlignment]::Center
$glyphRect  = [System.Drawing.RectangleF]::new($iconX, $iconY - 30, $iconSize, $iconSize)
$g.DrawString($Glyph, $glyphFont, $glyphBrush, $glyphRect, $glyphSF)
$glyphFont.Dispose(); $glyphBrush.Dispose(); $glyphSF.Dispose()

# "Accentra" title
$titleFont  = New-Object System.Drawing.Font("Segoe UI", 130, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$titleBrush = New-Object System.Drawing.SolidBrush($White)
$g.DrawString("Accentra", $titleFont, $titleBrush, [System.Drawing.PointF]::new(420, 80))
$titleFont.Dispose(); $titleBrush.Dispose()

# Subtitle
$subFont  = New-Object System.Drawing.Font("Segoe UI", 42, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$subBrush = New-Object System.Drawing.SolidBrush($LightBlue)
$g.DrawString("Accented character input for Windows", $subFont, $subBrush, [System.Drawing.PointF]::new(424, 270))
$subFont.Dispose(); $subBrush.Dispose()

$g.Dispose()

$out = Join-Path $PSScriptRoot "Assets\form-banner.png"
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Written: $out"
