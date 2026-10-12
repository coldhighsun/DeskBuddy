# Generates the pixel-art cat skin (no third-party artwork). Art faces right.
# Usage: powershell -NoProfile -File tools/GenerateCatSkin.ps1
. (Join-Path $PSScriptRoot 'SkinDrawing.ps1')
# The cat's limbs have a core two art pixels thick.
$script:LimbCore = 2

$outDir = Join-Path $PSScriptRoot '..\skins\cat'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Get-ChildItem $outDir -Filter *.png | Remove-Item

$pal = @{
    Outline    = New-Brush 56 34 30
    Orange     = New-Brush 240 160 70
    OrangeDark = New-Brush 200 105 45
    Cream      = New-Brush 255 238 210
    Whisker    = New-Brush 240 230 215
    Pink       = New-Brush 255 140 170
    White      = New-Brush 240 244 250
    Black      = New-Brush 24 22 28
    EyeG       = New-Brush 110 210 120
    Blue       = New-Brush 110 170 230
    Red        = New-Brush 255 70 70
    Yellow     = New-Brush 255 210 70
}
$textFont = New-Object System.Drawing.Font 'Consolas', 16, ([System.Drawing.FontStyle]::Bold)

# Renders one frame by running $Draw with a fresh canvas, then saves it. The cat's feet end two art pixels above the
# bottom of the frame, so a grounded frame is lowered by two to stand on the bottom edge; a negative Dy is a jump. Air is
# for frames in which the cat is not on the ground. Flip turns the picture upside down (hanging from the top of the
# screen); Mirror turns it left to right (a side view drawn facing left that has to face right).
function Save-Frame([string]$Name, [scriptblock]$Draw, [int]$Dy = 0, [switch]$Flip, [switch]$Mirror, [switch]$Air) {
    $bmp = New-Canvas
    $script:dy = if ($Air) { $Dy } else { [Math]::Min($Dy, 0) + 2 }
    & $Draw
    Save-Canvas $bmp (Join-Path $outDir $Name) -Flip:$Flip -Mirror:$Mirror
}

# A small plus-shaped star centered on the given art pixel.
function Add-Star([string]$Color, [int]$X, [int]$Y) {
    Px $Color $X ($Y - 1) 1 3; Px $Color ($X - 1) $Y 3 1
}

# Draws a picture given as rows of characters, its top-left corner at the given art pixel: X outline, o orange, d dark
# orange, c cream, p pink, g green, k black, w whisker, anything else is left clear.
function Add-Sprite([int]$X, [int]$Y, [string[]]$Rows) {
    $colors = @{ X = 'Outline'; o = 'Orange'; d = 'OrangeDark'; c = 'Cream'; p = 'Pink'; g = 'EyeG'; k = 'Black'; w = 'Whisker' }
    for ($r = 0; $r -lt $Rows.Count; $r++) {
        for ($c = 0; $c -lt $Rows[$r].Length; $c++) {
            $color = $colors["$($Rows[$r][$c])"]
            if ($color) { Px $color ($X + $c) ($Y + $r) }
        }
    }
}

# A pointed ear, an upright isosceles triangle 5 wide and 4 tall at the given top-left corner.
function Add-Ear([int]$X, [int]$Y) {
    Add-Sprite $X $Y @('..X..', '.XoX.', 'XopoX', 'XXXXX')
}

# The tail, a curve from the base to a tip that sways with Phase (0-3).
function Add-Tail([int]$Phase) {
    $tipX = @(27, 28, 29, 28)[$Phase]
    $tipY = @(16, 17, 18, 17)[$Phase]
    Add-Limb 22 27 27 24 Outline Orange
    Add-Limb 27 24 $tipX $tipY Outline Orange
    Px OrangeDark $tipX $tipY 2 2
}

