// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>
/// The skirmish start (D-062, D-067; M3-01 per faction): every non-neutral player gets its faction's start kit
/// (<see cref="SimRules.StartKits"/>, game.toml [faction.&lt;name&gt;]): the kit's construction yard at (or as near as possible
/// to) its map start position, then the kit's start units on the nearest free passable cells around it, plus the economy's
/// starting cash (set by the sim). With the default Authority kit (auth_conyard, no units) this is exactly the M2 start.
/// A kit is resolved only for factions actually present, so a rules set may name types of an absent faction that it does
/// not define yet. Shared by the game's skirmish screen, the AI harness and CI. Call before the first Step.
/// </summary>
public static class SkirmishStart
{
    /// <summary>Id of the Authority starting structure (the default Authority kit's conyard).</summary>
    public const string StartBuildingId = "auth_conyard";

    /// <summary>Largest ring searched around the start for start units.</summary>
    private const int MaxUnitRing = 8;

    /// <summary>Places each player's start building at the map start of the same index. Returns the placed ids by player (None if no spot was found).</summary>
    public static EntityId[] Apply(Simulation sim) => Apply(sim, null);

    /// <summary>
    /// Which map start each of <paramref name="players"/> players uses (M4-11): all starts in order when the map has exactly that many,
    /// the two opposite starts (0 and 2) for two players on a four-start map, else the first <paramref name="players"/> starts. The
    /// remaining starts stay empty (a Closed slot gets no start).
    /// </summary>
    public static int[] ChooseStarts(int players, int starts)
    {
        if (players > starts) throw new ArgumentOutOfRangeException(nameof(players), $"The map has {starts} starts for {players} players.");
        if (players == 2 && starts == 4) return new[] { 0, 2 };
        var chosen = new int[players];
        for (int i = 0; i < players; i++) chosen[i] = i;
        return chosen;
    }

    /// <summary>As <see cref="Apply(Simulation)"/> with <paramref name="startOf"/> naming the map start (index into the map's start
    /// positions) of each player; null = player p uses start p. Neutral players and players without a start get no building.</summary>
    public static EntityId[] Apply(Simulation sim, IReadOnlyList<int>? startOf)
    {
        ArgumentNullException.ThrowIfNull(sim);
        SimRules rules = sim.Setup.Rules;
        var map = sim.Setup.Map;
        var placed = new EntityId[sim.Setup.PlayerCount];
        for (int p = 0; p < sim.Setup.PlayerCount; p++)
        {
            placed[p] = EntityId.None;
            int startIndex = startOf is null ? p : p < startOf.Count ? startOf[p] : -1;
            if (sim.Players.IsNeutral((byte)p) || startIndex < 0 || startIndex >= map.StartPositions.Count) continue;
            (int type, IReadOnlyList<(ActorKind Kind, int Type)> units) = rules.ResolveStartKit(sim.Players.Faction[p]);
            CellCoord s = map.StartPositions[startIndex];
            if (type < 0)
            {
                // M6-08: no conyard building (Left Behind): the packed rig is the first start unit and stands in for it
                placed[p] = SpawnUnits(sim, (byte)p, s, units);
                continue;
            }
            BuildingType bt = rules.Buildings[type];
            // centre the footprint on the start cell, then search rings outward in a fixed order
            int cx = s.X - bt.FootprintW / 2, cy = s.Y - bt.FootprintH / 2;
            for (int r = 0; r <= 8 && placed[p] == EntityId.None; r++)
                for (int dy = -r; dy <= r && placed[p] == EntityId.None; dy++)
                    for (int dx = -r; dx <= r && placed[p] == EntityId.None; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        var tl = new CellCoord(cx + dx, cy + dy);
                        if (tl.X < 0 || tl.Y < 0 || tl.X + bt.FootprintW > map.Width || tl.Y + bt.FootprintH > map.Height) continue;
                        placed[p] = sim.PlaceBuilding((byte)p, type, tl);
                    }
            SpawnUnits(sim, (byte)p, s, units);
        }
        return placed;
    }

    /// <summary>Spawns each start unit on the next free cell of a ring search around <paramref name="start"/> (rings outward,
    /// rows then columns, as the conyard search): on the map, no building, passable for the unit, not already used.</summary>
    private static EntityId SpawnUnits(Simulation sim, byte player, CellCoord start, IReadOnlyList<(ActorKind Kind, int Type)> units)
    {
        EntityId first = EntityId.None;
        if (units.Count == 0) return first;
        SimRules rules = sim.Setup.Rules;
        var used = new List<CellCoord>();
        int next = 0;
        for (int r = 1; r <= MaxUnitRing && next < units.Count; r++)
            for (int dy = -r; dy <= r && next < units.Count; dy++)
                for (int dx = -r; dx <= r && next < units.Count; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var c = new CellCoord(start.X + dx, start.Y + dy);
                    if (!sim.Setup.Map.Contains(c) || !sim.Map.BuildingAt(c).IsNone || used.Contains(c)) continue;
                    (ActorKind kind, int type) = units[next];
                    Locomotion loco = kind == ActorKind.Vehicle ? rules.Vehicles[type].Locomotion : Locomotion.Foot;
                    if (!sim.Map.IsPassableTerrain(c, loco, rules.Terrain)) continue;
                    EntityId spawned = kind == ActorKind.Vehicle ? sim.SpawnVehicle(player, type, c) : sim.SpawnInfantry(player, type, c);
                    if (first == EntityId.None) first = spawned;
                    used.Add(c);
                    next++;
                }
        return first;
    }
}
