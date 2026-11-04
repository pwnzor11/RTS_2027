// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Tunnels;

/// <summary>
/// A surfacing telegraph in progress (design 05 §1, D-075): Cell = the cell it sits on; Owner = the player surfacing;
/// TicksLeft = ticks until the unit appears / the entrance is finished; Kind = unit or entrance; Subject = the surfacing
/// unit (Kind Unit) or the emerging building (Kind Entrance, which already exists with low HP).
/// </summary>
public struct Telegraph
{
    /// <summary>Cell the telegraph sits on (for an entrance: its top-left cell).</summary>
    public CellCoord Cell;
    /// <summary>The surfacing player.</summary>
    public byte Owner;
    /// <summary>Ticks left until it completes.</summary>
    public int TicksLeft;
    /// <summary>Unit or entrance.</summary>
    public TelegraphKind Kind;
    /// <summary>Surfacing unit, or the emerging building.</summary>
    public ActorRef Subject;
}
