# Generates the pixel-art robot skin for programmers (no third-party artwork). Art faces right.
# The robot sits at a desk and types code while the Coding state is on.
# Usage: powershell -NoProfile -File tools/GenerateRobotSkin.ps1
Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot '..\skins\robot'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Get-ChildItem $outDir -Filter *.png | Remove-Item

# One art pixel is 4x4 device pixels: the 128x128 frame is a 32x32 art grid.
$S = 4

function New-Brush([int]$R, [int]$G, [int]$B) {
    New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, $R, $G, $B))
}

$pal = @{
    Outline    = New-Brush 28 36 56
    Steel      = New-Brush 110 150 200
    SteelLight = New-Brush 170 200 240
    SteelDark  = New-Brush 70 100 150
    Screen     = New-Brush 14 22 38
    Cyan       = New-Brush 90 255 230
    CyanDim    = New-Brush 40 120 120
    Orange     = New-Brush 255 170 40
    Red        = New-Brush 255 70 70
    Green      = New-Brush 90 230 130
    Pink       = New-Brush 255 140 170
    White      = New-Brush 235 240 250
    Gray       = New-Brush 150 158 175
    Wood       = New-Brush 160 118 76
    WoodDark   = New-Brush 112 80 52
    Brown      = New-Brush 190 90 60
}
$textFont = New-Object System.Drawing.Font 'Consolas', 16, ([System.Drawing.FontStyle]::Bold)

# Fills a rectangle given in art pixels; $script:dy lifts or lowers everything drawn in the current frame.
function Px([string]$Color, [int]$X, [int]$Y, [int]$W = 1, [int]$H = 1) {
    $script:g.FillRectangle($pal[$Color], ($X * $S), (($Y + $script:dy) * $S), ($W * $S), ($H * $S))
}

function Add-Text([string]$Text, [string]$Color, [int]$X, [int]$Y) {
    $script:g.DrawString($Text, $textFont, $pal[$Color], [single]$X, [single]$Y)
}

function Add-Heart([int]$X, [int]$Y) {
    $rows = @('.X.X.', 'XXXXX', '.XXX.', '..X..')
    for ($r = 0; $r -lt 4; $r++) {
        for ($c = 0; $c -lt 5; $c++) {
            if ($rows[$r][$c] -eq 'X') { Px Pink ($X + $c) ($Y + $r) }
        }
    }
}

# Wisps of steam rising from an overheating head.
function Add-Steam([int]$Frame) {
    if ($Frame % 2 -eq 0) {
        Px White 12 3 1 2; Px White 15 1 1 2; Px White 19 3 1 2
    } else {
        Px White 12 2 1 2; Px White 16 2 1 2; Px White 19 1 1 2
    }
}

# A 3x5 pixel font for the small labels that say what the robot is doing. Each glyph is five rows of three pixels.
$glyphs = @{
    'A' = @('.X.', 'X.X', 'XXX', 'X.X', 'X.X'); 'B' = @('XX.', 'X.X', 'XX.', 'X.X', 'XX.')
    'C' = @('.XX', 'X..', 'X..', 'X..', '.XX'); 'D' = @('XX.', 'X.X', 'X.X', 'X.X', 'XX.')
    'E' = @('XXX', 'X..', 'XX.', 'X..', 'XXX'); 'F' = @('XXX', 'X..', 'XX.', 'X..', 'X..')
    'G' = @('.XX', 'X..', 'X.X', 'X.X', '.XX'); 'H' = @('X.X', 'X.X', 'XXX', 'X.X', 'X.X')
    'I' = @('XXX', '.X.', '.X.', '.X.', 'XXX'); 'J' = @('..X', '..X', '..X', 'X.X', '.X.')
    'K' = @('X.X', 'X.X', 'XX.', 'X.X', 'X.X'); 'L' = @('X..', 'X..', 'X..', 'X..', 'XXX')
    'M' = @('X.X', 'XXX', 'XXX', 'X.X', 'X.X'); 'N' = @('XX.', 'X.X', 'X.X', 'X.X', 'X.X')
    'O' = @('.X.', 'X.X', 'X.X', 'X.X', '.X.'); 'P' = @('XX.', 'X.X', 'XX.', 'X..', 'X..')
    'R' = @('XX.', 'X.X', 'XX.', 'X.X', 'X.X'); 'S' = @('.XX', 'X..', '.X.', '..X', 'XX.')
    'T' = @('XXX', '.X.', '.X.', '.X.', '.X.'); 'U' = @('X.X', 'X.X', 'X.X', 'X.X', 'XXX')
    'V' = @('X.X', 'X.X', 'X.X', 'X.X', '.X.'); 'W' = @('X.X', 'X.X', 'XXX', 'XXX', 'X.X')
    'X' = @('X.X', 'X.X', '.X.', 'X.X', 'X.X'); 'Y' = @('X.X', 'X.X', '.X.', '.X.', '.X.')
    'Z' = @('XXX', '..X', '.X.', 'X..', 'XXX'); '3' = @('XXX', '..X', '.XX', '..X', 'XXX')
    '!' = @('.X.', '.X.', '.X.', '...', '.X.'); '?' = @('XX.', '..X', '.X.', '...', '.X.')
}

