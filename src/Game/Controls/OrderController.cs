// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Sim.Core;
using Rts.Sim.View;

namespace Rts.Game.Controls;

/// <summary>
/// Turns keys and right-clicks into orders for the selected units. Every order goes through
/// <see cref="SimSnapshotSource.Order"/>, the same path a script uses. Keys: A arms attack-move (the next right-click
/// on the ground is an attack-move; A again disarms), S stops, G guards at each unit's own cell, X scatters, D deploys (M3: a
/// land-train or a transport at once; a Bore after D then a right-click on the cell to dig at).
/// Right-click: an enemy under the cursor is an Attack (an Engineer on an enemy or neutral building Enters to capture it, and on an own
/// damaged building Enters to repair it), an own transport (for infantry) or own tunnel entrance (for units that fit
/// it) is an Enter, the ground is a Move (or an attack-move when armed). M6-09: a unit that salvages (a Hauler or Tinker) right-clicking a wreck
/// is a Harvest on that cell.
/// </summary>
public sealed class OrderController
{
    private readonly SimSnapshotSource _source;
    private readonly Selection _selection;
    private readonly List<WreckView> _wrecks = new();   // M6-09: refreshed on each right click

    /// <summary>Creates the controller for the local player's selection.</summary>
    public OrderController(SimSnapshotSource source, Selection selection)
    {
        _source = source;
        _selection = selection;
    }

    /// <summary>True after A, until the next click (left or right), Esc, or A again.</summary>
    public bool AttackMoveArmed { get; private set; }

    /// <summary>True after D when a selected unit deploys at a chosen cell (a Bore), until the next right-click or D again.</summary>
    public bool DeployArmed { get; private set; }

    /// <summary>O1: where the order rings are queued (set by the game screen; null in tests).</summary>
    public OrderFeedback? Feedback { get; set; }

    /// <summary>True while any order waits for a click (attack-move or a deploy cell).</summary>
    public bool AnyArmed => AttackMoveArmed || DeployArmed;

    /// <summary>Esc: disarms attack-move and deploy. Returns true when something was armed (the key is then used up).</summary>
    public bool CancelArmed()
    {
        bool was = AnyArmed;
        AttackMoveArmed = false;
        DeployArmed = false;
        return was;
    }

    /// <summary>M4-09: the building the player has selected (set by the screen each frame; <see cref="EntityId.None"/> = none).
    /// D on a garrisoned own building then sends Unload.</summary>
    public EntityId SelectedBuilding { get; set; } = EntityId.None;

    /// <summary>Handles an order action whose key went down this frame. Ignores actions that are not orders.</summary>
    public void KeyPressed(GameAction action)
    {
        switch (action)
        {
            case GameAction.AttackMove:
                AttackMoveArmed = !AttackMoveArmed;
                break;
            case GameAction.Stop:
                IssueToSelection(CommandKind.Stop);
                break;
            case GameAction.Deploy:
                if (!TryUnloadSelectedBuilding()) Deploy();
                break;
            case GameAction.Scatter:
                IssueToSelection(CommandKind.Scatter);
                break;
            case GameAction.Guard:
                foreach (UnitKey unit in _selection.Units.ToList())
                {
                    if (_source.TryGetPosition(unit, out LeptonCoord at)) _source.Order(CommandKind.Guard, unit, at.ToCell());
                }
                break;
        }
    }

