# Design 20 · M9 LAN multiplayer plan
Design round only (session 19, parallel session; no code). Builds on D-005, D-006, D-014, D-017, D-034, D-051, D-110 and the
research in `docs/research/03_cnc_networking_determinism.md`, `10_rts_architecture.md`, `22_ageserver.md` and `00_SYNTHESIS.md` §14.
Build order is unchanged: M9 starts after M13 and M14 (D-205, D-206). Every choice below is an option with a recommendation; the
owner decides. The questions are logged as Q-M9-a..n in `docs/OPEN_QUESTIONS.md`, plus Q-M9-o, one batch of assumptions to confirm (§8).
**The owner answered all of them on 2026-11-12: see §9, which overrides §3–§5 wherever they differ.**

## 1. What is already settled
- **Goal (D-005):** family and friends on a home network. No public servers, matchmaking, accounts or hosting. Nice-to-have.
- **Lockstep on a deterministic sim (D-006):** only commands cross the wire. The sim is a pure function of (setup, seed, commands).
- **Command delay (D-014):** `ExecTick = Tick + 1` in single player, `Tick + 2..4` on LAN, both config values. Sim is 20 Hz (50 ms).
- **Scope (D-034):** host-star topology (clients talk only to the host), 2–4 humans plus AI slots in one lobby, UDP
  broadcast/multicast discovery plus join-by-IP, no host migration (host leaving ends the game; the protocol leaves room for a
  manual claim later), build/rules/map hash check on join.
- **AI outside the sim (D-051):** AI issues ordinary commands; in LAN one peer runs each AI. **Replays (D-017)** come from the
  same command stream. **Commander chat is outside the sim (D-110).**

## 2. What the code already gives us
Checked against main at `d12dd96`. M9 is mostly a new transport plus a new advance policy; the sim needs little or no change.
- `Command(ExecTick, Player, Sequence, Kind, Subject, Target, Cell, Arg, Arg2)` (`src/Sim/Core/Command.cs`). `Step` sorts by the
  whole tuple, so the order never depends on arrival order. `CommandKind.Chat = 20` exists but nothing uses it.
- `SimHost` is the only caller of `Simulation.Step` and has no clock; its comment already names "message arrival for a future LAN
  host" as an advance source (`src/Game/Host/SimHost.cs`). It owns the per-player sequence counters, pause, speed and the replay hook.
- `AiDriver` stamps `ExecTick = tick + command delay` and keeps its memory outside the sim (`src/AI/AiDriver.cs`).
- State hash: full xxHash every 16 ticks in a ring of 64 (`SimConstants.HashRingSize`), with named sub-hashes
  (`Simulation.Hashing.cs`). That is the desync detector; C&C's position-only CRC was weaker.
- Replay format 13 records build, rules and map hashes, seed, slots and periodic hashes; headless playback is a test.
- Saves (format 13) hold every sim array, RNG state, AI memory and Lua state, with a permanent save-round-trip-then-step test.
  A save is therefore also a valid "full state" message (used by §6 resync and reconnect).
- Skirmish setup: 4 slots, each Closed / Human / Ai (`SlotKind`), with faction, colour, team; `SkirmishSettings.Validate()`.
- The menu shows "LAN Game" greyed (`MainMenuModel`). `FocusPause` already skips multiplayer.
- Missing: `src/Net` (named in design 01 §2 but not created), a lobby screen, discovery, any socket code.

## 3. Architecture sketch (lead proposal, not a question)
- New project `src/Net` (no MonoGame, no Sim internals beyond `Command` and the save/replay codecs). Headless and unit-testable.
- `SimHost` gets one seam: an `ITurnSource` that says "tick T may run, here are its commands". Today's behaviour becomes
  `LocalTurnSource` (time-driven, delay 1). LAN adds `LanHostTurnSource` and `LanClientTurnSource` (bundle-driven, §4.2).
  Wall-clock time only decides *when* to ask; it never touches `ExecTick` on a client.
- Wire format: length-prefixed binary messages with a protocol version byte; commands reuse the replay command encoding.
  Message kinds: Hello/Welcome/Reject, LobbyState, SlotRequest, Ready, Start, Input (client → host), TurnBundle (host → all),
  Hash (client → host), Chat, Pause/Resume, Drop, Bye; later SaveBlob for resync/reconnect.
- The host checks every client Input: a client may only send commands for its own slot, within size limits. Malformed messages
  close that connection; they never reach the sim.
- Each peer writes its own replay from the TurnBundle stream, so all replays of one match are byte-identical (handy for desync
  reports).

## 4. Design questions

