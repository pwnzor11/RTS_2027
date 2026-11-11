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

namespace Rts.Game.Tests.S18;

/// <summary>S18 GAMEUI-05: a minimap right-click honours an armed attack-move (and ends the arming) instead of always moving.</summary>
public sealed class UiMinimapOrderTests : IDisposable
{
    private const int W = 1280, H = 720;
    private readonly string _rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;
    private readonly string _replay = Path.Combine(Path.GetTempPath(), "rts_s18mini_" + Guid.NewGuid().ToString("N") + ".rpl");
    private readonly SimSnapshotSource _source;
    private readonly Selection _selection = new();
    private readonly Camera _camera = new();
    private readonly SidebarPanel _panel;
    private readonly OrderController _orders;
    private readonly UnitKey _jeep;
    private MouseState _prev = new(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
    private KeyboardState _prevKeys;

    public UiMinimapOrderTests()
    {
        var game = SkirmishGame.Create(_rulesDir, 0, 1);
        _source = new SimSnapshotSource(game, _rulesDir, _replay);
        _panel = new SidebarPanel(_source, 0, _selection, null, Color.Green);
        _orders = new OrderController(_source, _selection);
        _panel.Orders = _orders;
        int jeep = SimRules.IndexOf(_source.Rules.Vehicles, "auth_jeep", v => v.Id);
        CellCoord at = _source.Map.StartPositions[0];
        EntityId id = _source.Sim.SpawnVehicle(0, jeep, new CellCoord(at.X + 4, at.Y + 4));
        _source.Host.RefreshSnapshots();
        _jeep = new UnitKey(ActorKind.Vehicle, id);
        _selection.Replace(new[] { _jeep });
        Frame(new Point(0, 0));
    }

    public void Dispose()
    {
        _panel.Dispose();
        _source.SaveReplay();
        foreach (string f in new[] { _replay, _replay + ".prev", _replay + ".map.toml" })
            if (File.Exists(f)) File.Delete(f);
    }

    private void Frame(Point at, bool right = false)
    {
        var mouse = new MouseState(at.X, at.Y, 0, ButtonState.Released, ButtonState.Released,
            right ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);
        var kb = new KeyboardState();
        var input = new InputFrame(kb, _prevKeys, mouse, _prev);
        _panel.Update(input, _camera, W, H);
        _prev = mouse;
        _prevKeys = kb;
    }

    private void RightClickMinimap()
    {
        Point centre = _panel.Layout.Minimap.Center;
        Frame(centre, right: true);
        Frame(centre);
    }

    [Fact]
    public void Right_click_on_the_minimap_moves_when_nothing_is_armed()
    {
        int before = _source.Host.Pending.Count;
        RightClickMinimap();
        Command c = Assert.Single(_source.Host.Pending.Skip(before));
        Assert.Equal((CommandKind.Move, _jeep.Id), (c.Kind, c.Subject));
    }

    [Fact]
    public void Right_click_on_the_minimap_with_attack_move_armed_attack_moves_and_disarms()
    {
        _orders.KeyPressed(GameAction.AttackMove);
        Assert.True(_orders.AttackMoveArmed, "the jeep can attack, so A arms");
        int before = _source.Host.Pending.Count;
        RightClickMinimap();
        Command c = Assert.Single(_source.Host.Pending.Skip(before));
        Assert.Equal((CommandKind.AttackMove, _jeep.Id), (c.Kind, c.Subject));
        Assert.False(_orders.AttackMoveArmed);
    }
}
