// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Tests.Production;
using Rts.Sim.Tunnels;
using Rts.Sim.View;

namespace Rts.Sim.Tests.M3;

/// <summary>M3-01 contracts: enum values, part order, empty defaults, save/load of the new parts and fields, factions in
/// the setup/header, command routing reaches the stubs safely, and the M3 view fog rules.</summary>
public sealed class M3ContractTests
{
    private static readonly Command[] None = Array.Empty<Command>();

    [Fact]
    public void EnumValues_ArePinned()
    {
        Assert.Equal(21, (int)CommandKind.Upgrade);
        Assert.Equal(5, (int)ProductionCategory.Upgrade);
        Assert.Equal(RuleCounts.ProductionCategories, Enum.GetValues<ProductionCategory>().Length);
        Assert.Equal(RuleCounts.Factions, Enum.GetValues<Faction>().Length);
        Assert.Equal(RuleCounts.TunnelSizes, Enum.GetValues<TunnelSize>().Length);
        Assert.Equal((0, 1), ((int)Faction.Authority, (int)Faction.Directorate));
        Assert.Equal((0, 1, 2, 3), ((int)TunnelSize.None, (int)TunnelSize.S, (int)TunnelSize.M, (int)TunnelSize.L));
        Assert.Equal((0, 1, 2), ((int)WeaponTargets.Ground, (int)WeaponTargets.Air, (int)WeaponTargets.Both));
        Assert.Equal(new[] { 9, 10, 11, 12, 13, 14 }, new[] { MissionKind.EnterTunnel, MissionKind.Transit, MissionKind.Burrow,
            MissionKind.Surface, MissionKind.Deploy, MissionKind.Capture }.Select(m => (int)m));
        Assert.Equal(7, Enum.GetValues<LandType>().Length);   // unchanged
    }

