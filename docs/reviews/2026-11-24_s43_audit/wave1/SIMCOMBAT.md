# SIMCOMBAT — combat, effects, superweapons, veterancy, stealth/submerge, garrison
Scope read: src/Sim/Simulation.Combat.cs, .Effects.cs, .Superweapon.cs, .Veterancy.cs, .Submerge.cs, .Garrison.cs, .Naval.cs (all in full); src/Sim/Vision/VisionState.cs (first half); Missions/CombatMissions.cs; parts of Simulation.cs (StepCore, RunCleanup, UpdateVision, FxFor), Simulation.M3.cs, Simulation.Ground.cs (stealth, IsHiddenFrom), Simulation.Transport.cs (capture), Simulation.Salvage.cs (VehicleWorth), Naval/NavalState.cs, Veterancy/VeterancyState.cs; data/rules weapons/warheads/vehicles/buildings/game.toml (targets, veterancy, superweapon keys). No tests read, nothing run except small toml scripts.

## Findings
### SIMCOMBAT-01 · MED · bug
What: Capturing a charged superweapon building hands the captor the victim's charge. CaptureBuilding changes the owner but never resets `Effects.BuildingCharge`; the charge is cleared only when a building is placed or removed.
Where: src/Sim/Simulation.Transport.cs:206-218 (CaptureBuilding); src/Sim/Effects/EffectsState.cs:125 (ClearBuilding, called only from OnM3BuildingPlaced / OnM3BuildingRemoving, Simulation.M3.cs:154-176); CanFireSuperweapon, Simulation.Superweapon.cs:~176.
Evidence: read. EngineerActionOn (Transport.cs:171-178) allows capture of any enemy building under CaptureBelowPermille health, with no exclusion for superweapon types. I found no `BuildingCharge` write anywhere else (grep). Not run.
Impact: an Engineer who takes a Sunrise Device, Atomic Battery or Salvo Array that is already ready (a likely event once it is under attack) gets a ready superweapon at once and can fire it on the next tick. This is a snowball swing the design never mentions. For the Sunrise Device the captor also needs an entrance within range.
Fix idea: reset BuildingCharge[b] = 0 in CaptureBuilding (or block capture of superweapon types).   Changes a match outcome / number / visible text?: yes (only in the capture case)

### SIMCOMBAT-02 · MED · bug
What: Superweapon area damage (the blast and each Salvo missile) does not spare garrisoned soldiers. D-205 says splash spares carried units "like every other area effect" (SIM-RULES-05). Only the normal Detonate path got that fix.
Where: ApplySpecial loop Simulation.Superweapon.cs:~92-101 and SalvoImpact :~150-162 only skip `IsUnderground` (Tunnel, Burrowed, Surfacing, Passenger; Core/ActorLayers.cs:9-10). `Garrisoned` is not in IsUnderground. Compare Detonate, Simulation.Combat.cs:400-401, which skips Garrisoned explicitly.
Evidence: read. A garrisoned soldier sits at the building centre (Garrison.cs:~64), so its Chebyshev distance is that of the building. It then takes the full 700 blast damage directly, on top of the building being hit.
Impact: garrisons in a bunker or ruin inside the blast radius die even though ordinary splash cannot touch them. Inconsistent with the garrison-shield rule and with the Salvo comment "spared like splash". The numbers are frozen, but the behaviour differs from what D-205 decided.
Fix idea: also skip `_tunnels.LayerOf == Garrisoned` (or IsCarried) in both loops.   Changes a match outcome / number / visible text?: yes (superweapon vs garrison)

### SIMCOMBAT-03 · LOW · bug
What: The two superweapon paths disagree about dived submarines, and the blast's own header comment disagrees with the code. SalvoImpact skips `IsHiddenSub` (comment "spared like splash, D-142"), but ApplySpecial (Sunrise, Atomic) has no such skip. The blast then hurts a hidden sub using Submerged armour (versus column 100 for both blast warheads). The header also says "every non-friendly surface actor", but nothing excludes aircraft.
Where: Simulation.Superweapon.cs:160 (Salvo) vs :92-101 (blast), header lines 21-23.
Evidence: read.
Impact: dived Expedition subs inside a nuke radius die while the same subs under a Salvo do not. Aircraft overhead also take 700.
Fix idea: decide the rule and make the two loops share one eligibility helper.   Changes a match outcome / number / visible text?: yes

