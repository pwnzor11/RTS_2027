<#
.SYNOPSIS
  RTS_2027 session 15 AI-vs-AI series for the owner's laptop (feeds Q-64 and Q-65).

.DESCRIPTION
  Run it from the ROOT of your RTS_2027 clone:

      git pull
      powershell -ExecutionPolicy Bypass -File tools\measure\run_s15_series.ps1

  Same settings as run_s14_baseline.ps1: tiered AI, 32 seeds x 2 start swaps = 64 games per row, map size 96.
  Results go to balance_runs\s15\ : one .md per row, a .log per row, and s15_summary.md (send that one back).

  Sets:
    A  Repeat of the six session 14 baseline rows, now with D-166 (ash 20, Conscript 50 HP) and D-173 (no-rebuild defeat).
       Same seeds, so every change against docs/balance/s14_laptop_baseline.md is caused by the code, not luck.
    B  Q-65 faction map: Directorate against every faction at tiers 1, 3 and 5, plus Left Behind against the other two,
       to tell "Directorate is weak" apart from "Left Behind is strong".
    C  Q-64 draw horizon: two draw-heavy rows at 60 000 ticks (double length). Shows whether Directorate draws are true
       stalemates or just slow, and is the "before" for the AI siege work.

  The run is deterministic: running more jobs in parallel changes only the wall time, never the numbers.
  If the run stops (sleep, reboot, Ctrl+C), start it again: finished rows are skipped.
  Tip: keep the laptop on mains power and stop it sleeping while it runs (Settings > System > Power).

.PARAMETER Jobs
  Matches to run at once. Default: half the logical CPUs. Use fewer to keep the laptop usable.
.PARAMETER Only
  Run only some sets, e.g. -Only A  or  -Only "A,B".
.PARAMETER SkipBuild
  Reuse the tool already built in balance_runs\s15\bin.
#>
param(
    [int]$Jobs = [Math]::Max(1, [int][Math]::Floor([Environment]::ProcessorCount / 2)),
    [string]$Only = '',
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root 'RTS_2027.slnx'))) {
    throw "Run this from the root of your RTS_2027 folder (RTS_2027.slnx not found in $root)."
}

# ---- 1. Code check: must contain session 14 (D-166 numbers, D-173 defeat rule) ----------------------------
$head = (git rev-parse --short HEAD).Trim()
git merge-base --is-ancestor 42c9f41 HEAD 2>$null
if ($LASTEXITCODE -ne 0) { throw "This clone is older than session 14 (commit 42c9f41). Run 'git pull' first." }
$dirty = git status --porcelain -- src data
if ($dirty) { Write-Warning "src\ or data\ has local changes: the numbers will not match a clean main." }

$outRel = 'balance_runs\s15'
$out = Join-Path $root $outRel
$bin = Join-Path $out 'bin'
New-Item -ItemType Directory -Force -Path $out | Out-Null

# ---- 2. Build the CLI once (Release) -----------------------------------------------------------------------
if (-not $SkipBuild) {
    Write-Host "Building the Tools CLI (Release)..."
    dotnet build src\Tools\Tools.csproj -c Release -o $bin --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
}
$exe = Join-Path $bin 'Tools.exe'
if (-not (Test-Path $exe)) { throw "Tools.exe not found in $bin (run without -SkipBuild)." }

# ---- 3. The series -----------------------------------------------------------------------------------------
# Base = session 14 laptop result for the same row (A / B / draws), for the comparison column.
function Row($set, $fa, $ta, $fb, $tb, $ticks, $base) {
    [pscustomobject]@{ Set = $set; FA = $fa; TA = $ta; FB = $fb; TB = $tb; Ticks = $ticks; Base = $base }
}
$rows = @(
    # Set C first: the long rows, so they do not finish last on their own.
    (Row 'C' 'directorate' 5 'directorate' 1 60000 ''),
    (Row 'C' 'directorate' 3 'authority'   3 60000 ''),
    # Set A: repeat of the session 14 baseline.
    (Row 'A' 'directorate' 3 'authority'   3 30000 '24 / 19 / 21'),
    (Row 'A' 'directorate' 5 'authority'   5 30000 '7 / 37 / 20'),
    (Row 'A' 'authority'   5 'authority'   1 30000 '41 / 18 / 5'),
    (Row 'A' 'directorate' 5 'directorate' 1 30000 '17 / 8 / 39'),
    (Row 'A' 'left_behind' 3 'directorate' 3 30000 '56 / 6 / 2'),
    (Row 'A' 'expedition'  3 'authority'   3 30000 '33 / 26 / 5'),
    # Set B: Q-65 faction map.
    (Row 'B' 'directorate' 1 'authority'   1 30000 ''),
    (Row 'B' 'directorate' 1 'left_behind' 1 30000 ''),
    (Row 'B' 'directorate' 5 'left_behind' 5 30000 ''),
    (Row 'B' 'directorate' 3 'expedition'  3 30000 ''),
    (Row 'B' 'directorate' 5 'expedition'  5 30000 ''),
    (Row 'B' 'left_behind' 3 'authority'   3 30000 ''),
    (Row 'B' 'left_behind' 5 'authority'   5 30000 ''),
    (Row 'B' 'left_behind' 3 'expedition'  3 30000 ''),
    (Row 'B' 'expedition'  5 'authority'   5 30000 '')
)
if ($Only) {
    $want = $Only.Split(',') | ForEach-Object { $_.Trim().ToUpper() }
    $rows = @($rows | Where-Object { $want -contains $_.Set })
}

