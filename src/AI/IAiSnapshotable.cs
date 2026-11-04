// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.AI;

/// <summary>
/// An AI that can write its own memory into a save and read it back (M3-21, D-093 follow-up). Optional: an AI that does not implement it
/// restarts fresh after a load, as before. The driver (<see cref="AiDriver"/>) wraps the bytes with a checksum and the slot's own counters,
/// so an implementation only writes its fields; it must read exactly what it wrote and must not read anything from the simulation.
/// </summary>
public interface IAiSnapshotable
{
    /// <summary>Writes the AI's memory. Called between ticks, after at least one <see cref="IAiPlayer.Think"/>.</summary>
    void SaveState(BinaryWriter w);

    /// <summary>
    /// Reads what <see cref="SaveState"/> wrote. Called on a freshly created AI right after <see cref="IAiPlayer.Start"/> (with the
    /// saved start cell in the context), before the first Think of the loaded game.
    /// </summary>
    void LoadState(BinaryReader r);
}
