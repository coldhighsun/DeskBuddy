# Generates the pixel-art ghost pet (no third-party artwork). Art faces right.
# Usage: powershell -NoProfile -File tools/GenerateGhostPet.ps1
. (Join-Path $PSScriptRoot 'PetDrawing.ps1')

$PetName = 'ghost'
$HasHop = $false
$NoLegs = $true
$NoArms = $true  # the long arms are not drawn; the hands are the little bumps drawn with the body
$ClimbFromWalk = $true
$PetNames = @{ En = 'Ghost'; Zh = '幽灵' }
$IconBox = @(8, 6, 16, 16)
$pal = @{
    Outline  = New-Brush 70 84 140
    Body     = New-Brush 205 220 248
    BodyDark = New-Brush 160 178 225
    Belly    = New-Brush 232 240 255
    Paw      = New-Brush 232 240 255
    Pink     = New-Brush 255 150 190
    Iris     = New-Brush 70 100 200
    EyeWhite = New-Brush 255 255 255
    Lid      = New-Brush 70 84 140
    Black    = New-Brush 20 24 50
    Whisker  = New-Brush 225 232 250
    White    = New-Brush 240 244 250
    Blue     = New-Brush 110 170 230
    Red      = New-Brush 255 70 70
    Yellow   = New-Brush 255 210 70
    Accent   = New-Brush 255 210 70
}
$textFont = New-Object System.Drawing.Font 'Consolas', 16, ([System.Drawing.FontStyle]::Bold)

# A ghost has nothing behind it, but its hem sways with Phase (0-3); drawn in the body so it hangs behind the hands.
function Add-PetBack([int]$Phase) {
    $script:hemPhase = $Phase
}

# Fills a shape row by row, outlined: each row is the y and the x of its left and right outline, and the outline is
# stretched across the steps where the shape gets wider or narrower.
function Add-GhostRows([object[]]$Rows) {
    $previous = $null
    foreach ($row in $Rows) {
        $y = $row[0]; $left = $row[1]; $right = $row[2]
        Px Body ($left + 1) $y ($right - $left - 1)
        if ($null -eq $previous) {
            Px Outline $left $y ($right - $left + 1)
        } else {
            $a = [Math]::Min($left, $previous[1]); $b = [Math]::Max($left, $previous[1])
            Px Outline $a $y ($b - $a + 1)
            $a = [Math]::Min($right, $previous[2]); $b = [Math]::Max($right, $previous[2])
            Px Outline $a $y ($b - $a + 1)
        }
        $previous = $row
    }
}

# The wavy hem under a shape, from x Left to x Right, starting at row Top and ending at row 28 or 29: scallops of five
# columns whose bumps shift by Shift columns as it flutters, and, when Lean is set, the bottom of every bump sits one
# column further back (to the right) than its body, as if the hem trailed behind.
function Add-GhostHem([int]$Left, [int]$Right, [int]$Top, [int]$Shift, [bool]$Lean) {
    foreach ($x in $Left..$Right) {
        $c = $x - $Left
        $bottom = if ((($c + $Shift) % 5) -in 1, 2, 3) { 29 } else { 28 }
        $edge = if ($x -eq $Left -or $x -eq $Right) { 'Outline' } else { 'Body' }
        if ($bottom -gt $Top) { Px $edge $x $Top 1 ($bottom - $Top) }
        Px Outline ($x + $(if ($Lean) { 1 } else { 0 })) $bottom
    }
}

# The outline of the ghost from the front: a rounded head that is narrow at the top and a body that gets wider and wider
# towards the hem, like a bell.
$frontRows = @(
    @(4, 12, 19), @(5, 10, 21), @(6, 10, 21), @(7, 9, 22), @(8, 9, 22), @(9, 9, 22), @(10, 9, 22), @(11, 8, 23),
    @(12, 8, 23), @(13, 8, 23), @(14, 8, 23), @(15, 7, 24), @(16, 7, 24), @(17, 7, 24), @(18, 7, 24), @(19, 7, 24),
    @(20, 7, 24), @(21, 6, 25), @(22, 6, 25), @(23, 6, 25), @(24, 6, 25), @(25, 6, 25)
)

