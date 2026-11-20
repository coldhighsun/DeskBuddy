# Generates the rabbit pet into pets\rabbit: smooth vector art (see PetVectorDrawing.ps1), no third-party artwork. The animation
# set comes from PetVectorAnimations.ps1; this script only draws the poses.
# Run: powershell -NoProfile -File tools/GenerateRabbitPet.ps1
. (Join-Path $PSScriptRoot 'PetVectorDrawing.ps1')

$PetName = 'rabbit'
$HasHop = $false
$WalkHops = $true
$WalkFps = 6
# 兔子 spelled with code points, so the script does not depend on its own file encoding.
$PetNames = @{ En = 'Rabbit'; Zh = ([string][char]0x5154 + [string][char]0x5B50) }
$IconBox = @(13, 1, 38, 38)
$HeadBox = @(17, 16, 47, 39)
$pal = @{
    Outline  = New-HdColor 100 80 112
    Body     = New-HdColor 240 234 244
    BodyDark = New-HdColor 204 192 216
    Belly    = New-HdColor 255 252 255
    Paw      = New-HdColor 255 252 255
    Pink     = New-HdColor 255 150 175
    Accent   = New-HdColor 245 140 50
    Black    = New-HdColor 56 40 68
}

# The fluffy tail, bobbing with Phase (0-3).
function Add-PetBack([int]$Phase) {
    Add-HdOval 'Belly' 48 (56 + @(0, 1, 0, -1)[$Phase]) 5.4 5.2
}

# The body with the big thighs and hind feet peeking out at the sides, and the front legs that are not raised.
function Add-PetBody([bool]$LeftDown, [bool]$RightDown) {
    Add-HdOval 'Body' 19.5 55.5 7.6 6.2
    Add-HdOval 'Body' 44.5 55.5 7.6 6.2
    Add-HdOval 'Belly' 17.5 60.6 6.6 2.7
    Add-HdOval 'Belly' 46.5 60.6 6.6 2.7
    Add-HdOval 'Body' 32 49 13.5 12
    Add-HdOval 'Belly' 32 52.5 8 8.5 ''
    foreach ($leg in @(@(27, $LeftDown), @(37, $RightDown))) {
        if ($leg[1]) {
            Add-HdLimb @($leg[0], 48, $leg[0], 58) 'Body' 4.8
            Add-HdOval 'Paw' $leg[0] 59.4 3.6 2.6
        }
    }
}

# The head: long upright ears with a pink inside, round cheeks, a nose and whiskers.
function Add-PetHead {
    Add-HdOval 'Body' 24.5 12.8 4.7 11.4 'Outline' 1.2 -6
    Add-HdOval 'Pink' 24.5 13.5 2.2 8.6 '' 0 -6
    Add-HdOval 'Body' 39.5 12.8 4.7 11.4 'Outline' 1.2 6
    Add-HdOval 'Pink' 39.5 13.5 2.2 8.6 '' 0 6
    Add-HdOval 'Body' 32 28 15.2 11.6
    Add-HdOval 'Belly' 32 33.6 8.4 5.4 ''
    Add-HdOval 'Pink' 32 31 1.9 1.4 ''
    Add-HdStroke @(32, 32.2, 32, 33.6) 'Outline' 0.8
    Add-HdStroke (Get-HdBezier 29 34 30.4 36 33.6 36 35 34 6) 'Outline' 0.8
    foreach ($side in -1, 1) {
        Add-HdStroke @((32 + $side * 9), 32.4, (32 + $side * 17.5), 31) 'BodyDark' 0.7
        Add-HdStroke @((32 + $side * 9), 34.2, (32 + $side * 17.5), 35.6) 'BodyDark' 0.7
    }
}

# The rabbit lying down, its ears flat along its back.
function Add-PetLie {
    Add-HdOval 'Belly' 58 53.5 5.2 5
    Add-HdOval 'Body' 37 51 22 10.4
    Add-HdOval 'Belly' 50 59.6 8 2.8
    Add-HdOval 'Body' 35 41.4 12.5 3.6 'Outline' 1.1 -6
    Add-HdOval 'Pink' 35 41.4 8.4 1.3 '' 0 -6
    Add-HdOval 'Belly' 12 59.3 4.6 2.6
    Add-HdOval 'Body' 19 49 12.5 9.6
    Add-HdOval 'Belly' 19 54.2 7 4.4 ''
    Add-HdOval 'Pink' 19 52.2 1.9 1.4 ''
}

# The rabbit seen from the side, facing left, in one of three poses of a hop chosen by D: 0 crouched on its haunches (a big
# thigh above a long flat hind foot), below 0 launched (the hind leg stretched out behind), above 0 tucked in the air. The front
# legs are short in every pose. Lift raises the fluffy tail.
function Add-PetSide([double]$D, [double]$Lift) {
    Add-HdOval 'Belly' 53 (44 - $Lift * 0.5) 4.8 4.6
    Add-HdOval 'Body' 36 46 15 10
    Add-HdOval 'Belly' 28 52 7 3.6 ''
    if ($D -eq 0) {
        Add-HdOval 'Body' 43 60.4 11 2.8
        Add-HdOval 'Body' 44 50 8.5 8.6
        Add-HdLimb @(20, 50, 18, 59) 'Body' 4
        Add-HdOval 'Paw' 17.2 60.2 3.3 2.2
    } elseif ($D -lt 0) {
        Add-HdOval 'Body' 46 47 7.6 5 'Outline' 1.1 24
        Add-HdLimb @(49, 49, 57, 53.5) 'Body' 3.6
        Add-HdLimb @(57, 53.5, 63, 55.5) 'Paw' 2.8
        Add-HdLimb @(21, 50, 11, 53.5) 'Body' 4
        Add-HdOval 'Paw' 9.6 54 3 2.2
    } else {
        Add-HdOval 'Body' 43 47.5 8 7.4
        Add-HdLimb @(40, 56, 53, 58.5) 'Body' 3.4
        Add-HdLimb @(20, 50, 15, 58.5) 'Body' 4
        Add-HdOval 'Paw' 14 59.6 3.2 2.2
    }
    # The head in profile with a long ear laid back along the body.
    Add-HdOval 'Body' 26 19.5 12.4 3.7 'Outline' 1.1 -33
    Add-HdOval 'Pink' 26 19.5 8.6 1.4 '' 0 -33
    Add-HdOval 'Body' 14.5 36 10.3 8.6
    Add-HdOval 'Belly' 9.5 40 5.8 3.3 ''
    Add-HdOval 'Pink' 3.9 37.4 1.8 1.4 ''
    Add-HdOval 'Black' 10.2 33.2 1.5 2.1 ''
    Add-HdOval 'Shine' 10.7 32.4 0.55 0.7 ''
    Add-HdStroke @(7, 38, 1, 37.4) 'BodyDark' 0.6
    Add-HdStroke @(7, 39.6, 1.4, 41) 'BodyDark' 0.6
}

# A carrot held in the paw, nibbled down to the leaves.
function Add-PetFood([int]$Len) {
    Add-HdStroke @(49, 25, 45.6, 22.4) 'Green' 1.6
    Add-HdStroke @(49, 25, 45, 26.6) 'Green' 1.6
    if ($Len -gt 0) {
        Add-HdPoly 'Accent' @(49, 22.4, (50 + 1.8 * $Len), 25, 49, 27.6) 'Outline' 0.9
    }
}

. (Join-Path $PSScriptRoot 'PetVectorAnimations.ps1')