# The front face on a head whose outline spans x 8-23, y 6-16. Eyes: open | closed | happy | tired | surprised | dizzy.
# EyeShift moves the pupils sideways and EyeDy up or down.
function Add-Face([string]$Eyes, [int]$EyeShift, [int]$EyeDy) {
    Px OrangeDark 15 7 2 1; Px OrangeDark 13 8 1 1; Px OrangeDark 18 8 1 1
    Px Cream 13 13 6 3
    Px Whisker 5 12 3 1; Px Whisker 5 14 3 1; Px Whisker 24 12 3 1; Px Whisker 24 14 3 1
    Px Pink 15 13 2 1
    Px Outline 15 14 2 1; Px Outline 14 15 1 1; Px Outline 17 15 1 1
    $y = 9 + $EyeDy
    switch ($Eyes) {
        'open' {
            Px EyeG 11 $y 3 3; Px EyeG 18 $y 3 3
            Px Black (12 + $EyeShift) $y 1 3; Px Black (19 + $EyeShift) $y 1 3
        }
        'closed' { Px Outline 11 10 3 1; Px Outline 18 10 3 1 }
        'happy' {
            foreach ($x0 in 11, 18) { Px Outline $x0 10; Px Outline ($x0 + 1) 9; Px Outline ($x0 + 2) 10 }
            Px Pink 10 12 2 1; Px Pink 20 12 2 1
        }
        'tired' {
            Px EyeG 11 10 3 2; Px EyeG 18 10 3 2; Px Black 12 10 1 2; Px Black 19 10 1 2
            Px OrangeDark 11 9 3 1; Px OrangeDark 18 9 3 1
        }
        'surprised' {
            Px White 11 8 3 4; Px White 18 8 3 4; Px Black 12 9 1 2; Px Black 19 9 1 2
        }
        'dizzy' {
            foreach ($x0 in 11, 18) { Px Outline $x0 9; Px Outline ($x0 + 2) 9; Px Outline ($x0 + 1) 10; Px Outline $x0 11; Px Outline ($x0 + 2) 11 }
        }
    }
}

# The sitting cat seen from the front. ArmsUp raises both front paws, RightArmUp only the right one (RightArmDx sways
# it, for a wave), LeftArmReach stretches the left one out to the edge of the frame to hold on to the screen, and
# HoldOverhead raises both behind the head to the handle of an umbrella or parachute above it.
function Add-Cat {
    param(
        [string]$Eyes = 'open', [int]$Tail = 0, [int]$EyeShift = 0, [int]$EyeDy = 0,
        [switch]$ArmsUp, [switch]$RightArmUp, [int]$RightArmDx = 0, [switch]$LeftArmReach, [int]$ReachY = 13,
        [switch]$HoldOverhead
    )

    Add-Tail $Tail

    # Both arms go up outside the head and in again behind it, so the paws meet over the head on either side of the handle.
    if ($HoldOverhead) {
        Add-Limb 10 20 4 12 Outline Orange; Add-Limb 4 12 13 3 Outline Orange
        Add-Limb 21 20 27 12 Outline Orange; Add-Limb 27 12 18 3 Outline Orange
    }

    # Haunches and body with a cream belly and stripes.
    Px Outline 8 24 16 6; Px Orange 9 25 14 4
    Px Outline 9 17 14 8; Px Orange 10 18 12 7
    Px Cream 13 20 6 9
    Px OrangeDark 10 20 2 1; Px OrangeDark 10 22 2 1; Px OrangeDark 20 20 2 1; Px OrangeDark 20 22 2 1

    # Front legs, unless raised.
    if (-not $ArmsUp -and -not $LeftArmReach) { Px Outline 11 23 4 7; Px Cream 12 24 2 5 }
    if (-not $ArmsUp -and -not $RightArmUp) { Px Outline 17 23 4 7; Px Cream 18 24 2 5 }

    # Head with ears.
    Add-Ear 8 2; Add-Ear 19 2
    Px Outline 8 6 16 11; Px Orange 9 7 14 9
    Add-Face $Eyes $EyeShift $EyeDy

    # Raised or reaching paws, on top of everything.
    if ($ArmsUp -or $RightArmUp) { Add-Limb 21 20 (26 + $RightArmDx) 13 Outline Orange; Px Cream (26 + $RightArmDx) 13 2 2 }
    if ($ArmsUp) { Add-Limb 10 20 5 13 Outline Orange; Px Cream 5 13 2 2 }
    if ($LeftArmReach) { Add-Limb 10 19 1 $ReachY Outline Orange; Px Cream 1 $ReachY 2 2 }
    if ($HoldOverhead) { Px Cream 13 2 2 3; Px Cream 17 2 2 3 }
}

