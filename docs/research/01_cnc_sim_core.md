# C&C Tiberian Dawn: How the Simulation Core Is Built

All paths are under `/home/claude/ref/CnC_Remastered_Collection/TIBERIANDAWN/` unless stated.

## Summary

- The sim is a deep single-inheritance-plus-mixins class hierarchy. Ten layers run from `AbstractClass` to `UnitClass`. Each layer adds a slice of state and overrides a virtual `AI()` that chains to its parent.
- Orders are a small state machine. `MissionClass` holds a `MISSION_*` enum, a queued mission, a `Status` integer and a countdown timer. Each `Mission_xxx()` virtual runs a `switch (Status)` and returns how many ticks to sleep.
- Objects talk through "radio" messages (`RADIO_HELLO`, `RADIO_DOCKING` and so on). That is how a harvester and a refinery negotiate docking. It is a synchronous virtual call with a return code, not an event bus.
- Objects live in fixed-size heaps (`UNIT_MAX 500`, `BUILDING_MAX 500`, and so on) and refer to each other through a 16-bit `TARGET` (4-bit kind + 12-bit index) wherever a reference must survive a save. Saves are raw struct dumps, and pointers are converted to `TARGET`s first.
- Space has three units. A `CELL` is a 6+6-bit map index on a 64x64 grid. A `COORDINATE` is 32 bits with 256 leptons per cell on each axis. A `DirType` is 0-255 for 360 degrees. All of it is integer math.
- The loop runs at 15 logic frames per second. A global `Frame` counter drives all timers. The original interleaves input and rendering with the sim on one thread. The Remastered `CNC_Advance_Instance()` shows the sim can be stepped on its own.
- The design is deterministic, with lockstep multiplayer as its goal, but it is full of 1995 idioms. The ideas are portable. The code mostly is not.

## 1. Object model

### Class hierarchy

```
AbstractClass            Coord, IsActive            ABSTRACT.H   (126 lines)
 └ ObjectClass           IsDown/IsInLimbo/IsSelected/Strength/Next(cell chain)/Trigger   OBJECT.H
    ├ OverlayClass, SmudgeClass            (map decoration; no AI to speak of)
    ├ AnimClass (+StageClass)              explosions, fire, effects
    ├ TerrainClass (+StageClass)           trees
    ├ BulletClass (+FlyClass,+FuseClass)   projectiles
    └ MissionClass       Mission, MissionQueue, Status, Timer     MISSION.H
       └ RadioClass      Radio* (one contact), LastMessage        RADIO.H
          └ TechnoClass  (+Flasher,+Stage,+Cargo,+Door,+Crew mixins)
             │           House*, TarCom, ArchiveTarget, PrimaryFacing, Arm, Ammo, Cloak
             ├ BuildingClass                        BUILDING.H
             └ FootClass    NavCom, Path[9], Team*, PathDelay        FOOT.H
                ├ InfantryClass
                ├ AircraftClass (+FlyClass)
                └ DriveClass   TrackNumber, smooth-turn movement     DRIVE.H
                   └ TurretClass  SecondaryFacing
                      └ TarComClass  turret targeting
                         └ UnitClass   (UNIT.H; vehicles, harvesters)
```

The chain is taken from the `class X : public Y` lines in each header: `MISSION.H:45`, `RADIO.H:52`, `TECHNO.H:51-57`, `FOOT.H:51`, `DRIVE.H:44`, `TURRET.H:40`, `TARCOM.H:46`, `UNIT.H:50`, `BULLET.H:43`, `AIRCRAFT.H:43`.

What each layer adds:

