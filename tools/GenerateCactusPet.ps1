# Generates the cactus pet into pets\cactus: a smooth, anti-aliased vector pet drawn at four times the resolution of its frame
# size (a 256x256 atlas cell for a 64x64 frame). It is a small cactus in a pot, seen from the front only, and defines the
# everyday states only (Idle, Walk, Sleep, Drag, Tired, Alert, Happy and the four Idle looks): everything else, including the
# screen-edge and scenario states, is left to the fallback rules. It has no Glide, so it has no grips.
# Run: powershell -NoProfile -File tools/GenerateCactusPet.ps1
. (Join-Path $PSScriptRoot 'PetVectorDrawing.ps1')

$PetName = 'cactus'
# 仙人掌 spelled with code points, so the script does not depend on its own file encoding.
$PetNames = @{ En = 'Cactus'; Zh = ([string][char]0x4ED9 + [string][char]0x4EBA + [string][char]0x638C); ZhHant = ([string][char]0x4ED9 + [string][char]0x4EBA + [string][char]0x638C) }

$pal = @{
    Outline     = New-HdColor 30 92 58
    Body        = New-HdColor 88 186 106
    BodyDark    = New-HdColor 58 150 82
    Pot         = New-HdColor 214 122 84
    PotRim      = New-HdColor 232 148 102
    PotDark     = New-HdColor 176 92 60
    Spine       = New-HdColor 255 246 200
    PetalDroop  = New-HdColor 214 176 196
    Black       = New-HdColor 30 40 34
    Pink        = New-HdColor 255 140 170
    Heart       = New-HdColor 255 96 128
    Red         = New-HdColor 232 60 60
    Blue        = New-HdColor 110 178 255
    Yellow      = New-HdColor 255 210 70
    Shine       = New-HdColor 255 255 255
    Sleepy      = New-HdColor 120 150 215
}
$textFont = New-Object System.Drawing.Font ('Arial', [single]9, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)

$outDir = Join-Path $PSScriptRoot "..\pets\$PetName"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Get-ChildItem $outDir -Filter *.png | Remove-Item

# Renders one frame. Dy lifts it (negative is up), Tilt turns it clockwise about the foot of the pot.
function Save-Frame([string]$Name, [scriptblock]$Draw, [double]$Dy = 0, [double]$Tilt = 0) {
    $bmp = New-HdCanvas -Dy $Dy -Tilt $Tilt -PivotX 32 -PivotY 56
    & $Draw
    Save-HdCanvas $bmp (Join-Path $outDir $Name)
}

# One pair of eyes: Eyes is open | closed | happy | tired | surprised; Shift and Lift move the pupils.
function Add-CactusEyes([double]$Lx, [double]$Rx, [double]$Y, [string]$Eyes, [double]$Shift = 0, [double]$Lift = 0) {
    foreach ($x in $Lx, $Rx) {
        switch ($Eyes) {
            'open' {
                Add-HdOval 'Black' ($x + $Shift) ($Y + $Lift) 2.2 2.9 ''
                Add-HdOval 'Shine' ($x + $Shift + 0.7) ($Y + $Lift - 1.1) 0.85 1 ''
            }
            'closed' { Add-HdStroke @(($x - 2.4), $Y, $x, ($Y + 1.2), ($x + 2.4), $Y) 'Black' 1.3 }
            'happy'  { Add-HdStroke @(($x - 2.4), ($Y + 1.5), $x, ($Y - 1.1), ($x + 2.4), ($Y + 1.5)) 'Black' 1.4 }
            'tired' {
                Add-HdOval 'Black' $x ($Y + 0.8) 2.2 1.5 ''
                Add-HdStroke @(($x - 2.8), ($Y - 0.5), ($x + 2.8), ($Y - 0.5)) 'Black' 1.4
            }
            'surprised' {
                Add-HdOval 'Shine' $x $Y 3 3.6 'Outline' 0.9
                Add-HdOval 'Black' $x $Y 1.4 1.8 ''
            }
        }
    }
    if ($Eyes -eq 'happy') {
        Add-HdOval 'Pink' ($Lx - 3) ($Y + 3.4) 2.2 1.3 ''
        Add-HdOval 'Pink' ($Rx + 3) ($Y + 3.4) 2.2 1.3 ''
    }
}

