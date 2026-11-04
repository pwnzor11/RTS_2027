# Open questions
🔴 blocks work · 🟡 needed soon · ⚪ later. Code waiting on one carries `// PENDING Q-xx`.
Context and trade-offs for every question are in `docs/research/00_SYNTHESIS.md` §2 and §4 and the
file cited after each question. Recommendations are the lead's, from the research.

## Open

- 🟡 Q-40 Default LLM model names in data/llm/providers.toml are placeholders (`claude-haiku-4-5` default, `claude-sonnet-4-5`
  stronger). Recommendation: confirm current model ids before M5 play-test.
- ⚪ Q-41 Six older lore files still carry a DRAFT banner (docs/lore/world_history, authority, structures, units, directorate,
  upgrades). Their Archive text was approved in session 4. Recommendation: confirm and clear the banners.
- ⚪ Q-42 Quick-Set Foam covers only 3×3 (the loader caps `splash` to a flag), so it slows a tunnel push but cannot block one.
  Options: (a) keep, (b) widen foam (sim/loader change). Recommendation: (a) until a play-test says otherwise.
- ⚪ Q-43 Tier costs: cut the tier-3 gate and tier-3 upgrades 2 500 → 2 000 (docs/balance/m4_16_rules.md §4)? Recommendation:
  measure in the next AI balance round first.

## Answered
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