# The same bell seen from the side, facing left: the front stays upright and the back flares out.
$sideRows = @(
    @(4, 6, 13), @(5, 5, 14), @(6, 4, 15), @(7, 4, 15), @(8, 4, 15), @(9, 4, 15), @(10, 4, 15), @(11, 3, 16),
    @(12, 3, 16), @(13, 3, 16), @(14, 3, 16), @(15, 3, 17), @(16, 3, 17), @(17, 3, 18), @(18, 3, 18), @(19, 3, 19),
    @(20, 3, 19), @(21, 3, 20), @(22, 3, 20), @(23, 3, 20), @(24, 3, 20), @(25, 3, 20)
)

# The same bell lying flat, like a puddle: narrow where the head is, wide at the bottom.
$lieRows = @(
    @(13, 9, 18), @(14, 7, 20), @(15, 6, 22), @(16, 5, 23), @(17, 4, 24), @(18, 4, 25), @(19, 3, 26),
    @(20, 3, 27), @(21, 3, 27), @(22, 3, 27), @(23, 3, 27), @(24, 3, 27), @(25, 3, 27), @(26, 3, 27), @(27, 3, 27)
)

# The sheet seen from the front, with a hem that flutters, a little bump at each side for the hands (down, or up when
# the arm is raised) and the face.
function Add-PetBody([bool]$LeftDown, [bool]$RightDown) {
    Add-GhostHem 6 25 26 ($script:hemPhase % 2) $false
    Add-GhostRows $frontRows
    Px Belly 11 6 4 2
    Px Belly 10 17 12 8
    if ($LeftDown) { Px Outline 5 17 3 4; Px Body 6 18 2 2 } else { Px Outline 6 11 3 4; Px Body 7 12 2 2 }
    if ($RightDown) { Px Outline 24 17 3 4; Px Body 24 18 2 2 } else { Px Outline 23 11 3 4; Px Body 23 12 2 2 }
    Px Pink 10 12 2 1; Px Pink 20 12 2 1
    Px Outline 15 13 2 3
}

# The ghost resting flat on the ground like a puddle of sheet.
function Add-PetLie {
    Add-GhostHem 3 27 28 ($script:hemPhase % 2) $false
    Add-GhostRows $lieRows
    Px Belly 8 22 12 4; Px Belly 11 14 4 1
    Px Pink 5 20 2 1; Px Pink 14 20 2 1
    Px Outline 10 21 2 1
}

# The ghost seen from the side, facing left, drifting: the bell stays as it is, and only the bumps of its hem, the last
# few pixels, trail backwards and flicker with D (-3 to 3). TipY is not used.
function Add-PetSide([int]$D, [int]$TipY) {
    $phase = ($D + 3) / 3
    Add-GhostHem 3 20 26 $phase $true
    Add-GhostRows $sideRows
    Px Belly 8 6 3 2
    Px Outline 1 (18 + ($phase % 2)) 3 4; Px Body 2 (19 + ($phase % 2)) 2 2
    Px Iris 5 12 2 2; Px Black 5 12 1 2
    Px Pink 4 15 2 1
    Px Outline 3 16 2 1
}

# A cookie floating in front of the ghost, nibbled away.
function Add-PetFood([int]$Len) {
    if ($Len -gt 0) {
        Px Outline 23 14 ($Len + 1) 4; Px Yellow 24 15 ($Len - 1) 2
        if ($Len -ge 5) { Px Red 26 15 1 1; Px Red 29 16 1 1 }
    } else {
        Px Yellow 24 16 4 1
    }
}

. (Join-Path $PSScriptRoot 'PetAnimations.ps1')
