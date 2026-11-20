# Generates the rabbit pet into pets\rabbit: smooth vector art (see PetVectorDrawing.ps1), no third-party artwork. The animation
# set comes from PetVectorAnimations.ps1; this script only draws the poses.
# The rabbit is a round caramel bunny with a cream blaze down its face, puffy cheeks and one ear that stands up while the other
# folds over at the middle; it gets about by hopping and now and then takes a big leap.
# Run: powershell -NoProfile -File tools/GenerateRabbitPet.ps1
. (Join-Path $PSScriptRoot 'PetVectorDrawing.ps1')

$PetName = 'rabbit'
$HasHop = $true
$WalkHops = $true
$WalkFps = 6
# 兔子 spelled with code points, so the script does not depend on its own file encoding.
$PetNames = @{ En = 'Rabbit'; Zh = ([string][char]0x5154 + [string][char]0x5B50) }
$IconBox = @(10, 1, 41, 41)
$HeadBox = @(16, 15, 48, 41)
$pal = @{
    Outline  = New-HdColor 92 58 44
    Body     = New-HdColor 226 178 128
    BodyDark = New-HdColor 196 142 98
    Belly    = New-HdColor 255 245 230
    Paw      = New-HdColor 255 245 230
    Pink     = New-HdColor 255 148 160
    Accent   = New-HdColor 247 140 48
    Black    = New-HdColor 58 34 28
}
$SitEyes = @{ Lx = 24.5; Rx = 39.5; Y = 26.8 }
$LieEyes = @{ Lx = 13.5; Rx = 23.5; Y = 47.6 }
$LieDrop = @(30, 39)
$Arms = @{ Width = 4.6; PawRx = 2.9; PawRy = 2.9 }

# The cotton tail, a cream puff that bobs with Phase (0-3).
function Add-PetBack([int]$Phase) {
    $bob = @(0, -1, 0, 1)[$Phase]
    Add-HdOval 'Belly' 48.5 (55.5 + $bob) 5.6 5.2
    Add-HdOval 'White' 47 (53.8 + $bob) 1.8 1.4 ''
}

# The body sitting as a real rabbit sits (see Add-HdSittingTorso), with long hind feet and short front legs.
function Add-PetBody([bool]$LeftDown, [bool]$RightDown) {
    Add-HdSittingTorso
    Add-HdSittingLegs $LeftDown $RightDown -HindRx 5.6 -LegTop 47
}

# The ears behind the head: the right one stands up, the left one bends over at the middle so its tip flops out to the side
# (the left, because the props of the scenario frames sit above the right side of the head).
function Add-RabbitEars {
    Add-HdOval 'Body' 40 13.4 4.8 9.6 'Outline' 1.2 9
    Add-HdOval 'Pink' 39.8 14 2.3 7 '' 0 9
    Add-HdPoly 'Body' (Get-MirroredXy @(35.6, 20, 35.4, 10.6, 38.4, 4.2, 43.4, 4, 50.2, 8.2, 52.6, 13.6, 49.6, 15.2, 44.6, 11.2, 43, 13, 43.6, 20)) 'Outline' 1.2 -Smooth -Tension 0.35
    Add-HdPoly 'Pink' (Get-MirroredXy @(38, 17, 37.8, 10.8, 39.6, 7.2, 41.4, 10, 41.2, 17)) '' 0 -Smooth -Tension 0.4
    Add-HdPoly 'BodyDark' (Get-MirroredXy @(44.2, 6.2, 49.4, 9.2, 51, 12.8, 49.4, 13.4, 45, 10)) '' 0 -Smooth -Tension 0.4
}

# The x y pairs mirrored left to right about the middle of the frame.
function Get-MirroredXy([double[]]$Xy) {
    $result = [double[]]$Xy.Clone()
    for ($i = 0; $i -lt $result.Count; $i += 2) { $result[$i] = 64 - $result[$i] }
    return , $result
}

# The head: a wide round face with a cream blaze from the forehead to the nose, puffy cream cheeks, a small pink nose, a little
# mouth and blush.
function Add-PetHead {
    Add-RabbitEars
    Add-HdOval 'Body' 32 28 15.6 12.4
    Add-HdOval 'Belly' 32 23 2.4 6.6 ''
    Add-HdOval 'Belly' 28.6 33.6 4 3.1 ''
    Add-HdOval 'Belly' 35.4 33.6 4 3.1 ''
    Add-HdOval 'Pink' 20.4 32.2 2.4 1.4 ''
    Add-HdOval 'Pink' 43.6 32.2 2.4 1.4 ''
    Add-HdPoly 'Pink' @(30.2, 30, 33.8, 30, 32, 32.2) 'Outline' 0.6 -Smooth -Tension 0.3
    Add-HdStroke @(32, 32.2, 32, 33.4) 'Outline' 0.8
    Add-HdStroke (Get-HdBezier 29.6 33.8 30.6 35.4 32 35 32 33.4 6) 'Outline' 0.8
    Add-HdStroke (Get-HdBezier 32 33.4 32 35 33.4 35.4 34.4 33.8 6) 'Outline' 0.8
}

