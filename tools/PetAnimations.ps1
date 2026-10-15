# The animation set shared by the pixel-art pets that are drawn from the same template (dog, rabbit, penguin, ghost).
# A generator dot-sources PetDrawing.ps1, defines $pal, $textFont, $PetName, $HasHop, $ClimbHead and the functions listed
# below, then dot-sources this file, which saves every frame and the manifest into pets\<name>.
#
# Required: $PetNames (@{ En = ...; Zh = ... }, the name in English and in Chinese) and $IconBox, the area of the head in the idle frame, for the tray icon.
# Optional settings: $WalkHops (the walk is a series of hops), $WalkFps (frames per second of the walk), $NoLegs (no feet
# when climbing), $NoArms (no arms: nothing is raised, held or reached out), $ClimbFromWalk (the climb is the walk turned a
# quarter turn; so is the climb of a $WalkHops pet, and its ceiling walk is its hops upside down).
# Palette keys used here: Outline, Body, BodyDark, Belly, Paw, Pink, Iris, EyeWhite, Lid, Black, Whisker, White, Blue,
#   Red, Yellow, Accent (any key the pet's own functions use may be added).
# Functions the generator supplies (art grid 32x32, the pet's feet end at y 29 so a grounded frame is lowered by two):
#   Add-PetBack $Phase                     Tail or anything behind the sitting pet (Phase 0-3).
#   Add-PetBody $LeftDown $RightDown       Sitting pet seen from the front, without eyes: body, legs or flippers that
#                                          are not raised, head with its face details. The head box is x 8-23, y 6-16.
#   Add-PetLie                             Lying pet without eyes; the head box is x 4-17, y 14-24 (eyes at y 17-18).
#   Add-PetSide $D $TipY                   Side view facing left, walking: D (-3 to 3) swings the legs.
#   Add-PetFood $Len                       Food held in the raised right paw (at x 23-31, y 10-13), Len 7, 5, 3 or 0.
#   $ClimbHead                             Rows of the head seen from the side looking up, for Add-Sprite (not needed by a pet
#                                          that sets $ClimbFromWalk).
$script:LimbCore = 2
if ($null -eq $WalkFps) { $WalkFps = 8 }

$outDir = Join-Path $PSScriptRoot "..\pets\$PetName"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Get-ChildItem $outDir -Filter *.png | Remove-Item

# Renders one frame; see GenerateCatPet.ps1 for the meaning of the switches.
function Save-Frame([string]$Name, [scriptblock]$Draw, [int]$Dy = 0, [switch]$Flip, [switch]$Mirror, [switch]$Air, [double]$Tilt = 0, [double]$PivotX = 16, [double]$PivotY = 24) {
    $bmp = New-Canvas
    $script:dy = if ($Air) { $Dy } else { [Math]::Min($Dy, 0) + 2 }
    & $Draw
    Save-Canvas $bmp (Join-Path $outDir $Name) -Flip:$Flip -Mirror:$Mirror -Tilt $Tilt -PivotX $PivotX -PivotY $PivotY
}

function Add-Star([string]$Color, [int]$X, [int]$Y) {
    Px $Color $X ($Y - 1) 1 3; Px $Color ($X - 1) $Y 3 1
}

# Draws a picture given as rows of characters, its top-left corner at the given art pixel: X outline, o body, d dark body,
# c belly, p pink, g iris, k black, w whisker, a accent, anything else is left clear.
function Add-Sprite([int]$X, [int]$Y, [string[]]$Rows) {
    $colors = @{ X = 'Outline'; o = 'Body'; d = 'BodyDark'; c = 'Belly'; p = 'Pink'; g = 'Iris'; k = 'Black'; w = 'Whisker'; a = 'Accent' }
    for ($r = 0; $r -lt $Rows.Count; $r++) {
        for ($c = 0; $c -lt $Rows[$r].Length; $c++) {
            $color = $colors["$($Rows[$r][$c])"]
            if ($color) { Px $color ($X + $c) ($Y + $r) }
        }
    }
}

