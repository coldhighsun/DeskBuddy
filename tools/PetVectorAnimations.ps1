# The animation set shared by the vector pets that are drawn from the same template (dog, cat, rabbit, penguin, ghost).
# A generator dot-sources PetVectorDrawing.ps1, defines $pal and the settings and functions listed below, then dot-sources
# this file, which saves every frame, the manifest, the tray icon and the atlas into pets\<name>.
#
# Everything is drawn on the 64x64 unit grid of PetVectorDrawing.ps1; a sitting pet stands on y 62, its head is centered near
# (32, 27) and spans about x 16-48, y 14-39 (give the real box in $HeadBox), so the props and the labels of the scenario
# frames fit every pet.
#
# Required: $PetName, $PetNames (@{ En = ...; Zh = ...; ZhHant = ... }, the name in English, in Simplified and in Traditional Chinese; ZhHant may be left out when it reads like Zh), $IconBox (the head in the idle
# frame, x y width height in drawing units, for the tray icon), $pal.
# Optional settings: $HasHop (a hop animation for the walk), $WalkHops (the walk is a series of hops; so is the climb, and the
#   ceiling walk is the hops upside down), $WalkFps (frames per second of the walk), $NoArms (no arms: nothing is raised, held
#   or reached out), $HeadBox (x0 y0 x1 y1 of the head, for the headband and the headset), $SitEyes / $LieEyes (@{ Lx; Rx; Y }
#   the eyes of the sitting and of the lying pet), $LieDrop (x y of the sweat drop of a tired lying pet), $EyeKeys (palette keys
#   of the eyes: Fill, Pupil (empty for none), Lid, Brow), $Arms (overrides for the defaults of $armSpec below: Color, Paw,
#   Width, ShoulderL, ShoulderR, UpL, UpR, ...).
# Functions the generator supplies (a pet is drawn facing front when sitting and facing left from the side):
#   Add-PetBack $Phase                  Tail or anything behind the sitting pet (Phase 0-3).
#   Add-PetBody $LeftDown $RightDown    Torso, and the front legs or flippers that are not raised (they are the arms when raised).
#                                       $script:PetPortrait is true while the tray icon portrait is drawn: leave out anything
#                                       that would show at the edges of the icon box, such as hands drawn with the body.
#   Add-PetHead                         Head with ears, cheeks and the face without the eyes.
#   Add-PetLie                          Lying pet without eyes.
#   Add-PetSide $D $Lift                Side view walking: D (-3 to 3) swings the legs, Lift (about -4 to 4) raises the tail tip.
#   Add-PetFood $Len                    Food held in the raised right paw (around x 46-62, y 18-30), Len 7, 5, 3 or 0.
#   $SleepFrames (how many frames the Sleep has, default 2; with more, Add-PetLie can read $script:SleepPhase to breathe)
#   $GlideFlies (the pet flies down on its own wings: no umbrella or parachute, the Glide is four frames drawn with
#   $script:Flying true, which Add-PetBack may read to spread the wings, and the manifest says "flies": true with no grips;
#   a pet may supply Add-PetFly $Phase (0-3, one flap of the wings) to draw the whole flying pose itself)
# Optional function:
#   Add-PetHoldOverhead                Both front limbs raised for the glide, drawn instead of the default overhead arms.
# In the glide Add-PetBody always gets both front limbs as raised, so they are never drawn hanging as well. A real pet cannot
# reach over its head, so it holds the umbrella or the parachute (drawn by the program) with its paws beside its cheeks: the
# arms OverL / OverR of $armSpec go straight up from the shoulders, and the paws at their ends are drawn in front of the head.
# The program draws the shaft and the strings down to the manifest's Glide "grips", which are the paws, or $GlideGrips
# (drawing units, before the glide's lift) for a pet that holds on otherwise: one that brings its own Add-PetHoldOverhead or
# has $NoArms.
$script:armSpec = @{
    Color = 'Body'; Paw = 'Paw'; Width = 5; PawRx = 3.1; PawRy = 3.1
    ShoulderL = @(22, 46); ShoulderR = @(42, 46); UpL = @(11, 34); UpR = @(53, 34)
    OverL = @(22, 46, 13, 31); OverR = @(42, 46, 51, 31); OverWidth = 4.8
    ReachFrom = @(22, 46); ReachX = 3
}
if ($Arms) { foreach ($key in $Arms.Keys) { $script:armSpec[$key] = $Arms[$key] } }
if ($null -eq $WalkFps) { $WalkFps = 8 }
if ($null -eq $SitEyes) { $SitEyes = @{ Lx = 24.5; Rx = 39.5; Y = 26.5 } }
if ($null -eq $LieEyes) { $LieEyes = @{ Lx = 14; Rx = 24; Y = 48.4 } }
if ($null -eq $LieDrop) { $LieDrop = @(31, 41) }
if ($null -eq $HeadBox) { $HeadBox = @(16, 14, 48, 39) }
$script:eyeStyle = @{ Fill = 'Black'; Pupil = ''; Lid = 'Black'; Brow = 'Black'; FillRx = 2.9; FillRy = 3.5; PupilRx = 1.6; PupilRy = 2.1 }
if ($EyeKeys) { foreach ($key in $EyeKeys.Keys) { $script:eyeStyle[$key] = $EyeKeys[$key] } }
Set-HdDefaultColors
$script:pawKey = if ($pal.ContainsKey('Paw')) { 'Paw' } else { 'Body' }

