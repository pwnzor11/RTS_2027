// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// M3-11 (flag Superweapons): fires every superweapon that is charged and powered. The target is the known enemy structure (live or
/// last seen) with the most known enemy structures within the blast radius, counting only targets the weapon can reach: when the weapon
/// needs an entrance (Range above 0) one of our finished entrances must lie within Range cells of the target. Enemy structures near our own
/// are not avoided because the blast spares friendly actors. A target needs at least <see cref="MinTargets"/> structures in the blast.
/// </summary>
internal sealed class SuperweaponManager
{
    /// <summary>Blast radius in cells the target scoring assumes (the sim's constant is internal; this matches design 05).</summary>
    public const int BlastCells = 5;   // PENDING M3-21: expose blast radius in view
    /// <summary>Structures that must stand in the blast for it to be worth firing.</summary>
    public const int MinTargets = 1;

    private readonly List<TelegraphView> _telegraphs = new();

    /// <summary>Special commands issued.</summary>
    public int Fired { get; private set; }

    public void Think(RecruitKnowledge k, ICommandSink sink, List<ViewActor> knownEnemyBuildings)
    {
        if (k.Hud.Superweapons.Count == 0 || knownEnemyBuildings.Count == 0) return;
        k.View.CopyTelegraphs(_telegraphs);
        foreach (SuperweaponView sw in k.Hud.Superweapons)
        {
            if (!sw.Ready || sw.Paused) continue;
            ViewActor? best = null;
            int bestScore = MinTargets - 1;
            foreach (ViewActor t in knownEnemyBuildings)
            {
                if (sw.Range > 0 && !EntranceNear(k, t.Cell, sw.Range)) continue;
                int score = 0;
                foreach (ViewActor o in knownEnemyBuildings)
                    if (Cheb(o.Cell, t.Cell) <= BlastCells) score++;
                if (score > bestScore) { bestScore = score; best = t; }
            }
            if (best is not ViewActor target) continue;
            Fired++;
            sink.Issue(CommandKind.Special, sw.Building, ActorKind.Building, target.Cell);
        }
    }

    /// <summary>True if a finished entrance of ours (not one still surfacing) is within <paramref name="range"/> cells of <paramref name="cell"/>.</summary>
    private bool EntranceNear(RecruitKnowledge k, CellCoord cell, int range)
    {
        foreach (ViewActor b in k.Buildings)
        {
            BuildingType bt = k.Rules.Buildings[b.TypeIndex];
            if (bt.Entrance == TunnelSize.None || Emerging(b)) continue;
            int dx = Math.Max(Math.Max(b.Cell.X - cell.X, 0), cell.X - (b.Cell.X + bt.FootprintW - 1));
            int dy = Math.Max(Math.Max(b.Cell.Y - cell.Y, 0), cell.Y - (b.Cell.Y + bt.FootprintH - 1));
            if (Math.Max(dx, dy) <= range) return true;
        }
        return false;
    }

    private bool Emerging(ViewActor b)
    {
        foreach (TelegraphView t in _telegraphs)
            if (t.Kind == TelegraphKind.Entrance && t.Cell == b.Cell) return true;
        return false;
    }

    private static int Cheb(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