# A body with a rounded top, outlined, in the body color: X, Y, W and H are the outer box in art pixels (W at least 10).
function Add-Dome([int]$X, [int]$Y, [int]$W, [int]$H) {
    Px Outline ($X + 3) $Y ($W - 6) 1; Px Outline ($X + 1) ($Y + 1) ($W - 2) 1; Px Outline $X ($Y + 2) $W ($H - 2)
    Px Body ($X + 4) ($Y + 1) ($W - 8) 1; Px Body ($X + 2) ($Y + 2) ($W - 4) 1; Px Body ($X + 1) ($Y + 3) ($W - 2) ($H - 3)
}

# The eyes on a head whose box spans x 8-23, y 6-16. Eyes: open | closed | happy | tired | surprised | dizzy.
function Add-PetEyes([string]$Eyes, [int]$Shift, [int]$Dy) {
    $y = 9 + $Dy
    switch ($Eyes) {
        'open' {
            Px Iris 11 $y 3 3; Px Iris 18 $y 3 3
            Px Black (12 + $Shift) $y 1 3; Px Black (19 + $Shift) $y 1 3
        }
        'closed' { Px Lid 11 10 3 1; Px Lid 18 10 3 1 }
        'happy' {
            foreach ($x0 in 11, 18) { Px Lid $x0 10; Px Lid ($x0 + 1) 9; Px Lid ($x0 + 2) 10 }
            Px Pink 10 12 2 1; Px Pink 20 12 2 1
        }
        'tired' {
            Px Iris 11 10 3 2; Px Iris 18 10 3 2; Px Black 12 10 1 2; Px Black 19 10 1 2
            Px BodyDark 11 9 3 1; Px BodyDark 18 9 3 1
        }
        'surprised' {
            Px EyeWhite 11 8 3 4; Px EyeWhite 18 8 3 4; Px Black 12 9 1 2; Px Black 19 9 1 2
        }
        'dizzy' {
            foreach ($x0 in 11, 18) { Px Lid $x0 9; Px Lid ($x0 + 2) 9; Px Lid ($x0 + 1) 10; Px Lid $x0 11; Px Lid ($x0 + 2) 11 }
        }
    }
}

# The sitting pet seen from the front. ArmsUp raises both front paws, RightArmUp only the right one (RightArmDx sways
# it, for a wave), LeftArmReach stretches the left one out to the edge of the frame to hold on to the screen, and
# HoldOverhead raises both behind the head to the handle of an umbrella or parachute above it.
function Add-Pet {
    param(
        [string]$Eyes = 'open', [int]$Tail = 0, [int]$EyeShift = 0, [int]$EyeDy = 0,
        [switch]$ArmsUp, [switch]$RightArmUp, [int]$RightArmDx = 0, [switch]$LeftArmReach, [int]$ReachY = 13,
        [switch]$HoldOverhead
    )
    Add-PetBack $Tail
    if ($HoldOverhead -and -not $NoArms) {
        Add-Limb 10 20 4 12 Outline Body; Add-Limb 4 12 13 3 Outline Body
        Add-Limb 21 20 27 12 Outline Body; Add-Limb 27 12 18 3 Outline Body
    }
    Add-PetBody (-not $ArmsUp -and -not $LeftArmReach) (-not $ArmsUp -and -not $RightArmUp)
    Add-PetEyes $Eyes $EyeShift $EyeDy
    if ($NoArms) { return }
    if ($ArmsUp -or $RightArmUp) { Add-Limb 21 20 (26 + $RightArmDx) 13 Outline Body; Px Paw (26 + $RightArmDx) 13 2 2 }
    if ($ArmsUp) { Add-Limb 10 20 5 13 Outline Body; Px Paw 5 13 2 2 }
    if ($LeftArmReach) { Add-Limb 10 19 1 $ReachY Outline Body; Px Paw 1 $ReachY 2 2 }
    if ($HoldOverhead) { Px Paw 13 2 2 3; Px Paw 17 2 2 3 }
}

# The pet lying down. Eyes: closed | tired.
function Add-PetLying([string]$Eyes = 'closed') {
    Add-PetLie
    if ($Eyes -eq 'tired') {
        Px Iris 6 18 3 1; Px Iris 13 18 3 1; Px Lid 6 17 3 1; Px Lid 13 17 3 1
        Px Blue 19 13 1 2
    } else {
        Px Lid 6 18 3 1; Px Lid 13 18 3 1
    }
}

