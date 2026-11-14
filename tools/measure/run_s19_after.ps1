<#
.SYNOPSIS
  RTS_2027 session 19 "after" series for the owner's laptop: the same 17 rows as the session 18 audit series, after the M13 fixes
  measured on main as it is, plus the faction matrix references for the balance findings.

.DESCRIPTION
  Run it from the ROOT of your RTS_2027 clone:

      git pull
      powershell -ExecutionPolicy Bypass -File tools\measure\run_s19_after.ps1

  EXPECTED RUN TIME: about 95 minutes with the default jobs (17 rows of 64 games; the session 17 handoff measured a 16-row 64-game
  series at about 90 minutes on this laptop). Use -Groups to run part of it.

  32 seeds x 2 start swaps = 64 games per row, map size 96 (medium), 30 000 ticks, tier 3 on both sides, --income 1.
  Group 1 (AI-01, tender spawn): Expedition mirror on glass_coast, rust_belt, ash_plains and mountains, layout 1.
  Group 2 (AI-08 / DATA-TOOLS-01, start skew): Directorate mirror and Left Behind mirror on mountains, layouts 1 and 2.
  Group 3 (balance reference): the six cross-faction pairs on rust_belt layout 1.
  Group 4 (ships and subs in play): Expedition v Authority, v Directorate, v Left Behind on glass_coast layout 1.
  Results go to balance_runs\s19after\ : one .md and .log per row and s19after_summary.md (send that one back).
  The summary adds, per start corner, the mean income (harvested + shipments) at 3 / 5 minutes over all games, at 10 minutes over the games
  still running at tick 12 000 (D-201), and the mean shipment credits at 3 / 5 minutes (Expedition sides only).
  Deterministic; if it stops, start it again: finished rows are skipped. It runs main as it is (no experimental switches).

.PARAMETER Jobs
  Matches to run at once. Default: half the logical CPUs.
.PARAMETER SkipBuild
  Reuse the tool already built in balance_runs\s19after\bin.
.PARAMETER Groups
  Row groups to run (1-4), default all four.
.PARAMETER OutName
  Output folder under balance_runs (default s19after). Use a new name to measure another commit, e.g. -OutName s21after;
  finished rows in the folder are skipped, so the same name would skip everything.
.PARAMETER Only
  Row names to run (as in the .md file names, e.g. g3_rust_belt_l1_authority3_v_expedition3_30k); default all rows of the groups.
.PARAMETER DryRun
  Print the rows and the command lines without building or running anything.
#>
param(
    [int]$Jobs = [Math]::Max(1, [int][Math]::Floor([Environment]::ProcessorCount / 2)),
    [switch]$SkipBuild,
    [int[]]$Groups = @(1, 2, 3, 4),
    [switch]$DryRun,
    [string]$OutName = 's19after',
    [string[]]$Only
)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root 'RTS_2027.slnx'))) {
    throw "Run this from the root of your RTS_2027 folder (RTS_2027.slnx not found in $root)."
}
$head = (git rev-parse --short HEAD).Trim()
$dirty = git status --porcelain -- src data
if ($dirty) { Write-Warning "src\ or data\ has local changes: the numbers will not match a clean main." }

$outRel = "balance_runs\$OutName"
$out = Join-Path $root $outRel
$bin = Join-Path $out 'bin'

