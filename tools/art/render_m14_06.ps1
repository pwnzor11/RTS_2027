<#
.SYNOPSIS
  RTS_2027 M14-06: render the game sprite sheets for the 26 units and buildings that had no art (owner's laptop, D-196).

.DESCRIPTION
  Run from the ROOT of your RTS_2027 clone, on the branch art/m14-06-pilot (git fetch; git checkout art/m14-06-pilot):

      powershell -ExecutionPolicy Bypass -File tools\art\render_m14_06.ps1            # all 26
      powershell -ExecutionPolicy Bypass -File tools\art\render_m14_06.ps1 -Ids auth_bomber,auth_airfield

  Uses .venv-art. Writes data\sprites\<kind>\<id>.png and its .sprite.toml (both generated; air units also get <id>_shadow,
  turreted vehicles <id>_turret). Ids whose PNG already exists are skipped, so a stopped run can be started again; -Force redoes them.
  Rough time on this laptop: vehicles about 20-60 s each, infantry about 1-2 min, buildings about 2-6 min (all their states).
  Then run the booklet heroes for the same ids:
      powershell -ExecutionPolicy Bypass -File tools\booklet\render_heroes.ps1 -Ids <the 26 ids printed at the end>
  and commit the new PNGs and .sprite.toml files (see the "Next" lines this script prints).
#>
param(
    [string[]]$Ids,
    [int]$Samples = 24,
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root 'RTS_2027.slnx'))) { throw "Run this from the root of your RTS_2027 folder." }
$act = Join-Path $root '.venv-art\Scripts\Activate.ps1'
if (-not (Test-Path $act)) { throw ".venv-art not found in $root (see D-196)." }
. $act

# id -> (script, output folder). auth_flak has its own wrapper (turret-style emplacement).
$jobs = [ordered]@{}
foreach ($v in 'auth_bomber','auth_carrier','auth_seismic_mortar','auth_foam_truck','auth_interceptor','auth_howitzer','auth_bulwark',
                'dir_ash_sower','dir_delver','dir_great_dirigible') { $jobs[$v] = @('tools\art\build_vehicle.py', 'vehicles') }
foreach ($i in 'auth_engineer','auth_marksman','dir_assessor','dir_volunteer') { $jobs[$i] = @('tools\art\build_infantry.py', 'infantry') }
foreach ($b in 'auth_airfield','auth_ration_hall','auth_signals_hq','auth_blockhouse','auth_atomic_battery',
                'dir_bunk_gallery','dir_redoubt','dir_committee_hall','neu_ruin_tenement','neu_ruin_depot','lb_still_rig') { $jobs[$b] = @('tools\art\build_structure.py', 'buildings') }
$jobs['auth_flak'] = @('tools\art\structures\auth_flak_build.py', 'buildings')

$want = if ($Ids) { (($Ids -join ',') -split ',') | Where-Object { $_ } } else { @($jobs.Keys) }
$failed = @(); $t0 = Get-Date
foreach ($id in $want) {
    if (-not $jobs.Contains($id)) { Write-Warning "unknown id $id"; continue }
    $script, $kind = $jobs[$id]
    $png = Join-Path $root "data\sprites\$kind\$id.png"
    if ((Test-Path $png) -and -not $Force) { Write-Host "skip   $id (exists)"; continue }
    Write-Host ("start  {0}  {1:HH:mm:ss}" -f $id, (Get-Date))
    $s = Get-Date
    if ($id -eq 'auth_flak') { python $script --samples $Samples } else { python $script $id --samples $Samples }
    $ok = ($LASTEXITCODE -eq 0) -and (Test-Path $png)
    Write-Host ("{0} {1}  ({2:mm\:ss})" -f ($(if ($ok) {'done  '} else {'FAILED'})), $id, ((Get-Date) - $s))
    if (-not $ok) { $failed += $id }
}
python tools\art\house_audit.py
Write-Host ("Elapsed {0:hh\:mm\:ss}. Failed: {1}" -f ((Get-Date) - $t0), ($(if ($failed) { $failed -join ', ' } else { 'none' })))
Write-Host "Next: booklet heroes:  powershell -ExecutionPolicy Bypass -File tools\booklet\render_heroes.ps1 -Ids $($want -join ',')"
Write-Host "Next: git add data\sprites art\booklet ; git commit -m `"art(m14-06): sprite sheets and heroes for the 26 ids [M14-06]`" ; git push"
Write-Host "      (then tell Claude: it removes the ids from KnownWithoutArt and the PLACEHOLDER ART comments in the same PR)"