# The pet seen from the side climbing the left edge of the screen: head up, body upright, paws on the edge of the frame
# at the given heights.
function Add-PetClimb([int]$NearHandY, [int]$FarHandY, [int]$NearFootY, [int]$FarFootY, [int]$TailX) {
    Add-Limb 7 14 1 $FarHandY Outline BodyDark
    if (-not $NoLegs) {
        Add-Limb 8 24 1 $FarFootY Outline BodyDark
        Add-Limb 12 25 $TailX 29 Outline Body
        Px BodyDark $TailX 29 2 2
    }
    Px Outline 5 12 9 14; Px Body 6 13 7 12
    Px Belly 6 18 3 6
    Add-Sprite 6 4 $ClimbHead
    if (-not $NoLegs) { Add-Limb 8 25 1 $NearFootY Outline Body; Px Paw 1 $NearFootY 2 2 }
    Add-Limb 7 15 1 $NearHandY Outline Body; Px Paw 1 $NearHandY 2 2
}

# Idle: sits, the tail or body moving, and blinks now and then.
Save-Frame 'idle_0.png' { Add-Pet -Tail 0 }
Save-Frame 'idle_1.png' { Add-Pet -Tail 1 }
Save-Frame 'idle_2.png' { Add-Pet -Tail 2 }
Save-Frame 'idle_3.png' { Add-Pet -Tail 1 -Eyes closed }

# Walk: seen from the side, drawn facing left and mirrored. A pet that moves by hopping ($WalkHops) crouches, springs up,
# floats and lands again; any other pet bounces a little as its legs swing.
# The poses of a hopping walk: pose D, tail height, tilt in degrees and the vertical offset of the frame (the first one
# is on the ground, which lowers a frame by two).
$hopPoses = @(@(0, 12, -4, 2), @(-3, 8, 14, -2), @(-3, 9, 0, -3), @(3, 10, -14, -1))
if ($WalkHops) {
    foreach ($i in 0..3) {
        $pose = $hopPoses[$i]
        $draw = { Add-PetSide $pose[0] $pose[1] }.GetNewClosure()
        if ($i -eq 0) { Save-Frame "walk_$i.png" -Mirror -Dy 1 -Tilt $pose[2] $draw }
        else { Save-Frame "walk_$i.png" -Mirror -Air -Dy $pose[3] -Tilt $pose[2] $draw }
    }
} else {
    Save-Frame 'walk_0.png' -Mirror { Add-PetSide -3 10 }
    Save-Frame 'walk_1.png' -Mirror -Dy -1 { Add-PetSide 0 11 }
    Save-Frame 'walk_2.png' -Mirror { Add-PetSide 3 10 }
    Save-Frame 'walk_3.png' -Mirror -Dy -1 { Add-PetSide 0 9 }
}

# Hop: a leap while walking, only for pets that define it.
if ($HasHop) {
    Save-Frame 'hop_0.png' -Mirror -Dy 1 -Tilt -4 { Add-PetSide 0 12 }
    Save-Frame 'hop_1.png' -Mirror -Air -Dy -1 -Tilt 18 { Add-PetSide -3 8 }
    Save-Frame 'hop_2.png' -Mirror -Air -Dy -1 -Tilt 4 { Add-PetSide -3 9 }
    Save-Frame 'hop_3.png' -Mirror -Air -Dy 0 -Tilt -18 { Add-PetSide -3 10 }
    Save-Frame 'hop_4.png' -Mirror -Dy 1 -Tilt -6 { Add-PetSide 0 12 }
}

# Sleep and tired: lying down.
Save-Frame 'sleep_0.png' { Add-PetLying }
Save-Frame 'sleep_1.png' { Add-PetLying; Add-Text 'z' 'White' 98 18; Add-Text 'Z' 'White' 106 4 }
Save-Frame 'tired_0.png' { Add-PetLying -Eyes tired }
Save-Frame 'tired_1.png' -Dy 0 { Add-PetLying -Eyes tired; Px Blue 19 11 1 2 }

# Drag: lifted, paws up and surprised.
Save-Frame 'drag_0.png' -Air { Add-Pet -Eyes surprised -ArmsUp -Tail 0 }
Save-Frame 'drag_1.png' -Air { Add-Pet -Eyes surprised -ArmsUp -Tail 2 }

