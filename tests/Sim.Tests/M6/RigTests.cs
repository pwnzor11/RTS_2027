// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.M6;

/// <summary>M6-03: rig pack / unpack (docs/design/10_m6_plan.md §3). Fixture: building "rigbase" (2x2, 400 hp, produces structures, packs into
/// vehicle "rig" over 20 ticks) and vehicle "rig" (200 hp, unpacks into "rigbase" over 20 ticks).</summary>
public sealed class RigTests
{
    private const int Base = 2, Rig = 1, Turret = 3, PackN = 20;
    private ushort _seq;

    private static SimSetup MakeSetup(bool quickPack = false)
    {
        BuildingType[] buildings = ScriptedGame.Buildings().Concat(new[] { ScriptedGame.Building("rigbase", 2, 2, 400) }).OrderBy(b => b.Id, StringComparer.Ordinal).ToArray();
        Assert.Equal("rigbase", buildings[Base].Id);
        Assert.Equal("turret", buildings[Turret].Id);
        buildings[Base] = buildings[Base] with { PacksInto = Rig, PackTicks = PackN, Produces = ProductionCategory.Structure };
        buildings[Turret] = buildings[Turret] with { BuildTime = 100 };
        VehicleType[] vehicles =
        {
            ScriptedGame.Vehicle("fast", 37, 100),
            ScriptedGame.Vehicle("rig", 11, 200) with { UnpacksInto = Base, PackTicks = PackN },
            ScriptedGame.Vehicle("slow", 11, 200),
        };
        SimRules rules = ScriptedGame.RulesWith(vehicles, Array.Empty<InfantryType>()) with { Buildings = buildings };
        if (quickPack)
            rules = rules with { Upgrades = new[] { new UpgradeType("quick_pack", Faction.Authority, 0, 0, Array.Empty<string>(), "") { PackTicksPermille = 500 } } };
        return new SimSetup(1, MapData.CreateFlat(64, 64), 2, rules, 0xABCDUL) { AllowDebug = true };
    }

    private Command Cmd(Simulation s, CommandKind k, EntityId subject, CellCoord cell = default, int arg = 0) =>
        new(s.Tick, 0, ++_seq, k, subject, default, cell, arg);

    private static void Run(Simulation s, int n) { for (int i = 0; i < n; i++) s.Step(Array.Empty<Command>()); }

    private static Simulation Packed(SimSetup setup, out EntityId rig)
    {
        var s = new Simulation(setup);
        rig = s.SpawnVehicle(0, Rig, new CellCoord(30, 30));
        s.Rigs.PackedFrom[rig.Index] = Base + 1;
        s.Rigs.HpPermille[rig.Index] = 1000;
        return s;
    }

    private static int VehicleCount(Simulation s) => s.Units.Slots.Count;

    [Fact]
    public void Pack_TakesExactlyPackTicks_AndFootprintIsFree()
    {
        var s = new Simulation(MakeSetup());
        EntityId b = s.PlaceBuilding(0, Base, new CellCoord(10, 10));
        s.Step(new[] { Cmd(s, CommandKind.Pack, b) });   // tick 0 counts as step 1 of PackN
        Assert.Equal(PackN - 1, s.Rigs.PackTicksLeft[b.Index]);
        Assert.Equal(50, s.PackProgressOf(0, new ActorRef(ActorKind.Building, b)));   // 1 of 20 = 5%
        Run(s, PackN - 2);
        Assert.True(s.Buildings.Slots.IsAlive(b));
        Assert.Equal(0, VehicleCount(s));
        Run(s, 1);
        Assert.False(s.Buildings.Slots.IsAlive(b));
        Assert.Equal(1, VehicleCount(s));
        for (int y = 10; y < 12; y++)
            for (int x = 10; x < 12; x++) Assert.True(s.Map.BuildingAt(new CellCoord(x, y)).IsNone);
        int v = 1;
        Assert.Equal(Rig, s.Units.TypeIndex[v]);
        Assert.Equal(Base + 1, s.Rigs.PackedFrom[v]);
        Assert.Equal(128, s.Units.Facing[v]);
        Assert.Equal(new CellCoord(10, 10), s.Units.Position[v].ToCell());
    }

