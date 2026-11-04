// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Rules;
using static Rts.Sim.Tests.M4.RulesTierFixture;

namespace Rts.Sim.Tests.M4;

/// <summary>M4-04: per-player derived rules (RulesDeriver, RulesOf): effects apply for the researching player only, in upgrade
/// order then list order, on every target kind; the cache is not state and is rebuilt after Load with the same hash.</summary>
public sealed class RulesDeriverTests
{
    [Fact]
    public void MulMaxHealth_ChangesRulesOfThatPlayerOnly()
    {
        Simulation sim = NewSim();
        int crawler = Veh("test_crawler");
        Assert.Same(Base, sim.RulesOf(0));
        Run(sim, 1, Grant(sim, Up("test_up_armor")));
        Assert.Equal(330, sim.RulesOf(0).Vehicles[crawler].MaxHealth);   // class:vehicle × 1100
        Assert.Equal(300, sim.RulesOf(1).Vehicles[crawler].MaxHealth);   // the other player
        Assert.Equal(300, Base.Vehicles[crawler].MaxHealth);              // the base is untouched
        Assert.Same(Base, sim.RulesOf(1));
        Assert.Same(sim.RulesOf(0), sim.RulesOf(0));                      // cached
    }

    [Fact]
    public void Effects_ApplyInListOrder()
    {
        SimRules r = RulesDeriver.Derive(Base, 1UL << Up("test_up_armor"));
        Assert.Equal(540, r.Vehicles[Veh("test_tank")].MaxHealth);       // (400 × 1100 / 1000) + 100, not (400 + 100) × 1.1
        Assert.Equal(165, r.Vehicles[Veh("test_jeep")].MaxHealth);
    }

    [Fact]
    public void EveryTargetKind_IsApplied()
    {
        SimRules r = RulesDeriver.Derive(Base, 1UL << Up("test_up_mixed"));
        WeaponType rifle = Base.Weapons[Wpn("test_rifle")];
        Assert.Equal(rifle.Damage * 1500 / 1000, r.Weapons[Wpn("test_rifle")].Damage);                     // weapon:
        Assert.Equal(Base.Weapons[Wpn("test_cannon")], r.Weapons[Wpn("test_cannon")]);
        Assert.Equal(Base.Warheads[Wh("test_he")].Contaminate + 40, r.Warheads[Wh("test_he")].Contaminate);  // warhead:
        Assert.Equal(Base.Buildings[Bld("test_airfield")].Sight + 2, r.Buildings[Bld("test_airfield")].Sight);  // provides name
        Assert.Equal(Base.Buildings[Bld("test_hq")].Sight, r.Buildings[Bld("test_hq")].Sight);
        Assert.Equal(123, r.Vehicles[Veh("test_jeep")].Cost);                                                 // set
        for (int i = 0; i < Base.Infantry.Count; i++)                                                          // class:infantry
            Assert.Equal(Base.Infantry[i].MaxHealth * 2, r.Infantry[i].MaxHealth);
        Assert.Equal(Base.Vehicles[Veh("test_tank")], r.Vehicles[Veh("test_tank")]);
    }

    [Fact]
    public void NoEffects_ReturnsTheBaseRules()
    {
        Assert.Same(Base, RulesDeriver.Derive(Base, 0));
        Assert.Same(Base, RulesDeriver.Derive(Base, 1UL << Up("test_up_unlock")));   // an upgrade without effects
        Assert.Same(Base, RulesDeriver.Derive(Base, 1UL << Up("auth_grouting")));    // special-behaviour only
    }

    [Fact]
    public void CostUpgrade_ReachesTheHudAndTheBill()
    {
        Simulation sim = NewSim(("test_factory", 4, 9));
        int jeep = Veh("test_jeep");
        Assert.Equal(400, sim.HudFor(0).Options.Single(o => o.Category == ProductionCategory.Vehicle && o.TypeIndex == jeep).Cost);
        Run(sim, 1, Grant(sim, Up("test_up_mixed")));
        Assert.Equal(123, sim.HudFor(0).Options.Single(o => o.Category == ProductionCategory.Vehicle && o.TypeIndex == jeep).Cost);
        int cash = sim.Players.Cash[0];
        Run(sim, 1, Build(sim, ProductionCategory.Vehicle, jeep));
        for (int i = 0; i < 2000 && sim.Production.QueueLength(0, ProductionCategory.Vehicle) > 0; i++) sim.Step(None);
        Assert.Equal(cash - 123, sim.Players.Cash[0]);
    }

