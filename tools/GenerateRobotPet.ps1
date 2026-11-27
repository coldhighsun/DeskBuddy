# Generates the robot pet for programmers into pets\robot: smooth vector art (see PetVectorDrawing.ps1), no third-party artwork.
# The robot sits at a desk and types code while the Coding state is on. It is the default pet, and it has its own state list
# (scenario states with a small label above the head), so it does not use PetVectorAnimations.ps1.
# Run: powershell -NoProfile -File tools/GenerateRobotPet.ps1
. (Join-Path $PSScriptRoot 'PetVectorDrawing.ps1')

$PetName = 'robot'
# 机器人 spelled with code points, so the script does not depend on its own file encoding.
$PetNameZh = [string][char]0x673A + [string][char]0x5668 + [string][char]0x4EBA
$PetNameZhHant = [string][char]0x6A5F + [string][char]0x5668 + [string][char]0x4EBA

$outDir = Join-Path $PSScriptRoot '..\pets\robot'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Get-ChildItem $outDir -Filter *.png | Remove-Item

$pal = @{
    Outline    = New-HdColor 28 36 56
    Steel      = New-HdColor 112 152 202
    SteelLight = New-HdColor 176 206 244
    SteelDark  = New-HdColor 70 100 150
    Screen     = New-HdColor 14 22 38
    Cyan       = New-HdColor 90 255 230
    CyanDim    = New-HdColor 40 120 120
    Orange     = New-HdColor 255 170 40
    Red        = New-HdColor 255 70 70
    Green      = New-HdColor 90 230 130
    Pink       = New-HdColor 255 140 170
    White      = New-HdColor 235 240 250
    Gray       = New-HdColor 150 158 175
    Wood       = New-HdColor 160 118 76
    WoodDark   = New-HdColor 112 80 52
    Brown      = New-HdColor 190 90 60
}
Set-HdDefaultColors
$textFont = New-Object System.Drawing.Font ('Consolas', [single]9, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)

# Everything below is drawn in the units of a grounded frame minus four: the robot's tread ends at y 60 and a frame that stands on
# the ground is lowered by four to rest on the bottom edge, which sits on the taskbar. $script:sink is how far the body sinks into
# the tread (a slouch), which stays on the ground.
$script:sink = 0

# Wisps of steam rising from an overheating head.
function Add-Steam([int]$Frame) {
    if ($Frame % 2 -eq 0) {
        Add-HdStroke @(24, 7, 23, 5, 24.4, 3, 23.6, 1) 'White' 1.3
        Add-HdStroke @(31, 5, 32.4, 3, 31, 1.4) 'White' 1.3
        Add-HdStroke @(39, 7, 38, 5, 39.4, 3, 38.6, 1) 'White' 1.3
    } else {
        Add-HdStroke @(24, 6, 25.4, 4, 24, 2) 'White' 1.3
        Add-HdStroke @(32, 7, 31, 5, 32.4, 3, 31.6, 1) 'White' 1.3
        Add-HdStroke @(38, 5, 39.4, 3, 38, 1.4) 'White' 1.3
    }
}

