# C&C Tiberian Dawn: Units, Movement, Combat, Production and Tech Tree

**Summary**

- Everything that differs between units is a row in a static C++ constructor table (`UDATA.CPP`, `IDATA.CPP`, `BDATA.CPP`, `AADATA.CPP`). Behaviour is shared code plus about 30 boolean flags per type. This is the data-driven model to copy.
- Combat is a pipeline: weapon (damage, ROF, range) to projectile type to warhead to a 5-entry armor multiplier table. The projectile is a real simulated object. Splash is limited to a 3x3 cell block with bit-shift falloff.
- Pathfinding is not A*. It is a straight-line walk that, on hitting an obstacle, wall-follows left and right and keeps the shorter. Units store only 9 steps and re-plan. Traffic handling is a patchwork (`MOVE_TEMP`, `MOVE_MOVING_BLOCK`, "scatter").
- Buildability is `(Pre & ownedBuildingBits) == Pre`, an owner-house bit test, and a gate on `Scenario <= mission number` in the campaign or `Level <= BuildLevel` in multiplayer. The campaign tech tree is literally the `Scenario` column.
- Production is lump-cost-drained-over-108 stages. Money is taken a little per stage, and the build stalls (rolls back one stage) when you are broke. Low power multiplies build time by 1.5x to 4x. Each extra factory of a kind adds one stage per tick for human players.
- There is one `FactoryClass` per category (building, vehicle, infantry, aircraft, special) per house. There is no queue in the sim code.
- Difficulty is six per-house multipliers (firepower, armor, ROF, speed, cost, build speed). This is a cheap, effective AI-difficulty lever.

All paths are under `TIBERIANDAWN/`. One game tick is 1/15 s (`DEFINES.H:2146`). One cell is 256 leptons and 24 pixels (`DISPLAY.H:42-52`).

## 1. How a type is defined

**Vehicle: medium tank** (`UDATA.CPP:282-316`, constructor `UDATA.CPP:1406-1500`). The constructor chains to `TechnoTypeClass` (`TYPE.H:395-575`). Field by field:

| Field(s) | Value | What it controls |
|---|---|---|
| id, name, ini `"MTNK"`, explosion | `UNIT_MTANK`, `ANIM_FRAG2` | Enum index, UI string, rules/scenario INI key, death animation |
| `Level` / `Pre` | 3 / `STRUCTF_NONE` | Multiplayer tech gate and building-bitfield prerequisite (section 5) |
| ~26 booleans | crusher, turret, tracked, crew, repairable, buildable, leader, gigundo... | Behaviour switches. `IsCrusher` lets it run over infantry. `IsTurretEquipped` means a separate turret facing. `IsCrew` means survivors may pop out. `IsBuildable` false means scenario-only. `IsLegalTarget` false means never auto-targeted |
| `MaxAmmo` | -1 | Unlimited |
| `MaxStrength` | 400 | Hit points |
| `SightRange` | 3 | Cells revealed |
| `Cost` | 800 | Credits; also the build time in ticks (section 5) |
| `Scenario` | 7 | First campaign mission where it can be built |
| `Risk`, `Reward` | 80, 62 | AI target value and pathing risk (`TechnoClass::Value`, `TECHNO.CPP:3659`) |
| `Ownable` | bitmask `HOUSEF_GOOD` plus multiplayer slots | Which houses may produce it |
| `Primary`, `Secondary` | `WEAPON_105MM`, none | Index into `Weapons[]` |
| `Armor` | `ARMOR_STEEL` | Column of the warhead matrix |
| `Speed` (locomotor) / `MaxSpeed` | `SPEED_TRACK` / `MPH_MEDIUM` (18) | Row of the terrain speed table; top speed byte |
| `ROT` | 5 | Turn rate in 1/256-circle steps per tick (`DRIVE.CPP:1316`) |
| `Mission` | `MISSION_HUNT` | Default order for newly built units |

**Infantry: rocket soldier E3** (`IDATA.CPP:259-292`). It has the same TechnoType fields plus a pointer to a `DO` animation table (rows of start frame, count, jump for stand, walk, fire, die...). It also has a projectile-launch frame, a few flags (female, civilian, fraidycat, can-capture), and no armor argument: all infantry are hard-wired `ARMOR_NONE` (`IDATA.CPP:1644`).

