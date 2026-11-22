# Generates the little dragon pet into pets\dragon: smooth vector art (see PetVectorDrawing.ps1), no third-party artwork. The
# animation set comes from PetVectorAnimations.ps1; this script only draws the poses.
# Run: powershell -NoProfile -File tools/GenerateDragonPet.ps1
. (Join-Path $PSScriptRoot 'PetVectorDrawing.ps1')

$PetName = 'dragon'
$HasHop = $false
# A dragon comes down on its own wings: no umbrella or parachute, so no grips either (see PetVectorAnimations.ps1).
$GlideFlies = $true
# The eyes and the sweat drop of the curled-up dragon, which has its head tucked in at the front left.
$LieEyes = @{ Lx = 19.6; Rx = 28.4; Y = 51.4 }
$LieDrop = @(36, 47)
# A dragon sleeps through a four-frame breathing cycle: the folded wing rises and falls, the tail tip twitches and a bubble
# grows at its nose (see Add-PetLie).
$SleepFrames = 4
# 小龙 spelled with code points, so the script does not depend on its own file encoding.
$PetNames = @{ En = 'Dragon'; Zh = ([string][char]0x5C0F + [string][char]0x9F99); ZhHant = ([string][char]0x5C0F + [string][char]0x9F8D) }
$IconBox = @(12, 4, 40, 40)
$HeadBox = @(16, 14, 48, 39)
$pal = @{
    Outline  = New-HdColor 24 74 62
    Body     = New-HdColor 98 200 150
    BodyDark = New-HdColor 62 158 118
    Belly    = New-HdColor 252 238 196
    Paw      = New-HdColor 98 200 150
    Accent   = New-HdColor 255 190 72
    Wing     = New-HdColor 244 128 118
    WingFar  = New-HdColor 206 96 92
    Black    = New-HdColor 24 36 40
}
# A slim body, so the arms start nearer the middle, are thinner, and the raised ones reach out from the new shoulders.
$Arms = @{
    Width = 3.6; PawRx = 2.5; PawRy = 2.5
    ShoulderL = @(24.6, 42.4); ShoulderR = @(39.4, 42.4); UpL = @(12, 34); UpR = @(52, 34); ReachFrom = @(24.6, 42.4)
}
# Yellow eyes with a narrow pupil.
$EyeKeys = @{ Fill = 'Yellow'; Pupil = 'Black'; Lid = 'Outline'; Brow = 'BodyDark'; FillRx = 2.8; FillRy = 3.3; PupilRx = 1; PupilRy = 2.8 }

# One wing of the sitting dragon, behind the body: Side is -1 (left) or 1 (right) and Flap (0-3) raises the tips.
function Add-DragonWing([double]$Side, [double]$Flap) {
    $f = @(0, 1.5, 3, 1.5)[[int]$Flap]
    $pts = @(
        @(27, 41), @(14, 35), @(7, (25 - $f)), @(11, (34 - $f * 0.6)), @(5, (36 - $f * 0.6)), @(11.4, 41), @(9, 47), @(25, 50)
    )
    $xy = foreach ($p in $pts) { (32 + $Side * (32 - $p[0])); $p[1] }
    Add-HdPoly 'Wing' ([double[]]$xy) 'Outline' 1.1 -Smooth -Tension 0.15
    foreach ($tip in @(@(7, (25 - $f)), @(5, (36 - $f * 0.6)))) {
        Add-HdStroke @((32 + $Side * 6), 44, (32 + $Side * (32 - $tip[0] - 1)), ($tip[1] + 1.4)) 'BodyDark' 1
    }
}

# The wings behind the shoulders, flapping with Phase (0-3), and the tail curling up on the right with an orange tip.
function Add-PetBack([int]$Phase) {
    Add-DragonWing -1 $Phase
    Add-DragonWing 1 $Phase
    $tip = @(@(56, 40), @(57, 43), @(58, 46), @(57, 43))[$Phase]
    $curve = Get-HdBezier 37 54 50 60 59 54 $tip[0] $tip[1] 24
    Add-HdStroke $curve 'Outline' 6.4
    Add-HdStroke $curve 'Body' 4.7
    Add-HdStroke $curve[36..($curve.Count - 1)] 'Accent' 4.7
}

