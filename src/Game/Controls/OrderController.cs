// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Controls;

/// <summary>
/// Turns keys and right-clicks into orders for the selected units. Every order goes through
/// <see cref="SimSnapshotSource.Order"/>, the same path a script uses. Keys: A arms attack-move (the next right-click
/// on the ground is an attack-move; A again disarms), S stops, G guards at each unit's own cell, X scatters, D deploys (M3: a
/// land-train or a transport at once; a Bore after D then a right-click on the cell to dig at).
/// Right-click: an enemy under the cursor is an Attack (an Engineer on an enemy or neutral building Enters to capture it, and on an own
/// damaged building Enters to repair it), an own transport (for infantry) or own tunnel entrance (for units that fit
/// it) is an Enter, the ground is a Move (or an attack-move when armed).
/// </summary>
public sealed class OrderController
{
    private readonly SimSnapshotSource _source;
    private readonly Selection _selection;

    /// <summary>Creates the controller for the local player's selection.</summary>
    public OrderController(SimSnapshotSource source, Selection selection)
    {
        _source = source;
        _selection = selection;
    }

    /// <summary>True after A, until the next right-click or A again.</summary>
    public bool AttackMoveArmed { get; private set; }

    /// <summary>True after D when a selected unit deploys at a chosen cell (a Bore), until the next right-click or D again.</summary>
    public bool DeployArmed { get; private set; }

    /// <summary>Handles a key that went down this frame. Ignores keys that are not orders.</summary>
    public void KeyPressed(Keys key)
    {
        switch (key)
        {
            case Keys.A:
                AttackMoveArmed = !AttackMoveArmed;
                break;
            case Keys.S:
                IssueToSelection(CommandKind.Stop);
                break;
            case Keys.D:
                Deploy();
                break;
            case Keys.X:
                IssueToSelection(CommandKind.Scatter);
                break;
            case Keys.G:
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
        foreach (UnitKey unit in _selection.Units.ToList())
        {
            if (enemy is { Kind: ActorKind.Building } building && EngineerOrderOn(unit, building, ownBuilding: false) != EngineerOrder.None)
            {
                _source.Order(CommandKind.Enter, unit, default, building);   // M3-INT: an Engineer captures instead of attacking
            }
            else if (enemy is ActorRef target)
            {
                _source.Order(CommandKind.Attack, unit, default, target);
            }
            else if (own is { } mine && CanEnter(unit, mine))
            {
                _source.Order(CommandKind.Enter, unit, default, mine.Target);
            }
            else if (_source.Map.Contains(cell))
            {
                _source.Order(armed ? CommandKind.AttackMove : CommandKind.Move, unit, cell);
            }
        }
    }

    // M3: Board a transport (infantry) or enter an own tunnel entrance (a unit that fits). The sim re-checks both.
    private bool CanEnter(UnitKey unit, (ActorRef Target, int TypeIndex) target)
    {
        if (!_source.TryGetTypeIndex(unit, out int type)) return false;
        if (target.Target.Kind == ActorKind.Building && EngineerOrderOn(unit, target.Target, ownBuilding: true) == EngineerOrder.Repair) return true;   // M3-INT
        return target.Target.Kind switch
        {
            ActorKind.Vehicle => M3Orders.CanBoard(_source.Rules, unit.Kind, target.TypeIndex),
            ActorKind.Building => M3Orders.CanEnterTunnel(_source.Rules, unit.Kind, type, target.TypeIndex),
            _ => false,
        };
    }

    // M3-INT: Engineer on a building: capture (not ours) or repair (ours, damaged). Health is read from the snapshot.
    private EngineerOrder EngineerOrderOn(UnitKey unit, ActorRef building, bool ownBuilding)
    {
        if (!_source.TryGetTypeIndex(unit, out int type)) return EngineerOrder.None;
        foreach (BuildingView view in _source.Current.Buildings)
        {
            if (view.Id == building.Id)
                return M3Orders.EngineerOrderOn(_source.Rules, unit.Kind, type, ownBuilding, view.TypeIndex, view.Health);
        }
        return EngineerOrder.None;
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