**Building: weapons factory** (`BDATA.CPP:279-337`). Beyond the TechnoType fields it has:

- `IsFactory` and `ToBuild` (`RTTI_UNITTYPE`): the category it produces.
- An exit-point coordinate and an exit-cell list.
- A size enum (`BSIZE_33`) with explicit occupy and overlap cell lists (the foundation footprint).
- `Power` (output) and `Drain` (consumption). The factory has 0/30; the power plant has 100/0.
- `IsBibbed`, `IsCaptureable`, `IsWall`, `IsSimpleDamage` and similar flags.
- `Capacity`: Tiberium storage.

Note `BDATA.CPP:301-305`: HP is `#ifdef ADVANCED 500 #else 200`, and `ADVANCED` is commented out (`DEFINES.H:44`). Alternative balance sets live as compile-time `#ifdef`s next to the data.

## 2. Movement

**Coordinates.** Positions are 32-bit `COORDINATE` values with 8 fractional bits per axis (leptons). Facings are 0..255 (`DirType`). Pathing uses 8 compass facings, sprites use 32.

**Path representation.** `FootClass::Path[CONQUER_PATH_MAX]` is an array of 9 facings (`DEFINES.H:1960`, `FOOT.H:165`). It is a short lookahead list, not a full route.

**Speed math** (`DRIVE.CPP:584-805`, `DRIVE.CPP:1125-1150`). Each tick `While_Moving` adds `MaxSpeed * House->GroundspeedBias * Speed/256` to an accumulator `SpeedAccum`. Here `Speed` is the terrain multiplier (0..255). Every time the accumulator exceeds `PIXEL_LEPTON_W` (10), the unit advances one step along a pre-baked "track": an array of sub-cell coordinate offsets and facings (`RawTracks`, 13 curves, picked by `TrackControl[from_facing*8+to_facing]`, `DRIVE.CPP:1727-2297`). For a medium tank on clear ground, 18 x 0x70/256 = 7.9 units per tick, so about 0.8 px per tick, about 0.5 cells/s. A damaged unit (below half HP) loses 25% of its speed (`DRIVE.CPP:1143-1147`). The "slow down near destination" factors (`0xA0`, `0xD0`, `DRIVE.CPP:1153-1162`) are computed into a local that is never applied afterwards. This is probably dead code (unverified).

**Terrain modifiers.** `Ground[]` (`CONST.CPP:261-269`) is the multiplier by land type and locomotor (the field is misleadingly called `Cost`):

| Land | Foot | Track | Harvester | Wheel | Hover |
|---|---|---|---|---|---|
| Clear / beach / Tiberium | 0.44 | 0.44 | 0.44 | 0.63 | 0.75 |
| Road | 0.75 | 0.63 | 0.63 | 0.63 | 0.75 |
| Water | 0 | 0 | 0 | 0 | 0.75 |
| Rock / wall | 0 | 0 | 0 | 0 | 0 |

A multiplier of 0 means impassable. Boats use the same table, where only water is non-zero. Wheeled vehicles are faster off-road than tracked ones; roads help infantry most.

**Occupying cells.** A vehicle sets `Flag.Occupy.Vehicle` on the cell it is entering before it starts moving, so nothing else can enter (`Overrun_Square` / `Start_Of_Move`, `DRIVE.CPP:886-1290`). Some tracks span two cells (flag `F_D`), and both are checked. Cell flags are one byte (`CELL.H:160-178`): five infantry spots, `Vehicle`, `Monolith` (terrain) and `Building`.

**Infantry sub-cells.** There are five stopping spots per cell, centre plus four quadrants (`StoppingCoordAbs`, `CONST.CPP:59-65`). `CellClass::Closest_Free_Spot` (`CELL.CPP:1657-1720`) picks the nearest free spot using precomputed priority tables and randomises when the centre is requested. Infantry reserve only the spot they are heading to, not the spots they pass over. A cell containing a vehicle or building has no free spots.

