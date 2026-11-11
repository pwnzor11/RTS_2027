<#
.SYNOPSIS
  RTS_2027 booklet hero renders for the owner's laptop (D-196): one full-resolution, transparent-background render per unit / building.

.DESCRIPTION
  Run it from the ROOT of your RTS_2027 clone (git pull first):

      powershell -ExecutionPolicy Bypass -File tools\booklet\render_heroes.ps1 -Pilot
      powershell -ExecutionPolicy Bypass -File tools\booklet\render_heroes.ps1 -Ids auth_tank,lb_mother_rig
      powershell -ExecutionPolicy Bypass -File tools\booklet\render_heroes.ps1 -All

  Uses .venv-art (Python 3.13, bpy, numpy, pillow). Output: art\booklet\<faction>\<id>.png (about 1600 px on the long side,
  transparent background, same 40 degree orthographic camera and upper-left sun as the game sprites, south-west three-quarter view,
  house paint in the faction's booklet accent) and a contact sheet art\booklet\contact_<date>.png. Send the contact sheet back.
  Ids whose model does not exist yet (status no_model in tools\booklet\render_manifest.toml, 26 of 150) are skipped and listed.
  PNGs that already exist are skipped, so a stopped run can simply be started again; add -Force to redo them.

  Estimated time (honest range). A game sprite sheet is about 18 s per vehicle on this PC, but that is 16 small 96 px frames with
  24 samples. One hero is a single 1600 px render at 128 samples, i.e. roughly 15 to 30 times the pixels-times-samples of one
  sprite frame, plus model build and denoise. Measured on the 2-core cloud box with 32 samples: auth_tank took 133 s, so 128
  samples there would be about 8 min. This script lets Blender use every core (the sprite scripts pin 2), so expect about
  1 to 4 minutes per id on a laptop:
      -Pilot (12 renders)   about 15 to 45 min
      -All   (124 renders)  about 2 to 8 hours (run it overnight; it can be stopped and resumed)
  For a quick look use -Samples 48 (about a third of the time, slightly noisier).

.PARAMETER Ids      Comma separated ids to render.
.PARAMETER Pilot    One vehicle, one infantry and one building per faction (12 renders).
.PARAMETER All      Every id with a model.
.PARAMETER Samples  Cycles samples (default 128).
.PARAMETER LongSide Long side in pixels (default 1600).
.PARAMETER Force    Re-render ids whose PNG already exists.
#>
param(
    [string[]]$Ids,
    [switch]$Pilot,
    [switch]$All,
    [int]$Samples = 128,
    [int]$LongSide = 1600,
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root 'RTS_2027.slnx'))) {
    throw "Run this from the root of your RTS_2027 folder (RTS_2027.slnx not found in $root)."
}
if (-not ($Pilot -or $All -or $Ids)) { throw "Give -Pilot, -All or -Ids a,b,c (see the header of this script)." }
$act = Join-Path $root '.venv-art\Scripts\Activate.ps1'
if (-not (Test-Path $act)) { throw ".venv-art not found in $root (see D-196 for how it is set up)." }
. $act

$argsList = @('tools\booklet\render_heroes.py', '--samples', $Samples, '--long-side', $LongSide)
if ($Pilot) { $argsList += '--pilot' }
elseif ($All) { $argsList += '--all' }
else { $argsList += '--ids'; $argsList += (($Ids -join ',') -split ',' | Where-Object { $_ }) -join ',' }
if ($Force) { $argsList += '--force' }

$t0 = Get-Date
Write-Host "Booklet hero renders: python $($argsList -join ' ')"
python @argsList
$code = $LASTEXITCODE
$el = (Get-Date) - $t0
Write-Host ("Elapsed: {0:hh\:mm\:ss} (contact sheet: art\booklet\contact_{1}.png)" -f $el, (Get-Date -Format 'yyyy-MM-dd'))
if ($code -ne 0) { Write-Warning "Some ids failed (see FAILED above). Re-run to retry only those."; exit $code }