$outDir = Join-Path $PSScriptRoot "..\pets\$PetName"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Get-ChildItem $outDir -Filter *.png | Remove-Item

# Renders one frame. Dy lifts it (negative is up, in drawing units), Dx moves it sideways (positive is right), Tilt turns it clockwise about (PivotX, PivotY), Flip turns
# it upside down and Mirror left to right (a side view is drawn facing left).
function Save-Frame([string]$Name, [scriptblock]$Draw, [double]$Dy = 0, [switch]$Flip, [switch]$Mirror, [double]$Tilt = 0, [double]$PivotX = 32, [double]$PivotY = 48, [double]$Dx = 0) {
    $bmp = New-HdCanvas -Dy $Dy -Dx $Dx -Tilt $Tilt -PivotX $PivotX -PivotY $PivotY
    & $Draw
    Save-HdCanvas $bmp (Join-Path $outDir $Name) -Flip:$Flip -Mirror:$Mirror
}

# One pair of eyes: Eyes is open | closed | happy | tired | surprised; Shift and Lift move the pupils.
function Add-PetEyes([double]$Lx, [double]$Rx, [double]$Y, [string]$Eyes, [double]$Shift = 0, [double]$Lift = 0) {
    $keys = $script:eyeStyle
    $ring = [bool]$keys.Pupil
    foreach ($x in $Lx, $Rx) {
        switch ($Eyes) {
            'open' {
                if ($ring) {
                    Add-HdOval $keys.Fill $x $Y $keys.FillRx $keys.FillRy ''
                    Add-HdOval $keys.Pupil ($x + $Shift * 0.8) ($Y + $Lift * 0.8) $keys.PupilRx $keys.PupilRy ''
                    Add-HdOval 'Shine' ($x + $Shift * 0.8 + 0.5) ($Y + $Lift * 0.8 - 0.8) 0.55 0.65 ''
                } else {
                    Add-HdOval $keys.Fill ($x + $Shift) ($Y + $Lift) 2.5 3.3 ''
                    Add-HdOval 'Shine' ($x + $Shift + 0.8) ($Y + $Lift - 1.2) 0.95 1.15 ''
                }
            }
            'closed' { Add-HdStroke @(($x - 2.6), $Y, $x, ($Y + 1.3), ($x + 2.6), $Y) $keys.Lid 1.3 }
            'happy'  { Add-HdStroke @(($x - 2.6), ($Y + 1.6), $x, ($Y - 1.2), ($x + 2.6), ($Y + 1.6)) $keys.Lid 1.4 }
            'tired' {
                if ($ring) {
                    Add-HdOval $keys.Fill $x ($Y + 0.9) $keys.FillRx ($keys.FillRy * 0.54) ''
                    Add-HdOval $keys.Pupil ($x + $Shift) ($Y + 1.1) $keys.PupilRx 1.4 ''
                } else {
                    Add-HdOval $keys.Fill $x ($Y + 0.9) 2.5 1.7 ''
                }
                Add-HdStroke @(($x - 3), ($Y - 0.6), ($x + 3), ($Y - 0.6)) $keys.Brow 1.5
            }
            'surprised' {
                Add-HdOval 'Shine' $x $Y 3.2 3.9 'Outline' 0.9
                Add-HdOval 'Black' ($x + $Shift) ($Y + $Lift) 1.5 1.9 ''
            }
        }
    }
    if ($Eyes -eq 'happy') {
        Add-HdOval 'Pink' ($Lx - 3.4) ($Y + 3.6) 2.4 1.4 ''
        Add-HdOval 'Pink' ($Rx + 3.4) ($Y + 3.6) 2.4 1.4 ''
    }
}