**Blockage handling.** `Can_Enter_Cell` (`UNIT.CPP:3133`, `INFANTRY.CPP:1566`) returns one of `MOVE_OK`, `MOVE_CLOAK`, `MOVE_MOVING_BLOCK` (an ally moving), `MOVE_DESTROYABLE` (an enemy or wall you can shoot), `MOVE_TEMP` (a stationary ally) and `MOVE_NO`. When `Start_Of_Move` finds the next cell `MOVE_TEMP`, it makes the blocker scatter (`Incoming(0,true)`) and retries up to `PATH_RETRY`=10 times. If it is `MOVE_DESTROYABLE`, the unit overrides its mission to attack the obstacle. If a head-on friendly mover is in range, it returns `MOVE_NO` and a new path is computed after `PATH_DELAY` (15 ticks). When the player's move target is within about 1.5 cells, the order is simply dropped. This is how units "give up" near crowded destinations.

**Crushing.** `IsCrusher` vehicles drive into enemy infantry cells and delete them if they come within 0x80 leptons (`DRIVE.CPP:226-290`). Infantry in the cell are first told to scatter (depending on difficulty).

## 3. Pathfinding

`FootClass::Find_Path` (`FINDPATH.CPP:528-926`) is a **line-of-sight walk with obstacle edge-following** (the file header says exactly this, `FINDPATH.CPP:20-29`). It is not A*, Dijkstra or any open/closed-list search. The steps:

1. From the current cell, step one cell toward the destination in a straight line (`CELL_FACING`).
2. If the cell is passable (`Passable_Cell`), append the move and continue.
3. If not, scan forward along the line to find the next passable cell. Then run `Follow_Edge` twice from the blocked point, clockwise and counter-clockwise, hugging the obstacle boundary until the line is rejoined (`FINDPATH.CPP:934-1200`). Keep the shorter result. Up to 5 "doughnut" retries.
4. Repeat from the new position until the destination or the list limit.
5. `Optimize_Moves` (`FINDPATH.CPP:1204`) smooths zig-zags into diagonals and removes back-tracks.

**Costs.** `Passable_Cell` (`FINDPATH.CPP:1442`) charges 1 for free cells, 3 for a moving ally, 8 for destroyable and 10 for a stationary ally (`MOVE_NO` is 0, meaning blocked). Terrain speed is not part of the path cost.

**Multiple passes.** `Basic_Path` (`FOOT.CPP:351-552`) calls `Find_Path` several times. First it runs with the most permissive blockage tolerance (the AI tolerates `MOVE_DESTROYABLE`, humans `MOVE_TEMP`). Then it runs with the strictest tolerance (`MOVE_CLOAK`) and prefers the strict path if its cost is under 1.5x the permissive one. Results are passed through `Fixup_Path` (`DRIVE.CPP:1370`).

**Limits.**
- Staging buffers hold 200 steps (`workpath`), the per-edge-follow list holds 300 (`MAX_MLIST_SIZE`) and edge following stops at 400 cells.
- The unit keeps only the first 9 steps (`Path[CONQUER_PATH_MAX]`) and re-plans when it runs out, throttled by `PATH_DELAY`.
- Threat avoidance exists only for AI teams marked `IsRoundAbout`. `Map.Cell_Threat` rejects cells above a threat tolerance (except within 5 cells of the destination), and the tolerance rises in 3 stages if no route is found (`FINDPATH.CPP:100`, `FINDPATH.CPP:625-675`). Normal units use `threat=-1`, meaning none.
- The final copy into `Path[]` uses `path->Length` instead of `path1.Length` (`FOOT.CPP:~536`); the impact is unverified.

**Weaknesses.**
- Paths are not optimal. Long detours around big lakes or cliff mazes can be far worse than the shortest route.
- Every unit paths independently. A big group clogs chokepoints because stationary friends are cost-10 blockers, and the scatter logic pushes them around.

## 4. Combat

**Pipeline.** `Weapons[]` rows (`CONST.CPP:73-100`) are `{bullet type, damage, ROF, range (leptons), sound, muzzle anim}`. The bullet type (`BBDATA.CPP`) holds flags (homing, arcing, high/over-wall, inaccurate, anti-aircraft, invisible), speed, turn rate, arming/fuel range, warhead, and impact anim. The warhead (`CONST.CPP:111-123`) holds spread factor, wall/wood/Tiberium destroyer flags and five armor multipliers.