# A slim, upright body: a narrow torso with a slight waist and no bulging thighs, a narrow cream belly with scale ridges, two
# thin legs ending in small claws, and thin arms hanging at the sides unless they are raised (then the template draws them).
function Add-PetBody([bool]$LeftDown, [bool]$RightDown) {
    foreach ($side in -1, 1) {
        $x = 32 + $side * 3.8
        Add-HdTaperedLeg $x 52.5 59 2.6 2.2 'Body'
        Add-HdOval 'Paw' ($x + $side * 1) 60 3.2 1.9
    }
    Add-HdMergedOvals 'Body' @(@(32, 43.5, 8, 7.5), @(32, 49, 7.2, 6), @(32, 52.5, 7.8, 4.6))
    Add-HdOval 'Belly' 32 47.4 4.4 8.6 ''
    foreach ($y in 43.2, 46.2, 49.2, 52.2) { Add-HdStroke @(29.8, $y, 34.2, $y) 'BodyDark' 0.7 }
    foreach ($arm in @(@($LeftDown, -1), @($RightDown, 1))) {
        if (-not $arm[0]) { continue }
        $side = $arm[1]
        Add-HdJoinedLimb @((32 + $side * 7.4), 42.4, (32 + $side * 9.4), 51.6) 'Body' 3.4
        Add-HdOval 'Paw' (32 + $side * 9.4) 52.4 2.4 2.3
    }
}

# The head: two orange horns, a round face with a cream snout, nostrils and a smile.
function Add-PetHead {
    Add-HdPoly 'Accent' @(21.4, 19.4, 18.2, 6.4, 27, 15.4) 'Outline' 1.1 -Smooth -Tension 0.25
    Add-HdPoly 'Accent' @(42.6, 19.4, 45.8, 6.4, 37, 15.4) 'Outline' 1.1 -Smooth -Tension 0.25
    Add-HdOval 'Body' 32 27.5 15.5 11.5
    Add-HdOval 'Belly' 32 33.4 7.6 4.6 ''
    Add-HdOval 'Outline' 29.6 32.4 0.8 0.6 ''
    Add-HdOval 'Outline' 34.4 32.4 0.8 0.6 ''
    Add-HdStroke (Get-HdBezier 28.6 35.4 30.2 37.2 33.8 37.2 35.4 35.4 6) 'Outline' 0.8
    foreach ($side in -1, 1) { Add-HdStroke @((32 + $side * 9.8), 17.6, (32 + $side * 9.6), 20.4) 'BodyDark' 1.3 }
}

# One big bat wing of the flying dragon, behind the body: Side is -1 (left) or 1 (right), Angle turns the whole wing about the
# shoulder (positive lifts the tips). The membrane is the polygon through the shoulder, elbow, wrist, the three finger tips and
# the scallops between them; the bones are drawn inside it, ending at its corners.
function Add-DragonFlyWing([double]$Side, [double]$Angle) {
    # The shoulder is up at the neck, level with the chin, so a raised wing shows beside the head instead of behind it.
    $shoulder = @(24, 36)
    $base = @(@(24, 36), @(17, 29), @(9, 25), @(3, 31), @(6, 36.5), @(5, 41), @(10, 43.5), @(12, 47), @(19, 45), @(25, 44))
    $rad = $Angle * [Math]::PI / 180
    $cos = [Math]::Cos($rad); $sin = [Math]::Sin($rad)
    $pts = foreach ($p in $base) {
        $dx = 1.1 * ($p[0] - $shoulder[0]); $dy = 1.1 * ($p[1] - $shoulder[1])
        $x = $shoulder[0] + $dx * $cos - $dy * $sin
        $y = $shoulder[1] + $dx * $sin + $dy * $cos
        , @($(if ($Side -gt 0) { 64 - $x } else { $x }), $y)
    }
    $xy = foreach ($p in $pts) { $p[0]; $p[1] }
    Add-HdPoly 'Wing' ([double[]]$xy) 'Outline' 1.1
    Add-HdStroke @($pts[0][0], $pts[0][1], $pts[1][0], $pts[1][1], $pts[2][0], $pts[2][1]) 'BodyDark' 1.5
    foreach ($tip in 3, 5, 7) {
        Add-HdStroke @($pts[2][0], $pts[2][1], $pts[$tip][0], $pts[$tip][1]) 'BodyDark' 1
    }
}

