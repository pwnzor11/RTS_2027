# C&C Event Queue, Lockstep Networking, Determinism and the Remaster DLL Interface

**Summary**

- Every player action in C&C is an `EventClass` (a 1-byte type, a frame number, a house ID and a small union of arguments). UI code never mutates the sim directly; it pushes events into `OutList`. Events are moved to `DoList` and executed at a specific frame by `Queue_AI()`, once per sim tick.
- Single player runs through the *same* queue (`Queue_AI_Normal`), just without the network. The event is stamped with the current frame, so it executes immediately. The recording and attract-mode system is therefore nearly free: save the seed plus each frame's events.
- Multiplayer is classic deterministic lockstep, peer to peer. Each machine sends its commands stamped `Frame + MaxAhead` and also sends a heartbeat (`FRAMEINFO`) every `FrameSendRate` frames. A machine may advance only if it is no more than `MaxAhead` frames ahead of the slowest peer and it has received every command the peers say they sent. There is no rollback and no server. If packets are late the game stalls and shows a reconnect dialog.
- Desync detection is a cheap 32-bit rolling checksum of unit/infantry/building positions and facings plus one RNG value. It is computed every frame, kept in a 32-entry ring, and sent in each heartbeat. A mismatch pops an "Out of Sync" box. There is no state-diff tooling beyond writing `CRCnn.TXT` dumps.
- Determinism rests on a single seeded RNG, a fixed processing order (players in `MPlayerID[]` order, a y-sorted draw layer, fixed-size heaps with stable indices), integer/fixed-point math, and a separate non-synced RNG for cosmetics. Weak spots exist: Tiberian Dawn's `IRandom` uses the C runtime `rand()`, some `float` handicap multipliers touch the sim, and save files do not appear to store RNG state.
- The 2020 Remaster rips the networking out of the game DLL (`CNC_Advance_Instance`, `CNC_Handle_Input`, `CNC_Get_Game_State`). The host passes in input, steps the sim one frame at a time, and reads back flat object lists to render. This is a working proof that sim and presentation can be cleanly separated.


## 1. The event model

**The type.** `EVENT.H:44-223` defines `EventClass`. The event types are a flat enum (`EVENT.H:51-88`):

- Player-originated, in the Tiberian Dawn list: `ALLY, MEGAMISSION, IDLE, SCATTER, DESTRUCT, DEPLOY, PLACE, OPTIONS, GAMESPEED, PRODUCE, SUSPEND, ABANDON, PRIMARY, SPECIAL_PLACE, EXIT, ANIMATION, REPAIR, SELL, SPECIAL`.

**The header fields:**

- `Frame : 27` bits. This is the frame the event must execute on. At 15 fps, 27 bits covers about 100 days of play (the source comment says 25 days, assuming 30 fps).
- `ID : 4` is the originating house. `IsExecuted : 1` marks it as done.
- `MPlayerID` (1 byte) is the multiplayer slot: the high nibble is the colour, the low nibble is the faction.
- A union `Data` carries the arguments: `Target{Whom}`, `MegaMission{Whom, Mission, Target, Destination}`, `Place{Type, Cell}`, `Specific{Type, ID}`, `Special{ID, Cell}`, `FrameInfo{CRC, CommandCount, Delay}`, `Timing{DesiredFrameRate, MaxAhead}`, and others.

**References are small integers.** A `TARGET` is a 16-bit `unsigned short` (`DEFINES.H:1651`). The top 4 bits are a `KindType` (cell, unit, infantry, building, aircraft, bullet, and so on) and the low 12 bits are a heap index (`TARGET.H:62-66`). Events never contain pointers. They refer to objects by kind and heap slot, which is why they can cross a wire or a save file. The 12-bit limit caps each object class at 4,096 instances.

**Click to event.** A mouse click runs the normal UI code (selection, cursor logic). The UI then calls something like `OutList.Add(EventClass(...))` or the wrapper `Queue_Mission(whom, mission, target, dest)` (`QUEUE.CPP:236-246`). Examples:


A group order is just one `MEGAMISSION` event per selected unit. The sender compresses runs of identical orders (section 2). Selection itself is not an event. It is purely local UI state, so it never goes on the wire.

**What an event does.** `EventClass::Execute` (`EVENT.CPP:400-798`) is one big `switch`. It looks up the object from the `TARGET`, checks that it is still alive (`techno->IsActive`), and calls the same methods the AI uses. `MEGAMISSION` calls `Assign_Mission`, `Assign_Target` and `Assign_Destination` (`EVENT.CPP:590-661`). `PLACE` calls `Houses.Raw_Ptr(ID)->Place_Object(...)`. A stale `TARGET` is harmless because the event is silently ignored.