function Row($group, $biome, $layout, $fa, $fb) {
    [pscustomobject]@{ Group = $group; Biome = $biome; Layout = $layout; FA = $fa; TA = 3; FB = $fb; TB = 3; Ticks = 30000 }
}
$rows = @(
    # group 1: Expedition mirror, tender spawn (AI-01); income lines carry the shipment split
    (Row 1 'glass_coast' 1 'expedition' 'expedition'),
    (Row 1 'rust_belt'   1 'expedition' 'expedition'),
    (Row 1 'ash_plains'  1 'expedition' 'expedition'),
    (Row 1 'mountains'   1 'expedition' 'expedition'),
    # group 2: start skew on Mountains (AI-08, DATA-TOOLS-01)
    (Row 2 'mountains' 1 'directorate' 'directorate'),
    (Row 2 'mountains' 2 'directorate' 'directorate'),
    (Row 2 'mountains' 1 'left_behind' 'left_behind'),
    (Row 2 'mountains' 2 'left_behind' 'left_behind'),
    # group 3: faction matrix at tier 3, rust_belt layout 1
    (Row 3 'rust_belt' 1 'authority'   'directorate'),
    (Row 3 'rust_belt' 1 'authority'   'left_behind'),
    (Row 3 'rust_belt' 1 'authority'   'expedition'),
    (Row 3 'rust_belt' 1 'directorate' 'left_behind'),
    (Row 3 'rust_belt' 1 'directorate' 'expedition'),
    (Row 3 'rust_belt' 1 'left_behind' 'expedition'),
    # group 4: Expedition v each other faction on glass_coast (ships and subs in play)
    (Row 4 'glass_coast' 1 'expedition' 'authority'),
    (Row 4 'glass_coast' 1 'expedition' 'directorate'),
    (Row 4 'glass_coast' 1 'expedition' 'left_behind')
) | Where-Object { $Groups -contains $_.Group }

$dataRow = '^\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|'
# D-242 rows also carry `| A win % | AdjW | AdjL | AdjEven |` after the first five numbers (older .md files do not: shown as '-').
$adjRow = '^\|\s*\d+\s*\|\s*\d+\s*\|\s*\d+\s*\|\s*\d+\s*\|\s*\d+\s*\|\s*[^|]*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|'
$queue = New-Object System.Collections.Queue
$all = @()
foreach ($r in $rows) {
    $name = "g{0}_{1}_l{2}_{3}{4}_v_{5}{6}_{7}k" -f $r.Group, $r.Biome, $r.Layout, $r.FA, $r.TA, $r.FB, $r.TB, ($r.Ticks / 1000)
    $r | Add-Member Name $name
    if ($Only -and -not ((($Only -join ',') -split ',') -contains $name)) { continue }
    $r | Add-Member Md (Join-Path $out "$name.md")
    $r | Add-Member Log (Join-Path $out "$name.log")
    $r | Add-Member ArgList ("ai match --a tiered:{0} --b tiered:{1} --faction-a {2} --faction-b {3} --seeds 32 --size 96 --layout {4} --biome {5} --ticks {6} --income 1 --out {7}\{8}.md" -f $r.TA, $r.TB, $r.FA, $r.FB, $r.Layout, $r.Biome, $r.Ticks, $outRel, $name)
    $all += $r
    $done = (Test-Path $r.Md) -and (Select-String -Path $r.Md -Pattern $dataRow -Quiet)
    if ($done) { Write-Host "skip (done): $name"; continue }
    $queue.Enqueue($r)
}

if ($DryRun) {
    Write-Host ("Dry run: {0} rows selected, {1} to run, {2} at a time, commit {3}. Output folder {4}." -f $all.Count, $queue.Count, $Jobs, $head, $outRel)
    foreach ($r in $queue) { Write-Host ("  {0}: Tools.exe {1}" -f $r.Name, $r.ArgList) }
    return
}

New-Item -ItemType Directory -Force -Path $out | Out-Null
if (-not $SkipBuild) {
    Write-Host "Building the Tools CLI (Release)..."
    dotnet build src\Tools\Tools.csproj -c Release -o $bin --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
}
$exe = Join-Path $bin 'Tools.exe'
if (-not (Test-Path $exe)) { throw "Tools.exe not found in $bin (run without -SkipBuild)." }