### 4.1 Transport (Q-M9-a)
1. **TCP for lobby and match, UDP only for discovery (recommended).** On a home LAN loss is near zero, so TCP's retransmit stalls
   (research 10, Fiedler) hardly ever happen. Reliable, ordered delivery removes C&C's whole ack/retry and "command count hole"
   layer (research 03 §2). Nagle off. Plain .NET sockets, no new dependency.
2. UDP with our own reliability (C&C / AoE style). Best on lossy links; a lot more code and tests for a LAN.
3. A library (LiteNetLib, MIT, reliable-ordered UDP channels). Less code than 2, one dependency to track in CREDITS.
   Good fallback if 1 shows stalls on Wi-Fi.

### 4.2 Turn model (Q-M9-b)
1. **Host-sequenced turn bundles (recommended).** Clients send their commands to the host as soon as they are issued. The host
   assigns each one `ExecTick = hostTick + delay` and its sequence, and once per tick broadcasts a TurnBundle: "tick T, these
   commands" (often empty). A peer steps tick T only when it holds bundle T. One message to wait for, one source of truth for order,
   and "packet too late" (C&C's fatal error) cannot happen because the host stamps the tick. The host's own input and its AI go
   through the same delay, so the host has no advantage beyond its zero ping.
2. Peer-equal lockstep relayed through the host (classic C&C): each client stamps its own ExecTick and every peer waits until it
   has every other peer's list for T. Same topology, more waiting rules, and a late command is a real failure case.

### 4.3 Command delay (Q-M9-c)
1. **Fixed 3 ticks (150 ms), a value in game settings, lobby shows each player's ping (recommended).** Inside D-014's 2..4. Wired
   home networks are under 5 ms; Wi-Fi spikes fit in 150 ms.
2. Fixed 2 ticks (100 ms): snappier, more stalls on busy Wi-Fi.
3. Adaptive: the host retunes the delay from measured round-trip (C&C's `MaxAhead`, research 03 §2). Only worth it for internet
   play; it needs the delay-change rescheduling rule C&C had.
4. Host picks 2/3/4 in the lobby.

### 4.4 Slow or vanished player (Q-M9-d)
Lockstep means everyone waits for the slowest peer. Proposed flow: a peer more than 1 s behind shows "Waiting for <name>" to
everyone; after 10 s the host gets a "Drop <name>" button; after 60 s the host drops automatically. What happens to the slot:
1. **An AI takes over the slot (recommended).** The host's `AiDriver` starts issuing that player's commands from the drop tick
   (tier picked in the lobby, default the skirmish default). No sim change: AI commands are ordinary commands, and every peer gets
   them in the bundle. The game stays fair for the remaining humans.
2. The slot goes idle: units stay, defend themselves, build nothing.
3. The player is defeated and their units removed.

### 4.5 Desync response (Q-M9-e)
Every client sends its 16-tick hash to the host; the host compares (it knows the tick each hash belongs to, so no stale-heartbeat
false alarms as in C&C). On a mismatch every peer writes a desync report folder: the replay so far, a save at the last agreed
tick, the sub-hash list naming the first part that differs. Then:
1. **Pause with a choice for the host: Resync from host / Continue / End match (recommended).** Resync sends the host's save at
   the current tick to every client, which load it and carry on. Cheap because the save round-trip is already a tested invariant.
2. Stop the match with "Out of sync" (C&C). Simplest; a desync bug ends a family evening.
3. Warn and keep playing. Peers silently play different games until it shows; not recommended.

### 4.6 Where AI and LLM commander slots run (Q-M9-f)
1. **The host runs every AI slot, including LLM commanders, with the host's own provider key (recommended).** Keys never leave
   the host machine (they are DPAPI-protected per user today). Directives are already commands (D-103), so lockstep holds. LLM slots
   are offered in the lobby only when the host has a key set. Commander chat lines go out as chat messages (§4.7), not sim commands.
2. Each AI runs on the peer that added it (D-051 allows it). Spreads CPU, but a client's drop also drops its AIs, and an LLM slot
   would use that client's key.

### 4.7 Player chat (Q-M9-g)
1. **Chat outside the sim (recommended):** a Chat message relayed by the host, shown at once, to All or Team, never in the sim
   hash. Same reasoning as D-110. `CommandKind.Chat` stays reserved. Replays keep a chat block only if the owner wants (sub-question:
   record chat in the replay, yes/no; recommended yes, it is family-only).
2. Chat as a sim command: in the replay for free, but delayed by the command delay and it needs a text table in the sim.

### 4.8 Pause and game speed (Q-M9-h)
1. **Any player can pause; anyone can resume after a 3-second countdown shown to all; speed fixed at 1x (recommended).** Pause is
   a control message: the host stops sending bundles, so all peers stop on the same tick. The Esc menu does not pause a LAN game
   (it opens over the running game), and focus loss never pauses (already true).
2. Host only can pause.
3. No pause at all.

### 4.9 Saving and resuming a LAN game (Q-M9-i)
1. **No saving in the first LAN build; host-only save and resume in a later phase (recommended).** The host saves (sim, RNG, AI
   memory, Lua state are already in the save); resuming opens a lobby from the save, and each joining human claims their old slot;
   unclaimed human slots go to AI (§4.4). Needs the same SaveBlob transfer as resync.
2. Host-only save in the first build.
3. Never: LAN games are one sitting.

### 4.10 Host leaving (Q-M9-j)
D-034: the game ends. What clients get:
1. **"Host left the game", their replay is kept, back to the menu (recommended for the first build).** If §4.9 option 1's later
   phase lands, the host's last autosave can be resumed from the host's machine.
2. Each client may also save locally and continue that match vs AI on its own (every client holds the full state). Nice, but it
   splits one match into several and needs a "convert LAN save to skirmish" path.

### 4.11 Maps in LAN (Q-M9-k)
Generated maps travel as their parameters and seed (already deterministic). Shipped maps travel as name plus map hash. For a
custom map from the map editor that a client does not have:
1. **The host sends the map file in the lobby (recommended).** Maps are small; the client checks the hash and keeps it in a
   downloads folder.
2. Every player must copy the file by hand; the lobby says which file is missing.

### 4.12 Spectators (Q-M9-l)
1. **Up to 2 spectators may join from the lobby (recommended).** A spectator is a client with no slot: it gets bundles, sends no
   commands, sees without fog (as in today's skirmish spectate). Nobody waits on a lagging spectator; it just falls behind.
2. No spectators in LAN.

### 4.13 Reconnect and late join (Q-M9-m)
1. **No reconnect in the first build; reconnect in the later phase (recommended).** Once SaveBlob exists (§4.5, §4.9),
   reconnect is a small step: the host pauses, sends a save at tick T plus the bundles since, the returning player takes back the
   slot from the AI.
2. Never (pure D-034 reading: a dropped player stays dropped).

### 4.14 Co-op campaign (Q-M9-n)
Missions are Lua inside `Step` with the sim RNG (D-015), so two humans on one side of a campaign mission is technically possible.
1. **Not in M9; note it as a possible later milestone (recommended).** The missions, briefings and objectives assume one player.
2. Plan it into M9.

## 5. Phases and proposed ROADMAP items
Item ids are proposals; they go into ROADMAP when the owner approves the plan. Helpers: Sonnet/Opus for netcode (00_START_HERE).
- **Phase 1 · Lockstep core, no UI.** M9-01 `src/Net` skeleton, message codec, protocol version. M9-02 `ITurnSource` seam in
  `SimHost`; single player moves onto `LocalTurnSource` with no behaviour change (golden replays and hashes unchanged). M9-03 host
  and client turn sources over loopback TCP. M9-04 `Tools lan soak`: host plus N in-process clients over real sockets with injected
  latency and jitter, AI-vs-AI, compares hashes every 16 ticks; runs in CI on Linux and Windows.
  *User can try:* `Tools lan soak --players 4 --minutes 20` prints 0 desyncs.
- **Phase 2 · Lobby and play.** M9-05 discovery (UDP broadcast on each interface plus multicast, one fixed port above 1024) and
  join-by-IP. M9-06 lobby screen (name, slots, faction/colour/team, ready, map, game options, ping, version/hash check with both
  versions shown on a mismatch). M9-07 in-game: waiting overlay, chat, pause, drop and AI takeover, end-of-match flow. M9-08 replays
  from the bundle stream. M9-09 two game windows on one PC (`--lan-test` loopback) for the owner's first check.
  *User can try:* menu → LAN Game on two PCs, a 2v2 with two AIs, chat, pause, pull a network cable and see the AI take over.
- **Phase 3 · Robustness.** M9-10 desync reports and Resync from host (SaveBlob). M9-11 host save/resume (if Q-M9-i says so).
  M9-12 reconnect (if Q-M9-m says so). M9-13 spectators, custom map transfer.
- **M9-14 Acceptance:** loopback soak in CI, a two-PC session by the owner, a Wi-Fi session, golden LAN replay.

## 6. Risks
- **Determinism outside the sim.** Anything a peer uses to build its sim must match: rules (rules hash), map (map hash), mission
  scripts and data files that are not in the rules hash yet. M9-01 adds a content hash over the whole `data/` folder to the join check.
- **Settings that change the sim.** Per-machine options (supply cap, no-rebuild rule, difficulty tables) must come from the host's
  lobby, never local settings. M9-06 checks every option the skirmish setup passes into the sim.
- **Slowest machine sets the pace.** A 4-player large map on an old laptop may not hold 20 Hz. Measure early with the existing
  `HostProfile` benchmark on the owner's laptop.
- **Windows firewall.** The first listen on a port shows the Windows Defender Firewall prompt; "Private networks" must be ticked.
  We ship a zip with no installer, so the lobby needs a clear "can't reach host" message pointing at that setting (§8).
- **Map hack.** Every peer holds the full state, so fog can be lifted by a modified client. Accepted for family LAN (§8).

## 7. Out of scope for M9
Internet play, NAT traversal, relay servers, accounts, matchmaking, ranked play, host migration (D-034), anti-cheat, voice chat,
co-op campaign (unless Q-M9-n says otherwise).

## 8. Assumptions to confirm in one go (lead defaults)
1. Map hack is accepted; no anti-cheat (family LAN).
2. No lobby password in the first build; the game only answers discovery on the local network.
3. Windows shows its own firewall prompt on first host; we add a How to Play page and a lobby message, no installer rule.
4. Player name defaults to the Windows user name, editable, stored in settings.
5. Game options in the LAN lobby are exactly the skirmish ones, set by the host.
6. Testing: loopback soak in CI, two windows on one PC, then the owner's desktop plus laptop.
7. One fixed port pair (discovery UDP, game TCP), changeable in settings for a clash.

## 9. Owner answers (2026-11-12) and what they change
| Q | Answer | Rec? |
|---|---|---|
| Q-M9-a Transport | **UDP with our own reliability** (option 2) | no |
| Q-M9-b Turn model | Host-sequenced turn bundles | yes |
| Q-M9-c Command delay | **Host picks 2/3/4 ticks in the lobby** (option 4); lead default 3 | no |
| Q-M9-d Dropped player | AI takes over the slot | yes |
| Q-M9-e Desync | Pause; host picks Resync from host / Continue / End | yes |
| Q-M9-f AI and LLM slots | Host runs every AI slot; LLM commanders on the host's key, offered only if the host has one | yes |
| Q-M9-g Chat | Outside the sim, All/Team, relayed by the host, kept in the replay | yes |
| Q-M9-h Pause and speed | Anyone pauses; anyone resumes after a 3 s countdown; speed fixed 1x | yes |
| Q-M9-i Saving LAN games | **Never**: LAN games are one sitting | no |
| Q-M9-j Host leaving | **Clients may also save and continue vs AI** (option 2) | no |
| Q-M9-k Custom maps | Host sends the file in the lobby, hash checked | yes |
| Q-M9-l Spectators | Up to 2 from the lobby; nobody waits on them | yes |
| Q-M9-m Reconnect | **Never**: a dropped player stays dropped | no |
| Q-M9-n Co-op campaign | Not in M9 | yes |
| Q-M9-o Assumptions §8 | All confirmed | yes |

The DECISIONS entries for these get their numbers at the next wrap-up, after session 19's (integ-s19 already uses up to D-210),
so the two sessions cannot pick the same id.

**What changes in the plan (lead reading; flag any that are wrong):**
- **Transport (a).** One UDP protocol for lobby and match (owner confirmed 2026-11-12: UDP for the lobby too); discovery uses the same socket family. `src/Net` gains a small
  reliability layer: per-peer packet ids, acks carried on every packet, resend of unacked reliable messages after a timeout
  set from measured round-trip, duplicate drop, and packets kept under 1 200 bytes (no IP fragmentation). TurnBundles and
  Inputs are reliable and ordered per peer; pings and hashes may be unreliable. Because the host stamps every tick (b), C&C's
  "command count hole" check is not needed: a peer simply waits for bundle T. New item **M9-01b reliability layer** with its
  own tests (loss, duplication, reordering), and the soak tool (M9-04) injects loss and reordering as well as latency.
- **Delay (c).** A lobby option "Command delay: 2 / 3 / 4 ticks (100 / 150 / 200 ms)", default 3, next to the ping column.
  Fixed for the whole match.
- **Saves (i) and host leaving (j).** No LAN save or resume. When the host leaves, each client offers "Continue against AI":
  it writes an ordinary single-player skirmish save of the last tick it fully stepped, with every other human slot (the host
  included) turned into an AI slot, and loads it. Host-run AIs start with empty memory on the client, because their memory
  lived on the host (D-051). The result is a normal skirmish save, never a LAN save, so it fits Q-M9-i. Needs a small
  "LAN match → skirmish save" converter in M9-07.
- **Reconnect (m).** Dropped stays dropped; the AI keeps the slot. A save is still sent over the network for desync Resync (e),
  so SaveBlob stays, but only as host → all at a paused tick.
- **Phase 3 shrinks** to: M9-10 desync reports and Resync from host, M9-13 spectators and custom map transfer. M9-11 (host
  save/resume) and M9-12 (reconnect) are dropped.
