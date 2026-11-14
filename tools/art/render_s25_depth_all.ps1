<#
.SYNOPSIS
  RTS_2027 M10-08 (session 25, D-245): render the approved depth pass for every building sheet on the owner's laptop.

.DESCRIPTION
  Run from the ROOT of your RTS_2027 clone:

      powershell -ExecutionPolicy Bypass -File tools\art\render_s25_depth_all.ps1              # render all, nothing installed
      powershell -ExecutionPolicy Bypass -File tools\art\render_s25_depth_all.ps1 -Install     # render what is missing, then copy into data\
      powershell -ExecutionPolicy Bypass -File tools\art\render_s25_depth_all.ps1 -Ids auth_power,dir_shaft

  Renders with build_structure.py --depth into scratch\art_s25\depth\ (data\sprites is not touched until -Install). Ids whose depth
  sheet already exists there are skipped, so a stopped run can be started again; -Force redoes them.
  Rotating-head defences (auth_flak, auth_turret, dir_pillbox, lb_harpoon_post, lb_spike_nest) use their own builders without a depth
  option and keep their current sheets.
  -Install copies <id>.png and <id>.sprite.toml for every finished id into data\sprites\buildings\.
  Rough time: 2-6 min per building at 64 samples, about 3-6 h for all 69. Do not switch git branches while it runs.
#>
param(
    [string[]]$Ids,
    [int]$Samples = 64,
    [switch]$Force,
    [switch]$Install
)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root 'RTS_2027.slnx'))) { throw "Run this from the root of your RTS_2027 folder." }
$act = Join-Path $root '.venv-art\Scripts\Activate.ps1'
if (-not (Test-Path $act)) { throw ".venv-art not found in $root (see D-196)." }
. $act

$skip = @('auth_flak', 'auth_turret', 'dir_pillbox', 'lb_harpoon_post', 'lb_spike_nest')
$outDir = Join-Path $root 'scratch\art_s25\depth'
$dataDir = Join-Path $root 'data\sprites\buildings'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$all = Get-ChildItem $dataDir -Filter *.png | ForEach-Object { $_.BaseName } |
    Where-Object { (Test-Path (Join-Path $root "tools\art\structures\$_.py")) -and ($skip -notcontains $_) }
$want = if ($Ids) { (($Ids -join ',') -split ',') | Where-Object { $_ } } else { @($all) }

$failed = @(); $done = 0; $t0 = Get-Date; $times = @()
$todo = @($want | Where-Object { $Force -or -not (Test-Path (Join-Path $outDir "$_.png")) })
Write-Host ("{0} building(s) wanted, {1} to render, {2} samples" -f $want.Count, $todo.Count, $Samples)
foreach ($id in $todo) {
    if ($skip -contains $id) { Write-Warning "$id has a rotating head (own builder): skipped"; continue }
    Write-Host ("start  {0}  {1:HH:mm:ss}" -f $id, (Get-Date))
    $s = Get-Date
    python tools\art\build_structure.py $id --depth --samples $Samples --out-dir $outDir
    $ok = ($LASTEXITCODE -eq 0) -and (Test-Path (Join-Path $outDir "$id.png")) -and (Test-Path (Join-Path $outDir "$id.sprite.toml"))
    $el = (Get-Date) - $s; $times += $el.TotalSeconds; $done++
    $left = $todo.Count - $done
    $eta = [TimeSpan]::FromSeconds((($times | Measure-Object -Average).Average) * $left)
    Write-Host ("{0} {1}  ({2:mm\:ss}; {3} left, about {4:hh\:mm} to go)" -f ($(if ($ok) {'done  '} else {'FAILED'})), $id, $el, $left, $eta)
    if (-not $ok) { $failed += $id }
}

$ready = @($want | Where-Object { (Test-Path (Join-Path $outDir "$_.png")) -and (Test-Path (Join-Path $outDir "$_.sprite.toml")) })
if ($ready.Count -gt 0) {
    python tools\art\depth_compare.py @($ready | Select-Object -First 12) --depth-dir $outDir --out (Join-Path $root 'scratch\art_s25\depth_compare.png')
    python tools\art\house_audit.py $outDir
}
if ($Install) {
    foreach ($id in $ready) {
        Copy-Item (Join-Path $outDir "$id.png") $dataDir -Force
        Copy-Item (Join-Path $outDir "$id.sprite.toml") $dataDir -Force
    }
    Write-Host "Installed $($ready.Count) depth sheet(s) into $dataDir"
}
Write-Host ("Elapsed {0:hh\:mm\:ss}. Failed: {1}" -f ((Get-Date) - $t0), ($(if ($failed) { $failed -join ', ' } else { 'none' })))
if ($failed) { exit 1 }
