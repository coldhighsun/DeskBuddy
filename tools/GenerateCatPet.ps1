# Generates the cat pet into pets\cat: smooth vector art (see PetVectorDrawing.ps1), no third-party artwork. The animation set
# comes from PetVectorAnimations.ps1; this script only draws the poses. Run: powershell -NoProfile -File tools/GenerateCatPet.ps1
. (Join-Path $PSScriptRoot 'PetVectorDrawing.ps1')

$PetName = 'cat'
$HasHop = $true
# 猫 spelled with a code point, so the script does not depend on its own file encoding.
$PetNames = @{ En = 'Cat'; Zh = [string][char]0x732B }
$IconBox = @(12, 3, 40, 40)
$pal = @{
    Outline    = New-HdColor 78 46 32
    Body       = New-HdColor 242 164 76
    BodyDark   = New-HdColor 206 110 46
    Belly      = New-HdColor 255 241 216
    Paw        = New-HdColor 255 241 216
    EyeG       = New-HdColor 112 204 120
    Black      = New-HdColor 36 28 30
}
# Green eyes with a narrow pupil.
$EyeKeys = @{ Fill = 'EyeG'; Pupil = 'Black'; Lid = 'Outline'; Brow = 'BodyDark'; FillRx = 2.8; FillRy = 3.4; PupilRx = 1; PupilRy = 2.9 }

# The tail, a curve from the base to a tip that sways with Phase (0-3).
function Add-PetBack([int]$Phase) {
    $tip = @(@(54, 23), @(56, 26), @(58, 29), @(56, 26))[$Phase]
    $curve = Get-HdBezier 41 57 55 60 59 42 $tip[0] $tip[1] 24
    Add-HdStroke $curve 'Outline' 6.4
    Add-HdStroke $curve 'Body' 4.7
    Add-HdStroke $curve[24..($curve.Count - 1)] 'BodyDark' 4.7
}

# The body sitting as a real cat sits (see Add-HdSittingTorso), with stripes on the sides and the haunches.
function Add-PetBody([bool]$LeftDown, [bool]$RightDown) {
    Add-HdSittingTorso
    foreach ($side in -1, 1) {
        Add-HdStroke @((32 + $side * 11.2), 42.6, (32 + $side * 8.4), 43.4) 'BodyDark' 1.5
        Add-HdStroke (Get-HdBezier (32 + $side * 13.6) 50.2 (32 + $side * 12.2) 49.6 (32 + $side * 10.6) 50.2 (32 + $side * 9.8) 51.4 6) 'BodyDark' 1.4
        Add-HdStroke (Get-HdBezier (32 + $side * 14.4) 54 (32 + $side * 13.2) 53.2 (32 + $side * 11.4) 53.6 (32 + $side * 10.6) 54.8 6) 'BodyDark' 1.4
    }
    Add-HdSittingLegs $LeftDown $RightDown
}

# The head: pointed ears with a pink inside, a striped forehead, a cream muzzle, a nose and whiskers.
function Add-PetHead {
    Add-HdPoly 'Body' @(17.5, 24, 18.4, 4.8, 29.5, 16.5) 'Outline' 1.2 -Smooth -Tension 0.3
    Add-HdPoly 'Pink' @(20.4, 19.5, 20.8, 10, 26.4, 16.6) '' 0 -Smooth -Tension 0.3
    Add-HdPoly 'Body' @(46.5, 24, 45.6, 4.8, 34.5, 16.5) 'Outline' 1.2 -Smooth -Tension 0.3
    Add-HdPoly 'Pink' @(43.6, 19.5, 43.2, 10, 37.6, 16.6) '' 0 -Smooth -Tension 0.3
    Add-HdOval 'Body' 32 27.5 15.5 11.5
    Add-HdStroke @(32, 16.8, 32, 21) 'BodyDark' 1.4
    Add-HdStroke @(27.6, 17.8, 28.6, 21.4) 'BodyDark' 1.3
    Add-HdStroke @(36.4, 17.8, 35.4, 21.4) 'BodyDark' 1.3
    Add-HdOval 'Belly' 32 34 8.2 4.8 ''
    Add-HdOval 'Pink' 32 31 1.9 1.3 ''
    Add-HdStroke @(32, 32.2, 32, 34) 'Outline' 0.8
    Add-HdStroke (Get-HdBezier 29 34.2 30.4 36.2 33.6 36.2 35 34.2 6) 'Outline' 0.8
    foreach ($side in -1, 1) {
        Add-HdStroke @((32 + $side * 9.5), 32.6, (32 + $side * 18.5), 31) 'BodyDark' 0.7
        Add-HdStroke @((32 + $side * 9.5), 34.4, (32 + $side * 18.5), 36) 'BodyDark' 0.7
    }
}

