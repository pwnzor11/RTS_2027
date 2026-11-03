// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.World;

namespace Rts.Sim.Tests.Movement;

/// <summary>Shared fixtures for movement tests: small hand-painted maps, rules with tracked / wheeled / non-crusher vehicles, invariants.</summary>
internal static class MoveWorld
{
    public static readonly Command[] None = Array.Empty<Command>();

    // vehicle type indexes
    public const int Tank = 0;       // tracked crusher, speed 40
    public const int Light = 1;      // tracked, not a crusher
    public const int Wheeled = 2;    // wheeled crusher, speed 40
    // infantry type indexes
    public const int Rifle = 0;

    /// <summary>Everything clear unless <paramref name="paint"/> changes it. Rock = impassable.</summary>
    public static MapData Grid(int w, int h, Action<LandType[], int>? paint = null)
    {
        var land = new LandType[w * h];
        paint?.Invoke(land, w);
        int n = w * h;
        return new MapData("t", "rust_belt", w, h, new ushort[n], land, new byte[n], new OverlayKind[n], new byte[n],
            new[] { new CellCoord(1, 1), new CellCoord(w - 2, h - 2) });
    }

    /// <summary>Clear 100%, rough 75%, road 125% wheeled / 110% others (D-044), water/rock/cliff impassable.</summary>
    public static TerrainTable Terrain()
    {
        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        Array.Fill(speed, 100);
        for (int l = 0; l < RuleCounts.Locomotions; l++)
        {
            speed[(int)LandType.Rough * RuleCounts.Locomotions + l] = 75;
            speed[(int)LandType.Road * RuleCounts.Locomotions + l] = l == (int)Locomotion.Wheeled ? 125 : 110;
            foreach (var blocked in new[] { LandType.Water, LandType.Rock, LandType.Cliff })
                speed[(int)blocked * RuleCounts.Locomotions + l] = 0;
        }
        var buildable = new bool[RuleCounts.LandTypes];
        buildable[(int)LandType.Clear] = true;
        return new TerrainTable(speed, buildable);
    }

    public static Simulation NewSim(MapData map, int players = 2, ulong seed = 1)
    {
        VehicleType tank = ScriptedGame.Vehicle("tank", 40, 100);
        VehicleType light = tank with { Id = "light", Crusher = false };
        VehicleType wheeled = tank with { Id = "wheeled", Locomotion = Locomotion.Wheeled };
        var rules = new SimRules(new[] { tank, light, wheeled }, new[] { ScriptedGame.Infantry("rifle", 20, 30) },
            ScriptedGame.Buildings(), Array.Empty<WeaponType>(), Array.Empty<WarheadType>(), Terrain(), Array.Empty<EffectDef>());
        return new Simulation(new SimSetup(seed, map, players, rules, 1) { AllowDebug = true });
    }

    public static Command Move(EntityId id, ActorKind kind, int x, int y, int tick = 0, byte player = 0, ushort seq = 0) =>
        new(tick, player, seq, CommandKind.Move, id, default, new CellCoord(x, y), (int)kind);

    public static CellCoord CellOf(Simulation sim, EntityId id) => sim.Units.Position[id.Index].ToCell();
    public static CellCoord CellOfInf(Simulation sim, EntityId id) => sim.Infantry.Position[id.Index].ToCell();

    /// <summary>
    /// Occupancy invariants that must hold after every tick: every live vehicle is the map holder of the cell its Position is in,
    /// no two share a cell, a held cell not under a vehicle is its reservation, and infantry hold the slot they claim.
    /// </summary>
    public static void AssertConsistent(Simulation sim)
    {
        var map = sim.Map;
        var seen = new HashSet<(int, int)>();
        int vehicles = 0;
        for (int i = 1; i < sim.Units.Capacity; i++)
        {
            if (!sim.Units.Slots.IsIndexAlive(i)) continue;
            vehicles++;
            EntityId id = sim.Units.Slots.IdOf(i);
            CellCoord c = sim.Units.Position[i].ToCell();
            Assert.True(seen.Add((c.X, c.Y)), $"two vehicles in cell {c} at tick {sim.Tick}");
            Assert.True(map.VehicleAt(c) == id, $"vehicle {id} is in {c} but the map holder is {map.VehicleAt(c)} (tick {sim.Tick})");
        }
        int held = 0;
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var c = new CellCoord(x, y);
                EntityId v = map.VehicleAt(c);
                if (!v.IsNone)
                {
                    held++;
                    Assert.True(sim.Units.Slots.IsAlive(v), $"cell {c} held by dead vehicle {v} (tick {sim.Tick})");
                    bool inside = sim.Units.Position[v.Index].ToCell() == c;
                    bool reserved = sim.Units.NextCellReserved[v.Index] && sim.Units.ReservedCell[v.Index] == c;
                    Assert.True(inside || reserved, $"cell {c} held by {v} which is neither in it nor reserving it (tick {sim.Tick})");
                }
                for (int s = 0; s < InfantryStore.SubSlotCount; s++)
                {
                    EntityId inf = map.InfantryAt(c, s);
                    if (inf.IsNone) continue;
                    Assert.True(sim.Infantry.Slots.IsAlive(inf), $"slot {c}/{s} held by dead infantry (tick {sim.Tick})");
                    int j = inf.Index;
                    bool standing = sim.Infantry.SlotCell[j] == c && sim.Infantry.SubSlot[j] == s;
                    bool reserved = sim.Infantry.NextCellReserved[j] && sim.Infantry.ReservedCell[j] == c && sim.Infantry.ReservedSlot[j] == s;
                    Assert.True(standing || reserved, $"slot {c}/{s} held by {inf} which neither stands nor reserves there (tick {sim.Tick})");
                }
            }
        for (int i = 1; i < sim.Infantry.Capacity; i++)
        {
            if (!sim.Infantry.Slots.IsIndexAlive(i)) continue;
            EntityId id = sim.Infantry.Slots.IdOf(i);
            Assert.True(map.InfantryAt(sim.Infantry.SlotCell[i], sim.Infantry.SubSlot[i]) == id,
                $"infantry {id} does not hold its slot (tick {sim.Tick})");
        }
        Assert.True(held >= vehicles);
    }

    /// <summary>Step with a command list, checking the invariants after every tick.</summary>
    public static void Run(Simulation sim, int ticks, IReadOnlyList<Command>? commands = null)
    {
        for (int t = 0; t < ticks; t++)
        {
            sim.Step(commands ?? None);
            AssertConsistent(sim);
        }
    }

    /// <summary>Step until the predicate holds (checking the invariants each tick); fails after <paramref name="max"/> ticks.</summary>
    public static int RunUntil(Simulation sim, Func<bool> done, int max, IReadOnlyList<Command>? commands = null)
    {
        for (int t = 0; t < max; t++)
        {
            if (done()) return t;
            sim.Step(commands ?? None);
            AssertConsistent(sim);
        }
        Assert.True(done(), $"condition not reached within {max} ticks");
        return max;
    }
}
