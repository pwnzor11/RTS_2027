// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Effects;
using Rts.Sim.Missions;
using Rts.Sim.Replay;
using Rts.Sim.Rigs;
using Rts.Sim.Rules;
using Rts.Sim.Salvage;
using Rts.Sim.Serialization;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Tests.Rules;

namespace Rts.Sim.Tests.M6;

/// <summary>M6-02 contracts (docs/design/10_m6_plan.md §2–§3): enum values, rules schema v5, the "rigs" and "salvage" parts, effects part
/// version 2 and the format versions. No gameplay behaviour is tested here: none exists yet.</summary>
public sealed class M6ContractTests
{
    // ---- enums and versions ---------------------------------------------------------------------

    [Fact]
    public void EnumValues_ArePinned()
    {
        Assert.Equal((0, 1, 2, 3), ((int)Faction.Authority, (int)Faction.Directorate, (int)Faction.Neutral, (int)Faction.LeftBehind));
        Assert.Equal(5, RuleCounts.Factions);   // M7-02 appended Expedition = 4
        Assert.Equal(RuleCounts.Factions, Enum.GetValues<Faction>().Length);
        Assert.Equal(3, RuleCounts.PlayableFactions);   // M6-09: the skirmish UI offers Authority, Directorate, Left Behind
        Assert.Equal(RuleCounts.PlayableFactions, RuleCounts.PlayableOrder.Count);
        Assert.All(RuleCounts.PlayableOrder, f => Assert.True(RuleCounts.IsPlayable(f)));
        Assert.True(RuleCounts.IsPlayable(Faction.LeftBehind));
        Assert.False(RuleCounts.IsPlayable(Faction.Neutral));
        Assert.Equal(24, (int)CommandKind.Pack);
        Assert.Equal(25, Enum.GetValues<CommandKind>().Length);
        Assert.Equal((16, 17, 18), ((int)MissionKind.Salvage, (int)MissionKind.Pack, (int)MissionKind.Withdraw));
        Assert.Equal(23, Enum.GetValues<MissionKind>().Length);   // M7-02 appended Unload..ConvoySail
        Assert.Equal(Faction.LeftBehind, SimRules.FactionFromId("lb_mother_rig"));
    }

    [Fact]
    public void FormatVersions_ArePinned()
    {
        Assert.Equal(11, SimWriter.SaveVersion);   // VET-1 took 11
        Assert.Equal(11, ReplayWriter.Version);
        Assert.Equal(11, RulesHash.FormatVersion);
    }