    [Fact]
    public void Cache_IsRebuiltAfterLoad_WithTheSameHash()
    {
        Simulation sim = NewSim();
        Run(sim, 3, Grant(sim, Up("test_up_armor")), Grant(sim, Up("test_up_mixed"), 1));
        Simulation loaded = Roundtrip(sim);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        for (byte p = 0; p < 2; p++)
        {
            Assert.Equal(sim.RulesOf(p).Vehicles, loaded.RulesOf(p).Vehicles);
            Assert.Equal(sim.RulesOf(p).Infantry, loaded.RulesOf(p).Infantry);
            Assert.Equal(sim.RulesOf(p).Weapons, loaded.RulesOf(p).Weapons);
            Assert.Equal(sim.RulesOf(p).Buildings, loaded.RulesOf(p).Buildings);
        }
        Assert.Equal(540, loaded.RulesOf(0).Vehicles[Veh("test_tank")].MaxHealth);
        Assert.Equal(123, loaded.RulesOf(1).Vehicles[Veh("test_jeep")].Cost);
        Run(sim, 20); Run(loaded, 20);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
    }

    [Fact]
    public void Research_ThenRulesOf_IsDeterministic()
    {
        static ulong Play()
        {
            Simulation sim = NewSim(("test_lab", 16, 4));
            Run(sim, 1, Build(sim, ProductionCategory.Upgrade, Up("test_up_armor")));
            Run(sim, 60);
            Assert.True(sim.HasUpgrade(0, Up("test_up_armor")));
            Assert.Equal(540, sim.RulesOf(0).Vehicles[Veh("test_tank")].MaxHealth);
            return sim.ComputeStateHash();
        }
        Assert.Equal(Play(), Play());
    }
}

/// <summary>
/// M4-04 fixture: the test rules plus tier gates (test_gate2 provides test_t2, test_gate3 provides test_t3), a research lab, an
/// airfield, an air vehicle (test_plane), a tier-2 infantry (test_elite), a vehicle locked behind an upgrade id (test_secret) and
/// upgrades whose effects cover every target kind. Loaded through <see cref="RulesLoader"/>, so tier sugar is the loader's.
/// </summary>
internal static class RulesTierFixture
{
    public static readonly Command[] None = Array.Empty<Command>();
    private static ushort _seq;

    private const string ExtraBuildings = """

        [test_lab]
        footprint_w = 2
        footprint_h = 2
        max_health = 500
        armor = "structure"
        sight = 3
        primary_weapon = ""
        cost = 300
        power = 0
        prerequisites = ["test_hq"]
        provides = []
        researches = true

        [test_airfield]
        footprint_w = 3
        footprint_h = 3
        max_health = 800
        armor = "structure"
        sight = 4
        primary_weapon = ""
        cost = 1000
        power = 0
        prerequisites = ["test_hq"]
        provides = ["test_airbase"]
        produces = "aircraft"

        [test_gate2]
        footprint_w = 2
        footprint_h = 2
        max_health = 600
        armor = "structure"
        sight = 3
        primary_weapon = ""
        cost = 500
        power = 0
        prerequisites = ["test_hq"]
        provides = ["test_t2"]
        tier = 2

        [test_gate3]
        footprint_w = 2
        footprint_h = 2
        max_health = 600
        armor = "structure"
        sight = 3
        primary_weapon = ""
        cost = 500
        power = 0
        prerequisites = ["test_gate2"]
        provides = ["test_t3"]
        tier = 3

        [test_bunker2]
        base = "test_power"
        tier = 2
        """;

    private const string ExtraVehicles = """

        [test_plane]
        speed = 24
        max_health = 200
        locomotion = "air"
        turn_rate = 8
        armor = "light"
        sight = 6
        primary_weapon = ""
        has_turret = false
        turret_turn_rate = 0
        crusher = false
        cost = 300
        build_time = 40
        prerequisites = ["test_airbase"]

        [test_secret]
        base = "test_tank"
        prerequisites = ["test_factory", "test_up_unlock"]
        """;

    private const string ExtraInfantry = """

        [test_elite]
        base = "test_rifleman"
        tier = 2
        """;

    private const string ExtraEffects = """

        [[effect]]
        id = "test_tank_hp"
        op = "add"
        target = "test_tank"
        attr = "max_health"
        value = 100

        [[effect]]
        id = "test_rifle_dmg"
        op = "mul"
        target = "weapon:test_rifle"
        attr = "damage"
        value = 1500

        [[effect]]
        id = "test_he_hot"
        op = "add"
        target = "warhead:test_he"
        attr = "contaminate"
        value = 40

        [[effect]]
        id = "test_air_sight"
        op = "add"
        target = "test_airbase"
        attr = "sight"
        value = 2

        [[effect]]
        id = "test_cheap_jeep"
        op = "set"
        target = "test_jeep"
        attr = "cost"
        value = 123

        [[effect]]
        id = "test_inf_hp"
        op = "mul"
        target = "class:infantry"
        attr = "max_health"
        value = 2000
        """;

