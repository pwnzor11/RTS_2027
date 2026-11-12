<#
.SYNOPSIS
  RTS_2027 M14-03 follow-up (session 22): re-render the game sprite sheets of the four wall and gate pieces after the model detail pass
  (owner's laptop, D-196).

.DESCRIPTION
  Run from the ROOT of your RTS_2027 clone, on the branch wp/s22-wallart (git fetch; git checkout wp/s22-wallart):

      powershell -ExecutionPolicy Bypass -File tools\art\render_s22_walls.ps1 -Force            # all four
      powershell -ExecutionPolicy Bypass -File tools\art\render_s22_walls.ps1 -Ids auth_wall -Force   # the pilot only

  Uses .venv-art. Writes data\sprites\buildings\<id>.png and <id>.sprite.toml (both generated; all states of the sheet, the wall pieces
  have 36 frames plus the build-up). These sheets already exist in the repo, so WITHOUT -Force every id is skipped; use -Force to redo them.
  Rough time on this laptop: about 2-6 min per id. Then run the booklet heroes for the same ids (the script prints the command)
  and commit the new PNGs and .sprite.toml files (see the "Next" lines).
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

# id -> (script, output folder)
$jobs = [ordered]@{}
foreach ($b in 'auth_wall','auth_gate','dir_bunker','lb_scrap_wall') { $jobs[$b] = @('tools\art\build_structure.py', 'buildings') }

$want = if ($Ids) { (($Ids -join ',') -split ',') | Where-Object { $_ } } else { @($jobs.Keys) }
$failed = @(); $t0 = Get-Date
foreach ($id in $want) {
    if (-not $jobs.Contains($id)) { Write-Warning "unknown id $id"; continue }
    $script, $kind = $jobs[$id]
    $png = Join-Path $root "data\sprites\$kind\$id.png"
    if ((Test-Path $png) -and -not $Force) { Write-Host "skip   $id (exists; use -Force to re-render)"; continue }
    Write-Host ("start  {0}  {1:HH:mm:ss}" -f $id, (Get-Date))
    $s = Get-Date
    python $script $id --samples $Samples
    $ok = ($LASTEXITCODE -eq 0) -and (Test-Path $png)
    Write-Host ("{0} {1}  ({2:mm\:ss})" -f ($(if ($ok) {'done  '} else {'FAILED'})), $id, ((Get-Date) - $s))
    if (-not $ok) { $failed += $id }
}
python tools\art\house_audit.py
Write-Host ("Elapsed {0:hh\:mm\:ss}. Failed: {1}" -f ((Get-Date) - $t0), ($(if ($failed) { $failed -join ', ' } else { 'none' })))
Write-Host "Next: booklet heroes:  tools\booklet\render_heroes.ps1 -Ids $($want -join ',') -Force"
Write-Host "      (run it as: powershell -ExecutionPolicy Bypass -File tools\booklet\render_heroes.ps1 -Ids $($want -join ',') -Force)"
Write-Host "Next: git add data\sprites art\booklet ; git commit -m `"art(structures): s22 wall pieces re-rendered, sprite sheets and heroes [M14-03]`" ; git push"