**Queues.** `OutList` holds 256 events (`MAX_EVENTS`, `DEFINES.H:2690`). `DoList` holds 8 times that, 2,048 (`GLOBALS.CPP:419-420`). Both are fixed-size ring buffers (`QueueClass`, `QUEUE.H`). The flow:


Where it sits in the frame (`CONQUER.CPP:1560-1694`): `Map.Layer[LAYER_GROUND].Sort()`, then `Logic.AI()` (all object AI), then `Queue_AI()` (events), then `Frame++`, then `Sync_Delay()` waits out the remainder of the 1/15 s tick. Events issued during frame N therefore take effect at the end of frame N and are seen by the logic of frame N+1.

## 2. One multiplayer frame, step by step

The core is `Queue_AI_Multiplayer` (`QUEUE.CPP:514-766`). Its header comment (`QUEUE.CPP:423-500`) states the design. Default parameters: 15 sim ticks per second (`TICKS_PER_SECOND`, `DEFINES.H:2146`), `FrameSendRate = 3`, so a packet goes out every 3 frames or 5 per second. `MPlayerMaxAhead` starts at `FrameSendRate*3 = 9` frames for internet play (`INIT.CPP:1737`) and is retuned during play.

Per frame:

1. **Frame 0.** Send a `FRAMESYNC` that includes the *scenario CRC*. Then block in `Wait_For_Players(first_time)` until every peer has been heard from. A scenario CRC mismatch gives "scenarios do not match" and aborts (`QUEUE.CPP:585-648`, `1649-1651`).
2. **Every 128 frames** (`QUEUE.CPP:653`), every machine broadcasts a `PROCESS_TIME` event: its average CPU ticks per sim frame. The host (`MPlayerID[0]`) then computes a `TIMING` event (`QUEUE.CPP:1157-1250`) containing:
   - `DesiredFrameRate = min(60/slowest_machine_ticks, user_setting)`.
   - `MaxAhead = (round_trip_ticks * fps) / (2*60)`, rounded up to a multiple of `FrameSendRate` and at least `3*FrameSendRate`.
   This *is* the frame-rate negotiation. The whole group runs at the speed of its slowest member, and the command delay tracks measured network latency.
3. **Compute the game CRC** for this frame and store it in `CRC[Frame & 31]` (`QUEUE.CPP:683-684`).
4. **Send period check.** If `Frame` is not a multiple of `FrameSendRate`, skip the send and wait (`Process_Send_Period`, `QUEUE.CPP:1328-1348`). Otherwise continue.
5. **Send packets** (`Send_Packets`, `QUEUE.CPP:1377-1482`):
   - Each event in `OutList` is stamped `Frame = ceil((Frame + MaxAhead)/SendRate)*SendRate`, `ID` and `MPlayerID` are set, and the event is added to *our own* `DoList` with the same future frame (`QUEUE.CPP:2411-2431`). The local player also waits for the delay, so there is no local advantage.
   - A `FRAMEINFO` header always goes first: `{Frame, ID, CRC = GameCRC, CommandCount = total commands sent so far, Delay = MaxAhead}`.
   - If the packet has commands it requires an ACK (`ack_req=1`). A pure heartbeat is fire-and-forget.
6. **Wait to advance** (`Wait_For_Players(0)` then `Can_Advance`, `QUEUE.CPP:1820-1863`). The frame may advance only if both hold:
   - `Frame < min(their_frame[i]) + MaxAhead` (`their_frame` is derived as `packet.Frame - packet.Delay`), and
   - `their_recv[i] >= their_sent[i]` for every peer, meaning every command that peer claims to have sent has arrived.
   The second rule is the important one. Command packets are reliable, but heartbeats are not. If a command packet is lost and a later heartbeat arrives, the `CommandCount` field reveals the hole, and we refuse to advance until the retry fills it.
7. **Execute** the `DoList` (section 3 covers ordering and the CRC check), then clean it.

**Late packets and pausing.** While waiting, the loop (`QUEUE.CPP:845-1041`):
- resends a `FRAMESYNC` every `MaxAhead*8` ticks to break deadlocks where each side is waiting on a lost heartbeat,
- shows a reconnect dialog after `max(3 * response_time, 180)` ticks (3 s),
- on timeout (`25*60` ticks, 25 s, or 6x that on the internet) calls `Handle_Timeout`, which drops the offending connection. The game continues without that player.

