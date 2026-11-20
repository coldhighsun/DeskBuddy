# Drawing helpers shared by every pet generator: pets are drawn as smooth, anti-aliased vector art (see GenerateFoxPet.ps1 and
# PetVectorAnimations.ps1). Dot-source this file first; the caller then defines $pal (a hashtable of colors made with
# New-HdColor) before it draws anything, and may replace $textFont.
#
# Pets are drawn on a 64x64 unit grid (the pet's frameSize) at $HdScale device pixels per unit, so the frames come out at
# 64 * $HdScale pixels square. The manifest keeps frameSize at 64: only the atlas cell grows, which makes the pet sharper
# on screen without making it any bigger.
Add-Type -AssemblyName System.Drawing

# Device pixels per drawing unit: the 64x64 frame becomes a 256x256 cell.
$HdScale = 4
$HdEdge = 64

# The font of the small texts (z, !, braces, ...), in drawing units.
$textFont = New-Object System.Drawing.Font ('Arial', [single]9, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)

function New-HdColor([int]$R, [int]$G, [int]$B, [int]$A = 255) {
    [System.Drawing.Color]::FromArgb($A, $R, $G, $B)
}

function Get-HdColor([string]$Key) {
    if (-not $pal.ContainsKey($Key)) { throw "The palette has no color named '$Key'; add it to `$pal." }
    $pal[$Key]
}

# Adds the colors that props, hearts and texts need to the palette of a pet, unless the pet defines them itself.
function Set-HdDefaultColors {
    $defaults = @{
        Black      = (New-HdColor 44 28 24)
        White      = (New-HdColor 255 255 255)
        Shine      = (New-HdColor 255 255 255)
        Pink       = (New-HdColor 255 140 150)
        Heart      = (New-HdColor 255 96 128)
        Red        = (New-HdColor 232 60 60)
        Blue       = (New-HdColor 110 178 255)
        Yellow     = (New-HdColor 255 210 70)
        Green      = (New-HdColor 90 190 90)
        Sleepy     = (New-HdColor 120 150 215)
        ScreenGray = (New-HdColor 120 130 150)
        ScreenDark = (New-HdColor 60 68 90)
        Paper      = (New-HdColor 250 248 240)
        PaperLine  = (New-HdColor 180 184 200)
        PropEdge   = (New-HdColor 38 42 60)
    }
    foreach ($key in $defaults.Keys) {
        if (-not $pal.ContainsKey($key)) { $pal[$key] = $defaults[$key] }
    }
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

# A filled ellipse with an optional outline (pass an empty Edge for none), turned clockwise by Angle degrees about its center.
function Add-HdOval([string]$Color, [double]$Cx, [double]$Cy, [double]$Rx, [double]$Ry, [string]$Edge = 'Outline', [double]$Width = 1.1, [double]$Angle = 0) {
    $state = $script:g.Save()
    try {
        if ($Angle -ne 0) {
            $script:g.TranslateTransform([single]$Cx, [single]$Cy)
            $script:g.RotateTransform([single]$Angle)
            $script:g.TranslateTransform([single](-$Cx), [single](-$Cy))
        }
        $brush = New-Object System.Drawing.SolidBrush (Get-HdColor $Color)
        try { $script:g.FillEllipse($brush, [single]($Cx - $Rx), [single]($Cy - $Ry), [single](2 * $Rx), [single](2 * $Ry)) } finally { $brush.Dispose() }
        if ($Edge) {
            $pen = New-Object System.Drawing.Pen (Get-HdColor $Edge), ([single]$Width)
            try { $script:g.DrawEllipse($pen, [single]($Cx - $Rx), [single]($Cy - $Ry), [single](2 * $Rx), [single](2 * $Ry)) } finally { $pen.Dispose() }
        }
    } finally {
        $script:g.Restore($state)
    }
}

# The outline of an ellipse only, without a fill.
function Add-HdRing([string]$Edge, [double]$Cx, [double]$Cy, [double]$Rx, [double]$Ry, [double]$Width = 1.1) {
    $pen = New-Object System.Drawing.Pen (Get-HdColor $Edge), ([single]$Width)
    try { $script:g.DrawEllipse($pen, [single]($Cx - $Rx), [single]($Cy - $Ry), [single](2 * $Rx), [single](2 * $Ry)) } finally { $pen.Dispose() }
}

# A filled polygon (or, with Smooth, a rounded closed curve through the points; Tension 0 is a polygon, 0.4 gently
# rounded, 1 very round) with an optional outline.
function Add-HdPoly([string]$Color, [double[]]$Xy, [string]$Edge = 'Outline', [double]$Width = 1.1, [switch]$Smooth, [double]$Tension = 0.4) {
    $points = New-HdPoints $Xy
    $brush = New-Object System.Drawing.SolidBrush (Get-HdColor $Color)
    try {
        if ($Smooth) { $script:g.FillClosedCurve($brush, $points, 'Alternate', [single]$Tension) } else { $script:g.FillPolygon($brush, $points) }
    } finally { $brush.Dispose() }
    if ($Edge) {
        $pen = New-Object System.Drawing.Pen (Get-HdColor $Edge), ([single]$Width)
        $pen.LineJoin = 'Round'
        try {
            if ($Smooth) { $script:g.DrawClosedCurve($pen, $points, [single]$Tension, 'Alternate') } else { $script:g.DrawPolygon($pen, $points) }
        } finally { $pen.Dispose() }
    }
}

# A closed shape made of cubic Bezier segments: Xy is the start point (x, y) followed by any number of segments of three
# points each (two control points and the end point); the shape is closed with a straight line back to the start.
function Add-HdShape([string]$Color, [double[]]$Xy, [string]$Edge = 'Outline', [double]$Width = 1.1) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $brush = New-Object System.Drawing.SolidBrush (Get-HdColor $Color)
    try {
        $x = $Xy[0]; $y = $Xy[1]
        for ($i = 2; $i + 5 -lt $Xy.Count; $i += 6) {
            $path.AddBezier([single]$x, [single]$y, [single]$Xy[$i], [single]$Xy[$i + 1], [single]$Xy[$i + 2], [single]$Xy[$i + 3], [single]$Xy[$i + 4], [single]$Xy[$i + 5])
            $x = $Xy[$i + 4]; $y = $Xy[$i + 5]
        }
        $path.CloseFigure()
        $script:g.FillPath($brush, $path)
        if ($Edge) {
            $pen = New-Object System.Drawing.Pen (Get-HdColor $Edge), ([single]$Width)
            $pen.LineJoin = 'Round'
            try { $script:g.DrawPath($pen, $path) } finally { $pen.Dispose() }
        }
    } finally { $brush.Dispose(); $path.Dispose() }
}

