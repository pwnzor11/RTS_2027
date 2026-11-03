# Open questions
🔴 blocks work · 🟡 needed soon · ⚪ later. Code waiting on one carries `// PENDING Q-xx`.
Context and trade-offs for every question are in `docs/research/00_SYNTHESIS.md` §2 and §4 and the
file cited after each question. Recommendations are the lead's, from the research.

## Open
From the 2026-11-03 adversarial review (docs/reviews/2026-11-03_adversarial). Needed before M2 planning
unless marked otherwise.
- 🟡 Q-27 · M2 Authority roster: which 4–6 units and 6–8 structures make the first slice (scraper,
  refinery, power plant, barracks, factory, wall, turret, one infantry, one light and one heavy vehicle?)
  — **recommendation:** the classic minimum loop above; names follow the period-designation idea in design 03.
- 🟡 Q-28 · Content budget (review D-2, D-8): a ceiling per category (unit types per faction, facings,
  animation frames, missions, briefings, music tracks, voice) and whether v1 is text-only for voice/radio.
  — **recommendation:** 8 facings for infantry, 16 for vehicles (as decided), text-only radio and
  announcer in v1, log real hours per sprite from now on.
- ⚪ Q-29 · Directorate tunnel spec (review D-3): capacity, cooldown, what the enemy sees of burrowed
  units and entrances, how the AI reasons about them, which land is "soft". Needed before M3, not M2.
- ⚪ Q-30 · Campaign pilot (review D-1): build one scripted Authority mission with a briefing on an M2
  map before M3, to measure mission cost? — **recommendation:** yes, as the last M2 item.
- ⚪ Q-31 · Audio sourcing (review D-2): music, SFX and announcer — commission, CC0/CC-BY libraries, or
  generated? Needs a short research note. Before M3.
- ⚪ Q-32 · Top AI tier (research 14 Q2): is *Commissar* allowed any information or economy advantage,
  or strictly better decisions only? — **recommendation:** decisions only (pillar 4: honest AI).


## Answered
- Q-26 · 2026-11-02 · Tank sprite in the debug view → D-041.
- Q-24 · 2026-11-02 · Art pilot: arm B at 64 px → D-039.
- Q-25 · 2026-11-02 · D-033 confirmed; setting draft accepted → D-036.
- Round 3 (Q-03b, Q-06, Q-07, Q-08, Q-23) · 2026-11-02 → D-027..D-034.
- Round 2 (Q-04, Q-05, Q-19..Q-22, Q-03 structure) · 2026-11-02 → D-020..D-026.
- Round 1 (Q-01, Q-02, Q-09..Q-18) · 2026-11-02 → D-009..D-019. Sub-choice MonoGame vs FNA left to the lead (see design/01_architecture.md).
- Q-00 · (session 1 interview) Licensing, platform, look, mode priorities, doc location → D-001..D-008.