$dataRow = '^\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|'
$queue = New-Object System.Collections.Queue
foreach ($r in $rows) {
    $name = "{0}_{1}{2}_v_{3}{4}_{5}k" -f $r.Set, $r.FA, $r.TA, $r.FB, $r.TB, ($r.Ticks / 1000)
    $r | Add-Member Name $name
    $r | Add-Member Md (Join-Path $out "$name.md")
    $done = (Test-Path $r.Md) -and (Select-String -Path $r.Md -Pattern $dataRow -Quiet)
    if ($done) { Write-Host "skip (done): $name"; continue }
    $r | Add-Member ArgList ("ai match --a tiered:{0} --b tiered:{1} --faction-a {2} --faction-b {3} --seeds 32 --size 96 --ticks {4} --out {5}\{6}.md" -f $r.TA, $r.TB, $r.FA, $r.FB, $r.Ticks, $outRel, $name)
    $queue.Enqueue($r)
}

# ---- 4. Run with at most $Jobs matches at once ------------------------------------------------------------
$t0 = Get-Date
$total = $queue.Count
Write-Host ("{0} rows to run, {1} at a time, commit {2}. Started {3:HH:mm}." -f $total, $Jobs, $head, $t0)
$running = @()
$finished = 0
$failed = @()
while ($queue.Count -gt 0 -or $running.Count -gt 0) {
    while ($running.Count -lt $Jobs -and $queue.Count -gt 0) {
        $r = $queue.Dequeue()
        $p = Start-Process -FilePath $exe -ArgumentList $r.ArgList -WorkingDirectory $root -NoNewWindow -PassThru `
            -RedirectStandardOutput (Join-Path $out "$($r.Name).log") -RedirectStandardError (Join-Path $out "$($r.Name).err")
        $null = $p.Handle   # keeps the exit code readable after the process ends
        $running += [pscustomobject]@{ Row = $r; Proc = $p; Start = Get-Date }
        Write-Host ("[{0:HH:mm}] start  {1}" -f (Get-Date), $r.Name)
    }
    Start-Sleep -Seconds 10
    $still = @()
    foreach ($j in $running) {
        if (-not $j.Proc.HasExited) { $still += $j; continue }
        $finished++
        $mins = [int]((Get-Date) - $j.Start).TotalMinutes
        if ($j.Proc.ExitCode -ne 0) { $failed += $j.Row.Name; $tag = 'FAILED' } else { $tag = 'done  ' }
        Write-Host ("[{0:HH:mm}] {1} {2} ({3} min; {4}/{5})" -f (Get-Date), $tag, $j.Row.Name, $mins, $finished, $total)
    }
    $running = $still
}

# ---- 5. Summary ---------------------------------------------------------------------------------------------
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Session 15 AI-vs-AI series (owner's laptop)")
[void]$sb.AppendLine()
[void]$sb.AppendLine(("Commit {0}, {1} logical CPUs, {2} jobs, finished {3:yyyy-MM-dd HH:mm}, wall time {4:N0} min. " -f $head, [Environment]::ProcessorCount, $Jobs, (Get-Date), ((Get-Date) - $t0).TotalMinutes) +
    "``ai match`` tiered AI, 32 seeds x 2 start swaps = 64 games per row, size 96. Decisive % = A / (A + B).")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| Set | Match (A v B) | Ticks | A wins | B wins | Draws | Decisive % for A | Avg end tick | S14 (A / B / draws) |")
[void]$sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---|")
foreach ($r in ($rows | Sort-Object Set)) {
    $m = $null
    if (Test-Path $r.Md) { $m = Select-String -Path $r.Md -Pattern $dataRow | Select-Object -First 1 }
    $label = "{0} {1} v {2} {3}" -f $r.FA, $r.TA, $r.FB, $r.TB
    if ($null -eq $m) {
        [void]$sb.AppendLine("| $($r.Set) | $label | $($r.Ticks) | - | - | - | missing | - | $($r.Base) |")
        continue
    }
    $g = $m.Matches[0].Groups
    $a = [int]$g[2].Value; $b = [int]$g[3].Value; $d = [int]$g[4].Value; $end = [int]$g[5].Value
    if ($a + $b -gt 0) { $pct = "{0} %" -f [int][Math]::Round(100.0 * $a / ($a + $b)) } else { $pct = 'n/a' }
    [void]$sb.AppendLine("| $($r.Set) | $label | $($r.Ticks) | $a | $b | $d | $pct | $end | $($r.Base) |")
}
if ($failed.Count -gt 0) {
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("Failed rows (see the .err file): " + ($failed -join ', '))
}
$summary = Join-Path $out 's15_summary.md'
[System.IO.File]::WriteAllText($summary, $sb.ToString())
Write-Host ""
Write-Host $sb.ToString()
Write-Host "Summary written to $summary. Send that file (or the whole balance_runs\s15 folder minus bin) to the next session."
