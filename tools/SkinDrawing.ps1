# Drawing helpers shared by the skin generators (GenerateRobotSkin.ps1, GenerateCatSkin.ps1). Dot-source this file; the
# caller defines $pal (a hashtable of brushes made with New-Brush) and $textFont before it draws anything.
Add-Type -AssemblyName System.Drawing

# One art pixel is 4x4 device pixels: the 128x128 frame is a 32x32 art grid.
$S = 4

function New-Brush([int]$R, [int]$G, [int]$B) {
    New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, $R, $G, $B))
}

# Fills a rectangle given in art pixels; $script:dy lifts or lowers everything drawn in the current frame, and
# $script:offsetX shifts it sideways (used by side views).
$script:offsetX = 0
$script:dy = 0
function Px([string]$Color, [int]$X, [int]$Y, [int]$W = 1, [int]$H = 1) {
    if (-not $pal.ContainsKey($Color)) { throw "The palette has no color named '$Color'; add it to `$pal." }
    $script:g.FillRectangle($pal[$Color], (($X + $script:offsetX) * $S), (($Y + $script:dy) * $S), ($W * $S), ($H * $S))
}

function Add-Text([string]$Text, [string]$Color, [int]$X, [int]$Y) {
    $script:g.DrawString($Text, $textFont, $pal[$Color], [single]$X, [single]$Y)
}

# A heart of 5x4 art pixels at the given top-left corner.
function Add-Heart([int]$X, [int]$Y) {
    $rows = @('.X.X.', 'XXXXX', '.XXX.', '..X..')
    for ($r = 0; $r -lt 4; $r++) {
        for ($c = 0; $c -lt 5; $c++) {
            if ($rows[$r][$c] -eq 'X') { Px Pink ($X + $c) ($Y + $r) }
        }
    }
}

# A limb drawn as a line between two points: an outline one art pixel wider than the core on every side. Core is the
# thickness of the core in art pixels (1 gives a limb three pixels thick, 2 one four pixels thick); a generator sets
# $script:LimbCore to change the default for all its limbs.
$script:LimbCore = 1
function Add-Limb([int]$X0, [int]$Y0, [int]$X1, [int]$Y1, [string]$Edge, [string]$Fill, [int]$Core = $script:LimbCore) {
    $steps = [Math]::Max([Math]::Max([Math]::Abs($X1 - $X0), [Math]::Abs($Y1 - $Y0)), 1)
    foreach ($pass in 0, 1) {
        foreach ($i in 0..$steps) {
            $x = [int][Math]::Round($X0 + ($X1 - $X0) * $i / $steps)
            $y = [int][Math]::Round($Y0 + ($Y1 - $Y0) * $i / $steps)
            if ($pass -eq 0) { Px $Edge ($x - 1) ($y - 1) ($Core + 2) ($Core + 2) } else { Px $Fill $x $y $Core $Core }
        }
    }
}

# Starts a frame: a transparent 128x128 canvas that Px and Add-Text draw on through $script:g. Returns the bitmap.
function New-Canvas {
    if ($null -eq $pal -or $null -eq $textFont) { throw 'Define $pal (brushes made with New-Brush) and $textFont before drawing a frame.' }
    $bmp = New-Object System.Drawing.Bitmap 128, 128
    $script:g = [System.Drawing.Graphics]::FromImage($bmp)
    $script:g.SmoothingMode = 'None'
    $script:g.PixelOffsetMode = 'Half'
    $script:g.TextRenderingHint = 'SingleBitPerPixelGridFit'
    $script:g.Clear([System.Drawing.Color]::Transparent)
    return $bmp
}

# Finishes a frame: Flip turns the picture upside down, Mirror turns it left to right, then it is saved as a PNG.
function Save-Canvas($Bitmap, [string]$Path, [switch]$Flip, [switch]$Mirror) {
    if ($Flip) { $Bitmap.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipY) }
    if ($Mirror) { $Bitmap.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipX) }
    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $script:g.Dispose()
    $Bitmap.Dispose()
}

# Antivirus / indexers often hold freshly written files for a moment, so retry the manifest write whatever the failure.
function Write-SkinManifest([string]$Path, [string]$Text) {
    foreach ($attempt in 1..10) {
        try {
            Set-Content -Encoding UTF8 -Path $Path -Value $Text -ErrorAction Stop
            break
        }
        catch {
            if ($attempt -eq 10) { throw }
            Start-Sleep -Milliseconds 500
        }
    }
}
