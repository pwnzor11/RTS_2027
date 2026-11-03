# Adversarial review · 2026-11-03 (session 3, end of M1)

Five read-only reviewers, each with one lens: A determinism and lockstep (Sonnet), B scale and M2
readiness (Sonnet), C front end and tooling (Haiku), D design and scope (Haiku), E licensing and
process (Haiku). The lead spot-checked the main claims against the code at `f10ac5f`. Every claim
checked was confirmed, except E-1's licence reading, which was wrong in the dangerous direction
(corrected below). Not legal advice: verify licence points before relying on them.

Nothing here is decided. Items become ROADMAP ids or OPEN_QUESTIONS only after the owner reviews them.

## Ranked synthesis

### Decide before M2 (cheap now, expensive or impossible later)
1. **Licence conflict, MonoGame vs GPL v3 (E-1, corrected).** MonoGame's LICENSE.txt is Ms-PL, with
   MIT only for some portions. The FSF classes Ms-PL as GPL-incompatible. Our own code could carry a
   GPL §7 additional permission for linking MonoGame. EA-derived code could not, because we cannot
   add permissions to EA's code. Options: (a) add the permission and never port EA code verbatim
   (study it only); (b) move the renderer to a GPL-compatible layer (SDL3 bindings or Silk.NET, both
   permissive) while `src/Game` is still small. The sim never references MonoGame, so (b) touches
   only the front end.
2. **Where AI and Lua run (A-1, A-7, B-7).** Either AI is an external command producer whose
   commands are recorded, which keeps replays independent of AI code, or it runs inside `Step`,
   where its memory has to be hashed and saved. The architecture doc says both. The same question
   applies to Lua: numbers are doubles, and `os`, `math.random` and `pairs` need a sandbox for
   determinism.
3. **Player relations and per-player state (B-2, B-3).** There is no team, ally or neutral concept;
   "enemy" means `owner != player` everywhere. There is no `PlayerState` part for credits, power,
   tech or queues. Type lookups read the shared rules table directly in about 20 places, which
   blocks upgrades and `Tech.Grant`.
4. **Actor-kind abstraction (B-1).** About 15 three-way switches cover vehicle, infantry and
   building. Aircraft and other new kinds would need edits in all of them, and missing one fails
   silently. Introduce one store view while there are only three kinds.
5. **Sim boundary hardening (A-4, A-5, B-4, B-8).**
   - Public mutators work outside `Step`.
   - The command sort can tie, and late commands are dropped silently.
   - `Command.Player` is trusted, and `Debug` is live in every build.
   - `Arg` is overloaded per kind.
   - A full `SlotArray` throws mid-`Step`. In lockstep, that would crash every peer at once.
6. **Saves and replays that survive patches (A-2, A-3, A-6, C-6).**
   - Embed the rules in the file, and record ids rather than list indices.
   - Stream the replay instead of writing it all at exit, and add a crash handler.
   - Make the hash/save coverage test see scalars and non-array fields.

### Cheap wins, do now
7. **The published zip can't start (C-1).** It has no `data/` folder and dies silently. Copy the data,
   add a crash message, and smoke-test the published exe in CI.
8. **Ship the licence and notices (E-1, E-7).** Add the licence, third-party notices and a source
   offer to the zip. Fill CREDITS with each library and its licence. Add an EA `NOTICE` before the
   first port (E-2). Add SPDX headers (E-3).
9. **Gitignore and LFS (E-4, E-5).** Add `publish/`, saves and logs to `.gitignore`. Decide on LFS
   for rendered sprite sheets before the M2 art.
10. **Tests (C-5, C-10).**
    - Move the test fixtures out of `data/rules`.
    - Replace disjunctive asserts with exact values.
    - Add one golden replay.
    - Log ticks per second instead of a wall-clock assert.
11. **Builds (C-12, C-11).** Stamp the version and SHA into builds and replay headers. Add a debug
    console that issues recorded `Debug` commands.

### Front-end skeleton before the M2 UI (C-2, C-3, C-4, C-7, C-8, C-9)
- A `SimHost` fixed-step driver, so the sim isn't stepped by the frame loop.
- A screen stack with input routing.
- Palette-indexed sprites with a house-colour shader, instead of a CPU copy per house. The VRAM
  estimate is unmeasured.
- Real fonts and a UI scale.
- `settings.toml`.
- An `IAudio` interface with a null backend.

### Scope and design (D)
- **M2 faction and economy (D-6).** Not chosen, and OPEN_QUESTIONS is empty while several questions
  are open.
- **The Directorate's underground (D-3).** So far it is only a speed column. The fog, AI and map
  rules for tunnels are unspecified.
- **Content and audio budget (D-2, D-8, D-10).** No roster, no hours per asset, and no audio
  research or sourcing plan.
- **Campaign timing (D-1).** The campaign is priority 1 but comes last (M4). Pilot one scripted
  mission early.
- **AI testing (D-4, D-5).** No AI-vs-AI win-rate harness and no `difficulty.toml` schema.
- **Learning goal (D-7).** The owner's learning goal versus "take all" approvals.
- **Documentation weight (D-9).** Docs are growing faster than they are used.

### Measure before acting
- **Path queue, acquisition and splash costs (B-5, B-6).** Unprofiled: the SDK was unavailable to
  the reviewer. Add a 1500-unit, 128×128 benchmark first.
- **Hash cost (A-8, B-9).** Serialises the whole state each time. Minor; fix when the Net layer
  lands.
- **Replay playback (A-9).** O(ticks × commands). Small, and the LAN work needs it fixed anyway.

## Sources checked by the lead
- MonoGame `LICENSE.txt` (github.com/MonoGame/MonoGame, develop): Ms-PL, plus MIT "Portions
  Copyright © The Mono.Xna Team".
- trisquel.info licence list: Ms-PL listed under GPL-incompatible free licences.