**Firing.** `TechnoClass::Can_Fire` (`TECHNO.CPP:2088`) checks, in order: legal target, cloak, has weapon, anti-air vs aircraft altitude, `Arm` timer (`FIRE_REARM`), `In_Range`, ammo, own cloak. `Fire_At` (`TECHNO.CPP:2282`) creates a `BulletClass` with `Strength = Attack x House->FirepowerBias`, sets the rearm timer to `ROF x ROFBias + 3` ticks (`TECHNO.CPP:2253`) and spawns the muzzle anim. Two-shooters alternate, and the first shot has only 9 ticks of delay. Range is `Weapon_Range` in leptons: 0x0400 is 4 cells.

**Projectile.** Bullets are real objects (`BULLET.CPP:307-500`). Light-speed invisible bullets (rifles, lasers) spawn at the target; others fly each tick with `Physics`. Arcing shells compute a vertical "riser". Homing missiles re-aim every other frame. Non-homing, non-arcing shots snap their impact coordinate to the target on arrival, so they cannot miss a stationary target. Inaccuracy applies when the bullet is flagged `IsInaccurate`, when the shooter is moving (only with `Special.IsDefenderAdvantage`), or when AP/fuelled weapons target infantry or a bare cell. Homing and arcing shots then scatter by up to a third of the range (max 2 cells); straight shots overshoot by up to 1 cell. Scatter is clamped to the shooter's range (`BULLET.CPP:644-790`).

**Damage formula.** `Explosion_Damage` (`COMBAT.CPP:134-232`) collects up to 32 objects in the impact cell plus its 8 neighbours, with a 384-lepton effective radius. For each (`Modify_Damage`, `COMBAT.CPP:66-110`):

```
damage = round(damage * Warhead.Modifier[armor] / 256)      // Fixed_To_Cardinal
damage >>= clamp(distance >> Warhead.SpreadFactor, 0, 16)    // falloff
```

House `ArmorBias` is applied beforehand (`TECHNO.CPP:3003`). HE does double damage to aircraft. A direct hit on any cell of a building counts as distance 0. The comment in the code promises a 1-point minimum, but `Modify_Damage` has none, and zero damage returns `RESULT_NONE` (`OBJECT.CPP:1397-1398`). If damage would kill, `ObjectClass::Take_Damage` caps it at current HP and triggers destruction (`OBJECT.CPP:1409-1428`).

**Warhead matrix** (x/256; columns none, wood, aluminum, steel, concrete):

| Warhead | None | Wood | Alum. | Steel | Concrete | Spread |
|---|---|---|---|---|---|---|
| SA (bullets) | 100% | 50% | 56% | 25% | 25% | 2 |
| HE | 88% | 75% | 56% | 25% | 100% | 6 |
| AP (tank shells) | 25% | 75% | 75% | 100% | 50% | 6 |
| FIRE | 88% | 100% | 69% | 25% | 50% | 8 |
| LASER | 100% | all | 100% | 100% | 100% | 4 |
| PB | 100% | 100% | 75% | 75% | 75% | 7 |
| HOLLOW_POINT (sniper) | 100% | 3% | 3% | 3% | 3% | 4 |

(Infantry is "none" armor; buildings are mostly wood/aluminum/concrete; tanks steel or aluminum.) Examples: a 15-damage M16 round vs a medium tank does 4; a 30-damage 105mm AP shell vs rifleman does 8 and is also inaccurate. This is classic rock-paper-scissors from one 5-column table.

**Weapon table (selection).** Range cells = leptons/256; ROF is in ticks, so effective cooldown is ROF+3.

| Weapon | Dmg | ROF | Range | Warhead |
|---|---|---|---|---|
| M16 | 15 | 20 | 2 | SA |
| Rifle (Rambo) | 125 | 40 | 5.5 | Hollow-point |
| Chain gun | 25 | 50 | 4 | HE |
| Grenade | 50 | 60 | 3.25 | HE |
| Dragon (rocket) | 30 | 60 | 4 | AP, homing |
| Flamethrower | 35 | 50 | 2 | FIRE |
| 75mm / 105mm / 120mm | 25 / 30 / 40 | 60 / 50 / 80 | 4 / 4.75 / 4.75 | AP |
| Turret gun | 40 | 60 | 6 | AP |
| 155mm artillery | 150 | 65 | 6 | HE, arcing |
| MLRS | 75 | 80 | 6 | HE, homing |
| Obelisk laser | 200 | 90 | 7.5 | LASER |
| Honest John SSM | 100 | 200 | 10 | FIRE |

