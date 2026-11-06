// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.AI;

/// <summary>
/// M8-06: what a mission script has asked of one AI slot (<c>AI.*</c>), with zones already resolved to centre cells by the host adapter.
/// A plain value read once per tick by <see cref="AiDriver"/>; the AI never writes it (D-051).
/// </summary>
/// <param name="AttackCell">Centre cell waves go to until cleared, or null.</param>
/// <param name="DefendCell">Centre cell of the zone to defend (rally point, waves held), or null.</param>
/// <param name="Tier">Difficulty tier 1..5 forced by the script, or 0 for none.</param>
/// <param name="WaveSeq">Counts AI.Wave calls; the AI launches one scripted wave each time this rises above what it served.</param>
/// <param name="WaveId">Script-chosen id of the latest scripted wave (informational).</param>
/// <param name="WaveCell">Centre cell the latest scripted wave attacks, or null.</param>
public readonly record struct AiHooks(CellCoord? AttackCell, CellCoord? DefendCell, int Tier, int WaveSeq, string WaveId, CellCoord? WaveCell)
{
    /// <summary>No hooks set.</summary>
    public static AiHooks None { get; } = new(null, null, 0, 0, "", null);
}
