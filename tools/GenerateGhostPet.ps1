# Generates the ghost pet into pets\ghost: smooth vector art (see PetVectorDrawing.ps1), no third-party artwork. The animation
# set comes from PetVectorAnimations.ps1; this script only draws the poses.
# Run: powershell -NoProfile -File tools/GenerateGhostPet.ps1
. (Join-Path $PSScriptRoot 'PetVectorDrawing.ps1')

$PetName = 'ghost'
$HasHop = $false
$WalkFloats = $true  # no feet: it drifts along without bouncing
$WalkFps = 6
$NoArms = $true  # a ghost has no arms or hands, just the sheet
# With no hands, the umbrella or the parachute is held at the top of the head (the manifest's Glide grip), just inside the
# sheet so it covers the ends of the shaft and the strings.
$GlideGrips = @(, @(32, 17))
# 幽灵 spelled with code points, so the script does not depend on its own file encoding.
$PetNames = @{ En = 'Ghost'; Zh = ([string][char]0x5E7D + [string][char]0x7075); ZhHant = ([string][char]0x5E7D + [string][char]0x9748) }
$IconBox = @(12, 12, 40, 40)
$HeadBox = @(16, 14, 48, 42)
$pal = @{
    Outline  = New-HdColor 76 90 150
    Body     = New-HdColor 208 224 250
    BodyDark = New-HdColor 164 182 230
    Belly    = New-HdColor 236 243 255
    Paw      = New-HdColor 236 243 255
    Black    = New-HdColor 30 36 70
    Cookie   = New-HdColor 232 184 92
    Chip     = New-HdColor 130 80 48
}
$SitEyes = @{ Lx = 25.5; Rx = 38.5; Y = 29.5 }
$LieEyes = @{ Lx = 14; Rx = 24; Y = 48.4 }
$LieDrop = @(30, 41)
$script:hemPhase = 0

# A ghost has nothing behind it, but its hem sways with Phase (0-3); drawn with the body.
function Add-PetBack([int]$Phase) {
    $script:hemPhase = $Phase
}

# The wavy hem as Bezier segments from the point (X0, Base) to the right: Count scallops of the same length up to X1. Sway
# leans every tip sideways by the same amount (the hem swinging gently). Returns the numbers of the segments.
function Get-GhostHem([double]$X0, [double]$X1, [int]$Count, [double]$Base, [double]$Drop, [double]$Sway = 0) {
    $width = ($X1 - $X0) / $Count
    $y = $Base + $Drop * 1.333
    $result = New-Object 'System.Collections.Generic.List[double]'
    for ($i = 0; $i -lt $Count; $i++) {
        $xa = $X0 + $i * $width
        $xb = $xa + $width
        $result.AddRange([double[]]@(($xa + $Sway), $y, ($xb + $Sway), $y, $xb, $Base))
    }
    return , $result.ToArray()
}

# The hem of a drifting ghost, which faces left, so it streams out behind to the right: scallops from (15, 57) along a line
# that lifts towards the tip of the tail, their tips leaning backwards. A wave (Wave is its angle in radians) runs from the
# front to the tail and grows on the way, so a loop of frames with the angle going round looks like cloth fluttering in the
# wind. Returns the numbers of the Bezier segments; the last two of them are the tip of the tail.
function Get-GhostTrail([double]$Wave) {
    $xs = @(15, 24, 33.5, 44, 60)
    $ys = @(57, 57.2, 56, 53.5, 48)
    $swing = @(0, 0.4, 1.1, 2, 3.4)
    $drops = @(6.2, 6, 5.2, 2.6)
    $y = foreach ($i in 0..4) { $ys[$i] + $swing[$i] * [Math]::Sin($Wave - $i * [Math]::PI / 2) }
    $result = New-Object 'System.Collections.Generic.List[double]'
    foreach ($i in 0..3) {
        $width = $xs[$i + 1] - $xs[$i]
        $drop = $drops[$i] * (0.9 + 0.1 * [Math]::Sin($Wave - $i * [Math]::PI / 2))
        $result.AddRange([double[]]@(
                ($xs[$i] + $width * 0.35), ($y[$i] + $drop),
                ($xs[$i + 1] + $width * 0.2), ($y[$i + 1] + $drop),
                $xs[$i + 1], $y[$i + 1]))
    }
    return , $result.ToArray()
}

