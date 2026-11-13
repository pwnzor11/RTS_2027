<#
.SYNOPSIS
  RTS_2027 M10-08 (session 24, D-243): render the depth-pass pilots (auth_conyard, dir_shaft) on the owner's laptop and build the
  committed-vs-depth comparison sheet.

.DESCRIPTION
  Run from the ROOT of your RTS_2027 clone, on the branch wp/s24-depth (git fetch; git checkout wp/s24-depth):

      powershell -ExecutionPolicy Bypass -File tools\art\render_s24_depth.ps1                 # both pilots, 64 samples
      powershell -ExecutionPolicy Bypass -File tools\art\render_s24_depth.ps1 -Ids dir_shaft -Samples 96

  Uses .venv-art.  Renders with --depth into scratch\art_s24\depth\ (a SEPARATE folder: data\sprites is never touched during a pilot),
  then runs tools\art\depth_compare.py and prints the sheet path (scratch\art_s24\depth_compare.png).  Use -Frames to compare other
  frames (the default compares frame 0 of each; the full sheet is always rendered unless you pass -Frames to render only those).
  Rough time on this laptop: a few minutes per id at 64 samples.
#>
param(
    [string[]]$Ids,
    [int]$Samples = 64,
    [int[]]$Frames
)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root 'RTS_2027.slnx'))) { throw "Run this from the root of your RTS_2027 folder." }
$act = Join-Path $root '.venv-art\Scripts\Activate.ps1'
if (-not (Test-Path $act)) { throw ".venv-art not found in $root (see D-196)." }
. $act

$outDir = Join-Path $root 'scratch\art_s24\depth'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$want = if ($Ids) { (($Ids -join ',') -split ',') | Where-Object { $_ } } else { @('auth_conyard', 'dir_shaft') }
$failed = @(); $t0 = Get-Date
foreach ($id in $want) {
    Write-Host ("start  {0}  {1:HH:mm:ss}" -f $id, (Get-Date))
    $s = Get-Date
    $args_ = @('tools\art\build_structure.py', $id, '--depth', '--samples', $Samples, '--out-dir', $outDir)
    if ($Frames) { $args_ += '--frames'; $args_ += ($Frames | ForEach-Object { "$_" }) }
    python @args_
    $ok = ($LASTEXITCODE -eq 0) -and (Test-Path (Join-Path $outDir "$id.png"))
    Write-Host ("{0} {1}  ({2:mm\:ss})" -f ($(if ($ok) {'done  '} else {'FAILED'})), $id, ((Get-Date) - $s))
    if (-not $ok) { $failed += $id }
}
$cmp = @('tools\art\depth_compare.py') + @($want | Where-Object { $failed -notcontains $_ }) + @('--depth-dir', $outDir)
if ($Frames) { $cmp += '--frames'; $cmp += ($Frames | ForEach-Object { "$_" }) }
python @cmp
python tools\art\house_audit.py
Write-Host ("Elapsed {0:hh\:mm\:ss}. Failed: {1}" -f ((Get-Date) - $t0), ($(if ($failed) { $failed -join ', ' } else { 'none' })))
Write-Host "Compare sheet: $(Join-Path $root 'scratch\art_s24\depth_compare.png')"
