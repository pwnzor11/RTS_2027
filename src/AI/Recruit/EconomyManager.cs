// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;

namespace Rts.AI.Recruit;

/// <summary>Economy for the Recruit tier: keeps the free scraper harvesting (it is never replaced, per the design) and
/// answers "is power short?" so the build order can insert a power plant. The sim's harvester only searches 16 cells around
/// its last field, so a scraper whose home patch is used up (or built over) would sit still for ever; when one has not moved
/// for <see cref="StallTicks"/> while on a Harvest mission, it is sent to look for a field at the next probe cell (a coarse
/// grid over the map, nearest first, never next to the enemy start). It reads no overlay data: it just goes and looks.</summary>
internal sealed class EconomyManager
{
    /// <summary>Ticks a Harvest-mission scraper may stand on one cell before it counts as stalled (loading takes 20 x 4 = 80).</summary>
    public const int StallTicks = 400;
    private const int ProbeStep = 8, MinProbeDistance = 10;

    private readonly List<CellCoord> _probes = new();
    private int _probeIdx;
    private readonly Dictionary<ulong, (CellCoord Cell, int Since)> _watch = new();

    /// <summary>Probe orders issued (diagnostic).</summary>
    public int Probes { get; private set; }

    /// <summary>Builds the probe list from the map size and our start (the enemy is assumed at the mirrored start).</summary>
    public void Start(AiContext ctx)
    {
        CellCoord s = ctx.StartCell, enemy = new(ctx.MapWidth - 1 - s.X, ctx.MapHeight - 1 - s.Y);
        var list = new List<(long D, int X, int Y)>();
        for (int y = ProbeStep / 2; y < ctx.MapHeight; y += ProbeStep)
            for (int x = ProbeStep / 2; x < ctx.MapWidth; x += ProbeStep)
            {
                long ds = Sq(x - s.X) + Sq(y - s.Y), de = Sq(x - enemy.X) + Sq(y - enemy.Y);
                if (ds < Sq(MinProbeDistance) || de < Sq(14)) continue;
                list.Add((ds, x, y));
            }
        list.Sort(static (a, b) => a.D != b.D ? a.D.CompareTo(b.D) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        foreach (var (_, x, y) in list) _probes.Add(new CellCoord(x, y));
    }

    private static long Sq(int v) => (long)v * v;

    /// <summary>Re-issues Harvest to a harvester that has gone idle (the sim starts harvesters on its own; this covers a
    /// scraper that was interrupted) and sends a stalled one to the next probe cell.</summary>
    public void Think(RecruitKnowledge k, ICommandSink sink)
    {
        int tick = k.View.Tick;
        foreach (var h in k.Harvesters)
        {
            if (h.Idle && h.Mission != MissionKind.Harvest)
            {
                sink.Issue(CommandKind.Harvest, h.Id, ActorKind.Vehicle);
                continue;
            }
            ulong key = ((ulong)h.Id.Index << 16) | h.Id.Generation;
            if (!_watch.TryGetValue(key, out var w) || w.Cell != h.Cell || h.Mission != MissionKind.Harvest)
            {
                _watch[key] = (h.Cell, tick);
                continue;
            }
            if (tick - w.Since < StallTicks || _probes.Count == 0) continue;
            CellCoord probe = _probes[_probeIdx++ % _probes.Count];
            sink.Issue(CommandKind.Harvest, h.Id, ActorKind.Vehicle, probe);
            Probes++;
            _watch[key] = (h.Cell, tick);
        }
    }

    /// <summary>True if building something that drains <paramref name="extraDrain"/> power would leave use above output.</summary>
    public static bool PowerShort(RecruitKnowledge k, int extraDrain) =>
        k.Hud.PowerUse + extraDrain > k.Hud.PowerOut;
}
