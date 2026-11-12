<#
.SYNOPSIS
  RTS_2027 laptop audio orchestrator: music export (M10-09), Kokoro radio voices (M8-18), sound effects.

.DESCRIPTION
  Run from the ROOT of your RTS_2027 clone (Windows PowerShell 5.1, CPU only is fine):

      powershell -ExecutionPolicy Bypass -File tools\audio\make_audio.ps1 -Setup           # once: .venv-audio + Kokoro
      powershell -ExecutionPolicy Bypass -File tools\audio\make_audio.ps1 -VoiceAudition   # candidate voices -> tools\voice\audition\index.html
      (listen, put one voice per leader in tools\voice\leaders.json)
      powershell -ExecutionPolicy Bypass -File tools\audio\make_audio.ps1 -Voice           # every radio line -> data\audio\voice\
      powershell -ExecutionPolicy Bypass -File tools\audio\make_audio.ps1 -Music           # take 1 of 20 tracks -> data\music\
      powershell -ExecutionPolicy Bypass -File tools\audio\make_audio.ps1 -All             # Music + Voice (+ Sfx if present)

  Options:
    -Pick "menu=2,victory=3"   music: use another take for some tracks (others use take 1)
    -AceDir "D:\ACE-Step-1.5"  music: where ACE-Step lives (default C:\ACE-Step-1.5; takes are in <dir>\output\rts)
    -Only leader_dir_larkin    voice / audition: one leader only
    -Force                     voice: redo files that already exist
    -Sfx                       runs tools\audio\sfx\make_sfx.ps1 if it exists
  Keeps the PC awake while it runs, appends to tools\audio\make_audio.log, prints the git lines at the end.
#>
param(
    [switch]$Setup,
    [switch]$Music,
    [string]$Pick = "",
    [string]$AceDir = "C:\ACE-Step-1.5",
    [switch]$VoiceAudition,
    [switch]$Voice,
    [string]$Only = "",
    [switch]$Force,
    [switch]$Sfx,
    [switch]$All
)
$ErrorActionPreference = 'Continue'
$env:PYTHONIOENCODING = 'utf-8'
$env:PYTHONUTF8 = '1'
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root 'RTS_2027.slnx'))) { throw "Run this from the root of your RTS_2027 folder." }
$venv = Join-Path $root '.venv-audio'
$py = Join-Path $venv 'Scripts\python.exe'
$log = Join-Path $root 'tools\audio\make_audio.log'
$sfxScript = Join-Path $root 'tools\audio\sfx\make_sfx.ps1'
if ($All) { $Music = $true; $Voice = $true; if (Test-Path $sfxScript) { $Sfx = $true } }
if (-not ($Setup -or $Music -or $VoiceAudition -or $Voice -or $Sfx)) {
    Write-Host "Nothing to do. Use -Setup, -VoiceAudition, -Voice, -Music, -Sfx or -All (see the header of this file)."; exit 0
}