# An arm from the shoulder through the given points to a paw at the last of them.
function Add-PetArm([double[]]$Xy, [double]$Width = $script:armSpec.Width) {
    Add-HdJoinedLimb $Xy $script:armSpec.Color $Width
    Add-HdOval $script:armSpec.Paw $Xy[$Xy.Count - 2] ($Xy[$Xy.Count - 1] - 1) $script:armSpec.PawRx $script:armSpec.PawRy
}

# The sitting pet seen from the front. ArmsUp raises both front paws, RightArmUp only the right one (RightArmDx sways it, for
# a wave), LeftArmReach stretches the left one out to the edge of the frame to hold on to the screen, HoldOverhead raises both
# above the head to the handle of an umbrella or parachute (drawn by the program). Portrait leaves out the tail and the front
# limbs, for the tray icon, so nothing but the head shows at the edges of the icon box.
function Add-Pet {
    param(
        [string]$Eyes = 'open', [int]$Tail = 0, [double]$EyeShift = 0, [double]$EyeLift = 0,
        [switch]$ArmsUp, [switch]$RightArmUp, [double]$RightArmDx = 0, [switch]$LeftArmReach, [double]$ReachY = 26,
        [switch]$HoldOverhead, [switch]$Portrait
    )
    $arms = $script:armSpec
    # Holding the handle raises both front limbs, so they are not drawn hanging as well (a pet without arms keeps them).
    $overhead = $HoldOverhead -and -not $NoArms
    if (-not $Portrait) { Add-PetBack $Tail }
    $script:PetPortrait = [bool]$Portrait
    Add-PetBody (-not $ArmsUp -and -not $LeftArmReach -and -not $overhead -and -not $Portrait) (-not $ArmsUp -and -not $RightArmUp -and -not $overhead -and -not $Portrait)
    $script:PetPortrait = $false
    if (-not $NoArms -and -not $Portrait) {
        if ($overhead -and (Get-Command Add-PetHoldOverhead -ErrorAction SilentlyContinue)) {
            Add-PetHoldOverhead
        } elseif ($overhead) {
            Add-PetArm $arms.OverL $arms.OverWidth
            Add-PetArm $arms.OverR $arms.OverWidth
        }
        if ($ArmsUp -or $RightArmUp) { Add-PetArm @($arms.ShoulderR[0], $arms.ShoulderR[1], ($arms.UpR[0] + $RightArmDx), $arms.UpR[1]) }
        if ($ArmsUp) { Add-PetArm @($arms.ShoulderL[0], $arms.ShoulderL[1], $arms.UpL[0], $arms.UpL[1]) }
        if ($LeftArmReach) { Add-PetArm @($arms.ReachFrom[0], $arms.ReachFrom[1], $arms.ReachX, $ReachY) }
    }
    Add-PetHead
    Add-PetEyes $SitEyes.Lx $SitEyes.Rx $SitEyes.Y $Eyes $EyeShift $EyeLift
    # Holding on, the paws close round the shaft or the strings in front of the cheeks and the ears.
    if ($overhead -and -not (Get-Command Add-PetHoldOverhead -ErrorAction SilentlyContinue)) {
        foreach ($arm in $arms.OverL, $arms.OverR) {
            Add-HdOval $arms.Paw $arm[$arm.Count - 2] ($arm[$arm.Count - 1] - 1) $arms.PawRx $arms.PawRy
        }
    }
}