- **AbstractClass** has a `COORDINATE Coord`, an `IsActive` flag (the "is this heap slot in use" bit), `Distance()`/`Direction()` helpers and an empty virtual `AI()` (`ABSTRACT.H:55-114`). It has no virtual `Draw`.
- **ObjectClass** adds the "placed on the map" machinery: `IsDown`, `IsInLimbo`, `IsSelected`, `Strength`, an intrusive `Next` pointer for the per-cell object chain, and a `Trigger*`. The virtuals cover the whole game surface: `What_Action`, `Mark`, `Limbo`/`Unlimbo`, `Occupy_List`, `Draw_It`, `Render`, `Active_Click_With`, `Take_Damage` and more (`OBJECT.H:58-262`). Selection, clicking and drawing are mixed into the sim class. This is the main coupling to undo.
- **MissionClass** adds orders (below).
- **RadioClass** adds a single contact pointer and the message protocol.
- **TechnoClass** is the "thing owned by a house that fights". It is the largest layer at 4,825 lines. It adds house ownership, weapons/arming, cloaking, ammo, targeting and the sight/threat logic. It inherits five mixins: `FlasherClass`, `StageClass` (animation frame timer), `CargoClass` (passenger list), `DoorClass` and `CrewClass`.
- **FootClass** adds mobility: `NavCom` (movement destination as a `TARGET`), a 9-step `Path[]` of facings (`CONQUER_PATH_MAX 9`), team membership and path-find throttling. Pathfinding itself lives in `FINDPATH.CPP`.
- **DriveClass** adds track-following movement for vehicles (`TrackNumber`, `Smooth_Turn`, `While_Moving` in `DRIVE.CPP`). **TurretClass** adds an independent turret facing. **TarComClass** adds turret aiming and target acquisition. **UnitClass** adds the vehicle-specific bits: harvester logic, the MCV, APC unloading and the Tiberium load.

### The virtual `AI()` chain

Each frame, every "sentient" object gets one `AI()` call. Each override first calls its parent, then does its own work. For example:

- `UnitClass::AI` (`UNIT.CPP:376`) calls `Commence()` if idle in a good spot, calls `TarComClass::AI()`, then does unit-specific work such as MSAM reload timers and deleting off-map idlers.
- `DriveClass::AI` (`DRIVE.CPP:1290`) calls `FootClass::AI()`, then advances the movement track, or the in-place rotation, or starts a new track if `NavCom` is set.
- `TechnoClass::AI` (`TECHNO.CPP:1881`) calls the `Cargo`, `Radio` and `Door` AIs, then handles cloaking, `Arm` (rate-of-fire cooldown), the stage animation timer and target scanning.
- `MissionClass::AI` (`MISSION.CPP:185`) is the dispatcher. If `Timer.Expired() && Strength > 0`, it switches on `Mission` and calls the matching virtual `Mission_Xxx()`.

Rendering concerns leak in. `TechnoClass::AI` calls `Mark(MARK_CHANGE)` to flag redraws (`TECHNO.CPP:1888-1960`), and `LogicClass::AI` touches `Map.Flag_To_Redraw`.

### The MissionClass state machine

There are 21 missions (`DEFINES.H:486-512`). The main ones:

| Mission | Meaning |
|---|---|
| SLEEP / STICKY | Do nothing. STICKY is never recruited into teams. |
| GUARD / GUARD_AREA | Idle but shoot at anything in range. GUARD_AREA is the AI's active variant. |
| ATTACK / HUNT / TIMED_HUNT | Seek and destroy. TIMED_HUNT waits first (used for skirmish AI). |
| MOVE / RETREAT / RETURN | Go somewhere. RETURN means go home to the refinery. |
| HARVEST / UNLOAD | Harvester and APC behaviours. |
| ENTER / CAPTURE / SABOTAGE | Cooperative or hostile entry into a building. |
| CONSTRUCTION / DECONSTRUCTION / REPAIR / MISSILE | Building-only states. |

How an order becomes a mission:

1. A player click or a scenario script calls `Assign_Mission(order)`. It only writes `MissionQueue = order` and returns. It deliberately does not change anything immediately (`MISSION.CPP:331-336`).
2. `Commence()` (`MISSION.CPP:297`) promotes the queue: `Mission = MissionQueue; Timer = 0; Status = 0;`. Callers invoke it only when the object is at a "good place to change", for example `UnitClass::AI` calls it only when `!IsDriving`. This lets a vehicle finish crossing a cell before turning.
3. Each `Mission_Xxx()` returns the number of ticks until it should run again. The default is `TICKS_PER_SECOND * 30`, which means "sleep". Active states return 1 to run next frame, or a longer delay to save CPU.