# A dark label with up to three letters in a top corner (13x6 art pixels), above the head and beside the antenna lamp.
# It does not move with the robot's bounce.
function Add-Tag([string]$Text, [string]$Color, [string]$Side) {
    $saved = $script:dy
    $script:dy = 0
    $width = 4 * $Text.Length + 1
    $x0 = if ($Side -eq 'Right') { 32 - $width } else { 0 }
    Px Outline $x0 0 $width 6
    for ($i = 0; $i -lt $Text.Length; $i++) {
        $rows = $glyphs["$($Text[$i])"]
        for ($r = 0; $r -lt 5; $r++) {
            for ($c = 0; $c -lt 3; $c++) {
                if ($rows[$r][$c] -eq 'X') { Px $Color ($x0 + 1 + 4 * $i + $c) (1 + $r) }
            }
        }
    }
    $script:dy = $saved
}

# Renders one frame by running $Draw with a fresh canvas, then saves it. A label is drawn on top when Tag is given.
function Save-Frame([string]$Name, [scriptblock]$Draw, [int]$Dy = 0, [string]$Tag = '', [string]$TagColor = 'White', [string]$TagSide = 'Left') {
    $bmp = New-Object System.Drawing.Bitmap 128, 128
    $script:g = [System.Drawing.Graphics]::FromImage($bmp)
    $script:g.SmoothingMode = 'None'
    $script:g.PixelOffsetMode = 'Half'
    $script:g.TextRenderingHint = 'SingleBitPerPixelGridFit'
    $script:g.Clear([System.Drawing.Color]::Transparent)
    $script:dy = $Dy
    & $Draw
    if ($Tag) { Add-Tag $Tag $TagColor $TagSide }
    $bmp.Save((Join-Path $outDir $Name), [System.Drawing.Imaging.ImageFormat]::Png)
    $script:g.Dispose()
    $bmp.Dispose()
}

# A check mark of 5x4 art pixels at the given top-left corner.
function Add-Check([int]$X, [int]$Y) {
    Px Green $X ($Y + 2) 1 2; Px Green ($X + 1) ($Y + 3) 1 1
    Px Green ($X + 2) ($Y + 2) 1 1; Px Green ($X + 3) ($Y + 1) 1 1; Px Green ($X + 4) $Y 1 1
}

# A small plus-shaped sparkle centered on the given art pixel.
function Add-Sparkle([int]$X, [int]$Y) {
    Px White $X ($Y - 1) 1 3; Px White ($X - 1) $Y 3 1
}

# A cardboard parcel with tape, $Size art pixels square (3 or 5).
function Add-Box([int]$X, [int]$Y, [int]$Size) {
    $mid = [int](($Size - 1) / 2)
    Px Outline $X $Y $Size $Size
    Px Wood ($X + 1) ($Y + 1) ($Size - 2) ($Size - 2)
    Px Brown ($X + $mid) ($Y + 1) 1 ($Size - 2)
}

# A magnifying glass, lens first at the given top-left corner, handle pointing down-left.
function Add-Magnifier([int]$X, [int]$Y) {
    Px White ($X + 1) $Y 3 1; Px White ($X + 1) ($Y + 4) 3 1
    Px White $X ($Y + 1) 1 3; Px White ($X + 4) ($Y + 1) 1 3
    Px CyanDim ($X + 1) ($Y + 1) 3 3
    Px Gray ($X - 1) ($Y + 5) 1 1; Px Gray ($X - 2) ($Y + 6) 1 1
}

# A lightning bolt, 4x5 art pixels.
function Add-Bolt([int]$X, [int]$Y) {
    $rows = @('..XX', '.XX.', 'XXXX', '.XX.', 'XX..')
    for ($r = 0; $r -lt 5; $r++) {
        for ($c = 0; $c -lt 4; $c++) {
            if ($rows[$r][$c] -eq 'X') { Px Orange ($X + $c) ($Y + $r) }
        }
    }
}

