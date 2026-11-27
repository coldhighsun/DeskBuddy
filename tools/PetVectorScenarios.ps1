# Frames for the situations the pet reacts to (Coding, NightCoding, Building, Focus, Reading, Meeting, Chatting), drawn
# as the sitting pet with a prop or a text next to it, so each of them is told apart from standing still at a glance.
# PetVectorAnimations.ps1 dot-sources this file after defining $pal, $outDir, $HeadBox, Save-Frame and Add-ScenarioPet (the
# sitting pet seen from the front, taking -Eyes, -Tail, -EyeShift and -EyeLift like Add-Pet), and adds $ScenarioManifest to
# the "states" of its manifest. Everything is in drawing units (see PetVectorDrawing.ps1).

# An open laptop in front of the pet, seen from the back of its screen. Typing 0 or 1 moves the paws on it.
function Add-Laptop([int]$Typing) {
    Add-HdRoundRect 'ScreenDark' 12 53 40 7 2.5 'PropEdge' 1
    Add-HdRoundRect 'ScreenGray' 16 38 32 16 2.5 'PropEdge' 1
    # The pet faces the user, so its hands are behind the lid and never drawn; typing shows as the logo flickering and key sparks.
    Add-HdOval $(if ($Typing % 2 -eq 0) { 'White' } else { 'PropEdge' }) 32 46 2.2 2.2 ''
    $sparks = @(@(11, 51), @(53, 49), @(14, 47), @(50, 54))
    foreach ($i in 0..1) {
        $spark = $sparks[($Typing % 2) * 2 + $i]
        Add-HdSparkle 'Yellow' $spark[0] $spark[1] 1.6
    }
}

# An open book in front of the pet. Page 0-3 changes the lines of text, as if reading on.
function Add-Book([int]$Page) {
    Add-HdRoundRect 'Paper' 14 41 18 16 2 'PropEdge' 1
    Add-HdRoundRect 'Paper' 32 41 18 16 2 'PropEdge' 1
    Add-HdStroke @(32, 41.6, 32, 56.4) 'PropEdge' 1
    $lines = @(0, 1, 2, 1)[$Page]
    foreach ($i in 0..2) {
        $len = if ($i -le $lines) { 11 } else { 7 }
        $y = 45.5 + $i * 4
        Add-HdStroke @(17, $y, (17 + $len), $y) 'PaperLine' 1
        Add-HdStroke @(35, $y, (35 + $len), $y) 'PaperLine' 1
    }
    Add-HdPoly 'Red' @(34.5, 56, 37.5, 56, 37.5, 61, 36, 59.6, 34.5, 61) '' 0
}

# Coding: sits at a laptop, typing, with braces floating up.
$codeTexts = @('{', '{}', '{;}', '{}')
foreach ($f in 0..3) {
    $text = $codeTexts[$f]
    $draw = { Add-ScenarioPet -Eyes open -EyeLift 1 -Tail $f; Add-Laptop $f; Add-HdLabel $text 'White' 45 2 }.GetNewClosure()
    Save-Frame "coding_$f.png" $draw
}

# NightCoding: the same, sleepy, under a moon and stars.
foreach ($f in 0..3) {
    $text = $codeTexts[$f]
    $draw = {
        Add-ScenarioPet -Eyes tired -Tail $f
        Add-Laptop $f
        Add-HdLabel $text 'White' 45 12
        Add-HdMoon 'Yellow' 56 7 5
        if ($f % 2 -eq 0) { Add-HdSparkle 'White' 45 8 2.6 } else { Add-HdSparkle 'Yellow' 45 14 2.6 }
    }.GetNewClosure()
    Save-Frame "nightcoding_$f.png" $draw
}

# Building: at the laptop while a bar fills and dots count up.
$buildTexts = @('.', '..', '...', '..')
foreach ($f in 0..3) {
    $text = $buildTexts[$f]
    $fill = @(4, 10, 16, 22)[$f]
    $draw = {
        Add-ScenarioPet -Eyes open -EyeLift 1 -EyeShift ($f % 2) -Tail $f
        Add-Laptop $f
        Add-HdLabel $text 'White' 46 7
        Add-HdRoundRect 'White' 36 0.8 26 5 2.5 'PropEdge' 1
        Add-HdRoundRect 'Blue' 37.5 2.3 $fill 2 1 '' 0
    }.GetNewClosure()
    Save-Frame "building_$f.png" $draw
}

