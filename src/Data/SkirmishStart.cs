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

    /// <summary>Largest ring of the fallback pass for start units the strict pass could not place (S17: Ash Plains layout 2 puts the Expedition's sea 9 cells from the start).</summary>
    private const int MaxFallbackRing = 16;

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
    public static EntityId[] Apply(Simulation sim, IReadOnlyList<int>? startOf) => Apply(sim, startOf, null);

    /// <summary>S19 (M13-15): the size of the small-army start: this many of the faction's cheapest armed tier 1 soldier ...</summary>
    public const int SmallArmySoldiers = 4;

    /// <summary>... and this many of its cheapest armed tier 1 land vehicle, on top of the faction kit.</summary>
    public const int SmallArmyVehicles = 2;

    /// <summary>
    /// S19 (M13-15): the units the "small army" start adds for <paramref name="faction"/>, from the rules alone: <see cref="SmallArmySoldiers"/> of
    /// the cheapest armed tier 1 soldier of the faction and <see cref="SmallArmyVehicles"/> of its cheapest armed, buildable tier 1 Wheeled or
    /// Tracked vehicle (cost, then rules order). A faction with neither gets nothing extra.
    /// </summary>
    public static IReadOnlyList<(ActorKind Kind, int Type)> SmallArmy(SimRules rules, Faction faction)
    {
        var army = new List<(ActorKind, int)>();
        int soldier = -1;
        if (rules.Infantry is { } infantry)
            for (int i = 0; i < infantry.Count; i++)
            {
                InfantryType t = infantry[i];
                if (t.Faction != faction || t.Tier != 1 || t.PrimaryWeapon < 0 || t.Engineer || t.Raider) continue;
                if (soldier < 0 || t.Cost < infantry[soldier].Cost) soldier = i;
            }
        for (int k = 0; soldier >= 0 && k < SmallArmySoldiers; k++) army.Add((ActorKind.Infantry, soldier));
        int vehicle = -1;
        for (int i = 0; i < rules.Vehicles.Count; i++)
        {
            VehicleType t = rules.Vehicles[i];
            if (t.Faction != faction || t.Tier != 1 || t.PrimaryWeapon < 0 || !t.Buildable || t.HarvestCapacity > 0) continue;
            if (t.Locomotion is not (Locomotion.Wheeled or Locomotion.Tracked)) continue;
            if (vehicle < 0 || t.Cost < rules.Vehicles[vehicle].Cost) vehicle = i;
        }
        for (int k = 0; vehicle >= 0 && k < SmallArmyVehicles; k++) army.Add((ActorKind.Vehicle, vehicle));
        return army;
    }

    /// <summary>As <see cref="Apply(Simulation, IReadOnlyList{int})"/>; <paramref name="smallArmy"/> (one flag per player, null = none) adds
    /// <see cref="SmallArmy"/> to the start units of the flagged players.</summary>
    public static EntityId[] Apply(Simulation sim, IReadOnlyList<int>? startOf, IReadOnlyList<bool>? smallArmy)
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
            if (smallArmy is not null && p < smallArmy.Count && smallArmy[p])
            {
                var all = new List<(ActorKind Kind, int Type)>(units);
                all.AddRange(SmallArmy(rules, sim.Players.Faction[p]));
                units = all;
            }
            CellCoord s = map.StartPositions[startIndex];
            if (type < 0)
            {
                // M6-08: no conyard building (Left Behind): the packed rig is the first start unit and stands in for it
                placed[p] = SpawnUnits(sim, (byte)p, s, units);
                continue;
            }
            bool flip = SecondHalf(s, map.Width, map.Height);
            BuildingType bt = rules.Buildings[type];
            // centre the footprint on the start cell, then search rings outward in a fixed order
            int cx = s.X - bt.FootprintW / 2, cy = s.Y - bt.FootprintH / 2;
            for (int r = 0; r <= 8 && placed[p] == EntityId.None; r++)
                for (int ky = 0; ky <= 2 * r && placed[p] == EntityId.None; ky++)
                    for (int kx = 0; kx <= 2 * r && placed[p] == EntityId.None; kx++)
                    {
                        int dy = Scan(ky, r, flip), dx = Scan(kx, r, flip);
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
        bool[]? edgeSea = null;   // M13-01: naval cells connected to the map edge, built on first use
        int next = 0;
        bool flip = SecondHalf(start, sim.Setup.Map.Width, sim.Setup.Map.Height);
        for (int r = 1; r <= MaxUnitRing && next < units.Count; r++)
            for (int ky = 0; ky <= 2 * r && next < units.Count; ky++)
                for (int kx = 0; kx <= 2 * r && next < units.Count; kx++)
                {
                    int dy = Scan(ky, r, flip), dx = Scan(kx, r, flip);
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var c = new CellCoord(start.X + dx, start.Y + dy);
                    if (!sim.Setup.Map.Contains(c) || !sim.Map.BuildingAt(c).IsNone || used.Contains(c)) continue;
                    (ActorKind kind, int type) = units[next];
                    Locomotion loco = kind == ActorKind.Vehicle ? rules.Vehicles[type].Locomotion : Locomotion.Foot;
                    if (!sim.Map.IsPassableTerrain(c, loco, rules.Terrain)) continue;
                    if (loco == Locomotion.Naval && !SeaOk(ref edgeSea, sim, c)) continue;   // M13-01: never an inland lake
                    EntityId spawned = kind == ActorKind.Vehicle ? sim.SpawnVehicle(player, type, c) : sim.SpawnInfantry(player, type, c);
                    if (first == EntityId.None) first = spawned;
                    used.Add(c);
                    next++;
                }
        // S17: units the strict pass left over (the first unit blocks the rest when it has no passable cell within MaxUnitRing, e.g. a Command Ship
        // whose sea is further out) are placed one by one on the nearest free passable cell, up to MaxFallbackRing. Never runs when the strict pass placed everything.
        for (; next < units.Count; next++)
        {
            (ActorKind kind, int type) = units[next];
            Locomotion loco = kind == ActorKind.Vehicle ? rules.Vehicles[type].Locomotion : Locomotion.Foot;
            bool done = false;
            // M13-01: a naval unit searches the whole map (nearest edge-connected sea, ties by row then column); others stop at MaxFallbackRing
            int maxRing = loco == Locomotion.Naval ? Math.Max(sim.Setup.Map.Width, sim.Setup.Map.Height) : MaxFallbackRing;
            for (int r = 1; r <= maxRing && !done; r++)
                for (int ky = 0; ky <= 2 * r && !done; ky++)
                    for (int kx = 0; kx <= 2 * r && !done; kx++)
                    {
                        int dy = Scan(ky, r, flip), dx = Scan(kx, r, flip);
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        var c = new CellCoord(start.X + dx, start.Y + dy);
                        if (!sim.Setup.Map.Contains(c) || !sim.Map.BuildingAt(c).IsNone || used.Contains(c)) continue;
                        if (!sim.Map.IsPassableTerrain(c, loco, rules.Terrain)) continue;
                        if (loco == Locomotion.Naval && !SeaOk(ref edgeSea, sim, c)) continue;
                        EntityId spawned = kind == ActorKind.Vehicle ? sim.SpawnVehicle(player, type, c) : sim.SpawnInfantry(player, type, c);
                        if (first == EntityId.None) first = spawned;
                        used.Add(c);
                        done = true;
                    }
        }
        return first;
    }

    /// <summary>M13-10 / DATA-TOOLS-01: true for a start past the map centre (rows, then columns), whose ring searches run in the reverse order so
    /// that on a half-turn map its kit is the point-mirror image of the first start's kit instead of a translation of it.</summary>
    private static bool SecondHalf(CellCoord s, int w, int h) => s.Y * 2 > h - 1 || (s.Y * 2 == h - 1 && s.X * 2 > w - 1);

    /// <summary>The k-th offset (0..2r) of a ring row/column scan: ascending from -r, or descending from +r when <paramref name="flip"/>.</summary>
    private static int Scan(int k, int r, bool flip) => flip ? r - k : k - r;

    /// <summary>M13-01: a naval start unit may use the cell when it is edge-connected sea; when the map has no edge-connected water at all,
    /// any water will do (the best available: the ship is never left out, so the player always has a unit and can be defeated).</summary>
    private static bool SeaOk(ref bool[]? edgeSea, Simulation sim, CellCoord c)
    {
        edgeSea ??= EdgeConnectedSea(sim);
        if (edgeSea[c.Y * sim.Setup.Map.Width + c.X]) return true;
        return Array.IndexOf(edgeSea, true) < 0;
    }

    /// <summary>Naval-passable cells connected (8-way) to a naval-passable cell on the map edge (M13-01): the water a supply tender can reach.</summary>
    private static bool[] EdgeConnectedSea(Simulation sim)
    {
        SimRules rules = sim.Setup.Rules;
        var map = sim.Setup.Map;
        int w = map.Width, h = map.Height;
        var seen = new bool[w * h];
        var queue = new int[w * h];
        int head = 0, tail = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if ((x == 0 || y == 0 || x == w - 1 || y == h - 1) && sim.Map.IsPassableTerrain(new CellCoord(x, y), Locomotion.Naval, rules.Terrain))
                { seen[y * w + x] = true; queue[tail++] = y * w + x; }
        while (head < tail)
        {
            int i = queue[head++], cx = i % w, cy = i / w;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen[ny * w + nx]) continue;
                    if (!sim.Map.IsPassableTerrain(new CellCoord(nx, ny), Locomotion.Naval, rules.Terrain)) continue;
                    seen[ny * w + nx] = true; queue[tail++] = ny * w + nx;
                }
        }
        return seen;
    }
}
