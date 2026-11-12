<#
.SYNOPSIS
  RTS_2027 M10-10: generate the sound effects with Stable Audio Open 1.0 on this laptop (CPU only, no GPU needed).

.DESCRIPTION
  Run from the ROOT of your RTS_2027 clone, on the branch wp/s22-sfx (or after it is merged). Windows PowerShell 5.1.
  Full guide: docs\audio\sfx_laptop.md

  Once:
      powershell -ExecutionPolicy Bypass -File tools\audio\sfx\make_sfx.ps1 -Setup     # venv .venv-sfx + packages (about 3 GB)
      # open https://huggingface.co/stabilityai/stable-audio-open-1.0 , log in, accept the terms, make a READ token
      powershell -ExecutionPolicy Bypass -File tools\audio\sfx\make_sfx.ps1 -Login     # paste the token (once)

  Then:
      ... make_sfx.ps1 -List                              # the 80 jobs and the time estimate, nothing runs
      ... make_sfx.ps1 -Generate -Draft                   # fast preview of take 1 of every sound (30 steps)
      ... make_sfx.ps1 -Generate -Only wpn_,exp_          # real quality (100 steps, 3 takes) for some sounds
      ... make_sfx.ps1 -Generate                          # everything (hours; safe to stop with Ctrl+C and start again)
      ... make_sfx.ps1 -Audition                          # builds tools\audio\sfx\out\index.html and opens it
      ... make_sfx.ps1 -Pick                              # take 1 of everything, or: -Pick wpn_flame=2,gui_back=d1
                                                          # (or save the page's picks.toml as tools\audio\sfx\picks.toml)

  Takes that already exist are skipped. The seed of every take is fixed, so a rerun gives the same sound.
  Writes tools\audio\sfx\out\<id>\take<N>.wav (git-ignored). -Pick copies to data\audio\sfx\ and edits data\audio\sounds.toml.
#>
param(
    [switch]$Setup,
    [switch]$Login,
    [switch]$List,
    [switch]$Generate,
    [switch]$Audition,
    [switch]$Pick,
    [string[]]$Only,            # job ids containing this text, or a category: weapons, explosions, buildings, deaths, production, special, alerts, ui, ambience
    [int]$Takes = 0,            # takes per sound (0 = each job's own, normally 3)
    [int]$Steps = 0,            # denoising steps (0 = 100)
    [switch]$Draft,             # 30 steps, 1 take, files named draft<N>.wav
    [double]$SecondsScale = 1.0,
    [int]$Rate = 0,             # override output sample rate (default 22050 from sfx_jobs.toml; 32000 gives crisper ambience loops)
    [int]$Threads = 0,          # CPU threads (0 = all)
    [string[]]$Picks,           # with -Pick: id=2, id=d1 ...
    [switch]$Mix,               # with -Pick: also mix the faction/class sounds into the generic events
    [switch]$ReplaceSfxr,       # with -Pick: drop the old sfxr variants where a generated sound exists
    [switch]$LatentFull         # stock 47 s window (much slower; only if short clips sound wrong)
)
$ErrorActionPreference = 'Continue'   # Windows PowerShell 5.1 turns native stderr into errors under 'Stop'
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root 'RTS_2027.slnx'))) { throw "Run this from the root of your RTS_2027 folder." }
$venv = Join-Path $root '.venv-sfx'
$py = Join-Path $venv 'Scripts\python.exe'
$tool = Join-Path $root 'tools\audio\sfx\make_sfx.py'
$outDir = Join-Path $root 'tools\audio\sfx\out'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$log = Join-Path $outDir 'make_sfx.log'
$env:PYTHONIOENCODING = 'utf-8'
$env:PYTHONUTF8 = '1'
$env:PYTHONUNBUFFERED = '1'
$env:HF_HUB_DISABLE_SYMLINKS_WARNING = '1'

