// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Serialization;
using Rts.Sim.Tests.M3.Superweapon;

namespace Rts.Sim.Tests.Qol;

/// <summary>QOL-07 (D-313): the opt-in match rules in <see cref="SimSetup"/>: a superweapon limit and a reveal-map switch. Defaults reproduce today's
/// game byte for byte; set, they are saved in the header, hashed in the players part, checked on load and enforced by the sim.</summary>
public sealed class MatchRulesTests
{
    private static Simulation Make(int limit = SimSetup.NoSuperweaponLimit, bool reveal = false, bool explicitDefaults = false, bool slowBuilds = false)
    {
        SimRules rules = SuperweaponFixture.Rules();
        if (slowBuilds) rules = rules with { Buildings = rules.Buildings.Select(b => b with { Cost = 100000, BuildTime = 100000 }).ToArray() };   // nothing finishes inside a test
        SimSetup setup = new SimSetup(1, MapData.CreateFlat(48, 48), 2, rules, 0x3613UL) { AllowDebug = true };
        if (limit != SimSetup.NoSuperweaponLimit || reveal || explicitDefaults) setup = setup with { SuperweaponLimit = limit, RevealMap = reveal };
        var sim = new Simulation(setup);
        sim.PlaceBuilding(0, SuperweaponFixture.Plant, new CellCoord(10, 10));
        return sim;
    }

