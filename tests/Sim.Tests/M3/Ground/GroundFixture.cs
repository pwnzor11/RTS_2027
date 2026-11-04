// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Combat;

namespace Rts.Sim.Tests.M3.Ground;

/// <summary>M3-05 test rules, built in code on top of the combat fixture rules: warheads, weapons and types for radiation,
/// Blackout, seismic, stealth, jam and targeting tests.</summary>
internal static class GroundFixture
{
    private static readonly int[] Versus = { 100, 100, 100, 100, 100 };

    public static readonly SimRules TestRules = Build();

    private static SimRules Build()
    {
        SimRules b = CombatFixture.Loaded.Sim;
        var wh = new List<WarheadType>(b.Warheads)
        {
            new("g_cont", Versus, 1, true) { Contaminate = 100 },
            new("g_decon", Versus, 1, true) { Decontaminate = 60 },
            new("g_black", Versus, 1, true) { BlackoutTicks = 30 },
            new("g_bonus", Versus, 0, false) { BonusVsIrradiatedPercent = 200 },
            new("g_plain", Versus, 0, false),
        };
        int w0 = b.Warheads.Count;
        var wp = new List<WeaponType>(b.Weapons)
        {
            new("g_ground", 10, 2048, 0, 5, 0, w0 + 4),
            new("g_aa", 10, 2048, 0, 5, 0, w0 + 4) { Targets = WeaponTargets.Air },
            new("g_both", 10, 2048, 0, 5, 0, w0 + 4) { Targets = WeaponTargets.Both },
        };
        int p0 = b.Weapons.Count;
        var pre = Array.Empty<string>();
        var veh = new List<VehicleType>(b.Vehicles)
        {
            new("g_gun", Locomotion.Tracked, 20, 6, 500, ArmorClass.Heavy, 8, p0, false, 0, false, 0, pre),
            new("g_aa_unit", Locomotion.Tracked, 20, 6, 500, ArmorClass.Heavy, 8, p0 + 1, false, 0, false, 0, pre),
            new("g_plane", Locomotion.Air, 20, 6, 500, ArmorClass.Light, 4, -1, false, 0, false, 0, pre),
            new("g_irr", Locomotion.Tracked, 20, 6, 500, ArmorClass.Heavy, 4, -1, false, 0, false, 0, pre) { Irradiated = true },
            new("g_immune", Locomotion.Tracked, 20, 6, 500, ArmorClass.Heavy, 4, -1, false, 0, false, 0, pre) { RadResistPermille = 1000 },
            new("g_jam", Locomotion.Tracked, 20, 6, 500, ArmorClass.Heavy, 4, -1, false, 0, false, 0, pre) { JamRadius = 5 },
            new("g_plain_veh", Locomotion.Tracked, 20, 6, 500, ArmorClass.Heavy, 4, -1, false, 0, false, 0, pre),
        };
        var inf = new List<InfantryType>(b.Infantry)
        {
            new("g_foot", 4, 100, ArmorClass.Unarmored, 4, -1, 0, pre),
            new("g_foot_half", 4, 100, ArmorClass.Unarmored, 4, -1, 0, pre) { RadResistPermille = 500 },
            new("g_sneak", 4, 100, ArmorClass.Unarmored, 4, -1, 0, pre) { Stealth = true },
        };
        var bld = new List<BuildingType>(b.Buildings)
        {
            new("g_seismo", 1, 1, 300, ArmorClass.Structure, 2, -1, 0, 0, pre, pre) { SeismicRadius = 6 },
            new("g_search", 1, 1, 300, ArmorClass.Structure, 6, -1, 0, 0, pre, pre) { SeesThroughBlackout = true },
        };
        return b with { Warheads = wh, Weapons = wp, Vehicles = veh, Infantry = inf, Buildings = bld };
    }

    // Linear search: the appended test types are not in id order, so SimRules.IndexOf (binary search) cannot be used.
    private static int Find<T>(IReadOnlyList<T> list, string id, Func<T, string> idOf)
    {
        for (int i = 0; i < list.Count; i++) if (idOf(list[i]) == id) return i;
        throw new KeyNotFoundException(id);
    }
    public static int Veh(string id) => Find(TestRules.Vehicles, id, v => v.Id);
    public static int Inf(string id) => Find(TestRules.Infantry, id, v => v.Id);
    public static int Bld(string id) => Find(TestRules.Buildings, id, v => v.Id);
    public static int Wh(string id) { for (int i = 0; i < TestRules.Warheads.Count; i++) if (TestRules.Warheads[i].Id == id) return i; throw new KeyNotFoundException(id); }
    public static int Wp(string id) { for (int i = 0; i < TestRules.Weapons.Count; i++) if (TestRules.Weapons[i].Id == id) return i; throw new KeyNotFoundException(id); }

    public static SimSetup Setup(ulong seed = 1) =>
        new(seed, MapData.CreateFlat(32, 32), 2, TestRules, CombatFixture.Loaded.RulesHash) { AllowDebug = true };

    /// <summary>A fresh sim at tick 0 (setup calls such as SpawnVehicle / PlaceBuilding are still allowed).</summary>
    public static Simulation Fresh(ulong seed = 1) => new(Setup(seed));

    /// <summary>Step once so vision and tick positions settle; later spawns must go through <see cref="Spawn"/>.</summary>
    public static Simulation Ready(Simulation sim) { sim.Step(CombatFixture.None); return sim; }

    public static Simulation NewSim(ulong seed = 1) => Ready(Fresh(seed));

    /// <summary>Spawn a vehicle (infantry when <paramref name="infantry"/>) mid-game with a debug command; one step.</summary>
    public static void Spawn(Simulation sim, byte player, bool infantry, int type, int x, int y) =>
        sim.Step(new[] { new Command(sim.Tick, player, (ushort)(1000 + sim.Tick), CommandKind.Debug, default, default,
            new CellCoord(x, y), (int)(infantry ? DebugOp.SpawnInfantry : DebugOp.SpawnVehicle), type) });

    public static void Steps(Simulation sim, int n) { for (int i = 0; i < n; i++) sim.Step(CombatFixture.None); }
}