    private const string ExtraUpgrades = """

        [test_up_armor]
        faction = "authority"
        cost = 200
        build_time = 20
        prerequisites = []
        effect = ""
        effects = ["test_armor_up", "test_tank_hp"]

        [test_up_mixed]
        faction = "authority"
        cost = 200
        build_time = 20
        prerequisites = []
        effect = ""
        effects = ["test_rifle_dmg", "test_he_hot", "test_air_sight", "test_cheap_jeep", "test_inf_hp"]

        [test_up_unlock]
        faction = "authority"
        cost = 200
        build_time = 20
        prerequisites = []
        effect = ""

        [test_up_t2]
        faction = "authority"
        cost = 200
        build_time = 20
        prerequisites = []
        effect = ""
        tier = 2
        """;

    private static readonly Lazy<RuleSet> LoadedLazy = new(() =>
    {
        using var temp = new TempRules(
            ("buildings.toml", TempRules.Real("buildings.toml") + ExtraBuildings),
            ("vehicles.toml", TempRules.Real("vehicles.toml") + ExtraVehicles),
            ("infantry.toml", TempRules.Real("infantry.toml") + ExtraInfantry),
            ("techs.toml", TempRules.Real("techs.toml") + ExtraEffects),
            ("upgrades.toml", TempRules.Real("upgrades.toml") + ExtraUpgrades),
            ("game.toml", TempRules.RealWith("game.toml", "conyard = \"auth_conyard\"",
                "conyard = \"auth_conyard\"\ntier2 = \"test_t2\"\ntier3 = \"test_t3\"")));
        return RulesLoader.Load(temp.Dir);
    });

    public static RuleSet Loaded => LoadedLazy.Value;
    public static SimRules Base => Loaded.Sim;

    public static int Veh(string id) => SimRules.IndexOf(Base.Vehicles, id, v => v.Id);
    public static int Inf(string id) => SimRules.IndexOf(Base.Infantry, id, v => v.Id);
    public static int Bld(string id) => SimRules.IndexOf(Base.Buildings, id, v => v.Id);
    public static int Wpn(string id) => SimRules.IndexOf(Base.Weapons, id, v => v.Id);
    public static int Wh(string id) => SimRules.IndexOf(Base.Warheads, id, v => v.Id);
    public static int Up(string id) => Base.UpgradeIndex(id);

    /// <summary>Two Authority players on a flat 40×40 map, debug on (GrantUpgrade), plentiful cash; player 0 has an HQ at (4,4)
    /// and a power plant at (12,4) plus <paramref name="extra"/>.</summary>
    public static Simulation NewSim(params (string Id, int X, int Y)[] extra)
    {
        var sim = new Simulation(new SimSetup(7, MapData.CreateFlat(40, 40), 2, Base, Loaded.RulesHash) { AllowDebug = true });
        Place(sim, 0, "test_hq", 4, 4);
        Place(sim, 0, "test_power", 12, 4);
        foreach (var (id, x, y) in extra) Place(sim, 0, id, x, y);
        for (int p = 0; p < 2; p++) sim.Players.Cash[p] = 20000;
        return sim;
    }

    public static EntityId Place(Simulation sim, byte player, string id, int x, int y)
    {
        EntityId b = sim.PlaceBuilding(player, Bld(id), new CellCoord(x, y));
        if (b.IsNone) throw new InvalidOperationException("setup place " + id);
        return b;
    }

    public static Command Cmd(Simulation sim, byte player, CommandKind kind, EntityId subject = default, int arg = 0, int arg2 = 0) =>
        new(sim.Tick, player, _seq++, kind, subject, default, default, arg, arg2);

    public static Command Grant(Simulation sim, int upgrade, byte player = 0) =>
        Cmd(sim, player, CommandKind.Debug, default, (int)DebugOp.GrantUpgrade, upgrade);

    public static Command Kill(Simulation sim, EntityId building) =>
        Cmd(sim, 0, CommandKind.Debug, building, (int)DebugOp.Kill, (int)ActorKind.Building);

    public static Command Build(Simulation sim, ProductionCategory cat, int type, byte player = 0) =>
        Cmd(sim, player, CommandKind.Build, default, (int)cat, type);

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
