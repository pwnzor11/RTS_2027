# Handoff: Session 2 — Design round 1 (technology and sim architecture)
Last commit: (set at wrap-up)
Where things stand: Session 1 bootstrapped the docs and produced 19 research files plus a synthesis
(`docs/research/00_SYNTHESIS.md`). No design decisions beyond D-001..D-008 are made. No game code exists.
Design round 1 was opened in chat at the end of session 1; the owner may have answered some of it.

## Start here (shown to the user as the plan; nothing starts until they say go)
1. Check the chat tail of session 1 / OPEN_QUESTIONS for any round-1 answers the owner gave; log them
   as D-009+ before anything else.
2. Finish design round 1 (Q-01, Q-02, Q-09..Q-18): present each with the research trade-offs, record
   decisions in DECISIONS.md, write `docs/design/01_architecture.md` from the answers.
3. If Q-09 = spike: plan the OpenRA spike as its own short milestone. Otherwise move to round 2 (Q-03..05, Q-19..22).
4. Only after rounds 1–3: M0-11 repo skeleton.

## Half-finished work
- Nothing in code. Research files 01, 02, 04, 05, 15, 20, 21, 22 run 5–15% over the 3,500-word target;
  trim only if the owner finds them long.

## Waiting on the user
- Design round 1 answers (see OPEN_QUESTIONS, round 1).

## How to run this session
- Lead: Sonnet is fine for a pure design-discussion session; Opus if it turns into the repo skeleton build.
- Repo clone: `/home/claude/rts_2027` (push to `origin main` directly; no branch protection).
- Reference clones (read-only, re-clone if the workspace was reclaimed):
  `/home/claude/ref/CnC_Remastered_Collection`, `/home/claude/ref/openage`, `/home/claude/ref/freeaoe`,
  `/home/claude/ref/ageServer`, `/home/claude/ref/AOEMods.Essence`, `/home/claude/ref/aoc-mgz`.
- Helpers: session 1 used 14 + 5 research helpers (Sonnet for code studies, Haiku for web sweeps).
  Haiku web-research output was good; several Haiku helpers hit robots.txt/bot walls on Spring, 0 A.D.
  and OpenRA wiki pages, so those details are marked unverified in the files.
- A classifier blocked one helper launch whose brief said "run the repo's Python"; read-only briefs pass.
- The owner's preferences: Opus lead for build sessions, Sonnet for doc/question sessions, Haiku
  helpers by default, pilot one sample before fanning out subjective work.
- Don't: start game code before the design rounds; add remotes; use EA names in anything.

## If time runs short
Get Q-01 (language) and Q-02 (EA reuse) decided and logged; everything else can wait a session.