# A small dark label with up to three letters in the top left corner that says what the robot is doing. It does not move with
# the robot's bounce.
function Add-Tag([string]$Text, [string]$Color) {
    $width = 5 + 6.6 * $Text.Length
    Add-HdRoundRect 'Outline' 0.5 0.5 $width 11.5 3 '' 0
    $font = New-Object System.Drawing.Font ('Consolas', [single]8.6, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = 'Center'; $format.LineAlignment = 'Center'
    $brush = New-Object System.Drawing.SolidBrush (Get-HdColor $Color)
    try {
        $script:g.DrawString($Text, $font, $brush, (New-Object System.Drawing.RectangleF ([single]0.5), ([single]0.8), ([single]$width), ([single]11.5)), $format)
    } finally { $brush.Dispose(); $font.Dispose(); $format.Dispose() }
}

# Renders one frame by running $Draw with a fresh canvas, then saves it; a label is drawn on top when Tag is given. Dy is in
# half units (art pixels of the old pixel version): a negative Dy is a jump, a positive one sinks the body into the tread.
# Air is for the frames in which the robot is not on the ground (hanging, dragged, climbing, on the ceiling): they are not
# lowered. Flip turns the finished picture upside down, for a robot on the ceiling; Mirror turns it left to right, for a side
# view drawn facing left. Tilt turns the drawing clockwise by that many degrees about the centre of the frame.
function Save-Frame([string]$Name, [scriptblock]$Draw, [int]$Dy = 0, [string]$Tag = '', [string]$TagColor = 'White', [switch]$Flip, [switch]$Mirror, [switch]$Air, [double]$Tilt = 0) {
    if ($Air) {
        $script:sink = 0
        $offset = 2 * $Dy
    } else {
        $script:sink = 2 * [Math]::Max($Dy, 0)
        $offset = 2 * [Math]::Min($Dy, 0) + 4 + $script:sink
    }
    $bmp = New-HdCanvas -Dy $offset -Tilt $Tilt
    & $Draw
    if ($Tag) {
        $script:g.ResetTransform()
        $script:g.ScaleTransform($HdScale, $HdScale)
        Add-Tag $Tag $TagColor
    }
    Save-HdCanvas $bmp (Join-Path $outDir $Name) -Flip:$Flip -Mirror:$Mirror
}

# A parcel with tape, S units square.
function Add-Box([double]$X, [double]$Y, [double]$S) {
    Add-HdRoundRect 'Wood' $X $Y $S $S 1 'Outline' 1
    Add-HdRoundRect 'Brown' ($X + $S / 2 - 1) ($Y + 0.7) 2 ($S - 1.4) 0.5 '' 0
}

# A magnifying glass, the lens centered on the given point and the handle pointing down-left.
function Add-Magnifier([double]$Cx, [double]$Cy) {
    Add-HdStroke @(($Cx - 3.4), ($Cy + 3.4), ($Cx - 7.4), ($Cy + 7.4)) 'Gray' 2.2
    Add-HdOval 'CyanDim' $Cx $Cy 4.4 4.4 'White' 1.3
}

# A lightning bolt, about 8 units wide and 11 tall, with its top left corner at the given point.
function Add-Bolt([double]$X, [double]$Y) {
    Add-HdPoly 'Orange' @(($X + 5), $Y, $X, ($Y + 6), ($X + 3.6), ($Y + 6), ($X + 1.4), ($Y + 11), ($X + 8), ($Y + 4.4), ($X + 4.2), ($Y + 4.4), ($X + 6.6), $Y) 'Outline' 0.7
}

# One tread: a dark belt with two wheels.
function Add-Tread([double]$X) {
    Add-HdRoundRect 'SteelDark' $X 53.6 12 6 2.8 'Outline' 1.1
    Add-HdOval 'SteelLight' ($X + 3.2) 56.6 1.3 1.3 ''
    Add-HdOval 'SteelLight' ($X + 8.8) 56.6 1.3 1.3 ''
}

# Where the hands hold on to the umbrella or the parachute in the glide, beside the head (the manifest's Glide grips; the
# glide frames are saved with -Air and no lift, so these are their places in the frame).
$glideGrips = @(@(13.4, 20), @(50.6, 20))

# The standing robot. Eyes: open | closed | happy | tired | focus | surprised | none. Feet: stride offset.
# Spinner: 0-3 lights one dot of a loading ring where the eyes are. Progress: 0-4 fills the chest bar (-1 hides it).
# EyeShift moves open eyes sideways; Glasses puts round lenses over them. HoldOverhead raises both arms behind the
# head to the handle of an umbrella or parachute above it.
function Add-Robot {
    param(
        [string]$Eyes = 'open', [string]$Lamp = 'Orange', [string]$Light = 'Cyan',
        [int]$Feet = 0, [switch]$ArmsUp, [switch]$Headset, [switch]$Glasses,
        [double]$EyeShift = 0, [int]$Spinner = -1, [int]$Progress = -1,
        [switch]$RightArmUp, [double]$RightArmDx = 0, [switch]$Mic,
        [switch]$Battery, [int]$BatteryBars = 0, [string]$BatteryColor = 'Red',
        [double]$EyeDy = 0, [switch]$LeftArmReach, [double]$ReachY = 27, [switch]$HoldOverhead
    )

    # Treads, which stay on the ground when the rest of the robot sinks into a slouch.
    $state = $script:g.Save()
    $script:g.TranslateTransform(0, [single](-$script:sink))
    Add-Tread (18 + 2 * $Feet)
    Add-Tread (34 - 2 * $Feet)
    $script:g.Restore($state)

    # Arms. Held up to the umbrella or parachute, they go straight up from the shoulders beside the head, and the hands
    # (drawn last, in front) hold on at the manifest's Glide grips.
    if ($HoldOverhead) {
        foreach ($side in @(@(16, $glideGrips[0]), @(48, $glideGrips[1]))) {
            Add-HdLimb @($side[0], 36, $side[1][0], ($side[1][1] + 1)) 'Steel' 3.6
        }
    } else {
        if ($LeftArmReach) {
            # The left arm stretches out sideways to hold on to the edge of the screen.
            Add-HdLimb @(20, 28, 5, $ReachY) 'Steel' 3.4
            Add-HdOval 'Orange' 3.6 $ReachY 3.2 2.8
        } else {
            $y0 = if ($ArmsUp) { 26 } else { 38 }
            Add-HdLimb @(17, $y0, 17, ($y0 + 10)) 'Steel' 3.6
        }
        $y1 = if ($ArmsUp -or $RightArmUp) { 26 } else { 38 }
        Add-HdLimb @((47 + $RightArmDx), $y1, (47 + $RightArmDx), ($y1 + 10)) 'Steel' 3.6
    }

    # Torso with a chest panel and status light.
    Add-HdRoundRect 'Steel' 20 34 24 20 4 'Outline' 1.1
    Add-HdStroke @(22.6, 37, 22.6, 50) 'SteelLight' 1.1
    Add-HdRoundRect 'Screen' 26 40 12 8 2 '' 0
    if ($Battery) {
        # A battery icon with up to three bars.
        Add-HdRoundRect 'White' 26.6 40.8 10 6.4 1.2 '' 0
        Add-HdRoundRect 'White' 36.4 42.4 1.6 3.2 0.5 '' 0
        Add-HdRoundRect 'Screen' 27.6 41.8 8 4.4 0.8 '' 0
        foreach ($i in 0..($BatteryBars - 1)) { if ($BatteryBars -gt 0) { Add-HdRoundRect $BatteryColor (28.1 + 2.4 * $i) 42.4 1.9 3.2 0.5 '' 0 } }
    } elseif ($Progress -ge 0) {
        Add-HdRoundRect 'CyanDim' 27.6 42.4 8.8 3.2 1.4 '' 0
        if ($Progress -gt 0) { Add-HdRoundRect 'Green' 27.6 42.4 (2.2 * $Progress) 3.2 1.4 '' 0 }
    } else {
        Add-HdOval $Light 32 44 2.2 2.2 ''
    }

    # Neck, then the headset (behind the head), then the head with a screen for a face.
    Add-HdRoundRect 'SteelDark' 28 31 8 4 1 'Outline' 0.9
    if ($Headset) {
        Add-HdStroke (Get-HdBezier 16.4 24 16.4 4 47.6 4 47.6 24 20) 'SteelDark' 2.4
        Add-HdRoundRect 'SteelDark' 13.4 17 6 13 2.8 'Outline' 1
        Add-HdRoundRect 'SteelDark' 44.6 17 6 13 2.8 'Outline' 1
        Add-HdRoundRect 'Orange' 15 21 1.8 4.4 0.8 '' 0
        Add-HdRoundRect 'Orange' 47.2 21 1.8 4.4 0.8 '' 0
        if ($Mic) {
            # A microphone boom from the left ear cup to the mouth.
            Add-HdStroke @(16.4, 30, 17.4, 36, 22.6, 36.6) 'SteelDark' 1.5
            Add-HdOval 'Red' 24 36.6 1.5 1.3 ''
        }
    }
    Add-HdRoundRect 'Steel' 18 12 28 20 5.5 'Outline' 1.1
    Add-HdRoundRect 'Screen' 21 15 22 14 3.5 '' 0

    # Antenna with a lamp.
    Add-HdRoundRect 'SteelDark' 30 5 4 7 1 'Outline' 0.9
    Add-HdOval $Lamp 32 4.6 4 3 'Outline' 1

    $ex = 2 * $EyeShift
    $ey = 2 * $EyeDy
    switch ($Eyes) {
        'open' {
            Add-HdRoundRect 'Cyan' (26 + $ex) (18 + $ey) 4 6 1.8 '' 0
            Add-HdRoundRect 'Cyan' (34 + $ex) (18 + $ey) 4 6 1.8 '' 0
        }
        'closed' {
            Add-HdRoundRect 'Cyan' 26 20.2 4 1.8 0.9 '' 0
            Add-HdRoundRect 'Cyan' 34 20.2 4 1.8 0.9 '' 0
        }
        'happy' {
            Add-HdStroke @(25.4, 22.4, 28, 19.2, 30.6, 22.4) 'Cyan' 1.8
            Add-HdStroke @(33.4, 22.4, 36, 19.2, 38.6, 22.4) 'Cyan' 1.8
            Add-HdOval 'Pink' 24.6 25.6 1.7 1.2 ''
            Add-HdOval 'Pink' 39.4 25.6 1.7 1.2 ''
        }
        'tired' {
            Add-HdRoundRect 'CyanDim' 26 19.6 6 4.4 1.8 '' 0
            Add-HdRoundRect 'CyanDim' 34 19.6 6 4.4 1.8 '' 0
            Add-HdRoundRect 'Steel' 25.4 17.6 7.2 2.6 0.8 '' 0
            Add-HdRoundRect 'Steel' 33.4 17.6 7.2 2.6 0.8 '' 0
        }
        'focus' {
            Add-HdRoundRect 'Cyan' 26 20 6 4 1.6 '' 0
            Add-HdRoundRect 'Cyan' 34 20 6 4 1.6 '' 0
            Add-HdRoundRect 'White' 24.4 17.6 9.6 1.8 0.8 '' 0
            Add-HdRoundRect 'White' 33.4 17.6 9.6 1.8 0.8 '' 0
        }
        'surprised' {
            Add-HdRoundRect 'Cyan' 25.6 17.6 6.4 6.4 2.4 '' 0
            Add-HdRoundRect 'Cyan' 34 17.6 6.4 6.4 2.4 '' 0
            Add-HdOval 'Screen' 28.8 20.8 1.2 1.2 ''
            Add-HdOval 'Screen' 37.2 20.8 1.2 1.2 ''
        }
    }

    if ($Spinner -ge 0) {
        $dots = @(@(28, 20), @(36, 20), @(36, 26), @(28, 26))
        for ($i = 0; $i -lt 4; $i++) {
            Add-HdOval $(if ($i -eq $Spinner) { 'Cyan' } else { 'CyanDim' }) $dots[$i][0] $dots[$i][1] 2.2 2.2 ''
        }
    }

    if ($Glasses) {
        Add-HdRing 'White' 27.4 21 4.8 4.6 1.1
        Add-HdRing 'White' 36.6 21 4.8 4.6 1.1
        Add-HdStroke @(32.2, 20.6, 31.8, 20.6) 'White' 1.1
    }

    if ($HoldOverhead) {
        foreach ($grip in $glideGrips) { Add-HdOval 'Orange' $grip[0] $grip[1] 3 3 }
    }
}

# The body of the robot seen from the side, facing left: backpack at the back, torso with the chest panel at the front,
# neck, head in profile with the screen at the front and a single eye, ear cup at the back, antenna. EyeY is the top of
# the eye. With HeadUp the head is turned a quarter turn clockwise about its centre, so the face looks up.
function Add-SideBody {
    param([string]$Lamp = 'Orange', [double]$EyeY = 20, [switch]$HeadUp, [double]$HeadShiftX = 0)

    Add-HdRoundRect 'SteelDark' 36 36 7 14 2.2 'Outline' 1
    Add-HdRoundRect 'Steel' 18 34 18 20 4 'Outline' 1.1
    Add-HdRoundRect 'Screen' 20 40 4 8 1.4 '' 0
    Add-HdRoundRect 'Cyan' 20.4 42 1.6 4 0.7 '' 0
    Add-HdRoundRect 'SteelDark' 24 31 5 4 1 'Outline' 0.9

    $state = $script:g.Save()
    try {
        $script:g.TranslateTransform([single]$HeadShiftX, 0)
        if ($HeadUp) {
            $script:g.TranslateTransform(26, 22)
            $script:g.RotateTransform(90)
            $script:g.TranslateTransform(-26, -22)
        }
        Add-HdRoundRect 'SteelDark' 30 18 4 8 2 'Outline' 0.9
        Add-HdRoundRect 'Steel' 16 12 20 20 5 'Outline' 1.1
        Add-HdRoundRect 'Screen' 18 16 10 12 3 '' 0
        Add-HdRoundRect 'Cyan' 20 $EyeY 4 6 1.8 '' 0
        Add-HdRoundRect 'SteelDark' 24 5 4 7 1 'Outline' 0.9
        Add-HdOval $Lamp 26 4.6 4 3 'Outline' 1
    } finally {
        $script:g.Restore($state)
    }
}

# The robot seen from the side, facing left, walking: the arms swing and the tread rolls under the body. Hand is where
# the near hand is, and the far hand is on the other side of the swing; Roll (0 to 3) moves the light blocks of the tread, a
# little further each frame, so the tread seems to roll backward along the ground.
function Add-RobotSideWalk {
    param([double]$NearHandX, [double]$FarHandX, [int]$Roll, [string]$Lamp = 'Orange')

    # Far arm, behind the body.
    Add-HdLimb @(24, 40, (2 * $FarHandX), 47) 'SteelDark' 3.4
    Add-HdOval 'Orange' (2 * $FarHandX) 48 2.6 2.4

    Add-SideBody -Lamp $Lamp -EyeY 20

    # Tread under the body: a dark belt with light blocks every eight units, shifted by Roll.
    Add-HdRoundRect 'SteelDark' 12 52 24 8 3.6 'Outline' 1.1
    foreach ($n in -1..3) {
        $x1 = [Math]::Max(14 + $n * 8 + 2 * $Roll, 14)
        $x2 = [Math]::Min(14 + $n * 8 + 2 * $Roll + 4, 34)
        if ($x2 -gt $x1) { Add-HdRoundRect 'SteelLight' $x1 54.4 ($x2 - $x1) 3.2 1.2 '' 0 }
    }

    # The near arm, in front of the tread.
    Add-HdLimb @(24, 40, (2 * $NearHandX), 47) 'Steel' 3.4
    Add-HdOval 'Orange' (2 * $NearHandX) 48 2.6 2.4
}

# The robot climbing a wall: the side walk turned a quarter turn clockwise (Save-Frame -Tilt 90), so the tread runs up the left
# edge of the frame, which is the wall, and the face looks up. The arms reach up past the head in turn, hand over hand: NearHand
# and FarHand are where the hands are before the turn (smaller x is higher up the wall, smaller y is further from it). Roll (0
# to 3) moves the light blocks of the tread, so it seems to roll up the wall.
function Add-RobotClimb {
    param([double[]]$NearHand, [double[]]$FarHand, [int]$Roll, [string]$Lamp = 'Orange')

    # Far arm, behind the body.
    Add-HdLimb @(24, 40, $FarHand[0], $FarHand[1]) 'SteelDark' 3.4
    Add-HdOval 'Orange' $FarHand[0] $FarHand[1] 2.6 2.4

    Add-SideBody -Lamp $Lamp -EyeY 20

    # The tread against the wall, its light blocks shifted by Roll the other way round from the walk.
    Add-HdRoundRect 'SteelDark' 12 52 24 8 3.6 'Outline' 1.1
    foreach ($n in -1..3) {
        $x1 = [Math]::Max(14 + $n * 8 - 2 * $Roll + 6, 14)
        $x2 = [Math]::Min(14 + $n * 8 - 2 * $Roll + 10, 34)
        if ($x2 -gt $x1) { Add-HdRoundRect 'SteelLight' $x1 54.4 ($x2 - $x1) 3.2 1.2 '' 0 }
    }

    # The near arm, in front of the body.
    Add-HdLimb @(24, 40, $NearHand[0], $NearHand[1]) 'Steel' 3.4
    Add-HdOval 'Orange' $NearHand[0] $NearHand[1] 2.6 2.4
}

# Idle: stands and blinks now and then.
Save-Frame 'idle_0.png' { Add-Robot }
Save-Frame 'idle_1.png' { Add-Robot -Lamp SteelDark }
Save-Frame 'idle_2.png' { Add-Robot }
Save-Frame 'idle_3.png' { Add-Robot -Eyes closed -Lamp SteelDark }

# Walk: seen from the side, rolls along on its tread with swinging arms and a little bounce. Drawn facing left and
# mirrored, as the art faces right.
Save-Frame 'walk_0.png' -Mirror { Add-RobotSideWalk -NearHandX 7 -FarHandX 14 -Roll 0 }
Save-Frame 'walk_1.png' -Mirror { Add-RobotSideWalk -NearHandX 10 -FarHandX 11 -Roll 1 -Lamp SteelDark }
Save-Frame 'walk_2.png' -Mirror { Add-RobotSideWalk -NearHandX 14 -FarHandX 7 -Roll 2 }
Save-Frame 'walk_3.png' -Mirror { Add-RobotSideWalk -NearHandX 11 -FarHandX 10 -Roll 3 -Lamp SteelDark }

# Sleep: standby mode, dim lights, floating z.
Save-Frame 'sleep_0.png' -Dy 1 { Add-Robot -Eyes closed -Lamp SteelDark -Light CyanDim }
Save-Frame 'sleep_1.png' -Dy 1 { Add-Robot -Eyes closed -Lamp SteelDark -Light CyanDim; Add-HdLabel 'z' 'White' 49 9 'Outline' 8; Add-HdLabel 'Z' 'White' 53 1 'Outline' 9 }

# Drag: lifted by the antenna, arms up, treads dangling.
Save-Frame 'drag_0.png' -Air { Add-Robot -Eyes surprised -ArmsUp -Feet 1 }
Save-Frame 'drag_1.png' -Air { Add-Robot -Eyes surprised -ArmsUp -Feet -1 -Lamp SteelDark }

# Glide: floating down under an umbrella or parachute, both hands up beside the head, treads dangling. Drawn without the canopy, which
# the program puts over the head with its shaft or strings down to the hands.
Save-Frame 'glide_0.png' -Air { Add-Robot -Eyes surprised -HoldOverhead -Feet 1 }
Save-Frame 'glide_1.png' -Air { Add-Robot -Eyes surprised -HoldOverhead -Feet -1 -Lamp SteelDark }

# Tired: overheating, low battery eyes, steam.
Save-Frame 'tired_0.png' -Dy 1 { Add-Robot -Eyes tired -Lamp Red -Light CyanDim; Add-Steam 0 }
Save-Frame 'tired_1.png' -Dy 1 { Add-Robot -Eyes tired -Lamp SteelDark -Light CyanDim; Add-Steam 1 }

# Alert: jumps with a flashing red lamp and an exclamation mark.
Save-Frame 'alert_0.png' -Dy -2 { Add-Robot -Eyes surprised -ArmsUp -Lamp Red -Light Red -Feet 1; Add-HdLabel '!' 'Red' 52 3 'Outline' 10 }
Save-Frame 'alert_1.png' { Add-Robot -Eyes surprised -ArmsUp -Lamp Orange -Light Red -Feet -1; Add-HdLabel '!' 'Red' 52 3 'Outline' 10 }

# Focus: headset on, eyes narrowed, quietly concentrating.
Save-Frame 'focus_0.png' { Add-Robot -Eyes focus -Headset }
Save-Frame 'focus_1.png' { Add-Robot -Eyes focus -Headset -Lamp SteelDark }

# Happy: bounces with smiling eyes and a heart.
Save-Frame 'happy_0.png' -Dy -2 { Add-Robot -Eyes happy -ArmsUp -Feet 1; Add-HdHeart 'Pink' 55 8 3.4 }
Save-Frame 'happy_1.png' { Add-Robot -Eyes happy -ArmsUp -Feet -1 }

# Building: a loading ring on the face and a filling bar on the chest, while the lamp blinks.
foreach ($f in 0..3) {
    $frame = $f
    $draw = { Add-Robot -Eyes none -Spinner $frame -Progress ($frame + 1) -Lamp $(if ($frame % 2 -eq 0) { 'Orange' } else { 'SteelDark' }) }.GetNewClosure()
    Save-Frame "building_$f.png" -Tag 'BLD' -TagColor Orange $draw
}

# BuildDone: arms up, smiling, a green check mark; the second frame adds a sparkle.
Save-Frame 'builddone_0.png' -Tag 'OK!' -TagColor Green -Dy -2 { Add-Robot -Eyes happy -ArmsUp -Feet 1; Add-HdCheck 'Green' 54 9 3.6 2.4 }
Save-Frame 'builddone_1.png' -Tag 'OK!' -TagColor Green { Add-Robot -Eyes happy -ArmsUp -Feet -1; Add-HdCheck 'Green' 54 9 3.6 2.4; Add-HdSparkle 'White' 9 12 3.2 }

# GitPush: holds a parcel overhead, throws it up, and it flies off.
Save-Frame 'gitpush_0.png' -Tag 'GIT' -TagColor Cyan { Add-Robot -Eyes open -ArmsUp; Add-Box 43 11 10 }
Save-Frame 'gitpush_1.png' -Tag 'GIT' -TagColor Cyan -Dy -1 { Add-Robot -Eyes happy -ArmsUp -Feet 1; Add-Box 50 3 10 }
Save-Frame 'gitpush_2.png' -Tag 'GIT' -TagColor Cyan { Add-Robot -Eyes happy; Add-Box 56 0.5 6; Add-HdOval 'Gray' 51.5 6 2 0.9 ''; Add-HdOval 'Gray' 53.5 9.5 2 0.9 '' }

# Reading: round glasses, eyes scanning along, a magnifying glass held up.
foreach ($f in 0..3) {
    $shift = @(-1, 0, 1, 0)[$f]
    $bob = @(0, 2, 0, 2)[$f]
    $draw = { Add-Robot -Eyes open -ArmsUp -Glasses -EyeShift $shift; Add-Magnifier 55 (19 + $bob) }.GetNewClosure()
    Save-Frame "reading_$f.png" -Tag 'WEB' -TagColor Cyan $draw
}

# Welcome: waves hello with one arm and hearts floating up.
foreach ($f in 0..3) {
    $dx = @(0, 2, 0, -2)[$f]
    $hearts = ($f % 2 -eq 0)
    $draw = { Add-Robot -Eyes happy -RightArmUp -RightArmDx $dx; if ($hearts) { Add-HdHeart 'Pink' 56 8 3.4 } }.GetNewClosure()
    Save-Frame "welcome_$f.png" -Tag 'HI!' -TagColor Pink $draw
}

# Meeting: headset with a microphone, listening quietly; the lamp glows red and dots show it is listening.
foreach ($f in 0..3) {
    $count = $f
    $draw = {
        Add-Robot -Eyes open -Headset -Mic -Lamp $(if ($count % 2 -eq 0) { 'Red' } else { 'SteelDark' })
        foreach ($i in 0..2) { if ($i -lt $count) { Add-HdOval 'White' (50 + 4 * $i) 8 1.2 1.2 '' } }
    }.GetNewClosure()
    Save-Frame "meeting_$f.png" -Tag 'MTG' -TagColor Red $draw
}

# Chatting: a speech bubble beside the head whose dots fill up one by one, the robot nodding along happily.
foreach ($f in 0..3) {
    $dots = $f
    $nod = @(0, 1, 0, 1)[$f]
    $draw = {
        Add-Robot -Eyes happy
        Add-HdRoundRect 'White' 44 3 18 14 5 'Outline' 1.1
        Add-HdPoly 'White' @(46, 15.6, 51.5, 15.6, 45, 20.4) 'Outline' 1.1
        Add-HdRoundRect 'White' 46.6 14.6 4.4 2 0.5 '' 0
        foreach ($i in 0..2) { if ($i -lt $dots) { Add-HdOval 'Cyan' (48.8 + 4.2 * $i) 10 1.3 1.3 '' } }
    }.GetNewClosure()
    Save-Frame "chatting_$f.png" -Tag 'CHT' -TagColor Cyan -Dy $nod $draw
}

# LowBattery: slouching, heavy eyes, an almost empty battery on the chest that blinks red.
Save-Frame 'lowbattery_0.png' -Tag 'LOW' -TagColor Red -Dy 1 { Add-Robot -Eyes tired -Lamp Red -Light CyanDim -Battery -BatteryBars 1 -BatteryColor Red }
Save-Frame 'lowbattery_1.png' -Tag 'LOW' -TagColor Red -Dy 1 { Add-Robot -Eyes tired -Lamp SteelDark -Light CyanDim -Battery -BatteryBars 0 -BatteryColor Red }

# Charging: plugged into a wall socket, the battery fills up and a bolt flashes.
foreach ($f in 0..3) {
    $bars = $f
    $bolt = ($f % 2 -eq 0)
    $draw = {
        Add-HdStroke @(45, 43, 54, 45, 58, 43) 'Outline' 2.6
        Add-HdStroke @(45, 43, 54, 45, 58, 43) 'Gray' 1.2
        Add-HdRoundRect 'Gray' 42 40.4 4.6 5.6 1.2 'Outline' 0.9
        Add-HdRoundRect 'White' 58 37.6 6.4 10.4 1.6 'Outline' 1
        Add-HdRoundRect 'Outline' 59.6 41.6 1.6 2 0.4 '' 0
        Add-Robot -Eyes happy -Battery -BatteryBars $bars -BatteryColor Green
        if ($bolt) { Add-Bolt 47 3 }
    }.GetNewClosure()
    Save-Frame "charging_$f.png" -Tag 'CHG' -TagColor Green $draw
}

# Meal: holds a steaming bowl of rice in one hand and eats with chopsticks.
foreach ($f in 0..3) {
    $steamUp = ($f % 2 -eq 0)
    $stickY = @(16, 12, 16, 14)[$f]
    $eyes = if ($f -eq 3) { 'closed' } else { 'happy' }
    $draw = {
        Add-Robot -Eyes $eyes -RightArmUp
        # The bowl: rice heaped over a brown bowl, steam above it.
        Add-HdOval 'White' 56 27.4 5.6 3.2 ''
        Add-HdShape 'Brown' @(50, 28, 50.6, 33, 53, 35, 56, 35, 59, 35, 61.4, 33, 62, 28) 'Outline' 1
        if ($steamUp) {
            Add-HdStroke @(54, 22, 53, 20, 54.4, 18) 'White' 1.2
            Add-HdStroke @(59, 21, 60, 19, 59, 17) 'White' 1.2
        } else {
            Add-HdStroke @(55, 21, 56, 19, 55, 17) 'White' 1.2
            Add-HdStroke @(59, 22, 58, 20, 59.4, 18) 'White' 1.2
        }
        # A pair of chopsticks dipping into the rice.
        Add-HdStroke @(55, $stickY, 56.6, ($stickY + 11)) 'Wood' 1.2
        Add-HdStroke @(58.6, ($stickY - 2), 57.4, ($stickY + 9)) 'Wood' 1.2
    }.GetNewClosure()
    Save-Frame "meal_$f.png" -Tag 'EAT' -TagColor Orange $draw
}

# ClockOut: smiling arms-up robot beside a big clock that rings at six.
foreach ($f in 0..3) {
    $shake = @(0, 2, 0, -2)[$f]
    $ringing = ($f % 2 -eq 0)
    $draw = {
        Add-Robot -Eyes happy -ArmsUp -Feet ($f % 2)
        $cx = 55 + $shake
        Add-HdOval 'White' $cx 12 6.8 6.8 'Outline' 1.2
        Add-HdStroke @($cx, 12, $cx, 7.4) 'Outline' 1.2
        Add-HdStroke @($cx, 12, ($cx + 3), 13.6) 'Red' 1.2
        Add-HdOval 'Outline' $cx 12 0.9 0.9 ''
        if ($ringing) {
            Add-HdStroke @(($cx - 7), 3.4, ($cx - 9), 5.8) 'Orange' 1.2
            Add-HdStroke @(($cx + 7), 3.4, ($cx + 9), 5.8) 'Orange' 1.2
        }
    }.GetNewClosure()
    Save-Frame "clockout_$f.png" -Tag 'BYE' -TagColor Orange $draw
}

# Chime: rings a small golden bell on the hour, with sound waves beside it.
foreach ($f in 0..3) {
    $swing = @(-2, 0, 2, 0)[$f]
    $waves = ($f % 2 -eq 0)
    $dy = if ($f % 2 -eq 1) { -1 } else { 0 }
    $draw = {
        Add-Robot -Eyes open -RightArmUp
        Add-HdShape 'Orange' @(53, 28, 53.4, 20, 56, 18, 58.6, 20, 59, 28, 61, 29.4, 51, 29.4, 53, 28) 'Outline' 1
        Add-HdOval 'Brown' (56 + $swing) 30.6 1.4 1.4 ''
        Add-HdStroke @(55, 20.6, 54.4, 25) 'White' 0.9
        if ($waves) {
            Add-HdStroke @(49.4, 20, 48.2, 23) 'White' 1.1
            Add-HdStroke @(62.6, 20, 63.8, 23) 'White' 1.1
        } else {
            Add-HdStroke @(49.4, 16, 48.2, 19) 'White' 1.1
            Add-HdStroke @(62.6, 16, 63.8, 19) 'White' 1.1
        }
    }.GetNewClosure()
    Save-Frame "chime_$f.png" -Tag 'BEL' -TagColor Orange -Dy $dy $draw
}

# Festive: a striped party hat on the head, confetti falling around the cheering robot.
$confetti = @(
    @(@(6, 6, 'Orange'), @(56, 12, 'Green'), @(10, 28, 'Pink'), @(54, 30, 'Cyan')),
    @(@(8, 12, 'Pink'), @(54, 6, 'Orange'), @(6, 34, 'Green'), @(56, 24, 'White')),
    @(@(4, 18, 'Green'), @(58, 18, 'Pink'), @(10, 6, 'Cyan'), @(52, 34, 'Orange')),
    @(@(6, 24, 'Cyan'), @(56, 6, 'White'), @(8, 40, 'Orange'), @(54, 16, 'Pink'))
)
foreach ($f in 0..3) {
    $bits = $confetti[$f]
    $draw = {
        Add-Robot -Eyes happy -ArmsUp -Feet ($f % 2)
        # The hat covers the antenna: a striped cone with a pompom on top.
        Add-HdPoly 'Pink' @(32, 1.6, 42, 12.6, 22, 12.6) 'Outline' 1
        Add-HdPoly 'Orange' @(29.4, 5.4, 34.6, 5.4, 37.4, 8.6, 26.6, 8.6) '' 0
        Add-HdOval 'White' 32 1.8 2 2 'Outline' 0.8
        foreach ($bit in $bits) { Add-HdOval $bit[2] $bit[0] $bit[1] 1.4 1 '' 0 35 }
    }.GetNewClosure()
    Save-Frame "festive_$f.png" -Tag 'YAY' -TagColor Pink $draw -Dy $(if ($f % 2 -eq 0) { 0 } else { 1 })
}

# Climb: the robot rolls up the left edge of the screen, which is the wall, with its tread against it, reaching up hand over
# hand. Mirrored for the right edge. ClimbDown is the same frames flipped vertically.
$climbPoses = @(
    @(@(10, 40), @(22, 47), 0, 'Orange'),
    @(@(14, 44), @(17, 44), 1, 'SteelDark'),
    @(@(22, 47), @(10, 40), 2, 'Orange'),
    @(@(17, 44), @(14, 44), 3, 'SteelDark')
)
foreach ($i in 0..3) {
    $pose = $climbPoses[$i]
    $draw = { Add-RobotClimb -NearHand $pose[0] -FarHand $pose[1] -Roll $pose[2] -Lamp $pose[3] }.GetNewClosure()
    Save-Frame "climb_$i.png" -Air -Dy 3 -Tilt 90 $draw
    # ClimbDown: the same climb upside down (flipped vertically, as for the other pets), so the robot goes down the wall head first.
    Save-Frame "climbdown_$i.png" -Air -Dy 3 -Tilt 90 -Flip $draw
}

# Ceiling: walks along the top of the screen upside down, in the same side view as walking on the ground.
Save-Frame 'ceiling_0.png' -Flip -Mirror { Add-RobotSideWalk -NearHandX 7 -FarHandX 14 -Roll 0 }
Save-Frame 'ceiling_1.png' -Flip -Mirror { Add-RobotSideWalk -NearHandX 10 -FarHandX 11 -Roll 1 -Lamp SteelDark }
Save-Frame 'ceiling_2.png' -Flip -Mirror { Add-RobotSideWalk -NearHandX 14 -FarHandX 7 -Roll 2 }
Save-Frame 'ceiling_3.png' -Flip -Mirror { Add-RobotSideWalk -NearHandX 11 -FarHandX 10 -Roll 3 -Lamp SteelDark }

# Idle looks: the eyes follow the mouse pointer.
Save-Frame 'idle_left.png'  { Add-Robot -Eyes open -EyeShift -1.5 }
Save-Frame 'idle_right.png' { Add-Robot -Eyes open -EyeShift 1.5 }
Save-Frame 'idle_up.png'    { Add-Robot -Eyes open -EyeDy -1 }
Save-Frame 'idle_down.png'  { Add-Robot -Eyes open -EyeDy 1 }

# Coding: the robot sits in a chair at a desk, typing at a keyboard. The scene is a side view, so the monitor in front of it is
# seen edge-on (a thin slab whose lit face points at the robot) and nothing on its screen can be seen.
$codeGlyphs = @('{', '{}', '{;}', '{}')

# Night: heavy eyes, a moon and stars, a steaming coffee and a sleepy 'zZ' now and then.
function Add-CodingScene([int]$Frame, [switch]$Night) {
    if ($Night) {
        Add-HdMoon 'White' 36 7 4
        Add-HdSparkle 'White' 27 7 2
        Add-HdSparkle 'White' 42 10 1.8
    }

    # Chair.
    Add-HdRoundRect 'SteelDark' 2 30 4 20 1.6 'Outline' 0.9
    Add-HdRoundRect 'SteelDark' 2 48 22 4 1.6 'Outline' 0.9
    Add-HdRoundRect 'SteelDark' 12 52 4 6 1 'Outline' 0.9
    Add-HdRoundRect 'SteelDark' 6 58 16 2 1 'Outline' 0.7

    # Desk.
    Add-HdRoundRect 'WoodDark' 58 46 4 14 1 'Outline' 0.9
    Add-HdRoundRect 'Wood' 22 40 42 4 1.4 'Outline' 1
    Add-HdRoundRect 'WoodDark' 24 43.6 38 1.8 0.8 '' 0

    # Monitor seen from the side: a thin slab on a neck and a flat foot. Its lit face (the left edge) glows in step with the typing.
    Add-HdRoundRect 'SteelDark' 44 38 14 2.4 1 'Outline' 0.9
    Add-HdRoundRect 'SteelDark' 51 30 3 8.4 0.8 'Outline' 0.9
    Add-HdRoundRect 'SteelDark' 50 12 6 20 2 'Outline' 1.1
    Add-HdRoundRect $(if ($Frame % 2 -eq 0) { 'Cyan' } else { 'CyanDim' }) 50.4 14.2 1.3 15.6 0.6 '' 0
    if (-not $Night) { Add-HdLabel $codeGlyphs[$Frame] 'White' 45 3 }

    # Keyboard and a mug of coffee.
    Add-HdRoundRect 'Gray' 24 37.6 12 2.4 1 'Outline' 0.7
    Add-HdRoundRect 'White' $(if ($Frame % 2 -eq 1) { 28.2 } else { 32.2 }) 37.8 1.8 1 0.4 '' 0
    Add-HdRoundRect 'Brown' 38 35.4 5 4.6 1.2 'Outline' 0.8
    Add-HdStroke @((40 + 1.4 * ($Frame % 2)), 33.6, (39.4 + 1.4 * ($Frame % 2)), 31.6) 'White' 1

    # The robot: legs under the desk, torso, head looking at the screen.
    Add-HdRoundRect 'SteelDark' 16 44 17 6 2.6 'Outline' 1
    Add-HdRoundRect 'SteelDark' 28 48 6 11 2.4 'Outline' 1
    Add-HdRoundRect 'SteelDark' 28 56 10 4 1.8 'Outline' 1
    Add-HdRoundRect 'Steel' 6 30 16 16 3.5 'Outline' 1.1
    Add-HdRoundRect 'Screen' 10 34 8 6 1.6 '' 0
    Add-HdRoundRect $(if ($Frame % 2 -eq 0) { 'Cyan' } else { 'CyanDim' }) 12 36.2 4 2 0.9 '' 0
    Add-HdRoundRect 'SteelDark' 12 27.4 6 3.2 1 'Outline' 0.8
    Add-HdRoundRect 'Steel' 6 10 22 18 5 'Outline' 1.1
    Add-HdRoundRect 'Screen' 10 13.4 14 11 3.2 '' 0
    Add-HdRoundRect 'SteelDark' 14 5.6 4 5 1 'Outline' 0.8
    Add-HdOval $(if ($Frame % 2 -eq 0) { 'Orange' } else { 'SteelDark' }) 16 4.6 4 2.8 'Outline' 1
    if ($Night) {
        # Half-lidded eyes with dark circles under them; the last frame nods off.
        if ($Frame -eq 3) {
            Add-HdRoundRect 'CyanDim' 15.4 18.2 4 1.8 0.8 '' 0
            Add-HdRoundRect 'CyanDim' 21.4 18.2 4 1.8 0.8 '' 0
        } else {
            Add-HdRoundRect 'CyanDim' 15.4 16.4 4 3.6 1.4 '' 0
            Add-HdRoundRect 'CyanDim' 21.4 16.4 4 3.6 1.4 '' 0
            Add-HdRoundRect 'Steel' 14.8 15.4 5.2 2 0.6 '' 0
            Add-HdRoundRect 'Steel' 20.8 15.4 5.2 2 0.6 '' 0
        }
        Add-HdRoundRect 'SteelDark' 15.4 20.8 4 1.2 0.5 '' 0
        Add-HdRoundRect 'SteelDark' 21.4 20.8 4 1.2 0.5 '' 0
    } elseif ($Frame -eq 3) {
        Add-HdRoundRect 'Cyan' 16.4 18.2 4 1.8 0.8 '' 0
        Add-HdRoundRect 'Cyan' 22.4 18.2 4 1.8 0.8 '' 0
    } else {
        # Looking right, at the monitor.
        Add-HdRoundRect 'Cyan' 16.4 16.2 4 4 1.6 '' 0
        Add-HdRoundRect 'Cyan' 22.4 16.2 4 4 1.6 '' 0
    }

    # Arms reaching the keyboard; the hands take turns pressing keys.
    # The far arm comes out from behind the torso (drawn first, in the dark steel), the near one from the shoulder at its front edge.
    $a = $Frame % 2
    Add-HdLimb @(20, 32.4, 26, 35.4, 33, (38.2 - 2 * $a)) 'SteelDark' 2.6
    Add-HdOval 'SteelLight' 33 (38.2 - 2 * $a) 1.7 1.5 ''
    Add-HdLimb @(21, 32.6, 25, 35.6, 29, (36.6 + 2 * $a)) 'Steel' 3.2
    Add-HdOval 'SteelLight' 29 (36.6 + 2 * $a) 1.7 1.5 ''

    if ($Night) {
        # A second wisp of steam over the coffee.
        Add-HdStroke @((38.6 + 1.2 * ($Frame % 2)), 32.6, 38, 30.4) 'White' 1
        if ($Frame % 2 -eq 1) { Add-HdLabel 'zZ' 'White' 42 0 'Outline' 8 }
    }
}

foreach ($f in 0..3) {
    $scene = { Add-CodingScene $f }.GetNewClosure()
    Save-Frame "coding_$f.png" -Tag 'DEV' -TagColor Green $scene
    $night = { Add-CodingScene $f -Night }.GetNewClosure()
    Save-Frame "nightcoding_$f.png" -Tag '3AM' -TagColor Orange $night
}

$manifest = @"
{
  "name": "robot",
  "names": { "en": "Robot", "zh": "$PetNameZh", "zh-Hant": "$PetNameZhHant" },
  "frameSize": [64, 64],
  "states": {
    "Idle":   { "fps": 2, "frames": ["idle_0.png", "idle_1.png", "idle_2.png", "idle_3.png"] },
    "Walk":   { "fps": 8, "frames": ["walk_0.png", "walk_1.png", "walk_2.png", "walk_3.png"] },
    "Sleep":  { "fps": 1, "frames": ["sleep_0.png", "sleep_1.png"] },
    "Drag":   { "fps": 6, "frames": ["drag_0.png", "drag_1.png"] },
    "Glide":   { "fps": 4, "frames": ["glide_0.png", "glide_1.png"], "grips": $(Format-GlideGrips $glideGrips) },
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
    "Chatting":    { "fps": 3, "frames": ["chatting_0.png", "chatting_1.png", "chatting_2.png", "chatting_3.png"] },
    "LowBattery":  { "fps": 2, "frames": ["lowbattery_0.png", "lowbattery_1.png"] },
    "Charging":    { "fps": 3, "frames": ["charging_0.png", "charging_1.png", "charging_2.png", "charging_3.png"] },
    "Meal":        { "fps": 3, "frames": ["meal_0.png", "meal_1.png", "meal_2.png", "meal_3.png"] },
    "ClockOut":    { "fps": 4, "frames": ["clockout_0.png", "clockout_1.png", "clockout_2.png", "clockout_3.png"] },
    "Chime":       { "fps": 4, "frames": ["chime_0.png", "chime_1.png", "chime_2.png", "chime_3.png"] },
    "Festive":     { "fps": 4, "frames": ["festive_0.png", "festive_1.png", "festive_2.png", "festive_3.png"] },
    "Climb":       { "fps": 6, "frames": ["climb_0.png", "climb_1.png", "climb_2.png", "climb_3.png"] },
    "ClimbDown":   { "fps": 6, "frames": ["climbdown_0.png", "climbdown_1.png", "climbdown_2.png", "climbdown_3.png"] },
    "Ceiling":     { "fps": 6, "frames": ["ceiling_0.png", "ceiling_1.png", "ceiling_2.png", "ceiling_3.png"] },
    "Idle.Left":   { "fps": 1, "frames": ["idle_left.png"] },
    "Idle.Right":  { "fps": 1, "frames": ["idle_right.png"] },
    "Idle.Up":     { "fps": 1, "frames": ["idle_up.png"] },
    "Idle.Down":   { "fps": 1, "frames": ["idle_down.png"] }
  }
}
"@

Write-PetManifest (Join-Path $outDir 'manifest.json') $manifest

# The tray icon: the antenna and the head (drawing units: x, y, width, height).
Save-HdIcon (Join-Path $outDir 'idle_0.png') (Join-Path $outDir 'icon.png') @(15, 5, 34, 34)

# Pack the single frames into the atlas the program reads.
. (Join-Path $PSScriptRoot 'PackPetAtlas.ps1')
Invoke-PetAtlasPacking $outDir

Write-Host "$PetName pet written to $outDir"
