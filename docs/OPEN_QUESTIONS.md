# Open questions
🔴 blocks work · 🟡 needed soon · ⚪ later. Code waiting on one carries `// PENDING Q-xx`.
Context and trade-offs for every question are in `docs/research/00_SYNTHESIS.md` §2 and §4 and the
file cited after each question. Recommendations are the lead's, from the research.

## Open
From the 2026-11-03 adversarial review (docs/reviews/2026-11-03_adversarial). Needed before M2 planning
unless marked otherwise.


(none blocking)

- ⚪ Q-34 Review the session 4 assumed values (D-089): radiation damage, Directorate start kit, engineer capture threshold,
  aura stacking, Sunrise numbers and fog, About page placement, sfxr licence. Recommendation: confirm in the M3-14 balance pass.
- 🟡 Q-35 Rad mortar green: the Directorate palette (PAL_DIR) has no green, so the rad mortar's radiation glow rendered
  rust-orange. Options: (a) add one radiation-green ramp to PAL_DIR, (b) keep the palette pure and show radiation only as
  the ground effect. Recommendation: (a), a single green ramp used only for radiation.
- 🟡 Q-36 Confirm the M4 assumptions (D-095, docs/design/08_m4_plan.md §9 Q-M4-a..j) and the M4-10 theater colours.
  Recommendation: play a game on each biome, then confirm or list changes.
- 🟡 Q-37 Neutral faction: `neu_*` types (ruins) default to the Authority faction enum. Options: (a) add Faction.Neutral
  (append-only enum, rules hash bump), (b) keep Authority and filter by id prefix. Recommendation: (a), in M4-16.
- 🟡 Q-38 Lore approvals (D-086): [DRAFT] Archive entries for the M4 types, docs/lore/left_behind.md and
  docs/lore/expedition.md. Recommendation: read and approve or mark up; nothing ships as canon until approved.
- ⚪ Q-39 M6/M7 design questions listed at the end of docs/design/06_left_behind.md and 07_expedition.md.
  Recommendation: answer before the M6 contracts session.

## Answered
- Q-29 · 2026-11-03 · Tunnel spec → D-074..D-076. Q-31 · 2026-11-03 · Audio sourcing → D-080.
- Q-27, Q-28, Q-30, Q-32, Q-33 · 2026-11-03 → D-058..D-061.
- Q-26 · 2026-11-02 · Tank sprite in the debug view → D-041.
- Q-24 · 2026-11-02 · Art pilot: arm B at 64 px → D-039.
- Q-25 · 2026-11-02 · D-033 confirmed; setting draft accepted → D-036.
- Round 3 (Q-03b, Q-06, Q-07, Q-08, Q-23) · 2026-11-02 → D-027..D-034.
- Round 2 (Q-04, Q-05, Q-19..Q-22, Q-03 structure) · 2026-11-02 → D-020..D-026.
- Round 1 (Q-01, Q-02, Q-09..Q-18) · 2026-11-02 → D-009..D-019. Sub-choice MonoGame vs FNA left to the lead (see design/01_architecture.md).
- Q-00 · (session 1 interview) Licensing, platform, look, mode priorities, doc location → D-001..D-008.