    [Fact]
    public void M3Parts_FollowProduction_ThenExtensions()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        string[] names = sim.StateParts.Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "production", "tunnels", "ground", "effects" }, names[^4..]);
        Assert.Same(sim.Tunnels, sim.StateParts[^3]);
        Assert.Same(sim.Ground, sim.StateParts[^2]);
        Assert.Same(sim.Effects, sim.StateParts[^1]);
    }

    [Fact]
    public void NewState_IsEmptyByDefault_AndStaysSoWithoutM3Content()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        var script = ScriptedGame.Script(100);
        for (int i = 0; i < 100; i++) sim.Step(script);
        Assert.Empty(sim.Tunnels.Queue);
        Assert.Empty(sim.Tunnels.Transit);
        Assert.Empty(sim.Tunnels.Telegraphs);
        Assert.All(sim.Tunnels.VehicleLayer, l => Assert.Equal(ActorLayer.Surface, l));
        Assert.All(sim.Ground.Radiation, v => Assert.Equal(0, v));
        Assert.All(sim.Ground.Hardened, v => Assert.Equal(0UL, v));
        Assert.All(sim.Effects.VehicleStill, v => Assert.Equal(0, v));
        Assert.Equal(64 * 64, sim.Ground.Radiation.Length);
        Assert.All(sim.Players.Faction, f => Assert.Equal(Faction.Authority, f));
        Assert.All(sim.Players.Upgrades, u => Assert.Equal(0UL, u));
    }

    [Fact]
    public void SaveLoad_RoundTripsEveryNewPartAndField()
    {
        var setup = ScriptedGame.Setup() with { Factions = new[] { Faction.Authority, Faction.Directorate } };
        var sim = ScriptedGame.NewSim(setup);
        for (int i = 0; i < 5; i++) sim.Step(None);
        sim.Players.Upgrades[1] = 0b101;
        sim.Tunnels.VehicleLayer[3] = ActorLayer.Burrowed;
        sim.Tunnels.InfantryLayer[2] = ActorLayer.Passenger;
        sim.Tunnels.InfantryCarrier[2] = new EntityId(3, 1);
        sim.Tunnels.BuildingMorphType[4] = 7; sim.Tunnels.BuildingMorphTicks[4] = 90;
        sim.Tunnels.NextTicket = 9;
        sim.Tunnels.Queue.Add(new TunnelQueueEntry { Entrance = new EntityId(1, 1), Unit = new ActorRef(ActorKind.Infantry, new EntityId(5, 2)), Destination = new EntityId(2, 1), Ticket = 8 });
        sim.Tunnels.Transit.Add(new TunnelTransit { Unit = new ActorRef(ActorKind.Vehicle, new EntityId(6, 1)), Owner = 1, Destination = new EntityId(2, 1), TicksLeft = 33, Points = 3 });
        sim.Tunnels.Telegraphs.Add(new Telegraph { Cell = new CellCoord(9, 10), Owner = 1, TicksLeft = 44, Kind = TelegraphKind.Entrance, Subject = new ActorRef(ActorKind.Building, new EntityId(3, 1)) });
        sim.Ground.Radiation[100] = 77; sim.Ground.Hardened[101] = 0b11; sim.Ground.Blackout[102] = 55; sim.Ground.HardeningDirty = true;
        sim.Effects.VehicleRofPermille[1] = 800; sim.Effects.InfantrySpeedPermille[2] = 1200; sim.Effects.InfantryStill[3] = 61;
        sim.Effects.BuildingCharge[2] = 500;

        var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        var loaded = Simulation.Load(ms, setup);
        Assert.Equal(sim.ComputeSubHashes(), loaded.ComputeSubHashes());
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(Faction.Directorate, loaded.Players.Faction[1]);
        Assert.True(loaded.HasUpgrade(1, 0));
        Assert.False(loaded.HasUpgrade(1, 1));
        Assert.True(loaded.HasUpgrade(1, 2));
        Assert.Equal(sim.Tunnels.Queue, loaded.Tunnels.Queue);
        Assert.Equal(sim.Tunnels.Transit, loaded.Tunnels.Transit);
        Assert.Equal(sim.Tunnels.Telegraphs, loaded.Tunnels.Telegraphs);
        Assert.Equal(ActorLayer.Passenger, loaded.Tunnels.InfantryLayer[2]);
        Assert.Equal(new EntityId(3, 1), loaded.Tunnels.InfantryCarrier[2]);
        Assert.Equal(90, loaded.Tunnels.BuildingMorphTicks[4]);
        Assert.Equal(77, loaded.Ground.Radiation[100]);
        Assert.True(loaded.Ground.HardeningDirty);
        Assert.Equal(1200, loaded.Effects.InfantrySpeedPermille[2]);
        Assert.Equal(500, loaded.Effects.BuildingCharge[2]);
    }

    /// <summary>HashCoverageTests only adds a default element to empty lists; this checks every field of every tunnel list entry
    /// reaches the "tunnels" bytes on its own.</summary>
    [Fact]
    public void EveryFieldOfTunnelListEntries_IsSerialised()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        sim.Tunnels.Queue.Add(default);
        sim.Tunnels.Transit.Add(default);
        sim.Tunnels.Telegraphs.Add(default);
        int checkedFields = 0;
        checkedFields += CheckEntry(sim.Tunnels, sim.Tunnels.Queue);
        checkedFields += CheckEntry(sim.Tunnels, sim.Tunnels.Transit);
        checkedFields += CheckEntry(sim.Tunnels, sim.Tunnels.Telegraphs);
        Assert.Equal(4 + 5 + 5, checkedFields);
    }

    private static int CheckEntry<T>(ISimStatePart part, List<T> list) where T : struct
    {
        int n = 0;
        foreach (var f in typeof(T).GetFields())
        {
            byte[] before = Bytes(part);
            object boxed = list[0];
            object old = f.GetValue(boxed)!;
            object next = old switch
            {
                int v => v + 1,
                byte v => (byte)(v + 1),
                EntityId id => new EntityId((ushort)(id.Index + 1), id.Generation),
                ActorRef r => r with { Kind = ActorKind.Infantry },
                CellCoord c => new CellCoord(c.X + 1, c.Y),
                TelegraphKind k => k == TelegraphKind.Unit ? TelegraphKind.Entrance : TelegraphKind.Unit,
                _ => throw new InvalidOperationException($"unsupported {f.FieldType}"),
            };
            f.SetValue(boxed, next);
            list[0] = (T)boxed;
            Assert.False(before.AsSpan().SequenceEqual(Bytes(part)), $"{typeof(T).Name}.{f.Name} is not serialised");
            f.SetValue(boxed, old);
            list[0] = (T)boxed;
            n++;
        }
        return n;
    }

    private static byte[] Bytes(ISimStatePart part)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) part.Write(w);
        return ms.ToArray();
    }

    [Fact]
    public void Factions_AreInTheSaveHeader_AndMustMatch()
    {
        var setup = ScriptedGame.Setup() with { Factions = new[] { Faction.Directorate, Faction.Authority } };
        var ms = new MemoryStream();
        ScriptedGame.NewSim(setup).Save(ms);
        byte[] bytes = ms.ToArray();
        var ex = Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(bytes), ScriptedGame.Setup()));
        Assert.Contains("faction", ex.Message);
        Assert.Equal(Faction.Directorate, Simulation.Load(new MemoryStream(bytes), setup).Players.Faction[0]);
    }

    [Fact]
    public void Factions_SurviveTheReplayHeader()
    {
        var setup = ScriptedGame.Setup() with { Factions = new[] { Faction.Authority, Faction.Directorate } };
        var sim = ScriptedGame.NewSim(setup);
        var ms = new MemoryStream();
        new Rts.Sim.Replay.ReplayWriter(ms, sim, "b", Array.Empty<(string, string)>()).Finish();
        var data = Rts.Sim.Replay.ReplayReader.Read(new MemoryStream(ms.ToArray()));
        Assert.Equal(new[] { Faction.Authority, Faction.Directorate }, data.Header.Factions);
        var plain = new MemoryStream();
        new Rts.Sim.Replay.ReplayWriter(plain, ScriptedGame.NewSim(ScriptedGame.Setup()), "b", Array.Empty<(string, string)>()).Finish();
        Assert.Null(Rts.Sim.Replay.ReplayReader.Read(new MemoryStream(plain.ToArray())).Header.Factions);
    }

    [Fact]
    public void FactionsDifferentFromDefault_ChangeThePlayersSubHash()
    {
        var a = ScriptedGame.NewSim(ScriptedGame.Setup());
        var b = ScriptedGame.NewSim(ScriptedGame.Setup() with { Factions = new[] { Faction.Authority, Faction.Directorate } });
        Assert.NotEqual(a.ComputeSubHashes().Single(h => h.Part == "players"), b.ComputeSubHashes().Single(h => h.Part == "players"));
    }

    [Fact]
    public void SlotData_IsClearedWhenActorsSpawnAndDie()
    {
        var sim = new Simulation(ScriptedGame.Setup());       // empty stores
        sim.Tunnels.VehicleLayer[1] = ActorLayer.Burrowed;   // stale data left in a free slot
        sim.Effects.VehicleStill[1] = 9;
        EntityId id = sim.SpawnVehicle(0, 0, new CellCoord(2, 2));
        Assert.Equal(1, id.Index);
        Assert.Equal(ActorLayer.Surface, sim.LayerOf(ActorKind.Vehicle, 1));
        Assert.Equal(0, sim.Effects.VehicleStill[1]);
        sim.Step(None);
        sim.Tunnels.VehicleLayer[1] = ActorLayer.Diving;
        sim.Effects.VehicleRofPermille[1] = 700;
        sim.Step(new[] { new Command(sim.Tick, 0, 0, CommandKind.Debug, id, default, default, (int)DebugOp.Kill, (int)ActorKind.Vehicle) });
        Assert.False(sim.Units.Slots.IsAlive(id));
        Assert.Equal(ActorLayer.Surface, sim.LayerOf(ActorKind.Vehicle, 1));
        Assert.Equal(0, sim.Effects.VehicleRofPermille[1]);
    }

    [Fact]
    public void BuildingPlacementAndRemoval_MarkHardeningDirty()
    {
        var sim = ProductionFixture.NewSim(new[] { ("test_hq", 4, 4) });
        Assert.True(sim.Ground.HardeningDirty);   // stub RunHardening does not clear it yet (M3-04 does)
        sim.Ground.HardeningDirty = false;
        EntityId hq = sim.Buildings.Slots.IdOf(1);
        sim.Step(new[] { new Command(0, 0, 0, CommandKind.Sell, hq, default, default, 0) });
        Assert.True(sim.Ground.HardeningDirty);
    }

    [Fact]
    public void M3Commands_RouteToStubs_WithoutChangingState()
    {
        var sim = ProductionFixture.Base();
        EntityId tank = sim.SpawnVehicle(0, ProductionFixture.Veh("test_tank"), new CellCoord(20, 20));
        EntityId rifle = sim.SpawnInfantry(0, ProductionFixture.Inf("test_rifleman"), new CellCoord(21, 20));
        EntityId hq = sim.Buildings.Slots.IdOf(1);
        sim.Step(None);
        ulong before = sim.ComputeStateHash();
        int t = sim.Tick;
        sim.Step(new[]
        {
            new Command(t, 0, 0, CommandKind.Enter, rifle, tank, default, (int)ActorKind.Infantry | ((int)ActorKind.Vehicle << 8)),
            new Command(t, 0, 1, CommandKind.Enter, rifle, hq, default, (int)ActorKind.Infantry | ((int)ActorKind.Building << 8)),
            new Command(t, 0, 2, CommandKind.Deploy, tank, default, new CellCoord(5, 5), 0),
            new Command(t, 0, 3, CommandKind.Upgrade, hq, default, default, ProductionFixture.Bld("test_silo")),
            new Command(t, 0, 4, CommandKind.Special, hq, default, new CellCoord(5, 5), 0),
            new Command(t, 0, 5, CommandKind.Build, default, default, default, (int)ProductionCategory.Upgrade, 0),
            new Command(t, 0, 6, CommandKind.Cancel, default, default, default, (int)ProductionCategory.Upgrade, 0),
            new Command(t, 0, 7, CommandKind.Enter, new EntityId(999, 1), hq, default, 0x0201),   // bad subject: ignored
        });
        var control = ProductionFixture.Base();
        control.SpawnVehicle(0, ProductionFixture.Veh("test_tank"), new CellCoord(20, 20));
        control.SpawnInfantry(0, ProductionFixture.Inf("test_rifleman"), new CellCoord(21, 20));
        control.Step(None);
        Assert.Equal(before, control.ComputeStateHash());
        control.Step(None);
        Assert.Equal(control.ComputeStateHash(), sim.ComputeStateHash());   // the stubs do nothing yet
    }

    [Fact]
    public void Hud_CarriesFactionAndUpgrades()
    {
        var setup = ScriptedGame.Setup() with { Factions = new[] { Faction.Authority, Faction.Directorate } };
        var sim = ScriptedGame.NewSim(setup);
        sim.Players.Upgrades[1] = 4;
        PlayerHud hud = sim.HudFor(1);
        Assert.Equal(Faction.Directorate, hud.Faction);
        Assert.Equal(4UL, hud.Upgrades);
        Assert.Empty(hud.BuildableUpgrades);              // CanResearch is a stub until M3-06
        Assert.Equal(5, hud.Queues.Count);                // the M2 queue views are unchanged
        Assert.Equal(Faction.Directorate, sim.ViewFor(1).Faction);
        Assert.True(sim.ViewFor(1).HasUpgrade(2));
    }

    [Fact]
    public void SkirmishStart_DefaultAuthorityKit_IsTheM2Start_AndDirectorateKitPlacesTheShaft()
    {
        RuleSet r = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);
        MapData map = MapFile.Load(Path.Combine(TestData.MapsDir, "test_32.map.toml"));
        var sim = new Simulation(new SimSetup(1, map, 2, r.Sim, r.RulesHash));
        EntityId[] placed = SkirmishStart.Apply(sim);
        Assert.All(placed, id => Assert.False(id.IsNone));
        Assert.Equal(2, sim.Buildings.Slots.Count);
        Assert.Equal(0, sim.Units.Slots.Count + sim.Infantry.Slots.Count);
        Assert.All(placed, id => Assert.Equal(SkirmishStart.StartBuildingId, r.Sim.Buildings[sim.Buildings.TypeIndex[id.Index]].Id));

        var dir = new Simulation(new SimSetup(1, map, 2, r.Sim, r.RulesHash) { Factions = new[] { Faction.Authority, Faction.Directorate } });
        EntityId[] dirPlaced = SkirmishStart.Apply(dir);   // M3-07 data: the Directorate kit now resolves
        Assert.False(dirPlaced[1].IsNone);
        Assert.Equal("dir_shaft", r.Sim.Buildings[dir.Buildings.TypeIndex[dirPlaced[1].Index]].Id);
        Assert.Equal(r.Sim.ResolveStartKit(Faction.Directorate).Item2.Count, dir.Units.Slots.Count + dir.Infantry.Slots.Count);
    }

    [Fact]
    public void SkirmishStart_SpawnsKitUnits()
    {
        string game = TempRulesText.GameWithKit("[faction.authority]\nconyard = \"test_hq\"\nunits = [\"test_tank\", \"test_rifleman\"]\n");
        using var temp = new Rules.TempRules(("game.toml", game));
        RuleSet r = RulesLoader.Load(temp.Dir);
        MapData map = MapFile.Load(Path.Combine(TestData.MapsDir, "test_32.map.toml"));
        var sim = new Simulation(new SimSetup(1, map, 2, r.Sim, r.RulesHash));
        SkirmishStart.Apply(sim);
        Assert.Equal(2, sim.Units.Slots.Count);
        Assert.Equal(2, sim.Infantry.Slots.Count);
    }

    // ---- view fog ------------------------------------------------------------------------------

    [Fact]
    public void UndergroundEnemies_AreHidden_OwnAreFlagged()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        EntityId mine = sim.SpawnVehicle(0, 0, new CellCoord(10, 10));
        EntityId theirs = sim.SpawnVehicle(1, 0, new CellCoord(11, 10));
        sim.Step(None);
        var list = new List<ViewActor>();
        sim.ViewFor(0).CopyVisible(list);
        Assert.Contains(list, a => a.Id == theirs);

        sim.Tunnels.VehicleLayer[theirs.Index] = ActorLayer.Burrowed;
        sim.Tunnels.VehicleLayer[mine.Index] = ActorLayer.Tunnel;
        sim.ViewFor(0).CopyVisible(list);
        Assert.DoesNotContain(list, a => a.Id == theirs);                   // no Seismograph yet (M3-05 stub)
        Assert.DoesNotContain(sim.Snapshot(0).Units, u => u.Id == theirs);
        Assert.Contains(sim.Snapshot(-1).Units, u => u.Id == theirs && u.Underground);
        sim.ViewFor(0).CopyOwn(list, includeBuildings: false);
        Assert.True(list.Single(a => a.Id == mine).Underground);
        Assert.False(list.Single(a => a.Id == mine).Revealed);
        Assert.True(sim.Snapshot(0).Units.Single(u => u.Id == mine).Underground);
    }

    [Fact]
    public void Telegraphs_AreFogFiltered()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        sim.SpawnVehicle(0, 0, new CellCoord(10, 10));   // player 0 sees around (10,10)
        sim.Step(None);
        sim.Tunnels.Telegraphs.Add(new Telegraph { Cell = new CellCoord(11, 10), Owner = 1, TicksLeft = 30, Kind = TelegraphKind.Unit });
        sim.Tunnels.Telegraphs.Add(new Telegraph { Cell = new CellCoord(60, 60), Owner = 1, TicksLeft = 20, Kind = TelegraphKind.Entrance });
        var list = new List<TelegraphView>();
        sim.ViewFor(0).CopyTelegraphs(list);
        Assert.Equal(new[] { new TelegraphView(new CellCoord(11, 10), 1, 30, TelegraphKind.Unit) }, list);
        sim.ViewFor(1).CopyTelegraphs(list);
        Assert.Equal(2, list.Count);                                          // the owner sees its own
        Assert.Equal(2, sim.Snapshot(-1).Telegraphs.Count);
        Assert.Single(sim.Snapshot(0).Telegraphs);
    }

    [Fact]
    public void GroundLayers_FollowTheFogRules()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        sim.SpawnVehicle(0, 0, new CellCoord(10, 10));
        sim.Step(None);
        var seen = new CellCoord(11, 10);
        var unseen = new CellCoord(60, 60);
        Assert.Empty(sim.Snapshot(0).Ground);                                 // clear map: no layer array
        sim.Ground.Radiation[sim.Ground.IndexOf(seen)] = 200;
        sim.Ground.Radiation[sim.Ground.IndexOf(unseen)] = 200;
        sim.Ground.Blackout[sim.Ground.IndexOf(seen)] = 5;
        sim.Ground.Hardened[sim.Ground.IndexOf(unseen)] = 1UL << 1;           // hardened by player 1
        PlayerView v0 = sim.ViewFor(0), v1 = sim.ViewFor(1);
        Assert.Equal(200, v0.RadiationAt(seen));
        Assert.Equal(0, v0.RadiationAt(unseen));
        Assert.True(v0.BlackoutAt(seen));
        Assert.False(v0.HardenedAt(unseen));
        Assert.True(v1.HardenedAt(unseen));                                   // own team's hardening is always known
        Assert.False(v0.JammedAt(seen));                                      // M3-05 stub
        SimSnapshot snap = sim.Snapshot(0);
        Assert.Equal(200 >> 4 | GroundLayers.Blackout, snap.Ground[sim.Ground.IndexOf(seen)]);
        Assert.Equal(0, snap.Ground[sim.Ground.IndexOf(unseen)]);
        Assert.Equal(GroundLayers.Hardened | (200 >> 4), sim.Snapshot(-1).Ground[sim.Ground.IndexOf(unseen)]);
    }
}

/// <summary>Builds fixture game.toml texts with a replaced faction section.</summary>
internal static class TempRulesText
{
    /// <summary>The fixture game.toml with every [faction.*] table replaced by <paramref name="kits"/>.</summary>
    public static string GameWithKit(string kits)
    {
        string game = Rules.TempRules.Real("game.toml");
        int cut = game.IndexOf("[faction.authority]", StringComparison.Ordinal);
        return game[..cut] + kits;
    }
}
