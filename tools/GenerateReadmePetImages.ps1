# Regenerates docs/pets/<name>.png (the pet pictures shown in the README) from every pet under pets/.
# Each picture is the first Idle frame cut out of the pet's atlas, scaled to $Size px on its longer edge.
# Nothing pet-specific lives here: the frame comes from the manifest, so a new pet just needs its folder.
param([int]$Size = 128)

Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'docs/pets'
New-Item -ItemType Directory -Force $out | Out-Null

foreach ($dir in Get-ChildItem (Join-Path $root 'pets') -Directory) {
    $manifest = Get-Content (Join-Path $dir.FullName 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $cellW = [int]$manifest.atlas.cell[0]
    $cellH = [int]$manifest.atlas.cell[1]
    $atlas = [System.Drawing.Image]::FromFile((Join-Path $dir.FullName $manifest.atlas.image))
    try {
        $columns = if ($manifest.atlas.columns) { [int]$manifest.atlas.columns } else { [Math]::Max(1, [int]($atlas.Width / $cellW)) }
        $index = [int]$manifest.states.Idle.frames[0]
        $src = New-Object System.Drawing.Rectangle(($index % $columns) * $cellW), ([Math]::Floor($index / $columns) * $cellH), $cellW, $cellH

        $scale = $Size / [Math]::Max($cellW, $cellH)
        $w = [int][Math]::Round($cellW * $scale)
        $h = [int][Math]::Round($cellH * $scale)
        $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.DrawImage($atlas, (New-Object System.Drawing.Rectangle 0, 0, $w, $h), $src, [System.Drawing.GraphicsUnit]::Pixel)
        $g.Dispose()
        $bmp.Save((Join-Path $out "$($dir.Name).png"), [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        Write-Host "docs/pets/$($dir.Name).png  (${w}x${h}, Idle frame $index)"
    }
    finally { $atlas.Dispose() }
}