### SIMCOMBAT-04 · LOW · bug
What: A unit left with Health <= 0 at the end of RunCleanup acts for one more tick. Mission handlers and Fire have no health check. RunCleanup visits Vehicle, then Infantry, then Building in slot order, and a death weapon or garrison ejection can kill an actor that was already passed (or one in a later kind pass). That actor is then removed next tick.
Where: Simulation.cs:697-725 (RunCleanup order), Simulation.Effects.cs OnDeathWeapon (applies damage at once during cleanup), Simulation.Garrison.cs:246 (eject damage applied while the building pass runs after infantry), Simulation.cs:RunMissions (no Health test), Combat.cs Fire (no Health test; only RunBuildingCombat checks health).
Evidence: read. Reasoned from the code; not run.
Impact: the dying unit fires one extra volley, can earn a veterancy kill, and sits one tick as a 0-HP actor that can still be selected. Eject damage (EjectDamagePermille default 200, GarrisonRules.cs) can also kill an already wounded soldier after the infantry pass has finished, which gives the same one-tick zombie.
Fix idea: skip Health <= 0 in RunMissions(store,kind) and in Fire.   Changes a match outcome / number / visible text?: yes (very rarely)

### SIMCOMBAT-05 · LOW · bug
What: The LE-05 fix (a dived sub that fires is revealed for 5 s) makes the depth charge unusable at exactly that moment. `Submerged`-targeting weapons test `IsHiddenSub`, which is false while RevealLeft > 0. The Patrol Launch (targets = "submerged", its only weapon) therefore cannot be aimed at a just-fired, visible sub, and the Launch keeps a stale AttackTarget it cannot fire at.
Where: Simulation.Submerge.cs:59-69 (WeaponTargetsActor: `case Submerged: return sub;` with `sub = IsHiddenSub`); Combat.cs:250 (Fire returns false); data/rules/weapons.toml:431-438 (exp_launch_charge); vehicles.toml:731-746 (Launch has no sonar, only exp_destroyer has sonar_radius, line 800).
Evidence: read. `NavalAndSubmerged` is the only enum value that covers both states, and no weapon in data uses it. The Launch is the sole `submerged` weapon.
Impact: for 5 s after a missile salvo the Launch does nothing (guard keeps the target and Engage chases but Fire refuses). Other ship guns (default targets) can hit the revealed sub, so the effect is small.
Fix idea: use `IsSubmergedShip` (or the dive flag) for the `Submerged` case.   Changes a match outcome / number / visible text?: yes (Launch vs a firing sub)

### SIMCOMBAT-06 · LOW · bug
What: Stealth timer after a shot is off by one when the reload is shortened. Fire sets `VehicleStill/InfantryStill = 0`. RunStealth then re-detects "fired" with `ReloadTicks >= RateOfFire` (Ground.cs:~148). A veteran or aura reload below the base rate fails that test, so the counter ticks to 1 in the same tick.
Where: Simulation.Ground.cs:~146-152 vs Combat.cs:267-268.
Evidence: read. Not run.
Impact: the stealth delay after firing is one tick shorter for ranked or boosted shooters. The s18 #15 problem itself (veteran stealth units staying hidden after firing) is fixed because Fire resets the counter directly.
Fix idea: drop the `fired` re-test; Fire already resets the counter.   Changes a match outcome / number / visible text?: no (practically)

