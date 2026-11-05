// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Rules;

/// <summary>
/// Checks the shipped Left Behind data (design 06 §4-6, D-102, M6-08) in data/rules: every lb_ id loads with the right faction,
/// counts match the design, prerequisites and tiers resolve, the packed-rig start kit spawns, the Second Drift warhead carries storm
/// fields, the salvage / raider / aura flags sit on the right units, and a Left Behind skirmish runs 2 000 ticks. Reads the real data.
/// </summary>
public sealed class LeftBehindDataTests
{
    private static readonly string[] Infantry =
        ["lb_drifter_brute", "lb_hook_runner", "lb_scavenger", "lb_slinger", "lb_tinker", "lb_veilwalker"];

    private static readonly string[] Vehicles =
        ["lb_buggy", "lb_dust_sail", "lb_gleaner", "lb_harpoon_wagon", "lb_hauler", "lb_mother_rig_packed", "lb_organ", "lb_rolling_fort", "lb_tow"];

    private static readonly string[] Air = ["lb_kite", "lb_rotor"];

    private static readonly string[] Structures =
    [
        "lb_bench", "lb_dynamo_rig", "lb_flak_net", "lb_garage_rig", "lb_harpoon_post", "lb_lookout", "lb_mother_rig", "lb_muster_rig",
        "lb_refinery_rig", "lb_rotor_pad", "lb_scrap_wall", "lb_spike_nest", "lb_yard",
    ];

    private static readonly string[] Upgrades =
        ["lb_up_ash_wraps", "lb_up_grapple", "lb_up_patchwork", "lb_up_quick_pack", "lb_up_salvage_rights", "lb_up_scrap_plate"];

