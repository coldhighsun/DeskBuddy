# Generates the application icon (tray, exe, windows, installer): the robot's head on a rounded navy tile, drawn on a
# 16x16 art grid like the robot skin and scaled up without smoothing, so it stays crisp at every size.
# Output: src/DeskBuddy.App/Assets/DeskBuddy.ico (PNG-compressed, 16-256 px) and DeskBuddy.png (256 px preview).
# Usage: powershell -NoProfile -File tools/GenerateAppIcon.ps1
Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot '..\src\DeskBuddy.App\Assets'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outDir = (Resolve-Path $outDir).Path

# Palette, shared with GenerateRobotSkin.ps1 where the colors have the same meaning.
$colors = @{
    'B' = @(54, 72, 112)     # tile background
    'K' = @(18, 24, 40)      # head outline
    'S' = @(110, 150, 200)   # Steel
    'L' = @(170, 200, 240)   # SteelLight (top highlight)
    'D' = @(70, 100, 150)    # SteelDark (ears, antenna stem)
    'N' = @(14, 22, 38)      # Screen
    'E' = @(90, 255, 230)    # Cyan (eyes)
    'M' = @(40, 120, 120)    # CyanDim (mouth)
    'O' = @(255, 170, 40)    # Orange (antenna lamp)
}

# '.' is transparent: only the four corners, which rounds the tile.
$grid = @(
    '.BBBBBBBBBBBBBB.',
    'BBBBBBBOOBBBBBBB',
    'BBBBBBBOOBBBBBBB',
    'BBBBBBBDDBBBBBBB',
    'BBKKKKKKKKKKKKBB',
    'BBKLLLLLLLLLLKBB',
    'BBKSNNNNNNNNSKBB',
    'BDKSNEENNEENSKDB',
    'BDKSNEENNEENSKDB',
    'BDKSNEENNEENSKDB',
    'BDKSNNNNNNNNSKDB',
    'BBKSNNMMMMNNSKBB',
    'BBKSSSSSSSSSSKBB',
    'BBKKKKKKKKKKKKBB',
    'BBBBBBBBBBBBBBBB',
    '.BBBBBBBBBBBBBB.'
)

# Renders the grid at a size that is a multiple of 16 and returns the PNG bytes.
function New-IconPng([int]$Size) {
    $cell = $Size / 16
    $bmp = New-Object System.Drawing.Bitmap $Size, $Size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'None'
    $g.PixelOffsetMode = 'Half'
    $g.Clear([System.Drawing.Color]::Transparent)
    for ($y = 0; $y -lt 16; $y++) {
        for ($x = 0; $x -lt 16; $x++) {
            $key = [string]$grid[$y][$x]
            if ($key -eq '.') { continue }
            $c = $colors[$key]
            $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, $c[0], $c[1], $c[2]))
            $g.FillRectangle($brush, ($x * $cell), ($y * $cell), $cell, $cell)
            $brush.Dispose()
        }
    }
    $g.Dispose()
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