# A crescent moon, 3x4 art pixels.
function Add-Moon([int]$X, [int]$Y) {
    Px White ($X + 1) $Y 2 1; Px White $X ($Y + 1) 1 2; Px White ($X + 1) ($Y + 3) 2 1
}

# The standing robot. Eyes: open | closed | happy | tired | focus | surprised | none. Feet: stride offset.
# Spinner: 0-3 lights one dot of a loading ring where the eyes are. Progress: 0-4 fills the chest bar (-1 hides it).
# EyeShift moves open eyes sideways; Glasses puts round lenses over them.
function Add-Robot {
    param(
        [string]$Eyes = 'open', [string]$Lamp = 'Orange', [string]$Light = 'Cyan',
        [int]$Feet = 0, [switch]$ArmsUp, [switch]$Headset, [switch]$Glasses,
        [int]$EyeShift = 0, [int]$Spinner = -1, [int]$Progress = -1,
        [switch]$RightArmUp, [int]$RightArmDx = 0, [switch]$Mic,
        [switch]$Battery, [int]$BatteryBars = 0, [string]$BatteryColor = 'Red',
        [int]$EyeDy = 0, [switch]$LeftArmReach
    )

    # Treads.
    Px Outline (9 + $Feet) 27 6 3;  Px SteelDark (10 + $Feet) 28 4 1
    Px Outline (17 - $Feet) 27 6 3; Px SteelDark (18 - $Feet) 28 4 1

    # Arms.
    $ayLeft = if ($ArmsUp) { 12 } else { 18 }
    $ayRight = if ($ArmsUp -or $RightArmUp) { 12 } else { 18 }
    if ($LeftArmReach) {
        # The left arm stretches out sideways to hold on to the edge of the screen.
        Px Outline 0 12 10 3; Px Steel 1 13 8 1; Px Orange 0 12 2 3
    } else {
        Px Outline 7 $ayLeft 3 7;  Px Steel 8 ($ayLeft + 1) 1 5
    }
    Px Outline (22 + $RightArmDx) $ayRight 3 7; Px Steel (23 + $RightArmDx) ($ayRight + 1) 1 5

    # Torso with a chest panel and status light.
    Px Outline 10 17 12 10; Px Steel 11 18 10 8
    Px Screen 13 20 6 4
    if ($Battery) {
        # A battery icon with up to three bars.
        Px White 13 20 5 4; Px Screen 14 21 3 2; Px White 18 21 1 2
        if ($BatteryBars -gt 0) { Px $BatteryColor 14 21 $BatteryBars 2 }
    } elseif ($Progress -ge 0) {
        Px CyanDim 14 21 4 2
        if ($Progress -gt 0) { Px Green 14 21 $Progress 2 }
    } else {
        Px $Light 15 21 2 2
    }

    # Neck and head with a screen for a face.
    Px Outline 14 16 4 1
    if ($Headset) {
        Px SteelDark 9 5 14 1
        Px Outline 7 8 3 6; Px Outline 22 8 3 6
        Px Orange 7 10 1 2; Px Orange 24 10 1 2
        if ($Mic) {
            # A microphone boom from the left ear cup to the mouth.
            Px SteelDark 8 14 1 3; Px SteelDark 9 16 2 1; Px Red 11 16 1 1
        }
    }
    Px Outline 9 6 14 10; Px Steel 10 7 12 8; Px Screen 11 8 10 6

    # Antenna with a lamp.
    Px SteelDark 15 3 2 3
    Px $Lamp 14 1 4 2

    switch ($Eyes) {
        'open'      { Px Cyan (13 + $EyeShift) (9 + $EyeDy) 2 3; Px Cyan (17 + $EyeShift) (9 + $EyeDy) 2 3 }
        'dizzy'     {
            foreach ($x0 in 13, 17) { Px Cyan $x0 9; Px Cyan ($x0 + 2) 9; Px Cyan ($x0 + 1) 10; Px Cyan $x0 11; Px Cyan ($x0 + 2) 11 }
        }
        'closed'    { Px Cyan 13 10 2 1; Px Cyan 17 10 2 1 }
        'happy'     {
            foreach ($x0 in 12, 17) { Px Cyan $x0 10; Px Cyan ($x0 + 1) 9; Px Cyan ($x0 + 2) 10 }
            Px Pink 12 12; Px Pink 19 12
        }
        'tired'     { Px CyanDim 13 10 3 2; Px CyanDim 17 10 3 2; Px Steel 13 9 3 1; Px Steel 17 9 3 1 }
        'focus'     { Px Cyan 13 10 3 2; Px Cyan 17 10 3 2; Px White 12 9 5 1; Px White 17 9 5 1 }
        'surprised' { Px Cyan 13 9 3 3; Px Screen 14 10; Px Cyan 17 9 3 3; Px Screen 18 10 }
    }

    if ($Spinner -ge 0) {
        $dots = @(@(13, 9), @(17, 9), @(17, 11), @(13, 11))
        for ($i = 0; $i -lt 4; $i++) {
            Px $(if ($i -eq $Spinner) { 'Cyan' } else { 'CyanDim' }) $dots[$i][0] $dots[$i][1] 2 2
        }
    }

    if ($Glasses) {
        foreach ($x0 in 11, 16) {
            Px White ($x0) 8 5 1; Px White ($x0) 12 5 1
            Px White ($x0) 9 1 3;  Px White ($x0 + 4) 9 1 3
        }
    }
}

