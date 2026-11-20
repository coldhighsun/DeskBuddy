# Generates the dog pet into pets\dog: smooth vector art (see PetVectorDrawing.ps1), no third-party artwork. The animation set
# comes from PetVectorAnimations.ps1; this script only draws the poses. Run: powershell -NoProfile -File tools/GenerateDogPet.ps1
. (Join-Path $PSScriptRoot 'PetVectorDrawing.ps1')

$PetName = 'dog'
$HasHop = $true
# 狗 spelled with a code point, so the script does not depend on its own file encoding.
$PetNames = @{ En = 'Dog'; Zh = [string][char]0x72D7 }
$IconBox = @(9, 9, 46, 46)
# Gliding, the paws hold on outside the floppy ears.
$Arms = @{ OverL = @(22, 46, 6.6, 33); OverR = @(42, 46, 57.4, 33) }
$pal = @{
    Outline  = New-HdColor 84 54 36
    Body     = New-HdColor 224 170 102
    BodyDark = New-HdColor 156 102 58
    Belly    = New-HdColor 252 238 210
    Paw      = New-HdColor 252 238 210
    Collar   = New-HdColor 226 62 62
    Nose     = New-HdColor 40 28 28
    Black    = New-HdColor 40 28 28
}

# The tail, wagging with Phase (0-3).
function Add-PetBack([int]$Phase) {
    $tip = @(@(53, 41), @(55, 44), @(56, 48), @(54, 43))[$Phase]
    $curve = Get-HdBezier 40 57 50 58 56 52 $tip[0] $tip[1] 20
    Add-HdStroke $curve 'Outline' 6
    Add-HdStroke $curve 'Body' 4.3
    Add-HdOval 'BodyDark' $tip[0] $tip[1] 2.3 2.3 ''
}

# The body sitting as a real dog sits (see Add-HdSittingTorso), with a cream bib and the collar.
function Add-PetBody([bool]$LeftDown, [bool]$RightDown) {
    Add-HdSittingTorso
    Add-HdSittingLegs $LeftDown $RightDown
    Add-HdStroke (Get-HdBezier 21 37 24 43.5 40 43.5 43 37 12) 'Outline' 4.2
    Add-HdStroke (Get-HdBezier 21 37 24 43.5 40 43.5 43 37 12) 'Collar' 2.6
}

# The head: floppy ears, a patch round one eye, a cream muzzle with a nose and a little tongue.
function Add-PetHead {
    Add-HdOval 'BodyDark' 14.2 28 5.2 10.5 'Outline' 1.2 10
    Add-HdOval 'BodyDark' 49.8 28 5.2 10.5 'Outline' 1.2 -10
    Add-HdOval 'Body' 32 27 15.5 12.5
    Add-HdOval 'BodyDark' 22.5 25.5 5.6 6.2 '' 0 -12
    Add-HdOval 'Belly' 32 33.6 9 6.3 ''
    Add-HdOval 'Pink' 32 37.2 1.9 2.2 ''
    Add-HdOval 'Nose' 32 30.8 2.7 2 ''
    Add-HdStroke @(32, 32.4, 32, 34.4) 'Nose' 0.9
    Add-HdStroke (Get-HdBezier 28 34.4 29.5 36.8 34.5 36.8 36 34.4 6) 'Nose' 0.9
}

# The dog lying down, head on its paws.
function Add-PetLie {
    Add-HdOval 'Body' 37 50 22 10.8
    Add-HdOval 'Body' 46 52 9.5 7.8
    $curve = Get-HdBezier 56 53 62 52 62 47 58 43 14
    Add-HdStroke $curve 'Outline' 5.6
    Add-HdStroke $curve 'Body' 3.9
    Add-HdOval 'Belly' 26 59.2 5 2.7
    Add-HdOval 'Belly' 12 59.2 5 2.7
    Add-HdStroke @(30, 42, 33, 51, 31, 58) 'Outline' 4
    Add-HdStroke @(30, 42, 33, 51, 31, 58) 'Collar' 2.4
    Add-HdOval 'BodyDark' 8.4 47 4.2 8.4 'Outline' 1.1 16
    Add-HdOval 'BodyDark' 29.6 47.6 4.2 8 'Outline' 1.1 -14
    Add-HdOval 'Body' 19 49 12.5 9.6
    Add-HdOval 'Belly' 19 54.2 7.6 4.6 ''
    Add-HdOval 'Nose' 19 52.6 2.4 1.7 ''
}

# The dog seen from the side, facing left, walking: D (-3 to 3) swings the legs, Lift raises the tip of the tail.
function Add-PetSide([double]$D, [double]$Lift) {
    $swing = $D * 2.2
    $tailTip = 31 - $Lift
    $curve = Get-HdBezier 47 43 54 42 58 38 56 $tailTip 18
    Add-HdStroke $curve 'Outline' 6
    Add-HdStroke $curve 'Body' 4.3
    Add-HdOval 'BodyDark' 56 $tailTip 2.3 2.3 ''
    foreach ($leg in @(@(30, (-$swing)), @(38, $swing))) {
        $foot = $leg[0] + $leg[1]
        Add-HdLimb @($leg[0], 50, $foot, 59.5) 'BodyDark' 4.4
        Add-HdOval 'Paw' ($foot - 1) 60.4 3.2 2.2
    }
    Add-HdOval 'Body' 34 46 16.5 10
    Add-HdOval 'Belly' 31 52 10.5 3.6 ''
    Add-HdOval 'BodyDark' 36 42 4.6 3.2 '' 0 -10
    foreach ($leg in @(@(25, $swing), @(42, (-$swing)))) {
        $foot = $leg[0] + $leg[1]
        Add-HdLimb @($leg[0], 50, $foot, 59.5) 'Body' 4.6
        Add-HdOval 'Paw' ($foot - 1) 60.4 3.4 2.3
    }
    # The collar, then the head with the muzzle pointing left and the floppy ear.
    Add-HdLimb @(21, 28, 24, 41) 'Collar' 2.4
    Add-HdOval 'Belly' 8 37.6 7 4.2
    Add-HdOval 'Body' 16 34 10.5 8.8
    Add-HdOval 'Belly' 13.5 39.6 6.4 3.4 ''
    Add-HdOval 'BodyDark' 21 34 4.5 8.4 'Outline' 1.1 -10
    Add-HdOval 'Black' 11.2 31.6 1.5 2.1 ''
    Add-HdOval 'Shine' 11.7 30.8 0.55 0.7 ''
    Add-HdOval 'Nose' 1.9 36.2 1.9 1.6 ''
}

# A bone held in the paw, gnawed down to crumbs.
function Add-PetFood([int]$Len) {
    if ($Len -gt 0) {
        $end = 47 + 2 * $Len
        Add-HdLimb @(47, 25, $end, 25) 'White' 2.6
        Add-HdOval 'White' 46.4 23.6 2 2 'Outline' 0.9
        Add-HdOval 'White' 46.4 26.4 2 2 'Outline' 0.9
        if ($Len -ge 5) {
            Add-HdOval 'White' ($end + 0.6) 23.6 2 2 'Outline' 0.9
            Add-HdOval 'White' ($end + 0.6) 26.4 2 2 'Outline' 0.9
        }
    } else {
        Add-HdStroke @(48, 26, 52, 26) 'White' 1.2
    }
}

. (Join-Path $PSScriptRoot 'PetVectorAnimations.ps1')