function Log($msg) { $line = "{0}  {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $msg; Write-Host $line; Add-Content -Path $log -Value $line }

function Run-Py {
    param([string[]]$PyArgs)
    & $py $tool @PyArgs 2>&1 | ForEach-Object { $s = "$_"; Write-Host $s; Add-Content -Path $log -Value $s }
    return $LASTEXITCODE
}

function New-Py311Venv([string]$dir) {
    # Python 3.11 is what torch / diffusers were checked with here (3.13 is UNVERIFIED for these pins, so it is not accepted). Try the py launcher,
    # then uv (it downloads 3.11 by itself), then winget's Python 3.11 folder.
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

# Keep Windows awake (the system, not the display) until this script ends.
Add-Type -Namespace RtsSfx -Name Power -MemberDefinition '[DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint esFlags);'

if ($Setup) {
    Log "=== setup ==="
    if (-not (Test-Path $py)) {
        New-Py311Venv $venv
        if (-not (Test-Path $py)) { throw "Could not create the venv. Run 'winget install --id Python.Python.3.11 -e', open a NEW PowerShell window, then run -Setup again." }
    }
    & $py -m pip install --upgrade pip
    # torch CPU build (no CUDA download), then the rest. diffusers/transformers versions are the ones the job runner was checked against.
    & $py -m pip install torch --index-url https://download.pytorch.org/whl/cpu
    & $py -m pip install "diffusers==0.43.0" "transformers==5.22.2" accelerate sentencepiece protobuf soundfile scipy numpy huggingface_hub
    & $py -c "import torch, diffusers, transformers; print('torch', torch.__version__, '| diffusers', diffusers.__version__, '| transformers', transformers.__version__, '| threads', torch.get_num_threads())"
    Write-Host ""
    Write-Host "Next (once): accept the model terms at  https://huggingface.co/stabilityai/stable-audio-open-1.0"
    Write-Host "             (log in, press 'Agree and access repository'), create a READ token at https://huggingface.co/settings/tokens, then:"
    Write-Host "             powershell -ExecutionPolicy Bypass -File tools\audio\sfx\make_sfx.ps1 -Login"
    exit 0
}

if (-not (Test-Path $py)) { throw ".venv-sfx not found. Run -Setup first (see docs\audio\sfx_laptop.md)." }

if ($Login) {
    & $py -c "from huggingface_hub import login; login()"
    Write-Host "If it says 'Login successful', run  make_sfx.ps1 -Generate -Draft  to try a few sounds. The first run downloads the model (several GB)."
    exit 0
}

# Arguments shared by list / generate
$common = @()
if ($Only) { $common += '--only'; $common += (($Only -join ',') -split ',' | Where-Object { $_ }) }
if ($Takes -gt 0) { $common += '--takes'; $common += "$Takes" }
if ($Steps -gt 0) { $common += '--steps'; $common += "$Steps" }
if ($Draft) { $common += '--draft' }
if ($SecondsScale -ne 1.0) { $common += '--seconds-scale'; $common += ([string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0}', $SecondsScale)) }
if ($Rate -gt 0) { $common += '--rate'; $common += "$Rate" }
if ($LatentFull) { $common += '--latent-mode'; $common += 'full' }

if ($List) { $code = Run-Py (@('--list') + $common); exit $code }

if ($Generate) {
    [RtsSfx.Power]::SetThreadExecutionState([uint32]2147483649) | Out-Null
    $t0 = Get-Date
    Log "=== generate start (log: $log) ==="
    Log "Time estimate below is UNVERIFIED (no laptop timing yet): measure the first sound, the [n/total] lines show the real pace and ETA."
    $null = Run-Py (@('--dry-run') + $common)
    $gen = @() + $common
    if ($Threads -gt 0) { $gen += '--threads'; $gen += "$Threads" }
    $code = Run-Py $gen
    Log ("=== generate end, elapsed {0:hh\:mm\:ss}, exit {1} ===" -f ((Get-Date) - $t0), $code)
    Write-Host "Next: powershell -ExecutionPolicy Bypass -File tools\audio\sfx\make_sfx.ps1 -Audition"
    exit $code
}

if ($Audition) {
    $code = Run-Py @('--audition')
    $index = Join-Path $outDir 'index.html'
    if ($code -eq 0 -and (Test-Path $index)) { Start-Process $index }
    exit $code
}

if ($Pick) {
    $pargs = @('--pick')
    if ($Only) { $pargs += '--only'; $pargs += (($Only -join ',') -split ',' | Where-Object { $_ }) }
    if ($Picks) { $pargs += '--picks'; $pargs += (($Picks -join ',') -split ',' | Where-Object { $_ }) }
    if ($Mix) { $pargs += '--mix' }
    if ($ReplaceSfxr) { $pargs += '--replace-sfxr' }
    $code = Run-Py $pargs
    if ($code -ne 0) { exit $code }
    Write-Host ""
    Write-Host "Check the game still starts, then commit:"
    Write-Host "  git add data\audio\sfx data\audio\sounds.toml tools\audio\sfx\picks.toml"
    Write-Host "  git commit -m `"audio(m10-10): Stable Audio Open sound effects [M10-10]`""
    Write-Host "  git push"
    Write-Host "  (then tell Claude: it checks the SoundBank tests and credits)"
    exit 0
}

Write-Host "Nothing to do. Use -Setup, -Login, -List, -Generate, -Audition or -Pick (see the top of this file)."