# Keep Windows awake (system, not display) until this script ends (same call as rts_batch.ps1).
Add-Type -Namespace RtsAudio -Name Power -MemberDefinition '[DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint esFlags);'
[RtsAudio.Power]::SetThreadExecutionState([uint32]2147483649) | Out-Null
function Log($msg) { $line = "{0}  {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $msg; Write-Host $line; Add-Content -Path $log -Value $line }
function Run-Logged([string]$what, [scriptblock]$cmd) {
    Log "start  $what"
    $t0 = Get-Date
    & $cmd 2>&1 | ForEach-Object { $s = "$_"; Write-Host $s; Add-Content -Path $log -Value $s }
    $ok = ($LASTEXITCODE -eq 0)
    Log ("{0} {1} ({2:mm\:ss})" -f $(if ($ok) { 'done  ' } else { 'FAILED' }), $what, ((Get-Date) - $t0))
    return $ok
}
function New-Py311Venv([string]$dir) {
    # Kokoro needs Python 3.11 (3.13 builds numpy 1.26 from source and fails). Try the py launcher, then uv (installed with
    # ACE-Step; it downloads 3.11 by itself), then winget's Python 3.11.
    try { & py -3.11 -m venv $dir 2>$null } catch { }
    if (Test-Path (Join-Path $dir 'Scripts\python.exe')) { return }
    $uv = Get-Command uv -ErrorAction SilentlyContinue
    if (-not $uv -and (Test-Path "$env:USERPROFILE\.local\bin\uv.exe")) { $uv = Get-Item "$env:USERPROFILE\.local\bin\uv.exe" }
    if ($uv) {
        Log "py launcher has no 3.11; using uv to fetch Python 3.11"
        $uvPath = if ($uv.Source) { $uv.Source } else { $uv.FullName }
        & $uvPath venv --seed --python 3.11 $dir
        if (Test-Path (Join-Path $dir 'Scripts\python.exe')) { return }
    }
    $wp = "$env:LOCALAPPDATA\Programs\Python\Python311\python.exe"
    if (Test-Path $wp) { & $wp -m venv $dir }
}
function Need-Venv { if (-not (Test-Path $py)) { throw ".venv-audio not found. Run with -Setup first." } }
function Find-Espeak {
    $dll = @("$env:ProgramFiles\eSpeak NG\libespeak-ng.dll", "${env:ProgramFiles(x86)}\eSpeak NG\libespeak-ng.dll") | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($dll) { $env:PHONEMIZER_ESPEAK_LIBRARY = $dll; $env:PHONEMIZER_ESPEAK_PATH = (Split-Path $dll) }   # UNVERIFIED: only needed if Kokoro cannot find espeak-ng by itself
    return [bool]$dll
}

Log "=== make_audio start ==="
$failed = @(); $todo = @()

if ($Setup) {
    if (-not (Test-Path $py)) {
        Log "creating .venv-audio with Python 3.11"
        New-Py311Venv $venv
        if (-not (Test-Path $py)) { throw "Could not create the venv. Run 'winget install --id Python.Python.3.11 -e', open a NEW PowerShell window, then run -Setup again." }
    }
    $ok = Run-Logged "pip install kokoro soundfile imageio-ffmpeg numpy" { & $py -m pip install --upgrade pip kokoro soundfile imageio-ffmpeg numpy }
    if (-not $ok) { $failed += 'setup' }
    if (-not (Find-Espeak)) {
        Write-Host ""
        Write-Host "espeak-ng is not installed. Install it once, then open a NEW PowerShell window:" -ForegroundColor Yellow
        Write-Host "    winget install --id eSpeak-NG.eSpeak-NG -e"
        Write-Host "(or download the .msi from https://github.com/espeak-ng/espeak-ng/releases)"
    } else { Log "espeak-ng found" }
    Log "Setup finished. The first -VoiceAudition downloads the Kokoro-82M model (a few hundred MB)."
}

if ($VoiceAudition) {
    Need-Venv; [void](Find-Espeak)
    $a = @('tools\voice\kokoro_batch.py', '--audition'); if ($Only) { $a += @('--only', $Only) }
    if (-not (Run-Logged "voice audition" { & $py @a })) { $failed += 'audition' }
    else {
        Log 'Open tools\voice\audition\index.html, pick one voice per leader and set "voice" in tools\voice\leaders.json.'
        $todo += "git add tools\voice\leaders.json ; git commit -m `"tools(voice): leader voices chosen [M8-18]`""
    }
}

if ($Voice) {
    Need-Venv; [void](Find-Espeak)
    $a = @('tools\voice\kokoro_batch.py'); if ($Only) { $a += @('--only', $Only) }; if ($Force) { $a += '--force' }
    if (-not (Run-Logged "voice lines" { & $py @a })) { $failed += 'voice' }
    else { $todo += "git add data\audio\voice ; git commit -m `"data(audio): Kokoro radio voice lines [M8-18]`"" }
}

if ($Music) {
    Need-Venv
    $a = @('tools\audio\export_music.py', '--ace-dir', $AceDir); if ($Pick) { $a += @('--pick', $Pick) }
    if (-not (Run-Logged "music export" { & $py @a })) { $failed += 'music' }
    else { $todo += "git add data\music ; git commit -m `"data(audio): music tracks from ACE-Step [M10-09]`"" }
}

if ($Sfx) {
    if (Test-Path $sfxScript) {
        if (-not (Run-Logged "sound effects" { & powershell -ExecutionPolicy Bypass -File $sfxScript })) { $failed += 'sfx' }
        else { $todo += "git add data\audio ; git commit -m `"data(audio): sound effects [M3-INT]`"" }
    } else { Log "skip -Sfx: tools\audio\sfx\make_sfx.ps1 not found" }
}

Log ("=== make_audio end. Failed: {0} ===" -f $(if ($failed) { $failed -join ', ' } else { 'none' }))
[RtsAudio.Power]::SetThreadExecutionState([uint32]2147483648) | Out-Null
if ($todo) {
    Write-Host ""
    foreach ($t in $todo) { Write-Host "Next: $t" }
    Write-Host "Next: git push"
}
