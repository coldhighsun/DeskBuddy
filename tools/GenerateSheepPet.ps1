# Generates the sheep pet into pets\sheep: smooth vector art (see PetVectorDrawing.ps1), no third-party artwork. The animation
# set comes from PetVectorAnimations.ps1; this script only draws the poses. Run: powershell -NoProfile -File tools/GenerateSheepPet.ps1
. (Join-Path $PSScriptRoot 'PetVectorDrawing.ps1')

$PetName = 'sheep'
$HasHop = $false
# 羊 spelled with a code point, so the script does not depend on its own file encoding.
$PetNames = @{ En = 'Sheep'; Zh = [string][char]0x7F8A; ZhHant = [string][char]0x7F8A }
$IconBox = @(8, 6, 48, 48)
$HeadBox = @(13, 10, 51, 39)
$pal = @{
    Outline  = New-HdColor 56 50 62
    Body     = New-HdColor 252 250 242
    BodyDark = New-HdColor 82 72 80
    Belly    = New-HdColor 240 236 226
    Paw      = New-HdColor 82 72 80
    Black    = New-HdColor 30 28 34
}
# Small light eyes with a dark pupil, because they sit in the dark face.
$SitEyes = @{ Lx = 26; Rx = 38; Y = 27.4 }
$LieEyes = @{ Lx = 14.6; Rx = 23.4; Y = 49.8 }
$LieDrop = @(31, 44)
$EyeKeys = @{ Fill = 'Belly'; Pupil = 'Black'; Lid = 'Belly'; Brow = 'Belly'; FillRx = 2.2; FillRy = 2.6; PupilRx = 1.4; PupilRy = 1.8 }
# The arms are dark, and held out lower than the ears so they do not hide them.
$Arms = @{
    Color = 'BodyDark'; Paw = 'BodyDark'; Width = 4.2; PawRx = 2.8; PawRy = 2.8
    UpL = @(8, 38); UpR = @(56, 38); OverL = @(22, 46, 11, 37); OverR = @(42, 46, 53, 37)
}

# The tail, a small tuft of wool peeking out behind the body, wagging with Phase (0-3).
function Add-PetBack([int]$Phase) {
    Add-HdOval 'Body' (47.6 + @(0, 0.8, 1.6, 0.8)[$Phase]) 55.6 3.8 3.4
}

# The body sitting: a fluffy cloud of wool, only the hind feet on the ground and a pair of short dark front legs with hooves.
function Add-PetBody([bool]$LeftDown, [bool]$RightDown) {
    Add-HdMergedOvals 'Body' @(@(32, 48, 13, 12), @(22.4, 50, 7.2, 7.6), @(41.6, 50, 7.2, 7.6), @(26, 43.4, 7, 6), @(38, 43.4, 7, 6), @(32, 55, 12, 6))
    Add-HdSittingLegs $false $false 'BodyDark' 'Paw'
    foreach ($leg in @(@(27.6, $LeftDown), @(36.4, $RightDown))) {
        if (-not $leg[1]) { continue }
        Add-HdTaperedLeg $leg[0] 54 58.4 2.6 2.2 'BodyDark'
        Add-HdOval 'Paw' $leg[0] 59.4 3.2 2.2
    }
}

# The head: a tuft of wool on top, floppy ears out to the sides, a dark face with a light nose and mouth.
function Add-PetHead {
    Add-HdOval 'BodyDark' 13.6 28.6 5.4 2.8 '' 0 24
    Add-HdOval 'BodyDark' 50.4 28.6 5.4 2.8 '' 0 -24
    Add-HdMergedOvals 'Body' @(@(25, 17.4, 5, 4.6), @(32, 14.8, 5.8, 5), @(39, 17.4, 5, 4.6))
    Add-HdOval 'BodyDark' 32 28.4 12.4 11.4
    Add-HdOval 'Belly' 32 32.6 2.4 1.5 ''
    Add-HdStroke @(32, 34, 32, 35.4) 'Belly' 0.8
    Add-HdStroke (Get-HdBezier 29.2 35.6 30.6 37 33.4 37 34.8 35.6 6) 'Belly' 0.8
}

# The sheep lying down: a heap of wool with the dark face in front, ears out to the sides and a tuft of wool on its head.
function Add-PetLie {
    Add-HdOval 'Body' 58.4 52.4 3.2 3
    Add-HdMergedOvals 'Body' @(@(37, 51, 19, 10.4), @(27, 48.6, 9, 9), @(47, 48.4, 9, 8.4), @(37, 45, 11, 7))
    Add-HdOval 'BodyDark' 8.4 49.8 4.6 2.5 '' 0 22
    Add-HdOval 'BodyDark' 29.6 49.8 4.6 2.5 '' 0 -22
    Add-HdMergedOvals 'Body' @(@(15.6, 42.2, 4.4, 3.8), @(21.6, 41.2, 4.8, 4))
    Add-HdOval 'BodyDark' 19 50.6 10.6 8.6
    Add-HdOval 'Belly' 19 54.4 2.2 1.4 ''
    Add-HdOval 'Paw' 12.4 60.2 3.4 2.2
    Add-HdOval 'Paw' 25.6 60.2 3.4 2.2
}

# The sheep seen from the side, facing left, walking: D (-3 to 3) swings the legs, Lift raises the tail tuft.
function Add-PetSide([double]$D, [double]$Lift) {
    $swing = $D * 2.2
    Add-HdOval 'Body' 53 (41.6 - $Lift * 0.4) 3.4 3.1
    foreach ($leg in @(@(32, (-$swing)), @(41, $swing))) {
        $foot = $leg[0] + $leg[1]
        Add-HdLimb @($leg[0], 53, $foot, 59) 'BodyDark' 4.2
        Add-HdOval 'Paw' ($foot - 0.6) 60 3 2.1
    }
    Add-HdMergedOvals 'Body' @(@(35, 46, 15.5, 10), @(28, 43.4, 8, 7), @(42, 43.4, 8, 7), @(35, 51.6, 13, 6.2))
    foreach ($leg in @(@(26, $swing), @(46, (-$swing)))) {
        $foot = $leg[0] + $leg[1]
        Add-HdLimb @($leg[0], 53, $foot, 59) 'BodyDark' 4.4
        Add-HdOval 'Paw' ($foot - 0.6) 60 3.2 2.2
    }
    # Head in profile with the nose pointing left, a tuft of wool on top and a floppy ear.
    Add-HdMergedOvals 'Body' @(@(15, 30.6, 4.2, 3.6), @(20.4, 30, 4.4, 3.8))
    Add-HdOval 'BodyDark' 15.6 38 8.8 7.6
    Add-HdOval 'BodyDark' 22.4 36.6 4.8 2.4 '' 0 34
    Add-HdOval 'Belly' 10.6 36.6 1.6 2 ''
    Add-HdOval 'Black' 10.2 36.6 0.7 1.1 ''
    Add-HdOval 'Belly' 6.4 40.6 1.7 1.2 ''
}

# A tuft of grass held in the paw, eaten down to a stub.
function Add-PetFood([int]$Len) {
    $h = 1 + 0.9 * $Len
    foreach ($dx in -2.4, 0, 2.4) {
        $line = @((54 + $dx), 28, (54 + 1.5 * $dx), (28 - $h))
        Add-HdStroke $line 'Outline' 2.4
        Add-HdStroke $line 'Green' 1.3
    }
}

. (Join-Path $PSScriptRoot 'PetVectorAnimations.ps1')