# The cat lying down in a loaf, head on its paws, tail across the front. Eyes: closed | tired.
function Add-CatLie([string]$Eyes = 'closed') {
    Px Outline 5 19 22 11; Px Orange 6 20 20 9
    Px OrangeDark 14 20 1 2; Px OrangeDark 17 20 1 2; Px OrangeDark 20 20 1 2; Px OrangeDark 23 20 1 2
    Px Outline 18 26 11 4; Px Orange 19 27 9 2; Px OrangeDark 26 27 2 2
    Add-Ear 4 10; Add-Ear 13 10
    Px Outline 4 14 14 11; Px Orange 5 15 12 9
    Px Cream 9 20 4 3; Px Pink 10 20 2 1
    Px Whisker 1 19 3 1; Px Whisker 1 21 3 1; Px Whisker 18 19 3 1; Px Whisker 18 21 3 1
    if ($Eyes -eq 'tired') {
        Px EyeG 6 18 3 1; Px EyeG 13 18 3 1; Px Outline 6 17 3 1; Px Outline 13 17 3 1
        Px Blue 19 13 1 2
    } else {
        Px Outline 6 18 3 1; Px Outline 13 18 3 1
    }
}

# The cat seen from the side, facing left, walking: D (-3 to 3) swings the legs, TipY is the height of the tail's tip.
function Add-CatSideWalk([int]$D, [int]$TipY) {
    # Far legs and tail, behind the body.
    Add-Limb 11 22 (11 - $D) 27 Outline OrangeDark
    Add-Limb 22 22 (22 + $D) 27 Outline OrangeDark
    Add-Limb 24 18 27 14 Outline Orange
    Add-Limb 27 14 29 $TipY Outline Orange
    Px OrangeDark 29 $TipY 2 2

    # Body and chest.
    Px Outline 8 15 17 9; Px Orange 9 16 15 7
    Px Cream 9 19 4 4
    Px OrangeDark 14 16 1 3; Px OrangeDark 17 16 1 3; Px OrangeDark 20 16 1 3

    # Head in profile, with the one ear that shows from the side, at the back of the head.
    Add-Ear 9 5
    Px Outline 2 9 12 10; Px Orange 3 10 10 8
    Px Cream 3 14 4 4; Px Pink 2 14 1 1
    Px EyeG 4 12 2 2; Px Black 4 12 1 2
    Px Whisker 7 14 4 1; Px Whisker 7 16 3 1

    # Near legs.
    Add-Limb 10 22 (10 + $D) 27 Outline Orange
    Add-Limb 21 22 (21 - $D) 27 Outline Orange
}