$t0 = Get-Date
$total = $queue.Count
Write-Host ("{0} rows to run, {1} at a time, commit {2}. Started {3:HH:mm}; expect about 95 minutes for all 17 rows." -f $total, $Jobs, $head, $t0)
$running = @(); $finished = 0; $failed = @()
while ($queue.Count -gt 0 -or $running.Count -gt 0) {
    while ($running.Count -lt $Jobs -and $queue.Count -gt 0) {
        $r = $queue.Dequeue()
        $p = Start-Process -FilePath $exe -ArgumentList $r.ArgList -WorkingDirectory $root -NoNewWindow -PassThru `
            -RedirectStandardOutput $r.Log -RedirectStandardError (Join-Path $out "$($r.Name).err")
        $null = $p.Handle
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

# Per start corner from the .log: income (harvested + shipments) at 3 / 5 min over all games, at 10 min over games still running at
# tick 12 000, and shipment credits at 3 / 5 min. Lines end in CRLF on Windows: strip "`r" before matching; the literal "3/5/10" in the
# label is skipped by anchoring on "A " / "B " before the numbers.
function Read-Income($log) {
    $s = @{ N = @(0, 0); I3 = @(0.0, 0.0); I5 = @(0.0, 0.0); N10 = @(0, 0); I10 = @(0.0, 0.0); SN = @(0, 0); S3 = @(0.0, 0.0); S5 = @(0.0, 0.0) }
    if (-not (Test-Path $log)) { return $null }
    $aStart = -1; $end = 0
    foreach ($raw in [System.IO.File]::ReadAllLines($log)) {
        $line = $raw.TrimEnd("`r")
        $g = [regex]::Match($line, '^seed \d+ game \d+ \(A in slot \d, A at start (\d)\): .* at tick (\d+)')
        if ($g.Success) { $aStart = [int]$g.Groups[1].Value; $end = [int]$g.Groups[2].Value; continue }
        if ($aStart -lt 0) { continue }
        $m = [regex]::Match($line, 'income at 3/5/10 min \(harvested \+ shipments\): A (\d+)/(\d+)/(\d+) \| B (\d+)/(\d+)/(\d+)')
        if ($m.Success) {
            for ($side = 0; $side -lt 2; $side++) {
                $st = $aStart; if ($side -eq 1) { $st = 1 - $aStart }
                $o = 1 + 3 * $side
                $s.N[$st]++
                $s.I3[$st] += [double]$m.Groups[$o].Value
                $s.I5[$st] += [double]$m.Groups[$o + 1].Value
                if ($end -gt 12000) { $s.N10[$st]++; $s.I10[$st] += [double]$m.Groups[$o + 2].Value }
            }
            continue
        }
        $e = [regex]::Match($line, 'shipments 3/5/10 A (\d+)/(\d+)/\d+ B (\d+)/(\d+)/\d+')
        if ($e.Success) {
            for ($side = 0; $side -lt 2; $side++) {
                $st = $aStart; if ($side -eq 1) { $st = 1 - $aStart }
                $o = 1 + 2 * $side
                $s.SN[$st]++
                $s.S3[$st] += [double]$e.Groups[$o].Value
                $s.S5[$st] += [double]$e.Groups[$o + 1].Value
            }
        }
    }
    return $s
}
function Mean($sum, $n) { if ($n -gt 0) { return [string][int][Math]::Round($sum / $n) } else { return '-' } }

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Series $OutName summary (owner's laptop)")
[void]$sb.AppendLine()
[void]$sb.AppendLine(("Commit {0}, {1} logical CPUs, {2} jobs, finished {3:yyyy-MM-dd HH:mm}, wall time {4:N0} min. " -f $head, [Environment]::ProcessorCount, $Jobs, (Get-Date), ((Get-Date) - $t0).TotalMinutes) +
    "``ai match`` tiered AI tier 3, 32 seeds x 2 start swaps = 64 games per row, size 96, --income 1. Start share = larger of the two start-corner win counts / decisive games.")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| Group | Biome | Layout | Match (A v B) | A wins | B wins | Draws | AdjW | AdjL | AdjEven | Avg end tick | Start 0 wins | Start 1 wins | Larger start share |")
[void]$sb.AppendLine("|---:|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|")
foreach ($r in $all) {
    $m = $null
    if (Test-Path $r.Md) { $m = Select-String -Path $r.Md -Pattern $dataRow | Select-Object -First 1 }
    $label = "{0} {1} v {2} {3}" -f $r.FA, $r.TA, $r.FB, $r.TB
    if ($null -eq $m) { [void]$sb.AppendLine("| $($r.Group) | $($r.Biome) | $($r.Layout) | $label | - | - | - | - | - | - | - | - | - | missing |"); continue }
    $g = $m.Matches[0].Groups
    $a = [int]$g[2].Value; $b = [int]$g[3].Value; $d = [int]$g[4].Value; $end = [int]$g[5].Value
    $s0 = '-'; $s1 = '-'; $share = 'n/a'
    $adjW = '-'; $adjL = '-'; $adjE = '-'
    $am = Select-String -Path $r.Md -Pattern $adjRow | Select-Object -First 1
    if ($null -ne $am) { $adjW = $am.Matches[0].Groups[1].Value; $adjL = $am.Matches[0].Groups[2].Value; $adjE = $am.Matches[0].Groups[3].Value }
    $sm = Select-String -Path $r.Md -Pattern 'start 0 \(\d+,\d+\) (\d+), start 1 \(mirror\) (\d+)' | Select-Object -First 1
    if ($null -ne $sm) {
        $s0 = [int]$sm.Matches[0].Groups[1].Value; $s1 = [int]$sm.Matches[0].Groups[2].Value
        if ($s0 + $s1 -gt 0) { $share = "{0} %" -f [int][Math]::Round(100.0 * [Math]::Max($s0, $s1) / ($s0 + $s1)) }
    }
    [void]$sb.AppendLine("| $($r.Group) | $($r.Biome) | $($r.Layout) | $label | $a | $b | $d | $adjW | $adjL | $adjE | $end | $s0 | $s1 | $share |")
}
[void]$sb.AppendLine()
[void]$sb.AppendLine("## Income by start corner (mean per side; 10 min over games still running at tick 12 000)")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| Group | Biome | Layout | Match (A v B) | Start 0: 3 / 5 / 10 min | Start 1: 3 / 5 / 10 min | Live at 10 min (sides) | Shipments start 0: 3 / 5 min | Shipments start 1: 3 / 5 min |")
[void]$sb.AppendLine("|---:|---|---:|---|---|---|---:|---|---|")
foreach ($r in $all) {
    $label = "{0} {1} v {2} {3}" -f $r.FA, $r.TA, $r.FB, $r.TB
    $s = Read-Income $r.Log
    if ($null -eq $s -or ($s.N[0] + $s.N[1]) -eq 0) { [void]$sb.AppendLine("| $($r.Group) | $($r.Biome) | $($r.Layout) | $label | - | - | - | - | - |"); continue }
    $c0 = "{0} / {1} / {2}" -f (Mean $s.I3[0] $s.N[0]), (Mean $s.I5[0] $s.N[0]), (Mean $s.I10[0] $s.N10[0])
    $c1 = "{0} / {1} / {2}" -f (Mean $s.I3[1] $s.N[1]), (Mean $s.I5[1] $s.N[1]), (Mean $s.I10[1] $s.N10[1])
    $h0 = "{0} / {1}" -f (Mean $s.S3[0] $s.SN[0]), (Mean $s.S5[0] $s.SN[0])
    $h1 = "{0} / {1}" -f (Mean $s.S3[1] $s.SN[1]), (Mean $s.S5[1] $s.SN[1])
    [void]$sb.AppendLine("| $($r.Group) | $($r.Biome) | $($r.Layout) | $label | $c0 | $c1 | $($s.N10[0] + $s.N10[1]) | $h0 | $h1 |")
}
[void]$sb.AppendLine()
[void]$sb.AppendLine("Shipment columns: both sides counted in a mirror; in a cross-faction row a non-Expedition side reports 0. In group 3 and 4 rows, read the start columns as start corners, not factions; the per-faction split is A v B in the first table.")
if ($failed.Count -gt 0) { [void]$sb.AppendLine(); [void]$sb.AppendLine("Failed rows (see the .err file): " + ($failed -join ', ')) }
$summary = Join-Path $out ($OutName + '_summary.md')
[System.IO.File]::WriteAllText($summary, $sb.ToString())
Write-Host $sb.ToString()
Write-Host "Summary written to $summary. Send that file back."