    private static byte[] HeaderBytes(Simulation sim)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) SetupHeader.From(sim, "b", Array.Empty<(string, string)>()).Write(w);
        return ms.ToArray();
    }

    [Fact]
    public void Defaults_AreByteIdentical_ToASetupThatNeverHeardOfTheRules()
    {
        Simulation plain = Make();
        Simulation explicitDefaults = Make(SimSetup.NoSuperweaponLimit, false, explicitDefaults: true);
        for (int i = 0; i < 60; i++) { plain.Step(SuperweaponFixture.None); explicitDefaults.Step(SuperweaponFixture.None); }
        Assert.Equal(plain.ComputeStateHash(), explicitDefaults.ComputeStateHash());
        Assert.Equal(HeaderBytes(plain), HeaderBytes(explicitDefaults));
        using var a = new MemoryStream();
        using var b = new MemoryStream();
        plain.Save(a);
        explicitDefaults.Save(b);
        Assert.Equal(a.ToArray(), b.ToArray());
        Assert.False(plain.Setup.HasMatchRules);
    }

    [Fact]
    public void ARuleThatIsSet_ChangesTheHash_AndTheHeader()
    {
        Simulation plain = Make();
        Assert.NotEqual(plain.ComputeStateHash(), Make(limit: 1).ComputeStateHash());
        Assert.NotEqual(plain.ComputeStateHash(), Make(reveal: true).ComputeStateHash());
        Assert.NotEqual(Make(limit: 1).ComputeStateHash(), Make(limit: 2).ComputeStateHash());
        Assert.NotEqual(HeaderBytes(plain), HeaderBytes(Make(reveal: true)));
        Assert.Equal(HeaderBytes(plain).Length + 1, HeaderBytes(Make(limit: 0)).Length);   // the limit is one byte after the flags
    }

    [Theory]
    [InlineData(SimSetup.NoSuperweaponLimit, true)]
    [InlineData(0, false)]
    [InlineData(2, true)]
    public void TheHeader_RoundTripsTheRules(int limit, bool reveal)
    {
        Simulation sim = Make(limit, reveal);
        SetupHeader back = SetupHeader.Read(new BinaryReader(new MemoryStream(HeaderBytes(sim))));
        Assert.Equal(limit, back.SuperweaponLimit);
        Assert.Equal(reveal, back.RevealMap);
        SimSetup rebuilt = back.ToSetup(sim.Setup.Map, sim.Setup.Rules, sim.Setup.RulesHash);
        Assert.Equal(limit, rebuilt.SuperweaponLimit);
        Assert.Equal(reveal, rebuilt.RevealMap);
        back.CheckMatches(sim.Setup, sim.MapHash, "test", checkSeed: true);
    }

    [Fact]
    public void AnOldHeader_ReadsAsDefaults_AndASetupWithoutTheRulesIsRefused()
    {
        SetupHeader old = SetupHeader.Read(new BinaryReader(new MemoryStream(HeaderBytes(Make()))));
        Assert.Equal(SimSetup.NoSuperweaponLimit, old.SuperweaponLimit);
        Assert.False(old.RevealMap);
        Simulation ruled = Make(limit: 1, reveal: true);
        SetupHeader header = SetupHeader.Read(new BinaryReader(new MemoryStream(HeaderBytes(ruled))));
        Assert.Throws<InvalidDataException>(() => header.CheckMatches(Make().Setup, ruled.MapHash, "replay", checkSeed: true));
    }

    [Fact]
    public void ASave_CarriesTheRules_AndRefusesASetupWithoutThem()
    {
        Simulation sim = Make(limit: 1, reveal: true);
        for (int i = 0; i < 20; i++) sim.Step(SuperweaponFixture.None);
        using var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        Simulation copy = Simulation.Load(ms, sim.Setup);
        Assert.Equal(sim.ComputeStateHash(), copy.ComputeStateHash());
        ms.Position = 0;
        Assert.ThrowsAny<Exception>(() => Simulation.Load(ms, Make().Setup));
    }

    [Fact]
    public void RevealMap_MakesEveryCellVisibleToEveryone_AndOffKeepsTheFog()
    {
        var far = new CellCoord(46, 46);
        Simulation fogged = Make();
        Assert.Equal(0, fogged.Vision.VisionAt(0, far));
        Simulation revealed = Make(reveal: true);
        Assert.Equal(2, revealed.Vision.VisionAt(0, far));
        Assert.Equal(2, revealed.Vision.VisionAt(1, new CellCoord(0, 0)));
        Assert.True(revealed.Vision.IsVisibleToAny(new byte[] { 0 }, far));
        var layer = new byte[48 * 48];
        revealed.Vision.FillVision(0, layer);
        Assert.All(layer, v => Assert.Equal(2, v));
        Assert.Equal(0, revealed.Vision.VisionAt(0, new CellCoord(-1, 0)));   // off the map stays shroud
    }

    [Fact]
    public void SuperweaponLimit_BlocksTheTypeOnceReached_AndOnlyThatType()
    {
        int sun = SuperweaponFixture.Sun, hut = SuperweaponFixture.Hut;
        Simulation none = Make(limit: 0);
        Assert.Equal(QueueBlockReason.SuperweaponLimit, none.QueueBlockReason(0, ProductionCategory.Structure, sun));
        Assert.Equal(QueueBlockReason.None, none.QueueBlockReason(0, ProductionCategory.Structure, hut));

        Simulation one = Make(limit: 1);
        Assert.Equal(QueueBlockReason.None, one.QueueBlockReason(0, ProductionCategory.Structure, sun));
        one.PlaceBuilding(0, sun, new CellCoord(14, 10));
        Assert.Equal(QueueBlockReason.SuperweaponLimit, one.QueueBlockReason(0, ProductionCategory.Structure, sun));
        Assert.Equal(QueueBlockReason.None, one.QueueBlockReason(1, ProductionCategory.Structure, sun));   // the limit is per player

        Simulation two = Make(limit: 2);
        two.PlaceBuilding(0, sun, new CellCoord(14, 10));
        Assert.Equal(QueueBlockReason.None, two.QueueBlockReason(0, ProductionCategory.Structure, sun));
        two.PlaceBuilding(0, sun, new CellCoord(20, 10));
        Assert.Equal(QueueBlockReason.SuperweaponLimit, two.QueueBlockReason(0, ProductionCategory.Structure, sun));

        Simulation unlimited = Make();
        for (int i = 0; i < 3; i++) unlimited.PlaceBuilding(0, sun, new CellCoord(14 + 4 * i, 10));
        Assert.Equal(QueueBlockReason.None, unlimited.QueueBlockReason(0, ProductionCategory.Structure, sun));
    }

    [Fact]
    public void SuperweaponLimit_CountsWhatIsQueued_SoTheCommandCannotGetAround_It()
    {
        int sun = SuperweaponFixture.Sun;
        Simulation sim = Make(limit: 1, slowBuilds: true);
        Assert.True(sim.CanBuild(0, ProductionCategory.Structure, sun));
        sim.Step(new[] { new Command(sim.Tick, 0, 1, CommandKind.Build, default, default, default, (int)ProductionCategory.Structure, sun) });
        Assert.Equal(1, sim.Production.QueueLength(0, ProductionCategory.Structure));   // it was queued
        Assert.Equal(QueueBlockReason.SuperweaponLimit, sim.QueueBlockReason(0, ProductionCategory.Structure, sun));
        Assert.Equal(QueueBlockReason.None, sim.QueueBlockReason(0, ProductionCategory.Structure, SuperweaponFixture.Hut));
    }

    [Fact]
    public void ASetupOutsideTheRange_IsRefused()
    {
        var setup = new SimSetup(1, MapData.CreateFlat(48, 48), 2, SuperweaponFixture.Rules(), 0x3613UL) { SuperweaponLimit = SimSetup.MaxSuperweaponLimit + 1 };
        Assert.Throws<ArgumentException>(() => new Simulation(setup));
    }
}