# The pet lying down. Eyes: closed | tired.
function Add-PetLying([string]$Eyes = 'closed', [double]$DropLift = 0) {
    Add-PetLie
    Add-PetEyes $LieEyes.Lx $LieEyes.Rx $LieEyes.Y $Eyes
    if ($Eyes -eq 'tired') { Add-HdOval 'Blue' $LieDrop[0] ($LieDrop[1] - $DropLift) 1.6 2.2 'Outline' 0.5 }
}

# Idle: sits, the tail or body moving, and blinks now and then.
Save-Frame 'idle_0.png' { Add-Pet -Tail 0 }
Save-Frame 'idle_1.png' { Add-Pet -Tail 1 }
Save-Frame 'idle_2.png' { Add-Pet -Tail 2 }
Save-Frame 'idle_3.png' { Add-Pet -Tail 1 -Eyes closed }

# Walk: seen from the side, drawn facing left and mirrored. A pet that moves by hopping ($WalkHops) crouches, springs up,
# floats and lands again; any other pet bounces a little as its legs swing.
# The poses of a hopping walk: pose D, tail lift, tilt in degrees and the vertical offset of the frame.
$hopPoses = @(@(0, -4, -4, 0), @(-3, 4, 14, -8), @(-3, 2, 0, -10), @(3, 0, -14, -6))
# The poses of a normal walk: pose D, tail lift and the vertical offset of the frame.
$walkPoses = @(@(-3, 0, 0), @(0, -2, -1), @(3, 0, 0), @(0, 2, -1))
foreach ($i in 0..3) {
    if ($WalkHops) {
        $pose = $hopPoses[$i]
        $draw = { Add-PetSide $pose[0] $pose[1] }.GetNewClosure()
        Save-Frame "walk_$i.png" -Mirror -Dy $pose[3] -Tilt $pose[2] $draw
    } else {
        $pose = $walkPoses[$i]
        $draw = { Add-PetSide $pose[0] $pose[1] }.GetNewClosure()
        Save-Frame "walk_$i.png" -Mirror -Dy $pose[2] $draw
    }
}

# Hop: a leap while walking, only for pets that define it.
if ($HasHop) {
    $leap = @(@(0, -4, -4, 0), @(-3, 4, 18, -6), @(-3, 2, 4, -6), @(-3, 0, -18, -4), @(0, -4, -6, 0))
    foreach ($i in 0..4) {
        $pose = $leap[$i]
        $draw = { Add-PetSide $pose[0] $pose[1] }.GetNewClosure()
        Save-Frame "hop_$i.png" -Mirror -Dy $pose[3] -Tilt $pose[2] $draw
    }
}

# Sleep and tired: lying down.
# A pet that sets $SleepFrames (more than two) sleeps through a longer breathing cycle: $script:SleepPhase (0 to the number of
# frames - 1) is set while each frame is drawn, so Add-PetLie can let a wing, the tail or a bubble follow the breath, and the
# z grows and fades over the cycle. With the default of two frames the sleep is as it always was.
$sleepCount = if ($SleepFrames) { [int]$SleepFrames } else { 2 }
foreach ($f in 0..($sleepCount - 1)) {
    $script:SleepPhase = $f
    if ($sleepCount -eq 2) { $smallZ = ($f -eq 1); $bigZ = ($f -eq 1) } else { $smallZ = ($f % 4 -in 1, 2); $bigZ = ($f % 4 -in 2, 3) }
    $draw = {
        Add-PetLying
        if ($smallZ) { Add-HdText 'z' 'Sleepy' 44 15 }
        if ($bigZ) { Add-HdText 'Z' 'Sleepy' 51 5 }
    }.GetNewClosure()
    Save-Frame "sleep_$f.png" $draw
}
$script:SleepPhase = 0
Save-Frame 'tired_0.png' { Add-PetLying -Eyes tired }
Save-Frame 'tired_1.png' { Add-PetLying -Eyes tired -DropLift 4 }

