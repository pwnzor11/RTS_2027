// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Rules;

/// <summary>
/// Checks the shipped Authority M2 roster (D-058) in data/rules: the exact 13 ids, and a prerequisite chain that is
/// acyclic and reaches every structure from auth_conyard. Reads the real data, not the fixtures.
/// </summary>
public sealed class AuthorityRosterTests
{
    private const string Conyard = "auth_conyard";

    private static readonly string[] ExpectedIds =
    [
        "auth_conyard", "auth_power", "auth_ration_hall", "auth_refinery", "auth_barracks", "auth_factory", "auth_silo", "auth_wall", "auth_turret",
        "auth_scraper", "auth_rifleman", "auth_rocketeer", "auth_jeep", "auth_tank",
        "auth_seismograph", "auth_searchlight", "auth_gate", "auth_engineering", "auth_decon_truck",   // M3 counters (M3-07)
        // M4-07 (design 08 §4): engineer, marksman, carrier, seismic mortar, foam truck, interceptor, bomber, howitzer, bulwark;
        // signals HQ (T3 gate), blockhouse, flak, airfield, atomic battery.
        "auth_engineer", "auth_marksman", "auth_carrier", "auth_seismic_mortar", "auth_foam_truck", "auth_interceptor", "auth_bomber",
        "auth_howitzer", "auth_bulwark", "auth_signals_hq", "auth_blockhouse", "auth_flak", "auth_airfield", "auth_atomic_battery",
    ];

    private static SimRules LoadReal()
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        return RulesLoader.Load(dir!).Sim;
    }

    [Fact]
    public void RealData_HasExactlyTheAuthorityIds()
    {
        SimRules sim = LoadReal();

        string[] actual = sim.Vehicles.Select(v => v.Id)
            .Concat(sim.Infantry.Select(i => i.Id))
            .Concat(sim.Buildings.Select(b => b.Id))
            .Where(id => id.StartsWith("auth_", StringComparison.Ordinal))   // M3: the Directorate roster lives alongside
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        string[] expected = ExpectedIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PrerequisiteChain_IsAcyclic()
    {
        SimRules sim = LoadReal();
        Dictionary<string, BuildingType> byName = ProvidersByName(sim);

        var state = new Dictionary<string, int>(StringComparer.Ordinal); // 1 = visiting, 2 = done
        foreach (BuildingType b in sim.Buildings)
        {
            Assert.False(HasCycle(b.Id, byName, state), $"prerequisite cycle through {b.Id}");
        }
    }

    [Fact]
    public void EveryStructure_IsReachableFromTheConyard()
    {
        SimRules sim = LoadReal();
        var satisfied = new HashSet<string>(StringComparer.Ordinal) { Conyard };
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (BuildingType b in sim.Buildings)
            {
                if (satisfied.Contains(b.Id) || !b.Prerequisites.All(satisfied.Contains)) continue;
                satisfied.Add(b.Id);
                foreach (string p in b.Provides) satisfied.Add(p);
                changed = true;
            }
        }

        string[] unreachable = sim.Buildings.Select(b => b.Id).Where(id => !satisfied.Contains(id)).ToArray();
        Assert.Empty(unreachable);
    }

    [Fact]
    public void Conyard_ProvidesItsOwnRootName_AndProducesNothing()
    {
        BuildingType conyard = LoadReal().Buildings.Single(b => b.Id == Conyard);

        Assert.Contains(Conyard, conyard.Provides);
        Assert.Null(conyard.Produces);
        Assert.Empty(conyard.Prerequisites);
    }

    /// <summary>Maps each id and each provides name to the structure that satisfies it.</summary>
    private static Dictionary<string, BuildingType> ProvidersByName(SimRules sim)
    {
        var map = new Dictionary<string, BuildingType>(StringComparer.Ordinal);
        foreach (BuildingType b in sim.Buildings)
        {
            map[b.Id] = b;
            foreach (string p in b.Provides) map[p] = b;
        }
        return map;
    }

    private static bool HasCycle(string id, Dictionary<string, BuildingType> byName, Dictionary<string, int> state)
    {
        if (!byName.TryGetValue(id, out BuildingType? b)) return false;
        if (state.TryGetValue(b.Id, out int s)) return s == 1;
        state[b.Id] = 1;
        foreach (string prereq in b.Prerequisites)
        {
            if (HasCycle(prereq, byName, state)) return true;
        }
        state[b.Id] = 2;
        return false;
    }
}