# Idle: stands and blinks now and then.
Save-Frame 'idle_0.png' { Add-Robot }
Save-Frame 'idle_1.png' { Add-Robot -Lamp SteelDark }
Save-Frame 'idle_2.png' { Add-Robot }
Save-Frame 'idle_3.png' { Add-Robot -Eyes closed -Lamp SteelDark }

# Walk: rolls along with alternating tread offsets and a little bounce.
Save-Frame 'walk_0.png' { Add-Robot -Feet 2 }
Save-Frame 'walk_1.png' -Dy -1 { Add-Robot -Feet 0 -Lamp SteelDark }
Save-Frame 'walk_2.png' { Add-Robot -Feet -2 }
Save-Frame 'walk_3.png' -Dy -1 { Add-Robot -Feet 0 -Lamp SteelDark }

# Sleep: standby mode, dim lights, floating z.
Save-Frame 'sleep_0.png' -Dy 1 { Add-Robot -Eyes closed -Lamp SteelDark -Light CyanDim }
Save-Frame 'sleep_1.png' -Dy 1 { Add-Robot -Eyes closed -Lamp SteelDark -Light CyanDim; Add-Text 'z' 'White' 98 18; Add-Text 'Z' 'White' 106 4 }

# Drag: lifted by the antenna, arms up, treads dangling.
Save-Frame 'drag_0.png' { Add-Robot -Eyes surprised -ArmsUp -Feet 1 }
Save-Frame 'drag_1.png' { Add-Robot -Eyes surprised -ArmsUp -Feet -1 -Lamp SteelDark }

# Tired: overheating, low battery eyes, steam.
Save-Frame 'tired_0.png' -Dy 1 { Add-Robot -Eyes tired -Lamp Red -Light CyanDim; Add-Steam 0 }
Save-Frame 'tired_1.png' -Dy 1 { Add-Robot -Eyes tired -Lamp SteelDark -Light CyanDim; Add-Steam 1 }

# Alert: jumps with a flashing red lamp and an exclamation mark.
Save-Frame 'alert_0.png' -Dy -2 { Add-Robot -Eyes surprised -ArmsUp -Lamp Red -Light Red -Feet 1; Add-Text '!' 'Red' 100 6 }
Save-Frame 'alert_1.png' { Add-Robot -Eyes surprised -ArmsUp -Lamp Orange -Light Red -Feet -1; Add-Text '!' 'Red' 100 6 }

# Focus: headset on, eyes narrowed, quietly concentrating.
Save-Frame 'focus_0.png' { Add-Robot -Eyes focus -Headset }
Save-Frame 'focus_1.png' { Add-Robot -Eyes focus -Headset -Lamp SteelDark }

# Happy: bounces with smiling eyes and a heart.
Save-Frame 'happy_0.png' -Dy -2 { Add-Robot -Eyes happy -ArmsUp -Feet 1; Add-Heart 24 2 }
Save-Frame 'happy_1.png' { Add-Robot -Eyes happy -ArmsUp -Feet -1 }

# Building: a loading ring on the face and a filling bar on the chest, while the lamp blinks.
foreach ($f in 0..3) {
    $frame = $f
    $draw = { Add-Robot -Eyes none -Spinner $frame -Progress ($frame + 1) -Lamp $(if ($frame % 2 -eq 0) { 'Orange' } else { 'SteelDark' }) }.GetNewClosure()
    Save-Frame "building_$f.png" -Tag 'BLD' -TagColor Orange $draw
}

