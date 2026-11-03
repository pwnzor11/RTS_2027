# Lens D: Game design, scope and project risk

## Findings (ranked, most dangerous first; max 12)

### D-1 · Campaign (priority 1) is scheduled last and has no breakdown · severity: COSTLY-LATER
- Evidence: DECISIONS D-005 ranks "campaign > skirmish > LAN" for design weight. ROADMAP M4 is one line: "Mission scripting/trigger system, briefing screens, tech gating, campaign flow, both campaigns." M3 holds both full rosters, the full tech tree, the map editor, real AI and audio. Campaign tech gating needs that full tree (design 02 §8, `Tech.Grant`). No mission list exists; only two spines in design 03. START_HERE says "Pilot one sample before fanning out ... writing, mission design", but no writing or mission pilot is on the ROADMAP.
- Failure: The owner's first priority is not playable until after M3, the largest milestone. M3 will overrun and M4 may never start. Mission writing (22–25 missions, about 22 briefings with portraits) is the largest unpiloted content task, and it is not in any milestone.
- Fix: Add a campaign pilot before M3: one scripted mission on an M2 map, with a briefing, two radio lines and one unlock, measured in hours. Split M3 into M3a (rosters and tech tree for one faction) and M3b, and write the M4 mission list with ids. when: before M3; rough cost: S for the pilot, M for the split.

### D-2 · Content volume and audio/voice are uncosted and partly unplanned · severity: WILL-BREAK
- Evidence: No unit roster exists; design 03 defers it to "the unit-roster pass". `data/rules` holds 4 vehicles, 2 infantry, 4 buildings, 4 weapons and 3 warheads. Research 17 §3 estimates ~1,700 hand hours or ~500 Blender hours plus 40–80 for the pipeline (its own note: "my estimates, not sourced"). That table assumes "3 theatres", while D-022 and design 02 say two. Design 02 §10 and 03 require an "EVA-style announcer per faction". D-030 needs in-mission radio lines and music for briefings. Cutscene art (2–3 per side) is in no budget. No research file covers audio or music; ROADMAP puts audio in M3 and music/SFX in M6, with no sourcing or licence check under D-033.
- Failure: From M3 on, the owner has no sourced path to music, voice or radio content and no hour budget to decide between text-only and voiced. Art (the current critical path) and audio will stall at M3 and M6, and a "finished" game cannot be defined.
- Fix: Write one content budget table (types × facings × frames, buildings, maps, missions, briefings, radio lines, EVA lines, music tracks, cutscene stills) with hours per row, and get the owner's ceiling for each. Add a short audio research note and a text-only v1 decision for voice and radio (D-030 already allows it). when: NOW; rough cost: S.

### D-3 · The Directorate's underground is only a speed column · severity: WILL-BREAK
- Evidence: `data/rules/terrain.toml` has a `burrow` speed column and nothing else. The only tunnel code is the locomotion enum (`src/Sim/Rules/Enums.cs`). Design 03 gives the faction tunnel networks, entrances and vents with HP, "burrow under terrain, surface anywhere soft", and radiation denial. The open threads (capacity, cooldown, visibility to the enemy, conscript pressing, radiation vs decon) are all still open. D-035 approves the locomotion class only.
- Failure: The asymmetry pillar (design 02 §1, point 2) cannot be built. At M3, the real AI (fog-honest, per research 14) must reason about hidden tunnels, and the fog model in M1-07 does not cover them. Retrofitting an information model is what research 00 §3 calls "expensive". Map format and pathing need tunnel rules before M3 map design.
- Fix: Write a one-page tunnel spec (visibility, capacity, cooldown, what the enemy sees, what the AI knows) as a DECISIONS entry before M2 ends. Add a small test map with two entrances to validate it. when: before M3; rough cost: M.

### D-4 · The "real capability" difficulty ladder is a sketch, with no AI-vs-AI harness on the roadmap · severity: COSTLY-LATER
- Evidence: Design 02 §7 says the tiers are "defined in `difficulty.toml`". No such file exists in `data/`. The values are unspecified. Research 14 §3.3 says the axes are "design proposal, not sourced". Research 14 recommended a six-level ladder with a Sandbox tier for learning, but D-026 chose five and dropped Sandbox without logging why. Design 02 §9 says a headless AI-vs-AI runner and a win-rate matrix are "from the start" and a "release check". ROADMAP has no such item: AI is "dumb AI" in M2 and "real AI" in M3.
- Failure: Difficulty becomes hand-tuned at M3 with nothing to measure it, so the "five tiers feel fair" goal is untestable. The top tier stays undefined (research 14 Q2 is unanswered and not in OPEN_QUESTIONS).
- Fix: Add a `difficulty.toml` schema with one placeholder row per tier and a `Tools match --ai` item to M2 that reports a win-rate. Log the sandbox decision. when: before M2; rough cost: M.

### D-5 · No balance or playtest loop · severity: COSTLY-LATER (tooling is CHEAP-WIN)
- Evidence: Design 02 §3 says "playtest every start position", and §2 marks many numbers "(tune)". The only playtester is the owner, who must run the Windows build. Replays (D-017) and `Tools match` exist (M1-10), but no item uses them for balance. HANDOFF records that the owner was only asked to "try the sandbox".
- Failure: Balance is decided by one person's play sessions, which are rare. At M3, with two factions and eight-plus upgrades each, no regression check exists, so a balance change in one place breaks another silently.
- Fix: Extend `Tools match` to take AI-vs-AI seeds and print a win-rate table; store the output as a golden file CI can diff. Add one playtest-log template in `docs/`. when: before M2 (CHEAP-WIN, S); the rest before M3.