# A five-petalled flower centered on the given point; Droop makes it pale, as a tired cactus's flower is.
function Add-CactusFlower([double]$Cx, [double]$Cy, [bool]$Droop) {
    $petal = if ($Droop) { 'PetalDroop' } else { 'Pink' }
    foreach ($k in 0..4) {
        $angle = (-90 + 72 * $k) * [Math]::PI / 180
        Add-HdOval $petal ($Cx + 3.2 * [Math]::Cos($angle)) ($Cy + 3.2 * [Math]::Sin($angle)) 2.3 2.3 'Outline' 0.7
    }
    Add-HdOval 'Yellow' $Cx $Cy 1.9 1.9 'Outline' 0.6
}

# The cactus in its pot, seen from the front. Sway (0-3) moves the flower and the tips of the arms, ArmsUp raises both arms,
# Droop lets the flower hang, LiftL and LiftR raise a foot of the pot. Portrait leaves out the arms, for the tray icon, so
# nothing but the head shows at the edges of the icon box.
function Add-Cactus {
    param(
        [string]$Eyes = 'open', [int]$Sway = 0, [double]$EyeShift = 0, [double]$EyeLift = 0,
        [switch]$ArmsUp, [switch]$Droop, [switch]$Portrait, [double]$LiftL = 0, [double]$LiftR = 0
    )
    $a = @(0, 0.8, 0, -0.8)[$Sway % 4]
    Add-HdOval 'PotRim' 27 (61.2 - $LiftL) 3.2 1.7
    Add-HdOval 'PotRim' 37 (61.2 - $LiftR) 3.2 1.7

    $arms = @()
    if (-not $Portrait) {
        if ($ArmsUp) { $arms = @(@(24, 31, 13, 29, 13, 17), @(40, 31, 51, 29, 51, 17)) }
        else { $arms = @(@(24, 34, 14, 34, 14, (25 + $a)), @(40, 30, 50, 30, 50, (21 - $a))) }
    }
    # One silhouette: the outlines of the trunk and the arms go down first, then the fills, so no line crosses a joint.
    foreach ($arm in $arms) { Add-HdStroke ([double[]]$arm) 'Outline' 7.7 }
    Add-HdRoundRect 'Outline' 21 13 22 36 11 '' 0
    foreach ($arm in $arms) { Add-HdStroke ([double[]]$arm) 'Body' 6 }
    Add-HdRoundRect 'Body' 22 14 20 34 10 '' 0
    foreach ($x in 27.6, 36.4) {
        Add-HdStroke @($x, 17.4, $x, 21.6) 'BodyDark' 0.9
        Add-HdStroke @($x, 37.4, $x, 44) 'BodyDark' 0.9
    }
    foreach ($spine in @(@(24.6, 24.6), @(39.4, 26.6), @(24.6, 36.6), @(39.4, 38.2))) {
        Add-HdStroke @(($spine[0] - 0.9), ($spine[1] - 0.9), $spine[0], $spine[1], ($spine[0] + 0.9), ($spine[1] - 0.9)) 'Spine' 0.7
    }
    if (-not $Portrait) {
        foreach ($arm in $arms) {
            $tx = $arm[4]; $ty = $arm[5]
            Add-HdStroke @(($tx - 0.9), ($ty - 3.4), $tx, ($ty - 4.4), ($tx + 0.9), ($ty - 3.4)) 'Spine' 0.7
        }
    }

    # The pot in front of the trunk, with a stripe.
    Add-HdPoly 'Pot' @(21, 48, 43, 48, 40.4, 61, 23.6, 61) 'Outline' 1.1 -Smooth -Tension 0.12
    Add-HdStroke @(23.6, 56.2, 40.4, 56.2) 'PotDark' 1.1
    Add-HdRoundRect 'PotRim' 18.5 43.5 27 6.4 2.8 'Outline' 1.1

    if ($Droop) { Add-CactusFlower 38.4 15.2 $true } else { Add-CactusFlower (32 + $a) 11.6 $false }
    Add-CactusEyes 28 36 27 $Eyes $EyeShift $EyeLift
    Add-HdStroke (Get-HdBezier 29.4 32 30.6 33.6 33.4 33.6 34.6 32 6) 'Black' 0.9
}

# Idle: sits in its pot, swaying a little, and blinks now and then.
Save-Frame 'idle_0.png' { Add-Cactus -Sway 0 }
Save-Frame 'idle_1.png' { Add-Cactus -Sway 1 }
Save-Frame 'idle_2.png' { Add-Cactus -Sway 2 }
Save-Frame 'idle_3.png' { Add-Cactus -Sway 1 -Eyes closed }

