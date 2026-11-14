<#
.SYNOPSIS
  RTS_2027 session 24 SFX catch-up on the owner's laptop (M10-10). Runs in this order, each step skipping takes that already exist:
    1. the 13 short missing sounds (menu, alerts, rotor hover)            ~1.5-3 h
    2. new takes 4-6 for the 15 sounds whose takes 1-3 droned (s24 prompts) ~5-7 h
    3. one full-window take (take 7, -LatentFull) of wpn_sam and die_veh_auth, to see whether the slow full window fixes the drone  ~2-3 h
    4. the five ambience loops (2 takes each)                              ~5-10 h
.DESCRIPTION
  From the ROOT of your RTS_2027 clone on integ-s24:  powershell -ExecutionPolicy Bypass -File tools\audio\sfx\run_s24_sfx.ps1
  Stop with Ctrl+C at any time; rerun to continue. -From 2 starts at step 2. Then tell Claude "sfx done".
#>
param([int]$From = 1, [int]$To = 4)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root 'RTS_2027.slnx'))) { throw "Run this from the root of your RTS_2027 folder." }
$sfx = 'tools\audio\sfx\make_sfx.ps1'
$drones = 'die_air_crash,die_inf_lb,die_veh_auth,die_veh_dir,die_veh_exp,exp_bomb_heavy,exp_rad_burst,wpn_cannon_dir,wpn_depth_charge,wpn_flame,wpn_naval_cruiser,wpn_pod_exp,wpn_rocket_auth,wpn_sam,wpn_spray'
$steps = @(
    @{ n = 1; name = 'missing short sounds'; args = @('-Generate', '-Only', 'gui_,evt_,air_rotor') },
    @{ n = 2; name = 'drone re-takes 4-6';    args = @('-Generate', '-Only', $drones, '-Takes', '6') },
    @{ n = 3; name = 'full-window test';      args = @('-Generate', '-Only', 'wpn_sam,die_veh_auth', '-Takes', '7', '-LatentFull') },
    @{ n = 4; name = 'ambience loops';        args = @('-Generate', '-Only', 'amb_') }
)
$t0 = Get-Date
foreach ($s in $steps) {
    if ($s.n -lt $From -or $s.n -gt $To) { continue }
    Write-Host ("===== step {0}: {1}  {2:HH:mm:ss}" -f $s.n, $s.name, (Get-Date))
    & powershell -ExecutionPolicy Bypass -File $sfx @($s.args)
    if ($LASTEXITCODE -ne 0) { Write-Warning "step $($s.n) exited with $LASTEXITCODE" }
}
Write-Host ("Elapsed {0:hh\:mm\:ss}. Next: tell Claude 'sfx done' (or run make_sfx.ps1 -Audition to listen)." -f ((Get-Date) - $t0))