# BuildDone: arms up, smiling, a green check mark; the second frame adds a sparkle.
Save-Frame 'builddone_0.png' -Tag 'OK!' -TagColor Green -Dy -2 { Add-Robot -Eyes happy -ArmsUp -Feet 1; Add-Check 24 2 }
Save-Frame 'builddone_1.png' -Tag 'OK!' -TagColor Green { Add-Robot -Eyes happy -ArmsUp -Feet -1; Add-Check 24 2; Add-Sparkle 4 5 }

# GitPush: holds a parcel overhead, throws it up, and it flies off.
Save-Frame 'gitpush_0.png' -Tag 'GIT' -TagColor Cyan { Add-Robot -Eyes open -ArmsUp; Add-Box 21 6 5 }
Save-Frame 'gitpush_1.png' -Tag 'GIT' -TagColor Cyan -Dy -1 { Add-Robot -Eyes happy -ArmsUp -Feet 1; Add-Box 25 2 5 }
Save-Frame 'gitpush_2.png' -Tag 'GIT' -TagColor Cyan { Add-Robot -Eyes happy; Add-Box 28 0 3; Px Gray 25 2 2 1; Px Gray 26 4 2 1 }

# Reading: round glasses, eyes scanning along, a magnifying glass held up.
foreach ($f in 0..3) {
    $shift = @(-1, 0, 1, 0)[$f]
    $bob = @(0, 1, 0, 1)[$f]
    $draw = { Add-Robot -Eyes open -ArmsUp -Glasses -EyeShift $shift; Add-Magnifier 25 (7 + $bob) }.GetNewClosure()
    Save-Frame "reading_$f.png" -Tag 'WEB' -TagColor Cyan $draw
}

# Welcome: waves hello with one arm and hearts floating up.
foreach ($f in 0..3) {
    $dx = @(0, 1, 0, -1)[$f]
    $hearts = ($f % 2 -eq 0)
    $draw = { Add-Robot -Eyes happy -RightArmUp -RightArmDx $dx; if ($hearts) { Add-Heart 24 2 } }.GetNewClosure()
    Save-Frame "welcome_$f.png" -Tag 'HI!' -TagColor Pink $draw
}

# Meeting: headset with a microphone, listening quietly; the lamp glows red and dots show it is listening.
foreach ($f in 0..3) {
    $count = $f
    $draw = {
        Add-Robot -Eyes open -Headset -Mic -Lamp $(if ($count % 2 -eq 0) { 'Red' } else { 'SteelDark' })
        for ($i = 0; $i -lt $count; $i++) { Px White (24 + 2 * $i) 4 1 1 }
    }.GetNewClosure()
    Save-Frame "meeting_$f.png" -Tag 'MTG' -TagColor Red $draw
}

# LowBattery: slouching, heavy eyes, an almost empty battery on the chest that blinks red.
Save-Frame 'lowbattery_0.png' -Tag 'LOW' -TagColor Red -Dy 1 { Add-Robot -Eyes tired -Lamp Red -Light CyanDim -Battery -BatteryBars 1 -BatteryColor Red }
Save-Frame 'lowbattery_1.png' -Tag 'LOW' -TagColor Red -Dy 1 { Add-Robot -Eyes tired -Lamp SteelDark -Light CyanDim -Battery -BatteryBars 0 -BatteryColor Red }

# Charging: plugged into a wall socket, the battery fills up and a bolt flashes.
foreach ($f in 0..3) {
    $bars = $f
    $bolt = ($f % 2 -eq 0)
    $draw = {
        Add-Robot -Eyes happy -Battery -BatteryBars $bars -BatteryColor Green
        Px Gray 22 21 7 1; Px Outline 21 20 2 3
        Px White 29 19 2 5; Px Outline 29 21 1 1
        if ($bolt) { Add-Bolt 24 2 }
    }.GetNewClosure()
    Save-Frame "charging_$f.png" -Tag 'CHG' -TagColor Green $draw
}

# Meal: holds a steaming bowl of rice in one hand and eats with chopsticks.
foreach ($f in 0..3) {
    $steamUp = ($f % 2 -eq 0)
    $stickY = @(8, 6, 8, 7)[$f]
    $eyes = if ($f -eq 3) { 'closed' } else { 'happy' }
    $draw = {
        Add-Robot -Eyes $eyes -RightArmUp
        # The bowl: rice heaped over a brown bowl, steam above it.
        Px White 26 12 4 1; Px White 25 13 6 1
        Px Brown 25 14 6 2; Px Outline 26 16 4 1
        if ($steamUp) { Px White 26 10 1 2; Px White 29 9 1 2 } else { Px White 27 9 1 2; Px White 29 10 1 2 }
        # A pair of chopsticks dipping into the rice.
        Px Wood 27 $stickY 1 5; Px Wood 29 ($stickY - 1) 1 5
    }.GetNewClosure()
    Save-Frame "meal_$f.png" -Tag 'EAT' -TagColor Orange $draw
}