# The cat lying down in a loaf, head on its paws, tail across the front.
function Add-PetLie {
    Add-HdOval 'Body' 37 50 22 10.6
    foreach ($x in 34, 40, 46, 52) { Add-HdStroke @($x, 41.2, ($x - 1), 44.6) 'BodyDark' 1.4 }
    $tail = Get-HdBezier 58 54 63 61 46 63 30 60.6 20
    Add-HdStroke $tail 'Outline' 5.8
    Add-HdStroke $tail 'Body' 4.1
    Add-HdStroke $tail[0..9] 'BodyDark' 4.1
    Add-HdOval 'Belly' 12 59.4 4.4 2.5
    Add-HdPoly 'Body' @(8.6, 45, 8, 32.8, 18.4, 40.6) 'Outline' 1.2 -Smooth -Tension 0.3
    Add-HdPoly 'Pink' @(10.6, 41, 10.6, 36, 14.6, 40) '' 0 -Smooth -Tension 0.3
    Add-HdPoly 'Body' @(29.4, 45, 30, 32.8, 19.6, 40.6) 'Outline' 1.2 -Smooth -Tension 0.3
    Add-HdPoly 'Pink' @(27.4, 41, 27.4, 36, 23.4, 40) '' 0 -Smooth -Tension 0.3
    Add-HdOval 'Body' 19 49 12.5 9.6
    Add-HdOval 'Belly' 19 54.4 7.2 4 ''
    Add-HdOval 'Pink' 19 52.4 1.9 1.3 ''
    Add-HdStroke @(19, 40.8, 19, 43.8) 'BodyDark' 1.3
    foreach ($side in -1, 1) {
        Add-HdStroke @((19 + $side * 8), 53, (19 + $side * 16), 52) 'BodyDark' 0.7
        Add-HdStroke @((19 + $side * 8), 54.6, (19 + $side * 16), 56) 'BodyDark' 0.7
    }
}

# The cat seen from the side, facing left, walking: D (-3 to 3) swings the legs, Lift raises the tip of the tail.
function Add-PetSide([double]$D, [double]$Lift) {
    $swing = $D * 2.2
    $tailTip = 24 - $Lift
    $curve = Get-HdBezier 46 44 55 44 59 36 57 $tailTip 22
    Add-HdStroke $curve 'Outline' 6.2
    Add-HdStroke $curve 'Body' 4.5
    Add-HdStroke $curve[22..($curve.Count - 1)] 'BodyDark' 4.5
    foreach ($leg in @(@(30, (-$swing)), @(38, $swing))) {
        $foot = $leg[0] + $leg[1]
        Add-HdLimb @($leg[0], 50, $foot, 59.5) 'BodyDark' 4.2
        Add-HdOval 'Paw' ($foot - 1) 60.4 3.2 2.2
    }
    Add-HdOval 'Body' 34 46 16.5 10
    Add-HdOval 'Belly' 30 52 10.5 3.6 ''
    foreach ($x in 30, 35, 40) { Add-HdStroke @($x, 36.8, ($x - 0.6), 40.4) 'BodyDark' 1.4 }
    foreach ($leg in @(@(25, $swing), @(42, (-$swing)))) {
        $foot = $leg[0] + $leg[1]
        Add-HdLimb @($leg[0], 50, $foot, 59.5) 'Body' 4.4
        Add-HdOval 'Paw' ($foot - 1) 60.4 3.4 2.3
    }
    # Head in profile with the muzzle pointing left. The ears stand up on top of the head behind the eye: the far one,
    # darker, peeks out behind the near one, and of the near one mostly the furred back shows, with the pink inside only as
    # a sliver along its front edge.
    Add-HdPoly 'BodyDark' @(20.5, 27.5, 26, 14, 28.4, 27) 'Outline' 1.2 -Smooth -Tension 0.3
    Add-HdPoly 'Body' @(14.4, 28.4, 20.4, 13.8, 25, 26.4) 'Outline' 1.2 -Smooth -Tension 0.3
    Add-HdPoly 'Pink' @(16.2, 26, 19.8, 16.6, 18.4, 26) '' 0 -Smooth -Tension 0.3
    Add-HdOval 'Belly' 7.4 38.2 6 3.5
    Add-HdOval 'Body' 16 34 10.5 8.8
    Add-HdOval 'Belly' 11.6 39.4 6 3.3 ''
    Add-HdOval 'EyeG' 10.8 32 2 2.6 ''
    Add-HdOval 'Black' 10.4 32 0.8 2.3 ''
    Add-HdOval 'Pink' 2.4 36.4 1.6 1.2 ''
    Add-HdStroke @(8, 38.6, 1, 37.6) 'BodyDark' 0.6
    Add-HdStroke @(8, 40.2, 1.6, 41.8) 'BodyDark' 0.6
}

# A fish held in the paw, nibbled down to the bone.
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
