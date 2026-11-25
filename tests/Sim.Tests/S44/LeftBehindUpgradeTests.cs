// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.Combat.CombatFixture;
using static Rts.Sim.Tests.Economy.EconomyFixture;

namespace Rts.Sim.Tests.S44;

/// <summary>S44 M-07 (Salvage Rights wreck value), M-08 (Scrap Plate armour), M-09 (Patchwork range), M-11 (aura_targets for repair).</summary>
public sealed class LeftBehindUpgradeTests
{
    private static SimRules LoadReal()
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        return RulesLoader.Load(dir!).Sim;
    }

    private static UpgradeType Up(SimRules r, string id) => r.Upgrades.Single(u => u.Id == id);

    private static ulong Bit(SimRules r, string id) => 1UL << r.Upgrades.ToList().FindIndex(u => u.Id == id);

    // ---- M-07 -----------------------------------------------------------------------------------

    private static int PaidForWreck(int wreckValuePermille)
    {
        SimRules r = Loaded.Sim;
        var inf = r.Infantry.ToArray();
        int rf = Inf("test_rifleman");
        inf[rf] = inf[rf] with { SalvageRate = 30 };
        var ups = r.Upgrades.ToArray();
        ups[0] = ups[0] with { WreckValuePermille = wreckValuePermille };
        r = r with { Infantry = inf, Upgrades = ups, Economy = r.Economy with { WreckTicks = 1_000_000, WreckMaxValue = 1000 } };
        var sim = NewSim(PaintMap(OverlayKind.None, 0), r);
        sim.PlaceBuilding(0, Bld("test_silo"), new CellCoord(4, 4));
        EntityId t = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(10, 10));
        var wreck = new CellCoord(12, 10);
        sim.Step(new[] { Debug(sim, 1, DebugOp.Kill, sim.SpawnVehicle(1, Veh("test_jeep"), wreck), (int)ActorKind.Vehicle) });   // wreck worth 100
        var cmds = new List<Command> { new(sim.Tick, 0, 200, CommandKind.Harvest, t, default, wreck, (int)ActorKind.Infantry) };
        if (wreckValuePermille > 0) cmds.Add(Debug(sim, 0, DebugOp.GrantUpgrade, default, 0));
        sim.Step(cmds.ToArray());
        RunUntil(sim, () => sim.Salvage.Count == 0, 2000);
        return sim.Players.Stored[0];
    }

    [Fact]
    public void SalvageRights_RaisesTheCreditsAWreckPays()
    {
        int plain = PaidForWreck(0), rights = PaidForWreck(1500);
        Assert.InRange(plain, 99, 100);
        Assert.InRange(rights, 148, 150);
    }

    [Fact]
    public void SalvageRights_DataSetsWreckValue()
    {
        UpgradeType u = Up(LoadReal(), "lb_up_salvage_rights");
        Assert.Equal(1500, u.WreckValuePermille);
        Assert.Equal(1500, u.SalvagePermille);
    }

    // ---- M-08 -----------------------------------------------------------------------------------

    [Fact]
    public void ScrapPlate_StepsEveryLightLeftBehindVehicleAndAircraftToHeavy()
    {
        SimRules real = LoadReal();
        SimRules plated = RulesDeriver.Derive(real, Bit(real, "lb_up_scrap_plate"));
        string[] light = ["lb_hauler", "lb_buggy", "lb_harpoon_wagon", "lb_organ", "lb_tow", "lb_dust_sail", "lb_rotor", "lb_kite"];
        foreach (string id in light)
        {
            VehicleType before = real.Vehicles.Single(v => v.Id == id), after = plated.Vehicles.Single(v => v.Id == id);
            Assert.Equal(ArmorClass.Light, before.Armor);
            Assert.Equal(ArmorClass.Heavy, after.Armor);
        }
        foreach (string id in new[] { "lb_gleaner", "lb_rolling_fort", "lb_mother_rig_packed" })
            Assert.Equal(ArmorClass.Heavy, plated.Vehicles.Single(v => v.Id == id).Armor);
        Assert.Equal(ArmorClass.Light, real.Vehicles.Single(v => v.Id == "lb_rotor").Armor);   // base rules untouched
    }

    [Fact]
    public void ScrapPlate_AlreadyHeavyUnitsKeepTheirHealthBonus()
    {
        SimRules real = LoadReal();
        SimRules plated = RulesDeriver.Derive(real, Bit(real, "lb_up_scrap_plate"));
        foreach (string id in new[] { "lb_gleaner", "lb_rolling_fort", "lb_mother_rig_packed" })
            Assert.True(plated.Vehicles.Single(v => v.Id == id).MaxHealth > real.Vehicles.Single(v => v.Id == id).MaxHealth, id);
    }

    // ---- M-09 / M-11 ----------------------------------------------------------------------------

    private const int Hull = 0, Medic = 1, Other = 2;
    private static readonly Command[] None = Array.Empty<Command>();

    private static Simulation RepairSim()
    {
        var none = Array.Empty<string>();
        VehicleType V(string id) => new(id, Locomotion.Tracked, 16, 8, 200, ArmorClass.Light, 5, -1, false, 0, false, 0, none);
        var vehicles = new[]
        {
            V("hull"),
            V("medic") with { AuraRadius = 3, AuraRepairPermille = 100, AuraTargets = new[] { "hull" } },
            V("other"),
        };
        InfantryType I(string id) => new(id, 9, 50, ArmorClass.Unarmored, 4, -1, 0, none);
        var infantry = new[] { I("rifle") };
        var buildings = new[] { new BuildingType("hq", 2, 2, 400, ArmorClass.Structure, 5, -1, 0, 0, none, new[] { "hq" }) };
        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        Array.Fill(speed, 100);
        var buildable = new bool[RuleCounts.LandTypes];
        buildable[(int)LandType.Clear] = true;
        var rules = new SimRules(vehicles, infantry, buildings, Array.Empty<WeaponType>(), Array.Empty<WarheadType>(),
            new TerrainTable(speed, buildable), Array.Empty<EffectDef>())
        {
            Upgrades = new[] { new UpgradeType("patch", Faction.Authority, 100, 10, none, "p") { RepairsVehiclesAtRange = true, RepairRangeCells = 6, RepairRatePermille = 2000 } },
        };
        return new Simulation(new SimSetup(1, MapData.CreateFlat(40, 40), 2, rules, 0x44UL) { AllowDebug = true });
    }

    private static void Run(Simulation sim, int n) { for (int i = 0; i < n; i++) sim.Step(None); }

    [Fact]
    public void RepairAura_HealsOnlyListedTargets()
    {
        Simulation sim = RepairSim();
        sim.SpawnVehicle(0, Medic, new CellCoord(10, 10));
        EntityId listed = sim.SpawnVehicle(0, Hull, new CellCoord(11, 10));
        EntityId unlisted = sim.SpawnVehicle(0, Other, new CellCoord(11, 11));
        sim.Units.Health[listed.Index] = 100; sim.Units.Health[unlisted.Index] = 100;
        Run(sim, 1);
        Assert.Equal(120, sim.Units.Health[listed.Index]);
        Assert.Equal(100, sim.Units.Health[unlisted.Index]);
    }

    [Fact]
    public void Patchwork_ExtendsRangeAndRate()
    {
        Simulation sim = RepairSim();
        sim.SpawnVehicle(0, Medic, new CellCoord(10, 10));
        EntityId near = sim.SpawnVehicle(0, Hull, new CellCoord(12, 10));
        EntityId far = sim.SpawnVehicle(0, Hull, new CellCoord(18, 10));   // 8 cells: outside 3, inside 3 + 6
        sim.Units.Health[near.Index] = 100; sim.Units.Health[far.Index] = 100;
        Run(sim, 1);
        Assert.Equal(120, sim.Units.Health[near.Index]);
        Assert.Equal(100, sim.Units.Health[far.Index]);
        sim.Players.Upgrades[0] |= 1UL;
        Run(sim, 10);
        Assert.Equal(160, sim.Units.Health[near.Index]);   // +200 * 100 * 2 / 1000 = +40
        Assert.Equal(140, sim.Units.Health[far.Index]);
    }

    [Fact]
    public void RealData_TinkerAndMenderTargetsMakeSense()
    {
        SimRules r = LoadReal();
        InfantryType tinker = r.Infantry.Single(i => i.Id == "lb_tinker");
        Assert.Contains("lb_gleaner", tinker.AuraTargets);
        Assert.DoesNotContain("lb_rotor", tinker.AuraTargets);
        Assert.DoesNotContain("lb_kite", tinker.AuraTargets);
        VehicleType mender = r.Vehicles.Single(v => v.Id == "exp_repair_hover");
        Assert.NotEmpty(mender.AuraTargets);
        Assert.All(mender.AuraTargets, id => Assert.StartsWith("exp_", id));
        // s45: every Expedition vehicle that is not an aircraft is on the Repair Hover's list (the Canister Skiff and Supply Tender
        // were missing after M-11 and the Expedition lost 3-13 points per balance row).
        foreach (VehicleType v in r.Vehicles)
            if (v.Faction == mender.Faction && v.Locomotion != Locomotion.Air) Assert.Contains(v.Id, mender.AuraTargets);
        UpgradeType p = Up(r, "lb_up_patchwork");
        Assert.True(p.RepairsVehiclesAtRange);
        Assert.True(p.RepairRangeCells > 2);
    }
}