# A rectangle with rounded corners (radius R) and an optional outline.
function Add-HdRoundRect([string]$Color, [double]$X, [double]$Y, [double]$W, [double]$H, [double]$R, [string]$Edge = 'Outline', [double]$Width = 1.1) {
    $d = [Math]::Min(2 * $R, [Math]::Min($W, $H))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc([single]$X, [single]$Y, [single]$d, [single]$d, 180, 90)
    $path.AddArc([single]($X + $W - $d), [single]$Y, [single]$d, [single]$d, 270, 90)
    $path.AddArc([single]($X + $W - $d), [single]($Y + $H - $d), [single]$d, [single]$d, 0, 90)
    $path.AddArc([single]$X, [single]($Y + $H - $d), [single]$d, [single]$d, 90, 90)
    $path.CloseFigure()
    $brush = New-Object System.Drawing.SolidBrush (Get-HdColor $Color)
    try {
        $script:g.FillPath($brush, $path)
        if ($Edge) {
            $pen = New-Object System.Drawing.Pen (Get-HdColor $Edge), ([single]$Width)
            $pen.LineJoin = 'Round'
            try { $script:g.DrawPath($pen, $path) } finally { $pen.Dispose() }
        }
    } finally { $brush.Dispose(); $path.Dispose() }
}

# A heart centered on the given point, S units across the half width.
function Add-HdHeart([string]$Color, [double]$Cx, [double]$Cy, [double]$S) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddBezier([single]$Cx, [single]($Cy + $S * 0.9), [single]($Cx - $S * 1.4), [single]($Cy + $S * 0.1), [single]($Cx - $S * 0.8), [single]($Cy - $S * 0.95), [single]$Cx, [single]($Cy - $S * 0.3))
    $path.AddBezier([single]$Cx, [single]($Cy - $S * 0.3), [single]($Cx + $S * 0.8), [single]($Cy - $S * 0.95), [single]($Cx + $S * 1.4), [single]($Cy + $S * 0.1), [single]$Cx, [single]($Cy + $S * 0.9))
    $brush = New-Object System.Drawing.SolidBrush (Get-HdColor $Color)
    try { $script:g.FillPath($brush, $path) } finally { $brush.Dispose(); $path.Dispose() }
}