# Focus: a red headband with its tails fluttering, a steady stare.
foreach ($f in 0..1) {
    $draw = {
        Add-ScenarioPet -Eyes open -EyeShift (-1.4 + 2.8 * $f) -Tail 0
        $x0 = $HeadBox[0]; $y0 = $HeadBox[1]; $x1 = $HeadBox[2]
        $band = Get-HdBezier ($x0 + 0.5) ($y0 + 8.8) ($x0 + 7) ($y0 + 4) ($x1 - 7) ($y0 + 4) ($x1 - 0.5) ($y0 + 8.8) 16
        Add-HdStroke $band 'Outline' 4
        Add-HdStroke $band 'Red' 2.6
        $tails = @(@(($x1 - 1.5), ($y0 + 8), ($x1 + 5.5), ($y0 + 9.5 + $f * 1.5)), @(($x1 - 1.5), ($y0 + 8.6), ($x1 + 4.5), ($y0 + 13 + $f * 1.5)))
        foreach ($tail in $tails) { Add-HdLimb $tail 'Red' 1.8 }
    }.GetNewClosure()
    Save-Frame "focus_$f.png" $draw
}

# Reading: a book held open, the eyes moving along the lines.
foreach ($f in 0..3) {
    $shift = @(-1.4, 0, 1.4, 0)[$f]
    $draw = { Add-ScenarioPet -Eyes open -EyeLift 1 -EyeShift $shift -Tail $f; Add-Book $f }.GetNewClosure()
    Save-Frame "reading_$f.png" $draw
}

# Meeting: a headset with a microphone, a recording light and the word REC blinking.
foreach ($f in 0..3) {
    $on = ($f -lt 2)
    $draw = {
        Add-ScenarioPet -Eyes open -Tail $f
        $x0 = $HeadBox[0]; $y0 = $HeadBox[1]; $x1 = $HeadBox[2]; $cx = ($x0 + $x1) / 2
        $arch = Get-HdBezier ($x0 - 0.8) ($y0 + 10) ($x0 + 1) ($y0 - 5) ($x1 - 1) ($y0 - 5) ($x1 + 0.8) ($y0 + 10) 20
        Add-HdStroke $arch 'Black' 1.8
        Add-HdOval 'Black' ($x0 - 0.8) ($y0 + 12) 2.4 4.2 ''
        Add-HdOval 'Black' ($x1 + 0.8) ($y0 + 12) 2.4 4.2 ''
        Add-HdStroke @(($x0 - 1), ($y0 + 16), ($x0 + 1.5), ($y0 + 22), ($cx - 4), ($y0 + 23.5)) 'Black' 1.1
        Add-HdOval 'Black' ($cx - 4) ($y0 + 23.5) 1.7 1.3 ''
        if ($on) {
            Add-HdOval 'Red' 59 4 2.4 2.4 'PropEdge' 0.8
            Add-HdLabel 'REC' 'Red' 45 8 'PropEdge' 8
        }
    }.GetNewClosure()
    Save-Frame "meeting_$f.png" $draw
}

# Chatting: a speech bubble whose dots fill in one by one.
foreach ($f in 0..3) {
    $dots = @(1, 2, 3, 0)[$f]
    $draw = {
        Add-ScenarioPet -Eyes happy -Tail $f
        Add-HdRoundRect 'White' 44 1 18 14 5 'PropEdge' 1
        Add-HdPoly 'White' @(46, 13.6, 51.5, 13.6, 45, 18.6) 'PropEdge' 1
        Add-HdRoundRect 'White' 46.6 12.6 4.4 2 0.5 '' 0
        foreach ($i in 0..($dots - 1)) { if ($dots -gt 0) { Add-HdOval 'PropEdge' (48.5 + $i * 4.2) 8 1.2 1.2 '' } }
    }.GetNewClosure()
    Save-Frame "chatting_$f.png" $draw
}

$ScenarioManifest = @"
    "Coding":  { "fps": 4, "frames": ["coding_0.png", "coding_1.png", "coding_2.png", "coding_3.png"] },
    "NightCoding": { "fps": 3, "frames": ["nightcoding_0.png", "nightcoding_1.png", "nightcoding_2.png", "nightcoding_3.png"] },
    "Building": { "fps": 4, "frames": ["building_0.png", "building_1.png", "building_2.png", "building_3.png"] },
    "Focus":   { "fps": 3, "frames": ["focus_0.png", "focus_1.png"] },
    "Reading": { "fps": 2, "frames": ["reading_0.png", "reading_1.png", "reading_2.png", "reading_3.png"] },
    "Meeting": { "fps": 3, "frames": ["meeting_0.png", "meeting_1.png", "meeting_2.png", "meeting_3.png"] },
    "Chatting": { "fps": 3, "frames": ["chatting_0.png", "chatting_1.png", "chatting_2.png", "chatting_3.png"] },

"@
