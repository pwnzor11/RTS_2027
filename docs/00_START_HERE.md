# START HERE: session handoff protocol
> Read this file first in every new session.

## What this project is
RTS_2027 is an open-source, Windows-only, 2D-sprite real-time strategy game in the spirit of the
classic Command & Conquer games (Tiberian Dawn / Red Alert era). It is a hobby project for the owner
(pwnzor11) to learn game AI, game architecture and coding. No deadline.

Priorities, in order:
1. **Single-player campaign** for both factions, with story and a tech tree that opens up as the
   campaign unfolds (mission-gated buildings, units and upgrades).
2. **Skirmish vs AI** with several difficulty levels that give the AI real capability upgrades, full
   tech tree from the start, each game standalone.
3. **LAN multiplayer** (nice-to-have): family and friends on a home network. No public servers,
   no matchmaking, no hosting infrastructure.

Build order (D-087, session 4): finish M3, then skirmish depth (M4), the LLM commander (M5) and two new factions —
The Left Behind (M6) and the Overseas Expedition (M7) — before the campaign (M8), so the campaign is written once with
all four factions. D-171 (session 14): M11 UI and input overhaul comes after M8 and before M9 LAN. Priorities above are unchanged in importance; only the build order moved.

- Repo: `github.com/pwnzor11/RTS_2027`, branch `main`. Changes reach main by: **pull request** from an integration
  branch, merged by the lead once Windows CI is green (D-179; direct pushes to main are refused by the session safety check).
  New LFS art cannot be pushed from the cloud: the owner pushes it with a packaged bundle (HANDOFF, session 15 lessons).
- Reference source: EA's C&C Remastered Collection (GPL v3 + EA additional terms), cloned read-only to
  `/home/claude/ref/CnC_Remastered_Collection` in the cloud workspace (re-clone if missing:
  `git clone --depth 1 https://github.com/electronicarts/CnC_Remastered_Collection`). It is a read-only
  reference only: no EA code is ever copied or ported (D-050). The same holds for OpenSAGE (`/home/claude/ref/OpenSAGE`,
  contains EA-derived Generals code; art/presentation reference, docs/design/18_art_guidance.md). See `docs/LICENSING.md`.
- User's setup: Windows. Experienced but rusty in C++, C#, Rust, Python, Unity, Godot, Unreal; is
  happy to take on complicated things. Tests by running the game.

## Hard rules
- No PII, credentials or tokens in docs, logs, commits, fixtures or briefs.
- Never guess a user decision (scope, design, names, story, balance numbers the user cares about):
  ask with options + recommendation, log in OPEN_QUESTIONS, mark code `// PENDING Q-xx`.
- On resume: read, give the user the session briefing, and wait for "go" before doing any work.
- One commit per item: `<type>(<area>): <summary> [<id>]`, id from ROADMAP.
- main only holds verified commits. Every change ends with a short "how to check it" note.
- **Licensing:** the project is and stays open source (GPL v3 or compatible). Any file that contains
  code copied or ported from the EA repo keeps EA's copyright header, is marked as modified, and is
  listed in `docs/LICENSING.md`. Never use the "Command & Conquer" name, EA trademarks or EA's
  art/audio/maps (they are not in the source repo and are not ours).
- **Determinism:** the game simulation must be deterministic (fixed-point or integer math, seeded
  RNG, no wall-clock or float in sim). This is required for LAN lockstep, replays and reproducible
  bugs, and it cannot be retrofitted. Enforce it from the first line of sim code.
- Design before build: session 1 settled that we hold several rounds of design discussion before
  writing game code. Do not start building until `docs/design/` records the decisions.
- Pilot one sample before fanning out subjective work (art, writing, mission design).

## Load order for a fresh session
1. This file. 2. HANDOFF.md — run the staleness check against `Last commit:`.
3. SESSION_LOG.md, latest entry only. 4. ROADMAP.md. 5. OPEN_QUESTIONS.md (red first).
6. DECISIONS.md: skim; never contradict without a new entry.
7. Only the design/code/research files the next task needs.
Then: session briefing → wait for "go".

## Where things live
- `docs/` — these handoff docs.
- `docs/research/` — research write-ups (C&C code study, RTS architecture, maps, resources, AI).
- `docs/design/` — our design decisions once the design rounds produce them.
- `docs/LICENSING.md` — license, reuse rules, list of files derived from the EA repo.
- `docs/CREDITS.md` — third-party libraries, imported assets, AI-assisted artwork.

## How a session runs
Lead (Opus) + up to 20 helpers (Haiku default; Sonnet/Opus for core sim, netcode, or after a Haiku
failure). Waves, batch merges, checkpoint after each wave. Use Sonnet as lead for pure question or
doc-edit sessions. The owner wants back-and-forth on design: when a session is a design round, the
lead's job is to present options with trade-offs and recommendations, not to decide.

## End-of-session checklist ("wrap up")
1. Append to SESSION_LOG. 2. Tick ROADMAP; add ids for new work. 3. Log decisions in DECISIONS; move
answered questions in OPEN_QUESTIONS; confirm any "assumed — confirm". 4. Add lasting checks to
USER_CHECKS. 5. Rewrite HANDOFF with `Last commit:` and next session's "Start here" list.
6. Fact-check the wrap-up docs with a fresh helper. 7. Data-safety sweep of changed docs.
8. Commit `docs: Session N handoff`, push to main. 9. Tell the user what changed, what to check,
what's waiting on them, and the resume prompt.

## Resume prompt
> "Resume RTS_2027. Read docs/00_START_HERE.md, docs/HANDOFF.md and the latest SESSION_LOG entry,
> run the staleness check, then give me the session briefing and wait for my go-ahead."