# The rabbit lying down like a loaf: both ears laid back along its back, the folded one short and darker behind the other, the
# front paws tucked under the chin.
function Add-PetLie {
    Add-HdOval 'Belly' 57.4 53.6 4.6 4.4
    Add-HdOval 'Body' 37 51 21.6 10.6
    Add-HdOval 'Paw' 50 59.8 8 2.6
    Add-HdOval 'BodyDark' 30.6 39.8 7.4 2.9 'Outline' 1.1 -16
    Add-HdOval 'Body' 36 41.4 12.6 3.7 'Outline' 1.1 -7
    Add-HdOval 'Pink' 36.4 41.4 8.4 1.3 '' 0 -7
    Add-HdOval 'Paw' 11.6 59.4 4.4 2.5
    Add-HdOval 'Paw' 22.6 59.8 4.4 2.5
    Add-HdOval 'Body' 18.6 48.4 12.8 10
    Add-HdOval 'Belly' 18.6 44 2 4.8 ''
    Add-HdOval 'Belly' 16.2 53.4 3.4 2.6 ''
    Add-HdOval 'Belly' 21 53.4 3.4 2.6 ''
    Add-HdPoly 'Pink' @(17.2, 50.6, 20, 50.6, 18.6, 52.4) 'Outline' 0.5 -Smooth -Tension 0.3
    Add-HdOval 'Pink' 9.6 52.4 2 1.2 ''
    Add-HdOval 'Pink' 27.6 52.4 2 1.2 ''
}

# The rabbit seen from the side, facing left, in one of three poses of a hop chosen by D: 0 crouched on its haunches (a round
# thigh over a long flat hind foot), below 0 launched (the hind leg stretched out behind), above 0 tucked up in the air. Lift
# raises the cotton tail. The standing ear streams back over the body; the folded one is just its flopped tip behind the head.
function Add-PetSide([double]$D, [double]$Lift) {
    # Drawn a little smaller about the ground under the body, so the head stays inside the frame when a hop tilts the rabbit.
    $state = $script:g.Save()
    $script:g.TranslateTransform(35, 62)
    $script:g.ScaleTransform(0.92, 0.92)
    $script:g.TranslateTransform(-35, -62)
    try { Add-RabbitSide $D $Lift } finally { $script:g.Restore($state) }
}

# The side view of Add-PetSide at full size.
function Add-RabbitSide([double]$D, [double]$Lift) {
    Add-HdOval 'BodyDark' 21 21.4 6 2.8 'Outline' 1 -58
    Add-HdOval 'Belly' 53 (44 - $Lift * 0.5) 4.8 4.6
    Add-HdOval 'Body' 36 46 15.2 10.4
    Add-HdOval 'Belly' 28.6 52.4 7.6 3.6 ''
    if ($D -eq 0) {
        Add-HdOval 'Paw' 43.6 60.4 11 2.6
        Add-HdOval 'Body' 44 50 8.6 8.6
        Add-HdLimb @(20, 50, 18.4, 58.6) 'Body' 4
        Add-HdOval 'Paw' 17.2 60 3.4 2.2
    } elseif ($D -lt 0) {
        Add-HdOval 'Body' 46 47 7.6 5.2 'Outline' 1.1 24
        Add-HdLimb @(49, 49, 56.6, 53.4) 'Body' 3.8
        Add-HdLimb @(56.6, 53.4, 62.6, 55.4) 'Paw' 3
        Add-HdLimb @(21, 50, 11.4, 53.4) 'Body' 4
        Add-HdOval 'Paw' 9.8 53.8 3.2 2.2
    } else {
        Add-HdOval 'Body' 43 47.4 8 7.4
        Add-HdLimb @(40, 55.6, 52.4, 58.2) 'Paw' 3.4
        Add-HdLimb @(20, 50, 15.4, 58.2) 'Body' 4
        Add-HdOval 'Paw' 14.4 59.4 3.2 2.2
    }
    # The head in profile under the ear that streams back along the body.
    Add-HdOval 'Body' 26.6 20.4 12.4 3.9 'Outline' 1.1 -32
    Add-HdOval 'Pink' 26.8 20.4 8.6 1.5 '' 0 -32
    Add-HdOval 'Body' 14.6 35.8 10.8 9
    Add-HdOval 'Belly' 7.6 32.6 1.7 4.2 '' 0 22
    Add-HdOval 'Belly' 8.6 40.2 5.4 3.4 ''
    Add-HdOval 'Pink' 3.8 37.6 1.6 1.3 ''
    Add-HdOval 'Pink' 15 40.2 2.2 1.2 ''
    Add-HdOval 'Black' 11.4 33.4 1.6 2.2 ''
    Add-HdOval 'Shine' 11.9 32.6 0.6 0.75 ''
}

# A carrot held in the raised paw with its leaves at the paw, nibbled away from the tip until only the leaves are left.
function Add-PetFood([int]$Len) {
    Add-HdOval 'Green' 46.6 21.8 1.3 3.2 'Outline' 0.6 -40
    Add-HdOval 'Green' 45.4 25.8 1.3 3.2 'Outline' 0.6 -110
    if ($Len -gt 0) {
        $tip = 50 + 1.9 * $Len
        Add-HdPoly 'Accent' @(49, 22.2, $tip, 25, 49, 27.8) 'Outline' 0.9 -Smooth -Tension 0.2
        if ($Len -ge 3) { Add-HdStroke @(51.6, 23.6, 52.6, 24.2) 'Outline' 0.5 }
        if ($Len -ge 5) { Add-HdStroke @(54.6, 26.2, 55.6, 25.6) 'Outline' 0.5 }
    }
}

. (Join-Path $PSScriptRoot 'PetVectorAnimations.ps1')
