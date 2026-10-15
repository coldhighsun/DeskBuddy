# Generates the pixel-art penguin pet (no third-party artwork). Art faces right.
# Usage: powershell -NoProfile -File tools/GeneratePenguinPet.ps1
. (Join-Path $PSScriptRoot 'PetDrawing.ps1')

$PetName = 'penguin'
$HasHop = $false
$ClimbFromWalk = $true
$PetNames = @{ En = 'Penguin'; Zh = '企鹅' }
$IconBox = @(8, 7, 16, 16)
$pal = @{
    Outline  = New-Brush 20 24 42
    Body     = New-Brush 52 62 92
    BodyDark = New-Brush 32 38 64
    Belly    = New-Brush 245 247 250
    Paw      = New-Brush 52 62 92
    Pink     = New-Brush 255 140 160
    Iris     = New-Brush 235 242 255
    EyeWhite = New-Brush 255 255 255
    Lid      = New-Brush 235 242 255
    Black    = New-Brush 14 14 22
    Whisker  = New-Brush 200 210 225
    White    = New-Brush 240 244 250
    Blue     = New-Brush 110 170 230
    Red      = New-Brush 255 70 70
    Yellow   = New-Brush 255 210 70
    Accent   = New-Brush 255 170 50
}
$textFont = New-Object System.Drawing.Font 'Consolas', 16, ([System.Drawing.FontStyle]::Bold)

# The short tail peeking out at the back, wagging with Phase (0-3).
function Add-PetBack([int]$Phase) {
    Px Outline 22 26 (3 + $Phase % 2) 3; Px BodyDark 22 27 (2 + $Phase % 2) 1
}

# The egg-shaped body with a white belly, flippers at the sides unless raised, orange feet, a dark head and a beak.
function Add-PetBody([bool]$LeftDown, [bool]$RightDown) {
    Px Outline 8 9 16 21; Px Body 9 10 14 19
    Px Outline 10 5 12 5; Px Body 11 6 10 4
    Px Belly 11 15 10 14; Px Belly 12 13 8 2
    Px Accent 9 28 5 2; Px Accent 18 28 5 2
    if ($LeftDown) { Px Outline 5 14 4 11; Px Body 6 15 2 9 }
    if ($RightDown) { Px Outline 23 14 4 11; Px Body 24 15 2 9 }
    # Beak.
    Px Outline 14 11 4 3; Px Accent 15 12 2 1; Px Accent 14 13 4 1
    Px Outline 15 14 2 1
}

# The penguin lying on its belly, flippers back.
function Add-PetLie {
    Px Outline 5 19 22 11; Px Body 6 20 20 9
    Px BodyDark 18 22 8 4
    Px Accent 27 27 3 2
    Px Outline 4 14 14 11; Px Body 5 15 12 9
    Px Belly 7 21 8 3
    Px Outline 9 20 4 3; Px Accent 10 20 2 2
}

# The penguin seen from the side, facing left, waddling: D (-3 to 3) swings the feet, TipY (9-12) bobs the tail.
function Add-PetSide([int]$D, [int]$TipY) {
    # Tail and far flipper.
    Px Outline 20 ($TipY + 12) 4 3; Px BodyDark 20 ($TipY + 13) 3 1
    Add-Limb 15 14 (18 + $D) 21 Outline BodyDark

    Add-Dome 6 5 15 23
    Px Belly 7 11 5 16
    Px Outline 3 11 5 3; Px Accent 3 12 4 1

    # Eye.
    Px White 8 9 2 2; Px Black 8 9 1 2

    # Near flipper and feet.
    Add-Limb 13 14 (15 - $D) 22 Outline Body
    Px Accent (7 - $D) 28 5 2; Px Accent (14 + $D) 28 5 2
}

# A fish held in the flipper, eaten down to the bone.
function Add-PetFood([int]$Len) {
    if ($Len -gt 0) {
        Px Outline 23 10 ($Len + 1) 4; Px Blue 24 11 ($Len - 1) 2
        if ($Len -ge 5) { Px Blue 31 10 1 1; Px Blue 31 13 1 1; Px White 25 11 1 1 }
    } else {
        Px White 24 12 5 1; Px White 28 11 1 3
    }
}

. (Join-Path $PSScriptRoot 'PetAnimations.ps1')
