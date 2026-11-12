<#
.SYNOPSIS
  RTS_2027 session 21 A/B for Q-77: the same four skirmish rows as the s21 after-series, played with the D-219 stance rule switched OFF
  (commit 7ae29317 reverted in a scratch copy), to see whether D-219 is what weakened the Authority.

.DESCRIPTION
  Run it from the ROOT of your RTS_2027 clone, on an up-to-date main:

      git checkout main
      git pull
      powershell -ExecutionPolicy Bypass -File tools\measure\run_s21_ab.ps1

  What it does:
    1. Makes a scratch copy of this repo next to it (..\RTS_2027_ab_nod219, a git worktree; art is not downloaded) and reverts the D-219
       commit there, without committing. Nothing in your own folder changes except the results folder.
    2. Runs the after-series script in that copy for four rows (64 games each): Authority v Directorate and Authority v Expedition on
       Rust Belt, Expedition v Authority on Glass Coast, and the Left Behind mirror on Mountains layout 1.
    3. Copies the summary to balance_runs\s21ab_nod219\s21ab_nod219_summary.md here.
  The "D-219 on" side is your existing balance_runs\s21after (same code: main only added docs since 8b418a2e).
  EXPECTED RUN TIME: about 25-35 minutes with the default jobs. Afterwards you can delete the scratch copy with
      git worktree remove --force ..\RTS_2027_ab_nod219

.PARAMETER Jobs
  Matches to run at once. Default: half the logical CPUs.
#>
param(
    [int]$Jobs = [Math]::Max(1, [int][Math]::Floor([Environment]::ProcessorCount / 2))
)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root 'RTS_2027.slnx'))) { throw "Run this from the root of your RTS_2027 folder." }
$d219 = '7ae29317'
git cat-file -e "$d219^{commit}" 2>$null
if ($LASTEXITCODE -ne 0) { throw "commit $d219 (D-219) not found: run 'git pull' on main first." }

$scratch = Join-Path (Split-Path $root -Parent) 'RTS_2027_ab_nod219'
if (-not (Test-Path $scratch)) {
    Write-Host "Making the scratch copy at $scratch (no art download)..."
    $env:GIT_LFS_SKIP_SMUDGE = '1'
    git worktree add --detach $scratch HEAD
    if ($LASTEXITCODE -ne 0) { throw "git worktree add failed" }
    Push-Location $scratch
    git revert --no-commit $d219
    $rc = $LASTEXITCODE
    Pop-Location
    Remove-Item Env:GIT_LFS_SKIP_SMUDGE
    if ($rc -ne 0) { throw "reverting D-219 in the scratch copy failed; delete $scratch and tell the lead" }
} else {
    Write-Host "Reusing the scratch copy at $scratch (finished rows are skipped)."
}

$rows = 'g3_rust_belt_l1_authority3_v_directorate3_30k', 'g3_rust_belt_l1_authority3_v_expedition3_30k',
        'g4_glass_coast_l1_expedition3_v_authority3_30k', 'g2_mountains_l1_left_behind3_v_left_behind3_30k'
Push-Location $scratch
try {
    & powershell -ExecutionPolicy Bypass -File tools\measure\run_s19_after.ps1 -OutName s21ab_nod219 -Jobs $Jobs -Only ($rows -join ',')
} finally { Pop-Location }

$from = Join-Path $scratch 'balance_runs\s21ab_nod219'
$to = Join-Path $root 'balance_runs\s21ab_nod219'
New-Item -ItemType Directory -Force -Path $to | Out-Null
Copy-Item (Join-Path $from '*.md') $to -Force
Copy-Item (Join-Path $from '*.log') $to -Force
Write-Host ""
Write-Host "Done. D-219 OFF: $to\s21ab_nod219_summary.md   D-219 ON: balance_runs\s21after\s21after_summary.md"