# The cat seen from the side climbing the left edge of the screen, which is the wall: head up and forward, body upright,
# paws on the edge of the frame at the given heights. The head is raised and the eyes look up.
function Add-CatClimb([int]$NearHandY, [int]$FarHandY, [int]$NearFootY, [int]$FarFootY, [int]$TailX) {
    Add-Limb 7 14 1 $FarHandY Outline OrangeDark
    Add-Limb 8 24 1 $FarFootY Outline OrangeDark
    Add-Limb 12 25 $TailX 29 Outline Orange
    Px OrangeDark $TailX 29 2 2

    Px Outline 5 12 9 14; Px Orange 6 13 7 12
    Px Cream 6 18 3 6
    Px OrangeDark 10 14 3 1; Px OrangeDark 10 17 3 1; Px OrangeDark 10 20 3 1; Px OrangeDark 10 23 3 1

    # The head, tilted back to look up: the standing head (ears on top, face at the front) turned a quarter turn
    # clockwise, so the face with its cream muzzle points up and the ear (a triangle pointing back) sits at the back of the head, at the bottom. Seen from the side, it has one ear and
    # one eye, and the skull is a square, 9 by 9 art pixels.
    Add-Sprite 6 4 @(
        'XXXpXXXXX.....',
        'XcccooooX.....',
        'XcccokkoX.....',
        'XcccoggoX.....',
        'XcccooooXX....',
        'XoooooooXoX...',
        'XoooooooXpoX..',
        'XoooooooXoX...',
        'XXXXXXXXXX....'
    )



    Add-Limb 8 25 1 $NearFootY Outline Orange; Px Cream 1 $NearFootY 2 2
    Add-Limb 7 15 1 $NearHandY Outline Orange; Px Cream 1 $NearHandY 2 2
}

# Idle: sits with a swishing tail and blinks now and then.
Save-Frame 'idle_0.png' { Add-Cat -Tail 0 }
Save-Frame 'idle_1.png' { Add-Cat -Tail 1 }
Save-Frame 'idle_2.png' { Add-Cat -Tail 2 }
Save-Frame 'idle_3.png' { Add-Cat -Tail 1 -Eyes closed }

# Walk: seen from the side on four legs, with a little bounce. Drawn facing left and mirrored.
Save-Frame 'walk_0.png' -Mirror { Add-CatSideWalk -D -3 -TipY 10 }
Save-Frame 'walk_1.png' -Mirror -Dy -1 { Add-CatSideWalk -D 0 -TipY 11 }
Save-Frame 'walk_2.png' -Mirror { Add-CatSideWalk -D 3 -TipY 10 }
Save-Frame 'walk_3.png' -Mirror -Dy -1 { Add-CatSideWalk -D 0 -TipY 9 }

# Sleep: curled up in a loaf, a floating z.
Save-Frame 'sleep_0.png' { Add-CatLie }
Save-Frame 'sleep_1.png' { Add-CatLie; Add-Text 'z' 'White' 98 18; Add-Text 'Z' 'White' 106 4 }

# Drag: lifted by the scruff, paws up and surprised.
Save-Frame 'drag_0.png' -Air { Add-Cat -Eyes surprised -ArmsUp -Tail 0 }
Save-Frame 'drag_1.png' -Air { Add-Cat -Eyes surprised -ArmsUp -Tail 2 }

# Tired: flopped down, heavy eyes, a drop of sweat.
Save-Frame 'tired_0.png' { Add-CatLie -Eyes tired }
Save-Frame 'tired_1.png' -Dy 0 { Add-CatLie -Eyes tired; Px Blue 19 11 1 2 }

# Alert: jumps with wide eyes and an exclamation mark.
Save-Frame 'alert_0.png' -Dy -2 { Add-Cat -Eyes surprised -ArmsUp -Tail 1; Add-Text '!' 'Red' 100 6 }
Save-Frame 'alert_1.png' { Add-Cat -Eyes surprised -ArmsUp -Tail 3; Add-Text '!' 'Red' 100 6 }

# Happy: bounces with smiling eyes and a heart.
Save-Frame 'happy_0.png' -Dy -2 { Add-Cat -Eyes happy -ArmsUp -Tail 1; Add-Heart 24 2 }
Save-Frame 'happy_1.png' { Add-Cat -Eyes happy -ArmsUp -Tail 3 }

