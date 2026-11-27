// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.TechTree;
using Rts.Game.Ui;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S47;

/// <summary>U-12: the tech-tree model of each faction, its statuses and its text.</summary>
public sealed class S47TechTreeTests
{
    private static readonly SimRules Rules = RulesLoader.Load(
        RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found")).Sim;

    public static TheoryData<Faction> Factions => new() { Faction.Authority, Faction.Directorate, Faction.LeftBehind, Faction.Expedition };

    private static IEnumerable<string> BuildableIds(Faction f)
    {
        foreach (BuildingType b in Rules.Buildings) if (b.Faction == f && b.Buildable) yield return b.Id;
        foreach (InfantryType u in Rules.Infantry) if (u.Faction == f) yield return u.Id;
        foreach (VehicleType v in Rules.Vehicles) if (v.Faction == f && v.Buildable) yield return v.Id;
        foreach (UpgradeType u in Rules.Upgrades) if (u.Faction == f) yield return u.Id;
    }

    [Theory]
    [MemberData(nameof(Factions))]
    public void Every_buildable_id_appears_exactly_once(Faction f)
    {
        TechTreeModel tree = TechTreeModel.Build(Rules, f);
        string[] ids = tree.Nodes.Select(n => n.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        foreach (string id in BuildableIds(f)) Assert.Single(ids, x => x == id);
        Assert.NotEmpty(ids);
    }

    [Theory]
    [MemberData(nameof(Factions))]
    public void No_edge_points_at_a_missing_id_and_unlocks_mirror_needs(Faction f)
    {
        TechTreeModel tree = TechTreeModel.Build(Rules, f);
        foreach (TechNode n in tree.Nodes)
        {
            foreach (string need in n.Needs)
            {
                TechNode? from = tree.Find(need);
                Assert.True(from is not null, $"{n.Id} needs missing {need}");
                Assert.Contains(n.Id, from!.Unlocks);
            }
            foreach (string u in n.Unlocks) Assert.Contains(n.Id, tree.Find(u)!.Needs);
        }
        Assert.Empty(tree.Unresolved);
    }

    [Theory]
    [MemberData(nameof(Factions))]
    public void Columns_respect_prerequisites(Faction f)
    {
        TechTreeModel tree = TechTreeModel.Build(Rules, f);
        foreach (TechNode n in tree.Nodes)
        {
            if (n.Needs.Count == 0) Assert.Equal(0, n.Column);
            foreach (string need in n.Needs) Assert.True(tree.Find(need)!.Column < n.Column, $"{need} must sit left of {n.Id}");
            Assert.InRange(n.Column, 0, tree.Columns - 1);
        }
    }

    [Fact]
    public void Status_follows_the_hud_and_the_mission_filter()
    {
        TechTreeModel tree = TechTreeModel.Build(Rules, Faction.Authority);
        TechNode structure = tree.Nodes.First(n => n.Category == ProductionCategory.Structure && n.Needs.Count == 0);
        TechNode gated = tree.Nodes.First(n => n.Category == ProductionCategory.Infantry);
        var options = new List<BuildOption> { new(structure.Category, structure.TypeIndex, true, QueueBlockReason.None, structure.Cost) };
        var hud = new PlayerHud(0, 0, 0, 0, 0, Array.Empty<QueueView>(), Array.Empty<(ProductionCategory, int)>(), false, options);

        TechNodeStatus[] s = tree.StatusFor(hud, new HashSet<int>(), 0, null);
        Assert.Equal(TechNodeStatus.Available, s[IndexOf(tree, structure)]);
        Assert.Equal(TechNodeStatus.Locked, s[IndexOf(tree, gated)]);

        s = tree.StatusFor(hud, new HashSet<int> { structure.TypeIndex }, 0, null);
        Assert.Equal(TechNodeStatus.Built, s[IndexOf(tree, structure)]);

        s = tree.StatusFor(hud, new HashSet<int>(), 0, (_, c, i) => !(c == gated.Category && i == gated.TypeIndex));
        Assert.Equal(TechNodeStatus.MissionLocked, s[IndexOf(tree, gated)]);
        Assert.Equal(TechNodeStatus.Available, s[IndexOf(tree, structure)]);
    }

    private static int IndexOf(TechTreeModel tree, TechNode n) { for (int i = 0; i < tree.Nodes.Count; i++) if (tree.Nodes[i].Id == n.Id) return i; return -1; }

    [Fact]
    public void Tooltip_lines_name_cost_time_needs_and_unlocks()
    {
        TechTreeModel tree = TechTreeModel.Build(Rules, Faction.Authority);
        TechNode n = tree.Nodes.First(x => x.Needs.Count > 0 && x.Unlocks.Count > 0);
        IReadOnlyList<string> lines = TechTreeText.Lines(n, TechNodeStatus.Locked, id => id);
        Assert.Equal(n.Id, lines[0]);
        Assert.Contains(lines, l => l.StartsWith("Cost: " + n.Cost, StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("Build time: ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("Needs: ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("Unlocks: ", StringComparison.Ordinal));
    }

    [Fact]
    public void Tech_tree_key_is_listed_and_does_not_clash()
    {
        ActionInfo info = GameKeys.InfoOf(GameAction.TechTree);
        Assert.Equal(GameAction.TechTree, info.Action);
        Assert.Contains(GameKeys.Rows, r => r.Action == GameAction.TechTree);
        Assert.DoesNotContain(GameKeys.Infos, i => i.Action != GameAction.TechTree && i.Default == info.Default);
        Assert.False(string.IsNullOrEmpty(UiText.Get(UiText.TechTreeMenu)));
        Assert.NotEqual(UiText.TechTreeMenu, UiText.Get(UiText.TechTreeMenu));
    }
}