# The dragon flying down on its own wings, seen from the front, one flap in four poses (Phase 0-3): wings up at the top, half
# way down, down at the bottom, half way up. The body rises as the wings come down and sinks as they go up, the legs hang and
# swing, the arms lift a little with the downstroke and the tail trails below, its tip swaying half a beat behind.
function Add-PetFly([int]$Phase) {
    $bob = @(1, 0, -2, -1)[$Phase]
    $angle = @(52, 14, -60, 26)[$Phase]
    $swing = @(1.2, 0.4, -1.2, -0.4)[$Phase]
    $armLift = @(0, 1.2, 3, 1.2)[$Phase]
    $tailSway = @(-3, 0, 3, 0)[$Phase]
    $state = $script:g.Save()
    try {
        $script:g.TranslateTransform(0, [single]$bob)
        Add-DragonFlyWing -1 $angle
        Add-DragonFlyWing 1 $angle
        $tail = Get-HdBezier 37 53 47 54 43 62 (53 + $tailSway) 57 24
        Add-HdStroke $tail 'Outline' 6
        Add-HdStroke $tail 'Body' 4.3
        Add-HdStroke $tail[34..($tail.Count - 1)] 'Accent' 4.3
        foreach ($side in -1, 1) {
            $foot = 32 + $side * (6.2 + $side * $swing)
            Add-HdLimb @((32 + $side * 5), 54, $foot, 58.2) 'BodyDark' 4
            Add-HdOval 'Paw' $foot 59.2 3.2 2.1
        }
        Add-HdOval 'Body' 32 47 8.2 10.6
        Add-HdOval 'Belly' 32 48.4 4.4 7.6 ''
        foreach ($side in -1, 1) {
            Add-HdLimb @((32 + $side * 6.8), 45.6, (32 + $side * 14.6), (49.6 - $armLift)) 'Body' 3.4
            Add-HdOval 'Paw' (32 + $side * 14.6) (49.6 - $armLift) 2.5 2.4
        }
        Add-PetHead
        Add-PetEyes $SitEyes.Lx $SitEyes.Rx $SitEyes.Y 'open'
    } finally {
        $script:g.Restore($state)
    }
}

# One wing of the sleeping dragon folded over its back like a blanket: Side is -1 (left) or 1 (right), Rise lifts it with the
# breath. It is a fan of three fingers from the middle of the back, with a scalloped lower edge.
function Add-DragonBlanketWing([double]$Side, [double]$Rise) {
    $base = @(@(34, 40), @(27, 37.6), @(19, 39.4), @(13.6, 44.6), @(17, 46), @(15.4, 51), @(20.4, 49.4), @(22, 54.4))
    $pts = foreach ($p in $base) { , @($(if ($Side -gt 0) { 68 - $p[0] } else { $p[0] }), ($p[1] - $Rise)) }
    $xy = foreach ($p in $pts) { $p[0]; $p[1] }
    Add-HdPoly 'Wing' ([double[]]$xy) 'Outline' 1.1
    foreach ($tip in 3, 5, 7) {
        Add-HdStroke @($pts[0][0], $pts[0][1], $pts[$tip][0], $pts[$tip][1]) 'BodyDark' 1
    }
}

# The dragon asleep curled up into a ball: the body a round heap with both wings folded over it like a blanket, the tail curled
# round the front with its tip by the chin, and the head tucked in at the front, its paws under the chin.
function Add-PetLie {
    # The breath (0-2): the heap and the wings rise with it, the tail tip lifts, and a bubble grows at the nose.
    $b = @(0, 1, 2, 1)[[int]$script:SleepPhase % 4]
    Add-HdOval 'Body' 33 46.5 21.5 (14 + 0.4 * $b)
    Add-DragonBlanketWing -1 (3 + 0.8 * $b)
    Add-DragonBlanketWing 1 (3 + 0.8 * $b)
    $tail = Get-HdBezier 56 47 63 58 48 63 36 (59.4 - 0.8 * $b) 20
    Add-HdStroke $tail 'Outline' 5.8
    Add-HdStroke $tail 'Body' 4.1
    Add-HdStroke $tail[28..($tail.Count - 1)] 'Accent' 4.1
    Add-HdPoly 'Accent' @(16.4, 47, 14.4, 37.6, 21.4, 44) 'Outline' 1.1 -Smooth -Tension 0.25
    Add-HdPoly 'Accent' @(31.6, 47, 33.6, 37.6, 26.6, 44) 'Outline' 1.1 -Smooth -Tension 0.25
    Add-HdOval 'Body' 24 52 11 8.5
    Add-HdOval 'Belly' 24 56.2 6.4 3.4 ''
    Add-HdOval 'Outline' 22.4 55 0.6 0.45 ''
    Add-HdOval 'Outline' 25.6 55 0.6 0.45 ''
    Add-HdOval 'Paw' 19.4 60.2 3.2 1.8
    Add-HdOval 'Paw' 28.6 60.2 3.2 1.8
    if ($b -gt 0) { Add-HdOval 'Shine' (27.6 + 0.5 * $b) (55 - 0.3 * $b) (0.5 + 0.9 * $b) (0.5 + 0.9 * $b) 'Outline' 0.5 }
}

