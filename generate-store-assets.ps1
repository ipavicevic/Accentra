Add-Type -AssemblyName System.Drawing

$Blue  = [System.Drawing.Color]::FromArgb(30, 90, 200)   # #1E5AC8
$White = [System.Drawing.Color]::White
$Glyph = [char]0x0101   # ā

function Get-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($x,           $y,           $r*2, $r*2, 180, 90)
    $path.AddArc($x+$w-$r*2,  $y,           $r*2, $r*2, 270, 90)
    $path.AddArc($x+$w-$r*2,  $y+$h-$r*2,  $r*2, $r*2,   0, 90)
    $path.AddArc($x,           $y+$h-$r*2,  $r*2, $r*2,  90, 90)
    $path.CloseFigure()
    return $path
}

function New-Bitmap([int]$Width, [int]$Height, [float]$FontScale = 0.50) {
    $bmp = New-Object System.Drawing.Bitmap($Width, $Height)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint  = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    $inset  = [float]1
    $radius = [float](($Width - $inset * 2) * 0.22)
    $path   = Get-RoundedRectPath $inset $inset ($Width - $inset*2) ($Height - $inset*2) $radius
    $fill   = New-Object System.Drawing.SolidBrush($Blue)
    $g.FillPath($fill, $path)
    $fill.Dispose(); $path.Dispose()

    $fontSize = [Math]::Min($Width, $Height) * $FontScale
    $font     = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $brush    = New-Object System.Drawing.SolidBrush($White)
    $sf       = New-Object System.Drawing.StringFormat
    $sf.Alignment     = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = [System.Drawing.RectangleF]::new(0, 0, $Width, $Height)
    $g.DrawString($Glyph, $font, $brush, $rect, $sf)

    $g.Dispose(); $font.Dispose(); $brush.Dispose(); $sf.Dispose()
    return $bmp
}

function Save-Asset([int]$Width, [int]$Height, [string]$Path) {
    $bmp    = New-Bitmap $Width $Height
    $folder = Split-Path $Path -Parent
    if ($folder) { New-Item -ItemType Directory -Force -Path $folder | Out-Null }
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Generated $Path ($Width x $Height)"
}

function Save-Ico([string]$Path, [int[]]$Sizes, [float]$FontScale = 0.50) {
    $pngData = @{}
    foreach ($s in $Sizes) {
        $bmp = New-Bitmap $s $s $FontScale
        $ms  = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngData[$s] = $ms.ToArray()
        $bmp.Dispose(); $ms.Dispose()
    }

    $folder = Split-Path $Path -Parent
    if ($folder) { New-Item -ItemType Directory -Force -Path $folder | Out-Null }

    $stream = [System.IO.File]::Create($Path)
    $w = New-Object System.IO.BinaryWriter($stream)

    # ICONDIR header
    $w.Write([int16]0)              # reserved
    $w.Write([int16]1)              # type: icon
    $w.Write([int16]$Sizes.Count)   # number of images

    # ICONDIRENTRY for each size (offset = 6 + count*16)
    $offset = 6 + $Sizes.Count * 16
    foreach ($s in $Sizes) {
        $dim = if ($s -ge 256) { 0 } else { $s }
        $w.Write([byte]$dim)                    # width  (0 = 256)
        $w.Write([byte]$dim)                    # height
        $w.Write([byte]0)                       # color count
        $w.Write([byte]0)                       # reserved
        $w.Write([int16]1)                      # planes
        $w.Write([int16]32)                     # bit depth
        $w.Write([int32]$pngData[$s].Length)    # image data size
        $w.Write([int32]$offset)                # offset to image data
        $offset += $pngData[$s].Length
    }

    foreach ($s in $Sizes) { $w.Write($pngData[$s]) }
    $w.Close()
    Write-Host "Generated $Path (sizes: $($Sizes -join ', '))"
}

# Store assets
Save-Asset 50  50  "Assets\StoreLogo.png"
Save-Asset 44  44  "Assets\Square44x44Logo.png"
Save-Asset 150 150 "Assets\Square150x150Logo.png"
Save-Asset 620 300 "Assets\SplashScreen.png"

# Tray / app icon
Save-Ico "icon.ico" @(16, 32, 48, 256) -FontScale 0.70
