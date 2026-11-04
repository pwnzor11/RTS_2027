// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Tests.Movement;

namespace Rts.Sim.Tests.M3.Hardening;

/// <summary>Fixture for M3-04: wall, gate and HQ types built in code, a "grouting" upgrade, and a walled ring with a gate.</summary>
internal static class HardeningWorld
{
    public const int Wall = 0;    // 1x1, Wall = true
    public const int Gate = 1;    // 1x1, Gate = true (Wall left false: the seal must treat gate as wall on its own)
    public const int Hq = 2;      // 3x3 structure
    public const int Tank = 0;    // tracked vehicle
    public const int Rifle = 0;   // infantry

    // Ring perimeter from (Lo,Lo) to (Hi,Hi); the gate sits at (GateX, Lo).
    public const int Lo = 5, Hi = 13, GateX = 9;
    public static readonly CellCoord GateCell = new(GateX, Lo);
    public static readonly CellCoord Inside = new(9, 9);

    public static SimRules Rules()
    {
        BuildingType wall = ScriptedGame.Building("auth_wall", 1, 1, 200) with { Wall = true };
        BuildingType gate = ScriptedGame.Building("auth_gate", 1, 1, 200) with { Gate = true };
        BuildingType hq = ScriptedGame.Building("hq", 3, 3, 500);
        VehicleType tank = ScriptedGame.Vehicle("tank", 40, 100);
        return new SimRules(new[] { tank }, new[] { ScriptedGame.Infantry("rifle", 20, 30) }, new[] { wall, gate, hq },
            Array.Empty<WeaponType>(), Array.Empty<WarheadType>(), MoveWorld.Terrain(), Array.Empty<EffectDef>())
        {
            Upgrades = new[] { new UpgradeType("auth_grouting", Faction.Authority, 1000, 0, Array.Empty<string>(), "grouting") },
        };
    }

    public static SimSetup Setup(int size = 24, int players = 2, IReadOnlyList<byte>? teams = null) =>
        new(7, MoveWorld.Grid(size, size), players, Rules(), 1) { AllowDebug = true, Teams = teams };

    /// <summary>Every perimeter cell of the ring, row by row, gate cell included.</summary>
    public static IEnumerable<CellCoord> RingCells(int lo = Lo, int hi = Hi)
    {
        for (int y = lo; y <= hi; y++)
            for (int x = lo; x <= hi; x++)
                if (x == lo || x == hi || y == lo || y == hi) yield return new CellCoord(x, y);
    }

    /// <summary>Places the ring for <paramref name="player"/> (gate at <see cref="GateCell"/>), skipping <paramref name="gap"/>.</summary>
    public static Dictionary<CellCoord, EntityId> BuildRing(Simulation sim, byte player = 0, CellCoord? gap = null)
    {
        var ids = new Dictionary<CellCoord, EntityId>();
        foreach (CellCoord c in RingCells())
        {
            if (gap == c) continue;
            EntityId id = sim.PlaceBuilding(player, c == GateCell ? Gate : Wall, c);
            Assert.False(id.IsNone, $"ring piece at {c} was refused");
            ids[c] = id;
        }
        return ids;
    }

    public static bool IsInterior(CellCoord c) => c.X > Lo && c.X < Hi && c.Y > Lo && c.Y < Hi;

    public static void GiveGrouting(Simulation sim, byte player) => sim.Players.Upgrades[player] |= 1UL << 0;

    public static Command Kill(EntityId building, int tick, byte player = 0) =>
        new(tick, player, 0, CommandKind.Debug, building, default, default, (int)DebugOp.Kill, (int)ActorKind.Building);

    public static int HardenedCount(Simulation sim) => sim.Ground.Hardened.Count(b => b != 0);
}