If an event arrives whose `Frame` is already in the past, that is fatal. `Execute_DoList` shows "Packet Too Late" and stops the game, after writing `toolate.txt` (`QUEUE.CPP:2932-2939`, `4184-4213`). The lockstep guarantee is that this can only happen if `MaxAhead` was lowered or a peer cheated. A raised `MaxAhead` could cause it too, so `TIMING` events that increase it reschedule any event in the vulnerable window to its end (`EVENT.CPP:749-753`, `QUEUE.CPP:2871-2879`).

**Packet format.** The wire packet is a "meta-packet" of at most `546 - sizeof(CommHeaderType)` bytes, rounded down to whole events (`GLOBALS.CPP:896`). That keeps it under typical MTU limits. `CommHeaderType` is `{magic u16, code u8, packetID u32}` (`CONNECT.H:212-216`). In `COMM_PROTOCOL_MULTI_E_COMP`, the compressed protocol:

- Events are written as `[type][only the bytes in EventLength[type]]`.
- Runs of identical `MEGAMISSION` orders (same mission, target and destination) are collapsed into one full record, a repeat count, then only the 2-byte `Whom` for each further unit (`QUEUE.CPP:2286-2520`). Sending 100 infantry to one spot costs about 200 bytes plus one record.


**ACK and retry** live in the connection layer (`CONNECT.H`, `SEQCONN.CPP`, `COMQUEUE.*`):

- Every packet carries a monotonically increasing `PacketID` equal to the sender's total send count. The receiver treats an ID below its receive count as a resend and re-ACKs it without delivering it twice (`CONNECT.H` header comment).
- `Service_Send_Queue` (`SEQCONN.CPP:393+`) works strictly in order. It looks at the head of the send ring. If ACKed, it records the round-trip time and pops it. Otherwise it resends when `now - LastTime > RetryDelta`. Only the head is serviced, which is simple, in-order and head-of-line blocking. It is effectively a stop-and-wait ARQ per peer.


**What "latency" means here.** Input lag is a *fixed command delay*: every action, including your own, executes `MaxAhead` frames later (9 frames = 0.6 s by default; the host retunes it). The sim never sees an inconsistent view, but the player feels a constant command delay. A slow peer or slow link slows everyone down (the `min(their_frame)` rule), and a long outage freezes everyone.

## 3. Determinism and desync detection

**Execution order.** `Execute_DoList` (`QUEUE.CPP:2887-3065`) loops over players in `MPlayerID[]` order, which is identical on every machine, and inside that over events in queue order, running those with matching `MPlayerID` whose frame is due. Two events from different players for the same frame are thus always applied in the same order, regardless of packet arrival order. AI houses (`!IsHuman`) do not appear in this loop, since their decisions come from deterministic AI code running inside `Logic.AI()`.

**Randomness.**

- Tiberian Dawn: `IRandom(min,max)` masks `rand()` and rejection-samples (`WIN32LIB/IRANDOM.CPP:51-65`). The seed is set once per game via `srand(Seed)` and `RandNumb = Seed` (`INIT.CPP:1632-1634`). In multiplayer `Seed` is agreed in the connection dialogs; in single player it is `rand()` seeded from `timeGetTime()` (`INIT.CPP:1602-1606`). Weakness: it depends on the C runtime's `rand()`, so the same seed on a different compiler or CRT would desync. It is safe in practice only because all peers run the same binary.
- Red Alert replaced this with its own `RandomClass`, a linear congruential generator (`Seed = Seed*0x41C64E6D + 0x3039`, return `(Seed >> 10) & 0x7FFF`, `REDALERT/RANDOM.H`, `RANDOM.CPP`), so it is fully self-contained.
- There is a *second* RNG for presentation. Tiberian Dawn has `Sim_Random()`, a 256-entry shuffled-byte table with a wrapping index (`RAND.CPP`), used for voice and scream choices. Red Alert has `NonCriticalRandomNumber`. These are never in the CRC, so cosmetic code can consume numbers freely without breaking sync. The name `Sim_Random` is misleading: it is for the *non*-sim.

