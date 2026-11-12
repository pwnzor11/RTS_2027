# Open questions
🔴 blocks work · 🟡 needed soon · ⚪ later. Code waiting on one carries `// PENDING Q-xx`.
Context and trade-offs for every question are in `docs/research/00_SYNTHESIS.md` §2 and §4 and the
file cited after each question. Recommendations are the lead's, from the research.

## Open
- 🟡 **Q-70 · 2026-11-10 · Layout 2 income after v6.** Moving the main fields from about 4 to about 9 cells (D-199) moved income only a few percent:
  the AI parks its refinery near the yard. On 16 sides per biome v6 is within 10 % of layout 1 at 3 minutes everywhere, but +13 % (rust_belt) and
  +28 % (glass_coast) at 5 minutes. Decide after the owner's 64-game run (`tools/measure/run_s17_layout2.ps1`). Options: **(a) wait for the 64-game
  numbers, then cut money per field cell on the biomes still over (recommended)**; (b) relax the layout check "100 field cells within 30 path cells"
  for layout 2 so the main field can be halved (the only change that matched layout 1 in the probe); (c) accept the richer early economy on layout 2.

### M9 LAN design round (2026-11-12, docs/design/20_m9_lan_plan.md; options and trade-offs are in the § cited). Needed before M9 starts, after M14.
- ⚪ **Q-M9-a · Transport (§4.1).** **(1) TCP for lobby and match, UDP only for discovery (recommended)**; (2) UDP with our own reliability; (3) LiteNetLib.
- ⚪ **Q-M9-b · Turn model (§4.2).** **(1) host-sequenced turn bundles: host stamps ExecTick and broadcasts one bundle per tick (recommended)**;
  (2) peer-equal lockstep relayed through the host (C&C style).
- ⚪ **Q-M9-c · Command delay (§4.3).** **(1) fixed 3 ticks (150 ms), a settings value, ping shown in the lobby (recommended)**; (2) fixed 2 ticks;
  (3) adaptive from round-trip; (4) host picks 2/3/4 in the lobby.
- ⚪ **Q-M9-d · Slow or dropped player (§4.4).** Wait overlay at 1 s, host may drop at 10 s, auto-drop at 60 s; then **(1) an AI takes over the slot
  (recommended)**; (2) slot goes idle; (3) player defeated.
- ⚪ **Q-M9-e · Desync response (§4.5).** Reports written on every peer, then **(1) pause with Resync from host / Continue / End (recommended)**;
  (2) stop the match; (3) warn and play on.
- ⚪ **Q-M9-f · AI and LLM slots (§4.6).** **(1) host runs every AI slot, LLM commanders on the host's key, LLM slots only if the host has a key
  (recommended)**; (2) each AI runs on the peer that added it.
- ⚪ **Q-M9-g · Player chat (§4.7).** **(1) outside the sim, All/Team, relayed by the host (recommended)**; (2) chat as a sim command.
  Sub-question: keep chat in replays? **yes (recommended)** / no.
- ⚪ **Q-M9-h · Pause and speed (§4.8).** **(1) anyone pauses, anyone resumes after a 3 s countdown, speed fixed 1x (recommended)**; (2) host only;
  (3) no pause.
- ⚪ **Q-M9-i · Saving LAN games (§4.9).** **(1) none in the first build, host-only save and resume in phase 3 (recommended)**; (2) host save in the
  first build; (3) never.
- ⚪ **Q-M9-j · Host leaving (§4.10).** **(1) "Host left", replay kept, back to menu (recommended)**; (2) clients may also save and continue vs AI.
- ⚪ **Q-M9-k · Custom maps (§4.11).** **(1) host sends the map file in the lobby, hash checked (recommended)**; (2) players copy it by hand.
- ⚪ **Q-M9-l · Spectators (§4.12).** **(1) up to 2 from the lobby, nobody waits on them (recommended)**; (2) none.
- ⚪ **Q-M9-m · Reconnect (§4.13).** **(1) not in the first build, in phase 3 via the resync save (recommended)**; (2) never.
- ⚪ **Q-M9-n · Co-op campaign (§4.14).** **(1) not in M9, a possible later milestone (recommended)**; (2) plan it into M9.
- ⚪ **Q-M9-o · Assumptions batch (§8).** Map hack accepted, no lobby password, Windows firewall prompt + help page, Windows user name as default
  player name, skirmish options set by the host, loopback → two windows → desktop + laptop testing, fixed changeable ports. **Confirm all
  (recommended)** / change some.

