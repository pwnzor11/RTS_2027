// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Controls;

/// <summary>The mouse cursor shown over the map: what a right-click here would do (M11-10e, audit F2 and O7). Append only.</summary>
public enum CursorKind
{
    /// <summary>Plain arrow: nothing selected or nothing to do here.</summary>
    Select,
    /// <summary>Over one of your own units or buildings (a left click selects it).</summary>
    Hover,
    /// <summary>Right-click moves the selection here.</summary>
    Move,
    /// <summary>Right-click attacks the enemy under the cursor.</summary>
    Attack,
    /// <summary>Attack-move is armed (A): the next click attack-moves here.</summary>
    AttackMove,
    /// <summary>Right-click boards a transport or enters a tunnel entrance.</summary>
    Enter,
    /// <summary>An Engineer captures the building under the cursor.</summary>
    Capture,
    /// <summary>An Engineer repairs the damaged own building under the cursor.</summary>
    Repair,
    /// <summary>Infantry garrisons the building under the cursor.</summary>
    Garrison,
    /// <summary>A salvager takes the wreck under the cursor.</summary>
    Salvage,
    /// <summary>A harvester works the ash field under the cursor.</summary>
    Harvest,
    /// <summary>A landing craft puts its passengers ashore at this cell.</summary>
    Unload,
    /// <summary>Deploy is armed (D, a Bore): the next click picks the cell to deploy at.</summary>
    DeployAt,
    /// <summary>The selection cannot go to this cell.</summary>
    Blocked,
}