# The sheet seen from the front: a rounded head that is narrow at the top, a body that gets wider towards the fluttering
# hem, and a mouth. It has no hands, so whether a paw is raised changes nothing.
function Add-PetBody([bool]$LeftDown, [bool]$RightDown) {
    $sway = @(0, 1, 0, -1)[$script:hemPhase % 4] * 0.8
    $shape = @(13.5, 57) + (Get-GhostHem 13.5 50.5 4 57 5.5 $sway) + @(52.4, 44, 49.6, 27, 41.5, 19.2, 36.5, 14.6, 27.5, 14.6, 22.5, 19.2, 15.4, 27, 11.8, 44, 13.5, 57)
    Add-HdShape 'Body' $shape
    Add-HdOval 'Belly' 32 47 11.5 10 ''
    Add-HdOval 'Belly' 28 21.5 4 1.8 '' 0 -20
    Add-HdOval 'Pink' 21 35 3.2 1.8 ''
    Add-HdOval 'Pink' 43 35 3.2 1.8 ''
    Add-HdOval 'Black' 32 38 2 2.6 ''
}

# The face is drawn with the body.
function Add-PetHead { }

# The ghost resting flat on the ground like a puddle of sheet.
function Add-PetLie {
    $shape = @(5.5, 58.5) + (Get-GhostHem 5.5 58.5 5 58.5 3.2) + @(61, 52, 57.5, 44, 47, 43.5, 40, 43, 35, 40.5, 26, 38.5, 20, 36.5, 11, 38, 8, 46, 6, 50, 4.5, 54, 5.5, 58.5)
    Add-HdShape 'Body' $shape
    Add-HdOval 'Belly' 40 52 12 5 ''
    Add-HdOval 'Pink' 9.4 53 2.6 1.5 ''
    Add-HdOval 'Pink' 28.6 53 2.6 1.5 ''
    Add-HdOval 'Black' 19 54 1.6 2 ''
}

# The ghost seen from the side, facing left, drifting: the head stays where it is while the sheet streams out behind into a
# fluttering tail. D (-3 to 3) and Lift (-2 to 2) together give the angle of the flutter, so the four walk poses go once
# round it.
function Add-PetSide([double]$D, [double]$Lift) {
    $wave = [Math]::Atan2(-$Lift / 2, -$D / 3)
    $trail = Get-GhostTrail $wave
    $tipY = $trail[$trail.Count - 1]
    $shape = @(15, 57) + $trail + @(54, ($tipY - 2), 47, 45, 44, 37, 42, 29, 39, 22.5, 35, 19.5, 30, 14.5, 20, 14.5, 16.5, 21, 13.5, 26, 11.5, 43, 15, 57)
    Add-HdShape 'Body' $shape
    Add-HdOval 'Belly' 22 47 6.5 9 ''
    Add-HdOval 'Belly' 22 21.5 3.6 1.7 '' 0 -20
    Add-HdOval 'Black' 19.5 31 1.6 2.3 ''
    Add-HdOval 'Shine' 20 30.2 0.55 0.7 ''
    Add-HdOval 'Pink' 17.4 36 2.6 1.5 ''
    Add-HdOval 'Black' 14.8 39 1.1 1.6 ''
}

# A cookie floating in front of the ghost, nibbled away.
function Add-PetFood([int]$Len) {
    if ($Len -gt 0) {
        $r = 1.6 + $Len * 0.6
        Add-HdOval 'Cookie' (47.5 + $r) 25 $r $r 'Outline' 0.9
        if ($Len -ge 5) {
            Add-HdOval 'Chip' (47.5 + $r - 1.2) 23.4 0.9 0.9 ''
            Add-HdOval 'Chip' (47.5 + $r + 1.6) 26.4 0.9 0.9 ''
        }
    } else {
        Add-HdOval 'Cookie' 50 26.4 0.8 0.8 ''
        Add-HdOval 'Cookie' 52.4 25.6 0.6 0.6 ''
    }
}

. (Join-Path $PSScriptRoot 'PetVectorAnimations.ps1')
