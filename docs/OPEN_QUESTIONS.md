# Open questions
🔴 blocks work · 🟡 needed soon · ⚪ later. Code waiting on one carries `// PENDING Q-xx`.
Context and trade-offs for every question are in `docs/research/00_SYNTHESIS.md` §2 and §4 and the
file cited after each question. Recommendations are the lead's, from the research.

## Open

- 🟡 Q-43 Tier-3 cut 2 500 → 2 000: measured (docs/balance/m4_16_round4.md); every cell moves only within noise. **Keep 2 500**
  (recommended) / cut to 2 000. Data stays at 2 500 until the owner picks.
- 🟡 Q-44 Session-9 numbers, all "assumed — confirm": supply, veterancy and Left Behind tables in docs/balance/s9_tables.md
  (D-125, D-127, D-130, D-132). **Confirm all** (recommended) / mark rows to change.
- 🟡 Q-45 Left Behind vehicle scale: drawn about 1.5× the Directorate's (to match the rig). **Keep and judge in game** (recommended)
  / shrink to Directorate scale (re-render, about 2 hours of art time).
- ⚪ Q-46 Veterancy own-unit info panel (rank name, kills) has no screen yet: **add it with VET-6** (recommended) / skip; pips only.
- ⚪ Q-47 Promotion sound reuses the unit_ready cue, quieter: **keep until a real cue is sourced** (recommended) / source one now (D-080).
- ⚪ Q-48 Left Behind AI uses Rotors instead of Kites (Kites lost ~20 points): **keep** (recommended) / make Kites usable first.
- ⚪ Q-49 M8 story questions Q-A..Q-I in docs/design/12_m8_plan.md: answer before M8 starts (not needed for M7).
- ⚪ Q-50 Lore, Archive entries and personas written this session (27 Left Behind, 40 Expedition, 3 supply buildings, 7 personas)
  are drafts: **approve as written** (recommended) / list changes. Archive pages for leaders/authority/directorate unlock at
  `campaign:TBD`.

## Answered
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
