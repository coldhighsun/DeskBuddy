# Frames for the situations the pet reacts to (Coding, NightCoding, Building, Focus, Reading, Meeting, Chatting), drawn
# as the sitting pet with a prop or a text next to it, so each of them is told apart from standing still at a glance.
# A generator dot-sources this file after PetDrawing.ps1, defining $pal, $textFont, $outDir, Save-Frame, Add-Star and
# Add-ScenarioPet (the sitting pet seen from the front, taking -Eyes, -Tail, -EyeShift and -EyeDy like Add-Pet), and adds
# $ScenarioManifest to the "states" of its manifest.

# Colors of the props, unless the pet already has them.
$propColors = @{
    ScreenGray = (New-Brush 120 130 150)
    ScreenDark = (New-Brush 60 68 90)
    Paper      = (New-Brush 250 248 240)
    PaperLine  = (New-Brush 180 184 200)
}
foreach ($key in $propColors.Keys) {
    if (-not $pal.ContainsKey($key)) { $pal[$key] = $propColors[$key] }
}
$script:pawKey = if ($pal.ContainsKey('Paw')) { 'Paw' } else { 'Cream' }

# An open laptop in front of the pet, seen from the back of its screen. Typing 0 or 1 moves the paws on it.
function Add-Laptop([int]$Typing) {
    Px Outline 6 26 20 4; Px ScreenDark 7 27 18 2
    Px Outline 8 19 16 8; Px ScreenGray 9 20 14 6; Px White 15 22 2 2
    $left = if ($Typing % 2 -eq 0) { 24 } else { 25 }
    $right = if ($Typing % 2 -eq 0) { 25 } else { 24 }
    Px Outline 9 ($left - 1) 6 4; Px $script:pawKey 10 $left 4 2
    Px Outline 17 ($right - 1) 6 4; Px $script:pawKey 18 $right 4 2
}

# An open book in front of the pet. Page 0-3 changes the lines of text, as if reading on.
function Add-Book([int]$Page) {
    Px Outline 7 20 18 9; Px Paper 8 21 8 7; Px Paper 17 21 7 7; Px Outline 16 21 1 7
    $lines = @(0, 1, 2, 1)[$Page]
    foreach ($i in 0..2) {
        $len = if ($i -le $lines) { 5 } else { 3 }
        Px PaperLine 9 (22 + ($i * 2)) $len 1; Px PaperLine 18 (22 + ($i * 2)) $len 1
    }
    Px Outline 15 19 3 1; Px Paper 16 19 1 1
}

# Coding: sits at a laptop, typing, with braces floating up.
$codeTexts = @('{', '{}', '{;}', '{}')
foreach ($f in 0..3) {
    $text = $codeTexts[$f]
    $draw = { Add-ScenarioPet -Eyes open -EyeDy 1 -Tail $f; Add-Laptop $f; Add-Text $text 'White' 90 4 }.GetNewClosure()
    Save-Frame "coding_$f.png" $draw
}

# NightCoding: the same, sleepy, under a moon and stars.
foreach ($f in 0..3) {
    $text = $codeTexts[$f]
    $draw = {
        Add-ScenarioPet -Eyes tired -Tail $f
        Add-Laptop $f
        Add-Text $text 'White' 90 20
        Px Yellow 26 2 4 4; Px Yellow 27 1 2 6
        if ($f % 2 -eq 0) { Add-Star White 22 4 } else { Add-Star Yellow 22 7 }
    }.GetNewClosure()
    Save-Frame "nightcoding_$f.png" $draw
}

# Building: at the laptop while a bar fills and dots count up.
$buildTexts = @('.', '..', '...', '..')
foreach ($f in 0..3) {
    $text = $buildTexts[$f]
    $fill = @(2, 5, 8, 11)[$f]
    $draw = {
        Add-ScenarioPet -Eyes open -EyeDy 1 -EyeShift ($f % 2) -Tail $f
        Add-Laptop $f
        Add-Text $text 'White' 92 4
        Px Outline 18 0 13 3; Px White 19 1 11 1; Px Blue 19 1 $fill 1
    }.GetNewClosure()
    Save-Frame "building_$f.png" $draw
}

# Focus: a red headband with its tails fluttering, a steady stare.
foreach ($f in 0..1) {
    $draw = {
        Add-ScenarioPet -Eyes open -EyeShift (-1 + $f) -Tail 0
        Px Red 10 8 12 1; Px Red 22 8 2 1
        Px Red 23 (9 + $f) 3 1; Px Red 24 (10 + $f) 2 1
    }.GetNewClosure()
    Save-Frame "focus_$f.png" $draw
}

# Reading: a book held open, the eyes moving along the lines.
foreach ($f in 0..3) {
    $shift = @(-1, 0, 1, 0)[$f]
    $draw = { Add-ScenarioPet -Eyes open -EyeDy 1 -EyeShift $shift -Tail $f; Add-Book $f }.GetNewClosure()
    Save-Frame "reading_$f.png" $draw
}

# Meeting: a headset with a microphone, a recording light and the word REC blinking.
foreach ($f in 0..3) {
    $on = ($f -lt 2)
    $draw = {
        Add-ScenarioPet -Eyes open -Tail $f
        Px Black 7 10 2 4; Px Black 23 10 2 4; Px Black 9 6 14 1; Px Black 8 7 1 3; Px Black 23 7 1 3
        Px Black 8 14 1 3; Px Black 9 17 3 1
        if ($on) { Px Red 28 1 3 3; Add-Text 'REC' 'Red' 90 14 }
    }.GetNewClosure()
    Save-Frame "meeting_$f.png" $draw
}

# Chatting: a speech bubble whose dots fill in one by one.
foreach ($f in 0..3) {
    $dots = @(1, 2, 3, 0)[$f]
    $draw = {
        Add-ScenarioPet -Eyes happy -Tail $f
        Px Outline 22 1 10 8; Px White 23 2 8 6; Px Outline 23 9 2 2; Px White 24 9 1 1
        foreach ($i in 0..($dots - 1)) { if ($dots -gt 0) { Px Black (24 + ($i * 2)) 5 1 1 } }
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
