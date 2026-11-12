# RTS_2027 soundtrack batch for ACE-Step 1.5 (M10-09). Runs every job in rts_jobs\ one take at a time.
# Usage (PowerShell, in C:\ACE-Step-1.5, with the ACE-Step browser UI CLOSED so the model is not loaded twice):
#   powershell -ExecutionPolicy Bypass -File .\rts_batch.ps1            # take 1 of every track (about 9-10 h on this laptop)
#   powershell -ExecutionPolicy Bypass -File .\rts_batch.ps1 -Takes 2   # adds take 2 (skips what already exists)
# Safe to stop (Ctrl+C) and re-run: finished takes are skipped. Keeps the PC awake while it runs.
param(
    [int]$Takes = 1,          # how many takes per track in total
    [string]$Only = ""        # optional: run only jobs whose name contains this text, e.g. -Only battle
)
$ErrorActionPreference = "Continue"
$env:PYTHONIOENCODING = "utf-8"   # ACE-Step prints emoji; the Windows console code page cannot
$env:PYTHONUTF8 = "1"
Set-Location -Path $PSScriptRoot
$python = Join-Path $PSScriptRoot ".venv\Scripts\python.exe"
if (-not (Test-Path $python)) { Write-Host "Cannot find $python - run 'uv sync' first."; exit 1 }
$jobs = Get-ChildItem -Path (Join-Path $PSScriptRoot "rts_jobs") -Filter "*.toml" | Sort-Object Name
if ($Only) { $jobs = $jobs | Where-Object { $_.Name -like "*$Only*" } }
$log = Join-Path $PSScriptRoot "rts_batch.log"
$tmp = Join-Path $PSScriptRoot "rts_jobs\_run"
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

# Keep Windows awake (system, not display) until this script ends.
Add-Type -Namespace RtsBatch -Name Power -MemberDefinition '[DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint esFlags);'
[RtsBatch.Power]::SetThreadExecutionState([uint32]2147483649) | Out-Null

function Log($msg) { $line = "{0}  {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $msg; Write-Host $line; Add-Content -Path $log -Value $line }

Log "=== batch start: $($jobs.Count) jobs, takes 1..$Takes ==="
for ($take = 1; $take -le $Takes; $take++) {
    foreach ($job in $jobs) {
        $track = ($job.BaseName -replace '^\d+_', '')
        $outDir = "output/rts/$track/take$take"
        $full = Join-Path $PSScriptRoot $outDir
        if ((Test-Path $full) -and (Get-ChildItem -Path $full -Filter "*.wav" -ErrorAction SilentlyContinue)) { continue }
        $runToml = Join-Path $tmp "$($job.BaseName)_take$take.toml"
        $text = (Get-Content -Raw $job.FullName) + "save_dir = '$outDir'`n"
        [System.IO.File]::WriteAllText($runToml, $text, (New-Object System.Text.UTF8Encoding $false))   # no BOM: Python's toml rejects it
        Log "start  $track take $take"
        $t0 = Get-Date
        & $python cli.py --config $runToml --log-level INFO *>> (Join-Path $PSScriptRoot "rts_batch_detail.log")
        $mins = [math]::Round(((Get-Date) - $t0).TotalMinutes, 1)
        if ((Test-Path $full) -and (Get-ChildItem -Path $full -Filter "*.wav" -ErrorAction SilentlyContinue)) { Log "done   $track take $take ($mins min)" }
        else { Log "FAILED $track take $take ($mins min) - see rts_batch_detail.log" }
    }
}
Log "=== batch end ==="
[RtsBatch.Power]::SetThreadExecutionState([uint32]2147483648) | Out-Null
