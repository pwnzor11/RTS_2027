// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// S19 M13-12 (owner option a): ashfall and pitchblende fields hurt soldiers standing on them (D-207), so the cells where the AI parks its
/// infantry (factory rally, wave staging) move to the nearest passable cell without a field. Rings are scanned outward, each ring in a
/// fixed order (row by row), so the choice is deterministic. Map data only: fields are terrain the player sees.
/// </summary>
public static class AshCells
{
    /// <summary>How far (Chebyshev cells) the search looks before giving up and keeping the cell.</summary>
    public const int SearchRadius = 8;

    /// <summary>True when the cell holds an ashfall or pitchblende field.</summary>
    public static bool IsField(PlayerView view, CellCoord c) => view.InMap(c) && OverlayKinds.IsResource(view.OverlayAt(c));

    /// <summary>The cell itself when it is off any field, else the nearest passable (Foot and <paramref name="loco"/>) cell off every
    /// field within <see cref="SearchRadius"/>; the cell itself when there is none.</summary>
    public static CellCoord OffField(PlayerView view, CellCoord cell, Locomotion loco = Locomotion.Tracked)
    {
        if (!IsField(view, cell)) return cell;
        for (int r = 1; r <= SearchRadius; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var c = new CellCoord(cell.X + dx, cell.Y + dy);
                    if (!view.InMap(c) || IsField(view, c)) continue;
                    if (view.IsPassable(c, Locomotion.Foot) && view.IsPassable(c, loco)) return c;
                }
        return cell;
    }
}