### D-6 · M2 cannot start contract-first: the owner has not chosen its faction, map or economy · severity: COSTLY-LATER
- Evidence: ROADMAP M2 says "one faction, one map, dumb AI", but no faction is named. The M2 harvester is the Authority's scraper or the Directorate's conscript gang, and design 03 leaves the Directorate's economy to "the unit-roster pass". The M2 economy item (ashfall, pitchblende) depends on that choice. OPEN_QUESTIONS.md has an empty "## Open" section, though research 14, 16 and 17 and design 03 list open owner questions (dynamic difficulty, meta layer, superweapon names, Blender commitment, the M2 faction). None are logged.
- Failure: The next session plans M2 with silent defaults, which START_HERE forbids. M2 then builds a faction the owner does not want to ship first.
- Fix: Log each open item in OPEN_QUESTIONS with a recommendation, starting with "which faction is the M2 faction" and "does the Directorate harvest by tunnel or by gang". when: NOW; rough cost: S.

### D-7 · Owner decisions are being batch-approved, which undercuts the learning goal and maintainability · severity: COSTLY-LATER
- Evidence: START_HERE says the project exists "to learn game AI, game architecture and coding". D-040 let the lead take every M1 design choice, logged "assumed — confirm". D-049 records "reviewed in session 3 as one table; the owner took every recommendation." HANDOFF step 2 asks for "take all" first. M1 was built overnight with 18 helper launches and about 9.7k lines of C# sim code plus 4.6k of tests. Desync bugs in this code need the sub-hash and replay tools.
- Failure: The owner is accepting the design without building anything himself and cannot debug the determinism rules, the hash-coverage test or the state-part order (HANDOFF) alone. Learning goal and maintainability both fail, and a bus factor of one plus an AI lead remains.
- Fix: For M2, the owner writes one system himself (suggested: the harvester loop) with the lead reviewing; decisions go back as a short reason, not a table of "take all". Record in START_HERE that overnight builds are for throughput, not for owner learning. when: NOW; rough cost: S process, M time.

### D-8 · Art is the critical path and is bound to owner approval, with no per-asset time log · severity: COSTLY-LATER
- Evidence: D-032 says "the owner approves designs" and makes the lead the artist. The only finished vehicle is the tank (M1-ART-01 is a polish round on it). Research 17 §3 puts a vehicle at 3–6 Blender hours and 25–50 hand hours, but the tank's real cost is not recorded. D-032 also permits AI-generated frames, while research 17 says "frame consistency is poor". 16 facings for vehicles (D-032) roughly double the frames versus 8.
- Failure: M2 uses placeholder art, but M3 needs about 56 types. The art rate is unknown, so the schedule is a guess, and the AI-frame path may fail late.
- Fix: Log hours for every sprite now. Set a v1 cap: 8 facings for all categories and a fixed minimum roster per faction, e.g. 6 vehicles, 6 infantry, 10 buildings. Decide the AI-frame policy before M3. when: NOW (logging), before M3 (cap); rough cost: S.

### D-9 · Documentation overhead is growing and the bus factor is one · severity: COSTLY-LATER
- Evidence: `docs/` has about 4,770 lines of Markdown, 3,690 of them research, against about 14.3k lines of code. A fresh session must read START_HERE, HANDOFF, SESSION_LOG, ROADMAP, OPEN_QUESTIONS and DECISIONS before work. HANDOFF and SESSION_LOG are rewritten every session.
- Failure: Each session pays a growing reading cost and the handoff docs become the only source of truth. The owner cannot audit them, so they drift from the code. (Not yet dominant; the trend is the risk.)
- Fix: Freeze the research folder as archive (no new research without a decision it serves). Merge HANDOFF and SESSION_LOG into one file with a size cap. when: before M2; rough cost: S.

### D-10 · The Directorate's surface base and superweapons have no named units or structures yet · severity: COSTLY-LATER
- Evidence: Design 03 leaves "the exact two superweapons" and "naming of individual units" open. Research 17 §3 counts 14 types per faction, which is an estimate, not a roster. D-023 fixes two superweapons per side with dedicated structures and long charges.
- Failure: Sprite, rules and tech work are all blocked by names and counts, and superweapons are the largest unscoped mechanic. A late decision here repeats the art cost in D-8.
- Fix: Name the roster and superweapons in one table with approximate hours; lock it before M2 art begins. when: before M2; rough cost: S.

## Things you checked that are fine
- Campaign shape (10–12 missions per side, reused maps, no carry-over, text-first) matches research 16 and D-030, and bounds scope.
- Licence allow-list (D-033) and the no-EA-assets rule constrain art and audio sourcing early, which is cheap.
- Deterministic sim, replays and `Tools match` (M1-10) give the balance harness (D-5) a cheap base.
- The factions are distinct in identity and in the asymmetry table (design 03), which is the right pillar.

## Unknowns
- No web search was run, so the content counts are not checked against shipped sprite RTS games. Research 17's hour figures are unsourced and I did not re-derive them.
- Owner's available hours, and the real time per sprite, are not recorded anywhere, so every schedule claim is conditional.
- Not read in full: research 00 §1–2 and 01–13, and 20–24. No audio research file exists, so I could not confirm whether one is planned elsewhere.
- MoonSharp versus embedded Lua (D-015) is not settled in the repo, so I did not judge M4 scripting cost.