# Drag: lifted, paws up and surprised.
Save-Frame 'drag_0.png' { Add-Pet -Eyes surprised -ArmsUp -Tail 0 }
Save-Frame 'drag_1.png' { Add-Pet -Eyes surprised -ArmsUp -Tail 2 }

# Alert: jumps with wide eyes and an exclamation mark.
Save-Frame 'alert_0.png' -Dy -2 { Add-Pet -Eyes surprised -ArmsUp -Tail 1; Add-HdText '!' 'Red' 50 3 }
Save-Frame 'alert_1.png' { Add-Pet -Eyes surprised -ArmsUp -Tail 3; Add-HdText '!' 'Red' 50 3 }

# Happy: bounces with smiling eyes and a heart.
Save-Frame 'happy_0.png' -Dy -2 { Add-Pet -Eyes happy -ArmsUp -Tail 1; Add-HdHeart 'Heart' 56 8 3.4 }
Save-Frame 'happy_1.png' { Add-Pet -Eyes happy -ArmsUp -Tail 3 }

# Welcome: waves hello with one paw, hearts floating up.
foreach ($f in 0..3) {
    $dx = @(0, 2, 0, -2)[$f]
    $hearts = ($f % 2 -eq 0)
    $draw = { Add-Pet -Eyes happy -RightArmUp -RightArmDx $dx -Tail $f; if ($hearts) { Add-HdHeart 'Heart' 57 6 3.2 } }.GetNewClosure()
    Save-Frame "welcome_$f.png" $draw
}

# Meal: eats something held in one paw until nothing is left, then licks its lips.
foreach ($f in 0..3) {
    $len = @(7, 5, 3, 0)[$f]
    $eyes = if ($f -eq 3) { 'closed' } else { 'happy' }
    $draw = { Add-Pet -Eyes $eyes -RightArmUp -Tail $f; Add-PetFood $len }.GetNewClosure()
    Save-Frame "meal_$f.png" $draw
}

# Glide: floating down holding the handle of an umbrella or parachute (drawn by the program), legs dangling.
Save-Frame 'glide_0.png' -Dy -1 { Add-Pet -Eyes surprised -HoldOverhead -Tail 0 }
Save-Frame 'glide_1.png' -Dy -1 { Add-Pet -Eyes surprised -HoldOverhead -Tail 2 }
# A pet that flies down on its own wings ($GlideFlies) gets no umbrella or parachute: its Glide is four frames of it flapping,
# replacing the two above. A pet that supplies Add-PetFly draws the whole flying pose itself; any other flies in its sitting
# pose with the arms hanging, drawn while $script:Flying is true so Add-PetBack can spread the wings.
if ($GlideFlies) {
    $script:Flying = $true
    $hasFly = [bool](Get-Command Add-PetFly -ErrorAction SilentlyContinue)
    foreach ($f in 0..3) {
        $draw = if ($hasFly) { { Add-PetFly $f }.GetNewClosure() } else { { Add-Pet -Eyes open -Tail $f }.GetNewClosure() }
        Save-Frame "glide_$f.png" -Dy -1 $draw
    }
    $script:Flying = $false
}

# Climb and ClimbDown (the same climb upside down), and Ceiling (the walk upside down). The climb is the walk turned a quarter
# turn clockwise, so the pet's nose points up and its belly is against the left edge; a pet that hops climbs by hopping.
foreach ($i in 0..3) {
    if ($WalkHops) {
        $pose = $hopPoses[$i]
        $draw = { Add-PetSide $pose[0] $pose[1] }.GetNewClosure()
        # On the wall the ground is the left edge, so the spring of a hop lifts the pet away from it, to the right.
        $turn = 90 + $pose[2]
        Save-Frame "climb_$i.png" -Dx (-$pose[3]) -Tilt $turn -PivotY 32 $draw
        Save-Frame "climbdown_$i.png" -Flip -Dx (-$pose[3]) -Tilt $turn -PivotY 32 $draw
        Save-Frame "ceiling_$i.png" -Flip -Mirror -Dy $pose[3] -Tilt $pose[2] $draw
    } else {
        $pose = $walkPoses[$i]
        $draw = { Add-PetSide $pose[0] $pose[1] }.GetNewClosure()
        Save-Frame "climb_$i.png" -Tilt 90 -PivotY 32 $draw
        Save-Frame "climbdown_$i.png" -Flip -Tilt 90 -PivotY 32 $draw
        Save-Frame "ceiling_$i.png" -Flip -Mirror -Dy $pose[2] $draw
    }
}

