# Drawing helpers for pets drawn as smooth, anti-aliased vector art instead of pixel art (see GenerateFoxPet.ps1).
# Dot-source this file together with PetDrawing.ps1 (which supplies Write-PetManifest for the atlas packer); the caller
# defines $pal (a hashtable of colors made with New-HdColor) and $textFont before it draws anything.
#
# Pets are drawn on a 64x64 unit grid (the pet's frameSize) at $HdScale device pixels per unit, so the frames come out at
# 64 * $HdScale pixels square. The manifest keeps frameSize at 64: only the atlas cell grows, which makes the pet sharper
# on screen without making it any bigger.
Add-Type -AssemblyName System.Drawing

# Device pixels per drawing unit: the 64x64 frame becomes a 256x256 cell.
$HdScale = 4
$HdEdge = 64

function New-HdColor([int]$R, [int]$G, [int]$B, [int]$A = 255) {
    [System.Drawing.Color]::FromArgb($A, $R, $G, $B)
}

function Get-HdColor([string]$Key) {
    if (-not $pal.ContainsKey($Key)) { throw "The palette has no color named '$Key'; add it to `$pal." }
    $pal[$Key]
}

# A list of points from x, y pairs.
function New-HdPoints([double[]]$Xy) {
    $count = [int]($Xy.Count / 2)
    $points = New-Object 'System.Drawing.PointF[]' $count
    for ($i = 0; $i -lt $count; $i++) {
        $points[$i] = New-Object System.Drawing.PointF ([single]$Xy[2 * $i]), ([single]$Xy[2 * $i + 1])
    }
    return , $points
}

# Samples a cubic Bezier curve into x, y pairs.
function Get-HdBezier([double]$X0, [double]$Y0, [double]$X1, [double]$Y1, [double]$X2, [double]$Y2, [double]$X3, [double]$Y3, [int]$Steps = 24) {
    $result = New-Object 'System.Collections.Generic.List[double]'
    for ($i = 0; $i -le $Steps; $i++) {
        $t = $i / $Steps
        $u = 1 - $t
        $result.Add($u * $u * $u * $X0 + 3 * $u * $u * $t * $X1 + 3 * $u * $t * $t * $X2 + $t * $t * $t * $X3)
        $result.Add($u * $u * $u * $Y0 + 3 * $u * $u * $t * $Y1 + 3 * $u * $t * $t * $Y2 + $t * $t * $t * $Y3)
    }
    return , $result.ToArray()
}

# A thick line with round ends and joints through the given x, y pairs.
function Add-HdStroke([double[]]$Xy, [string]$Color, [double]$Width) {
    $pen = New-Object System.Drawing.Pen (Get-HdColor $Color), ([single]$Width)
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    try { $script:g.DrawLines($pen, (New-HdPoints $Xy)) } finally { $pen.Dispose() }
}

# A limb: a stroke with an outline around it.
function Add-HdLimb([double[]]$Xy, [string]$Color, [double]$Width, [string]$Edge = 'Outline') {
    Add-HdStroke $Xy $Edge ($Width + 1.7)
    Add-HdStroke $Xy $Color $Width
}

# A filled ellipse with an optional outline (pass an empty Edge for none).
function Add-HdOval([string]$Color, [double]$Cx, [double]$Cy, [double]$Rx, [double]$Ry, [string]$Edge = 'Outline', [double]$Width = 1.1) {
    $brush = New-Object System.Drawing.SolidBrush (Get-HdColor $Color)
    try { $script:g.FillEllipse($brush, [single]($Cx - $Rx), [single]($Cy - $Ry), [single](2 * $Rx), [single](2 * $Ry)) } finally { $brush.Dispose() }
    if ($Edge) {
        $pen = New-Object System.Drawing.Pen (Get-HdColor $Edge), ([single]$Width)
        try { $script:g.DrawEllipse($pen, [single]($Cx - $Rx), [single]($Cy - $Ry), [single](2 * $Rx), [single](2 * $Ry)) } finally { $pen.Dispose() }
    }
}

