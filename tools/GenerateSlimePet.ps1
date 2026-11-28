# Generates the slime pet into pets\slime: smooth vector art (see PetVectorDrawing.ps1), no third-party artwork. The animation
# set comes from PetVectorAnimations.ps1; this script only draws the poses. A slime has no arms, legs or tail: it is a blob
# that breathes, squashes and stretches. Run: powershell -NoProfile -File tools/GenerateSlimePet.ps1
. (Join-Path $PSScriptRoot 'PetVectorDrawing.ps1')

$PetName = 'slime'
$HasHop = $false
$NoArms = $true  # nothing to raise, hold or reach out with
# With no hands, the umbrella or the parachute is held at the top of the blob (the manifest's Glide grip), just inside it so
# the paws are not needed to cover the ends of the shaft and the strings.
$GlideGrips = @(, @(32, 22.5))
# 史莱姆 spelled with code points, so the script does not depend on its own file encoding.
$PetNames = @{ En = 'Slime'; Zh = ([string][char]0x53F2 + [string][char]0x83B1 + [string][char]0x59C6); ZhHant = ([string][char]0x53F2 + [string][char]0x840A + [string][char]0x59C6) }
$IconBox = @(12, 18, 40, 40)
$HeadBox = @(14, 24, 50, 52)
$pal = @{
    Outline  = New-HdColor 28 104 84
    Body     = New-HdColor 118 222 172
    BodyDark = New-HdColor 78 188 140
    Belly    = New-HdColor 176 242 208
    Paw      = New-HdColor 118 222 172
    Black    = New-HdColor 20 54 52
    Chip     = New-HdColor 130 80 48
}
$SitEyes = @{ Lx = 24.5; Rx = 39.5; Y = 40.5 }
$LieEyes = @{ Lx = 22; Rx = 36; Y = 53 }
$LieDrop = @(45, 45)
$LaptopTop = 48  # a low lid, below the eyes and the smile
$script:breath = 0

# A slime has nothing behind it; the breathing phase (0-3) is kept for the body.
function Add-PetBack([int]$Phase) {
    $script:breath = $Phase
}

# A blob as Bezier segments: Cx the middle, Base the ground line, W half the width, H the height, Lean moves the little point
# on top sideways. The bottom corners are rounded and the bottom is flat.
function Get-SlimeShape([double]$Cx, [double]$Base, [double]$W, [double]$H, [double]$Lean = 0) {
    $top = $Base - $H
    $tipX = $Cx + $Lean
    return @(
        ($Cx - $W), ($Base - 3),
        ($Cx - $W - 0.6), ($Base - $H * 0.62), ($Cx - $W * 0.5), ($top + 1.5), $tipX, ($top - 2.6),
        ($Cx + $W * 0.5), ($top + 1.5), ($Cx + $W + 0.6), ($Base - $H * 0.62), ($Cx + $W), ($Base - 3),
        ($Cx + $W), ($Base + 0.4), ($Cx + $W * 0.8), $Base, ($Cx + $W * 0.55), $Base,
        ($Cx + $W * 0.2), $Base, ($Cx - $W * 0.2), $Base, ($Cx - $W * 0.55), $Base,
        ($Cx - $W * 0.8), $Base, ($Cx - $W), ($Base + 0.4), ($Cx - $W), ($Base - 3)
    )
}

# The blob seen from the front, breathing: it gets a little taller and narrower as it breathes in.
function Add-PetBody([bool]$LeftDown, [bool]$RightDown) {
    $b = @(0, 1, 0, -1)[$script:breath % 4]
    $w = 19 - 0.5 * $b
    $h = 36 + 1.4 * $b
    Add-HdShape 'Body' (Get-SlimeShape 32 61 $w $h)
    Add-HdOval 'Belly' 32 53 ($w * 0.6) 6 ''
    Add-HdOval 'Belly' 21.5 (61 - $h * 0.72) 3.4 1.8 '' 0 -35
    Add-HdOval 'Pink' 19 45.6 3 1.7 ''
    Add-HdOval 'Pink' 45 45.6 3 1.7 ''
    # A bubble inside the jelly.
    Add-HdRing 'BodyDark' 41.5 54.6 2 2 0.8
}

# The face is the eyes and a small smile.
function Add-PetHead {
    Add-HdStroke (Get-HdBezier 29.4 45.4 30.6 47.2 33.4 47.2 34.6 45.4 6) 'Outline' 0.9
}

# The slime flat on the ground like a puddle with a dome.
function Add-PetLie {
    Add-HdShape 'Body' (Get-SlimeShape 32 61 27 21)
    Add-HdOval 'Belly' 32 57 16 3 ''
    Add-HdOval 'Belly' 17 47.4 3.6 1.7 '' 0 -25
    Add-HdOval 'Pink' 17.6 56.2 2.8 1.5 ''
    Add-HdOval 'Pink' 40.4 56.2 2.8 1.5 ''
}

# The slime seen from the side, facing left, squelching along: D (-3 to 3) is how squashed (-3) or stretched (3) it is, Lift
# leans the top.
function Add-PetSide([double]$D, [double]$Lift) {
    $w = 18.5 - 1.6 * $D
    $h = 35 + 2.2 * $D
    $cx = 34
    Add-HdShape 'Body' (Get-SlimeShape $cx 61 $w $h (-2 - $Lift * 0.5))
    Add-HdOval 'Belly' $cx 56 ($w * 0.6) 3.4 ''
    Add-HdOval 'Belly' ($cx - $w * 0.45) (61 - $h * 0.72) 3 1.7 '' 0 -35
    $ey = 61 - $h * 0.52
    Add-HdOval 'Black' ($cx - 12) $ey 2.2 3 ''
    Add-HdOval 'Shine' ($cx - 11.4) ($ey - 1.1) 0.85 1 ''
    Add-HdOval 'Black' ($cx - 3.6) $ey 2.2 3 ''
    Add-HdOval 'Shine' ($cx - 3) ($ey - 1.1) 0.85 1 ''
    Add-HdOval 'Pink' ($cx - 15) ($ey + 4.2) 2.4 1.4 ''
    Add-HdStroke (Get-HdBezier ($cx - 10.6) ($ey + 4.6) ($cx - 9.6) ($ey + 6.2) ($cx - 6.8) ($ey + 6.2) ($cx - 5.8) ($ey + 4.6) 6) 'Outline' 0.9
}

# An apple the slime has taken in, dissolving until only the seeds are left.
function Add-PetFood([int]$Len) {
    if ($Len -gt 0) {
        $r = 1.6 + $Len * 0.6
        Add-HdOval 'Red' (47.5 + $r) 25 $r $r 'Outline' 0.9
        if ($Len -ge 5) { Add-HdOval 'Green' (48 + $r) (24.4 - $r) 1.4 0.8 'Outline' 0.5 -30 }
    } else {
        Add-HdOval 'Chip' 50 26 0.8 1.1 ''
        Add-HdOval 'Chip' 52.4 25 0.7 1 ''
    }
}

. (Join-Path $PSScriptRoot 'PetVectorAnimations.ps1')
