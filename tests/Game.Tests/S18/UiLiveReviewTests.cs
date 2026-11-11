// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Game.Ui.Card;
using Rts.Game.Ui.Sidebar;
using Rts.Game.Ui.Spectator;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S18;

/// <summary>S18 live-play review: UXG-01 (held structure stays on the sidebar), UXG-03 (card never covers the dialogue), UXG-06 (unit reason), UXG-08, UXG-10.</summary>
public sealed class UiLiveReviewTests : IDisposable
{
    private readonly string _rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;
    private readonly string _replay = Path.Combine(Path.GetTempPath(), "rts_s18live_" + Guid.NewGuid().ToString("N") + ".rpl");
    private SimSnapshotSource? _source;
    private SidebarPanel? _panel;

    public void Dispose()
    {
        _panel?.Dispose();
        _source?.SaveReplay();
        foreach (string f in new[] { _replay, _replay + ".prev", _replay + ".map.toml" })
            if (File.Exists(f)) File.Delete(f);
    }

    private static int Building(SimRules r, string id) => SimRules.IndexOf(r.Buildings, id, b => b.Id);

    private CellCoord FreeSpot(Simulation sim, int type)
    {
        CellCoord start = sim.Setup.Map.StartPositions[0];
        for (int r = 3; r < 16; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    var tl = new CellCoord(start.X + dx, start.Y + dy);
                    if (sim.CanPlace(0, type, tl) == Rts.Sim.World.PlacementResult.Ok) return tl;
                }
        throw new InvalidOperationException("no spot");
    }

    // UXG-01 ------------------------------------------------------------------------------------

    [Fact]
    public void Missing_returns_the_held_ready_and_in_progress_types_that_are_not_shown()
    {
        SimRules rules = RulesLoader.Load(_rulesDir).Sim;
        var hud = new PlayerHud(0, 0, 0, 0, 0,
            new[] { new QueueView(ProductionCategory.Structure, new[] { new QueueItemView(2, 500, false) }, 1), new QueueView(ProductionCategory.Infantry, Array.Empty<QueueItemView>(), -1) },
            Array.Empty<(ProductionCategory, int)>(), false);
        var shown = new List<BuildOption> { new(ProductionCategory.Structure, 2, true, QueueBlockReason.None, 0) };
        var missing = SidebarQueueOptions.Missing(hud, rules, ProductionCategory.Structure, shown).ToList();
        BuildOption only = Assert.Single(missing);
        Assert.Equal((ProductionCategory.Structure, 1), (only.Category, only.TypeIndex));
        Assert.Empty(SidebarQueueOptions.Missing(hud, rules, ProductionCategory.Infantry, shown));
    }

    [Fact]
    public void A_finished_structure_whose_prerequisite_was_sold_stays_on_the_sidebar_so_it_can_be_placed()
    {
        var game = SkirmishGame.Create(_rulesDir, 0, 1);
        _source = new SimSnapshotSource(game, _rulesDir, _replay);
        Simulation sim = game.Sim;
        SimRules rules = _source.Rules;
        int power = Building(rules, "auth_power"), refinery = Building(rules, "auth_refinery"), factory = Building(rules, "auth_factory");
        sim.Players.Cash[0] = 20000;
        sim.PlaceBuilding(0, power, FreeSpot(sim, power));
        EntityId refineryId = sim.PlaceBuilding(0, refinery, FreeSpot(sim, refinery));
        _source.Host.RefreshSnapshots();
        PlayerHud hud = sim.HudFor(0);
        BuildOption factoryOption = Assert.Single(hud.Options, o => o.Category == ProductionCategory.Structure && o.TypeIndex == factory);
        _source.Host.Issue(SidebarCommands.Build(0, factoryOption)!.Value);
        for (int i = 0; i < 6000 && SidebarCommands.ReadyType(sim.HudFor(0), ProductionCategory.Structure) != factory; i++) _source.StepTicks(1);
        Assert.Equal(factory, SidebarCommands.ReadyType(sim.HudFor(0), ProductionCategory.Structure));

        _source.Host.Issue(SidebarCommands.Sell(0, refineryId));   // the prerequisite goes away while the factory waits to be placed
        _source.StepTicks(400);
        hud = sim.HudFor(0);
        Assert.DoesNotContain(hud.Options, o => o.Category == ProductionCategory.Structure && o.TypeIndex == factory);   // the sim's behaviour: not offered
        Assert.Equal(factory, SidebarCommands.ReadyType(hud, ProductionCategory.Structure));                              // ... but still holding the slot

        _panel = new SidebarPanel(_source, 0, new Selection(), null, Color.Green);
        var none = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        _panel.Update(new InputFrame(new KeyboardState(), new KeyboardState(), none, none), new Camera(), 1280, 720);
        Assert.Contains(_panel.VisibleOptions, o => o.Category == ProductionCategory.Structure && o.TypeIndex == factory);
    }