# Idle looks: the eyes follow the mouse pointer.
Save-Frame 'idle_left.png'  { Add-Pet -EyeShift -1.4 }
Save-Frame 'idle_right.png' { Add-Pet -EyeShift 1.4 }
Save-Frame 'idle_up.png'    { Add-Pet -EyeLift -1.3 }
Save-Frame 'idle_down.png'  { Add-Pet -EyeLift 1.3 }

# The situations the pet reacts to (writing code, reading, meetings and so on).
function Add-ScenarioPet { Add-Pet @args }
. (Join-Path $PSScriptRoot 'PetVectorScenarios.ps1')

if ($null -eq $GlideGrips) {
    $GlideGrips = foreach ($arm in $script:armSpec.OverL, $script:armSpec.OverR) { , @($arm[$arm.Count - 2], ($arm[$arm.Count - 1] - 1)) }
}
$glideGripsJson = Format-GlideGrips $GlideGrips -1
$glideLine = if ($GlideFlies) {
    '"Glide":   { "fps": 8, "frames": ["glide_0.png", "glide_1.png", "glide_2.png", "glide_3.png"], "flies": true },'
} else {
    "`"Glide`":   { `"fps`": 4, `"frames`": [`"glide_0.png`", `"glide_1.png`"], `"grips`": $glideGripsJson },"
}
$sleepFramesJson = (0..($sleepCount - 1) | ForEach-Object { "`"sleep_$_.png`"" }) -join ', '
$sleepFps = if ($sleepCount -gt 2) { 2 } else { 1 }
$hopLine = if ($HasHop) { "    `"Hop`":     { `"fps`": 8, `"frames`": [`"hop_0.png`", `"hop_1.png`", `"hop_2.png`", `"hop_3.png`", `"hop_4.png`"] },`n" } else { '' }
$manifest = @"
{
  "name": "$PetName",
  "names": { "en": "$($PetNames.En)", "zh": "$($PetNames.Zh)", "zh-Hant": "$(if ($PetNames.ZhHant) { $PetNames.ZhHant } else { $PetNames.Zh })" },
  "frameSize": [64, 64],
  "states": {
    "Idle":    { "fps": 2, "frames": ["idle_0.png", "idle_1.png", "idle_2.png", "idle_3.png"] },
    "Walk":    { "fps": $WalkFps, "frames": ["walk_0.png", "walk_1.png", "walk_2.png", "walk_3.png"] },
    "Sleep":   { "fps": $sleepFps, "frames": [$sleepFramesJson] },
    "Drag":    { "fps": 6, "frames": ["drag_0.png", "drag_1.png"] },
    $glideLine
    "Tired":   { "fps": 2, "frames": ["tired_0.png", "tired_1.png"] },
    "Alert":   { "fps": 4, "frames": ["alert_0.png", "alert_1.png"] },
    "Happy":   { "fps": 5, "frames": ["happy_0.png", "happy_1.png"] },
    "Welcome": { "fps": 4, "frames": ["welcome_0.png", "welcome_1.png", "welcome_2.png", "welcome_3.png"] },
    "Meal":    { "fps": 3, "frames": ["meal_0.png", "meal_1.png", "meal_2.png", "meal_3.png"] },
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

# The tray icon: the head, as boxed by the generator in $IconBox (drawing units: x, y, width, height), cut from a portrait
# without the tail and the front limbs (the packing below deletes the portrait frame).
Save-Frame 'icon_portrait.png' { Add-Pet -Portrait }
Save-HdIcon (Join-Path $outDir 'icon_portrait.png') (Join-Path $outDir 'icon.png') $IconBox

# Pack the single frames into the atlas the program reads.
. (Join-Path $PSScriptRoot 'PackPetAtlas.ps1')
Invoke-PetAtlasPacking $outDir

Write-Host "$PetName pet written to $outDir"
