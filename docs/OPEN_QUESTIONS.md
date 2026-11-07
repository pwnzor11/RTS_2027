# Open questions
🔴 blocks work · 🟡 needed soon · ⚪ later. Code waiting on one carries `// PENDING Q-xx`.
Context and trade-offs for every question are in `docs/research/00_SYNTHESIS.md` §2 and §4 and the
file cited after each question. Recommendations are the lead's, from the research.

## Open
- 🟡 **Q-63 · Directorate games that never end (M4-16).** In Directorate mirror games most games are draws at 30 000 ticks
  (s14: tier 5 v tier 1 = 6 / 7 / 19 in 32 games; s13 at 64 games: 17 / 8 / 39). The loser is usually down to 1-2 units with no income but
  13-15 structures, and stray hatches count as a base (any non-wall building keeps a player alive). An early mop-up wave did not help
  (docs/balance/s14_mopup.md). Options: **a player with no production structure, no harvester and no army for 2 minutes is defeated**
  (classic "build nothing, lose") / hatches and other minor structures don't count toward survival / AI-only fix: bigger, later mop-up that
  targets outer structures first / leave it. Recommendation: the first option (rule change, applies to skirmish; campaign missions set their own rules).

## Answered
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
