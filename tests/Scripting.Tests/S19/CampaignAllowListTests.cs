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
        foreach (string m in new[] { "auth_02", "auth_03", "auth_04" })
            yield return new object[] { m, new[] { "auth_rocketeer", "auth_scraper", "auth_silo", "auth_seismograph" } };
        foreach (string m in new[] { "dir_01", "dir_02", "dir_03", "dir_04" })
            yield return new object[] { m, new[] { "dir_bore_head" } };
        foreach (string m in new[] { "dir_02", "dir_03", "dir_04" })
            yield return new object[] { m, new[] { "dir_bunk_gallery" } };
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
            foreach (string p in prereq!) Assert.Contains(p, allow);
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