**Target selection and auto-acquire.**
- *Triggers:* `Mission_Guard` (`FOOT.CPP:636`) calls `Target_Something_Nearby(THREAT_RANGE)` about every second (15 ticks plus 0-4). `THREAT_AREA` scans up to twice weapon range (capped at 0x0A00 leptons); `THREAT_NORMAL` (hunt) scans the whole map (`TECHNO.CPP:3701`).
- *Evaluation:* `Greatest_Threat` (`TECHNO.CPP:1584`) scans in expanding square rings of cells around the shooter (or all objects for map-wide scans). `Evaluate_Object` (`TECHNO.CPP:1333`) rejects allies, limbo, out-of-range, undiscovered, wrong RTTI (mask), cloaked, `!IsLegalTarget`, and unarmed buildings for human AI. Value = `Risk() + Reward + kills`, divided by distance in range units, with a floor of 2.
- *Bias:* Rings bail out early at radius crange/4 or crange/2 if anything has been found (cheaper but "nearest wins"). In the ring scan `bestval` is never updated (`TECHNO.CPP:1688-1730`), so within a ring the last candidate found wins, not the highest value. The map-wide scan does compare and breaks ties randomly. Aircraft are scanned separately because they are not in the cell grid.
- *Retaliation:* `FootClass::Take_Damage` (`FOOT.CPP:1152`) lets idle or guarding units target their attacker. For AI houses they switch to `MISSION_HUNT`. Human units only retaliate if the attacker is in range (otherwise they scatter if scatter is enabled).

## 5. Production and tech tree

**What is buildable** (`HouseClass::Can_Build`, `HOUSE.CPP:553-738`):

1. `IsBuildable` and `(1 << house) & Ownable`.
2. AI houses can build anything legal for them (`HOUSE.CPP:564`).
3. Humans: `flags = ActiveBScan`, a bitfield with one bit per owned, active structure type. Equivalences are patched in: advanced power implies power, Hand of Nod implies barracks, Obelisk implies advanced tower, Temple implies Eye, Airstrip implies weapons factory, SAM implies helipad (`HOUSE.CPP:585-591`).
4. Prerequisite: `(Pre & flags) == Pre`, meaning all required bits.
5. Tech gate: in multiplayer `Level <= BuildLevel`; in the campaign `type->Scenario <= level`, where `BuildLevel = Scenario` (mission number) (`INI.CPP:317-332`). Special-case overrides follow (for example, GDI gets no bazooka before mission 7; MLRS before 9; sandbags before 8).

`Pre` here only names *structures*. The factory is implicit. `Update_Buildables` (`BUILDING.CPP:2398-2475`) runs when a factory is placed or captured. It loops over every type of the factory's `ToBuild` category and adds those passing `Can_Build` to the sidebar. `StripClass::Recalc` (`SIDEBAR.CPP:1975`) removes entries that no building can produce (`Who_Can_Build_Me`, `BDATA.CPP:4454`) and abandons their production.

**Factory model** (`FACTORY.CPP`, `FACTORY.H:96`). `Set` creates the object immediately (in limbo) and records `Balance = Cost x CostBias`. The build is `STEP_COUNT = 108` stages. `Start` sets the stage timer rate to `Time_To_Build / 108` ticks, bounded 1..255. `Time_To_Build` is `Cost` in ticks (`TECHNO.CPP:291-347`), so an 800-credit tank takes about 750 ticks, around 50 s, at full power. On each stage:

```
cost = Balance / (108 - stage)       // Cost_Per_Tick
if (cost > money) stage--;           // stall: can't pay, undo the step
else money -= cost; Balance -= cost;
```

So cost **drains continuously during the build** (`FACTORY.CPP:231-300`). Cancelling refunds `Cost - Balance`, meaning only what was spent. Running out of money pauses progress without losing it. Modifiers to build time:

