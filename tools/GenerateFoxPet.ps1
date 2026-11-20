# Generates the fox pet into pets\fox: a smooth, anti-aliased vector pet drawn at four times the resolution of its frame
# size (a 256x256 atlas cell for a 64x64 frame), so it stays sharp on high-DPI screens at the same size on screen.
# It defines the everyday and screen-edge states only; the scenario states (coding, reading, ...) and Hop, Meal are left to
# the fallback rules. Run: powershell -NoProfile -File tools/GenerateFoxPet.ps1
. (Join-Path $PSScriptRoot 'PetVectorDrawing.ps1')

$PetName = 'fox'
# 狐狸 spelled with code points, so the script does not depend on its own file encoding.
$PetNames = @{ En = 'Fox'; Zh = ([string][char]0x72D0 + [string][char]0x72F8) }

$pal = @{
    Outline  = New-HdColor 84 44 24
    Body     = New-HdColor 240 138 52
    BodyDark = New-HdColor 204 102 36
    White    = New-HdColor 255 246 232
    Paw      = New-HdColor 92 54 38
    Ear      = New-HdColor 96 58 42
    Black    = New-HdColor 44 28 24
    Pink     = New-HdColor 255 140 150
    Heart    = New-HdColor 255 96 128
    Red      = New-HdColor 232 60 60
    Blue     = New-HdColor 110 178 255
    Shine    = New-HdColor 255 255 255
    Sleepy   = New-HdColor 120 150 215
}
$textFont = New-Object System.Drawing.Font ('Arial', [single]9, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)

$outDir = Join-Path $PSScriptRoot "..\pets\$PetName"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Get-ChildItem $outDir -Filter *.png | Remove-Item

# Renders one frame. Dy lifts it (negative is up), Tilt turns it clockwise about the middle, Flip turns it upside down and
# Mirror left to right (a side view is drawn facing left).
function Save-Frame([string]$Name, [scriptblock]$Draw, [double]$Dy = 0, [double]$Tilt = 0, [switch]$Flip, [switch]$Mirror) {
    $bmp = New-HdCanvas -Dy $Dy -Tilt $Tilt
    & $Draw
    Save-HdCanvas $bmp (Join-Path $outDir $Name) -Flip:$Flip -Mirror:$Mirror
}

# A bushy tail along a curve (given as the four Bezier points), orange with a white tip.
function Add-FoxTail([double[]]$P) {
    $curve = Get-HdBezier $P[0] $P[1] $P[2] $P[3] $P[4] $P[5] $P[6] $P[7] 28
    $tipFrom = 2 * [int](28 * 0.68)
    Add-HdStroke $curve 'Outline' 11.4
    Add-HdStroke $curve 'Body' 9.6
    Add-HdStroke $curve[$tipFrom..($curve.Count - 1)] 'White' 9.6
}

# A pointed ear with its dark inside; Xy are the three corners of the outer ear, Inner the three of the inside.
function Add-FoxEar([double[]]$Xy, [double[]]$Inner) {
    Add-HdPoly 'Body' $Xy 'Outline' 1.2 -Smooth
    Add-HdPoly 'Ear' $Inner '' 0 -Smooth
}

# One pair of eyes: Eyes is open | closed | happy | tired | surprised; Shift and Lift move the pupils.
function Add-FoxEyes([double]$Lx, [double]$Rx, [double]$Y, [string]$Eyes, [double]$Shift = 0, [double]$Lift = 0) {
    foreach ($x in $Lx, $Rx) {
        switch ($Eyes) {
            'open' {
                Add-HdOval 'Black' ($x + $Shift) ($Y + $Lift) 2.5 3.3 ''
                Add-HdOval 'Shine' ($x + $Shift + 0.8) ($Y + $Lift - 1.2) 0.95 1.15 ''
            }
            'closed' { Add-HdStroke @(($x - 2.6), $Y, $x, ($Y + 1.3), ($x + 2.6), $Y) 'Black' 1.3 }
            'happy'  { Add-HdStroke @(($x - 2.6), ($Y + 1.6), $x, ($Y - 1.2), ($x + 2.6), ($Y + 1.6)) 'Black' 1.4 }
            'tired' {
                Add-HdOval 'Black' $x ($Y + 0.9) 2.5 1.7 ''
                Add-HdStroke @(($x - 3), ($Y - 0.6), ($x + 3), ($Y - 0.6)) 'Black' 1.5
            }
            'surprised' {
                Add-HdOval 'Shine' $x $Y 3.2 3.9 'Outline' 0.9
                Add-HdOval 'Black' $x $Y 1.5 1.9 ''
            }
        }
    }
    if ($Eyes -eq 'happy') {
        Add-HdOval 'Pink' ($Lx - 3.4) ($Y + 3.6) 2.4 1.4 ''
        Add-HdOval 'Pink' ($Rx + 3.4) ($Y + 3.6) 2.4 1.4 ''
    }
}

