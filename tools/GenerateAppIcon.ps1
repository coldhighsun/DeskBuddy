# Generates the application icon (tray, exe, windows, installer): the robot pet's vector head icon (pets/robot/icon.png)
# on a rounded navy tile, resampled smoothly for every size.
# Output: src/DeskBuddy.App/Assets/DeskBuddy.ico (PNG-compressed, 16-256 px) and DeskBuddy.png (256 px preview).
# Usage: powershell -NoProfile -File tools/GenerateAppIcon.ps1
Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot '..\src\DeskBuddy.App\Assets'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outDir = (Resolve-Path $outDir).Path
$head = New-Object System.Drawing.Bitmap (Resolve-Path (Join-Path $PSScriptRoot '../pets/robot/icon.png')).Path

# Rows of icon.png that belong to the head (the neck and the shoulders start below it).
$HeadRows = 127

$tileColor = [System.Drawing.Color]::FromArgb(255, 54, 72, 112)

# Renders the tile at the given size and returns the PNG bytes.
function New-IconPng([int]$Size) {
    $bmp = New-Object System.Drawing.Bitmap $Size, $Size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $radius = $Size * 0.22
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($Size - $d, 0, $d, $d, 270, 90)
    $path.AddArc($Size - $d, $Size - $d, $d, $d, 0, 90)
    $path.AddArc(0, $Size - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $brush = New-Object System.Drawing.SolidBrush $tileColor
    $g.FillPath($brush, $path)
    $inset = $Size * 0.06
    # Draw only the head: the rows below HeadRows hold the neck and the shoulders. Keep the aspect ratio, centered.
    $box = $Size - 2 * $inset
    $scale = [Math]::Min($box / $head.Width, $box / $HeadRows)
    $w = $head.Width * $scale
    $h = $HeadRows * $scale
    $dest = New-Object System.Drawing.RectangleF ([single](($Size - $w) / 2)), ([single](($Size - $h) / 2)), ([single]$w), ([single]$h)
    $g.DrawImage($head, $dest, (New-Object System.Drawing.RectangleF 0, 0, $head.Width, $HeadRows), [System.Drawing.GraphicsUnit]::Pixel)
    $brush.Dispose(); $path.Dispose(); $g.Dispose()
    $stream = New-Object System.IO.MemoryStream
    $bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $stream.ToArray()
}

$sizes = 16, 32, 48, 64, 128, 256
$images = foreach ($size in $sizes) { , (New-IconPng $size) }

# ICO = ICONDIR (6 bytes) + one 16-byte ICONDIRENTRY per image + the PNG data. A width or height of 256 is stored as 0.
$ico = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $ico
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $w.Write($image) }
$w.Flush()

[System.IO.File]::WriteAllBytes((Join-Path $outDir 'DeskBuddy.ico'), $ico.ToArray())
[System.IO.File]::WriteAllBytes((Join-Path $outDir 'DeskBuddy.png'), $images[$sizes.Count - 1])
Write-Host "Wrote $outDir\DeskBuddy.ico ($($sizes -join ', ') px)"
