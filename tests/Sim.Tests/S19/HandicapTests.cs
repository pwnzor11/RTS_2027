// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Replay;
using Rts.Sim.Rules;
using Rts.Sim.Serialization;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Tests.Economy;
using Rts.Sim.Tests.Production;
using static Rts.Sim.Tests.Production.ProductionFixture;

namespace Rts.Sim.Tests.S19;

/// <summary>S19 D-205 (M13-15): per-slot handicap (production speed and income) and starting credits in <see cref="SimSetup"/>. Saved in the
/// header, hashed in the players part, carried by replays; a game that uses neither keeps its old bytes and hashes.</summary>
public sealed class HandicapTests
{
    private static Simulation Make(IReadOnlyList<int>? handicap, IReadOnlyList<int>? cash = null)
    {
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(32, 32), 2, Loaded.Sim, Loaded.RulesHash) { Handicap = handicap, StartingCash = cash });
        foreach ((string id, int x, int y) in new[] { ("test_hq", 4, 4), ("test_power", 12, 4), ("test_barracks", 8, 4), ("test_factory", 4, 8) })
            Assert.False(sim.PlaceBuilding(0, Bld(id), new CellCoord(x, y)).IsNone);
        SyncPower(sim);
        sim.Players.Cash[0] = 10000;
        return sim;
    }

    private static int TicksToBuild(Simulation sim, ProductionCategory cat, int type)
    {
        Step(sim, Build(sim, cat, type));
        return 1 + RunUntil(sim, () => cat is ProductionCategory.Structure or ProductionCategory.Defence ? sim.Production.ReadyType(0, cat) >= 0 : sim.Infantry.Slots.Count > 0, 4000);
    }

    [Fact]
    public void Handicap50_DoublesTheBuildTime_75IsAThirdLonger()
    {
        int normal = TicksToBuild(Make(null), ProductionCategory.Infantry, Inf("test_rifleman"));
        int half = TicksToBuild(Make(new[] { 50, 100 }), ProductionCategory.Infantry, Inf("test_rifleman"));
        int threeQuarters = TicksToBuild(Make(new[] { 75, 100 }), ProductionCategory.Infantry, Inf("test_rifleman"));
        Assert.InRange(half, normal * 2 - 3, normal * 2 + 3);
        Assert.InRange(threeQuarters, normal * 4 / 3 - 3, normal * 4 / 3 + 3);
        Assert.Equal(normal, TicksToBuild(Make(new[] { 100, 100 }), ProductionCategory.Infantry, Inf("test_rifleman")));
    }

    [Fact]
    public void Handicap_OnlyAffectsItsOwnPlayer_AndTheCostIsUnchanged()
    {
        Simulation slow = Make(new[] { 50, 100 });
        Simulation fast = Make(new[] { 50, 100 });
        TicksToBuild(slow, ProductionCategory.Infantry, Inf("test_rifleman"));
        int paid = 10000 - slow.Players.Cash[0];
        Step(fast, Build(fast, ProductionCategory.Infantry, Inf("test_rifleman"), player: 1));   // player 1 has no barracks: nothing to compare, just no crash
        Assert.Equal(Loaded.Sim.Infantry![Inf("test_rifleman")].Cost, paid);
    }

    [Fact]
    public void StartingCash_IsPerPlayer_AndMinusOneMeansTheRulesAmount()
    {
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(32, 32), 3, Loaded.Sim, Loaded.RulesHash) { StartingCash = new[] { 1234, -1, 0 } });
        Assert.Equal(1234, sim.Players.Cash[0]);
        Assert.Equal(Loaded.Sim.Economy.StartingCash, sim.Players.Cash[1]);
        Assert.Equal(0, sim.Players.Cash[2]);
    }

    [Fact]
    public void Income_FollowsTheHandicap()
    {
        int Banked(int percent)
        {
            var sim = new Simulation(new SimSetup(1, EconomyFixture.PaintMap(OverlayKind.Ashfall, 12), 2, Loaded.Sim, Loaded.RulesHash)
            { AllowDebug = true, Handicap = percent == 100 ? null : new[] { percent, 100 } });
            sim.PlaceBuilding(0, Bld("test_refinery"), EconomyFixture.Refinery);
            sim.PlaceBuilding(0, Bld("test_silo"), new CellCoord(8, 2));
            sim.SpawnVehicle(0, EconomyFixture.Harvester, new CellCoord(8, 8));
            EconomyFixture.Run(sim, 2500);
            return sim.Players.Stored[0];
        }

        int full = Banked(100), half = Banked(50);
        Assert.True(full > 200, $"the full-income harvester banked {full}");
        Assert.InRange(half * 100, full * 45, full * 62);   // about half; each delivery rounds to the nearest credit
    }

    [Fact]
    public void ADefaultSlotOptions_SetupKeepsTheOldHashAndHeaderBytes()
    {
        Simulation plain = Make(null);
        Simulation explicitDefaults = Make(new[] { 100, 100 }, new[] { -1, -1 });
        Assert.Equal(plain.ComputeStateHash(), explicitDefaults.ComputeStateHash());
        byte[] a = HeaderBytes(plain), b = HeaderBytes(explicitDefaults);
        Assert.Equal(a, b);
        Assert.NotEqual(plain.ComputeStateHash(), Make(new[] { 75, 100 }).ComputeStateHash());   // a handicap is part of the hashed state
    }

    private static byte[] HeaderBytes(Simulation sim)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) SetupHeader.From(sim, "b", Array.Empty<(string, string)>()).Write(w);
        return ms.ToArray();
    }

    [Fact]
    public void TheHeader_RoundTripsTheOptions_AndAnOldHeaderReadsAsDefaults()
    {
        Simulation sim = Make(new[] { 75, 50 }, new[] { 2000, -1 });
        byte[] bytes = HeaderBytes(sim);
        SetupHeader back = SetupHeader.Read(new BinaryReader(new MemoryStream(bytes)));
        Assert.Equal(new[] { 75, 50 }, back.Handicap);
        Assert.Equal(new[] { 2000, -1 }, back.StartingCash);
        SimSetup rebuilt = back.ToSetup(sim.Setup.Map, sim.Setup.Rules, sim.Setup.RulesHash);
        Assert.Equal(75, rebuilt.HandicapOf(0));
        Assert.Equal(2000, rebuilt.StartingCashOf(0));
        Assert.Equal(Loaded.Sim.Economy.StartingCash, rebuilt.StartingCashOf(1));

        SetupHeader old = SetupHeader.Read(new BinaryReader(new MemoryStream(HeaderBytes(Make(null)))));
        Assert.Null(old.Handicap);
        Assert.Null(old.StartingCash);
    }

    [Fact]
    public void ASave_CarriesTheOptions_AndRefusesASetupWithoutThem()
    {
        Simulation sim = Make(new[] { 75, 100 }, new[] { 2000, -1 });
        for (int i = 0; i < 40; i++) sim.Step(Array.Empty<Command>());
        using var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        Simulation copy = Simulation.Load(ms, sim.Setup);
        Assert.Equal(sim.ComputeStateHash(), copy.ComputeStateHash());

        ms.Position = 0;
        SimSetup without = sim.Setup with { Handicap = null, StartingCash = null };
        var e = Assert.Throws<InvalidDataException>(() => Simulation.Load(ms, without));
        Assert.Contains("handicap", e.Message);
    }

    [Fact]
    public void AReplay_CarriesTheOptions_AndPlaysBackWithoutMismatch()
    {
        SimSetup setup = ScriptedGame.Setup() with { Handicap = new[] { 75, 100 }, StartingCash = new[] { 3000, -1 } };
        byte[] bytes = Replay.ReplayTests.Record(setup, 300);
        ReplayData data = ReplayReader.Read(new MemoryStream(bytes));
        Assert.Equal(75, data.Header.Handicap![0]);
        Assert.Equal(3000, data.Header.StartingCash![0]);
        ReplayResult result = ReplayPlayer.Run(new MemoryStream(bytes), setup);
        Assert.Equal(-1, result.FirstMismatchTick);
    }

    [Fact]
    public void AnOutOfRangeHandicap_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => Make(new[] { 5, 100 }));
        Assert.Throws<ArgumentException>(() => Make(new[] { 100, 900 }));
    }
}