# A four-pointed sparkle centered on the given point, R units from the middle to a tip.
function Add-HdSparkle([string]$Color, [double]$Cx, [double]$Cy, [double]$R) {
    $k = $R * 0.28
    $points = New-HdPoints @($Cx, ($Cy - $R), ($Cx + $k), ($Cy - $k), ($Cx + $R), $Cy, ($Cx + $k), ($Cy + $k), $Cx, ($Cy + $R), ($Cx - $k), ($Cy + $k), ($Cx - $R), $Cy, ($Cx - $k), ($Cy - $k))
    $brush = New-Object System.Drawing.SolidBrush (Get-HdColor $Color)
    try { $script:g.FillPolygon($brush, $points) } finally { $brush.Dispose() }
}

# A check mark whose bend is at the given point, S units across.
function Add-HdCheck([string]$Color, [double]$X, [double]$Y, [double]$S, [double]$Width = 1.8) {
    Add-HdStroke @(($X - $S * 0.9), ($Y - $S * 0.4), $X, ($Y + $S * 0.5), ($X + $S * 1.5), ($Y - $S * 1.1)) $Color $Width
}

# A crescent moon in the given circle of radius R, opening towards the lower right.
function Add-HdMoon([string]$Color, [double]$Cx, [double]$Cy, [double]$R) {
    $bite = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bite.AddEllipse([single]($Cx + $R * 0.45 - $R * 0.85), [single]($Cy - $R * 0.4 - $R * 0.85), [single]($R * 1.7), [single]($R * 1.7))
    $state = $script:g.Save()
    $brush = New-Object System.Drawing.SolidBrush (Get-HdColor $Color)
    try {
        $script:g.SetClip($bite, [System.Drawing.Drawing2D.CombineMode]::Exclude)
        $script:g.FillEllipse($brush, [single]($Cx - $R), [single]($Cy - $R), [single](2 * $R), [single](2 * $R))
    } finally {
        $script:g.Restore($state)
        $brush.Dispose(); $bite.Dispose()
    }
}

function Add-HdText([string]$Text, [string]$Color, [double]$X, [double]$Y) {
    $brush = New-Object System.Drawing.SolidBrush (Get-HdColor $Color)
    try { $script:g.DrawString($Text, $textFont, $brush, [single]$X, [single]$Y) } finally { $brush.Dispose() }
}

# A text with an outline, so that it can be read on any desktop color. X and Y are the top-left corner and Size the height
# of the letters' em box, in drawing units.
function Add-HdLabel([string]$Text, [string]$Color, [double]$X, [double]$Y, [string]$Edge = 'PropEdge', [double]$Size = 9) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddString($Text, $textFont.FontFamily, [int]$textFont.Style, [single]$Size, (New-Object System.Drawing.PointF ([single]$X), ([single]$Y)), [System.Drawing.StringFormat]::GenericDefault)
    $brush = New-Object System.Drawing.SolidBrush (Get-HdColor $Color)
    $pen = New-Object System.Drawing.Pen (Get-HdColor $Edge), ([single]1.4)
    $pen.LineJoin = 'Round'
    try {
        $script:g.DrawPath($pen, $path)
        $script:g.FillPath($brush, $path)
    } finally { $brush.Dispose(); $pen.Dispose(); $path.Dispose() }
}

# Starts a frame: a transparent canvas of 64 units a side that the Add-Hd functions draw on through $script:g. Dy lifts
# everything (negative is up); Tilt turns it clockwise by that many degrees about the point (PivotX, PivotY), by default the
# middle of the frame. Returns the bitmap.
function New-HdCanvas([double]$Dy = 0, [double]$Tilt = 0, [double]$PivotX = 32, [double]$PivotY = 32, [double]$Dx = 0) {
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
    $script:g.TranslateTransform([single]($PivotX + $Dx), [single]($PivotY + $Dy))
    $script:g.RotateTransform([single]$Tilt)
    $script:g.TranslateTransform([single](-$PivotX), [single](-$PivotY))
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

# Antivirus / indexers often hold freshly written files for a moment, so retry the manifest write whatever the failure.
function Write-PetManifest([string]$Path, [string]$Text) {
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