# ClockOut: smiling arms-up robot beside a big clock that rings at six.
foreach ($f in 0..3) {
    $shake = @(0, 1, 0, -1)[$f]
    $ringing = ($f % 2 -eq 0)
    $draw = {
        Add-Robot -Eyes happy -ArmsUp -Feet ($f % 2)
        $cx = 24 + $shake
        Px Outline ($cx + 1) 2 5 1; Px Outline $cx 3 7 5; Px Outline ($cx + 1) 8 5 1
        Px White ($cx + 1) 3 5 5
        Px Outline ($cx + 3) 3 1 3; Px Red ($cx + 3) 5 1 2
        if ($ringing) { Px Orange ($cx - 1) 1 1 2; Px Orange ($cx + 7) 1 1 2 }
    }.GetNewClosure()
    Save-Frame "clockout_$f.png" -Tag 'BYE' -TagColor Orange $draw
}

# Chime: rings a small golden bell on the hour, with sound waves beside it.
foreach ($f in 0..3) {
    $swing = @(-1, 0, 1, 0)[$f]
    $waves = ($f % 2 -eq 0)
    $dy = if ($f % 2 -eq 1) { -1 } else { 0 }
    $draw = {
        Add-Robot -Eyes open -RightArmUp
        Px Orange 27 9 3 1; Px Orange 26 10 5 3; Px Orange 25 13 7 1
        Px White 27 10 1 2; Px Brown (28 + $swing) 14 1 1
        if ($waves) { Px White 24 10 1 2; Px White 31 10 1 2 } else { Px White 24 8 1 2; Px White 31 8 1 2 }
    }.GetNewClosure()
    Save-Frame "chime_$f.png" -Tag 'BEL' -TagColor Orange $draw -Dy $dy
}

# Festive: a striped party hat on the head, confetti falling around the cheering robot.
$confetti = @(
    @(@(3, 3, 'Orange'), @(28, 6, 'Green'), @(5, 14, 'Pink'), @(27, 15, 'Cyan')),
    @(@(4, 6, 'Pink'), @(27, 3, 'Orange'), @(3, 17, 'Green'), @(28, 12, 'White')),
    @(@(2, 9, 'Green'), @(29, 9, 'Pink'), @(5, 3, 'Cyan'), @(26, 17, 'Orange')),
    @(@(3, 12, 'Cyan'), @(28, 3, 'White'), @(4, 20, 'Orange'), @(27, 8, 'Pink'))
)
foreach ($f in 0..3) {
    $bits = $confetti[$f]
    $draw = {
        Add-Robot -Eyes happy -ArmsUp -Feet ($f % 2)
        # The hat covers the antenna: a cone with a pompom on top.
        Px White 16 0 1 1
        Px Pink 16 1 1 1; Px Orange 15 2 3 1; Px Pink 14 3 5 1; Px Orange 13 4 7 1; Px Pink 12 5 9 1
        foreach ($bit in $bits) { Px $bit[2] $bit[0] $bit[1] 1 1 }
    }.GetNewClosure()
    Save-Frame "festive_$f.png" -Tag 'YAY' -TagColor Pink $draw -Dy $(if ($f % 2 -eq 0) { 0 } else { 1 })
}

# Dizzy: crossed-out eyes, wobbling, with stars circling around the head.
$starSlots = @(@(19, 2), @(24, 1), @(28, 5), @(25, 9))
foreach ($f in 0..3) {
    $first = $starSlots[$f]
    $second = $starSlots[($f + 2) % 4]
    $draw = {
        Add-Robot -Eyes dizzy -Feet ($f % 2) -Lamp $(if ($f % 2 -eq 0) { 'Orange' } else { 'SteelDark' })
        Px Orange $first[0] $first[1] 1 1; Px Orange ($first[0] - 1) $first[1] 3 1; Px Orange $first[0] ($first[1] - 1) 1 3
        Px White $second[0] $second[1] 1 1; Px White ($second[0] - 1) $second[1] 3 1; Px White $second[0] ($second[1] - 1) 1 3
    }.GetNewClosure()
    Save-Frame "dizzy_$f.png" -Tag 'DIZ' -TagColor Orange $draw -Dy $(if ($f % 2 -eq 0) { 0 } else { 1 })
}

