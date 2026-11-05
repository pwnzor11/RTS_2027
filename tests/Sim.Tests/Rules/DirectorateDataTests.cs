// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Rules;

/// <summary>
/// Checks the shipped M3 Directorate roster (design 05 §5, D-078) and the Authority counters (D-079) in data/rules: the
/// data loads, every §5 id exists with the right faction, every referenced weapon, warhead, prerequisite and
/// deploys_into target resolves, the radiation and Blackout warheads are set, and the Directorate start kit resolves.
/// Reads the real data, not the fixtures.
/// </summary>
public sealed class DirectorateDataTests
{
    private static readonly string[] DirectorateInfantry =
        ["dir_ashthrower", "dir_commissar", "dir_conscript", "dir_informant", "dir_sapper"];

    private static readonly string[] DirectorateVehicles =
        ["dir_agitprop", "dir_bore", "dir_dirigible", "dir_landtrain", "dir_mole", "dir_reactor_crawler", "dir_rocket_truck", "dir_scraper"];

    private static readonly string[] DirectorateStructures =
    [
        "dir_assembly_bore", "dir_bore_head", "dir_bunk_gallery", "dir_bunker", "dir_cistern", "dir_hatch", "dir_isotope_works", "dir_mast",
        "dir_mooring", "dir_muster_hatch", "dir_pillbox", "dir_rad_mortar", "dir_reactor_vent", "dir_rocket_nest", "dir_shaft",
        "dir_smelter_vent", "dir_sunrise",
    ];

    private static readonly string[] AuthorityCounterStructures = ["auth_engineering", "auth_gate", "auth_searchlight", "auth_seismograph"];

    private static readonly string[] AuthorityCounterVehicles = ["auth_decon_truck"];

    /// <summary>Primary weapon of each type that has one (types with no weapon are absent).</summary>
    private static readonly Dictionary<string, string> PrimaryWeaponOf = new(StringComparer.Ordinal)
    {
        ["dir_conscript"] = "dir_rifle",
        ["dir_ashthrower"] = "dir_ash_spray",
        ["dir_sapper"] = "dir_collapse_charge",
        ["dir_commissar"] = "dir_pistol",
        ["dir_reactor_crawler"] = "dir_rad_cannon",
        ["dir_rocket_truck"] = "dir_rocket_salvo",
        ["dir_dirigible"] = "dir_blackout_drop",
        ["dir_landtrain"] = "dir_rail_gun",
        ["dir_pillbox"] = "dir_pillbox_gun",
        ["dir_rocket_nest"] = "dir_rocket_nest_gun",
        ["dir_rad_mortar"] = "dir_rad_mortar_shell",
        ["auth_decon_truck"] = "auth_decon_spray",
    };

    /// <summary>Warhead of each weapon the Directorate and the decon truck use.</summary>
    private static readonly Dictionary<string, string> WarheadOf = new(StringComparer.Ordinal)
    {
        ["dir_rifle"] = "dir_small_arms",
        ["dir_ash_spray"] = "dir_ash",
        ["dir_collapse_charge"] = "dir_demolition",
        ["dir_pistol"] = "dir_small_arms",
        ["dir_rad_cannon"] = "dir_rad_shell",
        ["dir_meltdown"] = "dir_meltdown_blast",
        ["dir_rocket_salvo"] = "dir_rocket",
        ["dir_blackout_drop"] = "dir_blackout",
        ["dir_rail_gun"] = "dir_slug",
        ["dir_rocket_nest_gun"] = "dir_rocket",
        ["dir_pillbox_gun"] = "dir_small_arms",
        ["dir_rad_mortar_shell"] = "dir_rad_shell",
        ["auth_decon_spray"] = "auth_decon",
    };

