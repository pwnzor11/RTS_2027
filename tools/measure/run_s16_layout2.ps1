<#
.SYNOPSIS
  RTS_2027 session 16 M12-12 AI-vs-AI series on map layout 2 for the owner's laptop (key tier 3 cross-faction cells plus Directorate 5 v 3).

.DESCRIPTION
  Run it from the ROOT of your RTS_2027 clone:

      git pull
      powershell -ExecutionPolicy Bypass -File tools\measure\run_s16_layout2.ps1

  32 seeds x 2 start swaps = 64 games per row, map size 96 (medium), 30 000 ticks, --layout 2, once per biome in -Biomes
  (default rust_belt and ash_plains). Same arguments as the 16-seed cells in docs/balance/s16_layout2.md.
  Results go to balance_runs\s16l2\ : one .md and .log per row and s16l2_summary.md (send that one back).
  Deterministic; if it stops, start it again: finished rows are skipped.

.PARAMETER Jobs
  Matches to run at once. Default: half the logical CPUs.
.PARAMETER SkipBuild
  Reuse the tool already built in balance_runs\s16l2\bin.
.PARAMETER Biomes
  Biomes to run, default rust_belt and ash_plains.
#>
param(
    [int]$Jobs = [Math]::Max(1, [int][Math]::Floor([Environment]::ProcessorCount / 2)),
    [switch]$SkipBuild,
    [string[]]$Biomes = @('rust_belt', 'ash_plains')
)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root 'RTS_2027.slnx'))) {
    throw "Run this from the root of your RTS_2027 folder (RTS_2027.slnx not found in $root)."
}
$head = (git rev-parse --short HEAD).Trim()
$dirty = git status --porcelain -- src data
if ($dirty) { Write-Warning "src\ or data\ has local changes: the numbers will not match a clean main." }

$outRel = 'balance_runs\s16l2'
$out = Join-Path $root $outRel
$bin = Join-Path $out 'bin'
New-Item -ItemType Directory -Force -Path $out | Out-Null

if (-not $SkipBuild) {
    Write-Host "Building the Tools CLI (Release)..."
    dotnet build src\Tools\Tools.csproj -c Release -o $bin --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
}
$exe = Join-Path $bin 'Tools.exe'
if (-not (Test-Path $exe)) { throw "Tools.exe not found in $bin (run without -SkipBuild)." }

function Row($fa, $ta, $fb, $tb, $base) {
    [pscustomobject]@{ FA = $fa; TA = $ta; FB = $fb; TB = $tb; Ticks = 30000; Base = $base }
}
# Base = layout-1 result on the default synthetic map at 16 seeds (32 games), A / B / draws (docs/balance/s16_q67.md, s15_lbknob.md).
$rows = @(
    (Row 'left_behind' 3 'directorate' 3 '17 / 12 / 3'),
    (Row 'directorate' 3 'authority'   3 '17 / 9 / 6'),
    (Row 'directorate' 3 'expedition'  3 '12 / 17 / 3'),
    (Row 'left_behind' 3 'authority'   3 '16 / 14 / 2'),
    (Row 'left_behind' 3 'expedition'  3 '15 / 12 / 5'),
    (Row 'authority'   3 'expedition'  3 ''),
    (Row 'directorate' 5 'directorate' 3 '21 / 8 / 3')
)

$dataRow = '^\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|'
$queue = New-Object System.Collections.Queue
$all = @()
foreach ($biome in $Biomes) { foreach ($r0 in $rows) {
    $r = $r0.PSObject.Copy(); $r | Add-Member Biome $biome
    $name = "{0}_{1}{2}_v_{3}{4}_{5}k" -f $biome, $r.FA, $r.TA, $r.FB, $r.TB, ($r.Ticks / 1000)
    $all += $r
    $r | Add-Member Name $name
    $r | Add-Member Md (Join-Path $out "$name.md")
    $done = (Test-Path $r.Md) -and (Select-String -Path $r.Md -Pattern $dataRow -Quiet)
    if ($done) { Write-Host "skip (done): $name"; continue }
    $r | Add-Member ArgList ("ai match --a tiered:{0} --b tiered:{1} --faction-a {2} --faction-b {3} --seeds 32 --size 96 --layout 2 --biome {4} --ticks {5} --out {6}\{7}.md" -f $r.TA, $r.TB, $r.FA, $r.FB, $r.Biome, $r.Ticks, $outRel, $name)
    $queue.Enqueue($r)
} }

$t0 = Get-Date
$total = $queue.Count
Write-Host ("{0} rows to run, {1} at a time, commit {2}. Started {3:HH:mm}." -f $total, $Jobs, $head, $t0)
$running = @(); $finished = 0; $failed = @()
while ($queue.Count -gt 0 -or $running.Count -gt 0) {
    while ($running.Count -lt $Jobs -and $queue.Count -gt 0) {
        $r = $queue.Dequeue()
        $p = Start-Process -FilePath $exe -ArgumentList $r.ArgList -WorkingDirectory $root -NoNewWindow -PassThru `
            -RedirectStandardOutput (Join-Path $out "$($r.Name).log") -RedirectStandardError (Join-Path $out "$($r.Name).err")
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

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Session 16 layout-2 AI-vs-AI series (owner's laptop)")
[void]$sb.AppendLine()
[void]$sb.AppendLine(("Commit {0}, {1} logical CPUs, {2} jobs, finished {3:yyyy-MM-dd HH:mm}, wall time {4:N0} min. " -f $head, [Environment]::ProcessorCount, $Jobs, (Get-Date), ((Get-Date) - $t0).TotalMinutes) +
    "``ai match`` tiered AI, 32 seeds x 2 start swaps = 64 games per row, size 96, --layout 2. Decisive % = A / (A + B). Base column = layout 1 on the default synthetic map, 32 games (different sample size and map).")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| Biome | Match (A v B) | A wins | B wins | Draws | Decisive % for A | Avg end tick | Before, 32 games (A / B / draws) |")
[void]$sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---|")
foreach ($r in $all) {
    $m = $null
    if (Test-Path $r.Md) { $m = Select-String -Path $r.Md -Pattern $dataRow | Select-Object -First 1 }
    $label = "{0} {1} v {2} {3}" -f $r.FA, $r.TA, $r.FB, $r.TB
    if ($null -eq $m) { [void]$sb.AppendLine("| $($r.Biome) | $label | - | - | - | missing | - | $($r.Base) |"); continue }
    $g = $m.Matches[0].Groups
    $a = [int]$g[2].Value; $b = [int]$g[3].Value; $d = [int]$g[4].Value; $end = [int]$g[5].Value
    if ($a + $b -gt 0) { $pct = "{0} %" -f [int][Math]::Round(100.0 * $a / ($a + $b)) } else { $pct = 'n/a' }
    [void]$sb.AppendLine("| $($r.Biome) | $label | $a | $b | $d | $pct | $end | $($r.Base) |")
}
if ($failed.Count -gt 0) { [void]$sb.AppendLine(); [void]$sb.AppendLine("Failed rows (see the .err file): " + ($failed -join ', ')) }
$summary = Join-Path $out 's16l2_summary.md'
[System.IO.File]::WriteAllText($summary, $sb.ToString())
Write-Host $sb.ToString()
Write-Host "Summary written to $summary. Send that file back."
