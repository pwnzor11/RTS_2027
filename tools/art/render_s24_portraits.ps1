<#
.SYNOPSIS
  RTS_2027 S24 (M10-11, D-243): pilot unit portraits and the infantry death frames, rendered on the owner's laptop (D-196, D-243).

.DESCRIPTION
  Run from the ROOT of your RTS_2027 clone, on the branch wp/s24-portrait (git fetch; git checkout wp/s24-portrait):

      powershell -ExecutionPolicy Bypass -File tools\art\render_s24_portraits.ps1             # pilot, into scratch\art_s24\
      powershell -ExecutionPolicy Bypass -File tools\art\render_s24_portraits.ps1 -Install    # also copy into the game data
      powershell -ExecutionPolicy Bypass -File tools\art\render_s24_portraits.ps1 -All        # fan-out: every unit id (resumes)
      powershell -ExecutionPolicy Bypass -File tools\art\render_s24_portraits.ps1 -All -Buildings -Install

  Uses .venv-art. Steps:
    1. Portraits (tools\booklet\render_portraits.py --pilot): auth_rifleman, auth_tank, dir_conscript, dir_rocket_truck, 96 x 96 RGBA,
       house paint in the faction accent  ->  scratch\art_s24\portraits\<id>.png
    2. Death frames (tools\art\build_infantry.py <id> --deaths): one infantry (default auth_rifleman), the whole sheet with the extra
       states die_blast, die_burn, die_rad  ->  scratch\art_s24\deaths\<id>.png + <id>.sprite.toml
       (the game sheet in data\sprites\infantry is not touched until -Install).
    3. Contact sheet of both  ->  scratch\art_s24\contact_s24.png  (send this one back).
  -Install copies the portraits to data\portraits\ and the death sheet + manifest to data\sprites\infantry\ (overwrites that id's sheet;
  the first 78 frames come out identical, the three new states are appended).
  -All: portraits for every vehicle and infantry id in tools\booklet\render_manifest.toml (status ok; -Buildings adds the 74 buildings) and
  the death states (die_blast, die_burn, die_rad) for every infantry id that has a model script, into the same scratch folders. It
  resumes: ids whose PNG is already in scratch\art_s24 are skipped unless -Force. After the first three ids of each phase it prints the
  time per id and an estimate for the rest. -Install then copies ALL portraits to data\portraits and ALL death sheets + manifests
  to data\sprites\infantry (each sheet keeps its original rows and appends the three new states). The contact sheet shows the
  portraits plus the death strips of -Infantry only.
  Rough time on this laptop: portraits about 1 min each at 64 samples; the death sheet (all frames, 3 passes each) about 3-8 min.
  Then commit data\portraits and data\sprites\infantry and look at the contact sheet first.
#>
param(
    [string]$Infantry = 'auth_rifleman',
    [int]$PortraitSamples = 64,
    [int]$Samples = 24,
    [switch]$Install,
    [switch]$Force,
    [switch]$All,
    [switch]$Buildings
)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root 'RTS_2027.slnx'))) { throw "Run this from the root of your RTS_2027 folder." }
$act = Join-Path $root '.venv-art\Scripts\Activate.ps1'
if (-not (Test-Path $act)) { throw ".venv-art not found in $root (see D-196)." }
. $act

$out = Join-Path $root 'scratch\art_s24'
$pdir = Join-Path $out 'portraits'
$ddir = Join-Path $out 'deaths'
New-Item -ItemType Directory -Force -Path $pdir, $ddir | Out-Null
$t0 = Get-Date

function Show-Eta($done, $total, $sw) {
    if ($done -ge 1 -and $done -le 3 -or ($done % 10 -eq 0)) {
        $per = $sw.Elapsed.TotalSeconds / $done
        Write-Host ("   ~{0:N0} s per id so far; about {1:N0} min left for {2} more" -f $per, ($per * ($total - $done) / 60), ($total - $done))
    }
}

# ids from the manifest: units (vehicle + infantry) with a model, buildings only with -Buildings; infantry with a model script
$idsPy = @'
import sys, tomllib
m = tomllib.load(open("tools/booklet/render_manifest.toml", "rb"))["hero"]
kinds = {"vehicle", "infantry"} | ({"building"} if sys.argv[1] == "1" else set())
what = sys.argv[2]
for i, h in m.items():
    if h["status"] != "ok":
        continue
    if what == "portraits" and h["kind"] in kinds:
        print(i)
    if what == "infantry" and h["kind"] == "infantry" and h["script"].endswith("build_infantry.py"):
        print(i)
'@
function Get-Ids($what) { $b = if ($Buildings) { '1' } else { '0' }; @($idsPy | python - $b $what | Where-Object { $_ }) }

