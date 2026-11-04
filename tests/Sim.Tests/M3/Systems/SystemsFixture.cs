// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.M3.Systems;

/// <summary>M3-06 test rules built in code (no TOML): vehicles air/aura/crawler/grunt/truck, infantry engineer/rifle, buildings
/// hq/lab/tower, upgrades auth_a/auth_b/dir_x. Indices follow the id-sorted order.</summary>
internal static class SystemsFixture
{
    public const int Air = 0, Aura = 1, Crawler = 2, Grunt = 3, Truck = 4;
    public const int Engineer = 0, Rifle = 1;
    public const int Hq = 0, Lab = 1, Tower = 2;
    public static readonly Command[] None = Array.Empty<Command>();
    private static ushort _seq;

    public static SimRules Rules()
    {
        var none = Array.Empty<string>();
        var wh = new WarheadType("boom", new[] { 100, 100, 100, 100, 100 }, 1, false);
        var single = new WarheadType("bullet", new[] { 100, 100, 100, 100, 100 }, 0, false);
        var weapons = new[]
        {
            new WeaponType("gun", 10, 6 * 256, 0, 30, 0, 1),        // instant, 30-tick reload
            new WeaponType("meltdown", 50, 0, 0, 10, 0, 0),         // death weapon: splash 1
        };
        VehicleType V(string id, Locomotion l, int speed, int weapon = -1) =>
            new(id, l, speed, 8, 100, ArmorClass.Light, 5, weapon, false, 0, false, 0, none);
        var vehicles = new[]
        {
            V("air", Locomotion.Air, 20),
            V("aura", Locomotion.Tracked, 16) with { AuraRadius = 5, AuraRofPermille = 800, AuraSpeedPermille = 1500, AuraTargets = new[] { "grunt" } },
            V("crawler", Locomotion.Tracked, 16) with { DeathWeapon = 1 },
            V("grunt", Locomotion.Tracked, 16, 0),
            V("truck", Locomotion.Tracked, 16) with { Passengers = 2 },
        };
        InfantryType I(string id) => new(id, 9, 50, ArmorClass.Unarmored, 4, -1, 0, none);
        var infantry = new[] { I("engineer") with { Engineer = true }, I("rifle") };
        BuildingType B(string id) => new(id, 2, 2, 400, ArmorClass.Structure, 5, -1, 0, 0, none, new[] { id });
        var buildings = new[] { B("hq"), B("lab") with { Researches = true }, B("tower") };
        var upgrades = new[]
        {
            new UpgradeType("auth_a", Faction.Authority, 500, 100, none, "a"),
            new UpgradeType("auth_b", Faction.Authority, 300, 50, new[] { "auth_a" }, "b"),
            new UpgradeType("dir_x", Faction.Directorate, 200, 40, none, "x"),
        };
        return new SimRules(vehicles, infantry, buildings, weapons, new[] { wh, single }, Terrain(), Array.Empty<EffectDef>())
        { Upgrades = upgrades };
    }

    /// <summary>Water, rock and cliff are impassable for everything, air included, so a flying unit proves it ignores the table.</summary>
    private static TerrainTable Terrain()
    {
        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        Array.Fill(speed, 100);
        foreach (var land in new[] { LandType.Water, LandType.Rock, LandType.Cliff })
            for (int l = 0; l < RuleCounts.Locomotions; l++) speed[(int)land * RuleCounts.Locomotions + l] = 0;
        var buildable = new bool[RuleCounts.LandTypes];
        buildable[(int)LandType.Clear] = true;
        return new TerrainTable(speed, buildable);
    }

    /// <summary>A 40x40 map; when <paramref name="barrier"/> is set, columns 20 (water) and 22 (cliff) are impassable full-height.</summary>
    public static MapData Map(bool barrier = false)
    {
        MapData flat = MapData.CreateFlat(40, 40);
        if (!barrier) return flat;
        var land = new LandType[40 * 40];
        for (int y = 0; y < 40; y++) { land[y * 40 + 20] = LandType.Water; land[y * 40 + 22] = LandType.Cliff; }
        return new MapData("barrier", "rust_belt", 40, 40, new ushort[1600], land, new byte[1600], new OverlayKind[1600], new byte[1600],
            new[] { new CellCoord(2, 2), new CellCoord(37, 37) });
    }

    public static Simulation NewSim(bool barrier = false, IReadOnlyList<Faction>? factions = null, ulong seed = 1)
    {
        SimRules rules = Rules();
        return new Simulation(new SimSetup(seed, Map(barrier), 2, rules, 0x3606UL) { AllowDebug = true, Factions = factions });
    }

    public static SimSetup SetupOf(Simulation sim) => sim.Setup;

    public static Command Cmd(Simulation sim, byte player, CommandKind kind, EntityId subject = default, EntityId target = default,
        CellCoord cell = default, int arg = 0, int arg2 = 0) => new(sim.Tick, player, _seq++, kind, subject, target, cell, arg, arg2);

    public static Command Enter(Simulation sim, byte player, ActorKind subject, EntityId who, ActorKind targetKind, EntityId target) =>
        Cmd(sim, player, CommandKind.Enter, who, target, default, (int)subject | ((int)targetKind << 8));

    public static Command Move(Simulation sim, byte player, ActorKind kind, EntityId who, int x, int y) =>
        Cmd(sim, player, CommandKind.Move, who, default, new CellCoord(x, y), (int)kind);

    public static Command Kill(Simulation sim, ActorKind kind, EntityId who) =>
        Cmd(sim, 0, CommandKind.Debug, who, default, default, (int)DebugOp.Kill, (int)kind);

    public static Command BuildUpgrade(Simulation sim, int index, byte player = 0) =>
        Cmd(sim, player, CommandKind.Build, default, default, default, (int)ProductionCategory.Upgrade, index);

    public static Command CancelUpgrade(Simulation sim, int position, byte player = 0) =>
        Cmd(sim, player, CommandKind.Cancel, default, default, default, (int)ProductionCategory.Upgrade, position);

    public static void Run(Simulation sim, int ticks, params Command[] first)
    {
        for (int i = 0; i < ticks; i++) sim.Step(i == 0 ? first : None);
    }

    public static Simulation Roundtrip(Simulation sim)
    {
        var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        return Simulation.Load(ms, sim.Setup);
    }
}