`UnitClass::Mission_Harvest` (`UNIT.CPP:2707-2830`) is a good example of the style. It is a local `enum {LOOKING, HARVESTING, FINDHOME, HEADINGHOME, GOINGTOIDLE}` plus `switch (Status)`. Each case does a small step, sets the next `Status` and returns a delay. There is no scripting language. States are hand-written `switch` blocks. `Override_Mission`/`Restore_Mission` (`MISSION.CPP:411-450`) let a temporary mission interrupt and later resume the old one, using `SuspendedMission`.

### RadioClass: object-to-object protocol

`RadioClass` holds one `RadioClass* Radio` contact. `Transmit_Message(msg, param, to)` calls `to->Receive_Message(this, msg, param)` directly and returns a `RadioMessageType` reply. `RADIO_HELLO` is the handshake. It first sends `OVER_OUT` to any current partner, then establishes mutual contact if the target answers `ROGER` (`RADIO.CPP`, `Transmit_Message`). There are 34 message types in `DEFINES.H:1600-1639`, each with a plain-English gloss ("Please pick me up", "You are clear to unload").

The harvester-to-refinery docking dialogue, traced:

1. The harvester's `FINDHOME` state calls `Find_Best_Refinery()` and `Transmit_Message(RADIO_HELLO, nearest)`. If the reply is `ROGER` it moves to `HEADINGHOME` and then to `MISSION_ENTER` (`UNIT.CPP:2790-2830`). The refinery had to pass `RADIO_ARE_REFINERY` first. That check returns `NEGATIVE` if the refinery is busy, in limbo, or on another team (`BUILDING.CPP:355-361`).
2. When the harvester arrives it sends `RADIO_DOCKING`. The refinery's `Receive_Message` (`BUILDING.CPP:284-355`) lights the "full" animation, makes sure it is in contact, asks `RADIO_NEED_TO_MOVE`, and answers with `RADIO_MOVE_HERE` plus a cell one step SW of its centre.
3. When the harvester is in position the refinery sends `RADIO_TETHER` then `RADIO_BACKUP_NOW`. The harvester backs in, unloads and sends `RADIO_UNLOADED`. `RADIO_OVER_OUT` ends the contact on both sides.

The same protocol runs transports loading passengers, repair-depot service, helipad landing and the hand-to-hand "box" taunts. It is flexible. Its weakness is that behaviour is scattered across two classes' `switch` statements, with implicit ordering assumptions.

## 2. Game loop

- **Rate.** `TICKS_PER_SECOND` is 15 and `TIMER_SECOND` is 60 (`DEFINES.H:2146-2149`). `Options.GameSpeed` defaults to `TIMER_SECOND / TICKS_PER_SECOND` = 4 hardware ticks (`OPTIONS.CPP:80`). Multiplayer negotiates a `DesiredFrameRate` (`QUEUE.CPP:1196-1207`).
- **Sequence in `Main_Loop()`** (`CONQUER.CPP:1469-1697`):
  1. Set `FrameTimer`.
  2. `Map.Input()` polls input and `Map.Render()` draws.
  3. `Map.Layer[LAYER_GROUND].Sort()`. This is done every frame, even if nothing redraws, so that all machines iterate in the same order.
  4. `Logic.AI()` runs the sim.
  5. `Queue_AI()` executes the events due this frame.
  6. Win/lose checks.
  7. `Frame++` (`CONQUER.CPP:1666`).
  8. `Sync_Delay()`.
