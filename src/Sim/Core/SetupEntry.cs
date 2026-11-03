// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>
/// One out-of-Step change to the sim (a scenario spawn or placement), logged by the Simulation in order
/// (M1.5, review A-3). Replays rebuild the start state from this log instead of callers re-recording spawns.
/// Kind = Vehicle / Infantry / Building; SubSlot is used by infantry only.
/// </summary>
public readonly record struct SetupEntry(ActorKind Kind, byte Player, int TypeIndex, CellCoord Cell, int SubSlot);
