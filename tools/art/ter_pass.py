#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""S43 terrain pass, batchable: one asset id per invocation, explicit output dir, deterministic.

  python3 tools/art/ter_pass.py <asset_id> --out <dir> [--palette <toml>] [--scale 64] [--bin <dir with TerrainDump.dll>]

asset ids (see tools/art/ter_pass_ids.txt): `map:<repo-relative map file>` or `gen:<biome>:<s|m|l>:<players>:<seed>`.
Bakes with the game's own TerrainBake/DecorBake (tools/terrain_dump, built once to --bin), composites decor over terrain at
<scale> px per cell (game: 64, nearest) and writes <out>/<id>.png plus <id>.timing.txt. Needs Pillow + numpy.
"""
import argparse, os, re, struct, subprocess, sys, time
import numpy as np
from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def load(f):
    b = open(f, 'rb').read(); w, h = struct.unpack('<ii', b[:8])
    return Image.frombytes('RGBA', (w, h), b[8:])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('asset_id'); ap.add_argument('--out', required=True)
    ap.add_argument('--palette'); ap.add_argument('--scale', type=int, default=64)
    ap.add_argument('--bin', default=os.path.join(REPO, 'scratch', 'terrain_dump_bin'))
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    dll = os.path.join(a.bin, 'TerrainDump.dll')
    if not os.path.exists(dll):
        subprocess.check_call(['dotnet', 'build', os.path.join(REPO, 'tools', 'terrain_dump'), '-c', 'Release', '-o', a.bin, '-m:1', '-v', 'q'])
    safe = re.sub(r'[^A-Za-z0-9_.-]+', '_', a.asset_id)
    prefix = os.path.join(a.out, safe)
    kind, _, rest = a.asset_id.partition(':')
    if kind == 'map': src = ['map', os.path.join(REPO, rest)]
    elif kind == 'gen': b, sz, pl, sd = rest.split(':'); src = ['gen', b, sz, pl, sd]
    else: sys.exit('asset id must be map:<path> or gen:<biome>:<size>:<players>:<seed>')
    env = dict(os.environ)
    if a.palette: env['HARNESS_PALETTE'] = a.palette
    t0 = time.time()
    subprocess.check_call(['dotnet', dll, prefix] + src, env=env)
    t1 = time.time()
    hdr = open(prefix + '.cells.txt').readline().split(); mw, mh = int(hdr[0]), int(hdr[1])
    k = a.scale
    t = load(prefix + '.terrain.rgba').resize((mw * k, mh * k), Image.NEAREST)
    d = np.asarray(load(prefix + '.decor.rgba').resize((mw * k, mh * k), Image.NEAREST)).astype(np.int32)
    tn = np.asarray(t).astype(np.int32); al = d[..., 3:4]
    out = tn.copy(); out[..., :3] = d[..., :3] + tn[..., :3] * (255 - al) // 255
    Image.fromarray(out.clip(0, 255).astype('uint8'), 'RGBA').save(prefix + '.png')
    open(prefix + '.timing.txt', 'w').write('bake+dump %.2fs, composite %.2fs\n' % (t1 - t0, time.time() - t1))
    print('wrote', prefix + '.png')

main()