- **`LogicClass::AI`** (`LOGIC.CPP:165-340`) runs in this order:
  1. Crate spawn.
  2. All `Teams`.
  3. Every object in the logic vector (`obj->AI()`, with an index fix-up so deleting during iteration doesn't skip anyone).
  4. A pass that sets per-house "what unit types exist" scan bits.
  5. `Map.Logic()`.
  6. `Factories` (production).
  7. `Houses` (the house-level AI and economy).
- **The logic list is a vector of pointers.** `LogicClass : LayerClass : DynamicVectorClass<ObjectClass*>` (`LOGIC.H:42`). `Unlimbo()` submits objects to it only if their type has `IsSentient` (`OBJECT.CPP:Unlimbo`). Update order is therefore order of arrival, which matters for determinism.
- **Timers are frame-keyed.** `TCountDownTimerClass` stores `Started = Frame` and a delay (`FTIMER.H:43-75`). Cooldowns, reload times and mission sleeps are counted in game frames, not wall time, so they are deterministic and survive a pause.
- **Render/logic coupling.** In the 1995 loop, `Sync_Delay()` (`CONQUER.CPP:1423-1446`) spins until `FrameTimer` hits zero. While it spins it keeps reading input and calling `Map.Render()`. So rendering is a "fill the idle time" activity. It is not decoupled, and there is no interpolation. It only repaints dirty objects. If sim takes longer than 1/15 s, the frame simply runs long and the game slows.
- **The Remastered port proves the split.** `CNC_Advance_Instance()` (`DLLInterface.cpp:1443-1560`) steps one logic frame (`Logic.AI()` then `Queue_AI()`) with all drawing stubbed out. A separate GlyphX renderer reads game state back through the DLL interface. That file is 7,817 lines. It shows the sim could be isolated, but only with heavy glue.

## 3. Memory and references

- **Fixed heaps.** Every class has a global `TFixedIHeapClass<T>` (`GLOBALS.CPP:62-76`) sized by constants in `DEFINES.H:1936-1951`:

  | Class | Max |
  |---|---|
  | Units | 500 |
  | Infantry | 500 |
  | Buildings | 500 |
  | Aircraft | 100 |
  | Anims | 200 |
  | Bullets | 50 |
  | Terrain | 500 |
  | Factories | 32 |
  | Houses | 12 |
  | Teams | 60 |
  | Triggers | 80 |

  `INIT.CPP:117-146` calls `Set_Heap(N)` once at startup. `operator new` is overridden per class to call `Units.Allocate()` and `Set_Active()` (`UNIT.CPP:1116`). `delete` just clears the slot. There is no malloc during play.
- **Allocation policy.** `FixedHeapClass::Allocate` returns the first free slot via `FreeFlag.First_False()` (`HEAP.CPP:187-200`). A new object can reuse the slot of one destroyed in the same frame, so a bare slot index is ambiguous. Remastered added `IsRecentlyCreated` for exactly this (`ABSTRACT.H:66-78`).
- **`TARGET`.** It is an `unsigned short`. The top 4 bits are a `KindType` (`KIND_UNIT`, `KIND_BUILDING`, `KIND_CELL`...) and the low 12 bits are an index (`TARGET.H:36-60`, `DEFINES.H:1651`). `As_Target(cell)` builds a cell target. `As_Unit(target)` and similar look up `Units.Raw_Ptr(index)` and optionally check `IsActive` (`TARGET.CPP`, e.g. `As_Bullet` at line 165). `TARGET_NONE` is 0. A `NavCom` or `TarCom` can be either an object or an empty map cell.
- **Why pointers are avoided for references that persist.** `TarCom`, `NavCom`, `ArchiveTarget`, trigger and team links are `TARGET`s. A `TARGET` can be copied around, compared, networked and saved without fix-up, and one that points at a dead object resolves to NULL instead of dangling. Hard pointers (`House*`, `Team*`, the radio contact, `Next`) exist but are converted in `Code_Pointers()`.
- **Save/load.** `Save_Game` (`SAVELOAD.CPP:145`) writes `Map`, then each heap, `Logic`, each layer, `Score`, plus the `Frame` counter (`SAVELOAD.CPP:616`). Each object is dumped with `Write_Object(this, sizeof(*this))`. Loading does placement-new to restore the vtable and then reads the raw bytes (`IOOBJ.CPP:150-175`). Before saving, `Code_Pointers()` walks all heaps converting pointers to `TARGET`s (`SAVELOAD.CPP:799-830`). After loading, `Decode_Pointers()` converts back. The `SaveLoadPadding` arrays in `AbstractClass` (8 bytes) and `ObjectClass` (16 bytes) are reserved so layouts can grow without breaking old saves.
- **Consequences.**
  - State is contiguous, bounded and trivially snapshot-able.
  - Heap slot order is deterministic.
  - Saves are tied to the exact struct layout and compiler, so saves break across builds.
  - Hard limits (500 units) are visible gameplay constraints. Even so, `EACH_UNIT_MAX` caps each player at 125 (`DEFINES.H:1962`).

## 4. Space

- **Types** (`DEFINES.H:1648-1651`): `COORDINATE` is `unsigned long`. `CELL` is `signed short`. `TARGET` is `unsigned short`.
- **Map.** `MAP_CELL_W/H` are `1<<6` = 64 each, so 4,096 cells (`DEFINES.H:269-277`). `CELL = y*64 + x` (`Cell_X = cell & 0x3F`, `Cell_Y = cell >> 6`, `FUNCTION.H:747-748`). A scenario uses a sub-rectangle of the 64x64 grid.
- **Leptons.** A `COORDINATE` packs Y into the high 16 bits and X into the low 16. In each half, the high byte is the cell number and the low byte is the sub-cell offset, 0-255 leptons (`FUNCTION.H:197-198, 741-758`). So there are 256 leptons per cell (`CELL_LEPTON_W 256`) and 24 pixels per cell (`ICON_PIXEL_W 24`), about 10.7 leptons per pixel (`DISPLAY.H:42-53`). `Cell_Coord()` gives the centre (offset 0x80). `Coord_Snap()` snaps to a cell centre. `Adjacent_Cell()` uses lookup tables `AdjacentCell[]`/`AdjacentCoord[]` (`CONST.CPP:144-170`). Packing and unpacking use byte-pointer tricks that assume little-endian layout. A clean port should use two `int16` values instead.
- **Facings.** `DirType` is an unsigned byte, 0-255 = 360 degrees, with `DIR_N=0`, `DIR_E=64`, `DIR_S=128`, `DIR_W=192` (`DEFINES.H:2111-2130`). Eight-way `FacingType` is `dir >> 5`. `Facing8/16/32[256]` lookup tables quantise to sprite frames (`CONST.CPP:173-250`). `FacingClass` (`FACING.H`) keeps `CurrentFacing` and `DesiredFacing` and rotates a fixed number of units per tick, which gives smooth turning with zero floating point. `Desired_Facing256` is an assembler/table atan.
- **Distance.** `Distance_Coord` is a Watcom inline-asm approximation (`FUNCTION.H:868-907`). It is cheap and deterministic, but must be rewritten.
- **CellClass** (`CELL.H:44-302`) is a plain struct, roughly 40-50 bytes. It holds:
  - `TType`/`TIcon`: the terrain template and tile index.
  - `Overlay`/`OverlayData`: walls, Tiberium (the amount is `OverlayData`) and concrete.
  - `Smudge`/`SmudgeData`: craters and scorch marks.
  - `Owner`.
  - `OccupierPtr`: head of a singly linked list of objects in the cell, chained through `ObjectClass::Next`.
  - `Overlapper[3]`: objects that visually overlap the cell.
  - Flags: `IsMapped`, `IsVisible`, `IsTrigger`, `IsWaypoint`, `IsFlagged`.
  - Per-player masks (`IsMappedByPlayerMask`, `IsVisibleByPlayerMask`) added in the Remastered build.
  - An 8-bit `Occupy` bitfield: five infantry spots (Center, NW, NE, SW, SE) plus Vehicle, Monolith and Building bits. Up to five infantry share a cell.
- **MapClass inheritance** (`MAP.H:43`, `DISPLAY.H:58`...): `GScreenClass : VectorClass<CellClass>`, then `MapClass`, `DisplayClass`, `RadarClass`, `ScrollClass`, `MouseClass`, `PowerClass`, `SidebarClass`, `TabClass`. A single global `Map` object is simultaneously the cell grid, the viewport, the radar and the sidebar. The cell array is therefore buried under the UI inheritance chain.
- **Occupancy.** `Mark(MARK_DOWN)` (`OBJECT.CPP`, `ObjectClass::Mark`) sets `IsDown`, adjusts a regional threat rating for AI, and flags the object for redraw. Placing it in cells is done through `Map.Occupy_Down/Up(cell, object)` and `Overlap_Down/Up`, driven by the type's `Occupy_List()` (a short list of cell offsets, used for multi-cell buildings). Moving units mark up and down as they cross cells.
- **Layers.** `LayerType` has `LAYER_GROUND`, `LAYER_AIR` and `LAYER_TOP` (`DEFINES.H:758-764`). `DisplayClass::Layer[]` are `LayerClass` vectors of object pointers, kept y-sorted for painter's-algorithm drawing. `Submit/Remove` are called from `Unlimbo`/`Limbo`. A layer is a draw-order list, but ground-layer order leaks into sim ordering, which is why `Main_Loop` sorts it before `Logic.AI`.

## 5. Data-driven type classes

- Every object class has a parallel `*TypeClass` in `TYPE.H` (2,016 lines):

  ```
  AbstractTypeClass -> ObjectTypeClass -> TechnoTypeClass -> { BuildingTypeClass, UnitTypeClass,
                                                              InfantryTypeClass, AircraftTypeClass }
  ObjectTypeClass -> { BulletTypeClass, AnimTypeClass, TerrainTypeClass, TemplateTypeClass,
                       OverlayTypeClass, SmudgeTypeClass }
  ```

  Plus `WeaponTypeClass`, `WarheadTypeClass` and `HouseTypeClass`.
- `TechnoTypeClass` (`TYPE.H:395`) carries the shared stat block: `SightRange`, `Cost`, build `Scenario`/`Level`, `Pre` (prerequisite structure bitmask), `Risk`/`Reward` (AI valuation), `MaxSpeed` (`MPHType`), `MaxAmmo`, `Ownable` (house bitmask), `Primary`/`Secondary` (`WeaponType` index), `Armor`, `Strength` and a dozen `Is*` flag bits. `UnitTypeClass` adds `Speed` (locomotion), `ROT`, `TurretOffset`, default `Mission`, `Explosion` and flags such as `IsCrusher`, `IsToHarvest` and `IsLockTurret` (`TYPE.H:858+`).
- Instances are `static UnitTypeClass const` objects built by a very long positional constructor (`UDATA.CPP`, e.g. the light tank at lines 225-280). That is about 30 anonymous booleans with trailing comments, then numbers: strength 300, cost 600, `WEAPON_75MM`, `ARMOR_STEEL`, `SPEED_TRACK`, `MPH_MEDIUM`, ROT 5, default mission `MISSION_HUNT`. They are then collected in `UnitTypeClass::Pointers[UNIT_COUNT]` (`UDATA.CPP:1359`), and `As_Reference(type)` indexes into it.
- The live object holds a `Class` reference to its type. `UnitClass::Class->MaxSpeed` and the like are looked up at run time. Only mutable state lives in the instance.
- Weapons and warheads are plain static tables in `CONST.CPP:73-130`. Example: `{BULLET_APDS, 25 damage, 60 ROF, 0x0400 range (leptons), VOC_TANK2, ANIM_MUZZLE_FLASH}` for the 75mm gun. Warhead tables hold the armour-versus-damage multipliers.
- The global tuning knobs are in `RulesClass`/`DifficultyClass` (`RULES.H:40-275`, `RULES.CPP`): AI attack interval, base size, power thresholds and the `*Bias` multipliers per difficulty (firepower, speed, armour, ROF, cost, build speed). In TD these are compile-time defaults and are not read from an INI file (`RULES.CPP` is only 126 lines, constructors with defaults). Red Alert later externalised more. The biases are `float`, which matters for determinism.
- Scenario content (placement, triggers, teams, waypoints) is read from INI files at load. Stats are in code. That is the main contrast with later data-driven engines.

## 6. Code quality notes

Approximate sizes (`wc -l`):

| Subsystem | Lines | Notes |
|---|---|---|
| Object classes (Abstract through Unit/Building/Bullet/Anim, mixins) | ~39,500 | Mixed sim, UI and draw code. TECHNO 4.8k, BUILDING 5.5k, UNIT 4.4k, INFANTRY 3.3k, AIRCRAFT 3.6k |
| Houses, teams, triggers, factories, scenario, specials, power | ~19,000 | Economy, AI, scripting |
| Map, cells, pathfinding, display, radar, sidebar | ~18,900 | Heavily UI-entangled |
| Heap, vector, target, save/load | ~5,100 | Small and reusable |
| Type data tables, `CONST.CPP`, `RULES` | ~19,500 | Mostly data |
| Networking and event queue (QUEUE, EVENT, IPX, modem, TCP...) | ~31,900 | Lockstep command queue plus obsolete transports |
| Main loop, init, startup, globals | ~9,600 | |
| `DLLInterface.cpp` (Remastered glue) | 7,800 | Shows how a sim/render split was bolted on |
| Whole top-level directory (`.CPP/.H`) | ~221,000 | `WIN32LIB` adds ~33,000 |

**Reusable ideas and code as-is:**
- `TARGET` encoding, frame-keyed timers (`FTIMER.H`), `FacingClass`, the 256-direction `DirType` with lookup tables, the cell and lepton packing as a concept, the `Occupy` bitfield, `RadioMessageType` semantics, and the static weapon and warhead tables.
- `HEAP.H` and `VECTOR.H` are generic enough to translate, but a modern language replaces them with an arena or slotmap.

**Tied to DOS/Win32/1995:**
- Watcom `#pragma aux` inline asm (`FUNCTION.H:871`) and 9 `.ASM` files.
- `GScreen`/`HidPage` software blitting.
- Mono-screen debug dumps.
- IPX, modem and serial transports.
- `WWMouse`, palette cycling, `Call_Back()` message pumping.
- Language-specific `#ifdef GERMAN/FRENCH` message boxes.
- Little-endian byte-pointer coordinate tricks.
- `new`/`delete` overloads plus raw `memcpy` saves with vtable fix-up.
- Hundreds of globals (`Frame`, `Map`, `PlayerPtr`, `Units`...).
- Per-class `Validate()` and `CHEAT_KEYS` debug scaffolding.

**Determinism hazards visible in the source:**
- `rand()` appears in `UNIT.CPP:2377`. `Sim_Random` is a 256-entry fixed table (`RAND.CPP:55`) and the gameplay `Random()` is a separate PRNG in `WIN32LIB/IRANDOM.CPP`. Two RNG streams exist.
- `float` biases in `DifficultyClass`.
- Update order depends on layer sort and vector insertion order. This is why the sort-before-AI comment exists.

## Implications for RTS_2027

- **Do not port the class hierarchy as-is.** The ten-deep chain means any behaviour change touches `TECHNO` (4.8k lines) and `UNIT` (4.4k). The multiple-inheritance mixins and the sim, UI and draw code in one class make it hard to test. A port would also inherit the `Map` god-object.
- **Per-object `AI()` is the right shape.** One virtual or function call per entity per tick, timer-gated, chained by state, is easy to reason about. The key observation is that behaviour is a state machine with a sleep timer, and this works whether it is in a class or a system.
- **ECS or hierarchy?** With 500-ish units per side, the C&C-style entity count does not need ECS for performance. ECS buys composability (give any entity a turret), testability and a clean sim/render split, at the cost of cross-entity protocols such as radio being harder to write. For an owner who wants to learn architecture, the middle path fits: plain structs of components in typed arrays, with an explicit per-kind `update()` function, no deep inheritance. **Recommendation:** use data-oriented composition (components in slot arrays), not inheritance and not a general-purpose ECS framework, and keep a per-entity mission state machine with `Status` and a wake-up tick.
- **Keep the mission model.** Queue-then-commence (`Assign_Mission`/`Commence`) is a clean answer to "when does an order take effect". `Mission` + `Status` + return-ticks-to-sleep is cheap, readable and replayable. **Recommendation:** port this idea directly. Use explicit state enums per mission rather than integers, and write the radio protocol as typed request/response pairs instead of 34 shared messages.
- **Keep the coordinate design.** 64x64 cells, 256 leptons per cell and a 0-255 angle are cheap, integer-only and lockstep-safe. Choose a wider range if you want bigger maps (6 bits per axis caps at 64x64, and TARGET's 12-bit index would need widening). **Recommendation:** keep cell plus sub-cell integer positions and 256 angle steps, but use 32-bit (or two int16) fields and a map size chosen up front (128x128 is plenty).
- **Keep `TARGET`, widen it.** A typed handle (kind plus index, ideally with a generation counter) is what makes saves, networking and replays simple. C&C's version lacks generations and was bitten by slot reuse (hence `IsRecentlyCreated`). **Recommendation:** use index+generation handles for every cross-entity reference, and never store a raw pointer in sim state.
- **Fixed heaps are a determinism and save-game tool.** Fixed capacity gives predictable slot order, no allocator variance and trivial snapshots. The cost is hard caps and layout-coupled saves. **Recommendation:** use preallocated slot arrays with an explicit free-list order (lowest index first) and serialise by field, not by memcpy, so saves survive refactors. Use a version number per save.
- **Separate sim from presentation from day one.** C&C's loop paints during idle time, and the Remastered port needed 7.8k lines of glue to undo that. **Recommendation:** a headless `Sim::step()` at a fixed 15 Hz with its own RNG and frame counter, and a renderer that reads state and interpolates between the last two frames. This also makes AI testing and replays cheap.
- **Put unit stats in data files.** C&C's positional constructors with 30 unnamed booleans are the worst part for modders and balance work. **Recommendation:** store types in TOML/JSON/INI loaded at start, with named fields, and keep the weapon/warhead/armour matrix as a table.
- **Take determinism seriously even without LAN play.** Floats in the biases, two RNG streams and sort-dependent update order are the traps. They matter for replays and bug repro even in single-player. **Recommendation:** integer or fixed-point sim, one seeded PRNG owned by the sim, and a state hash checked every N frames in debug builds.

## Open questions for the owner

1. **Language and ECS appetite.** Do you want to learn ECS as a goal in itself (more rewarding to learn, more code to write), or would you rather have a simple "arrays of structs plus update functions" design that is closer to the C&C shape? Options: (a) data-oriented structs, (b) an ECS library, (c) a faithful class hierarchy.
2. **Map size.** Is 64x64 enough for your campaign maps and skirmish? Options: keep 64x64 (small, fast, authentic feel), 128x128 (room for bigger skirmishes), or variable up to 256x256 (needs wider handles and cell indices).
3. **Is LAN multiplayer a hard constraint on the sim?** If yes, lockstep determinism (integers, one RNG, hash checks) must be designed in now. If it is only "maybe later", you could allow floats and wall-clock movement but would pay for a rewrite. Options: lockstep-safe from the start, or sim-first and retrofit.
4. **Fidelity versus freedom of the unit model.** Do you want C&C's rigid feel (cell-reserved movement, one vehicle per cell, five infantry per cell, 8 sprite facings)? Or something looser (free sub-cell movement, steering)? The first is much easier to make deterministic and to path-find.

## Sources

All in `/home/claude/ref/CnC_Remastered_Collection/TIBERIANDAWN/`:

- Hierarchy: `ABSTRACT.H:55-126`, `OBJECT.H:58-262`, `MISSION.H:45-133`, `MISSION.CPP:185-352, 411-450`, `RADIO.H:52-105`, `RADIO.CPP`, `TECHNO.H:51-176`, `TECHNO.CPP:1881-2060`, `FOOT.H:51-180`, `DRIVE.CPP:1290-1330`, `UNIT.CPP:376-420, 1116, 2505-2830`, `BUILDING.CPP:284-361`
- Loop: `CONQUER.CPP:1405-1760`, `LOGIC.H:36-56`, `LOGIC.CPP:165-340`, `QUEUE.CPP:330-415, 1196-1207`, `DLLInterface.cpp:1433-1560`, `INIT.CPP:107-146, 1595-1632`, `FTIMER.H:40-75`, `RAND.CPP:40-110`
- Memory: `HEAP.H:30-348`, `HEAP.CPP:187-200`, `TARGET.H:30-100`, `TARGET.CPP:140-260`, `GLOBALS.CPP:62-76`, `SAVELOAD.CPP:92-140, 616, 776-830`, `IOOBJ.CPP:150-175`
- Space: `DEFINES.H:269-288, 486-512, 758-765, 1600-1652, 1936-1963, 2111-2170`, `FUNCTION.H:197-198, 741-771, 868-907`, `CELL.H:44-302`, `MAP.H:43`, `DISPLAY.H:42-88`, `LAYER.H`, `FACING.H`
- Types: `TYPE.H:50-130, 232-420, 858-1000`, `UDATA.CPP:225-285, 1359`, `CONST.CPP:73-250`, `RULES.H:40-120`, `RULES.CPP`
- `wc -l` over the file groups in section 6.

Unverified: `Distance_Coord`'s exact approximation (inline asm not read), per-cell byte size of `CellClass` (estimated), and whether Remastered changed update ordering beyond what is cited.
