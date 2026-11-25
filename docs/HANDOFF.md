# Handoff: Session 45 — balance re-tune after the mechanics wave (Expedition), then the correctness wave (docs/BACKLOG.md)
Last commit: `docs: Session 44 handoff` on `integ-s44` (PR to main, merged by the lead). Laptop folder: `git checkout main; git pull` before building
(it was still on session 39 when s44 started).
Where things stand: session 44 landed the held s43 economy/AI fixes (D-318) and built every M16 mechanic the owner approved (D-317..D-320:
sea mines, Diver swimming, Drydock production/repair, second and third weapon slots, harpoon tether, grapple breach, Left Behind upgrade
effects, Provision Store stock, hover harvester on Shallow, beach-only deploy, beacon delivery, naval gunfire, sub dive time + torpedo, survey
overlay, the rule fixes, AI key/repair fixes, C&C control habits, refinery name, campaign tiers). Save/replay format 15. The 512-game re-measure
FAILED the D-305 gate: the Expedition fell 8-37 points in every row (docs/balance/s44_balance.md). One cause (the AI naval plan) is switched off
(D-323); with it off the table is still out of the gate (pooled Authority v Expedition 69.7, Left Behind v Expedition 66.3, Authority v Directorate 33.6; 4 rows outside 25-75). The campaign sweep meets every D-222 target (45/45). Decisions up to D-323. OPEN_QUESTIONS: Q-89, Q-90 (below).

## Start here (session 45; shown to the user as the plan; nothing starts until they say go)
1. **Owner balance calls first (Q-89, Q-90):** the s44 bisect found two more causes of the Expedition drop besides the naval plan:
   the Left Behind upgrades merge (Scrap Plate Light -> Heavy armour, D-322) and the A-02/rule-fix merge for the Authority (most likely M-22,
   Flak sparing Authority aircraft). Options per item: keep / soften (e.g. Scrap Plate as a damage-taken percent, Patchwork x2) / revert.
   Then screen variants (128 games, fresh seed chunks) and decide on 512 games; re-run the campaign sweep (D-222) after any change.
2. **Authority v Directorate pooled 33.6 %** (s42 40.6, bal43 36.5): the held D-311 items cost ~4 points (D-318); tune with the Expedition pass.
3. **Correctness wave (no outcome change):** BACKLOG M-27, M-30, M-31, C-05..C-07, C-10, U-01..U-04, U-06..U-10, U-14, V-06, N-01..N-05, T-01..T-08.
4. **Art wave (cluster):** V-03 Expedition + Left Behind infantry, C-01 ending slides, V-04/V-05.
5. Owner items still waiting: M9-15 two-PC LAN test; M7-14 Expedition play-test (now with the new naval mechanics); M10-27 voice log; the s43
   art in a skirmish; the auth_01 voice line still says "Refinery"; D-321 LFS migration (needs its own decision); confirm D-322 values;
   delete merged branches (list below).

## Half-finished work
- Balance: D-305 gate not met (above). Probe branch `probe-s44-nom16`, `probe-s44-noa03` (never merge).
- AI naval plan: levers kept in data (naval_min_tier 0 = off); AiNavalUseTests switch it on at tier 3.
- Pathing does not plan through a closed enemy wall for a Grapple breach (re-issue the move once it opens); the Skyhook 2-vehicle airlift TODO.
- Cluster: /work/out/f44 (24 rows, 5cd0c80), g44 (12 Expedition rows, d01a89c), sw44 (campaign sweep, 45/45 meet D-222), p44 / q44 / m16b /
  a03b / bis0..7 (probes). Laptop k3s-cluster folder `claude/rts/s44/`: t-integ1.sh (suite by REF), f44.sh, g44.sh, sw44.sh, bld*.sh, CSVs.
- Branches to delete after the PR merges: integ-s44, bal-s43, bal-s43-noaa, bal-s43-aioff, probe-s44-*, wp/s44-* (local worktrees only), and the
  older integ-s40..s43.

## Waiting on the user
- Q-89, Q-90; D-322 confirmations; M9-15; M7-14; M10-27; branch deletes; D-321.

## How to run this session
- Session 44 lessons:
  - Orphan `VBCSCompiler` servers inherit a `flock` file descriptor and hold the lock forever: use `flock -o <lock>` plus
    `--disable-build-servers`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `UseSharedCompilation=false` (docs/process/s44_wave_rules.md). With that, 12
    helpers built and tested one at a time on the 2-CPU box without starving.
  - Pre-add step hooks and stub partial files for every new sim system before the wave; helpers then only touch their own files. Expect
    conflicts in RulesHash.cs, UnitRulesLoader key lists and UpgradeType: keep both sides.
  - Every new state part shifts tail-indexed part-order pins (M3/M4 contract tests use `^n`), the save and replay versions, and the golden/
    ScriptedBattle/SkirmishDefaultHash/WaveCommit pins: re-pin once after the last merge (RTS_UPDATE_GOLDEN=1 for goldens).
  - A blanket .gitattributes LFS pattern re-flags every tracked non-LFS file of that type (D-321).
  - Series pods waiting for a binary hold the x86 slots their own build needs (and a piece that waits 30 min "completes" with no result): submit
    builds first, wait for them, then the series; if stuck, set the series parallelism to 0 until the build is done and re-run the job clean.
  - Bisect a balance regression over the merge commits (first-parent), 2 rows x 128 games per commit, 8 commits in one round (~45 min).
  - A mechanic that looks neutral can move balance a lot through the AI: measure AI-use changes separately from the rule itself.
- Session 43 lessons:
  - The cloud box has 2 CPUs / 7 GB. Never let helpers run dotnet test in parallel: they starve on one lock and their runs are killed at 30 min.
    Helpers compile once at most; the lead runs full suites on the cluster (`claude/rts/s43/test43.sh`, `--arch any --cpu 3 --mem 6Gi`, or
    `--cpu 2 --mem 4Gi` when series pods fill the nodes; failure messages from `$OUT/trx/*.trx`).
  - The laptop bridge drops large SSH/SFTP packets: kjob.py `cl_put` falls back to chunked base64; download big files with
    `PYTHONPATH=. python3 rts/s43/rget.py <remote> <local>` (resumable, 256 KB pieces, ~95 s per call). Copy both into k3s-cluster-code.
  - bpy runs on the x86 nodes only (huntsman, prince; queen cordoned): ~4 min per building, 8-35 min per vehicle with a move state, ~5 min
    per infantry at 1 CPU. The bpy venv needed scipy (art43.sh installs it).
  - A per-mechanic or per-asset helper must not delete module constants: dir_rad_mortar lost `ID` in the fan-out and failed on the cluster.
  - Audit accounting must only count `A43-`-tagged commits on the target branch; S18 ids collide with S43 ids.
  - `git stash` is shared across worktrees: tell helpers never to stash.
