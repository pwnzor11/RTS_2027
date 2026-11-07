// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Sandbox;
using Rts.Game.Ui.Card;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Ui.Card;

/// <summary>Which fields the card shows for a unit, a group and a building, on a real skirmish.</summary>
public sealed class CardModelBuilderTests
{
    private readonly string _rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;
    private readonly SimSnapshotSource _source;
    private readonly Selection _selection = new();

    public CardModelBuilderTests()
    {
        _source = new SimSnapshotSource(SkirmishGame.Create(_rulesDir, 0, 1), _rulesDir);
    }

    private UnitKey Spawn(int infantryType, int n)
    {
        CellCoord start = _source.Map.StartPositions[0];
        EntityId id = _source.Sim.SpawnInfantry(0, infantryType, new CellCoord(start.X + 3 + n, start.Y + 6));
        return new UnitKey(ActorKind.Infantry, id);
    }

    private void Settle() => _source.StepTicks(1);   // setup spawns are only allowed before the first step

    private CardModel Build(EntityId building = default) => CardModelBuilder.Build(_source, _selection, 0, building.IsNone ? EntityId.None : building);

    [Fact]
    public void Nothing_selected_gives_no_card()
    {
        Assert.False(Build().Visible);
        Assert.Equal(CardKind.None, Build().Kind);
    }

    [Fact]
    public void One_unit_shows_name_type_health_stats_and_orders()
    {
        UnitKey k = Spawn(0, 0);
        Settle();
        _selection.Replace(new[] { k });
        CardModel m = Build();
        Assert.Equal(CardKind.Unit, m.Kind);
        Assert.False(string.IsNullOrEmpty(m.Title));
        Assert.StartsWith("Infantry - ", m.SubLine);
        Assert.True(m.MaxHealth > 0);
        Assert.Equal(m.MaxHealth, m.Health);
        Assert.Equal(new[] { "Damage", "Range", "Speed", "Armour", "Sight" }, m.Stats.Select(s => s.Label).ToArray());
        Assert.Equal(new[] { GameAction.Stop, GameAction.Guard, GameAction.Scatter, GameAction.AttackMove },
            m.Buttons.Select(b => b.Action).Take(4).ToArray());
        Assert.All(m.Buttons, b => Assert.Equal(CardButtonKind.Order, b.Kind));
        Assert.All(m.Buttons.Where(b => b.Label != "Deploy"), b => Assert.Equal(KeyNames.Of(GameKeys.Active.Get(b.Action)), b.KeyText));
        Assert.DoesNotContain(m.Buttons, b => b.Action == GameAction.Deploy);   // an infantry unit cannot deploy
        Assert.Single(m.Cameos);
    }

    [Fact]
    public void A_group_shows_cameos_grouped_by_type_with_counts_and_no_stats()
    {
        int types = _source.Rules.Infantry.Count;
        Assert.True(types >= 2, "the rules need two infantry types for this test");
        UnitKey a1 = Spawn(0, 0), b1 = Spawn(1, 1), a2 = Spawn(0, 2);
        Settle();
        _selection.Replace(new[] { a1, b1, a2 });
        CardModel m = Build();
        Assert.Equal(CardKind.Multi, m.Kind);
        Assert.Equal(3, m.Cameos.Count);
        Assert.Equal(new[] { a1, a2, b1 }, m.Cameos.Select(c => c.Key).ToArray());
        Assert.Equal(new[] { 2, 1 }, m.Groups.Select(g => g.Count).ToArray());
        Assert.StartsWith("3 selected", m.Title);
        Assert.Empty(m.Stats);
        Assert.Null(m.RankLine);
        Assert.True(m.Buttons.Count >= 4);
    }

    [Fact]
    public void A_building_shows_power_production_rally_and_sell_repair()
    {
        BuildingView yard = _source.Current.Buildings.First(b => b.Player == 0 && !b.Ghost);
        BuildingType t = _source.Rules.Buildings[yard.TypeIndex];
        CardModel m = Build(yard.Id);
        Assert.Equal(CardKind.Building, m.Kind);
        Assert.StartsWith("Structure - ", m.SubLine);
        Assert.Equal(t.MaxHealth, m.MaxHealth);
        Assert.Empty(m.Stats);
        Assert.Contains(m.Lines, l => l.StartsWith("Power", StringComparison.Ordinal));
        if (t.Produces is not null)
        {
            Assert.Contains(m.Lines, l => l.StartsWith("Rally point", StringComparison.Ordinal));
            Assert.Contains(m.Lines, l => l is "Idle" || l.StartsWith("Building", StringComparison.Ordinal) || l.StartsWith("Ready", StringComparison.Ordinal));
        }

        Assert.Equal(new[] { CardButtonKind.Sell, CardButtonKind.Repair }, m.Buttons.Select(b => b.Kind).ToArray());
        Assert.Empty(m.Cameos);
    }

    [Fact]
    public void A_selected_unit_takes_precedence_over_a_building_id()
    {
        UnitKey k = Spawn(0, 0);
        Settle();
        _selection.Replace(new[] { k });
        BuildingView yard = _source.Current.Buildings.First(b => b.Player == 0 && !b.Ghost);
        Assert.Equal(CardKind.Unit, Build(yard.Id).Kind);
    }

    [Fact]
    public void An_unknown_building_gives_no_card()
    {
        Assert.False(Build(new EntityId(9999, 1)).Visible);
    }

    [Fact]
    public void Production_text_names_what_is_being_built_and_the_queue()
    {
        SimRules r = _source.Rules;
        var hud = _source.Sim.HudFor(0);
        Assert.Equal("Idle", CardModelBuilder.ProductionText(r, hud, ProductionCategory.Aircraft));
        var queued = hud with
        {
            Queues = new[]
            {
                new QueueView(ProductionCategory.Infantry, new[] { new QueueItemView(0, 450, false), new QueueItemView(0, 0, false), new QueueItemView(0, 0, false) }, -1),
            },
        };
        string text = CardModelBuilder.ProductionText(r, queued, ProductionCategory.Infantry);
        Assert.StartsWith("Building ", text);
        Assert.Contains("45%", text);
        Assert.EndsWith("+2 queued", text);
    }

    [Fact]
    public void Selection_remove_drops_one_unit_only()
    {
        UnitKey a = Spawn(0, 0), b = Spawn(0, 1);
        _selection.Replace(new[] { a, b });
        Assert.True(_selection.Remove(a));
        Assert.False(_selection.Remove(a));
        Assert.Equal(new[] { b }, _selection.Units.ToArray());
    }
}