# Alert: jumps with wide eyes and an exclamation mark.
Save-Frame 'alert_0.png' -Dy -2 { Add-Pet -Eyes surprised -ArmsUp -Tail 1; Add-Text '!' 'Red' 100 6 }
Save-Frame 'alert_1.png' { Add-Pet -Eyes surprised -ArmsUp -Tail 3; Add-Text '!' 'Red' 100 6 }

# Happy: bounces with smiling eyes and a heart.
Save-Frame 'happy_0.png' -Dy -2 { Add-Pet -Eyes happy -ArmsUp -Tail 1; Add-Heart 24 2 }
Save-Frame 'happy_1.png' { Add-Pet -Eyes happy -ArmsUp -Tail 3 }

# Welcome: waves hello with one paw, hearts floating up.
foreach ($f in 0..3) {
    $dx = @(0, 1, 0, -1)[$f]
    $hearts = ($f % 2 -eq 0)
    $draw = { Add-Pet -Eyes happy -RightArmUp -RightArmDx $dx -Tail $f; if ($hearts) { Add-Heart 24 1 } }.GetNewClosure()
    Save-Frame "welcome_$f.png" $draw
}

# Meal: eats something held in one paw until nothing is left, then licks its lips.
foreach ($f in 0..3) {
    $len = @(7, 5, 3, 0)[$f]
    $eyes = if ($f -eq 3) { 'closed' } else { 'happy' }
    $draw = { Add-Pet -Eyes $eyes -RightArmUp -Tail $f; Add-PetFood $len }.GetNewClosure()
    Save-Frame "meal_$f.png" $draw
}

# Dizzy: crossed-out eyes, wobbling, with stars circling around the head.
$starSlots = @(@(26, 3), @(28, 7), @(25, 10), @(4, 3))
foreach ($f in 0..3) {
    $first = $starSlots[$f]
    $second = $starSlots[($f + 2) % 4]
    $draw = {
        Add-Pet -Eyes dizzy -Tail $f
        Add-Star Yellow $first[0] $first[1]
        Add-Star White $second[0] $second[1]
    }.GetNewClosure()
    Save-Frame "dizzy_$f.png" $draw -Dy $(if ($f % 2 -eq 0) { 0 } else { 1 })
}

# Glide: floating down holding the handle of an umbrella or parachute (drawn by the program), legs dangling.
Save-Frame 'glide_0.png' -Air { Add-Pet -Eyes surprised -HoldOverhead -Tail 0 }
Save-Frame 'glide_1.png' -Air { Add-Pet -Eyes surprised -HoldOverhead -Tail 2 }

# Hang: clings to the left edge of the screen by one outstretched paw. Mirrored for the right edge.
Save-Frame 'hang_0.png' -Air { Add-Pet -LeftArmReach -Tail 0 }
Save-Frame 'hang_1.png' -Air { Add-Pet -LeftArmReach -Tail 2 }