    [Fact]
    public void PartOrder_RigsAndSalvageFollowDirective()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        string[] names = sim.StateParts.Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "stats", "directive", "rigs", "salvage", "veterancy" }, names[^5..]);   // VET-1 appended "veterancy"
        Assert.Same(sim.Rigs, sim.StateParts[^3]);
        Assert.Same(sim.Salvage, sim.StateParts[^2]);
    }

    [Fact]
    public void LeftBehindFaction_SurvivesSaveAndReplayHeader()
    {
        var setup = ScriptedGame.Setup() with { Factions = new[] { Faction.LeftBehind, Faction.Authority } };
        var sim = ScriptedGame.NewSim(setup);
        var ms = new MemoryStream();
        sim.Save(ms);
        Assert.Equal(Faction.LeftBehind, Simulation.Load(new MemoryStream(ms.ToArray()), setup).Players.Faction[0]);
        var rep = new MemoryStream();
        new ReplayWriter(rep, sim, "b", Array.Empty<(string, string)>()).Finish();
        Assert.Equal(new[] { Faction.LeftBehind, Faction.Authority }, ReplayReader.Read(new MemoryStream(rep.ToArray())).Header.Factions);
    }

    [Fact]
    public void PackCommand_IsAcceptedAndIgnored()
    {
        var a = ScriptedGame.NewSim(ScriptedGame.Setup());
        var b = ScriptedGame.NewSim(ScriptedGame.Setup());
        var pack = new Command(a.Tick, 0, 1, CommandKind.Pack, new EntityId(1, 0), default, new CellCoord(5, 5), 0);
        a.Step(new[] { pack });
        b.Step(Array.Empty<Command>());
        Assert.Equal(b.ComputeStateHash(), a.ComputeStateHash());
    }

    // ---- rules schema v5 ------------------------------------------------------------------------

    private static RuleSet Load(params (string File, string? Content)[] overrides)
    {
        using var temp = new TempRules(overrides);
        return RulesLoader.Load(temp.Dir);
    }

    private const string AllVehicleKeys = """

        [test_rig_vehicle]
        base = "test_crawler"
        unpacks_into = "test_hq"
        pack_ticks = 90
        salvage_rate = 4
        raider = true
        raider_one_use = true
        drops_blackout_ticks = 30
        aura_repair_permille = 20
        tow_bonus_permille = 300
        """;

    private const string AllInfantryKeys = """

        [test_scav]
        base = "test_rifleman"
        salvage_rate = 2
        raider = true
        raider_one_use = true
        ash_stealth_delay_ticks = 15
        ash_stealth_threshold = 40
        aura_repair_permille = 10
        """;

    private static string Buildings(string extra) => TempRules.Real("buildings.toml") + "\n[test_rig]\nbase = \"test_hq\"\n" + extra + "\n";

    [Fact]
    public void Defaults_AreOff()
    {
        RuleSet r = Load();
        foreach (VehicleType v in r.Sim.Vehicles)
        {
            Assert.Equal((-1, 0, 0, false, false, 0, 0, 0),
                (v.UnpacksInto, v.PackTicks, v.SalvageRate, v.Raider, v.RaiderOneUse, v.DropsBlackoutTicks, v.AuraRepairPermille, v.TowBonusPermille));
        }
        foreach (InfantryType i in r.Sim.Infantry)
        {
            Assert.Equal((0, false, false, 0, 0, 0),
                (i.SalvageRate, i.Raider, i.RaiderOneUse, i.AshStealthDelayTicks, i.AshStealthThreshold, i.AuraRepairPermille));
        }
        Assert.All(r.Sim.Buildings, b => Assert.Equal((-1, 0), (b.PacksInto, b.PackTicks)));
        Assert.All(r.Sim.Upgrades, u => Assert.Equal((0, 0, 0, false), (u.PackTicksPermille, u.SalvagePermille, u.PlunderPermille, u.RepairsVehiclesAtRange)));
        Assert.All(r.Sim.Warheads, w => Assert.Equal((0, 0, 0, 64, 0), (w.StormRadius, w.StormTicks, w.StormContaminateStep, w.StormContaminateCap, w.StormSpeedPermille)));
        EconomyRules e = r.Sim.Economy;
        Assert.Equal((0, 25, 25, 1000, 20, 1000, 0),
            (e.WreckTicks, e.SalvagePercent, e.WreckMinValue, e.WreckMaxValue, e.PlunderPercent, e.PlunderCap, e.SpoilsPermille));
    }

    [Fact]
    public void VehicleKeys_Parse_AndResolveBuildingIds()
    {
        RuleSet r = Load(("vehicles.toml", TempRules.Real("vehicles.toml") + AllVehicleKeys));
        VehicleType v = r.Sim.Vehicles.Single(x => x.Id == "test_rig_vehicle");
        Assert.Equal(r.Sim.Buildings.ToList().FindIndex(b => b.Id == "test_hq"), v.UnpacksInto);
        Assert.True(v.UnpacksInto >= 0);
        Assert.Equal((90, 4, true, true, 30, 20, 300), (v.PackTicks, v.SalvageRate, v.Raider, v.RaiderOneUse, v.DropsBlackoutTicks, v.AuraRepairPermille, v.TowBonusPermille));
    }

    [Fact]
    public void InfantryKeys_Parse()
    {
        RuleSet r = Load(("infantry.toml", TempRules.Real("infantry.toml") + AllInfantryKeys));
        InfantryType i = r.Sim.Infantry.Single(x => x.Id == "test_scav");
        Assert.Equal((2, true, true, 15, 40, 10), (i.SalvageRate, i.Raider, i.RaiderOneUse, i.AshStealthDelayTicks, i.AshStealthThreshold, i.AuraRepairPermille));
    }

    [Fact]
    public void BuildingKeys_Parse_AndResolveVehicleIds()
    {
        RuleSet r = Load(("buildings.toml", Buildings("packs_into = \"test_tank\"\npack_ticks = 120")));
        BuildingType b = r.Sim.Buildings.Single(x => x.Id == "test_rig");
        Assert.Equal(r.Sim.Vehicles.ToList().FindIndex(v => v.Id == "test_tank"), b.PacksInto);
        Assert.Equal(120, b.PackTicks);
    }

    [Fact]
    public void UnknownReferences_AreRejected()
    {
        Assert.Throws<RulesException>(() => Load(("buildings.toml", Buildings("packs_into = \"nope\""))));
        Assert.Throws<RulesException>(() => Load(("vehicles.toml", TempRules.Real("vehicles.toml") + AllVehicleKeys.Replace("\"test_hq\"", "\"nope\""))));
    }

    [Fact]
    public void UnknownKeys_StillRejected()
    {
        Assert.Throws<RulesException>(() => Load(("vehicles.toml", TempRules.Real("vehicles.toml") + AllVehicleKeys + "\npack_tick = 1\n")));
        Assert.Throws<RulesException>(() => Load(("buildings.toml", Buildings("pack_tick = 1"))));
    }

    [Fact]
    public void EconomyWarheadAndUpgradeKeys_Parse()
    {
        string game = TempRules.RealWith("game.toml", "low_power_slowdown_permille = [1500, 2500, 4000]\n",
            "low_power_slowdown_permille = [1500, 2500, 4000]\nwreck_ticks = 1200\nsalvage_percent = 30\nwreck_min_value = 10\nwreck_max_value = 900\n" +
            "plunder_percent = 15\nplunder_cap = 700\nspoils_permille = 250\n");
        string warheads = TempRules.Real("warheads.toml") +
            "\n[test_storm]\nversus = [100, 100, 100, 100, 100]\nsplash = 0\nhits_allies = false\nstorm_radius = 9\nstorm_ticks = 1800\n" +
            "storm_contaminate_step = 2\nstorm_contaminate_cap = 80\nstorm_speed_permille = 600\n";
        string upgrades = TempRules.Real("upgrades.toml") +
            "\n[lb_test_upgrade]\nfaction = \"left_behind\"\ncost = 100\npack_ticks_permille = 250\nsalvage_permille = 300\nplunder_permille = 200\nrepairs_vehicles_at_range = true\n";
        RuleSet r = Load(("game.toml", game), ("warheads.toml", warheads), ("upgrades.toml", upgrades));
        EconomyRules e = r.Sim.Economy;
        Assert.Equal((1200, 30, 10, 900, 15, 700, 250),
            (e.WreckTicks, e.SalvagePercent, e.WreckMinValue, e.WreckMaxValue, e.PlunderPercent, e.PlunderCap, e.SpoilsPermille));
        WarheadType w = r.Sim.Warheads.Single(x => x.Id == "test_storm");
        Assert.Equal((9, 1800, 2, 80, 600), (w.StormRadius, w.StormTicks, w.StormContaminateStep, w.StormContaminateCap, w.StormSpeedPermille));
        UpgradeType u = r.Sim.Upgrades.Single(x => x.Id == "lb_test_upgrade");
        Assert.Equal(Faction.LeftBehind, u.Faction);
        Assert.Equal((250, 300, 200, true), (u.PackTicksPermille, u.SalvagePermille, u.PlunderPermille, u.RepairsVehiclesAtRange));
    }

    [Fact]
    public void EveryNewRulesField_ChangesTheRulesHash()
    {
        ulong baseline = Load().RulesHash;
        var variants = new (string File, string Text)[]
        {
            ("vehicles.toml", TempRules.Real("vehicles.toml") + AllVehicleKeys),
            ("infantry.toml", TempRules.Real("infantry.toml") + AllInfantryKeys),
            ("buildings.toml", Buildings("packs_into = \"test_tank\"\npack_ticks = 120")),
        };
        var seen = new HashSet<ulong> { baseline };
        foreach ((string file, string text) in variants) Assert.True(seen.Add(Load((file, text)).RulesHash), file);

        // One key at a time: flipping any single new key must reach the hash.
        string veh = TempRules.Real("vehicles.toml");
        foreach (string line in AllVehicleKeys.Split('\n').Where(l => l.Contains(" = ") && !l.Contains("base")))
        {
            string one = "\n[test_rig_vehicle]\nbase = \"test_crawler\"\n" + line.Trim() + "\n";
            Assert.True(seen.Add(Load(("vehicles.toml", veh + one)).RulesHash), line.Trim());
        }
        string inf = TempRules.Real("infantry.toml");
        foreach (string line in AllInfantryKeys.Split('\n').Where(l => l.Contains(" = ") && !l.Contains("base")))
        {
            string one = "\n[test_scav]\nbase = \"test_rifleman\"\n" + line.Trim() + "\n";
            Assert.True(seen.Add(Load(("infantry.toml", inf + one)).RulesHash), line.Trim());
        }
        foreach (string key in new[] { "wreck_ticks = 7", "salvage_percent = 7", "wreck_min_value = 7", "wreck_max_value = 900", "plunder_percent = 7", "plunder_cap = 7", "spoils_permille = 7" })
        {
            string game = TempRules.RealWith("game.toml", "low_power_slowdown_permille = [1500, 2500, 4000]\n", "low_power_slowdown_permille = [1500, 2500, 4000]\n" + key + "\n");
            Assert.True(seen.Add(Load(("game.toml", game)).RulesHash), key);
        }
        foreach (string key in new[] { "storm_radius = 7", "storm_ticks = 7", "storm_contaminate_step = 7", "storm_contaminate_cap = 7", "storm_speed_permille = 7" })
        {
            string wh = TempRules.Real("warheads.toml") + "\n[test_storm]\nversus = [100, 100, 100, 100, 100]\nsplash = 0\nhits_allies = false\n" + key + "\n";
            Assert.True(seen.Add(Load(("warheads.toml", wh)).RulesHash), key);
        }
        foreach (string key in new[] { "pack_ticks_permille = 7", "salvage_permille = 7", "plunder_permille = 7", "repairs_vehicles_at_range = true" })
        {
            string up = TempRules.Real("upgrades.toml") + "\n[lb_test_upgrade]\nfaction = \"left_behind\"\ncost = 100\n" + key + "\n";
            Assert.True(seen.Add(Load(("upgrades.toml", up)).RulesHash), key);
        }
    }

    // ---- rigs and salvage parts -----------------------------------------------------------------

    private static byte[] Bytes(ISimStatePart p)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) p.Write(w);
        return ms.ToArray();
    }

    private static void ReadBack(ISimStatePart p, byte[] bytes)
    {
        using var r = new BinaryReader(new MemoryStream(bytes));
        p.Read(r);
        Assert.Equal(bytes.Length, r.BaseStream.Position);   // the whole run was consumed
    }

    [Fact]
    public void NewParts_StartEmpty_AndAreSizedFromTheLimits()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        Assert.All(sim.Rigs.PackTicksLeft, v => Assert.Equal(0, v));
        Assert.All(sim.Rigs.PackedFrom, v => Assert.Equal(0, v));
        Assert.Equal(sim.Setup.Limits.Buildings, sim.Rigs.PackTicksLeft.Length);
        Assert.Equal(sim.Setup.Limits.Vehicles, sim.Rigs.PackedFrom.Length);
        Assert.Equal(0, sim.Salvage.Count);
        Assert.Equal(SalvageState.DefaultCapacity, sim.Salvage.Capacity);
        Assert.Equal("rigs", sim.Rigs.Name);
        Assert.Equal("salvage", sim.Salvage.Name);
    }

    [Fact]
    public void RigState_RoundTrips_AndEveryFieldChangesTheHash()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        byte[] empty = Bytes(sim.Rigs);
        ulong h0 = sim.ComputeStateHash();
        var fields = new (string Name, int[] Array)[]
        {
            ("PackTicksLeft", sim.Rigs.PackTicksLeft), ("PackedFrom", sim.Rigs.PackedFrom), ("HpPermille", sim.Rigs.HpPermille),
            ("UnpackTicksLeft", sim.Rigs.UnpackTicksLeft), ("UnpackX", sim.Rigs.UnpackX), ("UnpackY", sim.Rigs.UnpackY),
            ("PausedQueue", sim.Rigs.PausedQueue),
        };
        foreach ((string name, int[] array) in fields)
        {
            array[3] = 17;
            Assert.NotEqual(h0, sim.ComputeStateHash());
            byte[] bytes = Bytes(sim.Rigs);
            Assert.NotEqual(empty, bytes);
            array[3] = 0;
            Assert.Equal(h0, sim.ComputeStateHash());
            Assert.Equal(empty, Bytes(sim.Rigs));
        }
        sim.Rigs.PackTicksLeft[2] = 5; sim.Rigs.PackedFrom[4] = 3; sim.Rigs.HpPermille[4] = 640; sim.Rigs.UnpackTicksLeft[4] = 9;
        sim.Rigs.UnpackX[4] = 11; sim.Rigs.UnpackY[4] = 12; sim.Rigs.PausedQueue[4] = 2;
        byte[] full = Bytes(sim.Rigs);
        var other = ScriptedGame.NewSim(ScriptedGame.Setup());
        ReadBack(other.Rigs, full);
        Assert.Equal(full, Bytes(other.Rigs));
        Assert.Equal(sim.ComputeStateHash(), other.ComputeStateHash());
    }

    [Fact]
    public void SalvageState_RoundTrips_AndEveryFieldChangesTheHash()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        byte[] empty = Bytes(sim.Salvage);
        ulong h0 = sim.ComputeStateHash();
        var fields = new (string Name, int[] Array)[]
        {
            ("CellX", sim.Salvage.CellX), ("CellY", sim.Salvage.CellY), ("Value", sim.Salvage.Value),
            ("ValueStart", sim.Salvage.ValueStart), ("TicksLeft", sim.Salvage.TicksLeft),
        };
        foreach ((string name, int[] array) in fields)
        {
            array[5] = 33;
            Assert.NotEqual(h0, sim.ComputeStateHash());
            array[5] = 0;
            Assert.Equal(h0, sim.ComputeStateHash());
        }
        sim.Salvage.CellX[1] = 4; sim.Salvage.CellY[1] = 6; sim.Salvage.Value[1] = 80; sim.Salvage.ValueStart[1] = 100; sim.Salvage.TicksLeft[1] = 900;
        sim.Salvage.CellX[256] = 1; sim.Salvage.TicksLeft[256] = 1;   // last usable slot
        Assert.Equal(2, sim.Salvage.Count);
        byte[] full = Bytes(sim.Salvage);
        Assert.NotEqual(empty, full);
        var other = ScriptedGame.NewSim(ScriptedGame.Setup());
        ReadBack(other.Salvage, full);
        Assert.Equal(full, Bytes(other.Salvage));
        Assert.Equal(sim.ComputeStateHash(), other.ComputeStateHash());
        sim.Salvage.Clear(1);
        Assert.Equal(1, sim.Salvage.Count);
    }

    [Fact]
    public void NewParts_SurviveSaveAndLoad()
    {
        var setup = ScriptedGame.Setup();
        var sim = ScriptedGame.NewSim(setup);
        sim.Step(Array.Empty<Command>());
        sim.Rigs.PackedFrom[1] = 2; sim.Rigs.HpPermille[1] = 500; sim.Rigs.PackTicksLeft[1] = 7;
        sim.Salvage.TicksLeft[3] = 12; sim.Salvage.Value[3] = 40; sim.Salvage.ValueStart[3] = 50; sim.Salvage.CellX[3] = 9;
        sim.Effects.StormTicks[2] = 30; sim.Effects.StormRadius[2] = 5; sim.Effects.StormX[2] = 10; sim.Effects.StormY[2] = 11; sim.Effects.StormOwner[2] = 1;
        sim.Effects.VehicleWithdraw[1] = 1; sim.Effects.InfantryWithdraw[1] = 1;
        var ms = new MemoryStream();
        sim.Save(ms);
        var loaded = Simulation.Load(new MemoryStream(ms.ToArray()), setup);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(2, loaded.Rigs.PackedFrom[1]);
        Assert.Equal(12, loaded.Salvage.TicksLeft[3]);
        Assert.Equal(30, loaded.Effects.StormTicks[2]);
        Assert.Equal(1, loaded.Effects.VehicleWithdraw[1]);
    }

    [Fact]
    public void RigAndWithdrawSlots_AreClearedWhenActorsSpawnAndDie()
    {
        var sim = new Simulation(ScriptedGame.Setup());       // empty stores
        sim.Rigs.PackedFrom[1] = 4; sim.Rigs.PausedQueue[1] = 3; sim.Effects.VehicleWithdraw[1] = 1;   // stale data left in a free slot
        EntityId v = sim.SpawnVehicle(0, 0, new CellCoord(2, 2));
        Assert.Equal(1, v.Index);
        Assert.Equal((0, 0, 0), (sim.Rigs.PackedFrom[1], sim.Rigs.PausedQueue[1], (int)sim.Effects.VehicleWithdraw[1]));
        sim.Rigs.PackedFrom[1] = 4; sim.Effects.VehicleWithdraw[1] = 1;
        sim.Step(new[] { new Command(sim.Tick, 0, 1, CommandKind.Debug, v, default, default, (int)DebugOp.Kill, (int)ActorKind.Vehicle) });
        sim.Step(Array.Empty<Command>());
        Assert.False(sim.Units.Slots.IsAlive(v));
        Assert.Equal((0, 0), (sim.Rigs.PackedFrom[1], (int)sim.Effects.VehicleWithdraw[1]));
    }

    [Fact]
    public void RigPackTicks_AreClearedWhenABuildingIsPlacedAndRemoved()
    {
        var sim = new Simulation(ScriptedGame.Setup());
        sim.Rigs.PackTicksLeft[1] = 9;   // stale
        EntityId b = sim.PlaceBuilding(0, 0, new CellCoord(10, 10));
        Assert.Equal(1, b.Index);
        Assert.Equal(0, sim.Rigs.PackTicksLeft[1]);
        sim.Rigs.PackTicksLeft[1] = 9;
        sim.Step(new[] { new Command(sim.Tick, 0, 1, CommandKind.Debug, b, default, default, (int)DebugOp.Kill, (int)ActorKind.Building) });
        sim.Step(Array.Empty<Command>());
        Assert.False(sim.Buildings.Slots.IsAlive(b));
        Assert.Equal(0, sim.Rigs.PackTicksLeft[1]);
    }

    // ---- effects part version 2 -----------------------------------------------------------------

    /// <summary>The version-1 byte layout, written by hand so a later Write cannot hide a reader regression.</summary>
    private static byte[] EffectsV1(int vehicles, int infantry, int buildings)
    {
        var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)1);
        w.Write(vehicles); w.Write(infantry); w.Write(buildings);
        w.Write(2); w.Write(1100); w.Write(900); w.Write(15); w.Write(0);   // vehicle slot 2: rof, speed, still; end
        w.Write(0);                                                           // no infantry
        w.Write(3); w.Write(77); w.Write(0);                                  // building slot 3 charge 77; end
        return ms.ToArray();
    }

    [Fact]
    public void EffectsVersion1_StillReads_WithEmptyNewFields()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        var lim = sim.Setup.Limits;
        sim.Effects.StormTicks[0] = 5; sim.Effects.VehicleWithdraw[1] = 1;   // stale values must be cleared by the read
        ReadBack(sim.Effects, EffectsV1(lim.Vehicles, lim.Infantry, lim.Buildings));
        Assert.Equal((1100, 900, 15), (sim.Effects.VehicleRofPermille[2], sim.Effects.VehicleSpeedPermille[2], sim.Effects.VehicleStill[2]));
        Assert.Equal(77, sim.Effects.BuildingCharge[3]);
        Assert.All(sim.Effects.StormTicks, v => Assert.Equal(0, v));
        Assert.All(sim.Effects.VehicleWithdraw, v => Assert.Equal(0, v));
        byte[] again = Bytes(sim.Effects);
        Assert.Equal(2, again[0]);   // rewritten as version 2
        var other = ScriptedGame.NewSim(ScriptedGame.Setup());
        ReadBack(other.Effects, again);
        Assert.Equal(again, Bytes(other.Effects));
    }

    [Fact]
    public void EffectsVersion3_IsRejected()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        byte[] bytes = Bytes(sim.Effects);
        bytes[0] = 3;
        Assert.Throws<InvalidDataException>(() => ReadBack(sim.Effects, bytes));
    }

    [Fact]
    public void EffectsVersion2_RoundTrips_AndEveryNewFieldChangesTheHash()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        ulong h0 = sim.ComputeStateHash();
        EffectsState e = sim.Effects;
        Assert.Equal(EffectsState.MaxStorms, e.StormTicks.Length);
        var ints = new (string Name, int[] Array)[] { ("StormX", e.StormX), ("StormY", e.StormY), ("StormRadius", e.StormRadius), ("StormTicks", e.StormTicks) };
        foreach ((string name, int[] array) in ints)
        {
            array[1] = 9;
            Assert.NotEqual(h0, sim.ComputeStateHash());
            array[1] = 0;
            Assert.Equal(h0, sim.ComputeStateHash());
        }
        foreach (byte[] array in new[] { e.StormOwner, e.VehicleWithdraw, e.InfantryWithdraw })
        {
            array[1] = 1;
            Assert.NotEqual(h0, sim.ComputeStateHash());
            array[1] = 0;
            Assert.Equal(h0, sim.ComputeStateHash());
        }
        e.StormX[0] = 3; e.StormY[0] = 4; e.StormRadius[0] = 5; e.StormTicks[0] = 6; e.StormOwner[0] = 1;
        e.StormTicks[7] = 1; e.VehicleWithdraw[2] = 1; e.InfantryWithdraw[3] = 1;
        byte[] bytes = Bytes(e);
        var other = ScriptedGame.NewSim(ScriptedGame.Setup());
        ReadBack(other.Effects, bytes);
        Assert.Equal(bytes, Bytes(other.Effects));
        Assert.Equal(sim.ComputeStateHash(), other.ComputeStateHash());
    }
}