    [Fact]
    public void SecondPack_IsIgnored_AndNonRigsAreRefused()
    {
        var s = new Simulation(MakeSetup());
        EntityId b = s.PlaceBuilding(0, Base, new CellCoord(10, 10));
        EntityId t = s.PlaceBuilding(0, Turret, new CellCoord(20, 20));
        s.Step(new[] { Cmd(s, CommandKind.Pack, b), Cmd(s, CommandKind.Pack, t) });
        s.Step(new[] { Cmd(s, CommandKind.Pack, b) });
        Assert.Equal(PackN - 2, s.Rigs.PackTicksLeft[b.Index]);
        Assert.Equal(0, s.Rigs.PackTicksLeft[t.Index]);
    }

    [Fact]
    public void Progress_Reads_ZeroWhenIdle_And_Permille()
    {
        var s = new Simulation(MakeSetup());
        EntityId b = s.PlaceBuilding(0, Base, new CellCoord(10, 10));
        var a = new ActorRef(ActorKind.Building, b);
        Assert.Equal(0, s.PackProgressOf(0, a));
        s.Step(new[] { Cmd(s, CommandKind.Pack, b) });
        Assert.Equal(50, s.PackProgressOf(0, a));
        Run(s, 9);
        Assert.Equal(500, s.PackProgressOf(0, a));
        Assert.Equal(0, s.PackProgressOf(1, a));   // not the owner
    }

    [Fact]
    public void Hp_RatioCarriesBothWays()
    {
        var s = new Simulation(MakeSetup());
        EntityId b = s.PlaceBuilding(0, Base, new CellCoord(10, 10));
        s.Buildings.Health[b.Index] = 150;   // 375 permille of 400
        s.Step(new[] { Cmd(s, CommandKind.Pack, b) });
        Run(s, PackN - 1);
        int v = 1;
        Assert.Equal(375, s.Rigs.HpPermille[v]);
        Assert.Equal(75, s.Units.Health[v]);   // 200 x 375 / 1000
        EntityId rig = s.Units.Slots.IdOf(v);
        s.Step(new[] { Cmd(s, CommandKind.Pack, rig, new CellCoord(10, 10)) });   // unpack right where it stands
        Run(s, PackN + 2);
        Assert.False(s.Units.Slots.IsAlive(rig));
        Assert.Equal(1, s.Buildings.Slots.Count);
        Assert.Equal(150, s.Buildings.Health[1]);
        Assert.Equal(Base, s.Buildings.TypeIndex[1]);
    }

    [Fact]
    public void Queue_PausesWhilePackingAndPacked_AndResumesAfterUnpack()
    {
        var s = new Simulation(MakeSetup());
        EntityId b = s.PlaceBuilding(0, Base, new CellCoord(10, 10));
        s.Step(new[] { new Command(s.Tick, 0, 1, CommandKind.Build, default, default, default, (int)ProductionCategory.Structure, Turret) });
        Assert.Equal(1, s.Production.QueueLength(0, ProductionCategory.Structure));
        Run(s, 4);
        int t0 = Progress(s);
        Assert.True(t0 > 0);
        s.Step(new[] { Cmd(s, CommandKind.Pack, b) });
        Run(s, PackN + 10);   // packing, then packed for ten ticks
        Assert.Equal(1, s.Production.QueueLength(0, ProductionCategory.Structure));
        Assert.Equal(t0, Progress(s));   // paused from the pack command's own tick
        int v = 1;
        Assert.Equal((int)ProductionCategory.Structure + 1, s.Rigs.PausedQueue[v]);   // player 0, lane = category
        EntityId rig = s.Units.Slots.IdOf(v);
        s.Step(new[] { Cmd(s, CommandKind.Pack, rig, new CellCoord(10, 10)) });
        Run(s, PackN + 2);
        Assert.Equal(1, s.Buildings.Slots.Count);
        int before = Progress(s);
        Run(s, 5);
        Assert.True(Progress(s) > before);
    }

    private static int Progress(Simulation s) => s.HudFor(0).Queues.Single(q => q.Category == ProductionCategory.Structure).Items[0].ProgressPermille;