# Hang: clings to the left edge of the screen by one outstretched arm, legs swinging. Mirrored for the right edge.
Save-Frame 'hang_0.png' { Add-Robot -Eyes open -LeftArmReach -Feet 1 }
Save-Frame 'hang_1.png' { Add-Robot -Eyes open -LeftArmReach -Feet -1 -Lamp SteelDark }

# Idle looks: the eyes follow the mouse pointer.
Save-Frame 'idle_left.png'  { Add-Robot -Eyes open -EyeShift -2 }
Save-Frame 'idle_right.png' { Add-Robot -Eyes open -EyeShift 2 }
Save-Frame 'idle_up.png'    { Add-Robot -Eyes open -EyeDy -1 }
Save-Frame 'idle_down.png'  { Add-Robot -Eyes open -EyeDy 1 }

# Coding: the robot sits in a chair at a desk, typing while code scrolls on the monitor.
$codeLines = @(
    @(0, 5, 'Green'), @(2, 3, 'Cyan'), @(2, 6, 'Orange'), @(0, 2, 'Green'),
    @(2, 5, 'Green'), @(4, 3, 'Cyan'), @(2, 4, 'Orange'), @(0, 6, 'Green')
)

# Night: heavy eyes, a moon and stars, a steaming coffee and a sleepy 'zZ' now and then.
function Add-CodingScene([int]$Frame, [switch]$Night) {
    if ($Night) {
        Add-Moon 16 1
        Px White 13 3; Px White 20 4
    }

    # Chair.
    Px SteelDark 1 15 2 10
    Px SteelDark 1 24 11 2
    Px Outline 6 26 2 3
    Px Outline 3 29 8 1

    # Desk.
    Px Outline 29 23 2 7
    Px Wood 11 20 21 2
    Px WoodDark 11 22 21 1

    # Monitor showing scrolling code and a blinking cursor.
    Px SteelDark 24 16 2 3
    Px SteelDark 22 19 6 1
    Px Outline 19 6 12 10
    Px Screen 20 7 10 8
    for ($i = 0; $i -lt 4; $i++) {
        $line = $codeLines[($Frame + $i) % $codeLines.Count]
        Px $line[2] (21 + $line[0]) (8 + 2 * $i) $line[1] 1
        if ($i -eq 3 -and $Frame % 2 -eq 0) {
            Px White ([Math]::Min(21 + $line[0] + $line[1] + 1, 28)) 14 1 1
        }
    }

    # Keyboard and a mug of coffee.
    Px Gray 12 19 6 1
    Px Brown 19 18 2 2
    Px White (19 + $Frame % 2) 17 1 1

    # The robot: legs under the desk, torso, head looking at the screen.
    Px Outline 8 22 8 3;   Px SteelDark 9 23 6 1
    Px Outline 14 24 3 5;  Px Outline 14 28 5 2
    Px Outline 3 15 8 8;   Px Steel 4 16 6 6
    Px Screen 5 17 4 3
    Px $(if ($Frame % 2 -eq 0) { 'Cyan' } else { 'CyanDim' }) 6 18 2 1
    Px Outline 7 14 3 1
    Px Outline 3 5 11 9;   Px Steel 4 6 9 7;  Px Screen 5 7 8 5
    Px SteelDark 7 3 2 2
    Px $(if ($Frame % 2 -eq 0) { 'Orange' } else { 'SteelDark' }) 6 1 4 2
    if ($Night) {
        # Half-lidded eyes with dark circles under them; the last frame nods off.
        if ($Frame -eq 3) {
            Px CyanDim 8 9 2 1; Px CyanDim 11 9 2 1
        } else {
            Px CyanDim 8 8 2 2; Px CyanDim 11 8 2 2; Px Steel 8 8 2 1; Px Steel 11 8 2 1
        }
        Px SteelDark 8 10 2 1; Px SteelDark 11 10 2 1
    } elseif ($Frame -eq 3) {
        Px Cyan 8 9 2 1; Px Cyan 11 9 2 1
    } else {
        Px Cyan 8 8 2 2; Px Cyan 11 8 2 2
    }

    # Arms reaching the keyboard; the hands take turns pressing keys.
    $a = $Frame % 2
    Px Outline 9 16 2 3
    Px Outline 10 17 6 2
    Px SteelLight 14 (18 + $a) 2 1
    Px SteelLight 16 (19 - $a) 2 1

    if ($Night) {
        # A second wisp of steam over the coffee.
        Px White (20 - $Frame % 2) 16 1 1
        if ($Frame % 2 -eq 1) { Add-Text 'zZ' 'White' 84 1 }
    }
}

