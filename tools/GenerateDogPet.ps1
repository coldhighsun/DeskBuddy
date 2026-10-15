# Generates the pixel-art dog pet (no third-party artwork). Art faces right.
# Usage: powershell -NoProfile -File tools/GenerateDogPet.ps1
. (Join-Path $PSScriptRoot 'PetDrawing.ps1')

$PetName = 'dog'
$HasHop = $true
$PetNames = @{ En = 'Dog'; Zh = '狗' }
$IconBox = @(6, 7, 20, 20)
$pal = @{
    Outline  = New-Brush 52 34 28
    Body     = New-Brush 214 160 96
    BodyDark = New-Brush 140 90 52
    Belly    = New-Brush 252 238 210
    Paw      = New-Brush 252 238 210
    Pink     = New-Brush 255 140 160
    Iris     = New-Brush 120 80 50
    EyeWhite = New-Brush 245 247 250
    Lid      = New-Brush 52 34 28
    Black    = New-Brush 24 22 28
    Whisker  = New-Brush 240 230 215
    White    = New-Brush 240 244 250
    Blue     = New-Brush 110 170 230
    Red      = New-Brush 235 60 60
    Yellow   = New-Brush 255 210 70
    Accent   = New-Brush 235 60 60
}
$textFont = New-Object System.Drawing.Font 'Consolas', 16, ([System.Drawing.FontStyle]::Bold)

# Looking up from the side: long muzzle at the top, a floppy ear hanging at the back of the head.
$ClimbHead = @(
    'XXXkkXXXX.....',
    'XcccooooX.....',
    'XcccokkoX.....',
    'XcccoggoX.....',
    'XcccooooXX....',
    'XoooooooXdX...',
    'XoooooooXddX..',
    'XoooooooXdX...',
    'XXXXXXXXXX....'
)

# The tail, wagging with Phase (0-3).
function Add-PetBack([int]$Phase) {
    $tipX = @(27, 28, 29, 28)[$Phase]
    $tipY = @(22, 21, 22, 23)[$Phase]
    Add-Limb 22 27 26 25 Outline Body
    Add-Limb 26 25 $tipX $tipY Outline Body
    Px BodyDark $tipX $tipY 2 2
}

# Body with a cream belly and a collar, front legs, and the head with floppy ears and a muzzle.
function Add-PetBody([bool]$LeftDown, [bool]$RightDown) {
    Px Outline 8 24 16 6; Px Body 9 25 14 4
    Px Outline 9 17 14 8; Px Body 10 18 12 7
    Px Belly 13 20 6 9
    Px BodyDark 10 21 2 2; Px BodyDark 20 22 2 2
    if ($LeftDown) { Px Outline 11 23 4 7; Px Belly 12 24 2 5 }
    if ($RightDown) { Px Outline 17 23 4 7; Px Belly 18 24 2 5 }
    Px Red 12 17 8 1
    # Floppy ears behind the head, then the head.
    Px Outline 5 7 5 10; Px BodyDark 6 8 3 8
    Px Outline 22 7 5 10; Px BodyDark 23 8 3 8
    Px Outline 8 6 16 11; Px Body 9 7 14 9
    Px BodyDark 9 7 3 3; Px Belly 14 7 4 2
    Px Belly 12 12 8 4; Px Black 15 12 2 2
    Px Outline 15 14 2 1; Px Outline 14 15 1 1; Px Outline 17 15 1 1; Px Pink 15 15 2 1
}

# The dog lying down, head on its paws.
function Add-PetLie {
    Px Outline 5 19 22 11; Px Body 6 20 20 9
    Px BodyDark 14 21 3 2; Px BodyDark 20 22 3 2
    Px Outline 22 25 8 4; Px Body 23 26 6 2; Px BodyDark 28 26 2 2
    Px Outline 2 15 5 8; Px BodyDark 3 16 3 6
    Px Outline 15 15 5 8; Px BodyDark 16 16 3 6
    Px Outline 4 14 14 11; Px Body 5 15 12 9
    Px Belly 8 20 6 4; Px Black 10 20 2 2
    Px Red 5 24 12 1
}

# The dog seen from the side, facing left, walking: D (-3 to 3) swings the legs, TipY is the height of the tail's tip.
function Add-PetSide([int]$D, [int]$TipY) {
    Add-Limb 11 22 (11 - $D) 27 Outline BodyDark
    Add-Limb 22 22 (22 + $D) 27 Outline BodyDark
    Add-Limb 24 18 27 14 Outline Body
    Add-Limb 27 14 29 $TipY Outline Body
    Px BodyDark 29 $TipY 2 2

    Px Outline 8 15 17 9; Px Body 9 16 15 7
    Px Belly 9 19 4 4
    Px BodyDark 16 16 3 3; Px BodyDark 20 18 3 2
    Px Red 12 15 2 4

    Px Outline 1 9 13 10; Px Body 2 10 11 8
    Px Belly 2 14 5 4; Px Black 1 13 2 2
    Px Iris 5 12 2 2; Px Black 5 12 1 2
    Px Outline 8 8 5 10; Px BodyDark 9 9 3 8

    Add-Limb 10 22 (10 + $D) 27 Outline Body
    Add-Limb 21 22 (21 - $D) 27 Outline Body
}

# A bone held in the paw, gnawed down to crumbs.
function Add-PetFood([int]$Len) {
    if ($Len -gt 0) {
        Px Outline 23 10 ($Len + 1) 4; Px White 24 11 ($Len - 1) 2
        if ($Len -ge 5) { Px White 24 10 1 1; Px White 24 13 1 1; Px White 31 10 1 1; Px White 31 13 1 1 }
    } else {
        Px White 24 12 5 1
    }
}

. (Join-Path $PSScriptRoot 'PetAnimations.ps1')