# Welcome: waves hello with one paw, hearts floating up.
foreach ($f in 0..3) {
    $dx = @(0, 1, 0, -1)[$f]
    $hearts = ($f % 2 -eq 0)
    $draw = { Add-Cat -Eyes happy -RightArmUp -RightArmDx $dx -Tail $f; if ($hearts) { Add-Heart 24 1 } }.GetNewClosure()
    Save-Frame "welcome_$f.png" $draw
}

# Meal: nibbles a fish held in one paw until only the bone is left, then licks its lips.
foreach ($f in 0..3) {
    $len = @(7, 5, 3, 0)[$f]
    $eyes = if ($f -eq 3) { 'closed' } else { 'happy' }
    $draw = {
        Add-Cat -Eyes $eyes -RightArmUp -Tail $f
        if ($len -gt 0) {
            Px Outline 23 10 ($len + 1) 4; Px Blue 24 11 ($len - 1) 2
            if ($len -ge 5) { Px Blue 31 10 1 1; Px Blue 31 13 1 1; Px White 25 11 1 1 }
        } else {
            Px White 24 12 5 1; Px White 28 11 1 3
        }
    }.GetNewClosure()
    Save-Frame "meal_$f.png" $draw
}

# Dizzy: crossed-out eyes, wobbling, with stars circling around the head.
$starSlots = @(@(26, 3), @(28, 7), @(25, 10), @(4, 3))
foreach ($f in 0..3) {
    $first = $starSlots[$f]
    $second = $starSlots[($f + 2) % 4]
    $draw = {
        Add-Cat -Eyes dizzy -Tail $f
        Add-Star Yellow $first[0] $first[1]
        Add-Star White $second[0] $second[1]
    }.GetNewClosure()
    Save-Frame "dizzy_$f.png" $draw -Dy $(if ($f % 2 -eq 0) { 0 } else { 1 })
}

# Glide: floating down under an umbrella or parachute, both paws on its handle, legs dangling. Drawn without the canopy, which the
# program puts over the head.
Save-Frame 'glide_0.png' -Air { Add-Cat -Eyes surprised -HoldOverhead -Tail 0 }
Save-Frame 'glide_1.png' -Air { Add-Cat -Eyes surprised -HoldOverhead -Tail 2 }

# Hang: clings to the left edge of the screen by one outstretched paw, legs dangling. Mirrored for the right edge.
Save-Frame 'hang_0.png' -Air { Add-Cat -LeftArmReach -Tail 0 }
Save-Frame 'hang_1.png' -Air { Add-Cat -LeftArmReach -Tail 2 }

# Climb: seen from the side, climbs the left edge of the screen paw over paw. Mirrored for the right edge.
Save-Frame 'climb_0.png' -Air { Add-CatClimb -NearHandY 8 -FarHandY 16 -NearFootY 29 -FarFootY 24 -TailX 16 }
Save-Frame 'climb_1.png' -Air -Dy -1 { Add-CatClimb -NearHandY 12 -FarHandY 14 -NearFootY 27 -FarFootY 26 -TailX 18 }
Save-Frame 'climb_2.png' -Air { Add-CatClimb -NearHandY 16 -FarHandY 8 -NearFootY 24 -FarFootY 29 -TailX 16 }
Save-Frame 'climb_3.png' -Air -Dy -1 { Add-CatClimb -NearHandY 14 -FarHandY 12 -NearFootY 26 -FarFootY 27 -TailX 14 }

# ClimbDown: the same climb turned upside down, so the cat climbs down head first with its face looking down.
Save-Frame 'climbdown_0.png' -Air -Flip { Add-CatClimb -NearHandY 8 -FarHandY 16 -NearFootY 29 -FarFootY 24 -TailX 16 }
Save-Frame 'climbdown_1.png' -Air -Flip -Dy -1 { Add-CatClimb -NearHandY 12 -FarHandY 14 -NearFootY 27 -FarFootY 26 -TailX 18 }
Save-Frame 'climbdown_2.png' -Air -Flip { Add-CatClimb -NearHandY 16 -FarHandY 8 -NearFootY 24 -FarFootY 29 -TailX 16 }
Save-Frame 'climbdown_3.png' -Air -Flip -Dy -1 { Add-CatClimb -NearHandY 14 -FarHandY 12 -NearFootY 26 -FarFootY 27 -TailX 14 }

