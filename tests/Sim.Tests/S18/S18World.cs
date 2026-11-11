// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.S18;

/// <summary>
/// Session 18 audit harness: the REAL shipped <c>data/rules</c> on a small hand-made map, two enemy players, Debug commands allowed.
/// Nothing here edits rules; every number a test sees is the shipped one.
/// </summary>
internal sealed class S18World
{
    private static readonly Lazy<SimRules> Shipped = new(() =>
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        return RulesLoader.Load(dir!).Sim;
    });

    private ushort _seq;
    public readonly SimRules Rules;
    public readonly Simulation Sim;

    /// <summary>The shipped rules (loaded once per test run).</summary>
    public static SimRules RealRules => Shipped.Value;

    /// <summary>A flat map, or one whose cells with x &gt;= <paramref name="waterFromX"/> are deep water.</summary>
    public S18World(Faction a = Faction.Authority, Faction b = Faction.Authority, int size = 64, int waterFromX = -1, bool noRebuild = false, bool keepAlive = true)
    {
        Rules = RealRules;
        int n = size * size;
        var land = new LandType[n];
        if (waterFromX >= 0)
            for (int y = 0; y < size; y++)
                for (int x = waterFromX; x < size; x++) land[y * size + x] = LandType.Water;
        var map = new MapData("s18", "rust_belt", size, size, new ushort[n], land, new byte[n], new OverlayKind[n], new byte[n],
            new[] { new CellCoord(5, 5), new CellCoord(size - 6, size - 6) });
        Sim = new Simulation(new SimSetup(18, map, 2, Rules, 0x518UL) { AllowDebug = true, Factions = new[] { a, b }, NoRebuildDefeat = noRebuild });
        if (keepAlive)
        {   // far-away keep-alive buildings so a lost fight does not defeat a player
            Assert.False(Sim.PlaceBuilding(0, B("auth_power"), new CellCoord(1, 1)).IsNone);
            Assert.False(Sim.PlaceBuilding(1, B("auth_power"), new CellCoord(1, size - 4)).IsNone);
        }
    }

    public int B(string id) { int i = SimRules.IndexOf(Rules.Buildings, id, x => x.Id); Assert.True(i >= 0, id); return i; }
    public int I(string id) { int i = SimRules.IndexOf(Rules.Infantry!, id, x => x.Id); Assert.True(i >= 0, id); return i; }
    public int V(string id) { int i = SimRules.IndexOf(Rules.Vehicles, id, x => x.Id); Assert.True(i >= 0, id); return i; }
    public int U(string id) { int i = SimRules.IndexOf(Rules.Upgrades, id, x => x.Id); Assert.True(i >= 0, id); return i; }
    public WeaponType W(string id) => Rules.Weapons.Single(w => w.Id == id);
    public WarheadType Wh(string id) => Rules.Warheads.Single(w => w.Id == id);

    public EntityId Place(byte player, string id, int x, int y)
    {
        EntityId b = Sim.PlaceBuilding(player, B(id), new CellCoord(x, y));
        Assert.False(b.IsNone, "cannot place " + id);
        return b;
    }

    public EntityId Veh(byte player, string id, int x, int y) => Sim.SpawnVehicle(player, V(id), new CellCoord(x, y));
    public EntityId Inf(byte player, string id, int x, int y, int sub = 0) => Sim.SpawnInfantry(player, I(id), new CellCoord(x, y), sub);

    public Command Cmd(byte player, CommandKind kind, EntityId subject = default, EntityId target = default, CellCoord cell = default,
        int arg = 0, int arg2 = 0) => new(Sim.Tick, player, _seq++, kind, subject, target, cell, arg, arg2);

    public Command Attack(byte player, ActorKind subjectKind, EntityId subject, ActorKind targetKind, EntityId target) =>
        Cmd(player, CommandKind.Attack, subject, target, default, (int)subjectKind | ((int)targetKind << 8));

    public Command Grant(byte player, string upgrade) => Cmd(player, CommandKind.Debug, default, default, default, (int)DebugOp.GrantUpgrade, U(upgrade));

    public void Run(int ticks, params Command[] first)
    {
        for (int i = 0; i < ticks; i++) Sim.Step(i == 0 ? first : Array.Empty<Command>());
    }

    public int VehHp(EntityId id) => Sim.Units.Slots.IsAlive(id) ? Math.Max(0, Sim.Units.Health[id.Index]) : 0;
    public int InfHp(EntityId id) => Sim.Infantry.Slots.IsAlive(id) ? Math.Max(0, Sim.Infantry.Health[id.Index]) : 0;
    public int BldHp(EntityId id) => Sim.Buildings.Slots.IsAlive(id) ? Math.Max(0, Sim.Buildings.Health[id.Index]) : 0;
}
