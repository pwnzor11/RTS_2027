// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests.S48;

/// <summary>Q-94 / D-341 Q5 (b): a restricted mission player researches an upgrade only if its id is in the granted set (the same lists as units);
/// skirmish and AI players are unchanged, and no built mission lost an upgrade it allowed before the gate.</summary>
public sealed class S48UpgradeGateTests
{
    private const string Flak = "auth_up_flak_vests";

    private static (Simulation Sim, MissionHost Host, int Upgrade) Setup(string? tech, string lua = "function OnInit() end")
    {
        var (sim, host) = TestData.Tiny(lua, tech);
        IReadOnlyList<BuildingType> b = sim.Setup.Rules.Buildings;
        int barracks = SimRules.IndexOf(b, "auth_barracks", x => x.Id);   // Researches = true
        Assert.True(sim.PlaceBuilding(0, barracks, new CellCoord(20, 20)) != EntityId.None);
        Assert.True(sim.PlaceBuilding(1, barracks, new CellCoord(40, 40)) != EntityId.None);
        int up = sim.Setup.Rules.UpgradeIndex(Flak);
        Assert.True(up >= 0);
        return (sim, host, up);
    }

    private static void Research(Simulation sim, int up) =>
        sim.Step(new[] { new Command(sim.Tick, 0, 1, CommandKind.Build, default, default, default, (int)ProductionCategory.Upgrade, up) });

    [Fact]
    public void Mission_player_without_the_id_cannot_research_it()
    {
        var (sim, host, up) = Setup("auth_power,auth_barracks");
        Assert.False(sim.CanResearch(0, up));
        Assert.DoesNotContain(up, sim.HudFor(0).BuildableUpgrades);
        long cash = sim.Players.Cash[0];
        Research(sim, up);
        TestData.Run(sim, 30);
        Assert.Equal(0UL, sim.Players.Upgrades[0]);
        Assert.False(sim.HasUpgrade(0, up));
        Assert.True(sim.Players.Cash[0] >= cash, "a refused command spends nothing");
        Assert.Empty(host.Errors);
    }

    [Fact]
    public void Mission_player_with_the_id_can_research_it()
    {
        var (sim, host, up) = Setup("auth_power,auth_barracks," + Flak);
        Assert.True(sim.CanResearch(0, up));
        Assert.Contains(up, sim.HudFor(0).BuildableUpgrades);
        Research(sim, up);
        Assert.False(sim.CanResearch(0, up), "queued now");
        for (int i = 0; i < 6000 && !sim.HasUpgrade(0, up); i++) sim.Step(TestData.NoCommands);
        Assert.True(sim.HasUpgrade(0, up));
        Assert.Empty(host.Errors);
    }

    [Fact]
    public void Skirmish_and_ai_players_are_unchanged()
    {
        var (sim, _, up) = Setup("auth_power");
        Assert.True(sim.CanResearch(1, up), "an unrestricted mission player (the AI) keeps the normal rules");
        sim.UnlockFilter = null;   // skirmish: no filter at all
        Assert.True(sim.CanResearch(0, up));
        Assert.Contains(up, sim.HudFor(0).BuildableUpgrades);
    }

    [Fact]
    public void Script_can_grant_an_upgrade_id_and_an_unknown_id_is_an_error()
    {
        var (sim, host, up) = Setup("auth_power", "function OnInit() Tech.Grant(0, 'auth_up_flak_vests') end");
        TestData.Run(sim, 3);
        Assert.Empty(host.Errors);
        Assert.True(sim.CanResearch(0, up));

        var (sim2, host2, _) = Setup("auth_power", "function OnInit() Tech.Grant(0, 'auth_up_no_such') end");
        TestData.Run(sim2, 3);
        Assert.Contains(host2.Errors, e => e.Contains("unknown type id 'auth_up_no_such'"));
    }

    private static string FactionOf(MissionDef d)
    {
        if (d.Faction.Length > 0) return d.Faction;
        MissionPlayer human = d.Players.Single(p => p.Role == "human");
        if (human.Faction.Length > 0) return human.Faction;
        return human.Tech!.First().Split('_')[0] switch { "auth" => "authority", "dir" => "directorate", "lb" => "left_behind", _ => "expedition" };
    }

    public static IEnumerable<object[]> Missions() =>
        Directory.GetDirectories(Path.Combine(TestData.DataDir, "missions")).Select(d => new object[] { Path.GetFileName(d) });

    /// <summary>Before the gate every upgrade of the human's faction was researchable wherever the human could own a researching building (listed
    /// in the allow-list, pre-placed, or named in the script). Computed from the data; each such mission must still list all of them in both the
    /// player tech and starts_with, and every upgrade id it lists must be a real upgrade of that faction.</summary>
    [Theory]
    [MemberData(nameof(Missions))]
    public void Built_missions_still_allow_every_upgrade_they_allowed_before(string mission)
    {
        string folder = MissionLoader.FolderOf(TestData.DataDir, mission);
        MissionDef d = MissionLoader.Load(folder);
        SimRules rules = RulesLoader.Load(Path.Combine(TestData.DataDir, "rules")).Sim;
        string faction = FactionOf(d);
        string[] mine = rules.Upgrades.Where(u => u.Faction == FactionIndex(rules, faction)).Select(u => u.Id).ToArray();
        HashSet<string> upgradeIds = rules.Upgrades.Select(u => u.Id).ToHashSet();
        MissionPlayer human = d.Players.Single(p => p.Role == "human");
        var tech = new HashSet<string>(human.Tech ?? Array.Empty<string>());
        var starts = new HashSet<string>(d.StartsWith);

        foreach (string id in tech.Concat(starts).Concat(d.UnlocksOnWin).Where(upgradeIds.Contains))
            Assert.Contains(id, mine);   // no stray upgrade of another faction

        string text = string.Concat(Directory.GetFiles(folder).Where(f => f.EndsWith(".toml") || f.EndsWith(".lua")).Select(File.ReadAllText));
        bool couldResearch = rules.Buildings.Any(b => b.Researches && b.Id.StartsWith(PrefixOf(faction), StringComparison.Ordinal)
                                                      && (tech.Contains(b.Id) || System.Text.RegularExpressions.Regex.IsMatch(text, "\\b" + b.Id + "\\b")));
        if (!couldResearch) return;
        foreach (string id in mine)
        {
            Assert.Contains(id, tech);
            Assert.Contains(id, starts);
        }
    }

    private static string PrefixOf(string faction) => faction switch { "authority" => "auth_", "directorate" => "dir_", "left_behind" => "lb_", _ => "exp_" };

    private static Faction FactionIndex(SimRules rules, string faction) =>
        rules.Upgrades.First(u => u.Id.StartsWith(PrefixOf(faction), StringComparison.Ordinal)).Faction;
}