# The tail's far tip for each swing phase of a pet sitting seen from the front.
$tailTips = @(@(52, 25), @(54, 29), @(55, 34), @(53, 28))

# The pet sitting, seen from the front. ArmsUp raises both front paws, RightArmUp only the right one (RightArmDx sways it, for
# a wave), LeftArmReach stretches the left one out to the edge of the frame to hold on to the screen, HoldOverhead raises both
# above the head to the handle of an umbrella or parachute (drawn by the program). Portrait leaves out the tail, for the
# tray icon, so nothing but the head shows at the edges of the icon box.
function Add-Fox {
    param(
        [string]$Eyes = 'open', [int]$Tail = 0, [double]$EyeShift = 0, [double]$EyeLift = 0,
        [switch]$ArmsUp, [switch]$RightArmUp, [double]$RightArmDx = 0, [switch]$LeftArmReach, [double]$ReachY = 26,
        [switch]$HoldOverhead, [switch]$Portrait
    )
    $tip = $tailTips[$Tail % 4]
    if (-not $Portrait) { Add-FoxTail @(40, 54, 58, 58, 58, 40, $tip[0], $tip[1]) }

    # Body and belly.
    Add-HdOval 'Body' 32 49 13.5 12
    Add-HdOval 'White' 32 52 8 9 ''

    # Legs that are down (a hind foot shows where a front leg is raised), then the raised arms.
    $leftDown = -not $ArmsUp -and -not $LeftArmReach -and -not $HoldOverhead
    $rightDown = -not $ArmsUp -and -not $RightArmUp -and -not $HoldOverhead
    foreach ($leg in @(@(25, $leftDown), @(39, $rightDown))) {
        if ($leg[1] -or $HoldOverhead) {
            Add-HdLimb @($leg[0], 48, $leg[0], 58) 'Body' 5.4
            Add-HdOval 'Paw' $leg[0] 59.4 4.1 2.7
        } else {
            Add-HdOval 'Paw' ($leg[0] + ($leg[0] - 32) * 0.4) 59.8 4.7 2.6
        }
    }
    if ($HoldOverhead) {
        Add-HdLimb @(21, 46, 10, 26, 27, 8) 'Body' 4.8
        Add-HdLimb @(43, 46, 54, 26, 37, 8) 'Body' 4.8
        Add-HdOval 'Paw' 27 7 3 3.1
        Add-HdOval 'Paw' 37 7 3 3.1
    }
    if ($ArmsUp -or $RightArmUp) {
        $x = 53 + $RightArmDx
        Add-HdLimb @(42, 46, $x, 34) 'Body' 5
        Add-HdOval 'Paw' $x 33 3.1 3.1
    }
    if ($ArmsUp) {
        Add-HdLimb @(22, 46, 11, 34) 'Body' 5
        Add-HdOval 'Paw' 11 33 3.1 3.1
    }
    if ($LeftArmReach) {
        Add-HdLimb @(22, 46, 3, $ReachY) 'Body' 5
        Add-HdOval 'Paw' 3.2 $ReachY 3.1 3.1
    }

    # Head: ears, cheeks, face.
    Add-FoxEar @(17, 22, 15, 3, 29, 15) @(19.2, 17.5, 18.2, 8, 25.5, 14.5)
    Add-FoxEar @(47, 22, 49, 3, 35, 15) @(44.8, 17.5, 45.8, 8, 38.5, 14.5)
    Add-HdPoly 'White' @(17, 29, 13, 34.5, 22, 37.5, 32, 41, 42, 37.5, 51, 34.5, 47, 29, 40, 33, 32, 35, 24, 33) 'Outline' 1.2
    Add-HdOval 'Body' 32 27 15.5 11.5
    Add-HdOval 'White' 32 33.8 9 5.6 ''
    Add-FoxEyes 24.5 39.5 26.5 $Eyes $EyeShift $EyeLift
    Add-HdOval 'Black' 32 31.6 2.3 1.7 ''
    Add-HdStroke @(32, 33, 32, 34.8) 'Black' 0.9
    Add-HdStroke @(29.2, 35.6, 32, 36.6, 34.8, 35.6) 'Black' 0.9
}