# Climb and ClimbDown (the same climb upside down), and Ceiling (the walk upside down). A pet that climbs by $ClimbFromWalk
# is the walk turned a quarter turn clockwise, so its nose points up and its belly is against the left edge.
if ($ClimbFromWalk -and $WalkHops) {
    foreach ($i in 0..3) {
        $pose = $hopPoses[$i]
        $draw = { Add-PetSide $pose[0] $pose[1] }.GetNewClosure()
        $turn = 90 + $pose[2]
        Save-Frame "climb_$i.png" -Air -Dy $pose[3] -Tilt $turn -PivotX 16 -PivotY 16 $draw
        Save-Frame "climbdown_$i.png" -Air -Flip -Dy $pose[3] -Tilt $turn -PivotX 16 -PivotY 16 $draw
    }
} elseif ($ClimbFromWalk) {
    $walkPoses = @(@(-3, 10, 2), @(0, 11, 1), @(3, 10, 2), @(0, 9, 1))
    foreach ($i in 0..3) {
        $d = $walkPoses[$i][0]; $tip = $walkPoses[$i][1]; $lift = $walkPoses[$i][2]
        $draw = { Add-PetSide $d $tip }.GetNewClosure()
        Save-Frame "climb_$i.png" -Air -Dy $lift -Tilt 90 -PivotX 16 -PivotY 16 $draw
        Save-Frame "climbdown_$i.png" -Air -Flip -Dy $lift -Tilt 90 -PivotX 16 -PivotY 16 $draw
    }
}
$climbPoses = @(
    @{ Args = @(8, 16, 29, 24, 16); Dy = 0 }, @{ Args = @(12, 14, 27, 26, 18); Dy = -1 },
    @{ Args = @(16, 8, 24, 29, 16); Dy = 0 }, @{ Args = @(14, 12, 26, 27, 14); Dy = -1 }
)
if (-not $ClimbFromWalk -and -not $WalkHops) { foreach ($i in 0..3) {
    $a = $climbPoses[$i].Args
    $dyI = $climbPoses[$i].Dy
    $draw = { Add-PetClimb $a[0] $a[1] $a[2] $a[3] $a[4] }.GetNewClosure()
    Save-Frame "climb_$i.png" -Air -Dy $dyI $draw
    Save-Frame "climbdown_$i.png" -Air -Flip -Dy $dyI $draw
} }
if ($WalkHops) {
    foreach ($i in 0..3) {
        $pose = $hopPoses[$i]
        $draw = { Add-PetSide $pose[0] $pose[1] }.GetNewClosure()
        if ($i -eq 0) { Save-Frame "ceiling_$i.png" -Flip -Mirror -Dy 1 -Tilt $pose[2] $draw }
        else { Save-Frame "ceiling_$i.png" -Flip -Mirror -Air -Dy $pose[3] -Tilt $pose[2] $draw }
    }
} else {
    Save-Frame 'ceiling_0.png' -Flip -Mirror { Add-PetSide -3 10 }
    Save-Frame 'ceiling_1.png' -Flip -Mirror -Dy -1 { Add-PetSide 0 11 }
    Save-Frame 'ceiling_2.png' -Flip -Mirror { Add-PetSide 3 10 }
    Save-Frame 'ceiling_3.png' -Flip -Mirror -Dy -1 { Add-PetSide 0 9 }
}

# Idle looks: the eyes follow the mouse pointer.
Save-Frame 'idle_left.png'  { Add-Pet -EyeShift -1 }
Save-Frame 'idle_right.png' { Add-Pet -EyeShift 1 }
Save-Frame 'idle_up.png'    { Add-Pet -EyeDy -1 }
Save-Frame 'idle_down.png'  { Add-Pet -EyeDy 1 }

# The situations the pet reacts to (writing code, reading, meetings and so on).
function Add-ScenarioPet { Add-Pet @args }
. (Join-Path $PSScriptRoot 'PetScenarios.ps1')

$hopLine = if ($HasHop) { "    `"Hop`":     { `"fps`": 8, `"frames`": [`"hop_0.png`", `"hop_1.png`", `"hop_2.png`", `"hop_3.png`", `"hop_4.png`"] },`n" } else { '' }
$manifest = @"
{
  "name": "$PetName",
  "names": { "en": "$($PetNames.En)", "zh": "$($PetNames.Zh)" },
  "frameSize": [64, 64],
  "states": {
    "Idle":    { "fps": 2, "frames": ["idle_0.png", "idle_1.png", "idle_2.png", "idle_3.png"] },
    "Walk":    { "fps": $WalkFps, "frames": ["walk_0.png", "walk_1.png", "walk_2.png", "walk_3.png"] },
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
$hopLine$ScenarioManifest    "Idle.Left":  { "fps": 1, "frames": ["idle_left.png"] },
    "Idle.Right": { "fps": 1, "frames": ["idle_right.png"] },
    "Idle.Up":    { "fps": 1, "frames": ["idle_up.png"] },
    "Idle.Down":  { "fps": 1, "frames": ["idle_down.png"] }
  }
}
"@

Write-PetManifest (Join-Path $outDir 'manifest.json') $manifest

# The tray icon: the head, as boxed by the generator in $IconBox (art pixels: x, y, width, height).
Save-PetIcon (Join-Path $outDir 'idle_0.png') (Join-Path $outDir 'icon.png') $IconBox

# Pack the single frames into the atlas the program reads.
. (Join-Path $PSScriptRoot 'PackPetAtlas.ps1')
Invoke-PetAtlasPacking $outDir

Write-Host "$PetName pet written to $outDir"
