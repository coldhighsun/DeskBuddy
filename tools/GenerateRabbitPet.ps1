# Generates the pixel-art rabbit pet (no third-party artwork). Art faces right.
# Usage: powershell -NoProfile -File tools/GenerateRabbitPet.ps1
. (Join-Path $PSScriptRoot 'PetDrawing.ps1')

$PetName = 'rabbit'
$HasHop = $false
$WalkHops = $true
$ClimbFromWalk = $true
$WalkFps = 6
$PetNames = @{ En = 'Rabbit'; Zh = '兔子' }
$IconBox = @(8, 2, 16, 18)
$pal = @{
    Outline  = New-Brush 70 55 80
    Body     = New-Brush 236 230 240
    BodyDark = New-Brush 190 180 200
    Belly    = New-Brush 255 252 255
    Paw      = New-Brush 255 252 255
    Pink     = New-Brush 255 150 175
    Iris     = New-Brush 200 70 90
    EyeWhite = New-Brush 255 255 255
    Lid      = New-Brush 70 55 80
    Black    = New-Brush 24 22 28
    Whisker  = New-Brush 225 220 235
    White    = New-Brush 240 244 250
    Blue     = New-Brush 110 170 230
    Red      = New-Brush 255 70 70
    Yellow   = New-Brush 255 210 70
    Accent   = New-Brush 245 140 50
    Green    = New-Brush 90 190 90
}
$textFont = New-Object System.Drawing.Font 'Consolas', 16, ([System.Drawing.FontStyle]::Bold)

# Looking up from the side: the long ear lies back along the head.
$ClimbHead = @(
    'XXXpXXXXX......',
    'XcccooooX......',
    'XcccokkoX......',
    'XcccoggoX......',
    'XcccooooXXXXXXX',
    'XoooooooXoooopX',
    'XoooooooXXXXXXX',
    'XoooooooX......',
    'XXXXXXXXX......'
)

# The fluffy tail, bobbing with Phase (0-3).
function Add-PetBack([int]$Phase) {
    $y = 24 + @(0, 1, 0, -1)[$Phase]
    Px Outline 21 $y 7 6; Px Belly 22 ($y + 1) 5 4
}

# Body, front legs, and the head with long upright ears.
function Add-PetBody([bool]$LeftDown, [bool]$RightDown) {
    Px Outline 8 24 16 6; Px Body 9 25 14 4
    Px Outline 9 17 14 8; Px Body 10 18 12 7
    Px Belly 13 20 6 9
    if ($LeftDown) { Px Outline 11 23 4 7; Px Belly 12 24 2 5 }
    if ($RightDown) { Px Outline 17 23 4 7; Px Belly 18 24 2 5 }
    # Long ears, then the head.
    Px Outline 10 0 5 9; Px Body 11 1 3 8; Px Pink 12 2 1 6
    Px Outline 17 0 5 9; Px Body 18 1 3 8; Px Pink 19 2 1 6
    Px Outline 8 7 16 10; Px Body 9 8 14 8
    Px Belly 13 12 6 4
    Px Pink 15 12 2 1
    Px Outline 15 13 2 1; Px Outline 14 14 1 1; Px Outline 17 14 1 1
    Px White 15 14 2 2; Px Outline 16 14 1 2
    Px Whisker 5 12 3 1; Px Whisker 5 14 3 1; Px Whisker 24 12 3 1; Px Whisker 24 14 3 1
}

# The rabbit lying down, ears flat along its back.
function Add-PetLie {
    Px Outline 5 19 22 11; Px Body 6 20 20 9
    Px Outline 22 22 8 8; Px Belly 23 23 6 6
    Px Outline 9 10 14 5; Px Body 10 11 12 3; Px Pink 12 12 8 1
    Px Outline 4 14 14 11; Px Body 5 15 12 9
    Px Belly 9 20 4 3; Px Pink 10 20 2 1
    Px Whisker 1 19 3 1; Px Whisker 1 21 3 1; Px Whisker 18 19 3 1; Px Whisker 18 21 3 1
}

# The rabbit seen from the side, facing left, in one of three poses of a hop chosen by D: 0 crouched on its haunches (a
# big thigh above a long flat hind foot), below 0 launched (the hind leg stretched out behind as thigh, shin and a long
# foot), above 0 tucked in the air. The front legs are short in every pose. TipY (8-12) bobs the tail.
function Add-PetSide([int]$D, [int]$TipY) {
    # Fluffy tail at the rump.
    Px Outline 24 ($TipY + 4) 5 5; Px Belly 25 ($TipY + 5) 3 3

    Px Outline 8 15 17 9; Px Body 9 16 15 7
    Px Belly 9 19 4 4

    # Head in profile with a long ear laid back along the body.
    Px Outline 2 9 12 10; Px Body 3 10 10 8
    Px Belly 3 14 4 4; Px Pink 2 14 1 1
    Px Iris 4 12 2 2; Px Black 4 12 1 2
    Px Whisker 7 14 4 1; Px Whisker 7 16 3 1
    Add-Limb 9 9 13 1 Outline Body
    Px Pink 11 3 1 4

    if ($D -eq 0) {
        # Crouched: the thigh sits on a long hind foot, the short front paws on the ground.
        Px Outline 16 16 10 10; Px Body 17 17 8 8; Px BodyDark 19 20 4 4
        Px Outline 12 26 15 4; Px Body 13 27 13 2
        Px Outline 8 23 5 7; Px Body 9 24 3 5
    } elseif ($D -lt 0) {
        # Launched: the hind leg stretched back, thigh then shin then foot, the front legs stretched forward but short.
        Add-Limb 19 19 23 21 Outline Body 4; Add-Limb 23 22 27 24 Outline Body 1; Add-Limb 27 24 31 25 Outline Belly 1
        Add-Limb 10 21 7 24 Outline Body
    } else {
        # Tucked: the thigh and the folded foot under the body, the front legs reaching down to land.
        Px Outline 17 16 8 8; Px Body 18 17 6 6; Px BodyDark 19 19 3 3
        Add-Limb 17 24 24 26 Outline Body
        Add-Limb 10 21 7 25 Outline Body
    }
}

# A carrot held in the paw, nibbled down to the stalk.
function Add-PetFood([int]$Len) {
    if ($Len -gt 0) {
        Px Outline 23 10 ($Len + 1) 4; Px Accent 24 11 ($Len - 1) 2
        if ($Len -ge 5) { Px Green 31 10 1 1; Px Green 31 13 1 1 }
    } else {
        Px Green 24 12 5 1
    }
}

. (Join-Path $PSScriptRoot 'PetAnimations.ps1')