**Iteration order.** The code explicitly sorts `Map.Layer[LAYER_GROUND]` by y every frame, with a comment that it is done "outside the IsToRedraw check, for the purposes of game sync'ing between machines" (`CONQUER.CPP:1565-1572`). Without it, per-machine redraw flags would change the order of object processing. Objects live in fixed-size indexed heaps (`TFixedIHeapClass`, `HEAP.CPP`), so creation order and slot reuse are deterministic.

**Floating point.** The sim is overwhelmingly integer: coordinates are 16-bit "leptons" (256 per cell), facings are 0-255, TARGETs are packed integers. A grep for `float`/`double` found no float math in movement, targeting or pathfinding. The exceptions are the house handicap multipliers (`FirepowerBias`, `ArmorBias`, `CostBias`, `GroundspeedBias` and so on, `HOUSE.H:68-76`, `RULES.H:44-53`), which are `float` and multiplied into costs (`FACTORY.CPP:402`, `588`). These are deterministic if every peer uses the same compiler and FPU mode, which is exactly the fragile assumption a modern project must avoid.

**Desync detection.**

- *What is hashed.* Tiberian Dawn's `Compute_Game_CRC` (`QUEUE.CPP:3323-3383`) folds `Coord + PrimaryFacing` of every infantry, every unit (plus `SecondaryFacing`) and every building, then one `rand()` value, using a trivial rotate-and-add: `crc = (crc<<1) + val + hibit` (`Add_CRC`, `QUEUE.CPP:3402-3417`). The Red Alert version also hashes speed, `NavCom`, mission, `TarCom`, vessel strength, house credits, power and drain, all map layers, and the RNG seed (`REDALERT/QUEUE.CPP:3799-3880`).
- *How often.* Every frame is computed and kept in a 32-entry ring (`CRC[Frame & 0x1f]`). It is shipped in every `FRAMEINFO` heartbeat, so effectively every `FrameSendRate` frames.
- *How compared.* When a peer's `FRAMEINFO` becomes due (its `Frame` equals ours), we look up our own CRC for `Frame - Delay`, the frame the sender computed it on. Mismatch means a desync (`QUEUE.CPP:3019-3049`). Stale heartbeats (`Frame != current`) are ignored because internet packets arrived up to 35 frames late and produced bogus errors. The `Delay < 32` check guards the ring index.
- *Limits.* The hash is weak (position and facing only in Tiberian Dawn, so a credits or health desync stays hidden until it propagates into movement) and it detects but does not localise. There is no automatic first-divergence search.

## 4. Single player uses the same queue

Yes, and this is the most reusable lesson. `Queue_AI()` (`QUEUE.CPP:318-346`) dispatches by `GameToPlay`. For `GAME_NORMAL` it runs `Queue_AI_Normal` (`QUEUE.CPP:371-407`):

1. Move `OutList` to `DoList`.
2. If `RecordGame`, call `Queue_Record()`.
3. `Execute_DoList`. In single player this simply runs every event with `Frame <= current` (`QUEUE.CPP:2849-2857`).
4. `Clean_DoList`.

The single-player `EventClass` constructors stamp `Frame = ::Frame` (`EVENT.CPP:178-184`), so the event runs at the end of the current tick. No delay, no network, but the identical code path.

**Recording and playback** are built on this. `Queue_Record` writes, per frame, the count of due events and then raw `EventClass` structs (`QUEUE.CPP:3140-3165`). A header written once (`Save_Recording_Values`, `INIT.CPP:2790-2830`) holds the seed, scenario, player and house setup, `Special` flags, options, `FrameSendRate` and `CommProtocol`. `Queue_Playback` (`QUEUE.CPP:3190-3305`) reads those events back into `DoList`, runs `Execute_DoList` and also computes the per-frame CRC. The attract mode is simply a playback that quits on mouse movement. Replays work because the sim is a pure function of (seed, initial scenario, event stream).


## 5. Save and load

Saving serialises the sim object by object (`Save_Game`, `SAVELOAD.CPP:145-306`):


`TFixedIHeapClass::Save` (`HEAP.CPP:409-445`) writes the active count, then for each live object its *slot index* followed by the object's own `Save`. Loading puts each object back into the same slot so that every encoded `TARGET` still resolves. Fixed heaps plus stable indices are what make this whole scheme work: a `TARGET` is both a network reference and a save-file reference.


## 6. The Remaster DLL interface

The 2020 Remaster runs the 1995 sim as a DLL called by a separate engine (GlyphX). The interface (`DLLInterface.h:121-152`, version in `DLLInterfaceVersion.h`) is plain `extern "C" __declspec(dllexport) __cdecl` functions:

