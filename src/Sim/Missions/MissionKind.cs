// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Missions;

/// <summary>Actor mission. Values are persisted in saves and replays; append only.
/// M3 (design 05): EnterTunnel = walk to an entrance and queue (M3-02); Transit = inside the network (M3-02); Burrow = dive and
/// travel underground (M3-03); Surface = surfacing telegraph (M3-03); Deploy = go to the deploy cell and deploy (M3-03);
/// Capture = engineer walking to its target building (M3-06). Until filled they run the idle handler.
/// M4: Garrisoned = inside a building, acquire and fire out (M4-03; walking in reuses Capture).
/// M6-02 (docs/design/10_m6_plan.md §3.1; handlers arrive with M6-03..05, until then they run the idle handler): Salvage = drain a wreck,
/// Pack = packing or unpacking a rig in progress, Withdraw = a raider leaving after a plunder.
/// M7-02 (docs/design/11_m7_plan.md §2.2; handlers arrive with M7-03..09, until then they run the idle handler): Unload = a landing craft
/// putting passengers ashore, Submerge = a submarine diving or surfacing, Sortie = a carrier launching or recovering aircraft,
/// ConvoySail = a supply tender sailing in. Embark reuses Enter.</summary>
public enum MissionKind : byte { None = 0, Sleep = 1, Guard = 2, Move = 3, Attack = 4, AttackMove = 5, Stop = 6,
    Harvest = 7, Return = 8, EnterTunnel = 9, Transit = 10, Burrow = 11, Surface = 12, Deploy = 13, Capture = 14,
    Garrisoned = 15,
    Salvage = 16, Pack = 17, Withdraw = 18,
    Unload = 19, Submerge = 20, Sortie = 21, ConvoySail = 22,
    /// <summary>S19 (FAD-05): fire at the cell in <c>Destination</c> until re-ordered.</summary>
    AttackGround = 23 }