- Session 42 lessons:
  - `start_row_shift` accepts 0..24 only; check a key's range in BiomeCatalog.cs before screening (a variant fails at once, not silently).
  - Lowering a running job's parallelism kills running pods (their pieces rerun); prefer submitting new jobs with a small --par and raising them.
  - The 3-CPU full-suite pod cannot land while series pods and the `whisper` app fill huntsman/prince: the cloud box ran the full suite per project in
    ~75 min (`/tmp/suite.sh` pattern: build once, `dotnet test tests/<P>.Tests --no-build` per project, sums in one file) — keep the turn alive with sleeps.
  - Reuse earlier tables: s41's ref41 + f41 (main, seeds 1-128) plus new chunks made the 512-game table; tab42.sh merges by chunk range and tag.
  - 64-game map screens swing up to ~24 points on the same build between seed sets; take screen winners only as candidates and decide on 256-512 games.
  - Map-hash pins are hex in MapGenOceanTests/OtherBiomePinTests/Layout2Tests: convert the decimal Actual from the test log before replacing.
- Session 41 lessons:
  - **One seed set is one set of maps.** Rows differ by ~13 points between seeds 1-8 and the 128-game table, and by 7.6 between seeds 1-64 and 65-128.
    Never run a sweep's variants all on the same seeds; spread variants over different seed chunks (or anchor at a many-seed reference) and decide
    on >= 256 games. Unit levers of 4-8 % move a row only 1.5-4 points.
  - `Tools ai duels` answers "is this unit worth its money" in ~0.2 s per fight in the cloud; use it before any game screen of unit numbers.
  - mkvar.py (s41 copy at /work/in/s41/mkvar.py) has op `~` for raw TOML values (arrays): `ai difficulty.toml [faction.authority] vehicle_tier2 ~["a","b"]`.
  - v41.sh takes OFF= (chunk offset) so fresh seeds are one variable away; tally41.py dumps tag,row,w,l,d for scripts.
  - Background runs in the cloud container stop when a reply ends: use send_later check-ins for long cluster jobs and keep cloud work inside a turn.
  - Git LFS: the cloud clone skips smudge; `git lfs pull` (~1 min) before Game.Tests or 28 art/audio tests fail on pointer files.
- s40: biome keys only change their own biome: screen biomes separately and compose (no combined screen needed). Screen winners regress on fresh
  seeds; decide on chunks 4-7. Use `/work/in/s40/tab40.py <dir> <tag> [chmin chmax]` and `cmp39.py var ref` (ref dir with main__ names, e.g. ref41).
- s40: the laptop can drop off the bridge for hours; cluster jobs carry on. Keep cloud work (tests, ladder runs) going meanwhile; checkpoint early.
- Session 39 lessons:
  - Keep the sum of all running jobs' parallelism at or below the free slots (~36 with queen cordoned): over-committed jobs left new jobs' pods
    Pending for 30+ min (scheduler backoff); a --fast build landed on a Pi once slots were freed. Lower parallelism, then restore.
  - Per-faction AI constants as data keys (FactionTuning init properties, defaults = the constants) make every variant data-only: one build per round.
    `/work/in/s39/mkvar.py` appends a missing `[section]` (e.g. `[faction.expedition.tier4]`).
  - Screen 64, decide 128 by running chunks 4-7 of the same seeds in a second job (`CH=$((CH + 4))` after the `read` line, see v39f.sh); cmp39.py merges dirs.
  - A per-tier ladder change touches no tier-3 table row and a handicap-gated change touches no handicap-100 game: prove it with two rows game for game.
  - Never write a file from a Python expression that can be None (`open(p,'w')` truncates first); a weapons.toml edit emptied the file once.
- Session 38 lessons:
  - Check for an unlanded earlier session before planning: session 37 ran overnight on integ-s37 and sat as a draft PR (`git ls-remote origin '*s37*'`,
    open PRs). Read its HANDOFF from that branch. Undraft with `gh api -X POST repos/pwnzor11/RTS_2027/pulls/<n>/ccr/ready_for_review` (GraphQL is blocked).
  - GitHub shows no Windows CI runs since Nov 12 (check-runs empty for every recent commit): verify with a cluster full suite (`claude/rts/s38/test_*.sh`,
    `--fast --cpu 3 --mem 6Gi`, ~18 min) before merging.
  - Map-generator data (`data/mapgen/biomes.toml`) is read next to Tools.dll, not from the working tree: a variant needs its own copy of the binary folder
    (`claude/rts/s38/var2.sh` does it). Rules and AI data come from the working tree. Check a variant moves at least one row before trusting "no effect".
  - `claude/rts/s38/var2.sh` takes per-line tiers ("tag biome FA TA FB TB"); `/work/in/s37/mkvar.py` now edits `mapgen` and string values
    (`symmetry_2p =rot2`). Helpers on the toolbox pod: tally.py, cmp.py (v the s37 table), cmp2.py (v any reference dir), inc.py (income), led.py (ledgers), sw.py (sweep).
  - Per-faction tier tables MERGE with the shared tier (keys not set keep the shared value); `respond_to_attacks` is now an allowed key.
  - `attack_ratio_pct` is inert for the Authority (as for the Expedition, s28). The Rocket Truck cost is a cliff.
  - A build pod waits behind series pods that wait for it: set the waiting job's parallelism to 0 until the build finishes.
  - Throughput: ~5 pieces (16 games) a minute on the whole cluster; plan ~25 000 games for a full day.
- Session 37 lessons:
  - The laptop folder mounts are named by path now (`$HOME/mnt/Documents--RTS_2027`, `$HOME/mnt/Roaming--RTS_2027`, `$HOME/mnt/k3s-cluster`).
  - Data-only variants need no build: `claude/rts/s37/mkvar.py` copies data/ from a built tree and edits keys; `var.sh` runs `ai match` from that
    tree with the same Tools.dll (rules are found by walking up from the working directory). One job per variant set (`SET=`), CHUNKS 4 screen /
    8 decide. `sum.py`, `pivot.py`, `ana.py` (variant v baseline per row), `/tmp/mad.py`-style table metrics.
  - Patch a running job's script by patching its configmap (`job-<name>`, key run.sh) with kubectl `--patch-file`; new pods read it.
  - A build pod (2 CPU) stays Pending behind a series on x86: lower the series' parallelism first (`kubectl patch job ... parallelism`).
  - gpt-oss:120b runs on the NAS's Ollama (80 GB limit): unload qwen3:30b first (`/api/generate` keep_alive 0); first load ~7 min; ~74 s per
    commander call; one 16-game set ~4 h. The proxy sends `"think": "low"`.
  - The Rocket Truck cost is a cliff lever; screen cost steps of 2-4 %, not 15 %.
  - Mirror-x maps with mid-row starts exposed an AI bug the diagonal maps hid: test new layouts with incomes per faction (`--income 1`), not only
    win rates.