foreach ($f in 0..3) {
    $scene = { Add-CodingScene $f }.GetNewClosure()
    Save-Frame "coding_$f.png" -Tag 'DEV' -TagColor Green $scene
    $night = { Add-CodingScene $f -Night }.GetNewClosure()
    Save-Frame "nightcoding_$f.png" -Tag '3AM' -TagColor Orange $night
}

$manifest = @'
{
  "name": "robot",
  "frameSize": [64, 64],
  "states": {
    "Idle":   { "fps": 2, "frames": ["idle_0.png", "idle_1.png", "idle_2.png", "idle_3.png"] },
    "Walk":   { "fps": 8, "frames": ["walk_0.png", "walk_1.png", "walk_2.png", "walk_3.png"] },
    "Sleep":  { "fps": 1, "frames": ["sleep_0.png", "sleep_1.png"] },
    "Drag":   { "fps": 6, "frames": ["drag_0.png", "drag_1.png"] },
    "Tired":  { "fps": 2, "frames": ["tired_0.png", "tired_1.png"] },
    "Alert":  { "fps": 4, "frames": ["alert_0.png", "alert_1.png"] },
    "Focus":  { "fps": 1, "frames": ["focus_0.png", "focus_1.png"] },
    "Happy":  { "fps": 5, "frames": ["happy_0.png", "happy_1.png"] },
    "Coding": { "fps": 4, "frames": ["coding_0.png", "coding_1.png", "coding_2.png", "coding_3.png"] },
    "Building":    { "fps": 4, "frames": ["building_0.png", "building_1.png", "building_2.png", "building_3.png"] },
    "BuildDone":   { "fps": 5, "frames": ["builddone_0.png", "builddone_1.png"] },
    "GitPush":     { "fps": 4, "frames": ["gitpush_0.png", "gitpush_1.png", "gitpush_2.png"] },
    "NightCoding": { "fps": 4, "frames": ["nightcoding_0.png", "nightcoding_1.png", "nightcoding_2.png", "nightcoding_3.png"] },
    "Reading":     { "fps": 3, "frames": ["reading_0.png", "reading_1.png", "reading_2.png", "reading_3.png"] },
    "Welcome":     { "fps": 4, "frames": ["welcome_0.png", "welcome_1.png", "welcome_2.png", "welcome_3.png"] },
    "Meeting":     { "fps": 2, "frames": ["meeting_0.png", "meeting_1.png", "meeting_2.png", "meeting_3.png"] },
    "LowBattery":  { "fps": 2, "frames": ["lowbattery_0.png", "lowbattery_1.png"] },
    "Charging":    { "fps": 3, "frames": ["charging_0.png", "charging_1.png", "charging_2.png", "charging_3.png"] },
    "Meal":        { "fps": 3, "frames": ["meal_0.png", "meal_1.png", "meal_2.png", "meal_3.png"] },
    "ClockOut":    { "fps": 4, "frames": ["clockout_0.png", "clockout_1.png", "clockout_2.png", "clockout_3.png"] },
    "Chime":       { "fps": 4, "frames": ["chime_0.png", "chime_1.png", "chime_2.png", "chime_3.png"] },
    "Festive":     { "fps": 4, "frames": ["festive_0.png", "festive_1.png", "festive_2.png", "festive_3.png"] },
    "Dizzy":       { "fps": 6, "frames": ["dizzy_0.png", "dizzy_1.png", "dizzy_2.png", "dizzy_3.png"] },
    "Hang":        { "fps": 2, "frames": ["hang_0.png", "hang_1.png"] },
    "Idle.Left":   { "fps": 1, "frames": ["idle_left.png"] },
    "Idle.Right":  { "fps": 1, "frames": ["idle_right.png"] },
    "Idle.Up":     { "fps": 1, "frames": ["idle_up.png"] },
    "Idle.Down":   { "fps": 1, "frames": ["idle_down.png"] }
  }
}
'@

# Antivirus / indexers often hold freshly written files for a moment, so retry the manifest write.
$manifestPath = Join-Path $outDir 'manifest.json'
foreach ($attempt in 1..10) {
    try {
        Set-Content -Encoding UTF8 -Path $manifestPath -Value $manifest -ErrorAction Stop
        break
    }
    catch [System.IO.IOException] {
        if ($attempt -eq 10) { throw }
        Start-Sleep -Milliseconds 500
    }
}

Write-Host "Robot skin written to $outDir"
