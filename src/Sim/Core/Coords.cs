// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>A map cell. Contract type — math helpers live in Rts.Sim.Numerics.</summary>
public readonly record struct CellCoord(int X, int Y);

/// <summary>A position in leptons (256 per cell per axis). Contract type.</summary>
public readonly record struct LeptonCoord(int X, int Y)
{
    public CellCoord ToCell() => new(X >> SimConstants.LeptonShift, Y >> SimConstants.LeptonShift);

    /// <summary>Centre of a cell in leptons.</summary>
    public static LeptonCoord CenterOf(CellCoord c) =>
        new((c.X << SimConstants.LeptonShift) + SimConstants.LeptonsPerCell / 2,
            (c.Y << SimConstants.LeptonShift) + SimConstants.LeptonsPerCell / 2);
}