The exported functions (`DLLInterface.h:121-152`) are plain `extern "C" __cdecl` calls: lifecycle (`CNC_Init`, `CNC_Start_Instance`, `CNC_Save_Load`), stepping (`CNC_Advance_Instance`, one frame per call), input in (`CNC_Handle_Input` with mouse requests and coordinates, plus `_Sidebar_`, `_Structure_`, `_Unit_`, `_SuperWeapon_`, `_ControlGroup_` requests), state out (`CNC_Get_Game_State` for map, layers, sidebar, placement, shroud), and a C callback for sound, speech, game-over, messages and camera events.

**Sim stepping.** `CNC_Advance_Instance` (`DLLInterface.cpp:1443-1670`) is the original main loop with everything blocking removed: `Map.Input`, layer sort, `Logic.AI()`, `Queue_AI()` (or `Glyphx_Queue_AI` for multiplayer), win/lose checks, `Frame++`. The `Sync_Delay()` pacing call is commented out. The host decides the tick rate.

**Rendering is decoupled by interception.** `Get_Layer_State` (`DLLInterface.cpp:3327+`) walks the ground and air layers and calls each object's *original* `Draw_It`, but a hook (`DLL_Draw_Intercept`) captures the call instead of blitting pixels. The result is a flat array of `CNCObjectStruct` (`DLLInterface.h:283-286`) for the host to draw with its own renderer. Each record holds the asset name, position, frame index, owner colour, health, `SortOrder`, selection mask, `SimLeptonX/Y` and draw flags. Pips and health bars come through similar intercepts. Shadows are reordered by swapping sort values. So the sim stays sprite-aware and remains the authority on *what* to draw, but never touches pixels. The legacy software renderer still exists but is gated by `Legacy_Render_Enabled()`.

**Multiplayer in the DLL.** The peer-to-peer queue is replaced. `Glyphx_Queue_AI` (`DLLInterface.cpp:6027+`) still moves `OutList` to `DoList` and executes it, but with no packet exchange, no CRC and no MaxAhead. The code comments say it was a "conversion from peer/peer to client/server" (`DLLInterface.cpp:156`, `6167`). GlyphX, which is not in this repository, presumably owns the lockstep or server logic (unverified). Because original code relies on a global `PlayerPtr` ("the local player"), `Set_Player_Context(player_id)` swaps that pointer before handling each player's input, so the events carry the right `ID`.

**Lessons.**

- The command boundary survived 25 years: input arrives as high-level requests that become `EventClass`es, and the sim has no direct dependence on the keyboard or mouse.

## Implications for RTS_2027

- **Every player action is a serialisable command from day one.** UI produces a `Command`; only the sim's `ApplyCommand` mutates state. Cost in single player is near zero; it buys replays, test harnesses, an AI that uses the player's verbs, and LAN later. **Recommendation:** copy the `OutList` to `DoList` to `Execute(tick)` shape as one small `CommandQueue` module.
- **Stamp commands with an execution tick even in single player** (`exec_tick = tick + delay`, delay 1 in SP, 2-4 on LAN). One path means single player constantly exercises the lockstep path. A 1-tick delay at 15 Hz (66 ms) is imperceptible.
- **Reference objects by (kind, index, generation), not pointers.** C&C's 16-bit `TARGET` plus fixed heaps is why save, network and replay all work. Add a generation counter (C&C lacks one and relies on `IsActive` checks) so stale handles to reused slots are caught.
- **Fixed sim rate, sim as a library, renderer reads snapshots.** The Remaster's `Advance_Instance` plus `Get_Game_State` is the model: `Sim::step(commands)` and `Sim::snapshot()`. No rendering or wall-clock calls in sim code; the renderer interpolates between ticks.
- **Determinism rules** (consistent with the hard rule in `00_START_HERE.md`): (1) one sim RNG with our own documented algorithm, never the C runtime, its state inside the sim and the hash; (2) a separate, never-hashed presentation RNG; (3) integer or fixed-point only, including handicap and cost multipliers (use percent integers); (4) no wall-clock, threads or hash-map iteration order in sim; process entities in index order; (5) AI runs inside the tick using only sim state and sim RNG; (6) commands ordered by player slot, then arrival sequence.
- **Better desync detection than C&C, still cheap.** **Recommendation:** every N ticks (say 16) hash all sim state (positions, health, orders, money, power, RNG) with xxHash or CRC32, keep a ring, compare on peer report. In debug builds hash per subsystem so the first mismatching one is named, and dump a diff. C&C's position-only hash hid divergence.
- **Replay format is nearly free.** **Recommendation:** header (game version, rules/content hash, scenario, seed, slots, factions, difficulty) then `(tick, player, command)` records plus a state hash every N ticks. Never record state. Headless playback gives regression tests and AI soak tests.
- **Saves must include RNG state** (C&C appears not to), versioned, with stable entity handles.
- **LAN lockstep, deferred but cheap.** Use reliable TCP (or UDP plus thin acks): this removes `SEQCONN` retry code and the `CommandCount` hole detection, which exists only because heartbeats were unreliable. Each client sends its commands for tick `T+delay` every tick (empty if none); the sim advances to `T` when all peers' lists arrive. Fixed delay, not C&C's adaptive `MaxAhead` negotiation (only worth it for internet play).
- **Do not port `QUEUE.CPP` or the connection stack.** Copy the ideas (event struct, future-tick stamping, per-slot order, ring-buffered hash, record/playback) and re-implement the roughly 300-line essence. The code is entangled with globals, in-loop dialogs, IPX, modem and mono-screen debugging. Any port would also need `docs/LICENSING.md` registration. The megamission run-length compression is the only literal-port candidate, and not needed on a LAN.

