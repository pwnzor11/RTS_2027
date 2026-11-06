// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Scripting;

/// <summary>Kinds of queued reinforcement work (persisted as a byte; append only).</summary>
internal enum ArrivalKind : byte
{
    /// <summary>Reinforce.Delayed: spawn the units like Reinforce.Edge when <see cref="PendingArrival.Tick"/> is reached.</summary>
    Delayed = 0,

    /// <summary>Reinforce.Transport: the transport <see cref="PendingArrival.Tag"/> unloads once it is next to the destination (or at the deadline tick).</summary>
    TransportDrop = 1,
}

/// <summary>One queued reinforcement step, saved in the version-4 tail of the "mission" part (M8-04).</summary>
internal sealed class PendingArrival
{
    /// <summary>What to do.</summary>
    public ArrivalKind Kind;

    /// <summary>Delayed: the tick at which the units arrive. TransportDrop: the tick at which the transport unloads wherever it is.</summary>
    public int Tick;

    /// <summary>Owner slot.</summary>
    public int Player;

    /// <summary>TransportDrop: tag of the transport. Delayed: unused.</summary>
    public string Tag = "";

    /// <summary>The map-edge cell the units come in at (Delayed).</summary>
    public CellCoord Edge;

    /// <summary>Where the units go (Delayed) or where the transport unloads (TransportDrop).</summary>
    public CellCoord Dest;

    /// <summary>Delayed: the type ids to spawn, in order.</summary>
    public List<string> Types = new();
}
