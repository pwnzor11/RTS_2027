// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Controls;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Game.Screens;
using Rts.Game.Tests.Skirmish;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S18;

/// <summary>S18 GAMEUI-01: a right-click on an allied unit or building must not issue an Attack the sim refuses.</summary>
public sealed class UiAllyRightClickTests : IDisposable
{
    private readonly string _replay = Path.Combine(Path.GetTempPath(), "rts_s18ally_" + Guid.NewGuid().ToString("N") + ".rpl");
    private readonly SkirmishTestMaps _maps = new();
    private SimSnapshotSource? _source;

    public void Dispose()
    {
        _source?.SaveReplay();
        foreach (string f in new[] { _replay, _replay + ".prev", _replay + ".map.toml" })
            if (File.Exists(f)) File.Delete(f);
        _maps.Dispose();
    }

    private static SkirmishSlot Human(int team) => new(SlotKind.Human, Faction.Authority, 2, 0, team);
    private static SkirmishSlot Ai(int colour, int team) => new(SlotKind.Ai, Faction.Authority, 2, colour, team);

    // Player 0 (human) and player 1 are allies (team 0); player 2 is the enemy (team 1).
    private (Selection Selection, OrderController Orders, Camera Camera, EntityId Mine, EntityId AllyUnit, EntityId EnemyUnit) Rig()
    {
        string map = _maps.WriteFourStartMap();
        var settings = new SkirmishSettings(new MapChoice(map, null), new[] { Human(0), Ai(1, 0), Ai(2, 1) }, 4242);
        SkirmishGame game = SkirmishGame.Create(_maps.RulesDir, settings);
        _source = new SimSnapshotSource(game, _maps.RulesDir, _replay);
        Simulation sim = game.Sim;
        Assert.Equal(Relation.Ally, sim.RelationOf(0, 1));
        Assert.Equal(Relation.Enemy, sim.RelationOf(0, 2));
        int jeep = SimRules.IndexOf(sim.Setup.Rules.Vehicles, "auth_jeep", v => v.Id);
        EntityId mine = sim.SpawnVehicle(0, jeep, new CellCoord(20, 20));
        EntityId ally = sim.SpawnVehicle(1, jeep, new CellCoord(22, 20));
        EntityId enemy = sim.SpawnVehicle(2, jeep, new CellCoord(24, 20));
        _source.StepTicks(2);
        _source.Host.RefreshSnapshots();
        Assert.Contains(_source.Current.Units, u => u.Id == ally);
        Assert.Contains(_source.Current.Units, u => u.Id == enemy);
        var selection = new Selection();
        selection.Replace(new[] { new UnitKey(ActorKind.Vehicle, mine) });
        return (selection, new OrderController(_source, selection), new Camera(), mine, ally, enemy);
    }

    [Fact]
    public void Classify_never_offers_Attack_on_an_allied_building()
    {
        var r = Rig();
        BuildingView ally = _source!.Current.Buildings.First(b => b.Player == 1);
        foreach (ActorKind kind in new[] { ActorKind.Vehicle, ActorKind.Infantry })
            Assert.NotEqual(BuildingClick.Attack, GarrisonOrders.Classify(_source.Rules, kind, 0, ally, Relation.Ally));
    }

    private static Point Screen(Camera camera, CellCoord cell) => camera.ScreenFromWorld(Camera.CentreOf(cell)).ToPoint();

    private List<RenderUnit> Units() =>
        _source!.Current.Units.Select(u => new RenderUnit(u, Camera.WorldFromLepton(u.Position))).ToList();

    [Fact]
    public void Right_click_on_an_allied_unit_moves_instead_of_attacking()
    {
        var r = Rig();
        int before = _source!.Host.Pending.Count;
        r.Orders.RightClick(Screen(r.Camera, new CellCoord(22, 20)), r.Camera, Units());
        var sent = _source.Host.Pending.Skip(before).ToList();
        Assert.Contains(sent, c => c.Subject == r.Mine && c.Kind == CommandKind.Move);
        Assert.DoesNotContain(sent, c => c.Kind == CommandKind.Attack);
    }

    [Fact]
    public void Right_click_on_an_enemy_unit_still_attacks()
    {
        var r = Rig();
        int before = _source!.Host.Pending.Count;
        r.Orders.RightClick(Screen(r.Camera, new CellCoord(24, 20)), r.Camera, Units());
        var sent = _source.Host.Pending.Skip(before).ToList();
        Assert.Contains(sent, c => c.Subject == r.Mine && c.Kind == CommandKind.Attack && c.Target == r.EnemyUnit);
    }

    [Fact]
    public void Right_click_on_an_allied_building_moves_and_the_cursor_is_not_attack()
    {
        var r = Rig();
        BuildingView ally = _source!.Current.Buildings.First(b => b.Player == 1);
        CellCoord inside = ally.Cell;
        int before = _source.Host.Pending.Count;
        r.Orders.RightClick(Screen(r.Camera, inside), r.Camera, Units());
        var sent = _source.Host.Pending.Skip(before).ToList();
        Assert.DoesNotContain(sent, c => c.Kind == CommandKind.Attack);
        Assert.Contains(sent, c => c.Subject == r.Mine && c.Kind == CommandKind.Move);

        var preview = new CursorPreview();
        CursorKind cursor = preview.Probe(_source, r.Selection, Screen(r.Camera, inside), r.Camera, Units(), false, false);
        Assert.NotEqual(CursorKind.Attack, cursor);
        CursorKind overAllyUnit = preview.Probe(_source, r.Selection, Screen(r.Camera, new CellCoord(22, 20)), r.Camera, Units(), false, false);
        Assert.NotEqual(CursorKind.Attack, overAllyUnit);
        CursorKind overEnemy = preview.Probe(_source, r.Selection, Screen(r.Camera, new CellCoord(24, 20)), r.Camera, Units(), false, false);
        Assert.Equal(CursorKind.Attack, overEnemy);
    }
}
