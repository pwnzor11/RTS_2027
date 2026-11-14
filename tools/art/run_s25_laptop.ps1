<#
.SYNOPSIS
  RTS_2027 session 25: one laptop run that renders and installs the approved art (D-245), applies the accepted sound picks, commits
  everything on a new branch and pushes it (LFS included). Claude then opens the pull request and merges it when CI is green.

.DESCRIPTION
  Stop the SFX run first (Ctrl+C in its window; rerun tools\audio\sfx\run_s24_sfx.ps1 -From 2 later, it resumes).
  Then, from the ROOT of your RTS_2027 clone:

      git fetch
      git checkout land/s25-laptop
      powershell -ExecutionPolicy Bypass -File tools\art\run_s25_laptop.ps1

  Steps (each one resumes: rerun the same command after a stop or a failure and finished work is skipped):
    1. new branch art/s25 from where you are (no other branch switch happens during the run)
    2. sound picks (tools\audio\sfx\picks_s25.toml) -> data\audio\sfx + sounds.toml       ~1 min    commit
    3. Authority UI skin render + install -> data\ui\skins\authority                     ~5-15 min commit
    4. portraits for every unit + infantry death frames, render + install               ~1-3 h    commit
    5. building depth pass for 69 buildings, render + install                            ~3-6 h    commit
    6. git push -u origin art/s25
  -Skip sfx,skin,portraits,depth leaves steps out; -NoPush stops before step 6. Do not switch branches while it runs.
  When it prints "ALL DONE", tell Claude "art done".
#>
param([string[]]$Skip = @(), [switch]$NoPush)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root 'RTS_2027.slnx'))) { throw "Run this from the root of your RTS_2027 folder." }
$Skip = (($Skip -join ',') -split ',') | Where-Object { $_ }
$log = Join-Path $root 'scratch\art_s25\run_s25_laptop.log'
New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null
function Log($m) { $line = "{0:yyyy-MM-dd HH:mm:ss}  {1}" -f (Get-Date), $m; Write-Host $line; Add-Content -Path $log -Value $line }

# The SFX generator reads the working tree and eats the CPU: refuse to start while it runs.
$gen = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like '*make_sfx.py*' }
if ($gen) { throw "The SFX generator is still running (make_sfx.py, pid $($gen.ProcessId -join ',')). Stop it with Ctrl+C in its window first." }
git lfs version | Out-Null
if ($LASTEXITCODE -ne 0) { throw "git-lfs is not installed." }

# 1. branch
$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne 'art/s25') {
    git show-ref --verify --quiet refs/heads/art/s25
    if ($LASTEXITCODE -eq 0) { git checkout art/s25 } else { git checkout -b art/s25 }
    if ($LASTEXITCODE -ne 0) { throw "could not switch to art/s25 (uncommitted changes in tracked files? git status)" }
}
Log "on branch art/s25 at $((git rev-parse --short HEAD).Trim())"
$t0 = Get-Date; $failed = @()

function Commit($paths, $msg) {
    git add -- @($paths)
    git diff --cached --quiet
    if ($LASTEXITCODE -eq 0) { Log "nothing new to commit for: $msg"; return }
    git commit -q -m $msg
    if ($LASTEXITCODE -ne 0) { throw "git commit failed: $msg" }
    Log "committed: $msg"
}
function Step($name, [scriptblock]$body) {
    if ($Skip -contains $name) { Log "skip $name"; return }
    Log "===== $name"
    $s = Get-Date
    try { & $body; Log ("----- {0} ok ({1:hh\:mm\:ss})" -f $name, ((Get-Date) - $s)) }
    catch { Log "----- $name FAILED: $_"; $script:failed += $name }
}

# 2. sound picks
Step 'sfx' {
    $pf = Join-Path $root 'tools\audio\sfx\picks_s25.toml'
    Copy-Item $pf (Join-Path $root 'tools\audio\sfx\picks.toml') -Force
    $ids = (Get-Content $pf | Where-Object { $_ -match '^\s*([a-z0-9_]+)\s*=' } | ForEach-Object { $Matches[1] }) -join ','
    & powershell -ExecutionPolicy Bypass -File tools\audio\sfx\make_sfx.ps1 -Pick -Only $ids
    if ($LASTEXITCODE -ne 0) { throw "make_sfx.ps1 -Pick exited $LASTEXITCODE" }
    Commit @('data\audio\sfx', 'data\audio\sounds.toml', 'tools\audio\sfx\picks.toml') 'audio(sfx): accepted Stable Audio Open picks, session 25 [M10-10]'
}

# 3. Authority skin
Step 'skin' {
    & powershell -ExecutionPolicy Bypass -File tools\art\render_s24_skin.ps1 -Install
    if ($LASTEXITCODE -ne 0) { throw "render_s24_skin.ps1 exited $LASTEXITCODE" }
    Commit @('data\ui\skins\authority') 'art(ui): Authority UI skin rendered and installed (D-245) [M10-15]'
}

# 4. portraits + death frames (resumes; -Install copies everything rendered)
Step 'portraits' {
    & powershell -ExecutionPolicy Bypass -File tools\art\render_s24_portraits.ps1 -All -Install
    if ($LASTEXITCODE -ne 0) { throw "render_s24_portraits.ps1 exited $LASTEXITCODE" }
    Commit @('data\portraits', 'data\sprites\infantry') 'art(units): portraits for every unit and infantry death frames (D-245) [M10-11]'
}

# 5. building depth pass (resumes; installs only finished ids)
Step 'depth' {
    & powershell -ExecutionPolicy Bypass -File tools\art\render_s25_depth_all.ps1 -Install
    $code = $LASTEXITCODE
    Commit @('data\sprites\buildings') 'art(buildings): depth pass on every building sheet (D-245) [M10-08]'
    if ($code -ne 0) { throw "some buildings failed (see the FAILED lines above); the finished ones are committed, rerun to retry" }
}

# 6. push
if (-not $NoPush) {
    Log "===== push"
    git push -u origin art/s25
    if ($LASTEXITCODE -ne 0) { $failed += 'push'; Log "----- push FAILED (rerun: git push -u origin art/s25)" } else { Log "----- pushed art/s25" }
}

Log ("Elapsed {0:hh\:mm\:ss}. Failed: {1}" -f ((Get-Date) - $t0), ($(if ($failed) { $failed -join ', ' } else { 'none' })))
Log "Review: scratch\art_s24\skin_preview.png, scratch\art_s24\contact_s24.png, scratch\art_s25\depth_compare.png"
if ($failed) { Log "Rerun the same command to retry the failed steps (finished work is skipped)." } else { Log "ALL DONE. Tell Claude: art done" }