## Open questions for the owner

1. **Replays: design constraint only, or an early feature?** Options: (a) commands plus seed only, no UI; (b) record and playback early as a test tool; (c) player-facing. Recommend (b).
2. **Sim tick rate: 15, 20 or 30 Hz?** Lower is cheaper for big armies and matches C&C's feel but needs render interpolation; higher is smoother but costs pathfinding and AI CPU.
3. **Should the skirmish AI issue commands through the same queue as humans?** (a) yes: replayable, fair, uniform; (b) AI mutates sim directly as C&C's `Logic.AI()` does: faster to write, invisible in replays.
4. **Fixed-point approach:** hand-written integer math, or a library, depending on the language chosen in the other packages?
5. **Mid-mission saves required?** If yes, all state including RNG and AI memory must be serialisable, which constrains AI design.

## Sources

Paths under `/home/claude/ref/CnC_Remastered_Collection/` (Tiberian Dawn unless marked RA):

- `TIBERIANDAWN/EVENT.H:44-223`; `EVENT.CPP:53-110, 160-185, 400-798`.
- `TIBERIANDAWN/QUEUE.CPP`: 318-407 (Queue_AI, Normal); 423-766 (multiplayer and its header comment); 800-1053 (Wait_For_Players); 1157-1348 (timing events, send period); 1377-1655 (Send_Packets, FrameSync, Process_Receive_Packet); 1820-1863 (Can_Advance); 2108-2520 (packet building, compression); 2839-3117 (Execute_DoList, Clean_DoList); 3140-3305 (record, playback); 3323-3435 (CRC); 4184-4213 (too-late dump).
- `TIBERIANDAWN/CONQUER.CPP:1407-1450, 1560-1694` (frame loop); `CONNECT.H:30-216`; `SEQCONN.CPP:393-460`; `COMQUEUE.H`; `IPXMGR.CPP`, `NULLMGR.CPP` (largely `#if (0)`).
- `TIBERIANDAWN/GLOBALS.CPP:419-420, 594-601, 895-896`; `DEFINES.H:1651, 2146, 2532-2536, 2690`; `INIT.CPP:862, 1576-1634, 1737-1742, 2790-2830`; `TARGET.H:40-100`; `HEAP.CPP:409-445`; `SAVELOAD.CPP:145-306, 593-870`.
- `TIBERIANDAWN/WIN32LIB/IRANDOM.CPP:36-100`; `RAND.CPP`; `FUNCTION.H:784-797`; `HOUSE.H:68-76`; `RULES.H:44-53`; `FACTORY.CPP:402, 588`.
- `TIBERIANDAWN/DLLInterface.h:60-152, 283-286, 413-429, 560-640`; `DLLInterface.cpp:156, 1443-1670, 3327-3420, 3513+, 6027-6200`.
- RA: `REDALERT/EVENT.H:53-101`; `QUEUE.CPP:3799-3882`; `RANDOM.H`, `RANDOM.CPP:95-125`; `GLOBALS.CPP:284`.

Not verified: exact `sizeof(EventClass)`; GlyphX internals (not in the repo); whether any save path stores RNG state beyond what a grep of `SAVELOAD.CPP` showed.
