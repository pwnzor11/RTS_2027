// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests.S19;

/// <summary>M13-08b (D-205; FAD-04, LE-09, MIS-16a/b): the campaign allow-lists grant the core units that used to be missing, from the
/// named mission on, and every id is a real type whose prerequisites are in the same list.</summary>
public sealed class CampaignAllowListTests
{
    private static HashSet<string> Allow(string mission)
    {
        MissionDef d = MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, mission));
        var set = new HashSet<string>(d.StartsWith);
        foreach (string t in d.Players.Single(p => p.Role == "human").Tech ?? Array.Empty<string>()) set.Add(t);
        return set;
    }

    public static IEnumerable<object[]> Grants()
    {
        yield return new object[] { "auth_01", new[] { "auth_factory" } };
        foreach (string m in new[] { "auth_02", "auth_07", "auth_03", "auth_04" })
            yield return new object[] { m, new[] { "auth_rocketeer", "auth_scraper", "auth_silo", "auth_seismograph" } };
        foreach (string m in new[] { "dir_01", "dir_02", "dir_03", "dir_04" })
            yield return new object[] { m, new[] { "dir_bore_head" } };
        foreach (string m in new[] { "dir_02", "dir_03", "dir_04" })
            yield return new object[] { m, new[] { "dir_bunk_gallery" } };
        foreach (string m in new[] { "dir_02", "dir_03", "dir_04" })
            yield return new object[] { m, new[] { "dir_cistern" } };            // A43 CAMPAIGN-02
        foreach (string m in new[] { "exp_06", "exp_02", "exp_03", "exp_04" })
            yield return new object[] { m, new[] { "exp_tank_farm" } };          // A43 CAMPAIGN-02
        yield return new object[] { "auth_04", new[] { "auth_airfield", "auth_interceptor", "auth_bomber" } };   // A43 CAMPAIGN-03
        foreach (string m in new[] { "lb_01", "lb_02", "lb_03", "lb_04" })
            yield return new object[] { m, new[] { "lb_still_rig" } };
        foreach (string m in new[] { "lb_02", "lb_03", "lb_04" })
            yield return new object[] { m, new[] { "lb_hauler" } };
    }

    [Theory]
    [MemberData(nameof(Grants))]
    public void Mission_allow_list_contains_the_granted_ids(string mission, string[] ids)
    {
        HashSet<string> allow = Allow(mission);
        SimRules rules = RulesLoader.Load(Path.Combine(TestData.DataDir, "rules")).Sim;
        foreach (string id in ids)
        {
            Assert.Contains(id, allow);
            IReadOnlyList<string>? prereq = rules.Buildings.FirstOrDefault(b => b.Id == id)?.Prerequisites
                ?? rules.Vehicles.FirstOrDefault(u => u.Id == id)?.Prerequisites
                ?? rules.Infantry.FirstOrDefault(u => u.Id == id)?.Prerequisites;
            Assert.NotNull(prereq);   // a real rules type
            foreach (string p in prereq!.Where(q => rules.Buildings.Any(b => b.Id == q))) Assert.Contains(p, allow);   // tier ids ("auth_tier2") are provided, not allowed
        }
    }

    public static IEnumerable<object[]> AllMissions() =>
        new[] { "auth_01", "auth_02", "auth_07", "auth_03", "auth_04", "dir_01", "dir_02", "dir_03", "dir_04", "exp_01", "exp_05", "exp_06", "exp_02", "exp_03", "exp_04",
                "lb_01", "lb_02", "lb_03", "lb_04" }.Select(m => new object[] { m });

    /// <summary>A43 CAMPAIGN-03: an allowed building that the rules use as a unit prerequisite must have at least one allowed unit to build
    /// from it (no dead Airfield), and every allowed unit's prerequisites are allowed too.</summary>
    // Buildings that gate an item without producing one in this mission (designed that way, outside A43 CAMPAIGN-03): the Anchorage only gates the
    // Supply Tender, the Isotope Works gates units that unlock a mission later, the Bench only unpacks the Mother Rig.
    private static readonly HashSet<string> NotAProducer = new() { "exp_anchorage", "dir_isotope_works", "lb_bench" };

    [Theory]
    [MemberData(nameof(AllMissions))]
    public void Every_allowed_production_building_can_produce_an_allowed_item(string mission)
    {
        HashSet<string> allow = Allow(mission);
        SimRules rules = RulesLoader.Load(Path.Combine(TestData.DataDir, "rules")).Sim;
        var units = rules.Vehicles.Select(v => (v.Id, v.Prerequisites)).Concat(rules.Infantry.Select(u => (u.Id, u.Prerequisites))).ToList();
        foreach (string b in allow.Where(a => !NotAProducer.Contains(a) && rules.Buildings.Any(x => x.Id == a)))
        {
            var users = units.Where(u => u.Prerequisites.Contains(b)).ToList();
            if (users.Count == 0) continue;   // not a production building
            Assert.True(users.Any(u => allow.Contains(u.Id)), mission + ": " + b + " is allowed but none of its units (" + string.Join(", ", users.Select(u => u.Id)) + ") is");
        }
    }

    [Fact]
    public void Atomic_artillery_archive_entry_unlocks_with_the_last_authority_mission()
    {
        string text = File.ReadAllText(Path.Combine(TestData.DataDir, "archive", "technology.toml"));
        Assert.Contains("unlock = \"campaign:auth_04\"", text);
        Assert.DoesNotContain("campaign:auth_05", text);
    }
}
