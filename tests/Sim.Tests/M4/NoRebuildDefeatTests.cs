// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Replay;
using Rts.Sim.Rules;
using Rts.Sim.Serialization;
using static Rts.Sim.Tests.Production.ProductionFixture;

namespace Rts.Sim.Tests.M4;

/// <summary>M4-17 (D-173): the skirmish "no way to rebuild for 2 minutes" defeat rule.</summary>
public sealed class NoRebuildDefeatTests
{
    private const int Limit = SimConstants.TicksPerSecond * 120;

    private static Simulation Sim(bool rule, params (string Id, int X, int Y)[] player0)
    {
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(32, 32), 2, Loaded.Sim, Loaded.RulesHash) { NoRebuildDefeat = rule, AllowDebug = true });
        foreach (var (id, x, y) in player0)
            if (sim.PlaceBuilding(0, Bld(id), new CellCoord(x, y)).IsNone) throw new InvalidOperationException("setup place " + id);
        if (sim.PlaceBuilding(1, Bld("test_hq"), new CellCoord(24, 24)).IsNone) throw new InvalidOperationException("enemy hq");
        return sim;
    }

    /// <summary>Spawns a vehicle after the first Step through the debug command (setup calls are closed by then).</summary>
    private static EntityId SpawnLater(Simulation sim, byte player, int vehicleType, int x, int y)
    {
        sim.Step(new[] { new Command(sim.Tick, player, 9001, CommandKind.Debug, default, default, new CellCoord(x, y), (int)DebugOp.SpawnVehicle, vehicleType) });
        for (int i = sim.Units.Capacity - 1; i >= 1; i--)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player && sim.Units.TypeIndex[i] == vehicleType) return sim.Units.Slots.IdOf(i);
        throw new InvalidOperationException("spawn failed");
    }

    private static void Kill(Simulation sim, int x, int y) => sim.Buildings.Health[BuildingOf(sim, x, y).Index] = 0;

    [Fact]
    public void LimitIsTwoMinutes() => Assert.Equal(2400, Limit);

    [Fact]
    public void ClockStartsAtTheTickTheLastLifelineIsLost_AndDefeatsExactlyAtTheLimit()
    {
        // HQ + a turret and a rifleman and a tank: only the HQ is a lifeline.
        var sim = Sim(true, ("test_hq", 4, 4), ("test_turret", 10, 4), ("test_power", 14, 4));
        sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(2, 12));
        sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(6, 12));
        Run(sim, 50);
        Assert.Equal(0, sim.Production.NoRebuildTicks[0]);
        Kill(sim, 4, 4);
        sim.Step(None);
        Assert.Equal(1, sim.Production.NoRebuildTicks[0]);   // the clock starts on the tick the HQ died
        Run(sim, Limit - 2);
        Assert.Equal(Limit - 1, sim.Production.NoRebuildTicks[0]);
        Assert.False(sim.Players.Defeated[0]);
        sim.Step(None);                                        // the 2400th tick without a lifeline
        Assert.Equal(Limit, sim.Production.NoRebuildTicks[0]);
        Assert.True(sim.Players.Defeated[0]);
        Assert.True(sim.GameOver);
        Assert.Equal(1, sim.WinningTeam);
        Assert.False(sim.Players.Defeated[1]);
    }

    [Fact]
    public void ClockResetsAsSoonAsALifelineExistsAgain_AndRestartsFromZero()
    {
        var sim = Sim(true, ("test_hq", 4, 4), ("test_turret", 10, 4));
        Kill(sim, 4, 4);
        Run(sim, 2000);
        Assert.Equal(2000, sim.Production.NoRebuildTicks[0]);
        EntityId h = SpawnLater(sim, 0, Veh("test_harvester"), 6, 12);
        Assert.Equal(0, sim.Production.NoRebuildTicks[0]);
        Run(sim, 1000);   // a long time with the harvester: still alive and the clock stays at zero
        Assert.False(sim.Players.Defeated[0]);
        sim.Units.Health[h.Index] = 0;
        sim.Step(None);
        Assert.Equal(1, sim.Production.NoRebuildTicks[0]);
        Run(sim, Limit - 2);
        Assert.False(sim.Players.Defeated[0]);
        sim.Step(None);
        Assert.True(sim.Players.Defeated[0]);
    }

    [Theory]
    [InlineData("test_barracks")]   // a unit-producing building
    [InlineData("test_factory")]
    [InlineData("test_hq")]         // the tech-tree root that opens the structure queue
    public void AProductionStructureAloneKeepsThePlayerAlive(string id)
    {
        var sim = Sim(true, (id, 4, 4));
        Run(sim, Limit + 300);
        Assert.False(sim.Players.Defeated[0]);
        Assert.Equal(0, sim.Production.NoRebuildTicks[0]);
    }

    [Fact]
    public void AHarvesterAloneKeepsThePlayerAlive()
    {
        var sim = Sim(true, ("test_silo", 4, 4));   // a non-producing building so the old rule does not apply
        sim.SpawnVehicle(0, Veh("test_harvester"), new CellCoord(8, 12));
        Run(sim, Limit + 300);
        Assert.False(sim.Players.Defeated[0]);
    }

    [Fact]
    public void DefensesPowerSilosAndAnArmyAloneDoNot()
    {
        var sim = Sim(true, ("test_turret", 4, 4), ("test_power", 10, 4), ("test_silo", 14, 4), ("test_wall", 20, 4));
        for (int i = 0; i < 6; i++) sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(2 + i, 14));
        for (int i = 0; i < 6; i++) sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(2 + i, 18));
        Run(sim, Limit - 1);
        Assert.False(sim.Players.Defeated[0]);
        sim.Step(None);
        Assert.True(sim.Players.Defeated[0]);
    }

    [Fact]
    public void WithTheRuleOffNothingChanges()
    {
        var sim = Sim(false, ("test_turret", 4, 4), ("test_power", 10, 4));
        Run(sim, Limit + 600);
        Assert.False(sim.Players.Defeated[0]);
        Assert.Equal(0, sim.Production.NoRebuildTicks[0]);
        // and the old rule is untouched: losing every non-wall building still defeats at once
        Kill(sim, 4, 4); Kill(sim, 10, 4);
        sim.Step(None);
        Assert.True(sim.Players.Defeated[0]);
    }

    [Fact]
    public void TheOldRuleStillDefeatsImmediately()
    {
        var sim = Sim(true, ("test_hq", 4, 4));
        sim.SpawnVehicle(0, Veh("test_harvester"), new CellCoord(8, 12));
        Run(sim, 5);
        Kill(sim, 4, 4);
        sim.Step(None);
        Assert.True(sim.Players.Defeated[0]);   // no building left, whatever units remain
    }

    [Fact]
    public void ANeverBasedPlayerIsNotDefeated()
    {
        var sim = Sim(true);
        Run(sim, Limit + 10);
        Assert.False(sim.Players.Defeated[0]);
    }

    // ---- shipped rules: founders per faction -------------------------------------------------------

    private static (Simulation Sim, SimRules Rules) RealSim(Faction f)
    {
        string dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;
        RuleSet rs = RulesLoader.Load(dir);
        var sim = new Simulation(new SimSetup(3, MapData.CreateFlat(48, 48), 2, rs.Sim, rs.RulesHash)
        { Factions = new[] { f, Faction.Authority }, NoRebuildDefeat = true, AllowDebug = true });
        return (sim, rs.Sim);
    }

    private static int V(SimRules r, string id) => SimRules.IndexOf(r.Vehicles, id, v => v.Id);
    private static int B(SimRules r, string id) => SimRules.IndexOf(r.Buildings, id, v => v.Id);

    [Theory]
    [InlineData(Faction.Directorate, "dir_landtrain", "dir_hatch", "dir_bore")]
    [InlineData(Faction.Expedition, "exp_command_ship", "exp_pile_house", null)]
    public void AFounderUnitAloneKeepsThePlayerAlive(Faction f, string founder, string leftover, string? notFounder)
    {
        var (sim, r) = RealSim(f);
        Assert.False(sim.PlaceBuilding(0, B(r, leftover), new CellCoord(4, 4)).IsNone);
        sim.PlaceBuilding(1, B(r, "auth_conyard"), new CellCoord(30, 30));
        EntityId u = sim.SpawnVehicle(0, V(r, founder), new CellCoord(10, 10));
        Run(sim, Limit + 100);
        Assert.False(sim.Players.Defeated[0]);
        sim.Units.Health[u.Index] = 0;
        sim.Step(None);
        if (notFounder != null) SpawnLater(sim, 0, V(r, notFounder), 12, 12);   // deploys into a non-root building: not a founder
        Run(sim, Limit - 2 - (notFounder != null ? 1 : 0));   // the spawn step above was one more tick without a lifeline
        Assert.False(sim.Players.Defeated[0]);
        sim.Step(None);
        Assert.True(sim.Players.Defeated[0]);
    }

    [Fact]
    public void ADirectorateBoreThatOnlyDeploysIntoAHatchIsNotAFounder_ButAScraperIsAHarvester()
    {
        var (sim, r) = RealSim(Faction.Directorate);
        sim.PlaceBuilding(0, B(r, "dir_hatch"), new CellCoord(4, 4));
        sim.PlaceBuilding(1, B(r, "auth_conyard"), new CellCoord(30, 30));
        EntityId bore = sim.SpawnVehicle(0, V(r, "dir_bore"), new CellCoord(10, 10));
        Run(sim, Limit);
        Assert.True(sim.Players.Defeated[0]);
        var (sim2, r2) = RealSim(Faction.Directorate);
        sim2.PlaceBuilding(0, B(r2, "dir_hatch"), new CellCoord(4, 4));
        sim2.PlaceBuilding(1, B(r2, "auth_conyard"), new CellCoord(30, 30));
        sim2.SpawnVehicle(0, V(r2, "dir_scraper"), new CellCoord(10, 10));
        Run(sim2, Limit + 100);
        Assert.False(sim2.Players.Defeated[0]);
        Assert.True(sim.Units.Slots.IsAlive(bore));
    }

    [Fact]
    public void ALeftBehindPackedMotherRigKeepsThePlayerAlive_AndEveryFactionRootIsAProductionStructure()
    {
        var (sim, r) = RealSim(Faction.LeftBehind);
        sim.PlaceBuilding(0, B(r, "lb_spike_nest"), new CellCoord(4, 4));   // a defence only
        sim.PlaceBuilding(1, B(r, "auth_conyard"), new CellCoord(30, 30));
        sim.SpawnVehicle(0, V(r, "lb_mother_rig_packed"), new CellCoord(10, 10));
        Run(sim, Limit + 100);
        Assert.False(sim.Players.Defeated[0]);
        // each faction's start building is a root and so counts on its own
        foreach (string root in new[] { "auth_conyard", "dir_shaft", "exp_landing_depot", "lb_mother_rig" })
        {
            var (s, rr) = RealSim(Faction.Authority);
            Assert.False(s.PlaceBuilding(0, B(rr, root), new CellCoord(4, 4)).IsNone);
            s.PlaceBuilding(1, B(rr, "auth_conyard"), new CellCoord(30, 30));
            Run(s, Limit + 10);
            Assert.False(s.Players.Defeated[0]);
        }
    }

    // ---- state, save and header --------------------------------------------------------------------

    [Fact]
    public void ClockIsHashedAndSurvivesSaveAndLoad()
    {
        var sim = Sim(true, ("test_hq", 4, 4), ("test_turret", 10, 4));
        Kill(sim, 4, 4);
        Run(sim, 700);
        var ms = new MemoryStream();
        sim.Save(ms);
        var setup = sim.Setup;
        var loaded = Simulation.Load(new MemoryStream(ms.ToArray()), setup);
        Assert.Equal(700, loaded.Production.NoRebuildTicks[0]);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        ulong before = sim.ComputeStateHash();
        sim.Production.NoRebuildTicks[0]++;
        Assert.NotEqual(before, sim.ComputeStateHash());
        sim.Production.NoRebuildTicks[0]--;
        Run(sim, Limit); Run(loaded, Limit);
        Assert.True(sim.Players.Defeated[0]);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
    }

    [Fact]
    public void TheFlagIsPartOfTheSetupIdentity()
    {
        var on = Sim(true, ("test_hq", 4, 4));
        var off = Sim(false, ("test_hq", 4, 4));
        Assert.NotEqual(on.ComputeStateHash(), off.ComputeStateHash());
        var ms = new MemoryStream(); on.Save(ms);
        Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(ms.ToArray()), off.Setup));
        // header round trip
        var bw = new MemoryStream();
        using (var w = new BinaryWriter(bw, System.Text.Encoding.UTF8, leaveOpen: true)) SetupHeader.From(on, "t", Array.Empty<(string, string)>()).Write(w);
        using var rd = new BinaryReader(new MemoryStream(bw.ToArray()));
        Assert.True(SetupHeader.Read(rd).NoRebuildDefeat);
    }

    [Fact]
    public void ReplayFormat13CarriesTheFlag_AndOlderReplaysReadAsRuleOff()
    {
        var sim = Sim(true, ("test_hq", 4, 4));
        var ms = new MemoryStream();
        var rw = new ReplayWriter(ms, sim, "t", Array.Empty<(string, string)>());
        rw.Finish();
        ReplayData d = ReplayReader.Read(new MemoryStream(ms.ToArray()));
        Assert.Equal(13, d.Version);
        Assert.True(d.Header.NoRebuildDefeat);
        // a format-12 replay is the same bytes without the flags byte: build one by hand
        byte[] b = ms.ToArray();
        int flagsAt = FindFlagsByte(b, sim);
        var old = new List<byte>(b); old.RemoveAt(flagsAt);
        old[4] = 12; old[5] = 0; old[6] = 0; old[7] = 0;
        ReplayData o = ReplayReader.Read(new MemoryStream(old.ToArray()));
        Assert.Equal(12, o.Version);
        Assert.False(o.Header.NoRebuildDefeat);
    }

    /// <summary>The flags byte is the last byte of the header: find it by writing the header alone and measuring its length.</summary>
    private static int FindFlagsByte(byte[] replay, Simulation sim)
    {
        var hs = new MemoryStream();
        using (var w = new BinaryWriter(hs, System.Text.Encoding.UTF8, leaveOpen: true)) SetupHeader.From(sim, "t", Array.Empty<(string, string)>()).Write(w);
        return 8 + (int)hs.Length - 1;   // magic (4) + version (4) + header
    }
}