## Suspects (unproven)
- Explicit Attack orders ignore fog on purpose (DECISIONS M1-09, "ignore fog") but IsValidTarget does not check vision, so an Attack-order unit chases a fleeing enemy through fog with exact position. Not a bug, it is a decision; flagged only because it works as a scouting leak against a hidden-in-fog target. Nothing to prove.
- Direct-fire projectiles have fixed destination and no homing (decision). SAM, AA missiles and rockets have projectile_speed 128 with splash 0, so a jet moving at 60 leptons per tick will miss the aim point unless it is still. Whether AirMission hovers while attacking decides if AA can ever hit moving jets. Prove with a sim run of Jet vs SAM.
- Stealth: Fire no longer breaks stealth only via the still counter; I did not check IsStealthRevealed side cases (detector range) beyond reading.
- AcquireTarget is O(all actors) per scan per unit with a sonar scan (IsSonarRevealed, O(units+buildings)) inside IsHiddenFrom for every hidden-sub candidate. Fine at current counts; perf only if many subs.
- Vision: I read only the first 200 lines of VisionState (Reveal/Conceal/counts are incremental and consistent). I did not read the save/load part or ghost handling beyond UpdateVision.

## Checked and fine
- s18 §2.1 #8 (Authority Atomic Battery cannot fire): FIXED. buildings.toml:569 `superweapon_entrance_range = 0`.
- s18 §2.1 #4 (garrisoned soldiers obey orders): FIXED (Simulation.cs:375-384 refuses orders to carried units).
- s18 §2.2 #12 (splash ignores air/ground, damage-0 sprays): FIXED. Combat.cs:403 and :407 (air/ground filter), :279 and :288 (damage 0 = 0).
- s18 §2.2 #13 (splash hurts passengers): FIXED in Detonate (Combat.cs:401); superweapons still miss it, see SIMCOMBAT-02.
- s18 §2.2 #14 (dived sub invulnerable, surfaced sub keeps submerged armour): partly changed as D-205 LE-05/LE-06: a firing sub is revealed 5 s (Combat.cs:267) and armour is submerged only while hidden (Simulation.Actors.cs:84). A dived unrevealed sub is still untouchable without sonar (D-143, by design).
- s18 §2.2 #15 (veteran stealth fires without breaking stealth): FIXED (Fire resets still counter).
- s18 §2.2 #21 (kill value of tender, Command Ship, packed rig): FIXED via VehicleWorth (Simulation.Salvage.cs:83).
- Dead-unit references: ActorRef carries the generation; TryGetActorInfo, ApplyPendingDamage, PendingHit.Shooter and OnVeterancyKill all check `IsAlive(id)`. OnCombatActorRemoving also clears AttackTarget and projectile targets. A reused slot cannot receive an old target's hit. ClearMobile resets kill value, so a new unit in a reused slot starts at rank 0.
- Veterancy does trigger: [veterancy] enabled = true, thresholds 600/1600/3200; shooter travels Fire -> projectile -> Detonate -> Hurt -> PendingHit -> OnVeterancyKill; only the `veteran = false` types (5 non-damaging vehicles) opt out. Hero self-heal path (RunVeterancy) is sound. Credit is lost only if the shooter dies before the shell lands (documented).
- Simultaneous damage (D-065): buffer empty between ticks; ApplyPendingDamage is also flushed inside ApplySpecial, RunSalvos and OnDeathWeapon with no carry into a save. RunSalvos flushes mid-tick, before RunCombat (Simulation.cs:266), so on salvo-impact ticks earlier queued mission hits land slightly early. Deterministic, so not a desync.
- Salvo RNG: two draws per missile even for off-map cells; deterministic. Slot reuse when all 4+ salvo slots are busy is deterministic.
- Superweapon charge: integer accumulator, band 3 pauses, defeated owner stops, charge reset at order. The five fire conditions in CanFireSuperweapon match the cursor check. Sunrise range 8 entrance is intentional (Directorate has tunnels); LB and Expedition use 0.
- Fog: FxFor filters events to visible cells (Simulation.cs:861-868). Stealth and hidden-sub targeting go through IsHiddenFrom; sonar and seismic reveal are team-wide.
- Garrison: enter, eject, flip-neutral and revert-empty paths read clean; occupants fire from footprint with range bonus; Garrisoned layer is untargetable and spared by splash; building hit passes a garrison share via Hurt.
- Combat/ammo: Fire ammo and OneUse (Kite) handling consistent; Kite bomb has projectile_speed 0 so it is instant.