| Condition | Effect |
|---|---|
| Power fraction (output/drain) < 100% | time x1.5 |
| < 50% | time x2.5 |
| 0 | time x4 (`TECHNO.CPP:331-346`) |
| Extra factories of same kind (humans only) | +1 stage per tick each, so N factories is about N-times faster (`FACTORY.CPP:243-268`) |
| Non-GDI vehicles (airstrip delivery) | -25% (`TECHNO.CPP`) |
| AI on Easy/Normal | cheap buildings slowed, plus an IQ-based slowdown |

`BuildSpeedBias` exists in `HouseClass` but is never used in `Time_To_Build` (only set; unverified elsewhere).

**One item per category.** `HouseClass` has `BuildingFactory`, `UnitFactory`, `InfantryFactory`, `AircraftFactory`, `SpecialFactory` slots (`HOUSE.H:359-363`). A second production in the same category returns `PROD_CANT`. There is no queue in this code. Remastered's "queue count" lives in the sidebar/DLL layer (`DLLInterface.cpp:3850`, unverified).

**Finished objects.** A finished building waits on the sidebar until the player places it (`DisplayClass::Passes_Proximity_Check`, `DISPLAY.CPP:790`: must be adjacent to owned structures or walls). Units and infantry exit from the "primary" factory: `BuildingClass::Toggle_Primary` (`BUILDING.CPP:3543`) marks one `IsLeader` among factories of the same `ToBuild`, and `UnitTypeClass::Who_Can_Build_Me` prefers it (`UDATA.CPP:1831`).

**Power** (`HOUSE.CPP:4669`, `BUILDING.CPP:5180`). `Power` is output and `Drain` is use. Fraction = output/drain, and 1.0 if there is no drain. A building's output scales with its health. Below 100%, every building with `Drain > 0` loses 1 HP per damage interval while above 50% HP (`HOUSE.CPP:1142-1155`), turrets and the Advanced Guard Tower stop working (`BUILDING.CPP:3510`), and radar flickers.

**Repair and sell.** Building repair runs once per second (`Frame % 15`): spends `Repair_Cost` and adds `Repair_Step` (5 HP for buildings, 4 for units) per step. Cost is about 0.4 x (Cost x step/MaxHP)/2, so a full repair costs about 20% of the price. It stops when money runs out (`BUILDING.CPP:1095-1115`, `BDATA.CPP:4766`). Units repair only at a repair bay. Selling refunds `Cost x CostBias`, halved for human players (`TECHNO.CPP:4493`), and plays a reverse construction animation. The helipad's cost excludes the free Orca it comes with.

## 6. Balance data (extracted from the type tables)

Speeds are `MaxSpeed` bytes (higher is faster). Armor: St=steel, Al=aluminum, Wd=wood. "Scn" is the first campaign mission; 98/99 means never by normal means. Side: G=GDI, N=Nod, both=shared.

**Infantry** (all armor None; prerequisite is just a barracks/Hand of Nod)

| Unit | $ | HP | Weapon (dmg/ROF/range) | Speed | Lvl | Scn | Side |
|---|---|---|---|---|---|---|---|
| E1 minigunner | 100 | 50 | M16 15/20/2 | 8 | 1 | 1 | both |
| E2 grenadier | 160 | 50 | Grenade 50/60/3.25 | 10 | 1 | 3 | G |
| E3 rocket soldier | 300 | 25 | Dragon 30/60/4 | 6 | 2 | 3 | both |
| E4 flamethrower | 200 | 70 | Flame 35/50/2 | 10 | 1 | 5 | N |
| E5 chem warrior | 300 | 70 | Chem 80/70/2 | 8 | 7 | 98 | N, needs Eye |
| Engineer | 500 | 25 | none (capture) | 8 | 3 | 2 | both |
| Commando | 1000 | 80 | Rifle 125/40/5.5 | 10 | 7 | 98 | both |

**Vehicles and aircraft**