    private static SimRules LoadReal()
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        return RulesLoader.Load(dir!).Sim;
    }

    private static InfantryType Inf(SimRules s, string id) => s.Infantry.Single(i => i.Id == id);

    private static VehicleType Veh(SimRules s, string id) => s.Vehicles.Single(v => v.Id == id);

    private static bool IsAir(VehicleType v) => v.Locomotion == Locomotion.Air;

    [Fact]
    public void EveryLeftBehindId_LoadsWithTheFaction()
    {
        SimRules s = LoadReal();

        foreach (string id in Infantry) Assert.Equal(Faction.LeftBehind, Inf(s, id).Faction);
        foreach (string id in Vehicles.Concat(Air)) Assert.Equal(Faction.LeftBehind, Veh(s, id).Faction);
        foreach (string id in Structures.Concat(["lb_second_drift"])) Assert.Equal(Faction.LeftBehind, s.Buildings.Single(b => b.Id == id).Faction);
        foreach (string id in Upgrades) Assert.Equal(Faction.LeftBehind, s.Upgrades.Single(u => u.Id == id).Faction);
    }

    [Fact]
    public void Counts_MatchTheDesign()
    {
        SimRules s = LoadReal();

        Assert.Equal(6, s.Infantry.Count(i => i.Faction == Faction.LeftBehind));
        Assert.Equal(9, s.Vehicles.Count(v => v.Faction == Faction.LeftBehind && !IsAir(v)));
        Assert.Equal(2, s.Vehicles.Count(v => v.Faction == Faction.LeftBehind && IsAir(v)));
        Assert.Equal(13 + 1, s.Buildings.Count(b => b.Faction == Faction.LeftBehind));   // design 06 §5 lists 13 structures plus the superweapon (the brief's "12" miscounts)
        Assert.Equal(1, s.Buildings.Count(b => b.Faction == Faction.LeftBehind && b.Superweapon.Length > 0));
        Assert.Equal(6, s.Upgrades.Count(u => u.Faction == Faction.LeftBehind));
    }

    [Fact]
    public void Prerequisites_Resolve()
    {
        SimRules s = LoadReal();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (BuildingType b in s.Buildings) { names.Add(b.Id); foreach (string p in b.Provides) names.Add(p); }
        foreach (VehicleType v in s.Vehicles) names.Add(v.Id);
        foreach (InfantryType i in s.Infantry) names.Add(i.Id);
        foreach (UpgradeType u in s.Upgrades) names.Add(u.Id);

        foreach (BuildingType b in s.Buildings.Where(b => b.Faction == Faction.LeftBehind)) Assert.All(b.Prerequisites, p => Assert.Contains(p, names));
        foreach (VehicleType v in s.Vehicles.Where(v => v.Faction == Faction.LeftBehind)) Assert.All(v.Prerequisites, p => Assert.Contains(p, names));
        foreach (InfantryType i in s.Infantry.Where(i => i.Faction == Faction.LeftBehind)) Assert.All(i.Prerequisites, p => Assert.Contains(p, names));
        foreach (UpgradeType u in s.Upgrades.Where(u => u.Faction == Faction.LeftBehind)) Assert.All(u.Prerequisites, p => Assert.Contains(p, names));
    }

    [Fact]
    public void Tiers_FollowSectionFour()
    {
        SimRules s = LoadReal();

        Assert.All(s.Infantry.Where(i => i.Faction == Faction.LeftBehind), i => Assert.InRange(i.Tier, 1, 3));
        Assert.All(s.Vehicles.Where(v => v.Faction == Faction.LeftBehind), v => Assert.InRange(v.Tier, 1, 3));
        Assert.All(s.Buildings.Where(b => b.Faction == Faction.LeftBehind), b => Assert.InRange(b.Tier, 1, 3));
        foreach (string id in new[] { "lb_scavenger", "lb_hook_runner" }) Assert.Equal(1, Inf(s, id).Tier);
        foreach (string id in new[] { "lb_slinger", "lb_tinker" }) Assert.Equal(2, Inf(s, id).Tier);
        Assert.Equal(3, Inf(s, "lb_veilwalker").Tier);
        foreach (string id in new[] { "lb_gleaner", "lb_buggy", "lb_hauler" }) Assert.Equal(1, Veh(s, id).Tier);
        foreach (string id in new[] { "lb_harpoon_wagon", "lb_organ", "lb_tow", "lb_dust_sail" }) Assert.Equal(2, Veh(s, id).Tier);
        foreach (string id in new[] { "lb_rolling_fort", "lb_rotor", "lb_kite" }) Assert.Equal(3, Veh(s, id).Tier);
        Assert.Equal(3, s.Buildings.Single(b => b.Id == "lb_second_drift").Tier);

        FactionStartKit kit = s.StartKits[(int)Faction.LeftBehind];
        Assert.Equal("lb_tier2", kit.Tier2Provides);
        Assert.Equal("lb_tier3", kit.Tier3Provides);
        Assert.Contains("lb_tier2", s.Buildings.Single(b => b.Id == "lb_lookout").Provides);
        Assert.Contains("lb_tier3", s.Buildings.Single(b => b.Id == "lb_rotor_pad").Provides);
        // tier sugar: a tier-3 unit needs the tier-3 gate, a tier-2 unit the tier-2 gate
        Assert.Contains("lb_tier3", Veh(s, "lb_rotor").Prerequisites);
        Assert.Contains("lb_tier2", Veh(s, "lb_organ").Prerequisites);
    }

    [Fact]
    public void WreckTicks_AreOn()
    {
        Assert.Equal(1200, LoadReal().Economy.WreckTicks);
    }

    [Fact]
    public void StartKit_IsAPackedRigAndThreeUnits()
    {
        SimRules s = LoadReal();

        (int conyard, IReadOnlyList<(ActorKind Kind, int Type)> units) = s.ResolveStartKit(Faction.LeftBehind);

        Assert.Equal(-1, conyard);
        Assert.Equal(4, units.Count);
        Assert.Equal("lb_mother_rig_packed", s.Vehicles[units[0].Type].Id);
        Assert.Equal("lb_mother_rig", s.Buildings[s.Vehicles[units[0].Type].UnpacksInto].Id);
        Assert.Equal(2, units.Count(u => u.Kind == ActorKind.Infantry && s.Infantry[u.Type].Id == "lb_scavenger"));
        Assert.Equal(1, units.Count(u => u.Kind == ActorKind.Vehicle && s.Vehicles[u.Type].Id == "lb_gleaner"));
    }

    [Fact]
    public void StartKit_SpawnsAtTheStart()
    {
        SimRules s = LoadReal();
        Simulation sim = NewSim(s, 1);

        EntityId[] placed = SkirmishStart.Apply(sim);

        Assert.False(placed[0].IsNone);
        int rigs = 0, scavengers = 0, gleaners = 0;
        foreach (int i in sim.Units.Slots.AliveIndexes())
        {
            if (sim.Units.Player[i] != 0) continue;
            string id = s.Vehicles[sim.Units.TypeIndex[i]].Id;
            if (id == "lb_mother_rig_packed") rigs++;
            if (id == "lb_gleaner") gleaners++;
        }
        foreach (int i in sim.Infantry.Slots.AliveIndexes())
            if (sim.Infantry.Player[i] == 0 && s.Infantry[sim.Infantry.TypeIndex[i]].Id == "lb_scavenger") scavengers++;
        Assert.Equal(1, rigs);
        Assert.Equal(2, scavengers);
        Assert.Equal(1, gleaners);
    }

    [Fact]
    public void LeftBehindSkirmish_Runs2000Ticks()
    {
        SimRules s = LoadReal();
        Simulation sim = NewSim(s, 7);
        SkirmishStart.Apply(sim);

        for (int t = 0; t < 2000; t++) sim.Step(Array.Empty<Command>());

        Assert.Equal(2000, sim.Tick);
    }

    [Fact]
    public void SecondDrift_WarheadHasStormFields()
    {
        SimRules s = LoadReal();
        BuildingType b = s.Buildings.Single(x => x.Id == "lb_second_drift");
        WarheadType w = s.Warheads.Single(x => x.Id == b.Superweapon + "_blast");

        Assert.True(w.StormRadius > 0);
        Assert.True(w.StormTicks > 0);
        Assert.True(w.StormContaminateStep > 0);
        Assert.Equal(64, w.StormContaminateCap);
        Assert.InRange(w.StormSpeedPermille, 1, 999);
        Assert.True(w.BlackoutTicks > 0);
        Assert.True(b.SuperweaponChargeTicks > 0);
    }

    [Fact]
    public void RigsPack_AndThePackedRigUnpacks()
    {
        SimRules s = LoadReal();

        foreach (string id in new[] { "lb_mother_rig", "lb_refinery_rig", "lb_dynamo_rig", "lb_muster_rig", "lb_garage_rig", "lb_lookout" })
        {
            BuildingType b = s.Buildings.Single(x => x.Id == id);
            Assert.True(b.PacksInto >= 0, id);
            Assert.True(b.PackTicks > 0, id);
        }
        foreach (string id in new[] { "lb_yard", "lb_bench", "lb_rotor_pad", "lb_spike_nest", "lb_harpoon_post", "lb_flak_net", "lb_scrap_wall", "lb_second_drift" })
            Assert.Equal(-1, s.Buildings.Single(x => x.Id == id).PacksInto);
        VehicleType rig = Veh(s, "lb_mother_rig_packed");
        Assert.True(rig.UnpacksInto >= 0);
        Assert.True(rig.PackTicks > 0);
    }

    [Fact]
    public void SalvageRaiderAndAuraFlags_SitOnTheRightUnits()
    {
        SimRules s = LoadReal();

        Assert.True(Inf(s, "lb_scavenger").SalvageRate > 0);
        Assert.True(Inf(s, "lb_tinker").SalvageRate > 0);
        Assert.True(Veh(s, "lb_hauler").SalvageRate > 0);
        Assert.True(Inf(s, "lb_hook_runner").Raider);
        Assert.True(Veh(s, "lb_buggy").Raider);
        Assert.False(Inf(s, "lb_hook_runner").RaiderOneUse);
        Assert.True(Inf(s, "lb_tinker").AuraRepairPermille > 0);
        Assert.True(Veh(s, "lb_tow").TowBonusPermille > 0);
        Assert.Equal(6, Veh(s, "lb_tow").Passengers);
        Assert.True(Veh(s, "lb_dust_sail").DropsBlackoutTicks > 0);
        Assert.True(Inf(s, "lb_veilwalker").AshStealthThreshold > 0);
        Assert.True(Inf(s, "lb_veilwalker").AshStealthDelayTicks > 0);
        Assert.True(Inf(s, "lb_veilwalker").Stealth);
        Assert.False(Inf(s, "lb_slinger").Raider);
        // nobody else has the flags
        Assert.All(s.Infantry.Where(i => i.Faction != Faction.LeftBehind), i => { Assert.Equal(0, i.SalvageRate); Assert.False(i.Raider); });
        Assert.All(s.Vehicles.Where(v => v.Faction != Faction.LeftBehind), v => { Assert.Equal(0, v.SalvageRate); Assert.False(v.Raider); });
    }

    [Fact]
    public void Upgrades_CarryTheirEffectHooks()
    {
        SimRules s = LoadReal();
        UpgradeType U(string id) => s.Upgrades.Single(u => u.Id == id);

        Assert.True(U("lb_up_quick_pack").PackTicksPermille > 0);
        Assert.True(U("lb_up_grapple").PlunderPermille > 0);
        Assert.True(U("lb_up_salvage_rights").SalvagePermille > 0);
        Assert.True(U("lb_up_patchwork").RepairsVehiclesAtRange);
        Assert.NotEmpty(U("lb_up_scrap_plate").Effects);
        Assert.NotEmpty(U("lb_up_ash_wraps").Effects);
    }

    [Fact]
    public void Defences_HaveWeapons()
    {
        SimRules s = LoadReal();

        foreach (string id in new[] { "lb_spike_nest", "lb_harpoon_post", "lb_flak_net" })
            Assert.True(s.Buildings.Single(b => b.Id == id).PrimaryWeapon >= 0, id);
    }

    private static Simulation NewSim(SimRules rules, ulong seed)
    {
        var map = new MapData("lb", "rust_belt", 40, 40, new ushort[1600], new LandType[1600], new byte[1600], new OverlayKind[1600], new byte[1600],
            new[] { new CellCoord(5, 5), new CellCoord(34, 34) });
        return new Simulation(new SimSetup(seed, map, 2, rules, 0x6806UL) { Factions = new[] { Faction.LeftBehind, Faction.Authority } });
    }
}
