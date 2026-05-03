Add-Type -AssemblyName System.Drawing

function Save-Asset {
    param([int]$Width, [int]$Height, [string]$Path)
    $bmp = New-Object System.Drawing.Bitmap($Width, $Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::FromArgb(30, 30, 30))
    $fontSize = [Math]::Min($Width, $Height) * 0.55
    $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = [System.Drawing.RectangleF]::new(0, 0, $Width, $Height)
    $g.DrawString([char]0x00E3, $font, $brush, $rect, $sf)
    $folder = Split-Path $Path -Parent
    if ($folder) { New-Item -ItemType Directory -Force -Path $folder | Out-Null }
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose(); $font.Dispose(); $brush.Dispose(); $sf.Dispose()
    Write-Host "Generated $Path ($Width x $Height)"
}

Save-Asset 50  50  "Assets\StoreLogo.png"
Save-Asset 44  44  "Assets\Square44x44Logo.png"
Save-Asset 150 150 "Assets\Square150x150Logo.png"
Save-Asset 620 300 "Assets\SplashScreen.png"