| Unit | $ | HP | Armor | Speed | Weapon | Pre | Lvl | Scn | Side |
|---|---|---|---|---|---|---|---|---|---|
| Jeep | 400 | 150 | Al | 30 | M60 15/30/4 | - | 2 | 5 | G |
| Light tank | 600 | 300 | St | 18 | 75mm | - | 3 | 5 | N |
| Medium tank | 800 | 400 | St | 18 | 105mm | - | 3 | 7 | G |
| Mammoth tank | 1500 | 600 | St | 12 | 120mm + missiles | Repair bay | 5 | 13 | G |
| Flame tank | 800 | 300 | St | 18 | Flame 50/50/2 | Comm. centre | 4 | 9 | N |
| Stealth tank | 900 | 110 | Al | 30 | Dragon | Comm. centre | 5 | 12 | N |
| APC | 700 | 200 | St | 35 | M60 | Barracks | 4 | 5 | both |
| Artillery | 450 | 75 | Al | 12 | 155mm 150/65/6 | - | 6 | 9 | N |
| MLRS | 800 | 100 | Al | 18 | 75/80/6 | Eye | 7 | 11 | G |
| SSM launcher | 750 | 120 | Al | 18 | Honest John 100/200/10 | Adv. tower | 7 | 98 | N |
| Harvester | 1400 | 600 | Al | 12 | none | Refinery | 2 | 7 | both |
| MCV | 5000 | 600 | Al | 12 | none | Eye | 7 | 15 | both |
| Orca / Apache | 1200 | 125 | St | 40 | Dragon / Chain gun | Helipad | 6 | 10 | G / N |

**Buildings** (power: +output / -drain)

| Building | $ | HP | Armor | Power | Pre | Lvl | Scn | Side |
|---|---|---|---|---|---|---|---|---|
| Construction yard | 5000 | 400 | Wd | +30/-15 | - | - | 1 | both |
| Power plant | 300 | 200 | Wd | +100 | - | 0 | 1 | both |
| Advanced power | 700 | 300 | Wd | +200 | Power | 5 | 13 | both |
| Barracks (G) / Hand of Nod (N) | 300 | 400 | Wd | -20 | Power | 0 | 1 / 2 | G / N |
| Refinery | 2000 | 450 | Wd | +10/-40 | Power | 1 | 2 | both |
| Comm. centre | 1000 | 500 | Wd | -40 | Refinery | 2 | 3 | both |
| Weapons factory (G) / Airstrip (N) | 2000 | 200 / 500 | Al / St | -30 | Refinery | 2 | 5 | G / N |
| Guard tower (chain gun) | 500 | 200 | Wd | -10 | Barracks | 2 | 7 | G |
| Gun turret (turret gun) | 250 | 200 | St | -20 | Barracks | 2 | 8 | N |
| Adv. guard tower (TOW 60/40/6.5) | 1000 | 300 | Al | -20 | Comm. | 4 | 13 | G |
| Obelisk (laser) | 1500 | 200 | Al | -150 | Comm. | 4 | 11 | N |
| Adv. comm. (Eye) / Temple | 2800 / 3000 | 500 / 1000 | Wd / Al | -200 / -150 | Comm. | 7 | 13 | G / N |

## Implications for RTS_2027

- **Keep the data-driven type table, but externalise it.** TD's constructor lists work but are brittle (48 positional args). Use a schema file (TOML/JSON/YAML) loaded at startup, validated, hot-reloadable. This also gives modding for free. Cost: a loader and validation layer.
- **Keep the armor x warhead matrix.** Five armor classes and about seven warheads produce real rock-paper-scissors with just 35 numbers, and it is easy to balance. **Recommendation:** keep exactly this scheme for MVP; add a minimum-damage rule (the original comments promise one, the code does not).
- **Keep the prerequisite bitfield plus tech-level gate; split the two gates cleanly.** `Pre` (what you own) and `Scenario` (campaign unlock) are orthogonal. **Recommendation:** model the campaign unlock as a per-mission "unlocked types" list (equivalent to `Scenario <= mission`), because that is exactly how the campaign gets its mission-by-mission tech progression, and keep a separate `Level` for skirmish presets. Avoid TD's hard-coded exceptions in `Can_Build`; put them in data as per-faction ownership and extra prerequisite lists.
- **Prerequisite equivalence ("Obelisk counts as advanced tower") should be data.** A `provides` list per building type replaces the hard-coded bit fix-ups.
- **Replace pathfinding.** The LOS plus wall-follow design is cheap but gives poor routes and scales badly in crowds. **Recommendation:** A* on the cell grid (8-way, terrain-cost weighted) with a hierarchical or cached layer for long routes, plus group move orders that share one search, and flow fields when many units head to the same target. Preserve TD's `MOVE_*` blockage classes (ally moving, ally stopped, destroyable, impassable) as path-cost tiers; they give nice emergent behaviour like "units shove idle friends aside" and "tanks shoot walls".
- **Improve unit collision.** TD's cell-granular reservation is deterministic and simple, and the 5-spot infantry scheme is cheap and looks good. Keep both. Improvements: reserve the *next* cell earlier, swap places with stopped allies instead of scattering them, and stop re-planning every 9 steps (store the full path, repair locally when blocked).
- **Cost-drain production changes the economy feel.** Spending credits during the build means: (a) you cannot bank-and-burst, and starting an expensive item early costs you gradually; (b) low money slows rather than blocks production, so the economy limits tempo smoothly; (c) cancelling is cheap and safe; (d) multiple parallel categories compete for the same income, which creates real choices. A lump-sum model (as in many modern RTS) is simpler and gives bursty "save up then buy" play. **Recommendation:** adopt TD's drain model; it fits the classic feel the owner wants, is easy to implement (a per-stage charge) and is easy for AI to reason about. Add an optional build queue and a "hold" state in the UI layer, as Remastered did.