## Answered
- Q-68 · 2026-11-09 · Owner: (a) half-turn skeleton for Ash Plains, same crossing pair both sides → D-198.
- Q-69 · 2026-11-09 · Owner: (a) move main/natural fields further from the refinery spot, fewer patches near the main → D-199.
- Q-67 · 2026-11-08 · Owner: band 50-65 % (D-191); hold 18 landed, tier-5 ladder follow-up → D-194.
- Q-64 · 2026-11-07 · Owner: AI siege behaviour → D-174.
- Q-65 · 2026-11-07 · Owner: re-measure (D-176), then AI-only fixes → D-182.
- Q-66 · 2026-11-07 · Owner: tune them → D-175.
- Q-63 · 2026-11-07 · Owner: rule 1, remaining units don't count → D-173.
- Q-60, Q-61, Q-62 · 2026-11-07 · Owner: Q-60 all three recommendations (ash contaminate 40 -> 20, no SAM rule change + check the
  low-power warning, Conscript health 40 -> 50) → D-166; Q-61 make it a per-faction setting in difficulty.toml → D-167; Q-62 tune the
  mission tiers → D-168. Q-59 · owner picked C (mix: game look + thin outlines) "but add more detail", from a 3-style contact sheet → D-169.
- Q-56, Q-57, Q-58 · 2026-11-06 · Owner played the campaign ("everything looked great"): Strand decides confirmed, all ten writer choices kept,
  atomic battery unlock kept, endings/briefings/radio lines signed off → D-164. Bot-found balance issues: table first (D-152).
- Q-55, Q-49 (Q-A..Q-I) · 2026-11-06 · Docks wholly on water → D-153; M8 story answers → D-154, Left Behind lore → D-155.
- Q-52, Q-53, Q-54 · 2026-11-05 · Owner took the recommendations → D-148 (LB income: re-measure after M7, table first; veterancy
  thresholds unchanged; supply spread re-measured with 64 games per pair in session 11).
- Q-43..Q-48, Q-50, Q-51 · 2026-11-05 · Owner: "all assumptions look great" → D-134 (keep 2 500; confirm session-9 numbers;
  keep LB vehicle scale; info panel with VET-6; keep promotion cue; keep Rotors; approve lore drafts; supply on after the AI fix).
- Q-41, Q-42 · 2026-11-05 · Lore banners cleared → D-119; foam stays 3×3 → D-120. Q-43 → measure only (D-121).
- 07_expedition.md §10 (M7 items 1–10) · 2026-11-05 · owner answers → D-124..D-127.
- Q-40 · 2026-11-04 · Owner: "look up the models" → Haiku 5.5 default, Sonnet 5.5 strong, effort low → D-108.
- Q-34, Q-36 · 2026-11-04 · Session 4 and M4 assumed values confirmed → D-099.
- Q-35 · 2026-11-04 · (a) one radiation-green ramp in PAL_DIR → D-099.
- Q-37 · 2026-11-04 · Faction.Neutral, option A → D-100.
- Q-38 · 2026-11-04 · Lore approved (plus leaders.md) → D-101.
- Q-39 · 2026-11-04 · M6 answers, all recommendations → D-102.
- Q-M5-a..e · 2026-11-04 · M5 plan answers → D-103.
- Q-29 · 2026-11-03 · Tunnel spec → D-074..D-076. Q-31 · 2026-11-03 · Audio sourcing → D-080.
- Q-27, Q-28, Q-30, Q-32, Q-33 · 2026-11-03 → D-058..D-061.
- Q-26 · 2026-11-02 · Tank sprite in the debug view → D-041.
- Q-24 · 2026-11-02 · Art pilot: arm B at 64 px → D-039.
- Q-25 · 2026-11-02 · D-033 confirmed; setting draft accepted → D-036.
- Round 3 (Q-03b, Q-06, Q-07, Q-08, Q-23) · 2026-11-02 → D-027..D-034.
- Round 2 (Q-04, Q-05, Q-19..Q-22, Q-03 structure) · 2026-11-02 → D-020..D-026.
- Round 1 (Q-01, Q-02, Q-09..Q-18) · 2026-11-02 → D-009..D-019. Sub-choice MonoGame vs FNA left to the lead (see design/01_architecture.md).
- Q-00 · (session 1 interview) Licensing, platform, look, mode priorities, doc location → D-001..D-008.
