<#
.SYNOPSIS
  RTS_2027 session 21 (M13-17): the campaign 12-seed sweep on the owner's laptop. Every mission-tier the campaign bot walks is played on
  12 mission seeds with the ashfall hazard on, and the wins are counted against the Q-75 targets (tier 1 >= 10/12, tier 3 >= 8/12,
  tier 5 >= 3/12).

.DESCRIPTION
  Run it from the ROOT of your RTS_2027 clone, on the branch the lead names (session 21: integ-s21):

      git fetch
      git checkout integ-s21
      git pull
      powershell -ExecutionPolicy Bypass -File tools\measure\run_s21_campaign_sweep.ps1

  EXPECTED RUN TIME: about 30-60 minutes with the default jobs (40 mission-tiers x 12 seeds; most walks take seconds, the act-4 missions
  about a minute each). Use -Cases to run part of it, e.g. -Cases exp_03:5,auth_03:3

  Results go to balance_runs\s21sweep\ : one .log per job and s21sweep_summary.md (send that one back, or leave it there for the lead
  to read through the linked folder). Deterministic: the same commit gives the same table on any machine.

.PARAMETER Jobs
  Sweep processes to run at once. Default: half the logical CPUs.
.PARAMETER Cases
  Mission-tiers to run as id:tier, default all 40.
.PARAMETER SkipBuild
  Reuse the last build.
#>
param(
    [int]$Jobs = [Math]::Max(1, [int][Math]::Floor([Environment]::ProcessorCount / 2)),
    [string[]]$Cases,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root 'RTS_2027.slnx'))) { throw "Run this from the root of your RTS_2027 folder." }

# The tiers each mission's walk supports (tests/Scripting.Tests/M8_15/CampaignWalks.cs, Tiers()).
$missions = 'auth_01','auth_02','auth_03','auth_04','dir_01','dir_02','dir_03','dir_04','lb_01','lb_02','lb_03','lb_04','exp_01','exp_02','exp_03','exp_04'
$all = foreach ($m in $missions) {
    $tiers = if ($m -in 'auth_04','dir_01','exp_01') { 3, 5 } else { 1, 3, 5 }
    foreach ($t in $tiers) { "${m}:$t" }
}
$want = if ($Cases) { (($Cases -join ',') -split ',') | Where-Object { $_ } } else { @($all) }

$out = Join-Path $root 'balance_runs\s21sweep'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$head = (git rev-parse --short HEAD).Trim()
$branch = (git rev-parse --abbrev-ref HEAD).Trim()

if (-not $SkipBuild) {
    Write-Host "Building the Scripting tests (Release)..."
    dotnet build tests\Scripting.Tests -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
}

# Deal the cases round-robin over the jobs so the slow act-4 missions spread out.
$lanes = @{}
for ($k = 0; $k -lt $want.Count; $k++) { $j = $k % $Jobs; if (-not $lanes.ContainsKey($j)) { $lanes[$j] = @() }; $lanes[$j] += $want[$k] }

Get-ChildItem $out -Filter 'job*.lines' -ErrorAction SilentlyContinue | Remove-Item
$t0 = Get-Date
Write-Host ("{0} mission-tiers, {1} jobs, {2} @ {3}. Started {4:HH:mm}." -f $want.Count, $Jobs, $branch, $head, $t0)
$procs = @()
foreach ($j in $lanes.Keys) {
    $log = Join-Path $out ("job{0}.log" -f $j)
    $env:RTS_SWEEP = ($lanes[$j] -join ',')
    $env:RTS_SWEEP_OUT = Join-Path $out ("job{0}.lines" -f $j)   # one file per job: no two processes append to the same file
    $procs += Start-Process -FilePath 'dotnet' -NoNewWindow -PassThru -RedirectStandardOutput $log -RedirectStandardError ($log + '.err') `
        -ArgumentList @('test', 'tests\Scripting.Tests', '-c', 'Release', '--no-build', '--filter', 'FullyQualifiedName~SeedSweep',
                        '--logger', '"console;verbosity=detailed"')
}
Remove-Item Env:RTS_SWEEP, Env:RTS_SWEEP_OUT
while (($procs | Where-Object { -not $_.HasExited }).Count -gt 0) {
    $done = @(Get-ChildItem $out -Filter 'job*.lines' -ErrorAction SilentlyContinue | Get-Content).Count
    Write-Host ("{0:HH:mm}  {1} of {2} mission-tiers done" -f (Get-Date), $done, $want.Count)
    Start-Sleep -Seconds 60
}

# Summary table against the Q-75 targets.
$target = @{ 1 = 10; 2 = 10; 3 = 8; 4 = 3; 5 = 3 }
$lines = @(Get-ChildItem $out -Filter 'job*.lines' -ErrorAction SilentlyContinue | Get-Content)
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Session 21 campaign sweep (12 seeds, ash on)")
[void]$sb.AppendLine("")
[void]$sb.AppendLine(("Branch {0} @ {1}, {2} logical CPUs, {3} jobs, finished {4:yyyy-MM-dd HH:mm}, wall time {5:N0} min. Targets (Q-75): tier 1 >= 10/12, tier 3 >= 8/12, tier 5 >= 3/12." -f $branch, $head, [Environment]::ProcessorCount, $Jobs, (Get-Date), ((Get-Date) - $t0).TotalMinutes))
[void]$sb.AppendLine("")
[void]$sb.AppendLine("| Mission | Tier | Wins /12 | Target | Win ticks (L = loss) |")
[void]$sb.AppendLine("|---|---|---|---|---|")
$below = @()
foreach ($c in $want) {
    $id, $t = $c -split ':'
    $l = $lines | Where-Object { $_ -match "^SWEEP $id t$t wins" } | Select-Object -First 1
    if (-not $l) { [void]$sb.AppendLine("| $id | $t | missing | | see job logs |"); $below += "$id t$t (missing)"; continue }
    $null = $l -match 'wins (\d+)/(\d+) \[(.*)\]'
    $w = [int]$Matches[1]; $ok = $w -ge $target[[int]$t]
    if (-not $ok) { $below += "$id t$t ($w/12)" }
    [void]$sb.AppendLine(("| {0} | {1} | {2} | {3} | {4} |" -f $id, $t, $w, $(if ($ok) { 'ok' } else { '**below**' }), $Matches[3]))
}
[void]$sb.AppendLine("")
[void]$sb.AppendLine(("Below target or missing: {0}" -f $(if ($below.Count) { $below -join ', ' } else { 'none' })))
$sum = Join-Path $out 's21sweep_summary.md'
Set-Content -Path $sum -Value $sb.ToString() -Encoding ascii
Write-Host ""
Write-Host ("Done in {0:N0} min. Summary: {1}" -f ((Get-Date) - $t0).TotalMinutes, $sum)
Write-Host ("Below target or missing: {0}" -f $(if ($below.Count) { $below -join ', ' } else { 'none' }))