# Ceiling: walks along the top of the screen upside down, in the same side view as walking on the ground.
Save-Frame 'ceiling_0.png' -Flip -Mirror { Add-CatSideWalk -D -3 -TipY 10 }
Save-Frame 'ceiling_1.png' -Flip -Mirror -Dy -1 { Add-CatSideWalk -D 0 -TipY 11 }
Save-Frame 'ceiling_2.png' -Flip -Mirror { Add-CatSideWalk -D 3 -TipY 10 }
Save-Frame 'ceiling_3.png' -Flip -Mirror -Dy -1 { Add-CatSideWalk -D 0 -TipY 9 }

# Idle looks: the eyes follow the mouse pointer.
Save-Frame 'idle_left.png'  { Add-Cat -EyeShift -1 }
Save-Frame 'idle_right.png' { Add-Cat -EyeShift 1 }
Save-Frame 'idle_up.png'    { Add-Cat -EyeDy -1 }
Save-Frame 'idle_down.png'  { Add-Cat -EyeDy 1 }

$manifest = @'
{
  "name": "cat",
  "frameSize": [64, 64],
  "states": {
    "Idle":    { "fps": 2, "frames": ["idle_0.png", "idle_1.png", "idle_2.png", "idle_3.png"] },
    "Walk":    { "fps": 8, "frames": ["walk_0.png", "walk_1.png", "walk_2.png", "walk_3.png"] },
    "Sleep":   { "fps": 1, "frames": ["sleep_0.png", "sleep_1.png"] },
    "Drag":    { "fps": 6, "frames": ["drag_0.png", "drag_1.png"] },
    "Glide":   { "fps": 4, "frames": ["glide_0.png", "glide_1.png"] },
    "Tired":   { "fps": 2, "frames": ["tired_0.png", "tired_1.png"] },
    "Alert":   { "fps": 4, "frames": ["alert_0.png", "alert_1.png"] },
    "Happy":   { "fps": 5, "frames": ["happy_0.png", "happy_1.png"] },
    "Welcome": { "fps": 4, "frames": ["welcome_0.png", "welcome_1.png", "welcome_2.png", "welcome_3.png"] },
    "Meal":    { "fps": 3, "frames": ["meal_0.png", "meal_1.png", "meal_2.png", "meal_3.png"] },
    "Dizzy":   { "fps": 6, "frames": ["dizzy_0.png", "dizzy_1.png", "dizzy_2.png", "dizzy_3.png"] },
    "Hang":    { "fps": 2, "frames": ["hang_0.png", "hang_1.png"] },
    "Climb":   { "fps": 6, "frames": ["climb_0.png", "climb_1.png", "climb_2.png", "climb_3.png"] },
    "ClimbDown": { "fps": 6, "frames": ["climbdown_0.png", "climbdown_1.png", "climbdown_2.png", "climbdown_3.png"] },
    "Ceiling": { "fps": 6, "frames": ["ceiling_0.png", "ceiling_1.png", "ceiling_2.png", "ceiling_3.png"] },
    "Idle.Left":  { "fps": 1, "frames": ["idle_left.png"] },
    "Idle.Right": { "fps": 1, "frames": ["idle_right.png"] },
    "Idle.Up":    { "fps": 1, "frames": ["idle_up.png"] },
    "Idle.Down":  { "fps": 1, "frames": ["idle_down.png"] }
  }
}
'@

$manifestPath = Join-Path $outDir 'manifest.json'
Write-SkinManifest $manifestPath $manifest

Write-Host "Cat skin written to $outDir"
