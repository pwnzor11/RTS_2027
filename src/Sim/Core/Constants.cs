// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>Global simulation constants (design/01_architecture.md §3). Integer only.</summary>
public static class SimConstants
{
    public const int TicksPerSecond = 20;
    public const int LeptonsPerCell = 256;
    public const int LeptonShift = 8;          // cell = lepton >> 8
    public const int HashIntervalTicks = 16;
    public const int HashRingSize = 64;
    public const int MaxMapCells = 128;
    /// <summary>M4-17 (D-173): ticks without a production structure, founder or harvester after which a skirmish player is defeated (2 minutes).</summary>
    public const int NoRebuildDefeatTicks = TicksPerSecond * 120;
}
