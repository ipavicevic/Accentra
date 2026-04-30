Add-Type -AssemblyName System.Drawing

function New-IconBitmap($size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    # Circle background — deep blue
    $bg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 30, 90, 200))
    $g.FillEllipse($bg, 0, 0, $size - 1, $size - 1)
    $bg.Dispose()

    # Draw 'á' centred
    $fontSize = [float]($size * 0.80)
    $font = New-Object System.Drawing.Font("Georgia", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $fg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    # Shift the rect up so the base 'a' (not the whole 'á') sits at the optical centre.
    $offsetY = [float]($fontSize * 0.12)
    $rect = New-Object System.Drawing.RectangleF(0, -$offsetY, $size, $size)
    $g.DrawString([string][char]0x00E1, $font, $fg, $rect, $sf)   # á = U+00E1

    $font.Dispose(); $fg.Dispose(); $sf.Dispose(); $g.Dispose()
    return $bmp
}

function Save-Ico($bitmaps, $path) {
    $pngs = $bitmaps | ForEach-Object {
        $ms = New-Object System.IO.MemoryStream
        $_.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        ,$ms.ToArray()
    }

    $fs = [System.IO.File]::OpenWrite($path)
    $w = New-Object System.IO.BinaryWriter($fs)

    # ICO header
    $w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$bitmaps.Count)

    $offset = 6 + 16 * $bitmaps.Count
    for ($i = 0; $i -lt $bitmaps.Count; $i++) {
        $dim = $bitmaps[$i].Width
        $w.Write([byte]$(if ($dim -ge 256) { 0 } else { $dim }))
        $w.Write([byte]$(if ($dim -ge 256) { 0 } else { $dim }))
        $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([int16]1); $w.Write([int16]32)
        $w.Write([int32]$pngs[$i].Length)
        $w.Write([int32]$offset)
        $offset += $pngs[$i].Length
    }
    $pngs | ForEach-Object { $w.Write($_) }
    $w.Dispose(); $fs.Dispose()
}

$sizes   = @(16, 32, 48, 256)
$bitmaps = $sizes | ForEach-Object { New-IconBitmap $_ }
$out     = Join-Path $PSScriptRoot "icon.ico"
Save-Ico $bitmaps $out
$bitmaps | ForEach-Object { $_.Dispose() }
Write-Host "Written: $out"
