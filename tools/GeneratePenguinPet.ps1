# Generates the penguin pet into pets\penguin: smooth vector art (see PetVectorDrawing.ps1), no third-party artwork. The
# animation set comes from PetVectorAnimations.ps1; this script only draws the poses.
# Run: powershell -NoProfile -File tools/GeneratePenguinPet.ps1
. (Join-Path $PSScriptRoot 'PetVectorDrawing.ps1')

$PetName = 'penguin'
$HasHop = $false
# 企鹅 spelled with code points, so the script does not depend on its own file encoding.
$PetNames = @{ En = 'Penguin'; Zh = ([string][char]0x4F01 + [string][char]0x9E45) }
$IconBox = @(13, 10, 38, 38)
$HeadBox = @(16, 13, 48, 40)
$pal = @{
    Outline  = New-HdColor 22 26 46
    Body     = New-HdColor 58 70 104
    BodyDark = New-HdColor 36 42 70
    Belly    = New-HdColor 246 248 252
    Paw      = New-HdColor 58 70 104
    Accent   = New-HdColor 255 172 54
    Black    = New-HdColor 16 16 26
}
# Light eyes with dark pupils, because the head is dark.
$SitEyes = @{ Lx = 25; Rx = 39; Y = 25.5 }
$EyeKeys = @{ Fill = 'Belly'; Pupil = 'Black'; Lid = 'Belly'; Brow = 'Outline' }
$Arms = @{
    Paw = 'Body'; Width = 5.4; PawRx = 2.9; PawRy = 3.1
    ShoulderL = @(18, 40); ShoulderR = @(46, 40); UpL = @(7, 32); UpR = @(57, 32); ReachFrom = @(18, 40)
    OverL = @(17, 38, 6, 25, 26, 7); OverR = @(47, 38, 58, 25, 38, 7)
}

# The short tail peeking out at the back, wagging with Phase (0-3).
function Add-PetBack([int]$Phase) {
    Add-HdPoly 'BodyDark' @(44, 54, (54 + $Phase % 2), (58 + $Phase % 2), 44, 60) 'Outline' 1.1 -Smooth -Tension 0.3
}

# The egg-shaped body with a white belly, orange feet, and flippers that hang at the sides unless they are raised.
function Add-PetBody([bool]$LeftDown, [bool]$RightDown) {
    Add-HdOval 'Accent' 25 60.8 5.8 2.2
    Add-HdOval 'Accent' 39 60.8 5.8 2.2
    Add-HdOval 'Body' 32 37.2 16.6 24.2
    Add-HdOval 'Belly' 32 46.5 11.2 14.6 ''
    if ($LeftDown) { Add-HdOval 'Body' 12.6 44 3.7 11 'Outline' 1.1 8 }
    if ($RightDown) { Add-HdOval 'Body' 51.4 44 3.7 11 'Outline' 1.1 -8 }
}

# Gliding: both flippers raised up and out from the shoulders, holding on to the umbrella or the parachute with their tips
# (the manifest's Glide grips, a little inside the tips so the flippers cover the ends of the shaft and the strings).
$GlideGrips = @(@(5.6, 19.6), @(58.4, 19.6))
function Add-PetHoldOverhead {
    Add-HdOval 'Body' 10.8 27 3.7 11 'Outline' 1.1 -35
    Add-HdOval 'Body' 53.2 27 3.7 11 'Outline' 1.1 35
}

# The face: the beak between the light eyes.
function Add-PetHead {
    Add-HdPoly 'Accent' @(28, 29.4, 36, 29.4, 32, 34.8) 'Outline' 1 -Smooth -Tension 0.25
    Add-HdStroke @(28.6, 31.4, 35.4, 31.4) 'Outline' 0.5
}

# The penguin lying on its belly, flippers back.
function Add-PetLie {
    Add-HdOval 'Accent' 60.4 56.2 3.6 2.3
    Add-HdOval 'Body' 37 50 22 10.6
    Add-HdOval 'BodyDark' 43 47.4 11 3.4 'Outline' 1.1 8
    Add-HdOval 'Body' 19 49 12.5 9.6
    Add-HdOval 'Belly' 22 55.2 8.6 3.4 ''
    Add-HdPoly 'Accent' @(15.6, 51.2, 22.4, 51.2, 19, 55) 'Outline' 0.9 -Smooth -Tension 0.25
}

# The penguin seen from the side, facing left, waddling: D (-3 to 3) swings the feet and the flipper, Lift raises the tail.
function Add-PetSide([double]$D, [double]$Lift) {
    $swing = $D * 2.2
    Add-HdPoly 'BodyDark' @(41, 52, 52, (55 - $Lift * 0.5), 42, 58) 'Outline' 1.1 -Smooth -Tension 0.3
    Add-HdOval 'Accent' (22 - $swing) 60.8 5.6 2.2
    Add-HdOval 'Accent' (36 + $swing) 60.8 5.6 2.2
    Add-HdOval 'Body' 30 37.2 15 24.2
    Add-HdOval 'Belly' 22.8 46 6.6 14 ''
    Add-HdOval 'Body' (34 - $swing * 0.3) 42 3.6 10.4 'Outline' 1.1 (4 + $D * 3)
    Add-HdPoly 'Accent' @(16.4, 27.4, 5, 30.6, 16.4, 33.4) 'Outline' 1 -Smooth -Tension 0.25
    Add-HdOval 'Belly' 20 25 2.6 3 ''
    Add-HdOval 'Black' 19.4 25.2 1.4 1.8 ''
}

# A fish held in the flipper, eaten down to the bone.
function Add-PetFood([int]$Len) {
    if ($Len -gt 0) {
        $center = 48 + $Len
        if ($Len -le 5) { Add-HdPoly 'Blue' @((49 + 2 * $Len), 25, (52.4 + 2 * $Len), 21.6, (52.4 + 2 * $Len), 28.4) 'Outline' 0.8 }
        Add-HdOval 'Blue' $center 25 ($Len + 1) 3.1 'Outline' 0.9
        Add-HdOval 'White' ($center - $Len + 0.4) 24.3 0.8 0.8 ''
    } else {
        Add-HdStroke @(49, 25, 56, 25) 'White' 1
        Add-HdStroke @(51, 22.8, 51, 27.2) 'White' 0.8
        Add-HdStroke @(53.4, 23, 53.4, 27) 'White' 0.8
    }
}

. (Join-Path $PSScriptRoot 'PetVectorAnimations.ps1')