    /// <summary>Handles a right-click at a screen point, using the units as drawn this frame for picking.</summary>
    public void RightClick(Point screen, Camera camera, IReadOnlyList<RenderUnit> units)
    {
        bool armed = AttackMoveArmed;
        bool deployArmed = DeployArmed;
        AttackMoveArmed = false;
        DeployArmed = false;
        if (_selection.Count == 0) return;

        Vector2 world = camera.WorldFromScreen(screen);
        CellCoord cell = Camera.CellAt(world);
        if (deployArmed)
        {
            DeployAt(cell);
            return;
        }

        ActorRef? enemy = TargetPicker.Pick(units, _source.Current, world, SimSnapshotSource.LocalPlayer);
        (ActorRef Target, int TypeIndex)? own = enemy is null
            ? TargetPicker.PickOwn(units, _source.Current, world, SimSnapshotSource.LocalPlayer, _selection.Contains)
            : null;
        EntityId buildingId = enemy is { Kind: ActorKind.Building } e ? e.Id : own is { Target.Kind: ActorKind.Building } o ? o.Target.Id : EntityId.None;
        BuildingView? building = buildingId.IsNone ? null : ViewOf(buildingId);
        bool wreckHere = false;
        if (!armed && _source.Map.Contains(cell))
        {
            _source.Sim.ViewFor(SimSnapshotSource.LocalPlayer).CopyWrecks(_wrecks);
            wreckHere = SalvageOrders.WreckAt(_wrecks, cell);
        }

        OrderMarkKind? mark = null;   // O1: one ring per click, not one per unit
        foreach (UnitKey unit in _selection.Units.ToList())
        {
            BuildingClick click = BuildingClick.None;
            if (building is BuildingView view && _source.TryGetTypeIndex(unit, out int type))
            {
                Relation relation = _source.Sim.RelationOf(SimSnapshotSource.LocalPlayer, view.Player);
                click = GarrisonOrders.Classify(_source.Rules, unit.Kind, type, view, relation);
            }

            if (click is BuildingClick.Capture or BuildingClick.Repair or BuildingClick.EnterTunnel or BuildingClick.Garrison)
            {
                _source.Order(CommandKind.Enter, unit, default, new ActorRef(ActorKind.Building, buildingId));   // M3-INT / M4-09
                mark ??= OrderMarkKind.Move;
            }
            else if (click == BuildingClick.Attack || (building is null && enemy is not null))
            {
                _source.Order(CommandKind.Attack, unit, default, enemy!.Value);
                mark = OrderMarkKind.Attack;
            }
            else if (own is { Target.Kind: ActorKind.Vehicle } mine && M3Orders.CanBoard(_source.Rules, unit.Kind, mine.TypeIndex))
            {
                _source.Order(CommandKind.Enter, unit, default, mine.Target);   // M3: board an own transport
            }
            else if (wreckHere && _source.TryGetTypeIndex(unit, out int salvager) && SalvageOrders.CanSalvage(_source.Rules, unit.Kind, salvager))
            {
                _source.Order(CommandKind.Harvest, unit, cell);   // M6-09: the order carries the unit kind, as SalvageMission expects
            }
            else if (!armed && unit.Kind == ActorKind.Vehicle && _source.TryGetTypeIndex(unit, out int carrier)
                && LandingOrders.ShouldUnload(_source.Rules, _source.Map, unit.Kind, carrier, _source.Sim.PassengersIn(unit.Id), cell))
            {
                _source.Order(CommandKind.Unload, unit, cell);   // M7-14: a landing craft or Skyhook with passengers puts them ashore at the cell
            }
            else if (_source.Map.Contains(cell))
            {
                _source.Order(armed ? CommandKind.AttackMove : CommandKind.Move, unit, cell);
                mark ??= armed ? OrderMarkKind.Attack : OrderMarkKind.Move;
            }
        }

        if (mark is { } kind) Feedback?.Add(enemy is not null ? world : Camera.CentreOf(cell), kind);
    }

    private BuildingView? ViewOf(EntityId id)
    {
        foreach (BuildingView view in _source.Current.Buildings)
            if (view.Id == id) return view;
        return null;
    }

    // M4-09: D on a selected own building that holds soldiers = Unload.
    private bool TryUnloadSelectedBuilding()
    {
        if (SelectedBuilding.IsNone) return false;
        if (!GarrisonOrders.CanUnload(ViewOf(SelectedBuilding), SimSnapshotSource.LocalPlayer)) return false;
        _source.Host.Issue(GarrisonOrders.Unload(SimSnapshotSource.LocalPlayer, SelectedBuilding));
        return true;
    }

    // D: units that deploy or unload where they stand do it now; one that digs at a chosen cell arms the next right-click.
    private void Deploy()
    {
        if (DeployArmed)
        {
            DeployArmed = false;
            return;
        }

        foreach (UnitKey unit in _selection.Units.ToList())
        {
            if (!_source.TryGetTypeIndex(unit, out int type)) continue;
            switch (M3Orders.DeployModeOf(_source.Rules, unit.Kind, type))
            {
                case DeployMode.InPlace:
                    if (_source.TryGetPosition(unit, out LeptonCoord at)) _source.Order(CommandKind.Deploy, unit, at.ToCell());
                    break;
                case DeployMode.AtCell:
                    DeployArmed = true;
                    break;
            }
        }
    }

    private void DeployAt(CellCoord cell)
    {
        if (!_source.Map.Contains(cell)) return;
        foreach (UnitKey unit in _selection.Units.ToList())
        {
            if (_source.TryGetTypeIndex(unit, out int type) && M3Orders.DeployModeOf(_source.Rules, unit.Kind, type) == DeployMode.AtCell)
            {
                _source.Order(CommandKind.Deploy, unit, cell);
            }
        }
    }

    private void IssueToSelection(CommandKind kind)
    {
        foreach (UnitKey unit in _selection.Units.ToList()) _source.Order(kind, unit);
    }
}
