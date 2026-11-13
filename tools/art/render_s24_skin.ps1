<#
.SYNOPSIS
  RTS_2027 M10-15 (session 24, D-243): render the Authority UI skin (Blender) on the owner's laptop.

.DESCRIPTION
  Run from the ROOT of your RTS_2027 clone (branch wp/s24-skinart):

      powershell -ExecutionPolicy Bypass -File tools\art\render_s24_skin.ps1              # render + preview, nothing installed
      powershell -ExecutionPolicy Bypass -File tools\art\render_s24_skin.ps1 -Install     # also copy into data\ui\skins\authority\

  Uses .venv-art. Renders to scratch\art_s24\skin\ (PNGs + skin.toml), then builds scratch\art_s24\skin_preview.png from them.
  Open the preview first; run again with -Install once it looks right, then commit data\ui\skins\authority.
#>
param(
    [int]$Samples = 128,
    [switch]$Install
)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root 'RTS_2027.slnx'))) { throw "Run this from the root of your RTS_2027 folder." }
$act = Join-Path $root '.venv-art\Scripts\Activate.ps1'
if (-not (Test-Path $act)) { throw ".venv-art not found in $root (see D-196)." }
. $act

$out = Join-Path $root 'scratch\art_s24\skin'
$preview = Join-Path $root 'scratch\art_s24\skin_preview.png'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$t0 = Get-Date
python tools\art\ui\auth_skin.py --out $out --samples $Samples
if ($LASTEXITCODE -ne 0) { throw "auth_skin.py failed" }
python -I tools\art\ui\skin_preview.py --skin $out --out $preview
if ($LASTEXITCODE -ne 0) { throw "skin_preview.py failed" }

if ($Install) {
    $dest = Join-Path $root 'data\ui\skins\authority'
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item (Join-Path $out '*.png') $dest -Force
    Copy-Item (Join-Path $out 'skin.toml') $dest -Force
    Write-Host "Installed into $dest"
    Write-Host "Next: git add data\ui\skins\authority ; git commit -m `"art(ui): Authority skin rendered [M10-15]`" ; git push"
}
Write-Host "Skin PNGs : $out"
Write-Host "Preview   : $preview"
Write-Host ("Elapsed {0:mm\:ss}. Samples {1}." -f ((Get-Date) - $t0), $Samples)