    private static SimRules LoadReal()
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        return RulesLoader.Load(dir!).Sim;   // throws RulesException listing every problem found
    }

    /// <summary>Every new M3-07 type id (Directorate roster plus the Authority counters).</summary>
    private static IEnumerable<string> NewIds() =>
        DirectorateInfantry.Concat(DirectorateVehicles).Concat(DirectorateStructures)
            .Concat(AuthorityCounterStructures).Concat(AuthorityCounterVehicles);

    /// <summary>Primary weapon index of a type (unit or building) by id, or -1 when it has none.</summary>
    private static int PrimaryOf(SimRules s, string id)
    {
        if (s.Infantry.FirstOrDefault(i => i.Id == id) is { } inf) return inf.PrimaryWeapon;
        if (s.Vehicles.FirstOrDefault(v => v.Id == id) is { } veh) return veh.PrimaryWeapon;
        return s.Buildings.Single(b => b.Id == id).PrimaryWeapon;
    }

    /// <summary>Prerequisite list of a new type by id.</summary>
    private static IReadOnlyList<string> PrerequisitesOf(SimRules s, string id)
    {
        if (s.Infantry.FirstOrDefault(i => i.Id == id) is { } inf) return inf.Prerequisites;
        if (s.Vehicles.FirstOrDefault(v => v.Id == id) is { } veh) return veh.Prerequisites;
        return s.Buildings.Single(b => b.Id == id).Prerequisites;
    }

    [Fact]
    public void RealData_LoadsWithoutProblems()
    {
        SimRules s = LoadReal();

        Assert.NotEmpty(s.Buildings);
        Assert.NotEmpty(s.Upgrades);
    }

    [Fact]
    public void EveryDirectorateId_ExistsWithDirectorateFaction()
    {
        SimRules s = LoadReal();

        foreach (string id in DirectorateInfantry)
            Assert.Equal(Faction.Directorate, s.Infantry.Single(i => i.Id == id).Faction);
        foreach (string id in DirectorateVehicles)
            Assert.Equal(Faction.Directorate, s.Vehicles.Single(v => v.Id == id).Faction);
        foreach (string id in DirectorateStructures)
            Assert.Equal(Faction.Directorate, s.Buildings.Single(b => b.Id == id).Faction);
    }

    [Fact]
    public void DirectorateRoster_IsExactlyTheSectionFiveIds()
    {
        SimRules s = LoadReal();

        // The M3 roster (design 05 §5) plus the M4 additions (design 08 §4): assessor, volunteer; ash_sower, delver, great_dirigible;
        // redoubt (T1 garrison defence) and committee_hall (T3 gate).
        string[] infantry = DirectorateInfantry.Concat(["dir_assessor", "dir_volunteer"]).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        string[] vehicles = DirectorateVehicles.Concat(["dir_ash_sower", "dir_delver", "dir_great_dirigible"]).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        string[] structures = DirectorateStructures.Concat(["dir_committee_hall", "dir_redoubt"]).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(7, infantry.Length);
        Assert.Equal(11, vehicles.Length);
        Assert.Equal(19, structures.Length);   // SUP-2: + dir_bunk_gallery
        Assert.Equal(infantry, s.Infantry.Select(i => i.Id).Where(IsDir).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.Equal(vehicles, s.Vehicles.Select(v => v.Id).Where(IsDir).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.Equal(structures, s.Buildings.Select(b => b.Id).Where(IsDir).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static bool IsDir(string id) => id.StartsWith("dir_", StringComparison.Ordinal);

    [Fact]
    public void EveryAuthorityCounter_ExistsWithAuthorityFaction()
    {
        SimRules s = LoadReal();

        foreach (string id in AuthorityCounterStructures)
            Assert.Equal(Faction.Authority, s.Buildings.Single(b => b.Id == id).Faction);
        foreach (string id in AuthorityCounterVehicles)
            Assert.Equal(Faction.Authority, s.Vehicles.Single(v => v.Id == id).Faction);
    }

    [Fact]
    public void EveryPrerequisite_ResolvesToABuildingIdOrProvidesName()
    {
        SimRules s = LoadReal();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (BuildingType b in s.Buildings)
        {
            names.Add(b.Id);
            foreach (string p in b.Provides) names.Add(p);
        }

        foreach (string id in NewIds())
        {
            foreach (string prereq in PrerequisitesOf(s, id))
                Assert.True(names.Contains(prereq), $"{id}: prerequisite '{prereq}' does not resolve");
        }
    }

    [Fact]
    public void EveryPrimaryWeapon_And_Warhead_Resolves()
    {
        SimRules s = LoadReal();

        foreach (string id in NewIds())
        {
            int w = PrimaryOf(s, id);
            if (!PrimaryWeaponOf.TryGetValue(id, out string? expected))
            {
                Assert.Equal(-1, w);
                continue;
            }
            Assert.InRange(w, 0, s.Weapons.Count - 1);
            Assert.Equal(expected, s.Weapons[w].Id);
        }

        foreach ((string weapon, string warhead) in WarheadOf)
        {
            WeaponType wt = s.Weapons.Single(x => x.Id == weapon);
            Assert.Equal(warhead, s.Warheads[wt.Warhead].Id);
        }
    }

    [Fact]
    public void RadiationWarheads_Contaminate_And_DecontDecontaminates()
    {
        SimRules s = LoadReal();

        Assert.True(s.Warheads.Single(w => w.Id == "dir_ash").Contaminate > 0);
        Assert.True(s.Warheads.Single(w => w.Id == "dir_rad_shell").Contaminate > 0);
        Assert.True(s.Warheads.Single(w => w.Id == "dir_meltdown_blast").Contaminate > 0);
        Assert.True(s.Warheads.Single(w => w.Id == "auth_decon").Decontaminate > 0);
    }

    [Fact]
    public void BlackoutWarhead_SetsBlackoutTicks_OnTheDirigibleDrop()
    {
        SimRules s = LoadReal();

        WeaponType drop = s.Weapons.Single(w => w.Id == "dir_blackout_drop");
        Assert.True(s.Warheads[drop.Warhead].BlackoutTicks > 0);
        Assert.Equal("dir_blackout", s.Warheads[drop.Warhead].Id);
    }

    [Fact]
    public void ReactorCrawler_IsLargeIrradiatedImmune_AndMeltsDownOnDeath()
    {
        SimRules s = LoadReal();

        VehicleType crawler = s.Vehicles.Single(v => v.Id == "dir_reactor_crawler");
        Assert.Equal(Locomotion.Tracked, crawler.Locomotion);
        Assert.Equal(TunnelSize.L, crawler.TunnelSize);
        Assert.True(crawler.Irradiated);
        Assert.Equal(1000, crawler.RadResistPermille);
        Assert.Equal(SimRules.IndexOf(s.Weapons, "dir_meltdown", w => w.Id), crawler.DeathWeapon);
    }

    [Fact]
    public void Burrowers_UseBurrowLocomotion_AndMoleCarriesFive()
    {
        SimRules s = LoadReal();

        foreach (string id in new[] { "dir_scraper", "dir_bore", "dir_mole" })
        {
            VehicleType v = s.Vehicles.Single(x => x.Id == id);
            Assert.Equal(Locomotion.Burrow, v.Locomotion);
            Assert.Equal(TunnelSize.M, v.TunnelSize);
        }
        Assert.Equal(5, s.Vehicles.Single(v => v.Id == "dir_mole").Passengers);
    }

    [Fact]
    public void Deploys_BoreIntoHatch_AndLandTrainIntoCommandShaft()
    {
        SimRules s = LoadReal();

        Assert.Equal("dir_hatch", s.Buildings[s.Vehicles.Single(v => v.Id == "dir_bore").DeploysInto].Id);
        VehicleType train = s.Vehicles.Single(v => v.Id == "dir_landtrain");
        Assert.Equal("dir_shaft", s.Buildings[train.DeploysInto].Id);
        Assert.Equal(TunnelSize.L, train.TunnelSize);
    }

    [Fact]
    public void Entrances_HatchesAreSmallBoreClassAreMedium_WithThroughputTicks()
    {
        SimRules s = LoadReal();
        BuildingType Get(string id) => s.Buildings.Single(b => b.Id == id);

        Assert.Equal(TunnelSize.S, Get("dir_hatch").Entrance);
        Assert.Equal(35, Get("dir_hatch").TicksPerPoint);           // M3-14: 20 -> 35 (tunnel time saved 30-50 %)
        Assert.Equal(TunnelSize.S, Get("dir_muster_hatch").Entrance);
        Assert.Equal(35, Get("dir_muster_hatch").TicksPerPoint);   // M3-14: 20 -> 35

        foreach (string id in new[] { "dir_bore_head", "dir_shaft", "dir_assembly_bore", "dir_smelter_vent" })
        {
            Assert.Equal(TunnelSize.M, Get(id).Entrance);
            Assert.Equal(20, Get(id).TicksPerPoint);                // M3-14: 13 -> 20
        }
    }

    [Fact]
    public void SmelterVent_IsTheHarvesterDock_WithAFreeScraper()
    {
        SimRules s = LoadReal();
        BuildingType vent = s.Buildings.Single(b => b.Id == "dir_smelter_vent");

        Assert.True(vent.Refinery);
        Assert.Equal(new CellCoord(1, 2), vent.Dock);   // 3x2 footprint: the cell just south of the ramp mouth
        Assert.Equal("dir_scraper", s.Vehicles[vent.FreeUnit].Id);
    }

    [Fact]
    public void Producers_ResearchersAndSuperweapon_AreWired()
    {
        SimRules s = LoadReal();
        BuildingType Get(string id) => s.Buildings.Single(b => b.Id == id);

        Assert.Equal(ProductionCategory.Infantry, Get("dir_muster_hatch").Produces);
        Assert.Equal(ProductionCategory.Vehicle, Get("dir_assembly_bore").Produces);
        Assert.Equal(ProductionCategory.Aircraft, Get("dir_mooring").Produces);
        Assert.True(Get("dir_isotope_works").Researches);
        Assert.False(string.IsNullOrEmpty(Get("dir_sunrise").Superweapon));
        Assert.Equal(8, Get("dir_sunrise").SuperweaponEntranceRange);
    }

    [Fact]
    public void AuthorityCounters_HaveTheirDesignShape()
    {
        SimRules s = LoadReal();
        BuildingType Get(string id) => s.Buildings.Single(b => b.Id == id);

        Assert.True(Get("auth_seismograph").SeismicRadius > 0);
        Assert.True(Get("auth_searchlight").SeesThroughBlackout);
        BuildingType gate = Get("auth_gate");
        Assert.True(gate.Gate);
        Assert.True(gate.Wall);
        Assert.Equal((1, 1), (gate.FootprintW, gate.FootprintH));
        Assert.True(Get("auth_engineering").Researches);
        Assert.Equal("auth_decon_spray", s.Weapons[s.Vehicles.Single(v => v.Id == "auth_decon_truck").PrimaryWeapon].Id);
    }

    [Fact]
    public void AuraTargets_Resolve_AndAgitpropAuraAndJamAreSet()
    {
        SimRules s = LoadReal();
        var ids = new HashSet<string>(s.Infantry.Select(i => i.Id).Concat(s.Vehicles.Select(v => v.Id)), StringComparer.Ordinal);

        AuraTargetsAll(s, ids);
        VehicleType agit = s.Vehicles.Single(v => v.Id == "dir_agitprop");
        Assert.Equal(4, agit.AuraRadius);
        Assert.Equal(6, agit.JamRadius);
    }

    private static void AuraTargetsAll(SimRules s, HashSet<string> ids)
    {
        foreach (VehicleType v in s.Vehicles)
            foreach (string t in v.AuraTargets) Assert.Contains(t, ids);
        foreach (InfantryType i in s.Infantry)
            foreach (string t in i.AuraTargets) Assert.Contains(t, ids);
    }

    [Fact]
    public void DirectorateStartKit_Resolves()
    {
        SimRules s = LoadReal();

        (int conyard, IReadOnlyList<(ActorKind Kind, int Type)> units) = s.ResolveStartKit(Faction.Directorate);

        Assert.Equal("dir_shaft", s.Buildings[conyard].Id);
        Assert.Equal(4, units.Count);
        Assert.All(units, u =>
        {
            Assert.Equal(ActorKind.Infantry, u.Kind);
            Assert.Equal("dir_conscript", s.Infantry[u.Type].Id);
        });
    }

    [Fact]
    public void AuthorityStartKit_IsUnchanged()
    {
        SimRules s = LoadReal();

        (int conyard, IReadOnlyList<(ActorKind Kind, int Type)> units) = s.ResolveStartKit(Faction.Authority);

        Assert.Equal("auth_conyard", s.Buildings[conyard].Id);
        Assert.Empty(units);
    }

    [Fact]
    public void Authority_ExistingBalance_IsUnchanged()
    {
        SimRules s = LoadReal();

        Assert.Equal(100, s.Infantry.Single(i => i.Id == "auth_rifleman").Cost);
        Assert.Equal(800, s.Vehicles.Single(v => v.Id == "auth_tank").Cost);
        Assert.Equal(2000, s.Buildings.Single(b => b.Id == "auth_refinery").Cost);
        Assert.Equal(0, s.Buildings.Single(b => b.Id == "auth_conyard").Cost);
        UpgradeType grouting = s.Upgrades.Single(u => u.Id == "auth_grouting");
        Assert.Equal(Faction.Authority, grouting.Faction);
        Assert.Equal(1500, grouting.Cost);
        Assert.Equal("grouting", grouting.Effect);
    }
}
