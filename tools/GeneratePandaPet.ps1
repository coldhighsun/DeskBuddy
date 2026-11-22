# Generates the panda pet into pets\panda: smooth vector art (see PetVectorDrawing.ps1), no third-party artwork. The animation
# set comes from PetVectorAnimations.ps1; this script only draws the poses. Run: powershell -NoProfile -File tools/GeneratePandaPet.ps1
. (Join-Path $PSScriptRoot 'PetVectorDrawing.ps1')

$PetName = 'panda'
$HasHop = $false
# 熊猫 spelled with code points, so the script does not depend on its own file encoding.
$PetNames = @{ En = 'Panda'; Zh = ([string][char]0x718A + [string][char]0x732B); ZhHant = ([string][char]0x718A + [string][char]0x8C93) }
$IconBox = @(12, 9, 40, 40)
$pal = @{
    Outline  = New-HdColor 38 36 46
    Body     = New-HdColor 252 252 248
    BodyDark = New-HdColor 62 62 72
    Belly    = New-HdColor 250 250 244
    Paw      = New-HdColor 62 62 72
    Black    = New-HdColor 24 24 32
}
# Small light eyes with a dark pupil, because they sit in the dark patches.
$EyeKeys = @{ Fill = 'Belly'; Pupil = 'Black'; Lid = 'Belly'; Brow = 'Belly'; FillRx = 2.1; FillRy = 2.5; PupilRx = 1.4; PupilRy = 1.7 }
# The front legs are black, so the arms are too.
$Arms = @{ Color = 'BodyDark'; Paw = 'BodyDark' }

# The tail, a small white tuft peeking out beside the haunch, wagging with Phase (0-3).
function Add-PetBack([int]$Phase) {
    Add-HdOval 'Body' (46.8 + @(0, 0.8, 1.6, 0.8)[$Phase]) 57.4 3.2 2.8
}

# The body sitting as a real panda sits (see Add-HdSittingTorso): a white torso with a black band over the shoulders, and
# black legs.
function Add-PetBody([bool]$LeftDown, [bool]$RightDown) {
    Add-HdSittingTorso 'Body' ''
    Add-HdStroke (Get-HdBezier 23.4 43.6 27 46 37 46 40.6 43.6 8) 'BodyDark' 3
    # Only the hind feet reach the ground; the short front legs hang from the shoulders and rest on the belly.
    Add-HdSittingLegs $false $false 'BodyDark' 'Paw'
    foreach ($arm in @(@($LeftDown, -1), @($RightDown, 1))) {
        if (-not $arm[0]) { continue }
        $side = $arm[1]
        Add-HdJoinedLimb @((32 + $side * 6.6), 45, (32 + $side * 4.4), 51.4) 'BodyDark' 4.8
        Add-HdOval 'BodyDark' (32 + $side * 4.4) 52 3.2 2.6
    }
}

# The head: round black ears, a white face with the two slanted black patches round the eyes, a nose and a mouth.
function Add-PetHead {
    Add-HdOval 'BodyDark' 19.4 17.8 5 4.8
    Add-HdOval 'BodyDark' 44.6 17.8 5 4.8
    Add-HdOval 'Body' 32 27.5 15.5 11.5
    Add-HdOval 'BodyDark' 24.4 27.2 4.6 5.6 '' 0 22
    Add-HdOval 'BodyDark' 39.6 27.2 4.6 5.6 '' 0 -22
    Add-HdOval 'Outline' 32 31.8 2.3 1.6 ''
    Add-HdStroke @(32, 33.2, 32, 35) 'Outline' 0.8
    Add-HdStroke (Get-HdBezier 28.8 35.4 30.4 37 33.6 37 35.2 35.4 6) 'Outline' 0.8
}

# The panda lying down in a loaf, head on its black paws.
function Add-PetLie {
    Add-HdOval 'Body' 59.8 49.4 2.7 2.4
    Add-HdOval 'Body' 37 50 22 10.6
    Add-HdOval 'BodyDark' 50 56 6.2 4.6
    Add-HdOval 'BodyDark' 11.2 41.4 3.8 3.6
    Add-HdOval 'BodyDark' 26.8 41.4 3.8 3.6
    Add-HdOval 'Body' 19 49 12.5 9.6
    Add-HdOval 'BodyDark' 14.4 48.8 3.8 4.4 '' 0 20
    Add-HdOval 'BodyDark' 23.6 48.8 3.8 4.4 '' 0 -20
    Add-HdOval 'Outline' 19 53.4 1.9 1.3 ''
    Add-HdOval 'BodyDark' 12.2 59.6 3.8 2.4
    Add-HdOval 'BodyDark' 25.8 59.6 3.8 2.4
}

# The panda seen from the side, facing left, walking: D (-3 to 3) swings the legs, Lift raises the tail tuft.
function Add-PetSide([double]$D, [double]$Lift) {
    $swing = $D * 2.2
    Add-HdOval 'Body' 54 (46 - $Lift * 0.4) 3.2 2.8
    foreach ($leg in @(@(31, (-$swing)), @(39, $swing))) {
        $foot = $leg[0] + $leg[1]
        Add-HdLimb @($leg[0], 54, $foot, 59.5) 'BodyDark' 4.4
        Add-HdOval 'BodyDark' ($foot - 1) 60.4 3.2 2.2
    }
    Add-HdOval 'Body' 35 49 16.5 10
    foreach ($leg in @(@(26, $swing), @(43, (-$swing)))) {
        $foot = $leg[0] + $leg[1]
        Add-HdLimb @($leg[0], 54, $foot, 59.5) 'BodyDark' 4.6
        Add-HdOval 'BodyDark' ($foot - 1) 60.4 3.4 2.3
    }
    # Head in profile with the snout pointing left, resting low on the shoulders; the far ear peeks out behind the near one.
    Add-HdOval 'BodyDark' 26 29.6 3.2 3.2
    Add-HdOval 'BodyDark' 21.6 28.8 3.7 3.7
    Add-HdOval 'Belly' 11.2 41.2 5 3.2
    Add-HdOval 'Body' 18 37 10.5 8.8
    Add-HdOval 'Belly' 14.4 42.2 6 3 ''
    Add-HdOval 'BodyDark' 13.4 35.6 3 3.8 '' 0 18
    Add-HdOval 'Belly' 12.8 35.2 1.3 1.7 ''
    Add-HdOval 'Black' 12.4 35.3 0.8 1.2 ''
    Add-HdOval 'Outline' 6.8 40.4 1.8 1.4 ''
    Add-HdStroke (Get-HdBezier 7 42.4 9 44.2 11 44.2 12.6 43.4 6) 'Outline' 0.7
}

# A stalk of bamboo held in the paw, eaten down to a stub.
function Add-PetFood([int]$Len) {
    if ($Len -gt 0) {
        $end = 49 + 1.8 * $Len
        Add-HdStroke @(49, 25.5, $end, 25.5) 'Outline' 4
        Add-HdStroke @(49, 25.5, $end, 25.5) 'Green' 2.6
        for ($x = 52.4; $x -lt $end - 1; $x += 3.6) { Add-HdStroke @($x, 24.1, $x, 26.9) 'Outline' 0.7 }
    } else {
        Add-HdStroke @(49, 25.5, 51.4, 25.5) 'Outline' 4
        Add-HdStroke @(49, 25.5, 51.4, 25.5) 'Green' 2.6
    }
}

. (Join-Path $PSScriptRoot 'PetVectorAnimations.ps1')