# Walk: shuffles along on the feet of its pot, leaning from side to side.
Save-Frame 'walk_0.png' -Dy -1.4 -Tilt -5 { Add-Cactus -Sway 0 -LiftR 1.8 }
Save-Frame 'walk_1.png' -Dy -1 { Add-Cactus -Sway 1 }
Save-Frame 'walk_2.png' -Dy -1.4 -Tilt 5 { Add-Cactus -Sway 2 -LiftL 1.8 }
Save-Frame 'walk_3.png' -Dy -1 { Add-Cactus -Sway 3 }

# Sleep: eyes closed, snoring.
Save-Frame 'sleep_0.png' { Add-Cactus -Eyes closed -Sway 0 }
Save-Frame 'sleep_1.png' { Add-Cactus -Eyes closed -Sway 1; Add-HdText 'z' 'Sleepy' 46 16; Add-HdText 'Z' 'Sleepy' 52 5 }

# Tired: eyes half shut, the flower hanging, a drop of sweat.
Save-Frame 'tired_0.png' { Add-Cactus -Eyes tired -Droop; Add-HdOval 'Blue' 46 27 1.6 2.2 'Outline' 0.5 }
Save-Frame 'tired_1.png' { Add-Cactus -Eyes tired -Droop; Add-HdOval 'Blue' 46 31 1.6 2.2 'Outline' 0.5 }

# Drag: lifted, arms up and surprised.
Save-Frame 'drag_0.png' { Add-Cactus -Eyes surprised -ArmsUp -Sway 0 }
Save-Frame 'drag_1.png' { Add-Cactus -Eyes surprised -ArmsUp -Sway 2 }

# Alert: jumps with wide eyes and an exclamation mark.
Save-Frame 'alert_0.png' -Dy -2 { Add-Cactus -Eyes surprised -ArmsUp -Sway 1; Add-HdText '!' 'Red' 53 3 }
Save-Frame 'alert_1.png' { Add-Cactus -Eyes surprised -ArmsUp -Sway 3; Add-HdText '!' 'Red' 53 3 }

# Happy: bounces with smiling eyes and a heart.
Save-Frame 'happy_0.png' -Dy -2 { Add-Cactus -Eyes happy -ArmsUp -Sway 1; Add-HdHeart 'Heart' 57 8 3.4 }
Save-Frame 'happy_1.png' { Add-Cactus -Eyes happy -ArmsUp -Sway 3 }

# Idle looks: the pupils follow the mouse pointer.
Save-Frame 'idle_left.png'  { Add-Cactus -EyeShift -1.4 }
Save-Frame 'idle_right.png' { Add-Cactus -EyeShift 1.4 }
Save-Frame 'idle_up.png'    { Add-Cactus -EyeLift -1.3 }
Save-Frame 'idle_down.png'  { Add-Cactus -EyeLift 1.3 }

$manifest = @"
{
  "name": "$PetName",
  "names": { "en": "$($PetNames.En)", "zh": "$($PetNames.Zh)", "zh-Hant": "$($PetNames.ZhHant)" },
  "frameSize": [64, 64],
  "states": {
    "Idle":    { "fps": 2, "frames": ["idle_0.png", "idle_1.png", "idle_2.png", "idle_3.png"] },
    "Walk":    { "fps": 6, "frames": ["walk_0.png", "walk_1.png", "walk_2.png", "walk_3.png"] },
    "Sleep":   { "fps": 1, "frames": ["sleep_0.png", "sleep_1.png"] },
    "Drag":    { "fps": 6, "frames": ["drag_0.png", "drag_1.png"] },
    "Tired":   { "fps": 2, "frames": ["tired_0.png", "tired_1.png"] },
    "Alert":   { "fps": 4, "frames": ["alert_0.png", "alert_1.png"] },
    "Happy":   { "fps": 5, "frames": ["happy_0.png", "happy_1.png"] },
    "Idle.Left":  { "fps": 1, "frames": ["idle_left.png"] },
    "Idle.Right": { "fps": 1, "frames": ["idle_right.png"] },
    "Idle.Up":    { "fps": 1, "frames": ["idle_up.png"] },
    "Idle.Down":  { "fps": 1, "frames": ["idle_down.png"] }
  }
}
"@

Write-PetManifest (Join-Path $outDir 'manifest.json') $manifest

# The tray icon: the head (drawing units: x, y, width, height).
Save-Frame 'icon_portrait.png' { Add-Cactus -Portrait }
Save-HdIcon (Join-Path $outDir 'icon_portrait.png') (Join-Path $outDir 'icon.png') @(12, 4, 40, 40)

# Pack the single frames into the atlas the program reads.
. (Join-Path $PSScriptRoot 'PackPetAtlas.ps1')
Invoke-PetAtlasPacking $outDir

Write-Host "$PetName pet written to $outDir"