# The pet lying down. Eyes: closed | tired.
function Add-FoxLying([string]$Eyes = 'closed') {
    Add-HdOval 'Body' 36 50 23 11.2
    Add-HdOval 'White' 38 55 14 5 ''
    Add-FoxTail @(54, 53, 62, 56, 48, 59, 28, 57.2)
    Add-FoxEar @(9, 43, 7, 28, 20, 36) @(10.4, 39, 9.6, 32, 16, 36.5)
    Add-FoxEar @(22, 37, 32, 28, 31, 43) @(24.4, 37, 29.8, 31.6, 29.4, 40)
    Add-HdOval 'Body' 19 49 12.5 9.6
    Add-HdOval 'White' 19 54.5 8 4.6 ''
    Add-FoxEyes 14 24 48.4 $Eyes
    Add-HdOval 'Black' 19 52.2 2 1.5 ''
    if ($Eyes -eq 'tired') { Add-HdOval 'Blue' 31 41 1.6 2.2 'Outline' 0.5 }
}

# The pet seen from the side facing left. D (-3 to 3) swings the legs, TipY is how high the tail tip is held.
function Add-FoxSide([double]$D, [double]$TipY) {
    Add-FoxTail @(44, 46, 54, 48, 58, 37, 55, $TipY)
    # Legs on the far side first, a shade darker, then the body, then the near legs.
    $swing = $D * 2.2
    foreach ($leg in @(@(30, (-$swing)), @(38, $swing))) {
        $foot = $leg[0] + $leg[1]
        Add-HdLimb @($leg[0], 50, $foot, 59.5) 'BodyDark' 4.4
        Add-HdOval 'Paw' ($foot - 1) 60.4 3.2 2.2
    }
    Add-HdOval 'Body' 34 46 16.5 10
    Add-HdOval 'White' 31 52 10.5 3.6 ''
    foreach ($leg in @(@(25, $swing), @(42, (-$swing)))) {
        $foot = $leg[0] + $leg[1]
        Add-HdLimb @($leg[0], 50, $foot, 59.5) 'Body' 4.6
        Add-HdOval 'Paw' ($foot - 1) 60.4 3.4 2.3
    }
    # Head with the muzzle pointing left.
    Add-FoxEar @(19, 27, 22, 13, 28, 25) @(21, 23.5, 22.6, 17, 25.6, 23.5)
    Add-HdOval 'White' 7 37.5 6.4 3.8
    Add-HdOval 'Body' 16 34 10.5 8.8
    Add-HdOval 'White' 13.5 39.6 6.4 3.4 ''
    Add-FoxEar @(10, 28, 10, 12, 20, 25) @(11.6, 24.5, 11.6, 16.4, 17, 23.5)
    Add-HdOval 'Black' 11.2 32 1.5 2.1 ''
    Add-HdOval 'Shine' 11.7 31.2 0.55 0.7 ''
    Add-HdOval 'Black' 1.6 36.6 1.8 1.5 ''
}

# A tail-sway phase for every frame of a sitting animation.
Save-Frame 'idle_0.png' { Add-Fox -Tail 0 }
Save-Frame 'idle_1.png' { Add-Fox -Tail 1 }
Save-Frame 'idle_2.png' { Add-Fox -Tail 2 }
Save-Frame 'idle_3.png' { Add-Fox -Tail 1 -Eyes closed }

# Walk: seen from the side, drawn facing left and mirrored; the body bounces a little as the legs swing.
Save-Frame 'walk_0.png' -Mirror { Add-FoxSide -3 24 }
Save-Frame 'walk_1.png' -Mirror -Dy -0.8 { Add-FoxSide 0 26 }
Save-Frame 'walk_2.png' -Mirror { Add-FoxSide 3 24 }
Save-Frame 'walk_3.png' -Mirror -Dy -0.8 { Add-FoxSide 0 22 }