    // UXG-03 ------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1280, 720, 1f)]
    [InlineData(1920, 1080, 1.5f)]
    public void Dialogue_bottom_is_above_the_card_when_the_card_shows(int w, int h, float ui)
    {
        var mapView = new Rectangle(0, 0, w - 300, h);
        float s = CardLayout.ScaleFor(mapView.Width, ui);
        CardLayout card = CardLayout.Compute(mapView, s, 14, CardKind.Unit, 6, 1);
        int defaultBottom = h - 8;
        int bottom = card.ClearBottom(defaultBottom, 4);
        Assert.True(bottom <= card.Card.Top - 4, $"{bottom} vs card top {card.Card.Top}");
        Assert.True(bottom < defaultBottom);
        Assert.Equal(defaultBottom, CardLayout.None.ClearBottom(defaultBottom, 4));
    }

    // UXG-06 ------------------------------------------------------------------------------------

    [Fact]
    public void A_unit_on_buildable_ground_is_named_as_a_unit_not_as_terrain()
    {
        var game = SkirmishGame.Create(_rulesDir, 0, 1);
        _source = new SimSnapshotSource(game, _rulesDir, _replay);
        Simulation sim = game.Sim;
        int jeep = SimRules.IndexOf(_source.Rules.Vehicles, "auth_jeep", v => v.Id);
        CellCoord start = sim.Setup.Map.StartPositions[0];
        CellCoord cell = default;
        bool found = false;
        for (int dx = 4; dx < 12 && !found; dx++)
        {
            var c = new CellCoord(start.X + dx, start.Y + 4);
            if (sim.Map.IsBuildable(c, _source.Rules.Terrain)) { cell = c; found = true; }
        }

        Assert.True(found);
        sim.SpawnVehicle(0, jeep, cell);
        Assert.False(sim.Map.IsBuildable(cell, _source.Rules.Terrain), "IsBuildable also fails for a unit; that is why the old call said terrain");
        CellProblem p = PlacementCheck.CellAt(sim.Map, _source.Rules.Terrain, false, cell, unitHere: true);
        Assert.Equal(CellProblem.Unit, p);
        Assert.Equal("Blocked by a unit", PlacementCheck.Reason(Rts.Sim.World.PlacementResult.Blocked, false, p));
        Assert.Equal(CellProblem.None, PlacementCheck.CellAt(sim.Map, _source.Rules.Terrain, false, new CellCoord(cell.X + 1, cell.Y), unitHere: false));
    }

    [Fact]
    public void A_building_cell_is_still_named_a_building()
    {
        Assert.Equal(CellProblem.Building, PlacementCheck.Cell(false, false, true, true, false));
        Assert.Equal(CellProblem.Terrain, PlacementCheck.Cell(false, false, false, false, true));
        Assert.Equal(CellProblem.Unit, PlacementCheck.Cell(false, false, true, false, true));
    }

    // UXG-08 ------------------------------------------------------------------------------------

    [Fact]
    public void Content_bottom_is_below_the_last_placed_row()
    {
        Assert.Equal(0, TierGrouping.ContentBottom(Array.Empty<TierGrouping.PlacedRow>()));
        var rows = new[] { new TierGrouping.PlacedRow(0, 0, 20), new TierGrouping.PlacedRow(1, 24, 60), new TierGrouping.PlacedRow(2, 88, 20) };
        Assert.Equal(108, TierGrouping.ContentBottom(rows));
    }

    // UXG-10 ------------------------------------------------------------------------------------

    // A proportional font: narrow i, l, 1, wide W, M; space 4 px.
    private static int Px(string s)
    {
        int w = 0;
        foreach (char c in s) w += c switch { ' ' => 4, 'i' or 'l' or '1' or 'I' => 3, 'W' or 'M' => 11, _ => 7 };
        return w;
    }

    [Fact]
    public void Final_table_columns_start_at_the_same_pixel_on_every_line()
    {
        var stats = new PlayerStats();
        SpectatorRow Row(int p, string _, int cash, int income, int army) =>
            new(p, 0, Faction.Authority, 2, cash, 0, income, army, 5, 100, 50, stats, 0, false, false);
        var rows = new[] { Row(0, "", 12, 0, 5), Row(1, "", 1234, 1500, 10000) };
        IReadOnlyList<string> lines = SpectatorTable.Final(rows, Px);
        Assert.Equal(3, lines.Count);
        // The header word and the cell below it begin within one space width of each other.
        foreach (string[] cells in lines.Skip(1).Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            Assert.True(cells.Length >= 10);
        int Col(string line, int index)
        {
            int seen = -1;
            for (int i = 0; i < line.Length; i++)
                if (line[i] != ' ' && (i == 0 || line[i - 1] == ' ') && ++seen == index) return Px(line[..i]);
            return -1;
        }

        for (int index = 1; index < 6; index++)
        {
            int header = Col(lines[0], index);
            foreach (string line in lines.Skip(1)) Assert.InRange(Math.Abs(Col(line, index) - header), 0, 4);
        }

        Assert.Contains("1500", lines[2]);
    }
}
