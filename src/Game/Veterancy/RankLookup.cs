// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.View;

namespace Rts.Game.Veterancy;

/// <summary>
/// Collects the veterancy rank of every vehicle and soldier the viewer can list (own units, and enemies and allies while in vision),
/// read from the player view only, so it is fog-honest. Buildings carry no rank and are skipped.
/// </summary>
public static class RankLookup
{
    /// <summary>Fills <paramref name="dest"/> (cleared first) with one entry per listed vehicle or soldier, in view order.</summary>
    /// <param name="view">The viewing player's view, read between Steps.</param>
    /// <param name="scratch">A reusable list for the view copies (cleared by this call).</param>
    /// <param name="dest">The output list.</param>
    public static void Fill(PlayerView view, List<ViewActor> scratch, List<(RankKey Key, int Rank)> dest)
    {
        dest.Clear();
        view.CopyOwn(scratch, includeBuildings: false);
        Add(scratch, dest);
        view.CopyVisible(scratch);
        Add(scratch, dest);
    }

    private static void Add(List<ViewActor> actors, List<(RankKey Key, int Rank)> dest)
    {
        foreach (ViewActor a in actors)
        {
            if (!a.IsMobile) continue;
            UnitKind kind = a.Kind == ActorKind.Vehicle ? UnitKind.Vehicle : UnitKind.Infantry;
            dest.Add((new RankKey(kind, a.Id), a.Rank));
        }
    }
}
