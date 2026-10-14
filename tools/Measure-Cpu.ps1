<#
.SYNOPSIS
    Measures the CPU use of a running DeskBuddy build.
.DESCRIPTION
    Starts the executable with a temporary data directory, then samples its processor time every interval. The pet can be
    forced to walk continuously (-Walk) or to stay idle (default), and the samples show both the start-up cost (JIT) and the
    steady state. Results are CPU percent of one core.
.PARAMETER Exe
    Path to DeskBuddy.exe.
.PARAMETER Seconds
    How long to sample.
.PARAMETER IntervalSeconds
    Sampling interval.
.PARAMETER Walk
    Keep the pet walking for the whole run.
.PARAMETER Still
    Keep the pet standing still for the whole run.
#>
param(
    [Parameter(Mandatory)] [string] $Exe,
    [int] $Seconds = 60,
    [int] $IntervalSeconds = 5,
    [switch] $Walk,
    [switch] $Still
)

$data = Join-Path ([IO.Path]::GetTempPath()) ("DeskBuddyCpu-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $data | Out-Null
$env:DataDirectory = $data

# A busy machine would otherwise put the pet into its Tired state and hide the cost of walking.
$env:SystemLoad__HighCpuPercent = "101"
$env:SystemLoad__HighMemoryPercent = "101"

# Nobody is touching the keyboard during a measurement, so the pet would fall asleep after a few minutes.
$env:Behavior__SleepAfter = "12:00:00"
if ($Walk) {
    $env:Behavior__IdleDwellMin = "00:00:01"
    $env:Behavior__IdleDwellMax = "00:00:01"
    $env:Behavior__WalkDurationMin = "01:00:00"
    $env:Behavior__WalkDurationMax = "01:00:00"
}
if ($Still) {
    $env:Behavior__IdleDwellMin = "01:00:00"
    $env:Behavior__IdleDwellMax = "01:00:00"
}

$process = Start-Process -FilePath $Exe -PassThru
try {
    $cores = [Environment]::ProcessorCount
    $last = $process.TotalProcessorTime
    $samples = @()
    for ($elapsed = $IntervalSeconds; $elapsed -le $Seconds; $elapsed += $IntervalSeconds) {
        Start-Sleep -Seconds $IntervalSeconds
        $process.Refresh()
        $now = $process.TotalProcessorTime
        $percent = ($now - $last).TotalSeconds / $IntervalSeconds * 100
        $last = $now
        $samples += [pscustomobject]@{ At = $elapsed; CpuPercentOfOneCore = [math]::Round($percent, 1) }
    }
    $samples | Format-Table -AutoSize
    $tail = $samples | Select-Object -Last ([math]::Max(1, [int]($samples.Count / 2)))
    $avg = ($tail | Measure-Object CpuPercentOfOneCore -Average).Average
    "Total CPU time: {0:N1} s over {1} s" -f $process.TotalProcessorTime.TotalSeconds, $Seconds
    "Second-half average: {0:N1}% of one core ({1} logical cores); working set {2:N0} MB" -f $avg, $cores, ($process.WorkingSet64 / 1MB)
}
finally {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
}
