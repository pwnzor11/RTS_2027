// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Ui.Superweapon;

/// <summary>
/// Targeting mode for a superweapon (M3-13): clicking a READY button arms it, the next map click issues Special (Subject = the
/// building, Cell = the clicked cell), Escape or a right click cancels. Pure: the caller feeds it clicks and sends the command.
/// </summary>
public sealed class SuperweaponTargeting
{
    private EntityId _armed = EntityId.None;

    /// <summary>The building armed for a shot, or <see cref="EntityId.None"/>.</summary>
    public EntityId Armed => _armed;

    /// <summary>True while a shot is armed (draw the targeting cursor).</summary>
    public bool IsArmed => !_armed.IsNone;

    /// <summary>Click on a button: a ready one arms (clicking the armed one again disarms); anything else is ignored.</summary>
    public void ClickButton(SuperweaponButton button)
    {
        if (button.State != SuperweaponState.Ready) return;
        _armed = _armed == button.Building ? EntityId.None : button.Building;
    }

    /// <summary>Escape or right click.</summary>
    public void Cancel() => _armed = EntityId.None;

    /// <summary>
    /// A left click on the map at <paramref name="cell"/>. Returns the Special command to issue and disarms, or null when nothing is
    /// armed. <paramref name="canFire"/> (optional) vetoes a click the sim would refuse, for example out of entrance range; the
    /// targeting then stays armed so the player can click again.
    /// </summary>
    public Command? ClickMap(byte player, CellCoord cell, Func<EntityId, CellCoord, bool>? canFire = null)
    {
        if (_armed.IsNone) return null;
        if (canFire is not null && !canFire(_armed, cell)) return null;
        EntityId building = _armed;
        _armed = EntityId.None;
        return new Command(0, player, 0, CommandKind.Special, building, EntityId.None, cell, 0);
    }

    /// <summary>Drops the arm if the building is no longer ready (fired, destroyed or the power failed).</summary>
    public void Refresh(IReadOnlyList<SuperweaponButton> buttons)
    {
        if (_armed.IsNone) return;
        foreach (SuperweaponButton b in buttons)
            if (b.Building == _armed && b.State == SuperweaponState.Ready) return;
        _armed = EntityId.None;
    }
}
