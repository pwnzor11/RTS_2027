// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Ui;

/// <summary>The sidebar's mouse and key handling on a real skirmish, driven by synthetic input frames (no window).</summary>
public sealed class SidebarPanelTests : IDisposable
{
    private const int W = 1280, H = 720;
    private readonly string _rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;
    private readonly SimSnapshotSource _source;
    private readonly Selection _selection = new();
    private readonly Camera _camera = new();
    private readonly SidebarPanel _panel;
    private MouseState _prev = new(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
    private KeyboardState _prevKeys;
    private readonly int _power;

    public SidebarPanelTests()
    {
        var game = SkirmishGame.Create(_rulesDir, 0, 1);
        _source = new SimSnapshotSource(game, _rulesDir);
        _panel = new SidebarPanel(_source, 0, _selection, null, Color.Green);
        _power = SimRules.IndexOf(_source.Rules.Buildings, "auth_power", b => b.Id);
        Frame(new Point(0, 0));   // first update computes the layout
    }

    public void Dispose() => _panel.Dispose();

    private InputFrame Frame(Point at, bool left = false, bool right = false, int wheel = 0, params Keys[] keys)
    {
        var mouse = new MouseState(at.X, at.Y, wheel, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released,
            right ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);
        var kb = new KeyboardState(keys);
        var input = new InputFrame(kb, _prevKeys, mouse, _prev);
        _panel.Update(input, _camera, W, H);
        _prev = mouse;
        _prevKeys = kb;
        return input;
    }

    // A click is a press frame then a release frame.
    private InputFrame Click(Point at, bool right = false)
    {
        InputFrame press = Frame(at, left: !right, right: right);
        Frame(at);
        return press;
    }

    // M4: the Structures tab opens with a "Tier 1" heading row (locked tier 2/3 previews exist), so the first cameo is one row down.
    private Rectangle PowerCameo() => _panel.TierCellRect(1, 0);   // row 0 is the Tier 1 heading

    private PlayerHud Hud() => _source.Sim.HudFor(0);

    private void StepUntil(Func<bool> done, int max = 3000)
    {
        for (int i = 0; i < max && !done(); i++) _source.StepTicks(1);
        Assert.True(done());
    }

    private void BuildPowerPlant()
    {
        Point cameo = PowerCameo().Center;
        Click(cameo);
        StepUntil(() => SidebarCommands.ReadyType(Hud(), ProductionCategory.Structure) == _power);
    }

    private Point ScreenOfCell(CellCoord c) => new(c.X * Camera.CellPx + 32, c.Y * Camera.CellPx + 32);

    private CellCoord FindValidTopLeft()
    {
        CellCoord start = _source.Map.StartPositions[0];
        for (int dy = -6; dy <= 6; dy++)
            for (int dx = -6; dx <= 6; dx++)
            {
                var tl = new CellCoord(start.X + dx, start.Y + dy);
                if (tl.X >= 0 && tl.Y >= 0 && tl.X < 14 && tl.Y < 10 && _source.Sim.CanPlaceReady(0, _power, tl)) return tl;
            }
        throw new InvalidOperationException("no spot");
    }

    private int BuildingCount() => _source.Current.Buildings.Count(b => b.Player == 0 && !b.Ghost);

    [Fact]
    public void Panel_swallows_the_mouse_over_it_and_leaves_the_map_alone()
    {
        InputFrame over = Frame(_panel.Layout.Grid.Center, left: true);
        Assert.False(over.LeftPressed);
        Assert.False(over.RightPressed);
        Frame(new Point(100, 100));   // button released
        InputFrame outside = Frame(new Point(100, 100), left: true);
        Assert.True(outside.LeftPressed);
        Assert.True(_panel.HitTest(_panel.Layout.Panel.Center));
        Assert.False(_panel.HitTest(new Point(100, 100)));
    }

    [Fact]
    public void Tab_keys_and_tab_clicks_switch_tabs()
    {
        Frame(new Point(100, 100), keys: Keys.W);
        Assert.Equal(SidebarTab.Defences, _panel.Tab);
        Frame(new Point(100, 100), keys: Keys.E);
        Assert.Equal(SidebarTab.Infantry, _panel.Tab);
        Frame(new Point(100, 100), keys: Keys.R);
        Assert.Equal(SidebarTab.Vehicles, _panel.Tab);
        Frame(new Point(100, 100), keys: Keys.Q);
        Assert.Equal(SidebarTab.Structures, _panel.Tab);
        Click(_panel.Layout.Tabs[2].Center);
        Assert.Equal(SidebarTab.Infantry, _panel.Tab);
    }

    [Fact]
    public void Left_click_queues_right_click_cancels()
    {
        Point cameo = PowerCameo().Center;
        InputFrame press = Click(cameo);
        Assert.False(press.LeftPressed);   // consumed: the map never sees it
        _source.StepTicks(2);
        QueueView q = Hud().Queues.Single(x => x.Category == ProductionCategory.Structure);
        Assert.Single(q.Items);
        Assert.Equal(_power, q.Items[0].TypeIndex);

        Click(cameo, right: true);
        _source.StepTicks(2);
        Assert.Empty(Hud().Queues.Single(x => x.Category == ProductionCategory.Structure).Items);
    }

    [Fact]
    public void Placement_mode_places_on_a_valid_cell_and_ignores_invalid_ones()
    {
        BuildPowerPlant();
        Click(PowerCameo().Center);
        Assert.Equal(SidebarMode.Placement, _panel.Mode);

        int before = BuildingCount();
        CellCoord start = _source.Map.StartPositions[0];
        InputFrame bad = Frame(ScreenOfCell(start), left: true);   // on top of the yard: invalid
        Assert.False(bad.LeftPressed);
        Frame(ScreenOfCell(start));
        Assert.Equal(SidebarMode.Placement, _panel.Mode);
        _source.StepTicks(2);
        Assert.Equal(before, BuildingCount());

        CellCoord tl = FindValidTopLeft();
        Point at = ScreenOfCell(new CellCoord(tl.X + 1, tl.Y + 1));   // 2x2 footprint is centred on the cursor cell
        Frame(at);
        InputFrame good = Frame(at, left: true);
        Assert.False(good.LeftPressed);
        Assert.Equal(SidebarMode.None, _panel.Mode);
        _source.StepTicks(2);
        Assert.Equal(before + 1, BuildingCount());
        Assert.Equal(-1, SidebarCommands.ReadyType(Hud(), ProductionCategory.Structure));
    }

    [Fact]
    public void Right_click_leaves_placement_and_keeps_the_finished_item()
    {
        BuildPowerPlant();
        Click(PowerCameo().Center);
        Assert.Equal(SidebarMode.Placement, _panel.Mode);
        InputFrame rc = Frame(new Point(300, 300), right: true);
        Assert.False(rc.RightPressed);
        Assert.Equal(SidebarMode.None, _panel.Mode);
        Assert.Equal(_power, SidebarCommands.ReadyType(Hud(), ProductionCategory.Structure));
        Click(PowerCameo().Center);
        Assert.True(_panel.CancelMode());
        Assert.False(_panel.CancelMode());
    }

    [Fact]
    public void Sell_mode_sells_the_clicked_own_building_and_stays_on_until_right_click()
    {
        BuildPowerPlant();
        CellCoord tl = FindValidTopLeft();
        Click(PowerCameo().Center);
        Point at = ScreenOfCell(new CellCoord(tl.X + 1, tl.Y + 1));
        Frame(at, left: true);
        Frame(at);
        _source.StepTicks(2);
        int built = BuildingCount();
        int cash = _source.Sim.Players.Cash[0];

        Click(_panel.Layout.SellButton.Center);
        Assert.Equal(SidebarMode.Sell, _panel.Mode);
        Frame(ScreenOfCell(tl), left: true);   // the new power plant
        Frame(ScreenOfCell(tl));
        _source.StepTicks(2);
        Assert.Equal(built - 1, BuildingCount());
        Assert.True(_source.Sim.Players.Cash[0] > cash);
        Assert.Equal(SidebarMode.Sell, _panel.Mode);

        Frame(new Point(500, 500), right: true);
        Assert.Equal(SidebarMode.None, _panel.Mode);
    }

    [Fact]
    public void Repair_button_toggles_its_mode_and_switching_replaces_the_mode()
    {
        Click(_panel.Layout.RepairButton.Center);
        Assert.Equal(SidebarMode.Repair, _panel.Mode);
        Click(_panel.Layout.SellButton.Center);
        Assert.Equal(SidebarMode.Sell, _panel.Mode);
        Click(_panel.Layout.SellButton.Center);
        Assert.Equal(SidebarMode.None, _panel.Mode);
    }

    [Fact]
    public void Minimap_click_moves_the_camera_and_dragging_keeps_following()
    {
        Rectangle mini = _panel.Layout.Minimap;
        Point far = new(mini.X + mini.Width * 9 / 10, mini.Y + mini.Height * 9 / 10);
        Vector2 before = _camera.WorldFromScreen(Point.Zero);
        InputFrame press = Frame(far, left: true);
        Assert.False(press.LeftPressed);
        Vector2 after = _camera.WorldFromScreen(Point.Zero);
        Assert.True(after.X > before.X + 100 && after.Y > before.Y + 100, $"{before} -> {after}");
        Frame(far);   // release ends the drag

        // Drag keeps following while the button is held, even if the cursor leaves the minimap.
        Point near = new(mini.X + mini.Width / 10, mini.Y + mini.Height / 10);
        Frame(near, left: true);
        Vector2 back = _camera.WorldFromScreen(Point.Zero);
        Assert.True(back.X < after.X && back.Y < after.Y);
    }
}
