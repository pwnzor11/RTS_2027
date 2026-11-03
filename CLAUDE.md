# RTS_2027

Open-source (GPL v3) 2D real-time strategy game in the classic Command & Conquer style. Windows only.

**Every session: read `docs/00_START_HERE.md` first and follow it.** It holds the hard rules, load
order, session protocol and resume procedure.

Quick rules that must never be broken:
- Simulation code is deterministic (no float, no wall-clock, seeded RNG).
- EA-derived code is marked and registered in `docs/LICENSING.md`; no EA trademarks or assets.
- Design decisions live in `docs/DECISIONS.md`; never contradict one without a new entry.