# Sleep and tired: lying down.
Save-Frame 'sleep_0.png' { Add-FoxLying }
Save-Frame 'sleep_1.png' { Add-FoxLying; Add-HdText 'z' 'Sleepy' 44 15; Add-HdText 'Z' 'Sleepy' 51 5 }
Save-Frame 'tired_0.png' { Add-FoxLying -Eyes tired }
Save-Frame 'tired_1.png' { Add-FoxLying -Eyes tired; Add-HdOval 'Blue' 31 37 1.6 2.2 'Outline' 0.5 }

# Drag: lifted, paws up and surprised.
Save-Frame 'drag_0.png' { Add-Fox -Eyes surprised -ArmsUp -Tail 0 }
Save-Frame 'drag_1.png' { Add-Fox -Eyes surprised -ArmsUp -Tail 2 }

# Alert: jumps with wide eyes and an exclamation mark.
Save-Frame 'alert_0.png' -Dy -2 { Add-Fox -Eyes surprised -ArmsUp -Tail 1; Add-HdText '!' 'Red' 50 3 }
Save-Frame 'alert_1.png' { Add-Fox -Eyes surprised -ArmsUp -Tail 3; Add-HdText '!' 'Red' 50 3 }

# Happy: bounces with smiling eyes and a heart.
Save-Frame 'happy_0.png' -Dy -2 { Add-Fox -Eyes happy -ArmsUp -Tail 1; Add-HdHeart 'Heart' 56 8 3.4 }
Save-Frame 'happy_1.png' { Add-Fox -Eyes happy -ArmsUp -Tail 3 }

# Welcome: waves hello with one paw, hearts floating up.
foreach ($f in 0..3) {
    $dx = @(0, 2, 0, -2)[$f]
    $hearts = ($f % 2 -eq 0)
    $draw = { Add-Fox -Eyes happy -RightArmUp -RightArmDx $dx -Tail $f; if ($hearts) { Add-HdHeart 'Heart' 57 6 3.2 } }.GetNewClosure()
    Save-Frame "welcome_$f.png" $draw
}

# Glide: floating down holding the handle of an umbrella or parachute (drawn by the program), legs dangling.
Save-Frame 'glide_0.png' -Dy -1 { Add-Fox -Eyes surprised -HoldOverhead -Tail 0 }
Save-Frame 'glide_1.png' -Dy -1 { Add-Fox -Eyes surprised -HoldOverhead -Tail 2 }

# Hang: clings to the left edge of the screen by one outstretched paw. Mirrored for the right edge.
Save-Frame 'hang_0.png' -Dy -1 { Add-Fox -LeftArmReach -Tail 0 }
Save-Frame 'hang_1.png' -Dy -1 { Add-Fox -LeftArmReach -Tail 2 -ReachY 28 }

# Climb and ClimbDown (the same climb upside down), and Ceiling (the walk upside down). The climb is the walk turned a quarter
# turn clockwise, so its nose points up and its belly is against the left edge.
$walkPoses = @(@(-3, 24), @(0, 26), @(3, 24), @(0, 22))
foreach ($i in 0..3) {
    $d = $walkPoses[$i][0]; $tip = $walkPoses[$i][1]
    $draw = { Add-FoxSide $d $tip }.GetNewClosure()
    Save-Frame "climb_$i.png" -Tilt 90 $draw
    Save-Frame "climbdown_$i.png" -Tilt 90 -Flip $draw
    Save-Frame "ceiling_$i.png" -Flip -Mirror $draw
}

# Idle looks: the pupils follow the mouse pointer.
Save-Frame 'idle_left.png'  { Add-Fox -EyeShift -1.4 }
Save-Frame 'idle_right.png' { Add-Fox -EyeShift 1.4 }
Save-Frame 'idle_up.png'    { Add-Fox -EyeLift -1.3 }
Save-Frame 'idle_down.png'  { Add-Fox -EyeLift 1.3 }

$manifest = @"
{
  "name": "$PetName",
  "names": { "en": "$($PetNames.En)", "zh": "$($PetNames.Zh)" },
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
"@

Write-PetManifest (Join-Path $outDir 'manifest.json') $manifest

# The tray icon: the head (drawing units: x, y, width, height).
Save-Frame 'icon_portrait.png' { Add-Fox -Portrait }
Save-HdIcon (Join-Path $outDir 'icon_portrait.png') (Join-Path $outDir 'icon.png') @(12, 2, 40, 40)

# Pack the single frames into the atlas the program reads.
. (Join-Path $PSScriptRoot 'PackPetAtlas.ps1')
Invoke-PetAtlasPacking $outDir

Write-Host "$PetName pet written to $outDir"