# One big bat wing of the walking dragon, on its back, seen from the side (the dragon faces left, so the wing points back, to
# the right): Angle turns it about the shoulder (negative lifts it, positive lowers it), Scale sizes it, Fill is its palette
# key, and Dx and Dy move the shoulder. Membrane through the shoulder, elbow, wrist, the three finger tips and the scallops
# between them, with the bones drawn inside it.
function Add-DragonSideWing([double]$Angle, [double]$Scale, [string]$Fill, [double]$Dx = 0, [double]$Dy = 0) {
    $shoulder = @((36 + $Dx), (39 + $Dy))
    $base = @(@(0, 0), @(5, -6), @(12, -14), @(23, -13), @(17, -8), @(24, -3), @(17, 0), @(19, 6), @(8, 5))
    $rad = $Angle * [Math]::PI / 180
    $cos = [Math]::Cos($rad); $sin = [Math]::Sin($rad)
    $pts = foreach ($p in $base) {
        $dx = $Scale * $p[0]; $dy = $Scale * $p[1]
        , @(($shoulder[0] + $dx * $cos - $dy * $sin), ($shoulder[1] + $dx * $sin + $dy * $cos))
    }
    $xy = foreach ($p in $pts) { $p[0]; $p[1] }
    Add-HdPoly $Fill ([double[]]$xy) 'Outline' 1.1
    Add-HdStroke @($pts[0][0], $pts[0][1], $pts[1][0], $pts[1][1], $pts[2][0], $pts[2][1]) 'BodyDark' 1.4
    foreach ($tip in 3, 5, 7) {
        Add-HdStroke @($pts[2][0], $pts[2][1], $pts[$tip][0], $pts[$tip][1]) 'BodyDark' 0.9
    }
}

# The dragon seen from the side, facing left, walking: D (-3 to 3) swings the legs, Lift raises the tail tip. D and Lift
# together go once round a circle over the four walk poses, which sets the beat of the wings: mid, down, mid, up.
function Add-PetSide([double]$D, [double]$Lift) {
    $swing = $D * 2.2
    $tailTip = 24 - $Lift
    $beat = [Math]::Atan2(-$Lift / 2, -$D / 3)
    $wingAngle = -2 + 32 * [Math]::Sin($beat)
    $curve = Get-HdBezier 46 44 55 44 59 36 57 $tailTip 22
    Add-HdStroke $curve 'Outline' 6.2
    Add-HdStroke $curve 'Body' 4.5
    Add-HdStroke $curve[22..($curve.Count - 1)] 'Accent' 4.5
    foreach ($leg in @(@(30, (-$swing)), @(38, $swing))) {
        $foot = $leg[0] + $leg[1]
        Add-HdLimb @($leg[0], 50, $foot, 59.5) 'BodyDark' 4.2
        Add-HdOval 'BodyDark' ($foot - 1) 60.4 3.2 2.2
    }
    # The far wing peeks out above the back, a beat behind the near one.
    Add-DragonSideWing ($wingAngle - 30) 0.8 'WingFar' -4 -2
    Add-HdOval 'Body' 34 46 16.5 10
    Add-HdOval 'Belly' 28 51.4 9.4 3.6 ''
    Add-DragonSideWing $wingAngle 0.95 'Wing' -7
    foreach ($leg in @(@(25, $swing), @(42, (-$swing)))) {
        $foot = $leg[0] + $leg[1]
        Add-HdLimb @($leg[0], 50, $foot, 59.5) 'Body' 4.4
        Add-HdOval 'Paw' ($foot - 1) 60.4 3.4 2.3
    }
    # Head in profile with the snout pointing left, a horn leaning back over the eye.
    Add-HdPoly 'Accent' @(21, 27.6, 27.6, 14.4, 28, 27) 'Outline' 1.1 -Smooth -Tension 0.25
    Add-HdOval 'Belly' 7.6 38.2 6 3.5
    Add-HdOval 'Body' 16 34 10.5 8.8
    Add-HdOval 'Belly' 11.6 39.4 6 3.3 ''
    Add-HdOval 'Yellow' 10.8 32 2 2.6 ''
    Add-HdOval 'Black' 10.4 32 0.8 2.3 ''
    Add-HdOval 'Outline' 3.4 36.2 0.7 0.5 ''
    Add-HdStroke (Get-HdBezier 7.6 40.2 9.4 41.8 11.4 41.8 12.8 40.6 6) 'Outline' 0.7
}

# A drumstick held in the paw, gnawed down to the bone.
function Add-PetFood([int]$Len) {
    if ($Len -gt 0) {
        $r = 1.8 + $Len * 0.5
        Add-HdStroke @(49, 25.5, (49 + $r), 25.5) 'Outline' 3.2
        Add-HdStroke @(49, 25.5, (49 + $r), 25.5) 'Belly' 1.8
        Add-HdOval 'Accent' (50 + $r + $r * 0.5) 25.5 ($r * 0.9) ($r * 0.7) 'Outline' 0.9
    } else {
        Add-HdStroke @(49, 25.5, 55, 25.5) 'Belly' 1.4
        Add-HdOval 'Belly' 55.4 24.4 1 1 ''
        Add-HdOval 'Belly' 55.4 26.6 1 1 ''
    }
}

. (Join-Path $PSScriptRoot 'PetVectorAnimations.ps1')