- Session 36 lessons:
  - `Tools ai mirror` now follows the map's symmetry; a first divergence on an early building placement points at a map/exit asymmetry.
  - Every building exits and docks south: any top/bottom start layout leans to the top base. Check a new map layout with the mirror probe
    and a 4-faction mirror screen (128 games per faction) before shipping it.
  - Map changes move balance rows that play on that map: re-run those rows before landing (Glass Coast mirror_x looked fair in mirrors but
    broke Expedition v Authority).
  - `ai commander --random-only <fields>` gives paired ablations (same random stream): 128 games per row resolve a 10-point effect; compare
    each row game for game with the "none" row. Many small cluster jobs (16 games per index) on all nodes finish 2 000 games in ~40 min.
  - A full-suite test pod (3 CPU x86) stays Pending while a series fills the x86 slots: submit it before the series or lower the series'
    parallelism. `kjob.py fetch` into an existing folder fails on overwrite (no delete permission in the bridge): fetch into a new folder.
  - The NAS's Ollama runs one commander game set in ~4 h: start the model run early in a session.
- Session 35 lessons:
  - The cluster now includes queen (8-vCPU VM on the TrueNAS box) and /work lives on the NAS (file locks work there). The NAS's Ollama
    (port 30068) runs local models: the commander needs a localhost proxy (KeyPolicy only sends a key over http to loopback) that maps the
    OpenAI chat call to `/api/chat` with `think: false` and `format: "json"`, plus `timeout_seconds = 180` in providers.toml
    (`claude/rts/s35b/cmd_model.sh`). Without JSON mode qwen3 writes until the token cap.
  - Debian Blender (Pis) in the `python:3.13-slim` image could not import numpy (it took /usr/local's Python); setup-tools.sh now installs
    a wrapper, and Pi sheets match x86 ones. setup-tools.sh runs under `set -e`: a failing pipe in the job script ends the job.
  - Unit sheet re-render: one shared claim list for x86 and Pi jobs (`claude/rts/s35b/u32/units32.sh`); 77 items took ~2 h on the x86
    nodes alone. Verify against the previous sheets by script (states, origin, px_per_cell, shared facings' alpha).
  - Files committed to the laptop folder through device_commit_files can be read one version late by the next device_bash call (twice
    this session): edit job scripts in place on the device with sed/python, or check the content before submitting.
  - kjob gives each job its own `$OUT`: jobs sharing a claim list set `OUT` themselves; setup-tools sets `ARCH`, so use `MYARCH`.
- Session 34 lessons:
  - `build_vehicle.py <id> --facings ...` writes straight into data/sprites/vehicles, even for an existing id and a facing subset: it
    overwrote the live auth_bulwark sheets. Copy the outputs to scratch, then `git checkout -- data/sprites/...` and remove candidate sheets.
  - The anonymous git lane cannot see PRs or CI (api.github.com refuses unattached repos): attach push access (owner asked in chat) to read
    PR state and merge.
  - Warzone weapon .pie models are very low-poly (gnhcan 8 points, gnhvcan 14) and carry their detail in the CC BY-SA weapon page: not worth
    another kitbash attempt (D-274). Cloud Blender (pip bpy 5.3) renders a 4-facing two-layer vehicle preview in ~20 s.
  - A docs-only PR's Windows CI takes 10-15 min: poll check-runs in one foreground loop under 600 s, then poll again; do not end the turn on it.
  - The owner's folder had ~170 untracked run/transfer leftovers at the root: future laptop scripts should write into scratch/ (git-ignored).
- Session 33 lessons:
  - Read the roadmap critically: four "open" M10 items were already done in earlier sessions (session logs and decisions said so). Grep
    SESSION_LOG/DECISIONS for an item before planning work on it.
  - With push access attached, LFS uploads from the cloud worked again (151 hero PNGs; checked with an LFS batch "download" request).
  - Cluster Blender: `bpy-python` needs `pip install pillow numpy` for our tools; a 5-CPU pod cannot schedule beside another 5-CPU pod on one
    x86 node (allocatable 6 / 4): use 2-3 CPU pods or the second node; prince has 4 cores. Directorate still scripts write to scratch/stills.
  - kjob.py has no --env: prepend `export VAR=...` to a copy of the script. A fixed ids[IDX::N] split under-uses mixed-speed nodes.
  - Fetching large outputs through the device bridge is slow (~150 KB/s): split on snowwhite into 1 MB parts and pull with several sftp
    threads in <170 s calls (resume by .ok markers), then stage one .tar into the cloud.
  - Booklet: `.unit .art img` was `image-rendering: pixelated`, which made the hero renders look blocky when zoomed; fixed. The PDF is plain
    git (38.6 MB): keep embedded pictures at 900/560 px.
  - The owner reviews art by before/after sheets and answers quickly; "full send" = finish without another review gate.
- Session 32 lessons:
  - The owner gave Claude rights to delete cluster jobs ("give yourself access"); `kjob.py prune` at session start, jobs self-delete after 2 days.
  - Cluster: Pis fit 4 x 800m pods (Longhorn takes 444m); submit a build and let it start BEFORE series or traces that wait on it (trace pods on
    huntsman starved their own build); `--cpu 100m` for single-thread side jobs on a full cluster. A 135-index campaign sweep takes ~14 min.
  - device_commit_files called in parallel with the Bash edit that writes the file can upload the old version: commit in a later call.
  - A variant that changes one faction's key leaves rows without that faction game-for-game identical: run only the rows it can touch, and a
    code variant with the rule switched off (e0) is a free control (it matched main game for game).
  - 64-game screens can mislead by ~7 points (shepherd s1: -7 on 64, -1.4 on 128): decide on 128.
  - Built-in Agent worktree isolation is refused: create worktrees under /home/claude/wt yourself and give each helper its path.
  - Merging a PR needs the owner's OK in chat ("merge N" / "land it").
- Session 31 lessons:
  - Cluster changed at the end of s31 (pi-cluster skill updated): x86 node **huntsman** (6 fast cores, 15 GB) joined; `kjob.py submit` now runs
    on every node by default (`--arch any`; `--arch arm64|amd64`, `--fast` to prefer huntsman for builds, `--scratch` for its 234 GB NVMe at
    `$SCRATCH`, deleted after 7 idle days). Series scripts work unchanged (portable Tools.dll); give the Tools/Scripting build `--fast`.
  - The laptop folder lagged main by two sessions; always read HANDOFF from origin/main (clone first) before briefing.
  - Destructible props change generated skirmish maps (props are on them): any sim change that touches props or land voids the balance baseline.
    Re-measure main with the change before judging variants (s31base pattern, 72 indices x 8 seeds).
  - A per-faction tier table ([faction.<id>.tier<N>]) replaces the whole tier for that faction: tests that edit a profile with `with { }` must
    apply the edit to `ForFaction` too (TierCapabilityTests.WaveProfile).
  - Build pods: request 1 CPU (not 3) while a series fills the cluster, or they stay Pending; a 1-CPU Tools build takes ~15 min.
  - Campaign sweep on the cluster: 135 indices in ~70 min at 8-22 parallel; sweeps on the 2-CPU box time out (helpers: walks + 6-seed scouting only).
  - Three server restarts this session: helpers resumed by themselves; nothing committed was lost. Check worktrees with git log/status after each.
- Session 30 lessons:
  - Screen a new key's smallest setting first: the "one Sparker allowed" mode behaved like the full key, so a cap (dial) was the useful form.
  - A rule-change screen of 9 variants x 4-6 rows (200 indices) takes ~2 h on 22 cluster cores; drop the series' parallelism to 16 before submitting
    builds (3-CPU build pods then schedule at once), and restore it after.
  - Merge helpers that add per-faction keys one at a time: FactionAiKeys/DifficultyLoader conflict every time (new ctor params at the end).
  - The sum28.py baseline is the 64-game main table (seeds 1-32); it is enough to reject variants on the screen.
- Session 29 lessons:
  - With push access attached (owner asked in chat), LFS uploads from the cloud worked: a branch with 4 new atlas PNGs pushed, and the LFS batch
    API listed all objects. Check with a "download" batch request before assuming the bundle route is needed.
  - Variant scripts: insert keys after the exact header line, never after a text match (the comment block names `[faction.expedition]`); check one
    variant's file before building nine.
  - Cluster: build pods (3 CPU) stay Pending behind a series of 1-CPU pods; drop the series' parallelism (kubectl patch job, via `cl.py run
    snowwhite`) before submitting builds, and set a waiting series to parallelism 0 until its build finishes (pods give up after 30 min).
  - 64-game screens of 16 variants took ~4 h of cluster time in three batches; screen only rows the change can touch.
  - Land finished non-balance work in its own PR while the balance series runs (PR #95 merged mid-session).
  - Sonnet helpers on the 2-CPU box with traces took 100-150 min each; ask for the default-identical proof and one trace per lever, then measure
    on the cluster yourself instead of waiting for the helper's variant step.
- Session 28 lessons:
  - A 64-game `ai match` row moved up to 7 points between seed sets on rows a variant could not touch: screen on 64, decide on 128 games (seeds 1-64,
    `CHUNKS=8`) against a 128-game main table. Rows untouched by a variant reproduce main game for game, a free consistency check.
  - The cloud box plays a 16-game row in ~2.5 min per core (one game ~10 s), about 5x a Pi core; use it for traces and quick checks.
  - Per-faction tier keys: `attack_ratio_pct` is inert for the Expedition (identical games); `attack_threshold`, `harvester_target`,
    `expansion_tick` work. Check a knob changes games before measuring it on every row.
  - Cluster: build first, then submit the series (pods wait 30 min for Tools.dll, then fail). Never run a script that `rm -rf`s
    /work/src/rts/<sha> while a series uses that tree; build Scripting.Tests in place (`tools/measure/cluster/README.md`).
  - The owner's laptop pushes LFS: for new sounds hand him one script that pulls main, picks on a branch and pushes (scratch\s28_sfx.ps1 pattern),
    then open the PR from the cloud.
- Session 27 lessons:
  - Cluster (D-253): the k3s-cluster-code repo has `claude/examples/rts27/` (build-any.sh, var27.sh + mkvar.py for rows x seed chunks,
    sweep27.sh, skins27.sh, summarize.py, varsum.py-style tallies). A 17-row x 64-game series takes ~65 min on 24-28 cores, the 45 x 12 campaign
    sweep ~25 min, a 9-row variant ~35-50 min when two variants share the cluster. Results are seed-for-seed identical to x64.
  - Run `Tools.dll` from the source tree (`cd /work/src/rts/<sha>`): it looks for data/rules above the working directory.
  - The Pi Blender (Debian 4.3) renders our bpy scripts through a runpy wrapper and matched Blender 5.3 to <1/255; a 128-sample skin took ~32 min
    on a shared Pi. Skin PNGs are not LFS, so they push from the cloud.
  - `harass_min_army` does nothing at tier 3 (the veteran table has no `harass` flag): check the tier's flags before tuning a per-faction knob.
  - A per-faction tier-3 army cap moves the target row a lot and the neighbours too; always measure all nine cross rows.
  - Windows CI: `UdpTransportTests.ByeEndsTheConnectionAndSilenceTimesOut` failed once on timing; a job rerun
    (`gh api -X POST repos/pwnzor11/RTS_2027/actions/jobs/<id>/rerun`) passed.
  - Merging PRs: the owner approves in chat ("send it" / picking "Land VE"), then `gh api -X PUT .../pulls/<n>/merge` after CI is green.
- Session 26 lessons:
  - Compute: CPU batch work (ai match series, campaign sweeps, test shards) can run on the owner's Pi cluster (7 Pi 4 nodes, 24
    worker cores, arm64, no GPU); use the `pi-cluster` skill. GPU work uses Anthropic-side resources or the laptop. Blender bpy
    has no Linux-arm64 wheel, so art renders stay on the laptop.
  - The owner's folder can lag main by a whole session (it was on art/s25 with the session 24 HANDOFF): read the handoff from
    origin/main, and check the laptop branch before giving him commands.
  - A laptop render that stops silently (sleep) shows as no new file in scratch for more than 35 min: check the output folder's
    newest file time, and give `powercfg /change standby-timeout-ac 0` with overnight runs.
- Session 25 lessons:
  - A balance claim from 16 seeds can flip when the AI seeds change: the Left Behind cap 4500 gave 47 % LB on the old seeds and 33 % on new ones.
    Use 32 seeds (64 games) for any row you keep a change on, and let the laptop series confirm.
  - Data files are copied into src/Tools/bin at build time: rebuild Tools after every data edit or the run measures the old data.
  - The building depth pass takes 10-35 min per building on the laptop (~20 h for 69), not minutes: plan it as an overnight run.
  - SoundBankTests caps one-shots at 270 000 bytes (about 6 s at 22 kHz mono); make_sfx.py --pick now trims, but check new long sounds.
  - Git in the device bridge is slow on this repo (git diff over the mount timed out at 120 s): use `git diff --cached --name-only` with a timeout.
  - The owner's laptop can push LFS; the cloud cannot. If the owner's commit fails, read `git diff --cached --name-only` and hand him the two lines.
- Session 24 lessons:
  - Laptop art renders read the scripts from the working tree for 20-60 min: tell the owner not to switch branches until a render finishes (a
    `git checkout main` mid-run swapped build_structure.py and the run failed halfway).
  - Placeholder art committed by an art helper into data/ goes live as soon as the loader code lands (flat-colour skin): keep placeholders in
    scratch or tests until the owner approves the real render.
  - A new state part changes every pinned hash even when empty (its name is hashed): plan the re-pin into the package.
  - The device bridge has numpy and Pillow (Python 3.10): audio/image screens can run there on the owner's files without staging 100+ files.
  - Cloud measurements of AI tweaks on 8 seeds are noisy; keep a change only if it is not worse, and let the laptop series confirm.
- Session 23 lessons:
  - add_repo can fail for a while ("service temporarily unavailable"); retry later in the session rather than switching to bundles at once.
    Without it, gh, LFS and pushes are all refused (the anonymous clone still works).
  - Files copied to the owner's PC with device_commit_files can be re-encoded by type (PNG LFS objects arrived with other hashes): ship LFS
    objects inside a .zip and check SHA-256 against the oid in push_art.ps1.
  - A server restart stops helpers mid-run and leaves their test processes orphaned and still holding the dotnet lock: kill them by pid
    (never pkill -f), then SendMessage each helper to resume. Two tier-5 helpers had made no commits despite the brief: say "commit each
    mission as soon as it is measured" and check `git status` in their worktrees after any restart.
  - Campaign tier-5 sweeps are steep and some missions are all-or-nothing (one unit flips 12/12 to 0/12): budget 6-seed scouting, then a
    12-seed confirm; when a band is unreachable, report the edge and leave the mission (owner accepted for lb_04/exp_02).
  - `ai match --ledger` has no kill attribution and counts transforms (tenders, rigs, Command Ship) as losses; read loss rates with that in mind.
- Session 20 lessons: (1) a single fixed-seed bot walk is not a balance measure: the campaign walks flip with the seed; judge a mission-tier
  by wins over 12 seeds (scratch/s20-ash-diag S20Sweep, ~2 h for all 45 on the 2-CPU box). (2) To split a rules effect, run the variants
  separately (ash damage only vs path cost only) before tuning. (3) Another session may land on main at any time: fetch main before opening
  or refreshing a PR, and pick D-/Q-numbers from origin/main at wrap-up, not from the session start. (4) A PR whose branch conflicts with
  main gets no CI run at all: check `mergeable` when CI doesn't start. (5) Tools tests that print to the console must be in the
  ToolsConsole collection, or they leak into another test's captured output on Windows CI.
- Lead: Opus for build sessions (the owner wants long unattended overnight builds); Sonnet for a pure review/question session. Helpers: Haiku default; Sonnet for sim core,
  determinism, serialisation, movement, combat, AI, scripting, and all Blender art.
- Repo clone: `/home/claude/rts_2027` (remote URL github.com/pwnzor11/RTS_2027.git; re-set it after a container restart); main has no branch protection, but land through a PR so Windows CI runs first
  (`gh pr create` fails — use `gh api repos/pwnzor11/RTS_2027/pulls -f title=… -f head=… -f base=main`); GitHub marks the
  PR merged when main fast-forwards to its head. Tag pushes are blocked: use `restore/<name>` branches.
- Setup: `apt-get update && apt-get install -y dotnet-sdk-10.0`; Blender `pip install bpy --break-system-packages`
  (got 5.2.3 on Python 3.13). Fonts come from apt (fonts-noto-core, OFL).
- Shallow clone: add fetch refspecs for other branches, e.g.
  `git config --add remote.origin.fetch '+refs/heads/wp/*:refs/remotes/origin/wp/*'` (and `integ-*`), so pushed
  branches get upstream tracking (the stop hook checks it).
- Helpers: worktrees under /home/claude/wt (create them yourself, from the integration branch that holds the contracts);
  wave rules in docs/process/HELPER_WAVE_RULES.md (copy docs/process/HELPER_WAVE_RULES.md to /home/claude/wt/WAVE1.md at the start of a session); build with
  `-m:1` (2 CPUs). Lead writes contracts first; helpers own files by function; expect merge-time fixes (late spawns in
  new tests, pinned hashes, golden regeneration, part-order tests).
- Art: renders take 5–20 min and share ONE lock — wrap each render in exactly one
  `flock -o /home/claude/wt/blender.lock …` (scripts must not self-lock). Art helpers run out of turns while queued: brief
  them to commit after each asset, and be ready to finish contact sheets yourself. Owner approves a pilot before any
  fan-out; send contact sheets with SendUserFile (tall sheets get scaled — split them to review).
- Windows CRLF: raw string literals in tests keep CRLF on a Windows checkout — normalise before searching for "\n".
  To check, convert all tests/*.cs to CRLF locally and run the suite.
- Sim rules: every new field hashed or allow-listed with a reason in HashCoverageTests; state parts are append-only
  (… players, economy, production, then extension parts such as "mission"); setup calls throw after the first Step (tests
  use Debug/Place commands); golden: `RTS_UPDATE_GOLDEN=1 dotnet test tests/Sim.Tests --filter Golden`.
- Never copy EA code (D-050). AI never runs inside Step (D-051). Lua only via ISimExtension (D-071). No floats or
  dictionary iteration in src/Sim. Scheduling tools stay forbidden for helpers (one created a stray task this session).
- Session 14 lessons:
  - 2 CPUs + 6-8 code helpers = 30-90 min dotnet lock waits. Brief helpers to run builds/tests in the FOREGROUND with a 600 000 ms timeout
    (or poll); a helper that backgrounds a queued test and ends its turn hands back "unverified" work. Stop such helpers and verify at merge.
  - Give every helper a private scratch dir (/tmp/claude-0/<pkg>/): one overwrote another's measurement script in the shared /tmp.
  - Overlapping UI packages: let ONE integration package merge them, build, and fix (s14-ui3 did ui1+ui2+obj+scale cleanly).
  - The game runs under xvfb for screenshots (recipe in the UI audit "Method"; box-drag needs xdotool mousemove_relative while held).
  - Stills: Cycles renders ~10 min each on one blender lock; three faction helpers in parallel worked (scenes built while others render).
  - Safety check refused: bulk deletes of CI artifacts, force-push, installing binaries on the owner's PC. Don't retry; hand the owner the command.
  - D-173 lesson: check what a diagnosis actually measured (here "13-15 structures" included the shaft and factories) before recommending a rule.
- Session 5 lessons:
  - Always `flock -o` (D-096) and install the dotnet wrapper first: /usr/local/bin/dotnet sets DOTNET_CLI_USE_MSBUILD_SERVER=0,
    MSBUILDDISABLENODEREUSE=1, UseSharedCompilation=false, then execs /usr/bin/dotnet. Without it VBCSCompiler holds the lock.
  - After cloning or creating worktrees run `git lfs pull && git lfs checkout` (pointer PNGs break Game tests).
  - Check what the remote integration branches already hold before launching a package (M3-16 was built twice).
  - Base every wave-2 worktree on the integration branch AFTER wave 1 merged (M4-12 missed the generator).
  - `git diff origin/main..` not `main..` (the local main ref goes stale after fast-forward landings).
- Session 4 lessons:
  - .NET builds also share ONE lock: wrap every dotnet build/test in `flock /home/claude/wt/dotnet.lock …` (D-090). Full
    suite ≈ 8 min; with 8 helpers queued, expect 30–60 min waits. Merge in batches and run one full suite per batch.
  - Helpers hand back while still queued, or re-send the same report many times: verify with git log, then TaskStop them.
  - Never `pkill -f` with a pattern that also matches your own shell command (killed the lead twice).
  - Shared enums are a collision point (two helpers both took FxKind = 3). Pre-assign enum values in briefs.
  - Art branches cannot be pushed from the cloud (LFS, D-084): keep them as `wp/art-*`, and at wrap-up rebuild
    `_art_push` in the owner's folder (LFS objects + bundle + push_art.ps1) through the device bridge.
  - Address SendMessage notes to the right helper id (a sprite note went to the audio helper).
  - The owner likes to see art: send every finished contact sheet for approval with a 1-line read per asset.
- Session 6 lessons:
  - Helpers get cut off mid-task while queued on the dotnet lock and hand back uncommitted work. Brief them to commit after
    each step, and when one reports "not finished", either resume it once with numbered steps or finish it yourself.
  - A helper that touches AI managers must also add any new long-lived field to that manager's Snapshot/Restore (M3-21 save
    format); the AI save→load determinism test catches it only for the paths it plays.
  - Mirror-symmetric AI placement stalemates identical AIs (D-104). Any symmetry fix in AI code needs the Directorate mirror
    test (ADirectorateMirrorAtVeteranDoesNotFreeze) and the pinned default-skirmish hash run before merging.
  - `ai match` now seeds each game of a pair separately; numbers from before session 6 are not comparable.
  - Remote branch deletion and folder requests outside RTS_2027 are blocked by the session safety checks: ask the owner.
  - The owner's RTS_2027 folder can be linked through the desktop app (device bridge); read-only checks of it are fine.
- Session 7 lessons:
  - Every helper test command gets a hang guard: `flock /home/claude/wt/dotnet.lock timeout 580 dotnet test ... --blame-hang-timeout 5m`.
    One deadlocked test (an awaited hand-made Task) held the shared lock and stalled the whole wave; kill only that testhost by pid.
  - Helpers that queue on the lock hand back early or reach for scheduling tools (three created "placeholder" reminders even
    though the brief forbade it). Brief them: run builds as FOREGROUND calls with a 600 000 ms tool timeout, rerun if still queued,
    never hand back while queued, no monitors, no scheduling, no mcp__remote-devices__* tools.
  - Haiku managed well-bounded packages (personas, chat panel) but gave up on the setup/save package while queued; Sonnet finished it.
  - After cloning, run `git lfs install` BEFORE `git lfs pull`; check `git lfs ls-files` shows `*` (not `-`) or the scene/menu proof
    tests fail with DivideByZero.
  - A worktree for measurements: `git worktree add --detach /home/claude/wt/measure <branch>`, Release build of src/Tools; each
    16-game `ai match` cell takes 1–2.5 min, the whole M4-16 set about 15 min with two lanes.
  - New sim state parts move the golden hashes, SkirmishDefaultHashTests and CombatTests' final hash; re-pin those only, and only
    after checking that the part sub-hashes other than the new one are unchanged.

- Session 8 lessons:
  - `gh pr checks` uses GraphQL, which is blocked: poll `gh api repos/pwnzor11/RTS_2027/commits/<sha>/check-runs` instead (a watcher
    on `gh pr checks` silently waits forever).
  - GitHub's GH008 hook rejects a push that references any LFS object it lacks anywhere in history; `lfs.allowincompletepush` does not
    help. Rewrite the offending commits (filter-branch index-filter to existing blobs) and check objects with the LFS batch API
    ("download", real sizes, at most 100 objects per request).
  - Building sprite manifests need a [house] block (HouseRamp.Standard) and fx anchors inside the frame, or Game tests fail.
  - `ai match` cells need 64 games (32 seeds x 2 sides): a 16-game cell resolves only about +-12 points; a 32-seed cell takes 8-10 min.
  - Background runs must use run_in_background directly; a trailing `&` inside the command is killed.
  - `ai match --trace N --game G` prints per-side army, income, tier and wave lines for one game (M4-16 diagnosis).
  - Fetch refspec for integration branches: `+refs/heads/integ-*:refs/remotes/origin/integ-*`.
  - When checking on a helper, cd back to your own worktree afterwards: the stop hook checks the current folder and will flag the
    helper's uncommitted work.
  - CommanderReplayAndSaveTests can time out under heavy CPU load (passed 3/3 alone).

- Session 9 lessons:
  - Direct pushes to main are refused by the session safety check even as a fast-forward: land with
    `gh api -X PUT repos/pwnzor11/RTS_2027/pulls/<n>/merge -f merge_method=merge -f sha=<head>` after CI is green.
  - Pattern: integration branch `integ-s<N>`, snapshot it to `land/s<N>-w<k>` for each PR so later merges do not move the PR head.
  - Test summary: `--logger "console;verbosity=normal"` and grep `Total tests|Passed:|Failed:|^\s+Failed ` ("Passed!" never prints).
  - bpy through the proxy stalls: download the wheel with a resumable `curl -C - --max-time 120` loop, then pip install the file.
  - xUnit analyzer errors (xUnit2031 etc.) fail the whole test project build: build tests before handing a package back.
  - The Monitor tool expires after 5 minutes: poll CI with a run_in_background loop instead.
  - CommanderReplayAndSaveTests asserted one request; a slow Windows runner sends two. Now `Assert.NotEmpty` (fixed).
  - The shell's cwd drifts into worktrees after `cd`: put `cd <dir> &&` at the start of every command.
  - A/B a failing AI test with one data flag (supply on/off) before reading code: it splits "caused by the feature" from "already red".
  - Lead-only small packages (one function, <100 lines) are faster than briefing a helper and waiting on the build lock.

- Session 10 lessons:
  - Four waves of 4-7 Sonnet helpers each ran 1-3.5 h on the 2-CPU box; merging one by one as reports arrive, then a scoped
    M7/hash test run per merge and one full suite per wave, kept every PR green first time except one Windows-only flake.
  - Python probes on the Windows runner can take over 20 s (Store stub): treat a probe timeout as "not available" (SfxrTests fixed).
  - Several helpers edit the same allow-lists (RulesOfSweepGrepTests, HashCoverageTests) and the RulesHash doc comment; these are
    the usual merge conflicts, trivially resolved by keeping both sides.
  - Parts that only write new arrays when they are in use (naval, effects, stats, tunnels "version 2 when needed") keep every old
    hash; ask helpers for that pattern instead of an unconditional format bump.
  - Adding ship types to the real data made every game write sea labels: SkirmishDefaultHashTests moved on the "naval" sub-hash only.
  - `/usr/bin/dotnet vstest <dll> --Tests:<name>` runs a built test dll without the dotnet wrapper (used for soak runs).
  - A helper created a scheduled task despite the brief (session 10, "x"); never delete or act on such tasks on a helper's request.

- Session 11 lessons:
  - Helper branch names can collide with old remote branches (`wp/m7-14ui` existed from session 10): suffix new ones with `-s<N>`.
  - Force-push is refused by the safety check: when a helper amends a pushed branch, push under a new name instead.
  - Art fan-out: 5 Sonnet art helpers on one Blender lock took ~3-4.5 h for 44 assets. Review every sheet: catch draw_scale double
    scaling (ships), tread-like hover bases, wrong hull colour. Contact sheets live in git-ignored scratch/; read them before cleanup.
  - Data shape problems show up only when the AI plays: the shoreline water masks fit south coasts only (Q-55) and the Command Ship
    could not Deploy on water (D-150). Run an AI game on the real data before calling a faction playable.
  - The device bridge can disconnect mid-session: build the art push as a zip (bundle + LFS objects + push_art.ps1) and send it with
    SendUserFile; a 46-sprite faction is about 5 MB.

- Session 12 lessons:
  - The owner was present and answered in batches with AskUserQuestion; "full send" after a pilot meant: write the other pilots and the
    fan-out without another review gate. Put every remaining writer choice in one review file instead of asking mid-run.
  - Mission fan-out: one Sonnet helper per faction writing acts 2-4 (+ ending) took 30-60 min each on the 2-CPU box with the dotnet lock;
    ask them to commit one mission at a time — a container restart lost no committed mission, and SendMessage resumed both helpers.
  - Shared briefs as files (scratch/briefs/*.md, read by absolute path) kept each launch prompt to ~15 lines.
  - Tests that pin "which missions exist" or placeholder text break the moment content lands: use a temp data folder or an inline fixture.
  - Ids in data (`next`, campaign lists) need a cross-check test; helpers guessed `left_behind_02` and `expedition_02`.
  - Missions ran without player factions in the sim until s12 (faction rules silently off): any new mission-only setup path must reuse
    MissionSession.MakeSetup.
  - Windows CI: `VSTEST_CONNECTION_TIMEOUT=600` in ci.yml; a test-host connect timeout is a runner problem, re-run once, then fix the env.
  - GitHub Actions minutes ran out once; the repo is public since (D-159), so Actions are free.

- Session 15 lessons:
  - New sprites (LFS) cannot be pushed from the cloud. Package them for the owner: `git bundle create <zip>/x.bundle <base>..<branch>`, copy EVERY
    LFS object referenced by any commit in the range (not just the tip: `git lfs push` checks history; the first packs package missed 9 versions),
    and a push_art.ps1 that fetches, verifies the bundle, copies objects into .git/lfs/objects, `git lfs push origin <branch>`, `git push`.
    Ship it as a .zip (the owner's browser refuses a bare .ps1). Keep art commits off branches you need to push until the owner has pushed.
  - Licences: OpenGameArt and kenney.nl block automated reads. Read the packs' own licence files; get verbatim licence texts from GitHub
    (raw.githubusercontent.com/spdx/license-list-data/main/text/<id>.txt works). CC-BY-SA 3.0 is not GPL-compatible: keep such art as separate,
    separately licensed data files (0 A.D. props) and never in GPL sprite sheets; avoid it in tooling that mixes (Warzone page-6/17/30, wheels).
  - Enum-count pins live in several contract tests (M4, M6, M7): grep for `Enum.GetValues<CommandKind>().Length` when adding a command.
  - Windows CI catches file-lock bugs Linux misses (a test deleted a replay file still open): close sessions (SaveReplay) before deleting.
  - A container restart lost nothing committed; helper reports can be recovered from the end of their task transcripts.
  - When a re-rendered asset and a renderer-side scale option overlap, give the asset a manifest flag (figure_scale_permille) so they do not stack.
  - The owner answered every question within minutes; batching 2-4 questions with AskUserQuestion and sending contact sheets first worked well.
- Session 13 lessons:
  - The auto-mode safety check refused `gh api -X PUT …/pulls/<n>/merge` ("merge without review"). Do not look for a way around it;
    leave PRs open, stack later work on the same integration branch, and ask the owner to merge (or to allow merges) at the end.
  - Squashing a helper branch with `git reset --soft <integ>` after the integration branch moved on silently REVERTS everything merged
    since the helper's base. Squash against the merge base (`git reset --soft $(git merge-base …)`) or apply the helper's diff
    (`git diff <base> <tip> | git apply --index`) onto the integration branch, and check `git diff --stat` before landing.
  - The repo moved to github.com/pwnzor11/RTS_2027 ("This repository moved" on push): `git remote set-url origin` fixed it.
  - LFS pulls in the cloud need the repo added to the session (add_repo, push access); then all LFS files check out.
  - The device bridge VM has no git-lfs: pulls in the owner's folder fail; the owner pulls on Windows.
  - Helpers on the shared dotnet lock could not finish Sim.Tests/Tools.Tests inside 580 s: the lead runs one full suite per batch.
  - The solution file is `RTS_2027.slnx` (dotnet test RTS_2027.slnx …).
- Session 17 lessons:
  - A container restart stops running helpers; nothing committed or on disk is lost and SendMessage to the same helper resumes it with its context.
    Brief every helper to commit after each numbered step, and push helper branches when they have commits (the stop hook asks for it).
  - The owner's laptop runs a 16-row, 64-game series in about 90 minutes; the 2-CPU cloud box needs that long for one row. Put measurement scripts
    in tools/measure, hand them over, and read the results from his folder through the device bridge (logs are ASCII with CRLF; PowerShell `*>`
    logs such as the re-render logs are UTF-16).
  - Measure the baseline before setting a blocker: layout 1 fails the 60 % start limit itself on two biomes, and the plain 10-minute income mean
    tracks game length, not income.
  - A stall with "0 structures, 0 units" for BOTH sides is a setup problem (start units), not an AI problem: check SkirmishStart before the tactics code.
  - Art drift: when a full re-render differs from committed sheets, compare render-commit times with later edits to shared material tables
    (`lb_common.py` MATERIALS) before touching a script; here the sprites were stale, not the scripts.
  - To read LFS files from a local-only branch in the owner's folder (no git-lfs in the bridge VM): `git cat-file -p <branch>:<path>` gives the
    pointer, the object is at `.git/lfs/objects/<aa>/<bb>/<oid>`. In the cloud, `git lfs fetch origin refs/remotes/origin/<branch>` (the short name fails).
  - A helper made one stray device-bridge call despite the brief (no effect). Keep "no mcp__remote-devices__* tools" in every brief.

- Session 19 lessons:
  - Local merge batches must include Scripting.Tests (or the campaign bot classes) whenever a change touches movement, combat, economy or
    infantry rules: the campaign bots swing on small changes and Windows CI found ashfall and path regressions only after the PR.
  - Bisect campaign regressions with temporary rules switches (e.g. `ashfall_damage = 0` locally) and per-merge-commit checkouts before reading
    code; it split "ash" from "path" from "data" quickly. Never commit the local switch.
  - A full Scripting.Tests run plus Sim/AI/Game takes well over an hour on the loaded box; the background run limit (2 h) killed one batch.
    Let Windows CI verify slow projects in parallel by pushing a land/ branch early.
  - The safety check refuses force-pushes even to our own land/ branches: open a new land branch and PR instead (#66 superseded #62).
  - Helpers' "assumed" picks that touch player-visible behaviour go to OPEN_QUESTIONS as "assumed — confirm" (Q-72).
  - The owner answers phone pushes quickly: batch 1-5 picks per push with a recommendation each; PushNotification works.
- Session 22 lessons:
  - add_repo (push access) is refused by the auto-mode check unless the owner asks for it in chat; once added, git push and LFS *downloads* work,
    but LFS *uploads* from the cloud are still refused (S3 "Forbidden"). Keep new LFS files off branches the lead must push, or hand the owner a
    package: bundle + a push script. Do NOT send LFS objects through SendUserFile/device_commit_files: WAVs arrived re-encoded (checksums broke).
    Rebuild objects on the owner's side from his own working files (git show HEAD:<path> | sha256) instead.
  - git commands in the device bridge leave .lock files (no delete permission): use `git show`/`git cat-file`, not status/diff/checkout, and move
    any stray `.git/index.lock` aside.
  - The owner's Windows has Python 3.13 (art venv) but no 3.11; laptop setups now try py -3.11, then uv, then winget's Python 3.11.
  - Laptop scripts written blind need one quick dry run by the owner before a long run: missing packages (torchsde) only show at model load.
  - Never put credentials in docs/briefs; when the owner pastes one, don't use or store it and ask him to rotate it.
  - Art: the owner judges heroes by eye; a quick 24-sample cloud preview sheet per round, then full renders on the laptop, worked well.
- Session 21 lessons:
  - Check GitHub for a parallel session before starting (`git ls-remote origin 'refs/heads/*s<N>*'`): a whole session 20 had run overnight;
    build on its PR and handoff instead of redoing it.
  - Owner policy (D-220): never spend time keeping development saves or replays loadable; re-pin hashes freely.
  - The owner wants campaign sweeps and skirmish series on his laptop, not the cloud box: write a tools/measure script, he runs it and says
    "done"; read the summary through the linked folder. The laptop runs all 45 campaign mission-tiers in ~21 min (cloud: 1-2 h per pass) and
    gives seed-for-seed identical results.
  - Find root causes with the per-unit trace before scaling numbers: three missions lost to the same weapon-range gap, fixed once in the sim.
  - A sim rule change voids every earlier sweep: re-sweep all missions, keep only retunes that are still needed (dir_04's round-1 change was not).
  - Brief retune helpers: don't shorten visible timers, keep each tier's identity, set TMPDIR for the trace probe; tier-5 cliffs are common
    (several missions are all-or-nothing for the bot).
  - Don't merge into the folder a running test reads its data from; don't wrap long test runs in the shared build lock (helpers queue behind it).
  - The proxy blocks Actions caches and numeric-id artifact pagination; page artifacts with &page=N.
- Session 18 lessons:
  - Helpers cannot write report .md files (the harness refuses "report files from subagents"): brief them to hand back the full text, and save
    it from the task transcript (the lead used a small script that pulls the SubagentHandback text out of the .output JSONL).
  - The 5-hour usage limit cut off 5 helpers twice; SendMessage after the reset resumed every one of them with its context and commits.
  - "Verify only" packages that pin today's behaviour (`Audit_` test + skipped `Desired_` twin) let a whole audit land without moving a hash,
    and give the fix session ready-made acceptance tests.
  - A full-suite run in the background on a box loaded with live-play helpers hit the 1-hour background limit: run it per project
    (`dotnet test tests/<P>.Tests --no-build`) after the helpers finish, with run_in_background and a 2-hour timeout.
  - Live play under Xvfb found the worst UI bug (structure-lane soft-lock); code reading alone had missed it. Keep one live-play reviewer.
  - Wave-1 static estimates were wrong both ways when measured (path stall 3 s estimated, 22.6 s measured; hash cost 9-15 ms estimated, 3-5 ms
    measured; Mountains Expedition "3 of 8 seeds" was 80-98 % of maps). Always measure before ranking.
