// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Replay;
using Rts.Sim.Rules;
using Rts.Sim.Serialization;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Tests.Rules;

namespace Rts.Sim.Tests.Veterancy;

/// <summary>VET-1 contracts (docs/design/14_veterancy.md §3–§4): the [veterancy] rules, the schema v6 hash block, the "veterancy" part
/// and format 11. Behaviour (credit, bonuses) is tested in the VET-2 and VET-3 files.</summary>
public sealed class VeterancyContractTests
{
    private static RuleSet Load(params (string File, string? Content)[] overrides)
    {
        using var temp = new TempRules(overrides);
        return RulesLoader.Load(temp.Dir);
    }

    private static string Game(string veterancy) => TempRules.Real("game.toml") + "\n[veterancy]\n" + veterancy + "\n";

    private static byte[] Bytes(ISimStatePart p)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) p.Write(w);
        return ms.ToArray();
    }

    [Fact]
    public void Formats_AndPartOrder_ArePinned()
    {
        Assert.Equal(11, SimWriter.SaveVersion);
        Assert.Equal(11, ReplayWriter.Version);
        Assert.Equal(7, ReplayWriter.OldestReadableVersion);
        Assert.Equal(11, RulesHash.FormatVersion);
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        string[] names = sim.StateParts.Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "rigs", "salvage", "veterancy" }, names[^3..]);
        Assert.Same(sim.Veterancy, sim.StateParts[^1]);
    }

    [Fact]
    public void Defaults_AreOff_AndTheDesignTable()
    {
        VeterancyRules v = Load().Sim.Veterancy;
        Assert.False(v.Enabled);
        Assert.Equal(new[] { 600, 1600, 3200 }, v.Thresholds);
        Assert.Equal((1000, 500), (v.KillPermilleUnit, v.KillPermilleBuilding));
        Assert.Equal(new[] { 1000, 1000, 1000 }, v.DamagePermille);
        Assert.Equal(new[] { 0, 0, 10 }, v.HealPermille);
        Assert.Equal((20, 100), (v.HealIntervalTicks, v.HealQuietTicks));
    }

    [Fact]
    public void VeterancyTable_Parses()
    {
        RuleSet r = Load(("game.toml", Game("""
            enabled = true
            thresholds = [100, 200, 300]
            kill_permille_unit = 900
            kill_permille_building = 400
            damage_permille = [1100, 1200, 1300]
            taken_permille = [950, 900, 800]
            reload_permille = [950, 900, 800]
            speed_permille = [1000, 1050, 1100]
            heal_interval_ticks = 10
            heal_permille = [0, 5, 20]
            heal_quiet_ticks = 50
            """)));
        VeterancyRules v = r.Sim.Veterancy;
        Assert.True(v.Enabled);
        Assert.Equal(new[] { 100, 200, 300 }, v.Thresholds);
        Assert.Equal((900, 400), (v.KillPermilleUnit, v.KillPermilleBuilding));
        Assert.Equal(new[] { 1100, 1200, 1300 }, v.DamagePermille);
        Assert.Equal(new[] { 950, 900, 800 }, v.TakenPermille);
        Assert.Equal(new[] { 950, 900, 800 }, v.ReloadPermille);
        Assert.Equal(new[] { 1000, 1050, 1100 }, v.SpeedPermille);
        Assert.Equal((10, 50), (v.HealIntervalTicks, v.HealQuietTicks));
        Assert.Equal(new[] { 0, 5, 20 }, v.HealPermille);
    }

    [Fact]
    public void BadVeterancyTables_AreRejected()
    {
        Assert.Throws<RulesException>(() => Load(("game.toml", Game("thresholds = [100, 50, 300]"))));
        Assert.Throws<RulesException>(() => Load(("game.toml", Game("thresholds = [100, 200]"))));
        Assert.Throws<RulesException>(() => Load(("game.toml", Game("enabld = true"))));
        Assert.Throws<RulesException>(() => Load(("game.toml", Game("damage_permille = [0, 1000, 1000]"))));
    }

    [Fact]
    public void VeteranFlag_DefaultsToArmed_AndCanBeSet()
    {
        string veh = TempRules.Real("vehicles.toml");
        RuleSet r = Load(("vehicles.toml", veh + "\n[test_unarmed_vet]\nbase = \"test_crawler\"\nveteran = true\n\n[test_armed_novet]\nbase = \"test_tank\"\nveteran = false\n"));
        VehicleType crawler = r.Sim.Vehicles.Single(x => x.Id == "test_crawler");
        VehicleType tank = r.Sim.Vehicles.Single(x => x.Id == "test_tank");
        Assert.True(crawler.PrimaryWeapon < 0);
        Assert.False(crawler.CanRank);
        Assert.True(tank.PrimaryWeapon >= 0);
        Assert.True(tank.CanRank);
        Assert.True(r.Sim.Vehicles.Single(x => x.Id == "test_unarmed_vet").CanRank);
        Assert.False(r.Sim.Vehicles.Single(x => x.Id == "test_armed_novet").CanRank);
        InfantryType rifle = Load().Sim.Infantry.Single(x => x.Id == "test_rifleman");
        Assert.True(rifle.CanRank);
        RuleSet r2 = Load(("infantry.toml", TempRules.Real("infantry.toml") + "\n[test_medic_novet]\nbase = \"test_rifleman\"\nveteran = false\n"));
        Assert.False(r2.Sim.Infantry.Single(x => x.Id == "test_medic_novet").CanRank);
        Assert.Throws<RulesException>(() => Load(("vehicles.toml", veh + "\n[test_bad]\nbase = \"test_tank\"\nveteran = 3\n")));
    }

    [Fact]
    public void ThresholdEffect_IsAcceptedAndApplied()
    {
        string techs = TempRules.Real("techs.toml") + "\n[[effect]]\nid = \"test_vet_drill\"\nop = \"mul\"\ntarget = \"class:infantry\"\nattr = \"vet_threshold_permille\"\nvalue = 800\n";
        string upgrades = TempRules.Real("upgrades.toml") + "\n[test_vet_upgrade]\nfaction = \"authority\"\ncost = 100\neffects = [\"test_vet_drill\"]\n";
        RuleSet r = Load(("techs.toml", techs), ("upgrades.toml", upgrades));
        int u = r.Sim.UpgradeIndex("test_vet_upgrade");
        Assert.True(u >= 0);
        SimRules derived = RulesDeriver.Derive(r.Sim, 1UL << u);
        Assert.All(derived.Infantry, i => Assert.Equal(800, i.VetThresholdPermille));
        Assert.All(derived.Vehicles, x => Assert.Equal(1000, x.VetThresholdPermille));
        Assert.All(r.Sim.Infantry, i => Assert.Equal(1000, i.VetThresholdPermille));
    }

    [Fact]
    public void EveryNewRulesField_ChangesTheRulesHash()
    {
        ulong baseline = Load().RulesHash;
        var seen = new HashSet<ulong> { baseline };
        foreach (string line in new[]
        {
            "enabled = true", "thresholds = [601, 1600, 3200]", "thresholds = [600, 1601, 3200]", "thresholds = [600, 1600, 3201]",
            "kill_permille_unit = 999", "kill_permille_building = 499",
            "damage_permille = [1001, 1000, 1000]", "damage_permille = [1000, 1001, 1000]", "damage_permille = [1000, 1000, 1001]",
            "taken_permille = [999, 1000, 1000]", "reload_permille = [999, 1000, 1000]", "speed_permille = [1000, 1001, 1000]",
            "heal_interval_ticks = 21", "heal_permille = [1, 0, 10]", "heal_permille = [0, 0, 11]", "heal_quiet_ticks = 101",
        })
            Assert.True(seen.Add(Load(("game.toml", Game(line))).RulesHash), line);
        string veh = TempRules.Real("vehicles.toml");
        Assert.True(seen.Add(Load(("vehicles.toml", veh + "\n[test_x]\nbase = \"test_tank\"\nveteran = false\n")).RulesHash), "vehicle veteran");
        Assert.True(seen.Add(Load(("vehicles.toml", veh + "\n[test_x]\nbase = \"test_crawler\"\nveteran = true\n")).RulesHash), "vehicle veteran on");
        Assert.True(seen.Add(Load(("infantry.toml", TempRules.Real("infantry.toml") + "\n[test_x]\nbase = \"test_rifleman\"\nveteran = false\n")).RulesHash), "infantry veteran");
    }

    [Fact]
    public void VeterancyState_RoundTrips_AndEveryFieldChangesTheHash()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        byte[] empty = Bytes(sim.Veterancy);
        ulong h0 = sim.ComputeStateHash();
        var fields = new (string Name, int[] Array)[]
        {
            ("VehicleKillValue", sim.Veterancy.VehicleKillValue), ("VehicleLastCombat", sim.Veterancy.VehicleLastCombat),
            ("InfantryKillValue", sim.Veterancy.InfantryKillValue), ("InfantryLastCombat", sim.Veterancy.InfantryLastCombat),
        };
        foreach ((string name, int[] array) in fields)
        {
            array[3] = 17;
            Assert.NotEqual(h0, sim.ComputeStateHash());
            Assert.NotEqual(empty, Bytes(sim.Veterancy));
            array[3] = 0;
            Assert.Equal(h0, sim.ComputeStateHash());
            Assert.Equal(empty, Bytes(sim.Veterancy));
        }
        sim.Veterancy.VehicleKillValue[2] = 700; sim.Veterancy.VehicleLastCombat[2] = 55;
        sim.Veterancy.InfantryKillValue[4] = 1700; sim.Veterancy.InfantryLastCombat[5] = 9;
        byte[] full = Bytes(sim.Veterancy);
        var other = ScriptedGame.NewSim(ScriptedGame.Setup());
        using (var r = new BinaryReader(new MemoryStream(full)))
        {
            other.Veterancy.Read(r);
            Assert.Equal(full.Length, r.BaseStream.Position);
        }
        Assert.Equal(full, Bytes(other.Veterancy));
        Assert.Equal(sim.ComputeStateHash(), other.ComputeStateHash());
    }

    [Fact]
    public void VeterancyState_SurvivesSaveAndLoad()
    {
        var setup = ScriptedGame.Setup();
        var sim = ScriptedGame.NewSim(setup);
        sim.Veterancy.VehicleKillValue[3] = 1234; sim.Veterancy.InfantryLastCombat[2] = 77;
        var ms = new MemoryStream();
        sim.Save(ms);
        var loaded = Simulation.Load(new MemoryStream(ms.ToArray()), setup);
        Assert.Equal(1234, loaded.Veterancy.VehicleKillValue[3]);
        Assert.Equal(77, loaded.Veterancy.InfantryLastCombat[2]);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
    }

    [Fact]
    public void SlotReuse_StartsAtZero()
    {
        var setup = ScriptedGame.Setup();
        var sim = new Simulation(setup);
        // Spawn clears: dirty the next free slot, then spawn into it.
        sim.Veterancy.VehicleKillValue[1] = 99; sim.Veterancy.VehicleLastCombat[1] = 5;
        sim.Veterancy.InfantryKillValue[1] = 98; sim.Veterancy.InfantryLastCombat[1] = 4;
        EntityId v = sim.SpawnVehicle(0, 0, new CellCoord(5, 5));
        EntityId n = sim.SpawnInfantry(0, 0, new CellCoord(8, 8));
        Assert.Equal(1, v.Index);
        Assert.Equal(1, n.Index);
        Assert.Equal((0, 0, 0, 0), (sim.Veterancy.VehicleKillValue[1], sim.Veterancy.VehicleLastCombat[1],
            sim.Veterancy.InfantryKillValue[1], sim.Veterancy.InfantryLastCombat[1]));
        // Removal clears: a dying actor leaves nothing behind in its slot.
        sim.Veterancy.VehicleKillValue[1] = 99; sim.Veterancy.InfantryKillValue[1] = 98;
        sim.Units.Health[1] = 0; sim.Infantry.Health[1] = 0;
        sim.Step(Array.Empty<Command>());
        Assert.False(sim.Units.Slots.IsIndexAlive(1));
        Assert.Equal(0, sim.Veterancy.VehicleKillValue[1]);
        Assert.Equal(0, sim.Veterancy.InfantryKillValue[1]);
    }
}
