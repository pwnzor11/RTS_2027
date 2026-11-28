# Open questions
🔴 blocks work · 🟡 needed soon · ⚪ later. Code waiting on one carries `// PENDING Q-xx`.
Context and trade-offs for every question are in `docs/research/00_SYNTHESIS.md` §2 and §4 and the
file cited after each question. Recommendations are the lead's, from the research.

## Open
- 🟡 Q-100 · lb_06 "Salvage Rights": haulers are ordinary harvesters, so they fill the Yard from the ash fields beside the camp and the
  "Bank 1200 ash" goal is met without the train (the bot wins in about 45 s). Options: (a) keep it as an easy act-1 breather; (b) **move the camp
  and Yard to ash-free ground (map/mission edit) so only wrecks pay**; (c) a new salvage counter in the sim/Lua (code package). Recommendation: (b).
- ⚪ Q-101 · Per-map balance below the 35-65 aim (D-344): Glass Coast Directorate v Expedition 30.0, Ash Plains Authority v Directorate 32.0.
  Options: (a) **accept (D-305 is met)**; (b) take ash resist 825 (Ash about 45, but Glass and Rust Belt Authority v Directorate about 68); (c) a
  code package for per-biome unit or AI keys, then screen again. Recommendation: (a) now, (c) when per-map balance is next on the plan.

## Answered
- ✅ Q-94 · Campaign outline Q5: upgrades are not mission-gated today (CanResearch ignores UnlockFilter). (a) leave it, (b) **gate them all as mission rewards (small code package)**, (c) gate tier 3 only. docs/design/22_campaign_expansion.md §1. **Answered (D-341):** as recommended.
- ✅ Q-95 · Outline Q6: maps for the 32 new missions. (a) reuse the 3 hand maps, (b) **add 3 new frozen-and-edited maps (rustbelt_mill, tern_point, annex_foothills)**, (c) one map per mission. **Answered (D-341):** as recommended.
- ✅ Q-96 · Outline Q7: C-03 choice points. (a) none, (b) **reserve slots at missions 6 and 10 per faction, written as dilemmas**, (c) build one now. **Answered (D-341):** as recommended.
- ✅ Q-97 · Outline Q8: when the Left Behind and Expedition campaigns open. (a) **keep the current ids (no data change)**, (b) move to the new act ends, (c) open all. **Answered (D-341):** as recommended.
- ✅ Q-98 · Outline Q9: leader Archive secrets as story beats. (a) open, (b) **hinted on screen, the Archive entry confirms**, (c) Archive only. **Answered (D-341):** as recommended.
- ✅ Q-99 · Lore: Wrenfield is "Hester / her brother" in docs/lore/leaders.md but "his brother" in left_behind.md. Which is right? **Answered (D-342):** Hester, "her brother"; left_behind.md fixed.
- Q-93 · 2026-11-26 · Owner (s46): tune with unit numbers on paired screens, decide on 512 games → D-330 (gate met; numbers assumed — confirm).
- Q-91, Q-92 · 2026-11-25 · Owner (s46): take the recommendations → D-328 (Alt-modifier bindings, Laptop keys preset), D-329 (lb_02 line re-voiced).
- Q-89, Q-90 · 2026-11-25 · Owner (s45): take the recommendations → D-324; screened → D-325 (Scrap Plate stays, the Repair Hover list fix lands, M-22 no effect).
- Q-88 · 2026-11-24 · Owner (s43): (a) finish line met; freeze balance numbers → D-310.
- Q-87 · 2026-11-23 · Owner: (c) judge the six pairings over the map pool now, the 24 per-map rows later once map-specific causes are fixed → D-305.
- Q-73 · 2026-11-21 · Owner (s39): par = the bot's tier-3 median win x 2.5 (the s37 table), none on timer missions → D-289.
- Q-86 · 2026-11-17 · Owner: love the draft; keep 144 pages; keep the spoiler handling and the cover; accept the design 13/14 pages as written → D-271.
- Q-85 · 2026-11-16 · Owner (b): dress rustbelt_m2 fully and retune the tier-5 bots → D-262.
- Q-84 · 2026-11-13 · Owner: name Grapeshot; art pilot now → D-236.
- Q-79..Q-83 · 2026-11-13 · Owner: Q-79 (a) diagnose late-game income/rebuild first; Q-80 (c) a new tier-2 ground anti-infantry unit, shape:
  hover canister skiff; Q-81 (b) booklet waits until balance settles; Q-82 (b) raise tier 5 now, band 4-9 of 12; Q-83 (a) instructor on Kokoro by
  audition, unit barks after SFX → D-234.
- Q-78 · 2026-11-12 · Owner: all four assumed items confirmed (Kokoro CC0, mop-up constants for now, Harpoon anti-air, horn/sonar) → D-228.
- Q-77 · 2026-11-12 · Owner: take all recommendations (docs/balance/s22_balance_options.md); the A/B ruled out D-219 → D-223.
  Q-76 · 2026-11-12 · Owner (a), "go ahead and fix this" → D-225.
- Q-75 · 2026-11-12 · Owner: targets over 12 seeds with ash on are **tier 1 >= 10/12, tier 3 >= 8/12, tier 5 >= 3/12** (harder than the
  recommendation) → D-222. Q-74 · 2026-11-12 · Owner (a): chat block at the next replay format bump → D-222.
- Session 21 · 2026-11-12 · Owner: default stance engages hostile units in sight and returns to its post → D-219; development saves and
  replays need not outlive the next session → D-220; tier-5 fixed-seed walks only need a clean run → D-221.
- Q-71 · 2026-11-12 · Owner (a): accept the winnable auth_01 tier 5; the S18 MIS-03 expectation is dropped → D-217.
- Q-72 · 2026-11-12 · Owner: keep the ash step-off rules (3-cell step-off, ash-stealth infantry stay, foot penalty 4) → D-217.
- Owner confirmations · 2026-11-12 · Confirmed the remaining "assumed — confirm" items after D-153's blanket confirmation: the revised Left
  Behind lore text (D-155), D-156 (floating docks, aircraft ammo), D-157 (1) (lb_yard storage 3000), D-165 (act-break stills, tier-4 AI levers),
  and the Archive upgrade field notes marked "DRAFT (lead, session 12)" (they lose the DRAFT mark). DECISIONS entry at the next wrap-up.
- Q-M9-a..o · 2026-11-12 · M9 LAN design round (docs/design/20_m9_lan_plan.md §9). Owner: UDP with own reliability (a, not the rec);
  host-sequenced turn bundles (b); host picks 2/3/4-tick delay in the lobby (c, not the rec); AI takes over a dropped slot (d); desync pauses,
  host picks Resync/Continue/End (e); host runs all AI incl. LLM on its key (f); chat outside the sim, kept in replays (g); anyone pauses,
  3 s resume, 1x (h); no LAN saves ever (i, not the rec); clients may save and continue vs AI when the host leaves (j, not the rec); host
  sends custom maps (k); up to 2 spectators (l); no reconnect ever (m, not the rec); no co-op campaign in M9 (n); §8 assumptions confirmed (o).
  DECISIONS entries are numbered at the next wrap-up, after session 19's.
- Q-70 · 2026-11-10 · Owner (a): layout 2 money per field cell cut on Rust Belt (-9.8 % measured) and Glass Coast (-12.8 %, density floor); decide on Glass Coast fields after the after-series → D-208.
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
