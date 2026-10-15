# Packs the frames of a pet into one atlas image. The generators draw one PNG per frame and write a manifest that names
# them; this turns that into the form the program reads: atlas.png with all the different frames in a grid of equal cells,
# and a manifest whose frames are the numbers of their cells (counted along the rows from the top left). Frames that are
# the same picture share one cell. The single frame files are deleted afterwards, except icon.png, which the tray reads
# as it is. Dot-source this file and call Invoke-PetAtlasPacking; PetDrawing.ps1 must be dot-sourced as well.
Add-Type -AssemblyName System.Drawing

# Cells in a row of the atlas.
$script:AtlasColumns = 10

function Invoke-PetAtlasPacking([string]$PetDir) {
    $manifestPath = Join-Path $PetDir 'manifest.json'
    $text = [System.IO.File]::ReadAllText($manifestPath)

    # The frame files in the order the manifest names them, and the cell each one goes to.
    $names = [regex]::Matches($text, '"([^"]+\.png)"') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique
    $cellOf = @{}
    $cellByHash = @{}
    $images = New-Object System.Collections.Generic.List[string]
    foreach ($name in $names) {
        $path = Join-Path $PetDir $name
        $hash = (Get-FileHash -Algorithm SHA256 -Path $path).Hash
        if (-not $cellByHash.ContainsKey($hash)) {
            $cellByHash[$hash] = $images.Count
            $images.Add($path)
        }
        $cellOf[$name] = $cellByHash[$hash]
    }

    $first = [System.Drawing.Image]::FromFile($images[0])
    $cellWidth = $first.Width
    $cellHeight = $first.Height
    $first.Dispose()
    $columns = [Math]::Min($script:AtlasColumns, $images.Count)
    $rows = [int][Math]::Ceiling($images.Count / $columns)

    $atlas = New-Object System.Drawing.Bitmap ($columns * $cellWidth), ($rows * $cellHeight), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($atlas)
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.Clear([System.Drawing.Color]::Transparent)
    for ($i = 0; $i -lt $images.Count; $i++) {
        $image = [System.Drawing.Image]::FromFile($images[$i])
        if ($image.Width -ne $cellWidth -or $image.Height -ne $cellHeight) {
            throw "Frame $($images[$i]) is $($image.Width)x$($image.Height), not $cellWidth x $cellHeight like the first one."
        }
        # Drawn into an exact cell: DrawImageUnscaled would size the frame by its own DPI.
        $g.DrawImage($image, ($i % $columns) * $cellWidth, [int][Math]::Floor($i / $columns) * $cellHeight, $cellWidth, $cellHeight)
        $image.Dispose()
    }
    $g.Dispose()
    $atlas.Save((Join-Path $PetDir 'atlas.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    $atlas.Dispose()

    # The manifest: the file names become cell numbers, and the atlas is declared after the frame size.
    $text = [regex]::Replace($text, '"([^"]+\.png)"', { param($m) $cellOf[$m.Groups[1].Value] })
    $atlasLine = "  `"atlas`": { `"image`": `"atlas.png`", `"cell`": [$cellWidth, $cellHeight], `"columns`": $columns },"
    $text = [regex]::Replace($text, '("frameSize":[^\r\n]*\r?\n)', "`$1$atlasLine`n", 1)
    Write-PetManifest $manifestPath $text

    Get-ChildItem $PetDir -Filter *.png | Where-Object { $_.Name -notin 'atlas.png', 'icon.png' } | Remove-Item
    Write-Host "Packed $($names.Count) frames into $($images.Count) cells of $cellWidth x $cellHeight ($columns x $rows) in $PetDir"
}
