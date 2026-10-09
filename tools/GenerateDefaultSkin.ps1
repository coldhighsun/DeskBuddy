# Generates the placeholder default skin (hand-drawn blob, no third-party artwork). Art faces right.
# Usage: powershell -NoProfile -File tools/GenerateDefaultSkin.ps1
Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot '..\skins\default'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Get-ChildItem $outDir -Filter *.png | Remove-Item

$bodyBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 186, 120))
$footBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 220, 140, 80))
$darkBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 60, 40, 30))
$darkPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 60, 40, 30)), 3
$textFont = New-Object System.Drawing.Font 'Segoe UI', 14, ([System.Drawing.FontStyle]::Bold)

# Draws one frame. Eyes: open | closed | happy. Mouth: none | smile | oh. Feet: -1 (hidden) or stride offset.
function New-Frame {
    param(
        [string]$Name, [int]$W = 84, [int]$H = 80, [int]$Lift = 0,
        [string]$Eyes = 'open', [string]$Mouth = 'none', [int]$Feet = 0, [string]$Extra = ''
    )
    $bmp = New-Object System.Drawing.Bitmap 128, 128
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    $x = (128 - $W) / 2
    $y = 120 - $H - $Lift

    if ($Feet -ge 0) {
        $g.FillEllipse($footBrush, (64 - 28 + $Feet), 110, 22, 12)
        $g.FillEllipse($footBrush, (64 + 6 - $Feet), 110, 22, 12)
    }
    $g.FillEllipse($bodyBrush, $x, $y, $W, $H)

    $ey = $y + $H * 0.35
    $e1 = 64 - 16 + 4
    $e2 = 64 + 8 + 4
    switch ($Eyes) {
        'open'   { $g.FillEllipse($darkBrush, $e1, $ey, 9, 12); $g.FillEllipse($darkBrush, $e2, $ey, 9, 12) }
        'closed' { $g.DrawArc($darkPen, $e1 - 2, $ey, 14, 8, 0, 180); $g.DrawArc($darkPen, $e2 - 2, $ey, 14, 8, 0, 180) }
        'happy'  { $g.DrawArc($darkPen, $e1 - 2, $ey, 14, 10, 180, 180); $g.DrawArc($darkPen, $e2 - 2, $ey, 14, 10, 180, 180) }
    }

    $my = $ey + 20
    switch ($Mouth) {
        'smile' { $g.DrawArc($darkPen, 64 - 4, $my - 6, 18, 12, 0, 180) }
        'oh'    { $g.FillEllipse($darkBrush, 64 + 2, $my - 2, 9, 11) }
    }

    if ($Extra) { $g.DrawString($Extra, $textFont, $darkBrush, 86, [Math]::Max(0, $y - 14)) }

    $bmp.Save((Join-Path $outDir $Name), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()
}

# Idle: gentle breathing.
New-Frame 'idle_0.png' -Feet 0
New-Frame 'idle_1.png' -W 90 -H 74 -Feet 0

# Walk: hop with alternating stride.
New-Frame 'walk_0.png' -Feet 10 -Mouth smile
New-Frame 'walk_1.png' -Feet 0 -Lift 6 -H 76 -Mouth smile
New-Frame 'walk_2.png' -Feet -10 -Mouth smile
New-Frame 'walk_3.png' -Feet 0 -Lift 6 -H 76 -Mouth smile

# Sleep: squashed, eyes closed, floating z.
New-Frame 'sleep_0.png' -W 94 -H 66 -Eyes closed -Feet -1
New-Frame 'sleep_1.png' -W 98 -H 70 -Eyes closed -Feet -1 -Extra 'z'

# Drag: stretched, surprised, feet dangling.
New-Frame 'drag_0.png' -W 72 -H 92 -Lift 0 -Eyes open -Mouth oh -Feet 4
New-Frame 'drag_1.png' -W 70 -H 94 -Lift 0 -Eyes open -Mouth oh -Feet -4

# Happy: bounce with smiling eyes.
New-Frame 'happy_0.png' -Lift 12 -Eyes happy -Mouth smile
New-Frame 'happy_1.png' -Lift 0 -H 74 -W 90 -Eyes happy -Mouth smile

@'
{
  "name": "default",
  "frameSize": [128, 128],
  "states": {
    "Idle":  { "fps": 2,  "frames": ["idle_0.png", "idle_1.png"] },
    "Walk":  { "fps": 8,  "frames": ["walk_0.png", "walk_1.png", "walk_2.png", "walk_3.png"] },
    "Sleep": { "fps": 1,  "frames": ["sleep_0.png", "sleep_1.png"] },
    "Drag":  { "fps": 6,  "frames": ["drag_0.png", "drag_1.png"] },
    "Happy": { "fps": 5,  "frames": ["happy_0.png", "happy_1.png"] }
  }
}
'@ | Set-Content -Encoding UTF8 (Join-Path $outDir 'manifest.json')

Write-Host "Default skin written to $outDir"
