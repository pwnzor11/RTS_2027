<#
.SYNOPSIS
  RTS_2027 session 24 art pilots on the owner's laptop (D-243): building depth (auth_conyard, dir_shaft), Authority UI skin, unit portraits and
  infantry death frames. Runs the three pilot scripts one after another and lists the sheets to review. Nothing in data\ changes (no -Install).

.DESCRIPTION
  From the ROOT of your RTS_2027 clone, on branch integ-s24 (git fetch; git checkout integ-s24; git pull):

      powershell -ExecutionPolicy Bypass -File tools\art\render_s24_pilots.ps1

  Uses .venv-art. Rough time: 30-60 min. Then open the three sheets it prints and tell Claude what you think.
  After approval each piece is installed with its own script and -Install (skin, portraits) or a full re-render (depth).
#>
param([switch]$SkipDepth, [switch]$SkipSkin, [switch]$SkipPortraits)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root 'RTS_2027.slnx'))) { throw "Run this from the root of your RTS_2027 folder." }
$t0 = Get-Date; $failed = @()
$steps = @()
if (-not $SkipDepth)     { $steps += ,@('depth',     'tools\art\render_s24_depth.ps1') }
if (-not $SkipSkin)      { $steps += ,@('skin',      'tools\art\render_s24_skin.ps1') }
if (-not $SkipPortraits) { $steps += ,@('portraits', 'tools\art\render_s24_portraits.ps1', '-Force') }
foreach ($s in $steps) {
    Write-Host ("===== {0}  {1:HH:mm:ss}" -f $s[0], (Get-Date))
    try { & powershell -ExecutionPolicy Bypass -File $s[1] @($s | Select-Object -Skip 2); if ($LASTEXITCODE -ne 0) { $failed += $s[0] } }
    catch { Write-Warning $_; $failed += $s[0] }
}
Write-Host ""
Write-Host "Review these:"
Write-Host "  Depth (left today, right new): $(Join-Path $root 'scratch\art_s24\depth_compare.png')"
Write-Host "  Authority skin mock-up:        $(Join-Path $root 'scratch\art_s24\skin_preview.png')"
Write-Host "  Portraits and death frames:    $(Join-Path $root 'scratch\art_s24\contact_s24.png')"
Write-Host ("Elapsed {0:hh\:mm\:ss}. Failed: {1}" -f ((Get-Date) - $t0), ($(if ($failed) { $failed -join ', ' } else { 'none' })))