    [Fact]
    public void Unpack_OnBlockedFootprint_IsRefused_AndRevalidatedAtTheEnd()
    {
        var setup = MakeSetup();
        var s = Packed(setup, out EntityId rig);
        EntityId blocker = s.PlaceBuilding(1, Turret, new CellCoord(41, 41));
        // blocked from the start: refused outright
        s.Step(new[] { Cmd(s, CommandKind.Pack, rig, new CellCoord(40, 40)) });
        Assert.Equal(0, s.Units.Mission[rig.Index] == Rts.Sim.Missions.MissionKind.Pack ? 1 : 0);
        Run(s, 3);
        Assert.Equal(0, s.Rigs.UnpackTicksLeft[rig.Index]);
        // free site: unpack starts, then something is built on the footprint before it ends
        s.Step(new[] { Cmd(s, CommandKind.Pack, rig, new CellCoord(30, 30)) });
        Assert.Equal(PackN - 1, s.Rigs.UnpackTicksLeft[rig.Index]);
        Run(s, 5);
        s.Step(new[] { new Command(s.Tick, 0, 99, CommandKind.Place, default, default, new CellCoord(31, 31), Turret) });
        Run(s, PackN + 5);
        Assert.True(s.Units.Slots.IsAlive(rig));   // blocked at the end: stays packed
        Assert.True(s.Rigs.IsPackedRig(rig.Index));
        Assert.Equal(0, s.Rigs.UnpackTicksLeft[rig.Index]);
        Assert.True(s.Buildings.Slots.IsAlive(blocker));
    }

    [Fact]
    public void Unpack_DrivesToTheSite_ThenTakesPackTicks()
    {
        var s = Packed(MakeSetup(), out EntityId rig);
        s.Step(new[] { Cmd(s, CommandKind.Pack, rig, new CellCoord(36, 30)) });
        int guard = 0;
        while (s.Rigs.UnpackTicksLeft[rig.Index] == 0 && guard++ < 600) Run(s, 1);
        Assert.True(guard < 600);
        Assert.InRange(s.Units.Position[rig.Index].ToCell().X, 36, 37);
        Run(s, PackN + 1);
        Assert.False(s.Units.Slots.IsAlive(rig));
        Assert.Equal(new CellCoord(36, 30), s.Buildings.Cell[1]);
    }

    [Fact]
    public void Upgrade_HalvesPackTime()
    {
        var s = new Simulation(MakeSetup(quickPack: true));
        s.Players.Upgrades[0] |= 1UL;
        EntityId b = s.PlaceBuilding(0, Base, new CellCoord(10, 10));
        s.Step(new[] { Cmd(s, CommandKind.Pack, b) });
        Assert.Equal(PackN / 2 - 1, s.Rigs.PackTicksLeft[b.Index]);
        Run(s, PackN / 2 - 2);
        Assert.True(s.Buildings.Slots.IsAlive(b));
        Run(s, 1);
        Assert.False(s.Buildings.Slots.IsAlive(b));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaveLoad_MidPackOrUnpack_ReplaysToTheSameHash(bool unpack)
    {
        var setup = MakeSetup();
        Simulation Build(out EntityId subject, out CommandKind k, out CellCoord cell)
        {
            k = CommandKind.Pack;
            if (unpack) { var p = Packed(setup, out subject); cell = new CellCoord(32, 30); return p; }
            var s = new Simulation(setup);
            subject = s.PlaceBuilding(0, Base, new CellCoord(10, 10));
            cell = default;
            return s;
        }
        Simulation a = Build(out EntityId sub, out CommandKind kind, out CellCoord at);
        a.Step(new[] { new Command(a.Tick, 0, 1, kind, sub, default, at, 0) });
        Run(a, unpack ? 12 : 8);
        Assert.True(unpack ? a.Rigs.UnpackTicksLeft[sub.Index] > 0 || a.Units.Position[sub.Index].ToCell().X < 32 : a.Rigs.PackTicksLeft[sub.Index] > 0);
        var ms = new MemoryStream();
        a.Save(ms);
        Simulation b = Simulation.Load(new MemoryStream(ms.ToArray()), setup);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        for (int i = 0; i < 80; i++)
        {
            a.Step(Array.Empty<Command>()); b.Step(Array.Empty<Command>());
            Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        }
        Assert.Equal(unpack ? 1 : 0, a.Buildings.Slots.Count);   // unpack placed the building; pack removed it
        Assert.Equal(unpack ? 0 : 1, VehicleCount(a));
    }
}
