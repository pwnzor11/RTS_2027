<#
.SYNOPSIS
  RTS_2027 session 22 mop-up A/B for the owner's laptop: does the new mop-up (the AI finishing a beaten opponent) turn draws into wins
  without costing decisive games? Every row is played twice, `--mopup on` and `--mopup off`, on the same seeds.

.DESCRIPTION
  Run it from the ROOT of your RTS_2027 clone:

      git pull
      powershell -ExecutionPolicy Bypass -File tools\measure\run_s22_mopup.ps1

  EXPECTED RUN TIME: about 100 minutes with the default jobs (18 matches of 64 games; a 17-row 64-game series took about 95 minutes).
  Use -Only to run part of it.

  32 seeds x 2 start swaps = 64 games per match, map size 96 (medium), 30 000 ticks, --income 1 (the same row format as run_s19_after.ps1).
  Rows (each one with mopup on and off):
    Directorate 5 v 1, 4 v 3, 5 v 4, 3 v 3 mirror (default map, as the session 14 mop-up table);
    Authority 5 v 1 and 4 v 3 (default map);
    Authority 3 v Directorate 3, v Left Behind 3 and v Expedition 3 on rust_belt layout 1 (the Authority rows of docs/balance/s21_after_series.md).
  Results go to balance_runs\s22mopup\ : one .md and .log per match and s22mopup_summary.md (send that one back). The summary lists, per row,
  A wins / B wins / draws and the average end tick with mopup on and off, and the change in draws and in decisive games.
  Deterministic; if it stops, start it again: finished matches are skipped.

.PARAMETER Jobs
  Matches to run at once. Default: half the logical CPUs.
.PARAMETER SkipBuild
  Reuse the tool already built in balance_runs\s22mopup\bin.
.PARAMETER OutName
  Output folder under balance_runs (default s22mopup). Use a new name to measure another commit; finished matches in the folder are skipped.
.PARAMETER Only
  Match names to run (as in the .md file names, e.g. dir_directorate5_v_directorate1_mopup_on); default all.
.PARAMETER DryRun
  Print the matches and the command lines without building or running anything.