$pids = if ($All) { Get-Ids 'portraits' } else { @('auth_rifleman','auth_tank','dir_conscript','dir_rocket_truck') }
$dids = if ($All) { Get-Ids 'infantry' } else { @($Infantry) }
if ($All -and ($dids -notcontains $Infantry)) { throw "-Infantry $Infantry is not an infantry id with a model script." }

Write-Host "== 1/3 portraits: $($pids.Count) ids"
$sw = [Diagnostics.Stopwatch]::StartNew(); $n = 0; $failed = @()
foreach ($id in $pids) {
    $png = Join-Path $pdir "$id.png"
    if ((Test-Path $png) -and -not $Force) { Write-Host "skip   $id (exists; -Force to redo)"; continue }
    python tools\booklet\render_portraits.py --ids $id --samples $PortraitSamples --out $pdir --force
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $png)) { Write-Warning "FAILED portrait $id"; $failed += "portrait:$id"; continue }
    $n++; Show-Eta $n ($pids.Count) $sw
}

Write-Host "== 2/3 death frames: $($dids.Count) infantry ids"
$sw = [Diagnostics.Stopwatch]::StartNew(); $n = 0
foreach ($id in $dids) {
    $png = Join-Path $ddir "$id.png"
    if ((Test-Path $png) -and -not $Force) { Write-Host "skip   $id (exists; -Force to redo)"; continue }
    python tools\art\build_infantry.py $id --deaths --samples $Samples --out $ddir
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $png)) { Write-Warning "FAILED deaths $id"; $failed += "deaths:$id"; continue }
    $n++; Show-Eta $n ($dids.Count) $sw
}
if ($failed) { Write-Warning ("Failed: " + ($failed -join ', ')) }

Write-Host "== 3/3 contact sheet"
$py = @'
import sys, os, tomllib
from PIL import Image, ImageDraw
pdir, ddir, sid, path = sys.argv[1:5]
man = tomllib.load(open(os.path.join(ddir, sid + ".sprite.toml"), "rb"))
fw, fh = man["frame_w"], man["frame_h"]
sheet = Image.open(os.path.join(ddir, man["file"])).convert("RGBA")
cols = sheet.width // fw
bg = (126, 150, 120, 255)
ports = sorted(f for f in os.listdir(pdir) if f.endswith(".png"))
K = 3
pl = [(f[:-4], Image.open(os.path.join(pdir, f)).convert("RGBA")) for f in ports]
rows = [("portraits", pl[i:i + 8]) for i in range(0, len(pl), 8)]
for st in man["state"]:
    if not st["name"].startswith("die"):
        continue
    fr = []
    for k in range(st["frames"]):
        i = st["first"] + k
        fr.append((f"{k}", sheet.crop(((i % cols) * fw, (i // cols) * fh, (i % cols) * fw + fw, (i // cols) * fh + fh))))
    rows.append((st["name"], fr))
cell = max(96 * 2, fw * K)
H = sum(cell + 18 for _ in rows)
W = max(len(r[1]) for r in rows) * cell
out = Image.new("RGBA", (W, H), bg)
d = ImageDraw.Draw(out)
y = 0
for name, items in rows:
    for n, (lab, im) in enumerate(items):
        s = 2 if name == "portraits" else K
        im2 = im.resize((im.width * s, im.height * s), Image.NEAREST)
        out.paste(im2, (n * cell + (cell - im2.width) // 2, y + (cell - im2.height) // 2), im2)
        d.text((n * cell + 4, y + cell + 2), f"{name} - {lab}", fill=(20, 20, 20, 255))
    y += cell + 18
os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
out.convert("RGB").save(path)
print("contact sheet:", path)
'@
$contact = Join-Path $out 'contact_s24.png'
$py | python - $pdir $ddir $Infantry $contact
if ($LASTEXITCODE -ne 0) { throw "contact sheet failed" }

if ($Install) {
    $pd = Join-Path $root 'data\portraits'
    New-Item -ItemType Directory -Force -Path $pd | Out-Null
    Copy-Item (Join-Path $pdir '*.png') $pd -Force
    $sd = Join-Path $root 'data\sprites\infantry'
    foreach ($id in $dids) {
        if (-not (Test-Path (Join-Path $ddir "$id.png"))) { Write-Warning "no death sheet for $id, not installed"; continue }
        Copy-Item (Join-Path $ddir "$id.png") $sd -Force
        Copy-Item (Join-Path $ddir "$id.sprite.toml") $sd -Force
    }
    Write-Host "Installed: $((Get-ChildItem $pdir -Filter *.png).Count) portraits in data\portraits, death sheets of: $($dids -join ', ')"
    Write-Host "Next: git add data\portraits data\sprites ; git commit -m `"art(units): portraits and infantry death frames [M10-11]`""
} else {
    Write-Host "Not installed (add -Install after you have looked at the contact sheet)."
}
Write-Host ("Elapsed {0:hh\:mm\:ss}. Contact sheet: {1}" -f ((Get-Date) - $t0), $contact)