# A filled polygon (or, with Smooth, a rounded closed curve through the points) with an optional outline.
function Add-HdPoly([string]$Color, [double[]]$Xy, [string]$Edge = 'Outline', [double]$Width = 1.1, [switch]$Smooth) {
    $points = New-HdPoints $Xy
    $brush = New-Object System.Drawing.SolidBrush (Get-HdColor $Color)
    try {
        if ($Smooth) { $script:g.FillClosedCurve($brush, $points, 'Alternate', [single]0.4) } else { $script:g.FillPolygon($brush, $points) }
    } finally { $brush.Dispose() }
    if ($Edge) {
        $pen = New-Object System.Drawing.Pen (Get-HdColor $Edge), ([single]$Width)
        $pen.LineJoin = 'Round'
        try {
            if ($Smooth) { $script:g.DrawClosedCurve($pen, $points, [single]0.4, 'Alternate') } else { $script:g.DrawPolygon($pen, $points) }
        } finally { $pen.Dispose() }
    }
}

# A heart centered on the given point, S units across the half width.
function Add-HdHeart([string]$Color, [double]$Cx, [double]$Cy, [double]$S) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddBezier([single]$Cx, [single]($Cy + $S * 0.9), [single]($Cx - $S * 1.4), [single]($Cy + $S * 0.1), [single]($Cx - $S * 0.8), [single]($Cy - $S * 0.95), [single]$Cx, [single]($Cy - $S * 0.3))
    $path.AddBezier([single]$Cx, [single]($Cy - $S * 0.3), [single]($Cx + $S * 0.8), [single]($Cy - $S * 0.95), [single]($Cx + $S * 1.4), [single]($Cy + $S * 0.1), [single]$Cx, [single]($Cy + $S * 0.9))
    $brush = New-Object System.Drawing.SolidBrush (Get-HdColor $Color)
    try { $script:g.FillPath($brush, $path) } finally { $brush.Dispose(); $path.Dispose() }
}

function Add-HdText([string]$Text, [string]$Color, [double]$X, [double]$Y) {
    $brush = New-Object System.Drawing.SolidBrush (Get-HdColor $Color)
    try { $script:g.DrawString($Text, $textFont, $brush, [single]$X, [single]$Y) } finally { $brush.Dispose() }
}

# Starts a frame: a transparent canvas of 64 units a side that the Add-Hd functions draw on through $script:g. Dy lifts
# everything (negative is up); Tilt turns it clockwise by that many degrees about the middle of the frame. Returns the bitmap.
function New-HdCanvas([double]$Dy = 0, [double]$Tilt = 0) {
    if ($null -eq $pal -or $null -eq $textFont) { throw 'Define $pal and $textFont before drawing a frame.' }
    $size = $HdEdge * $HdScale
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $bmp.SetResolution(96, 96)
    $script:g = [System.Drawing.Graphics]::FromImage($bmp)
    $script:g.SmoothingMode = 'AntiAlias'
    $script:g.PixelOffsetMode = 'HighQuality'
    $script:g.TextRenderingHint = 'AntiAlias'
    $script:g.Clear([System.Drawing.Color]::Transparent)
    $script:g.ScaleTransform($HdScale, $HdScale)
    $mid = $HdEdge / 2
    $script:g.TranslateTransform([single]$mid, [single]($mid + $Dy))
    $script:g.RotateTransform([single]$Tilt)
    $script:g.TranslateTransform([single](-$mid), [single](-$mid))
    return $bmp
}

# Finishes a frame: Flip turns it upside down, Mirror left to right, then it is saved as a PNG.
function Save-HdCanvas($Bitmap, [string]$Path, [switch]$Flip, [switch]$Mirror) {
    $script:g.Dispose()
    if ($Flip) { $Bitmap.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipY) }
    if ($Mirror) { $Bitmap.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipX) }
    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $Bitmap.Dispose()
}

# Saves the pet's tray icon, icon.png in its folder: the part of a frame that shows its head, given in drawing units as the
# top-left corner and the size (kept square, so it fills the icon).
function Save-HdIcon([string]$Frame, [string]$Path, [double[]]$Box) {
    $source = New-Object System.Drawing.Bitmap $Frame
    try {
        $area = New-Object System.Drawing.Rectangle ([int]($Box[0] * $HdScale)), ([int]($Box[1] * $HdScale)), ([int]($Box[2] * $HdScale)), ([int]($Box[3] * $HdScale))
        $icon = $source.Clone($area, $source.PixelFormat)
        try { $icon.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png) } finally { $icon.Dispose() }
    } finally {
        $source.Dispose()
    }
}