#>
param(
    [int]$Jobs = [Math]::Max(1, [int][Math]::Floor([Environment]::ProcessorCount / 2)),
    [switch]$SkipBuild,
    [switch]$DryRun,
    [string]$OutName = 's22mopup',
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

# Biome '' = the default map (no --biome / --layout flags), as the session 14 mop-up tables
function Row($key, $biome, $layout, $fa, $ta, $fb, $tb) {
    [pscustomobject]@{ Key = $key; Biome = $biome; Layout = $layout; FA = $fa; TA = $ta; FB = $fb; TB = $tb; Ticks = 30000 }
}
$rows = @(
    (Row 'dir'  ''          0 'directorate' 5 'directorate' 1),
    (Row 'dir'  ''          0 'directorate' 4 'directorate' 3),
    (Row 'dir'  ''          0 'directorate' 5 'directorate' 4),
    (Row 'dir'  ''          0 'directorate' 3 'directorate' 3),
    (Row 'auth' ''          0 'authority'   5 'authority'   1),
    (Row 'auth' ''          0 'authority'   4 'authority'   3),
    (Row 'rb'   'rust_belt' 1 'authority'   3 'directorate' 3),
    (Row 'rb'   'rust_belt' 1 'authority'   3 'left_behind' 3),
    (Row 'rb'   'rust_belt' 1 'authority'   3 'expedition'  3)
)

$dataRow = '^\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|'
$queue = New-Object System.Collections.Queue
$all = @()
foreach ($r in $rows) {
    foreach ($mop in @('on', 'off')) {
        $c = [pscustomobject]@{ Key = $r.Key; Biome = $r.Biome; Layout = $r.Layout; FA = $r.FA; TA = $r.TA; FB = $r.FB; TB = $r.TB; Ticks = $r.Ticks }
        $base = "{0}_{1}{2}_v_{3}{4}{5}" -f $c.Key, $c.FA, $c.TA, $c.FB, $c.TB, $(if ($c.Biome) { '_' + $c.Biome + '_l' + $c.Layout } else { '' })
        $name = "{0}_mopup_{1}" -f $base, $mop
        $c | Add-Member Mopup $mop
        $c | Add-Member Pair $base
        $c | Add-Member Name $name
        if ($Only -and -not ((($Only -join ',') -split ',') -contains $name)) { continue }
        $c | Add-Member Md (Join-Path $out "$name.md")
        $c | Add-Member Log (Join-Path $out "$name.log")
        $map = ''
        if ($c.Biome) { $map = " --layout {0} --biome {1}" -f $c.Layout, $c.Biome }
        $c | Add-Member ArgList ("ai match --a tiered:{0} --b tiered:{1} --faction-a {2} --faction-b {3} --seeds 32 --size 96{4} --ticks {5} --income 1 --mopup {6} --out {7}\{8}.md" -f $c.TA, $c.TB, $c.FA, $c.FB, $map, $c.Ticks, $mop, $outRel, $name)
        $all += $c
        $done = (Test-Path $c.Md) -and (Select-String -Path $c.Md -Pattern $dataRow -Quiet)
        if ($done) { Write-Host "skip (done): $name"; continue }
        $queue.Enqueue($c)
    }
}

if ($DryRun) {
    Write-Host ("Dry run: {0} matches selected, {1} to run, {2} at a time, commit {3}. Output folder {4}." -f $all.Count, $queue.Count, $Jobs, $head, $outRel)
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
Write-Host ("{0} matches to run, {1} at a time, commit {2}. Started {3:HH:mm}; expect about 100 minutes for all 18 matches." -f $total, $Jobs, $head, $t0)
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

function Read-Row($r) {
    if (-not (Test-Path $r.Md)) { return $null }
    $m = Select-String -Path $r.Md -Pattern $dataRow | Select-Object -First 1
    if ($null -eq $m) { return $null }
    $g = $m.Matches[0].Groups
    return [pscustomobject]@{ A = [int]$g[2].Value; B = [int]$g[3].Value; D = [int]$g[4].Value; End = [int]$g[5].Value }
}
function Pct($a, $b) { if (($a + $b) -gt 0) { return ("{0} %" -f [int][Math]::Round(100.0 * $a / ($a + $b))) } else { return 'n/a' } }

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Session 22 mop-up A/B (new mop-up on vs off), owner's laptop")
[void]$sb.AppendLine()
[void]$sb.AppendLine(("Commit {0}, {1} logical CPUs, {2} jobs, finished {3:yyyy-MM-dd HH:mm}, wall time {4:N0} min. " -f $head, [Environment]::ProcessorCount, $Jobs, (Get-Date), ((Get-Date) - $t0).TotalMinutes) +
    "``ai match`` tiered AI, 32 seeds x 2 start swaps = 64 games per match, size 96, 30 000 ticks, --income 1. A / B / draw = wins of side A, wins of side B, draws. Decisive % = A wins / (A + B wins).")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| Row (A v B) | Map | Mop-up | A wins | B wins | Draws | Avg end tick | Decisive % (A) |")
[void]$sb.AppendLine("|---|---|---|---:|---:|---:|---:|---:|")
foreach ($r in $all) {
    $label = "{0} {1} v {2} {3}" -f $r.FA, $r.TA, $r.FB, $r.TB
    $map = if ($r.Biome) { "$($r.Biome) L$($r.Layout)" } else { 'default' }
    $v = Read-Row $r
    if ($null -eq $v) { [void]$sb.AppendLine("| $label | $map | $($r.Mopup) | - | - | - | - | missing |"); continue }
    [void]$sb.AppendLine("| $label | $map | $($r.Mopup) | $($v.A) | $($v.B) | $($v.D) | $($v.End) | $(Pct $v.A $v.B) |")
}
[void]$sb.AppendLine()
[void]$sb.AppendLine("## Change with the mop-up on (on minus off)")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| Row (A v B) | Map | Draws on / off | Draws change | Avg end tick change | Decisive % (A) on / off |")
[void]$sb.AppendLine("|---|---|---|---:|---:|---|")
foreach ($pair in ($all | Group-Object Pair)) {
    $on = $pair.Group | Where-Object { $_.Mopup -eq 'on' } | Select-Object -First 1
    $off = $pair.Group | Where-Object { $_.Mopup -eq 'off' } | Select-Object -First 1
    if ($null -eq $on -or $null -eq $off) { continue }
    $a = Read-Row $on; $b = Read-Row $off
    $label = "{0} {1} v {2} {3}" -f $on.FA, $on.TA, $on.FB, $on.TB
    $map = if ($on.Biome) { "$($on.Biome) L$($on.Layout)" } else { 'default' }
    if ($null -eq $a -or $null -eq $b) { [void]$sb.AppendLine("| $label | $map | - | - | - | - |"); continue }
    [void]$sb.AppendLine(("| {0} | {1} | {2} / {3} | {4:+#;-#;0} | {5:+#;-#;0} | {6} / {7} |" -f $label, $map, $a.D, $b.D, ($a.D - $b.D), ($a.End - $b.End), (Pct $a.A $a.B), (Pct $b.A $b.B)))
}
[void]$sb.AppendLine()
[void]$sb.AppendLine("Reading: the mop-up works when the draws fall and the average end tick drops while the decisive % of the stronger side (A) does not fall (the session 14 version lost 12 points of it on Directorate 4 v 3). 64 games per match: a few points either way is noise.")
if ($failed.Count -gt 0) { [void]$sb.AppendLine(); [void]$sb.AppendLine("Failed matches (see the .err file): " + ($failed -join ', ')) }
$summary = Join-Path $out ($OutName + '_summary.md')
[System.IO.File]::WriteAllText($summary, $sb.ToString())
Write-Host $sb.ToString()
Write-Host "Summary written to $summary. Send that file back."