## Open questions for the owner

1. **Production model.** (a) TD-style drain over time with stall-on-broke, (b) lump sum at start with timed build, (c) lump sum at completion. Drain is the classic feel; the options trade simplicity for how "tight" the early economy feels.
2. **Build queue and parallelism.** One item per category (TD) vs a queue per category vs one queue per factory building. More factories as a speed-up (TD) or as extra parallel slots (later RTS)?
3. **How faithful should the tech gate be?** (a) campaign mission number unlocks types (TD), (b) buildings you own unlock types (TD skirmish), (c) both with a mission-defined "allowed set". The campaign design (story-driven unlocks) probably wants (c).
4. **Unit crowding.** Keep cell-based reservation (deterministic, cheap, classic traffic jams) or move to continuous steering and unit radii (smoother, harder to make lockstep-deterministic)?
5. **Splash and friendly fire.** TD splash damages everyone in 3x3 cells including allies (the source skips the shooter only). Keep friendly fire, or exclude allies?
6. **Infantry density.** Keep five infantry per cell (TD) or allow more or fewer? It determines squad density and crush behaviour.

## Sources

All under `/home/claude/ref/CnC_Remastered_Collection/TIBERIANDAWN/`:

- Types: `UDATA.CPP:282-316,1406-1500,1831-1850`; `IDATA.CPP:103-300,1644`; `BDATA.CPP:279-337,505-567,4428-4470,4766-4937`; `AADATA.CPP:56-285`; `TYPE.H:395-575,858-948`
- Movement: `DRIVE.CPP:226-290,584-805,886-1290,1370,1727-2297`; `FOOT.CPP:351-552,636-660,1152-1262,1886-1902`; `UNIT.CPP:886-960,3133-3280`; `INFANTRY.CPP:1566-1700`; `CELL.H:160-195`; `CELL.CPP:1606-1720`; `CARGO.CPP:40-180`; `CONST.CPP:59-65,245-270`
- Pathfinding: `FINDPATH.CPP:20-110,528-926,934-1200,1204,1442-1503`
- Combat: `COMBAT.CPP:66-232`; `BULLET.CPP:307-500,644-790`; `BBDATA.CPP:43-510`; `CONST.CPP:73-123`; `TECHNO.CPP:1333-1530,1584-1815,2088-2172,2253-2420,3003-3090,3659-3725`; `OBJECT.CPP:1385-1470`; `MiscAsm.cpp:725-745`
- Production: `TECHNO.CPP:276-347,4493-4510`; `FACTORY.CPP:231-300,395-410,500-640,720-730`; `FACTORY.H:96`; `STAGE.H:60-95`; `HOUSE.CPP:246-264,553-738,1138-1160,2450-2500,4669-4680`; `HOUSE.H:359-363`; `INI.CPP:317-340`; `SIDEBAR.CPP:1975-2030`; `BUILDING.CPP:1038-1115,2398-2475,3011-3045,3529-3560,5180-5186`; `DISPLAY.CPP:790-840`; `DEFINES.H:403-429,612-624,1849-1857,1960,2146`
- Gaps (unverified): infantry foot-movement speed math; Remastered build-queue behaviour; whether the Remastered client overrides these tables from external rules files.
