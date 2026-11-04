// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.M4;

/// <summary>M4-03 test rules built in code (no TOML; real data is M4-07's). Infantry: rifle (armed, sight 10), civ (unarmed, 500 hp),
/// engineer, nogar (armed, cannot garrison). Vehicles: dummy (1000 hp, unarmed), mortar (1000 hp, splash weapon). Buildings: bunker
/// (2x2, garrison 5), tenement (3x2, garrison 6), hq (2x2, no garrison).</summary>
internal static class GarrisonFixture
{
    public const int Rifle = 0, Civ = 1, Engineer = 2, NoGar = 3;
    public const int Dummy = 0, Mortar = 1;
    public const int Bunker = 0, Tenement = 1, Hq = 2;
    public const int GunRange = 6 * 256, GunReload = 30;
    public static readonly Command[] None = Array.Empty<Command>();
    private static ushort _seq;

    public static SimRules MakeRules()
    {
        var none = Array.Empty<string>();
        var boom = new WarheadType("boom", new[] { 100, 100, 100, 100, 100 }, 1, false);
        var bullet = new WarheadType("bullet", new[] { 100, 100, 100, 100, 100 }, 0, false);
        var weapons = new[]
        {
            new WeaponType("gun", 10, GunRange, 0, GunReload, 0, 1),      // instant
            new WeaponType("mortar", 40, 8 * 256, 0, 40, 0, 0),           // instant, splash 1
        };
        VehicleType V(string id, int hp, int weapon) =>
            new(id, Locomotion.Tracked, 16, 8, hp, ArmorClass.Light, 5, weapon, false, 0, false, 0, none);
        var vehicles = new[] { V("dummy", 1000, -1), V("mortar", 1000, 1) };
        InfantryType I(string id, int hp, int weapon) => new(id, 9, hp, ArmorClass.Unarmored, 10, weapon, 0, none);
        var infantry = new[]
        {
            I("rifle", 50, 0), I("civ", 500, -1), I("engineer", 50, -1) with { Engineer = true, CanGarrison = false },
            I("nogar", 50, 0) with { CanGarrison = false },
        };
        BuildingType B(string id, int w, int h, int garrison) =>
            new(id, w, h, 400, ArmorClass.Structure, 5, -1, 0, 0, none, new[] { id }) { Garrison = garrison };
        var buildings = new[] { B("bunker", 2, 2, 5), B("tenement", 3, 2, 6), B("hq", 2, 2, 0) };
        return new SimRules(vehicles, infantry, buildings, weapons, new[] { boom, bullet }, Terrain(), Array.Empty<EffectDef>());
    }

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

    /// <summary>Flat 40x40, or a "pocket": all water except the 2x2 at (10,10) and the single cell (12,10) next to it.</summary>
    public static MapData MakeMap(bool pocket = false)
    {
        if (!pocket) return MapData.CreateFlat(40, 40);
        var land = new LandType[1600];
        Array.Fill(land, LandType.Water);
        foreach (var (x, y) in new[] { (10, 10), (11, 10), (10, 11), (11, 11), (12, 10) }) land[y * 40 + x] = LandType.Clear;
        return new MapData("pocket", "rust_belt", 40, 40, new ushort[1600], land, new byte[1600], new OverlayKind[1600], new byte[1600],
            new[] { new CellCoord(2, 2), new CellCoord(37, 37) });
    }

    public static SimSetup Setup(bool pocket = false, ulong seed = 1) =>
        new(seed, MakeMap(pocket), 2, MakeRules(), 0x4303UL) { AllowDebug = true };

    public static Simulation NewSim(bool pocket = false) => new(Setup(pocket));

    public static Command Cmd(Simulation sim, byte player, CommandKind kind, EntityId subject = default, EntityId target = default,
        CellCoord cell = default, int arg = 0, int arg2 = 0) => new(sim.Tick, player, _seq++, kind, subject, target, cell, arg, arg2);

    public static Command EnterCmd(Simulation sim, byte player, EntityId who, EntityId building) =>
        Cmd(sim, player, CommandKind.Enter, who, building, default, (int)ActorKind.Infantry | ((int)ActorKind.Building << 8));

    public static Command Unload(Simulation sim, byte player, EntityId building) =>
        Cmd(sim, player, CommandKind.Unload, building, default, default, (int)ActorKind.Building);

    public static Command Sell(Simulation sim, byte player, EntityId building) => Cmd(sim, player, CommandKind.Sell, building);

    public static Command Kill(Simulation sim, ActorKind kind, EntityId who) =>
        Cmd(sim, 0, CommandKind.Debug, who, default, default, (int)DebugOp.Kill, (int)kind);

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

    /// <summary>Garrisoned soldiers of a building, in slot order.</summary>
    public static List<int> Occupants(Simulation sim, EntityId building)
    {
        var list = new List<int>();
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Tunnels.InfantryLayer[i] == ActorLayer.Garrisoned && sim.Tunnels.InfantryCarrier[i] == building) list.Add(i);
        return list;
    }

    public static int Reach(CellCoord c, CellCoord topLeft, int w, int h) =>
        Math.Max(Math.Max(Math.Max(topLeft.X - c.X, c.X - (topLeft.X + w - 1)), 0), Math.Max(Math.Max(topLeft.Y - c.Y, c.Y - (topLeft.Y + h - 1)), 0));
}
